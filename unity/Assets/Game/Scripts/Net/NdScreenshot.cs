using System.Collections;
using UnityEngine;

namespace Studio.Net
{
    /// <summary>Captura screenshot após o mundo popular (evidência MP).</summary>
    public class NdScreenshot : MonoBehaviour
    {
        public string Path = "evidence/net/nd_shot.png";
        private IEnumerator Start()
        {
            yield return new WaitForSeconds(25f); // spawn+mundo populado
            ScreenCapture.CaptureScreenshot(Path);
            Debug.Log($"[NDNET] screenshot salvo: {Path}");
        }
    }
}
