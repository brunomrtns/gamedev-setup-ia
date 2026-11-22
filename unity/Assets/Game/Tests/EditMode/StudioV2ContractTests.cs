using System.IO;
using NUnit.Framework;
using Studio.Cutscenes;
using Studio.Editor;
using Studio.Gameplay;
using Studio.UI;
using UnityEngine;

namespace Studio.Tests.EditMode
{
    /// <summary>
    /// Testes de contrato Python <-> C# do Studio V2 (Auditoria P0-3 + P0-1 + P1-8).
    /// Garante que mudancas de schema falhem alto em vez de zerar campos silenciosamente.
    /// </summary>
    public class StudioV2ContractTests
    {
        [Test]
        public void CutsceneBuilder_RejectsMissingSchemaLoudly()
        {
            string tmp = Path.Combine(Path.GetTempPath(), "cutscene_missing_schema.json");
            File.WriteAllText(tmp, "{\"cutscene_id\":\"bad_test\",\"shots\":[{\"id\":\"s1\",\"duration_sec\":2.0}]}");
            try
            {
                var report = CutsceneBuilder.BuildFromJson(tmp, captureKeyframes: false, saveAssets: false);
                Assert.IsNotEmpty(report.errors, "CutsceneSpec sem campo 'schema' deve falhar explicitamente.");
            }
            finally
            {
                if (File.Exists(tmp)) File.Delete(tmp);
            }
        }

        [Test]
        public void CutsceneBuilder_RejectsWrongSchemaVersion()
        {
            string tmp = Path.Combine(Path.GetTempPath(), "cutscene_wrong_schema.json");
            File.WriteAllText(tmp, "{\"schema\":\"cutscene-spec-v99\",\"cutscene_id\":\"bad_ver\",\"shots\":[{\"id\":\"s1\",\"duration_sec\":2.0}]}");
            try
            {
                var report = CutsceneBuilder.BuildFromJson(tmp, captureKeyframes: false, saveAssets: false);
                Assert.IsNotEmpty(report.errors, "Schema incompatível deve gerar erro explícito.");
            }
            finally
            {
                if (File.Exists(tmp)) File.Delete(tmp);
            }
        }

        [Test]
        public void CutsceneBuilder_AcceptsValidSchemaAndCalculatesDuration()
        {
            string tmp = Path.Combine(Path.GetTempPath(), "cutscene_valid_schema.json");
            File.WriteAllText(tmp,
                "{\"schema\":\"cutscene-spec-v1\",\"cutscene_id\":\"valid_test\"," +
                "\"shots\":[{\"id\":\"s1\",\"duration_sec\":2.5,\"camera_from\":[0,2,5],\"camera_to\":[0,2,3],\"look_at\":[0,1,0],\"fov\":42}," +
                "{\"id\":\"s2\",\"duration_sec\":3.5,\"camera_from\":[1,2,4],\"camera_to\":[1,2,2],\"look_at\":[0,1,0],\"fov\":38}]}");
            try
            {
                var report = CutsceneBuilder.BuildFromJson(tmp, captureKeyframes: false, saveAssets: false);
                Assert.IsEmpty(report.errors, "Spec valido nao deve ter erros.");
                Assert.AreEqual(2, report.shot_count);
                Assert.AreEqual(6.0f, report.total_duration_sec, 0.01f);
            }
            finally
            {
                if (File.Exists(tmp)) File.Delete(tmp);
            }
        }

        [Test]
        public void DialoguePresenter_ParsesPackageAndResolvesBilingualSubtitles()
        {
            string json = "{\"schema\":\"vo-manifest-v1\",\"scene\":\"cs01\"," +
                          "\"lines\":[{\"id\":\"cs01_01\",\"character\":\"dez\",\"display_name\":\"Dez\"," +
                          "\"text\":\"Texto base\",\"subtitle_pt\":\"Eu conserto isso.\",\"subtitle_en\":\"I fix this.\",\"duration_sec\":2.5}]}";
            var pkg = DialoguePresenter.ParsePackage(json);
            Assert.IsNotNull(pkg);
            Assert.AreEqual(1, pkg.lines.Count);

            var go = new GameObject("TestDialogue");
            try
            {
                var presenter = go.AddComponent<DialoguePresenter>();
                presenter.activeLanguage = "pt-BR";
                Assert.AreEqual("Eu conserto isso.", presenter.ResolveSubtitle(pkg.lines[0]));
                presenter.activeLanguage = "en";
                Assert.AreEqual("I fix this.", presenter.ResolveSubtitle(pkg.lines[0]));

                string startedText = null;
                presenter.OnLineStarted += (_, txt) => startedText = txt;
                presenter.EnqueueLine(pkg.lines[0]);
                Assert.IsTrue(presenter.IsSpeaking);
                Assert.AreEqual("I fix this.", startedText);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void InteractionSystem_TriggersInteractableAndExposesIkTarget()
        {
            var obj = new GameObject("SpringLever");
            var player = new GameObject("Dez");
            try
            {
                var interactable = obj.AddComponent<InteractableBase>();
                bool triggered = false;
                interactable.OnInteracted += who => triggered = (who == player);

                Assert.IsTrue(interactable.CanInteract(player));
                Assert.IsNotNull(interactable.IkHandTarget);
                interactable.Interact(player);
                Assert.IsTrue(triggered);
            }
            finally
            {
                Object.DestroyImmediate(obj);
                Object.DestroyImmediate(player);
            }
        }
    }
}
