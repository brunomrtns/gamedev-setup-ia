#!/usr/bin/env python3
"""ingame_proof.py — prova IN-GAME do Green Hills via pipeline do editor vivo.
Teleporta o carro ao longo da rota e captura Camera.main -> PNG em cada ponto.
Uso: python tools/qa/ingame_proof.py [--stop]"""
import json, os, subprocess, sys, time

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PROJ = os.path.join(ROOT, "unity")
OUT = os.path.join(ROOT, "evidence", "ingame")
STEP = os.path.join(ROOT, "tools", "qa", "_proof_step.cs")

ROUTE = [
    ("f1_poste1",  -21.6, 1, 19.3, 0),
    ("f1_poste2",   23.5, 1, 18.5, 0),
    ("f2_luz",     167.7, 1, 17.0, 90),
    ("f4_zfight",  184.3, 1, 22.3, 90),
    ("f5_frag1",   -33.5, 1, 31.4, 0),
    ("f5_frag2",   230.0, 1, 0.3, 90),
    ("f5_campo",   219.7, 1, -102.9, 0),
    ("f6_letreiro",210.2, 1, -124.5, 0),
]

CS_MOVE = """
var car = UnityEngine.Object.FindFirstObjectByType<Studio.Gameplay.PlayerCarController>();
var rb = car.GetComponent<UnityEngine.Rigidbody>();
if (rb != null) {{ rb.linearVelocity=UnityEngine.Vector3.zero; rb.angularVelocity=UnityEngine.Vector3.zero;
  rb.position=new UnityEngine.Vector3({x}f,{y}f,{z}f); rb.rotation=UnityEngine.Quaternion.Euler(0f,{yaw}f,0f); }}
else car.transform.SetPositionAndRotation(new UnityEngine.Vector3({x}f,{y}f,{z}f), UnityEngine.Quaternion.Euler(0f,{yaw}f,0f));
UnityEngine.Debug.Log("[step] {tag} pos="+car.transform.position);
"""

CS_SHOT = """
var car = UnityEngine.Object.FindFirstObjectByType<Studio.Gameplay.PlayerCarController>();
var cam = UnityEngine.Camera.main;
var rt = new UnityEngine.RenderTexture(1280,720,24);
cam.targetTexture = rt; cam.Render();
UnityEngine.RenderTexture.active = rt;
var tex = new UnityEngine.Texture2D(1280,720,UnityEngine.TextureFormat.RGB24,false);
tex.ReadPixels(new UnityEngine.Rect(0,0,1280,720),0,0); tex.Apply();
System.IO.Directory.CreateDirectory(@"ROOT/evidence/ingame".Replace("/",System.IO.Path.DirectorySeparatorChar.ToString()));
System.IO.File.WriteAllBytes(@"OUT/INGAME_TAG.png".Replace("/",System.IO.Path.DirectorySeparatorChar.ToString()), tex.EncodeToPNG());
cam.targetTexture=null; UnityEngine.RenderTexture.active=null;
UnityEngine.Debug.Log("[shot] TAG saved car="+car.transform.position);
"""


def ucmd(*args, timeout=60):
    cmd = ["unity", "cmd", "--project-path", PROJ, "--result-only", *args]
    r = subprocess.run(cmd, capture_output=True, text=True, timeout=timeout)
    return r.returncode == 0, (r.stdout or r.stderr or "").strip()


def wait_status(want, timeout_s=240):
    t0 = time.time()
    while time.time() - t0 < timeout_s:
        ok, out = ucmd("editor_status", timeout=30)
        try:
            st = json.loads(out)
        except Exception:
            st = {}
        if st.get("status") == want:
            return True
        time.sleep(4)
    return False


def ev(cs):
    with open(STEP, "w", encoding="utf8") as f:
        f.write(cs)
    ok, out = ucmd("eval_file", STEP.replace(os.sep, "/"), timeout=60)
    print("  eval:", out[:160].replace("\n", " "))
    return ok


def main():
    os.makedirs(OUT, exist_ok=True)
    # editor vivo?
    ok, out = ucmd("editor_status", timeout=15)
    if not ok or "status" not in out:
        print("[proof] editor offline — abrindo Unity...")
        subprocess.Popen([r"C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe",
                          "-projectPath", PROJ])
        if not wait_status("ready", 300):
            print("[proof] ERRO editor não subiu"); sys.exit(2)
    ucmd("open_scene", "--path", "Assets/Game/Scenes/NeonDistrict.unity", timeout=120)
    ucmd("editor_play", timeout=30)
    if not wait_status("playing", 120):
        print("[proof] ERRO play mode não iniciou"); sys.exit(2)
    time.sleep(8)

    for tag, x, y, z, yaw in ROUTE:
        print(f"[proof] {tag}")
        ev(CS_MOVE.format(x=x, y=y, z=z, yaw=yaw, tag=tag))
        time.sleep(2.5)  # física assenta
        ev(CS_SHOT.replace("OUT", OUT.replace("\\", "/")).replace("TAG", tag)
             .replace("ROOT", ROOT.replace("\\", "/")))

    if "--stop" in sys.argv:
        ucmd("editor_stop", timeout=30)
    print("[proof] done")


if __name__ == "__main__":
    main()
