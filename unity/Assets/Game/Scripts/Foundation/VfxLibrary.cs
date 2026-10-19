using UnityEngine;

namespace Studio.Foundation
{
    /// <summary>
    /// Catálogo de prefabs VFX (War FX/CFX). O builder atribui os assets;
    /// Awake registra no VfxPool e expõe refs para consumidores
    /// (VehicleDamageController, Ak47Weapon). Chaves: impact_metal,
    /// fire_small, explosion, residual_smoke.
    /// </summary>
    public class VfxLibrary : MonoBehaviour
    {
        public static VfxLibrary Instance { get; private set; }

        [Header("War FX / CFX (mobile)")]
        public GameObject impactMetal;
        public GameObject fireSmall;
        public GameObject explosion;
        public GameObject residualSmoke;

        private void Awake()
        {
            Instance = this;
            VfxPool.Register("impact_metal", impactMetal);
            VfxPool.Register("fire_small", fireSmall);
            VfxPool.Register("explosion", explosion);
            VfxPool.Register("residual_smoke", residualSmoke);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
