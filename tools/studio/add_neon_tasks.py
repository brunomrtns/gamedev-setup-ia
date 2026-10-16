import json
from pathlib import Path
from datetime import datetime

tasks_dir = Path("c:/Users/Bruno/workspace/gamedev-setup-ia/.studio/tasks")

tasks = [
    {
        "id": "TASK-011",
        "title": "Neon Rush - Foundation Services & Procedural Synthesized Audio",
        "stage": "engineering",
        "command": None,
        "dependencies": ["TASK-010"],
        "status": "pending",
        "evidence_files": [],
        "created_at": datetime.now().isoformat(),
        "updated_at": datetime.now().isoformat(),
        "error_message": None,
        "retries": 0,
        "max_retries": 3
    },
    {
        "id": "TASK-012",
        "title": "Neon Rush - Input System & Touch/Keyboard Swipe Adapter",
        "stage": "engineering",
        "command": None,
        "dependencies": ["TASK-011"],
        "status": "pending",
        "evidence_files": [],
        "created_at": datetime.now().isoformat(),
        "updated_at": datetime.now().isoformat(),
        "error_message": None,
        "retries": 0,
        "max_retries": 3
    },
    {
        "id": "TASK-013",
        "title": "Neon Rush - Player 3-Lane Movement & Procedural Robot Animator",
        "stage": "engineering",
        "command": None,
        "dependencies": ["TASK-012"],
        "status": "pending",
        "evidence_files": [],
        "created_at": datetime.now().isoformat(),
        "updated_at": datetime.now().isoformat(),
        "error_message": None,
        "retries": 0,
        "max_retries": 3
    },
    {
        "id": "TASK-014",
        "title": "Neon Rush - Hazards, Obstacles & Energy Cell Collectibles",
        "stage": "engineering",
        "command": None,
        "dependencies": ["TASK-013"],
        "status": "pending",
        "evidence_files": [],
        "created_at": datetime.now().isoformat(),
        "updated_at": datetime.now().isoformat(),
        "error_message": None,
        "retries": 0,
        "max_retries": 3
    },
    {
        "id": "TASK-015",
        "title": "Neon Rush - GameManager, UI Canvas & Audio Integration",
        "stage": "engineering",
        "command": None,
        "dependencies": ["TASK-014"],
        "status": "pending",
        "evidence_files": [],
        "created_at": datetime.now().isoformat(),
        "updated_at": datetime.now().isoformat(),
        "error_message": None,
        "retries": 0,
        "max_retries": 3
    },
    {
        "id": "TASK-016",
        "title": "Neon Rush - Track Builder, URP Materials, Lighting & Extraction Portal",
        "stage": "engineering",
        "command": None,
        "dependencies": ["TASK-015"],
        "status": "pending",
        "evidence_files": [],
        "created_at": datetime.now().isoformat(),
        "updated_at": datetime.now().isoformat(),
        "error_message": None,
        "retries": 0,
        "max_retries": 3
    },
    {
        "id": "TASK-017",
        "title": "Neon Rush - UTF EditMode & PlayMode Automated Tests",
        "stage": "qa",
        "command": None,
        "dependencies": ["TASK-016"],
        "status": "pending",
        "evidence_files": [],
        "created_at": datetime.now().isoformat(),
        "updated_at": datetime.now().isoformat(),
        "error_message": None,
        "retries": 0,
        "max_retries": 3
    },
    {
        "id": "TASK-018",
        "title": "Neon Rush - Scene Assembly & Visual Capture Validation",
        "stage": "qa",
        "command": None,
        "dependencies": ["TASK-017"],
        "status": "pending",
        "evidence_files": [],
        "created_at": datetime.now().isoformat(),
        "updated_at": datetime.now().isoformat(),
        "error_message": None,
        "retries": 0,
        "max_retries": 3
    },
    {
        "id": "TASK-019",
        "title": "Neon Rush - Android Build (APK) & Final Verification",
        "stage": "build",
        "command": None,
        "dependencies": ["TASK-018"],
        "status": "pending",
        "evidence_files": [],
        "created_at": datetime.now().isoformat(),
        "updated_at": datetime.now().isoformat(),
        "error_message": None,
        "retries": 0,
        "max_retries": 3
    }
]

for t in tasks:
    file_path = tasks_dir / f"{t['id']}.json"
    with open(file_path, "w", encoding="utf-8") as f:
        json.dump(t, f, indent=2, ensure_ascii=False)
print(f"Adicionadas {len(tasks)} tarefas no DAG com sucesso!")
