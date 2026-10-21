
var car = UnityEngine.Object.FindFirstObjectByType<Studio.Gameplay.PlayerCarController>();
var cam = UnityEngine.Camera.main;
var rt = new UnityEngine.RenderTexture(1280,720,24);
cam.targetTexture = rt; cam.Render();
UnityEngine.RenderTexture.active = rt;
var tex = new UnityEngine.Texture2D(1280,720,UnityEngine.TextureFormat.RGB24,false);
tex.ReadPixels(new UnityEngine.Rect(0,0,1280,720),0,0); tex.Apply();
System.IO.Directory.CreateDirectory(@"C:/Users/Bruno/workspace/gamedev-setup-ia/evidence/ingame".Replace("/",System.IO.Path.DirectorySeparatorChar.ToString()));
System.IO.File.WriteAllBytes(@"C:/Users/Bruno/workspace/gamedev-setup-ia/evidence/ingame/INGAME_f6_letreiro.png".Replace("/",System.IO.Path.DirectorySeparatorChar.ToString()), tex.EncodeToPNG());
cam.targetTexture=null; UnityEngine.RenderTexture.active=null;
UnityEngine.Debug.Log("[shot] f6_letreiro saved car="+car.transform.position);
