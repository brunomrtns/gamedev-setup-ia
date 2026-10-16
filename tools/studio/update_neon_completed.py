import json
from pathlib import Path
from datetime import datetime

tasks_dir = Path("c:/Users/Bruno/workspace/gamedev-setup-ia/.studio/tasks")

updates = {
    "TASK-011": {
        "status": "completed",
        "evidence_files": [
            "unity/Assets/Game/Scripts/Foundation/GameConstants.cs",
            "unity/Assets/Game/Scripts/Foundation/LaneMath.cs",
            "unity/Assets/Game/Scripts/Foundation/ProceduralAudioService.cs"
        ]
    },
    "TASK-012": {
        "status": "completed",
        "evidence_files": [
            "unity/Assets/Game/Scripts/Input/NeonInputAdapter.cs"
        ]
    },
    "TASK-013": {
        "status": "completed",
        "evidence_files": [
            "unity/Assets/Game/Scripts/Gameplay/PlayerMovement.cs",
            "unity/Assets/Game/Scripts/Gameplay/RobotAnimator.cs",
            "unity/Assets/Game/Scripts/Gameplay/PlayerController.cs"
        ]
    },
    "TASK-014": {
        "status": "completed",
        "evidence_files": [
            "unity/Assets/Game/Scripts/Gameplay/ObstacleHazard.cs",
            "unity/Assets/Game/Scripts/Gameplay/EnergyCellCollectible.cs",
            "unity/Assets/Game/Scripts/Gameplay/ExtractionPortal.cs"
        ]
    },
    "TASK-015": {
        "status": "completed",
        "evidence_files": [
            "unity/Assets/Game/Scripts/Gameplay/GameManager.cs",
            "unity/Assets/Game/Scripts/UI/UIManager.cs"
        ]
    },
    "TASK-016": {
        "status": "completed",
        "evidence_files": [
            "unity/Assets/Editor/NeonRushSceneBuilder.cs",
            "unity/Assets/Game/Scenes/NeonRush.unity"
        ]
    },
    "TASK-017": {
        "status": "completed",
        "evidence_files": [
            "unity/Assets/Game/Tests/EditMode/NeonRushGameplayTests.cs",
            "evidence/test-reports/junit-results.xml"
        ]
    },
    "TASK-018": {
        "status": "completed",
        "evidence_files": [
            "evidence/captures/neon_rush_01_start_screen.png",
            "evidence/captures/neon_rush_02_gameplay.png",
            "evidence/captures/neon_rush_03_obstacles_hurdles.png",
            "evidence/captures/neon_rush_04_extraction_portal.png",
            "evidence/captures/neon_rush_05_game_over.png",
            "evidence/captures/neon_rush_06_victory.png"
        ]
    }
}

for tid, data in updates.items():
    fpath = tasks_dir / f"{tid}.json"
    if fpath.exists():
        with open(fpath, "r", encoding="utf-8") as f:
            t = json.load(f)
        t["status"] = data["status"]
        t["evidence_files"] = data["evidence_files"]
        t["updated_at"] = datetime.now().isoformat()
        with open(fpath, "w", encoding="utf-8") as f:
            json.dump(t, f, indent=2, ensure_ascii=False)

print("Tarefas atualizadas com sucesso!")
