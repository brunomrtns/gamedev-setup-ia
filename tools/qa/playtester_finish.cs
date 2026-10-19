// playtester_finish.cs — eval_file: exporta sessão e despeja relatório no console.
var pt = UnityEngine.Object.FindFirstObjectByType<Studio.Gameplay.AutonomousPlaytester>();
if (pt == null)
{
    UnityEngine.Debug.Log("[QA] playtester ausente — nada a exportar");
}
else
{
    var path = pt.ExportSession();
    var rep = pt.Report();
    // console pode perder o relatório (buffer); grava em arquivo também
    var repPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(
        UnityEngine.Application.dataPath, "../Temp/qa_report.md"));
    System.IO.File.WriteAllText(repPath, rep);
    UnityEngine.Debug.Log("===QA_REPORT_BEGIN===\n" + rep
        + "\n===QA_REPORT_END===");
}
