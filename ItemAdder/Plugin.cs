using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace ItemAdder
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("Supermarket Together.exe")]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.username.supermarkettogethermod";
        public const string PluginName = "ItemAdder";
        public const string PluginVersion = "1.2.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<float> FundsMultiplier;
        internal static ConfigEntry<bool> EnableTobacco;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            FundsMultiplier = Config.Bind(
                "Général",
                "MultiplicateurDeFonds",
                1f,
                "Multiplicateur appliqué aux fonds modifiés via GameData.CmdAlterFunds. 1 = comportement vanilla.");

            EnableTobacco = Config.Bind(
                "Tabac",
                "ActiverRayonTabac",
                true,
                "Ajoute le rayon Tabac (cigarettes, tabac à rouler, vapes, snus, cigares, accessoires) et son palier dans l'arbre de compétences.");

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();

            Log.LogInfo($"{PluginName} v{PluginVersion} chargé.");
        }

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.F5))
                return;

            if (!NetworkServer.active)
            {
                Log.LogWarning("[Fonds] F5 : seul l'hôte peut ajouter des points de franchise.");
                return;
            }

            GameData data = GameData.Instance;
            if (data == null)
            {
                Log.LogWarning("[Fonds] F5 : GameData n'est pas encore disponible.");
                return;
            }

            data.NetworkgameFranchisePoints =
                Mathf.Clamp(data.gameFranchisePoints + 50, 0, 100000);
            Log.LogInfo("[Fonds] 50 points de franchise ajoutés.");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
