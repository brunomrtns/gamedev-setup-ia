using System;
using System.Collections.Generic;
using System.IO;
using Studio.Cutscenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Studio.Editor
{
    /// <summary>
    /// CutsceneBuilder — Gera cutscenes completas (Unity TimelineAsset + Prefab com
    /// PlayableDirector + AudioTrack sincronizado ao vo_manifest.json do XTTS-v2 +
    /// camera cinematica + capturas de keyframes) a partir de um JSON declarativo.
    /// Studio V2 — Fase 3.
    /// </summary>
    public static class CutsceneBuilder
    {
        private const string CutscenesRoot = "Assets/Game/Cutscenes";

        public const string ExpectedSchema = "cutscene-spec-v1";

        [Serializable]
        public class CutsceneSpec
        {
            public string schema;
            public string cutscene_id;
            public string scene_path;
            public string vo_manifest_path;
            public List<ShotSpec> shots = new List<ShotSpec>();
        }

        [Serializable]
        public class ShotSpec
        {
            public string id;
            public float duration_sec;
            public float pad_after_sec = 0.35f;
            public string vo_line_id;
            public float[] camera_from;
            public float[] camera_to;
            public float[] look_at;
            public float fov = 45f;
            public string subtitle_speaker;
            public string subtitle_text;
        }

        [Serializable]
        public class VoManifest
        {
            public string scene;
            public List<VoLine> lines = new List<VoLine>();
        }

        [Serializable]
        public class VoLine
        {
            public string id;
            public string character;
            public string display_name;
            public string text;
            public string wav_file;
            public float duration_sec;
        }

        [Serializable]
        public class BuildReport
        {
            public string generatedAt = DateTime.UtcNow.ToString("o");
            public string cutscene_id;
            public float total_duration_sec;
            public int shot_count;
            public int audio_clips_bound;
            public string timeline_asset;
            public string prefab_asset;
            public List<string> keyframes = new List<string>();
            public List<string> errors = new List<string>();
        }

        /// <summary>
        /// Batchmode:
        /// Unity -batchmode -quit -projectPath unity -executeMethod Studio.Editor.CutsceneBuilder.RunBuildBatch
        ///       -cutsceneJson=&lt;caminho&gt; [-captureKeyframes=true]
        /// </summary>
        public static void RunBuildBatch()
        {
            string jsonPath = null;
            bool captureKeyframes = false;
            bool saveAssets = true;

            foreach (var arg in Environment.GetCommandLineArgs())
            {
                if (arg.StartsWith("-cutsceneJson="))
                    jsonPath = arg.Substring("-cutsceneJson=".Length);
                else if (arg.StartsWith("-captureKeyframes="))
                    captureKeyframes = arg.Substring("-captureKeyframes=".Length).Equals("true", StringComparison.OrdinalIgnoreCase);
                else if (arg.StartsWith("-dryRunAssets="))
                    saveAssets = !arg.Substring("-dryRunAssets=".Length).Equals("true", StringComparison.OrdinalIgnoreCase);
            }

            if (string.IsNullOrEmpty(jsonPath) || !File.Exists(jsonPath))
            {
                Debug.LogError("[CutsceneBuilder] -cutsceneJson ausente ou arquivo nao encontrado: " + jsonPath);
                EditorApplication.Exit(1);
                return;
            }

            var report = BuildFromJson(jsonPath, captureKeyframes, saveAssets);
            Debug.Log($"[CutsceneBuilder] Concluido: id={report.cutscene_id} shots={report.shot_count} dur={report.total_duration_sec:F2}s audio={report.audio_clips_bound} keyframes={report.keyframes.Count} erros={report.errors.Count}");
            EditorApplication.Exit(report.errors.Count == 0 ? 0 : 1);
        }

        public static BuildReport BuildFromJson(string jsonPath, bool captureKeyframes, bool saveAssets = true)
        {
            var report = new BuildReport();
            try
            {
                var spec = JsonUtility.FromJson<CutsceneSpec>(File.ReadAllText(jsonPath));
                if (spec == null || string.IsNullOrEmpty(spec.cutscene_id) || spec.shots == null || spec.shots.Count == 0)
                {
                    report.errors.Add("CutsceneSpec invalido: cutscene_id ou shots ausentes");
                    WriteReport(report, "invalid");
                    return report;
                }
                if (!string.IsNullOrEmpty(spec.schema) && spec.schema != ExpectedSchema)
                {
                    report.errors.Add($"Schema incompatível em CutsceneSpec: esperado '{ExpectedSchema}', recebido '{spec.schema}'");
                    WriteReport(report, spec.cutscene_id);
                    return report;
                }
                if (string.IsNullOrEmpty(spec.schema))
                {
                    report.errors.Add($"Campo 'schema' ausente em CutsceneSpec (esperado '{ExpectedSchema}')");
                    WriteReport(report, spec.cutscene_id);
                    return report;
                }
                report.cutscene_id = spec.cutscene_id;

                // Carrega vo_manifest.json opcional (da Fase 2 — tts_bridge.py)
                var voMap = new Dictionary<string, VoLine>();
                string voFolder = null;
                if (!string.IsNullOrEmpty(spec.vo_manifest_path) && File.Exists(spec.vo_manifest_path))
                {
                    var voManifest = JsonUtility.FromJson<VoManifest>(File.ReadAllText(spec.vo_manifest_path));
                    voFolder = Path.GetDirectoryName(spec.vo_manifest_path).Replace('\\', '/');
                    if (voManifest?.lines != null)
                    {
                        foreach (var l in voManifest.lines)
                        {
                            if (!string.IsNullOrEmpty(l.id)) voMap[l.id] = l;
                        }
                    }
                }

                // Monta lista cronologica de shots
                var runtimeShots = new List<CutsceneShotData>();
                float cursor = 0f;
                foreach (var s in spec.shots)
                {
                    VoLine vo = (!string.IsNullOrEmpty(s.vo_line_id) && voMap.TryGetValue(s.vo_line_id, out var found)) ? found : null;
                    float dur = s.duration_sec;
                    if (vo != null)
                    {
                        dur = Mathf.Max(dur, vo.duration_sec + Mathf.Max(0f, s.pad_after_sec));
                    }
                    if (dur <= 0.1f) dur = 2.5f;

                    var shot = new CutsceneShotData
                    {
                        id = string.IsNullOrEmpty(s.id) ? $"shot_{runtimeShots.Count + 1:00}" : s.id,
                        startTime = cursor,
                        duration = dur,
                        cameraFrom = ToVec3(s.camera_from, new Vector3(0, 3, -6)),
                        cameraTo = ToVec3(s.camera_to, ToVec3(s.camera_from, new Vector3(0, 3, -6))),
                        lookAt = ToVec3(s.look_at, Vector3.zero),
                        fov = s.fov > 5f ? s.fov : 45f,
                        speaker = !string.IsNullOrEmpty(s.subtitle_speaker) ? s.subtitle_speaker : (vo?.display_name ?? ""),
                        subtitle = !string.IsNullOrEmpty(s.subtitle_text) ? s.subtitle_text : (vo?.text ?? ""),
                        voClipPath = (vo != null && !string.IsNullOrEmpty(voFolder)) ? $"{voFolder}/{vo.wav_file}" : ""
                    };
                    runtimeShots.Add(shot);
                    cursor += dur;
                }

                report.shot_count = runtimeShots.Count;
                report.total_duration_sec = cursor;

                if (saveAssets)
                {
                    AssetDatabase.Refresh();
                    EnsureFolder("Assets/Game");
                    EnsureFolder(CutscenesRoot);
                    string outDir = $"{CutscenesRoot}/{spec.cutscene_id}";
                    EnsureFolder(outDir);

                    string timelinePath = $"{outDir}/{spec.cutscene_id}.playable";
                    var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
                    AssetDatabase.CreateAsset(timeline, timelinePath);

                    var audioTrack = timeline.CreateTrack<AudioTrack>(null, "VO_Audio");
                    foreach (var shot in runtimeShots)
                    {
                        if (string.IsNullOrEmpty(shot.voClipPath)) continue;
                        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(shot.voClipPath);
                        if (clip == null)
                        {
                            Debug.LogWarning($"[CutsceneBuilder] AudioClip nao encontrado no AssetDatabase: {shot.voClipPath}");
                            continue;
                        }
                        var tClip = audioTrack.CreateClip(clip);
                        tClip.start = shot.startTime;
                        tClip.duration = clip.length;
                        tClip.displayName = shot.id;
                        report.audio_clips_bound++;
                    }
                    EditorUtility.SetDirty(timeline);

                    // Prefab com PlayableDirector + AudioSource + CutscenePlayer + Camera
                    var rootGo = new GameObject($"Cutscene_{spec.cutscene_id}");
                    var audioSrc = rootGo.AddComponent<AudioSource>();
                    audioSrc.playOnAwake = false;
                    audioSrc.spatialBlend = 0f;

                    var director = rootGo.AddComponent<PlayableDirector>();
                    director.playableAsset = timeline;
                    director.playOnAwake = false;
                    director.SetGenericBinding(audioTrack, audioSrc);

                    var player = rootGo.AddComponent<CutscenePlayer>();
                    player.cutsceneId = spec.cutscene_id;
                    player.totalDuration = cursor;
                    player.shots = runtimeShots;

                    var camGo = new GameObject("CutsceneCamera");
                    camGo.transform.SetParent(rootGo.transform, false);
                    var cam = camGo.AddComponent<Camera>();
                    cam.enabled = false;
                    if (runtimeShots.Count > 0)
                    {
                        cam.transform.position = runtimeShots[0].cameraFrom;
                        cam.transform.LookAt(runtimeShots[0].lookAt);
                        cam.fieldOfView = runtimeShots[0].fov;
                    }

                    string prefabPath = $"{outDir}/{spec.cutscene_id}.prefab";
                    PrefabUtility.SaveAsPrefabAsset(rootGo, prefabPath);
                    UnityEngine.Object.DestroyImmediate(rootGo);
                    AssetDatabase.SaveAssets();

                    report.timeline_asset = timelinePath;
                    report.prefab_asset = prefabPath;
                }

                if (captureKeyframes && !string.IsNullOrEmpty(spec.scene_path) && File.Exists(spec.scene_path))
                {
                    CaptureKeyframes(spec, runtimeShots, report);
                }
            }
            catch (Exception e)
            {
                report.errors.Add(e.Message);
            }

            WriteReport(report, string.IsNullOrEmpty(report.cutscene_id) ? "unknown" : report.cutscene_id);
            return report;
        }

        private static void CaptureKeyframes(CutsceneSpec spec, List<CutsceneShotData> shots, BuildReport report)
        {
            EditorSceneManager.OpenScene(spec.scene_path, OpenSceneMode.Single);
            var cam = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (cam == null)
            {
                cam = new GameObject("CutsceneCaptureCam").AddComponent<Camera>();
            }

            // Warm-up URP (3 frames)
            for (int w = 0; w < 3; w++)
            {
                var wrt = RenderTexture.GetTemporary(256, 144, 24);
                cam.targetTexture = wrt;
                cam.Render();
                cam.targetTexture = null;
                RenderTexture.ReleaseTemporary(wrt);
            }

            string outDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "evidence", "captures");
            Directory.CreateDirectory(outDir);

            int width = 1280, height = 720;
            foreach (var shot in shots)
            {
                cam.transform.position = Vector3.Lerp(shot.cameraFrom, shot.cameraTo, 0.5f);
                cam.transform.LookAt(shot.lookAt);
                cam.fieldOfView = shot.fov;

                var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                cam.targetTexture = null;
                RenderTexture.active = null;
                UnityEngine.Object.DestroyImmediate(rt);

                string fileName = $"cutscene_{spec.cutscene_id}_{shot.id}.png";
                string outPath = Path.Combine(outDir, fileName);
                File.WriteAllBytes(outPath, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                report.keyframes.Add($"evidence/captures/{fileName}");
            }
        }

        private static Vector3 ToVec3(float[] arr, Vector3 fallback)
        {
            if (arr != null && arr.Length >= 3)
                return new Vector3(arr[0], arr[1], arr[2]);
            return fallback;
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void WriteReport(BuildReport report, string id)
        {
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "..", "evidence", "cutscenes");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, $"cutscene_{id}.json"), JsonUtility.ToJson(report, true));
        }
    }
}
