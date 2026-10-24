#!/usr/bin/env python3
"""v38_ingame_proof.py — frames IN-GAME nas posicoes do dono (M2.6) + pontos
criticos do fix. Padrao ingame_proof.py (editor vivo, teleporta carro + shot).
"""
import os
import subprocess
import sys
import time

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PROJ = os.path.join(ROOT, "unity")
OUT = os.path.join(ROOT, "evidence", "ingame", "v38")
STEP = os.path.join(ROOT, "tools", "qa", "_v38_step.cs")

ROUTE = [
    # (tag, x, y, z, yaw) — M3: ruas com asfalto restaurado
    ("m3_rua_ew_z-26", 0.0, 1, -26.0, 90),
    ("m3_rua_ns_x26", 26.0, 1, 0.0, 0),
    ("m3_crosswalk_26", 26.0, 1, 34.2, 0),
    ("m3_rua_ns_x-78", -78.0, 1, -40.0, 0),
    ("m3_rua_ew_z78", 40.0, 1, 78.0, 90),
    ("m3_avenida", 0.0, 1, 26.0, 90),
]

CS_MOVE = """
var car = UnityEngine.Object.FindFirstObjectByType<Studio.Gameplay.PlayerCarController>();
var rb = car.GetComponent<UnityEngine.Rigidbody>();
if (rb != null) {{ rb.linearVelocity=UnityEngine.Vector3.zero; rb.angularVelocity=UnityEngine.Vector3.zero;
  rb.position=new UnityEngine.Vector3({x}f,{y}f,{z}f); rb.rotation=UnityEngine.Quaternion.Euler(0f,{yaw}f,0f); }}
else car.transform.SetPositionAndRotation(new UnityEngine.Vector3({x}f,{y}f,{z}f), UnityEngine.Quaternion.Euler(0f,{yaw}f,0f));
UnityEngine.Debug.Log("[v38proof] {tag} pos="+car.transform.position);
"""

CS_SHOT = """
var car = UnityEngine.Object.FindFirstObjectByType<Studio.Gameplay.PlayerCarController>();
var cam = UnityEngine.Camera.main;
var rt = new UnityEngine.RenderTexture(1280,720,24);
cam.targetTexture = rt; cam.Render();
UnityEngine.RenderTexture.active = rt;
var tex = new UnityEngine.Texture2D(1280,720,UnityEngine.TextureFormat.RGB24,false);
tex.ReadPixels(new UnityEngine.Rect(0,0,1280,720),0,0); tex.Apply();
System.IO.Directory.CreateDirectory(@"OUTDIR".Replace("/",System.IO.Path.DirectorySeparatorChar.ToString()));
System.IO.File.WriteAllBytes(@"OUTDIR/V38_TAG.png".Replace("/",System.IO.Path.DirectorySeparatorChar.ToString()), tex.EncodeToPNG());
cam.targetTexture=null; UnityEngine.RenderTexture.active=null;
UnityEngine.Debug.Log("[v38proof] shot TAG saved");
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
            import json
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
    ok, out = ucmd("eval_file", STEP.replace(os.sep, "/"), timeout=90)
    return ok


def main():
    os.makedirs(OUT, exist_ok=True)
    ok, out = ucmd("editor_status", timeout=15)
    try:
        import json
        st = json.loads(out) if ok else {}
    except Exception:
        st = {}
    if st.get("status") != "playing":
        ucmd("open_scene", "--path", "Assets/Game/Scenes/NeonDistrict.unity", timeout=120)
        ucmd("editor_play", timeout=30)
        if not wait_status("playing", 120):
            print("[v38proof] ERRO play mode"); sys.exit(2)
        time.sleep(8)
    for tag, x, y, z, yaw in ROUTE:
        print(f"[v38proof] {tag}")
        ev(CS_MOVE.format(x=x, y=y, z=z, yaw=yaw, tag=tag))
        time.sleep(2.5)
        ev(CS_SHOT.replace("OUTDIR", OUT.replace("\\", "/")).replace("TAG", tag))
    print("[v38proof] done ->", OUT)


if __name__ == "__main__":
    main()
