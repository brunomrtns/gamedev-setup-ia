using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Studio.Tests.PlayMode
{
    /// <summary>
    /// Reproduz o caminho de runtime do PedestrianSystem com os prefabs
    /// Quaternius (esqueleto humanóide limpo): instancia quat_*, atribui
    /// Pedestrian.controller e verifica se os ossos realmente se movem.
    /// Nenhum rebind é necessário — SMR já aponta pro esqueleto do avatar.
    /// </summary>
    public class PedestrianAnimPlayModeTests
    {
        private static GameObject LoadPedPrefab()
        {
            var go = Resources.Load<GameObject>("Art/Pedestrians/quat_male_a");
            if (go == null)
                go = Resources.Load<GameObject>(
                    "Art/Pedestrians/npc_csl_00_character_01f_01_baked");
            return go;
        }

        [UnityTest]
        public IEnumerator Pedestrian_Animates_LegsMove()
        {
            var prefab = LoadPedPrefab();
            Assert.NotNull(prefab, "prefab de pedestre não encontrado");
            var go = Object.Instantiate(prefab);

            var ctrl = Resources.Load<RuntimeAnimatorController>(
                "Art/Pedestrians/Pedestrian");
            Assert.NotNull(ctrl, "Pedestrian.controller não carregou via Resources");

            var anim = go.GetComponentInChildren<Animator>();
            Assert.NotNull(anim, "Animator ausente no prefab");
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            Debug.Log($"[Test] avatar={(anim.avatar ? anim.avatar.name : "NULL")} " +
                $"isHuman={anim.isHuman} ctrl={anim.runtimeAnimatorController?.name}");

            anim.SetFloat("Speed", 0.6f); // -> Walk
            yield return null; yield return null;

            var leg = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            Assert.NotNull(leg, "osso LeftUpperLeg não mapeado no avatar");

            // osso que a MALHA usa (SMR) — é isso que o usuário vê
            var smr = go.GetComponentsInChildren<SkinnedMeshRenderer>()
                .OrderByDescending(r => r.bones.Length).First();
            // prova que a MALHA se move: qualquer osso do SMR deve girar
            var bones = smr.bones.Where(b => b != null).ToArray();
            var baseRot = bones.Select(b => b.rotation).ToArray();
            Debug.Log($"[Test] smr={smr.name} bones={bones.Length} " +
                $"meshContemLeg={(bones.Contains(leg))}");

            var r0 = leg.rotation;
            float moved = 0f, meshMoved = 0f;
            for (int i = 0; i < 40; i++)
            {
                anim.Update(1f / 30f); // tick determinístico do Animator
                moved = Mathf.Max(moved,
                    Quaternion.Angle(r0, leg.rotation));
                for (int b = 0; b < bones.Length; b++)
                    meshMoved = Mathf.Max(meshMoved,
                        Quaternion.Angle(baseRot[b], bones[b].rotation));
                yield return null;
            }
            var info = anim.GetCurrentAnimatorStateInfo(0);
            Debug.Log($"[Test] stateHash={info.shortNameHash} " +
                $"len={info.length:F2} normT={info.normalizedTime:F2} " +
                $"maxLegDelta={moved:F1}° meshDelta={meshMoved:F1}°");
            Assert.Greater(moved, 5f,
                "osso da perna (avatar) não moveu — Animator não anima");
            Assert.Greater(meshMoved, 3f,
                "osso da perna (SMR) não moveu — malha travada");
            // sem Destroy manual — play-mode limpa objetos ao sair
        }

        /// <summary>
        /// Prova visual de runtime: mesmo fluxo do PedestrianSystem, mas
        /// renderiza 2 frames do ciclo de walk pra PNG — o que o usuário vê.
        /// </summary>
        [UnityTest]
        public IEnumerator Pedestrian_Render_WalkFrames()
        {
            var prefab = LoadPedPrefab();
            Assert.NotNull(prefab);
            var go = Object.Instantiate(prefab);
            go.transform.position = Vector3.zero;
            var ctrl = Resources.Load<RuntimeAnimatorController>(
                "Art/Pedestrians/Pedestrian");
            var anim = go.GetComponentInChildren<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            anim.SetFloat("Speed", 0.6f);

            // chão + luz
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(30f, 1f, 30f);
            var light = new GameObject("L").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.6f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.55f);

            var camGo = new GameObject("Cam");
            var cam = camGo.AddComponent<Camera>();
            cam.transform.position = new Vector3(2.7f, 1.4f, 2.7f);
            cam.transform.LookAt(new Vector3(0f, 0.8f, 0f));

            for (int i = 0; i < 40; i++) { anim.Update(1f/30f); yield return null; }
            SaveFrame(cam, "quaternius_pedestrian_walk.png");
            for (int i = 0; i < 18; i++) { anim.Update(1f/30f); yield return null; }
            SaveFrame(cam, "ped_runtime_walk_b.png");
            // objetos de play se autodestroem ao sair do play mode
        }

        private static void SaveFrame(Camera cam, string file)
        {
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            System.IO.File.WriteAllBytes(
                System.IO.Path.Combine("../evidence/captures", file),
                tex.EncodeToPNG());
            Debug.Log("[Test] frame salvo: " + file);
        }
    }
}
