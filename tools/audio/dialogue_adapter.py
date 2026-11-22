#!/usr/bin/env python3
"""
tools/audio/dialogue_adapter.py
Adapter G2 (Studio V2): converte um contrato rico `dialogues.yaml` (ex.: CONCERTO)
nos artefatos derivados `voice-cast.json` e `dialogues_<scene>.json` consumidos
pelo `tts_bridge.py` (XTTS-v2 WSL), preservando legendas bilingues, speed e
max_duration_ms e normalizando codigos de idioma ("pt-BR" -> "pt").
"""

import argparse
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent
sys.path.insert(0, str(ROOT / "tools" / "studio"))
from yaml_loader import load_yaml  # noqa: E402

LANG_MAP = {
    "pt-br": "pt",
    "pt_br": "pt",
    "en-us": "en",
    "en_us": "en",
    "es-es": "es",
}


def normalize_lang(lang: str) -> str:
    if not lang:
        return "pt"
    low = lang.strip().lower()
    return LANG_MAP.get(low, low.split("-")[0])


def adapt_dialogues(yaml_path: Path, out_dir: Path, scene_filter: str = None,
                    voices_root: str = "") -> dict:
    data = load_yaml(yaml_path)
    meta = data.get("meta", {})
    default_lang = normalize_lang(meta.get("lang_default", "pt-BR"))
    voices = data.get("voices", {})
    lines = data.get("lines", [])

    warnings = []
    cast = {}
    for vid, vcfg in voices.items():
        if not isinstance(vcfg, dict):
            continue
        engine = str(vcfg.get("engine", "xtts-v2")).lower()
        if engine != "xtts-v2":
            warnings.append(
                f"Voz '{vid}' pede engine '{engine}' -> roteada para fallback 'xtts-v2' (requer speaker_wav)"
            )
        ref = vcfg.get("ref", "")
        if ref and voices_root and not Path(ref).is_absolute():
            speaker_wav = str((Path(voices_root) / ref).as_posix())
        else:
            speaker_wav = ref or f"<definir-amostra-wav-para-{vid}>"
        cast[vid] = {
            "display_name": vid.replace("_", " ").title(),
            "language": default_lang,
            "requested_engine": engine,
            "effective_engine": "xtts-v2",
            "speaker_wav": speaker_wav,
            "speed": float(vcfg.get("speed", 1.0)),
            "fx": vcfg.get("fx", []),
        }

    out_dir.mkdir(parents=True, exist_ok=True)
    cast_path = out_dir / "voice-cast.json"
    cast_path.write_text(
        json.dumps({"version": "1.0", "source": yaml_path.name, "characters": cast},
                   indent=2, ensure_ascii=False),
        encoding="utf-8",
    )

    by_scene = {}
    for ln in lines:
        sc = str(ln.get("scene", "default")).replace("/", "_")
        if scene_filter and sc != scene_filter and ln.get("scene") != scene_filter:
            continue
        by_scene.setdefault(sc, []).append({
            "id": ln["id"],
            "character": ln.get("voice") or ln.get("character"),
            "language": normalize_lang(ln.get("language") or default_lang),
            "text": ln["text"],
            "emotion": ln.get("emotion", "neutro"),
            "max_duration_ms": ln.get("max_duration_ms"),
            "subtitles": ln.get("subtitles", {}),
        })

    scene_files = []
    for sc, sc_lines in by_scene.items():
        sp = out_dir / f"dialogues_{sc}.json"
        sp.write_text(
            json.dumps({"scene": sc, "source": yaml_path.name, "lines": sc_lines},
                       indent=2, ensure_ascii=False),
            encoding="utf-8",
        )
        scene_files.append(str(sp))

    return {
        "source_yaml": str(yaml_path),
        "cast_json": str(cast_path),
        "voices_count": len(cast),
        "scenes_count": len(by_scene),
        "lines_exported": sum(len(v) for v in by_scene.values()),
        "scene_files": scene_files,
        "warnings": warnings,
    }


def main():
    ap = argparse.ArgumentParser(description="Adapter G2: dialogues.yaml -> voice-cast.json + dialogues_<scene>.json")
    ap.add_argument("--input", required=True, help="Caminho para dialogues.yaml")
    ap.add_argument("--out-dir", required=True, help="Pasta de saida para os JSONs derivados")
    ap.add_argument("--scene", default=None, help="Filtrar apenas uma cena (opcional)")
    ap.add_argument("--voices-root", default="", help="Diretorio base das amostras .wav (opcional)")
    args = ap.parse_args()

    report = adapt_dialogues(Path(args.input).resolve(), Path(args.out_dir).resolve(),
                             args.scene, args.voices_root)
    print(json.dumps(report, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
