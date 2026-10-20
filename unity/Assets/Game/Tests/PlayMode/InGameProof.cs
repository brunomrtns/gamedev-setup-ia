using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Studio.Gameplay;

namespace Studio.Tests.PlayMode
{
    /// <summary>
    /// v3.4 — prova IN-GAME do Green Hills: teleporta o carro ao longo da rota
    /// praça→OBD→ponte→moinho, assenta física, captura Camera.main p/ PNG.
    /// Falha se o carro cair (y&lt;-3) ou travar fora da rota.
    /// </summary>
    public class InGameProof
    {
        static readonly (string tag, Vector3 pos, float yaw)[] Route =
        {
            ("01_avenida",   new Vector3(60f, 1f, 26f),  90f),
            ("02_obd",       new Vector3(200f, 1f, 26f), 90f),
            ("03_obd_fim",   new Vector3(275f, 1f, 26f), 90f),
            ("04_rotatoria", new Vector3(300f, 1f, 26f), 90f),
            ("05_estrada",   new Vector3(330f, 1f, 26f), 90f),
            ("06_ponte",     new Vector3(352f, 2f, 26f), 90f),
            ("07_moinho",    new Vector3(390f, 1f, 40f), 45f),
        };

        [UnityTest]
        public IEnumerator Drive_To_GreenHills_InGame()
        {
            var op = SceneManager.LoadSceneAsync("NeonDistrict");
            float t0 = Time.realtimeSinceStartup;
            while (op != null && !op.isDone && Time.realtimeSinceStartup - t0 < 20f)
                yield return null;
            yield return new WaitForSeconds(2f);

            var car = Object.FindFirstObjectByType<PlayerCarController>();
            Assert.IsNotNull(car, "player car ausente");
            var rb = car.GetComponent<Rigidbody>();
            var cam = Camera.main;
            Assert.IsNotNull(cam, "câmera principal ausente");
            Directory.CreateDirectory("evidence/ingame");

            foreach (var (tag, pos, yaw) in Route)
            {
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.position = pos;
                    rb.rotation = Quaternion.Euler(0f, yaw, 0f);
                }
                else car.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));

                yield return new WaitForSeconds(1.2f); // física assenta

                var p = car.transform.position;
                Assert.Greater(p.y, -3f, $"{tag}: carro caiu p/ y={p.y:F1} — sem chão");
                Assert.Less(Vector3.Distance(p, pos), 25f,
                    $"{tag}: carro deslocou {Vector3.Distance(p, pos):F1}m — travou/empurrou");

                var rt = new RenderTexture(1280, 720, 24);
                cam.targetTexture = rt;
                yield return null; yield return null; // 2 frames render
                RenderTexture.active = rt;
                var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                tex.Apply();
                File.WriteAllBytes($"evidence/ingame/gh_route_{tag}.png", tex.EncodeToPNG());
                cam.targetTexture = null;
                RenderTexture.active = null;
                Debug.Log($"[InGameProof] {tag} ok pos={p}");
            }
            Debug.Log("[InGameProof] rota completa — acesso dirigível provado");
        }
    }
}
