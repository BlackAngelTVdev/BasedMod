using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ItemAdder.Discord;
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
        public const string PluginVersion = "1.5.3";

        /// <summary>
        /// Application Discord du mod, valable pour tous les joueurs : son nom
        /// est le texte que Discord affiche après « Joue à ». Surchargeable
        /// dans la configuration pour utiliser sa propre application.
        /// </summary>
        public const string DiscordApplicationId = "1564011498009923655";

        internal static ManualLogSource Log;
        internal static ConfigEntry<float> FundsMultiplier;
        internal static ConfigEntry<bool> EnableTobacco;
        internal static ConfigEntry<bool> EnableUnderground;
        internal static ConfigEntry<bool> EnableSmartRestock;
        internal static ConfigEntry<float> UndergroundRaidFine;
        internal static ConfigEntry<float> UndergroundGrowHours;
        internal static ConfigEntry<float> UndergroundRaidChance;
        internal static ConfigEntry<bool> EnableDiscordPresence;
        internal static ConfigEntry<string> DiscordAppId;
        internal static ConfigEntry<string> DiscordDetails;
        internal static ConfigEntry<string> DiscordState;

        // TEMPORAIRE (mise au point du placement) : à retirer avec le mode pause.
        internal static ConfigEntry<bool> PlacementPauseBeforeLog;

        private Harmony _harmony;
        private DiscordRichPresence _presence;

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

            EnableDiscordPresence = Config.Bind(
                "Discord",
                "ActiverPresence",
                true,
                "Affiche sur Discord que le jeu tourne avec le mod (statut « Joue à … »). " +
                "Le texte du statut vient du nom de l'application Discord renseignée dans IdentifiantApplication.");

            DiscordAppId = Config.Bind(
                "Discord",
                "IdentifiantApplication",
                DiscordApplicationId,
                "Identifiant (Application ID) de l'application Discord à afficher. Son nom est ce que " +
                "Discord écrit après « Joue à ». Par défaut celui du mod, donc rien à configurer ; " +
                "vide = identifiant du mod. Mets le tien (https://discord.com/developers/applications) " +
                "pour afficher ton propre nom.");

            DiscordDetails = Config.Bind(
                "Discord",
                "TexteDetails",
                "Mod ItemAdder v" + PluginVersion,
                "Première ligne du statut Discord (2 à 128 caractères).");

            DiscordState = Config.Bind(
                "Discord",
                "TexteEtat",
                "By BlackAngelTVdev",
                "Seconde ligne du statut Discord (2 à 128 caractères).");

            PlacementPauseBeforeLog = Config.Bind(
                "Placement",
                "PauseAvantLog",
                true,
                "Met le jeu en pause quand F10 relève la trappe : le fantôme et les coordonnées " +
                "restent affichés pour vérifier l'emplacement, un second F10 écrit dans le log. " +
                "Réglage temporaire de mise au point.");

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();

            if (EnableUnderground.Value)
            {
                GameObject underground = new GameObject("ItemAdder_UndergroundManager");
                Object.DontDestroyOnLoad(underground);
                underground.AddComponent<UndergroundManager>();
                underground.AddComponent<UndergroundPlacementMode>();
            }
            else
            {
                // Le mode placement reste disponible même sous-sol désactivé :
                // il sert à relever les coordonnées de la trappe.
                GameObject placement = new GameObject("ItemAdder_PlacementMode");
                Object.DontDestroyOnLoad(placement);
                placement.AddComponent<UndergroundPlacementMode>();
            }

            GameObject escapeMenu = new GameObject("ItemAdder_EscapeMenu");
            Object.DontDestroyOnLoad(escapeMenu);
            escapeMenu.AddComponent<EscapeMenuSaveButton>();

            StartDiscordPresence();

            Log.LogInfo($"{PluginName} v{PluginVersion} chargé.");
        }

        /// <summary>
        /// Statut Discord : purement local, chaque joueur affiche le sien.
        /// Sans identifiant d'application valide, Discord n'a aucun nom à afficher,
        /// donc on se contente d'expliquer une fois ce qu'il manque.
        /// </summary>
        private void StartDiscordPresence()
        {
            if (!EnableDiscordPresence.Value)
            {
                Log.LogInfo("[Discord] Statut désactivé (ActiverPresence = false).");
                return;
            }

            string appId = (DiscordAppId.Value ?? string.Empty).Trim();

            // Champ vidé à la main : on revient à l'application du mod plutôt que
            // de priver le joueur du statut sans qu'il ait rien demandé.
            if (appId.Length == 0)
            {
                appId = DiscordApplicationId;
            }

            if (!IsDiscordAppId(appId))
            {
                Log.LogWarning(
                    "[Discord] Identifiant d'application invalide (" + appId + ") : " +
                    "attendu un nombre de 17 à 20 chiffres.");
                return;
            }

            _presence = new DiscordRichPresence(
                appId,
                DiscordDetails.Value,
                DiscordState.Value,
                message => Log.LogInfo(message));
            _presence.Start();
        }

        private static bool IsDiscordAppId(string value)
        {
            if (value.Length < 17 || value.Length > 20)
            {
                return false;
            }

            foreach (char character in value)
            {
                if (character < '0' || character > '9')
                {
                    return false;
                }
            }

            return true;
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
            _presence?.Dispose();
            _presence = null;
            _harmony?.UnpatchSelf();
        }
    }
}
