// playtester_status.cs — eval_file: linha de status do bot (heartbeat do runner).
var pt = UnityEngine.Object.FindFirstObjectByType<Studio.Gameplay.AutonomousPlaytester>();
UnityEngine.Debug.Log(pt != null ? pt.StatusLine() : "[QA-STATUS] bot ausente");
