using UnityEngine;
using UnityEngine.SceneManagement;

namespace Studio.Net
{
    /// <summary>
    /// Hook de runtime: após carregar a cena, se args pedem rede
    /// (-ndserver/-ndclient/-ndhost), desativa o rig SP e inicia o
    /// NetworkManager. Sem args → não faz nada (SP preservado).
    /// </summary>
    public static class NdNetBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnSceneReady()
        {
            NdNetSession.InitFromArgs();
            if (!NdNetSession.NetActive) return;
            if (SceneManager.GetActiveScene().name != "NeonDistrict") return;
            NdNetSession.DisableSpRig();
            NdNetSession.StartNet();
            // -ndshot=path → screenshot ~25s após spawn (evidência visual MP)
            foreach (var a in System.Environment.GetCommandLineArgs())
                if (a.StartsWith("-ndshot="))
                {
                    var go = new GameObject("NdShot");
                    go.AddComponent<NdScreenshot>()
                        .Path = a.Substring(8);
                }
        }
    }
}
