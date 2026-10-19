using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using Studio.Gameplay;

namespace Studio.Tests.PlayMode
{
    /// <summary>
    /// QA autônomo: abre a cena, anexa o AutonomousPlaytester ao player car,
    /// roda N segundos simulados, gera relatório em evidence/qa/.
    /// </summary>
    public class AutonomousPlaytest
    {
        [UnityTest]
        public IEnumerator Bot_Drives_And_Detects_Anomalies()
        {
            var op = SceneManager.LoadSceneAsync("NeonDistrict");
            float t0 = Time.realtimeSinceStartup;
            while (op != null && !op.isDone
                && Time.realtimeSinceStartup - t0 < 20f)
                yield return null;
            yield return new WaitForSeconds(2f); // subsistemas Awake/Start

            var playerGo = GameObject.Find("PlayerCar");
            var player = playerGo != null
                ? playerGo.GetComponent<PlayerCarController>()
                : Object.FindFirstObjectByType<PlayerCarController>();
            Assert.IsNotNull(player,
                $"player car ausente; cena ativa={SceneManager.GetActiveScene().name}");
            var pt = AutonomousPlaytester.Attach(player.transform);
            Assert.IsNotNull(pt);

            float sim = 60f; // segundos simulados
            Time.timeScale = 4f;
            float deadline = Time.realtimeSinceStartup + sim / 4f;
            while (Time.realtimeSinceStartup < deadline)
                yield return null;
            Time.timeScale = 1f;

            var report = pt.Report();
            Directory.CreateDirectory("evidence/qa");
            File.WriteAllText("evidence/qa/playtest_report.md", report);
            Debug.Log(report);
            // não falha o build por anomalias — são dados p/ o agente
        }
    }
}
