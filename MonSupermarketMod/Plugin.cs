using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace MonSupermarketMod
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("Supermarket Together.exe")]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.username.supermarkettogethermod";
        public const string PluginName = "Mon Super Mod";
        public const string PluginVersion = "1.0.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<float> FundsMultiplier;
        internal static ConfigEntry<bool> EnablePlayerTweaks;
        internal static ConfigEntry<float> SpeedMultiplier;
        internal static ConfigEntry<float> JumpMultiplier;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            FundsMultiplier = Config.Bind(
                "Général",
                "MultiplicateurDeFonds",
                1f,
                "Multiplicateur appliqué aux fonds modifiés via GameData.CmdAlterFunds. 1 = comportement vanilla.");

            EnablePlayerTweaks = Config.Bind(
                "Joueur",
                "ActiverModifications",
                true,
                "Active ou désactive les multiplicateurs de vitesse et de saut.");

            SpeedMultiplier = Config.Bind(
                "Joueur",
                "MultiplicateurVitesse",
                2f,
                new ConfigDescription(
                    "Multiplicateur de vitesse (marche, sprint, accroupi). 1 = comportement vanilla.",
                    new AcceptableValueRange<float>(0.1f, 10f)));

            JumpMultiplier = Config.Bind(
                "Joueur",
                "MultiplicateurSaut",
                1.8f,
                new ConfigDescription(
                    "Multiplicateur de hauteur de saut. 1 = comportement vanilla.",
                    new AcceptableValueRange<float>(0.1f, 10f)));

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();

            Log.LogInfo($"{PluginName} v{PluginVersion} chargé.");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
