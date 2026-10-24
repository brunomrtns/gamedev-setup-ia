// _drv_attach.cs — eval_file: anexa DrivabilityProbe no carro do jogador.
// Tráfego e pedestres são desligados durante a varredura (geometria estática
// determinística; obstáculos móveis são visíveis/legítimos por definição).
var old = UnityEngine.Object.FindObjectsByType<Studio.Gameplay.DrivabilityProbe>(
    UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
foreach (var o in old) UnityEngine.Object.Destroy(o);

foreach (var v in UnityEngine.Object.FindObjectsByType<Studio.Gameplay.TrafficVehicle>(
    UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
    v.gameObject.SetActive(false);
var tm = UnityEngine.Object.FindFirstObjectByType<Studio.Gameplay.TrafficManager>();
if (tm != null) tm.enabled = false;
var ps = Studio.Gameplay.PedestrianSystem.Instance;
if (ps != null) ps.enabled = false;

var car = UnityEngine.Object.FindFirstObjectByType<Studio.Gameplay.PlayerCarController>();
if (car == null)
{
    UnityEngine.Debug.LogError("[DRV-ATTACH] FALHOU: PlayerCarController ausente");
}
else
{
    var probe = car.gameObject.AddComponent<Studio.Gameplay.DrivabilityProbe>();
    UnityEngine.Debug.Log($"[DRV-ATTACH] ok waypoints={probe.WaypointCount} segs={probe.TotalSegments}");
}
