using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ItemAdder.UI;
using ItemAdder.Underground;
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
        public const string PluginVersion = "1.4.2";

        internal static ManualLogSource Log;
        internal static ConfigEntry<float> FundsMultiplier;
        internal static ConfigEntry<bool> EnableTobacco;
        internal static ConfigEntry<bool> EnableUnderground;
        internal static ConfigEntry<bool> EnableSmartRestock;
        internal static ConfigEntry<float> UndergroundRaidFine;
        internal static ConfigEntry<float> UndergroundGrowHours;
        internal static ConfigEntry<float> UndergroundRaidChance;

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

            EnableSmartRestock = Config.Bind(
                "Employés",
                "ReassortIntelligent",
                true,
                "Les employés de réassort rangent les produits restés en réserve sur l'étagère compatible la plus proche, même si le joueur ne les a jamais placés.");

            EnableUnderground = Config.Bind(
                "Souterrain",
                "ActiverSousSol",
                true,
                "Construit un sous-sol clandestin sous le magasin (layout Classic) avec culture, caisse et police.");

            UndergroundRaidFine = Config.Bind(
                "Souterrain",
                "AmendeRaid",
                5000f,
                "Montant retiré des fonds lorsqu'un raid de police trouve des plants non cachés.");

            UndergroundGrowHours = Config.Bind(
                "Souterrain",
                "DureePousseHeures",
                8f,
                "Nombre d'heures de jeu nécessaires pour qu'une parcelle arrosée arrive à maturité.");

            UndergroundRaidChance = Config.Bind(
                "Souterrain",
                "ChanceRaidParInspection",
                0.35f,
                "Probabilité qu'une inspection de police déclenche un raid si la trappe est ouverte et qu'un plant est visible.");

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();

            if (EnableUnderground.Value)
            {
                GameObject underground = new GameObject("ItemAdder_UndergroundManager");
                Object.DontDestroyOnLoad(underground);
                underground.AddComponent<UndergroundManager>();
            }

            GameObject escapeMenu = new GameObject("ItemAdder_EscapeMenu");
            Object.DontDestroyOnLoad(escapeMenu);
            escapeMenu.AddComponent<EscapeMenuSaveButton>();

            Log.LogInfo($"{PluginName} v{PluginVersion} chargé.");
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F5))
                AddFranchisePoints();
            else if (Input.GetKeyDown(KeyCode.F6))
                GiveUnlimitedFunds();
        }

        private static void AddFranchisePoints()
        {
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

        // F6 : argent « illimité » pour acheter les extensions du magasin et
        // casser les murs. On reste sous le plafond interne du jeu (2,14 Md $).
        private static void GiveUnlimitedFunds()
        {
            if (!NetworkServer.active)
            {
                Log.LogWarning("[Fonds] F6 : seul l'hôte peut modifier les fonds.");
                return;
            }

            GameData data = GameData.Instance;
            if (data == null)
            {
                Log.LogWarning("[Fonds] F6 : GameData n'est pas encore disponible.");
                return;
            }

            data.NetworkgameFunds = 2000000000f;
            Log.LogInfo("[Fonds] F6 : fonds fixés à 2 000 000 000 $ (argent illimité).");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
