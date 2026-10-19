// attach_playtester.cs — eval_file: garante UM bot fresco no carro do jogador.
var car = UnityEngine.Object.FindFirstObjectByType<Studio.Gameplay.PlayerCarController>();
if (car == null)
{
    UnityEngine.Debug.LogError("[QA] PlayerCarController nao encontrado — cena errada?");
}
else
{
    // remove instâncias antigas (desabilitadas/encerradas) antes de anexar
    foreach (var old in UnityEngine.Object.FindObjectsByType<Studio.Gameplay.AutonomousPlaytester>(
        UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
        UnityEngine.Object.Destroy(old);
    var pt = Studio.Gameplay.AutonomousPlaytester.Attach(car.transform);
    UnityEngine.Debug.Log($"[QA-ATTACH] ok seed={pt.Seed} id={pt.SessionId} @ {car.transform.position}");
}
