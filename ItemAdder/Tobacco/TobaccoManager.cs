using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ItemAdder.Tobacco
{
    /// <summary>
    /// Ajoute un rayon "Tabac" au jeu, sur le modèle des rayons déjà présents :
    /// - un catalogue complet (cigarettes, tabac à rouler, vapes, snus, cigares, accessoires) ;
    /// - un nouveau palier "Tabac I" dans l'arbre de compétences, après "Fruits de mer I" ;
    /// - les chaînes de localisation associées.
    ///
    /// Toutes les données sont ajoutées à la fin des tableaux du jeu : aucun index
    /// existant n'est décalé, donc les sauvegardes restent lisibles.
    /// Le nœud d'arbre de compétences est un clone du nœud "Fruits de mer I" dont
    /// les PlayMakerFSM sont désactivés et dont l'achat est géré par TobaccoSkillNode.
    /// </summary>
    internal static class TobaccoManager
    {
        private const string Brand = "Kiosque";
        private const string TobaccoName = "Tabac I";

        private enum ProductLook
        {
            Pack,
            Pouch,
            Tube,
            Bottle,
            Vape,
            Tin,
            Cigar,
            Lighter,
            Ashtray,
            Grinder,
            Paper,
            Box
        }

        private sealed class ProductDefinition
        {
            internal readonly string Name;
            internal readonly float Price;
            internal readonly int BoxSize;
            internal readonly Color Color;
            internal readonly Vector3 Size;
            internal readonly ProductLook Look;

            internal ProductDefinition(
                string name, float price, int boxSize, Color color, Vector3 size, ProductLook look)
            {
                Name = name;
                Price = price;
                BoxSize = boxSize;
                Color = color;
                Size = size;
                Look = look;
            }
        }

        private static readonly Vector3 PackSize = new Vector3(0.09f, 0.12f, 0.03f);
        private static readonly Vector3 CigarilloSize = new Vector3(0.09f, 0.07f, 0.035f);
        private static readonly Vector3 PouchSize = new Vector3(0.11f, 0.14f, 0.04f);
        private static readonly Vector3 TubeSize = new Vector3(0.10f, 0.05f, 0.04f);
        private static readonly Vector3 BottleSize = new Vector3(0.06f, 0.15f, 0.06f);
        private static readonly Vector3 VapeSize = new Vector3(0.035f, 0.15f, 0.035f);
        private static readonly Vector3 TinSize = new Vector3(0.10f, 0.035f, 0.10f);
        private static readonly Vector3 CigarSize = new Vector3(0.16f, 0.03f, 0.03f);
        private static readonly Vector3 LighterSize = new Vector3(0.04f, 0.07f, 0.02f);
        private static readonly Vector3 AshtraySize = new Vector3(0.13f, 0.03f, 0.13f);
        private static readonly Vector3 GrinderSize = new Vector3(0.09f, 0.035f, 0.09f);
        private static readonly Vector3 PaperSize = new Vector3(0.07f, 0.06f, 0.01f);

        private static readonly ProductDefinition[] Catalog =
        {
            // Les quatre premiers produits gardent les noms et les indices de la v1.1,
            // et le palier reste l'index 55 : les sauvegardes existantes conservent
            // leur déblocage et leurs prix.
            // Cigarettes et tabac à rouler
            new ProductDefinition("Cigarettes Classic", 9.50f, 10, new Color(0.72f, 0.08f, 0.10f), PackSize, ProductLook.Pack),
            new ProductDefinition("Cigarettes Menthol", 9.90f, 10, new Color(0.05f, 0.45f, 0.25f), PackSize, ProductLook.Pack),
            new ProductDefinition("Cigarillos", 7.50f, 10, new Color(0.35f, 0.18f, 0.08f), CigarilloSize, ProductLook.Pack),
            new ProductDefinition("Tabac à rouler", 14.00f, 5, new Color(0.10f, 0.25f, 0.60f), PouchSize, ProductLook.Pouch),
            new ProductDefinition("Cigarettes Blondes", 9.20f, 10, new Color(0.78f, 0.58f, 0.18f), PackSize, ProductLook.Pack),
            new ProductDefinition("Cigarettes Brunes", 9.40f, 10, new Color(0.33f, 0.20f, 0.12f), PackSize, ProductLook.Pack),
            new ProductDefinition("Cigarettes Light", 9.60f, 10, new Color(0.35f, 0.55f, 0.75f), PackSize, ProductLook.Pack),
            new ProductDefinition("Cigarettes Rollées", 10.20f, 10, new Color(0.10f, 0.48f, 0.46f), PackSize, ProductLook.Pack),
            new ProductDefinition("Tabac à rouler Blond", 14.50f, 5, new Color(0.62f, 0.38f, 0.12f), PouchSize, ProductLook.Pouch),
            new ProductDefinition("Tabac à rouler Brun", 14.20f, 5, new Color(0.28f, 0.16f, 0.08f), PouchSize, ProductLook.Pouch),
            new ProductDefinition("Tabac à rouler Menthol", 14.90f, 5, new Color(0.15f, 0.55f, 0.40f), PouchSize, ProductLook.Pouch),
            new ProductDefinition("Blague de tabac", 16.00f, 5, new Color(0.55f, 0.35f, 0.12f), PouchSize, ProductLook.Pouch),
            new ProductDefinition("Tubes à rouler", 3.50f, 20, new Color(0.35f, 0.45f, 0.60f), TubeSize, ProductLook.Tube),
            new ProductDefinition("Filtres à rouler", 2.80f, 25, new Color(0.75f, 0.75f, 0.70f), new Vector3(0.08f, 0.06f, 0.04f), ProductLook.Box),
            new ProductDefinition("Filtreuse", 24.90f, 4, new Color(0.22f, 0.24f, 0.28f), new Vector3(0.16f, 0.12f, 0.10f), ProductLook.Box),
            new ProductDefinition("Feuilles à rouler", 1.80f, 50, new Color(0.88f, 0.82f, 0.55f), PaperSize, ProductLook.Paper),
            new ProductDefinition("Feuilles à rouler Slim", 2.10f, 50, new Color(0.45f, 0.60f, 0.85f), PaperSize, ProductLook.Paper),

            // Cigarettes électroniques et e-liquides
            new ProductDefinition("Vape jetable", 12.90f, 10, new Color(0.42f, 0.22f, 0.62f), VapeSize, ProductLook.Vape),
            new ProductDefinition("Vape jetable Menthe", 13.20f, 10, new Color(0.20f, 0.65f, 0.55f), VapeSize, ProductLook.Vape),
            new ProductDefinition("Vape jetable Fruits rouges", 13.50f, 10, new Color(0.78f, 0.20f, 0.35f), VapeSize, ProductLook.Vape),
            new ProductDefinition("E-liquide Tabac", 8.90f, 12, new Color(0.60f, 0.36f, 0.10f), BottleSize, ProductLook.Bottle),
            new ProductDefinition("E-liquide Menthe", 9.20f, 12, new Color(0.10f, 0.55f, 0.50f), BottleSize, ProductLook.Bottle),
            new ProductDefinition("E-liquide Fruits rouges", 9.50f, 12, new Color(0.68f, 0.16f, 0.38f), BottleSize, ProductLook.Bottle),
            new ProductDefinition("Recharge de vape", 7.50f, 12, new Color(0.30f, 0.42f, 0.55f), new Vector3(0.07f, 0.07f, 0.04f), ProductLook.Box),
            new ProductDefinition("Résistances de vape", 5.90f, 20, new Color(0.55f, 0.58f, 0.62f), new Vector3(0.06f, 0.05f, 0.03f), ProductLook.Box),

            // Nicotine alternative
            new ProductDefinition("Sachet de nicotine", 6.90f, 20, new Color(0.80f, 0.82f, 0.86f), TinSize, ProductLook.Tin),
            new ProductDefinition("Nicotine pouches Menthe", 7.20f, 20, new Color(0.35f, 0.70f, 0.55f), TinSize, ProductLook.Tin),
            new ProductDefinition("Tabac chauffé", 8.50f, 10, new Color(0.55f, 0.40f, 0.20f), new Vector3(0.10f, 0.06f, 0.05f), ProductLook.Box),
            new ProductDefinition("Bâtonnets de tabac chauffé", 7.90f, 10, new Color(0.65f, 0.42f, 0.22f), new Vector3(0.09f, 0.05f, 0.04f), ProductLook.Box),

            // Cigares et cigarillos
            new ProductDefinition("Cigare", 6.50f, 10, new Color(0.42f, 0.24f, 0.12f), CigarSize, ProductLook.Cigar),
            new ProductDefinition("Cigare premium", 12.90f, 5, new Color(0.30f, 0.14f, 0.10f), new Vector3(0.18f, 0.035f, 0.035f), ProductLook.Cigar),
            new ProductDefinition("Cigarillos vanille", 7.90f, 10, new Color(0.75f, 0.60f, 0.30f), new Vector3(0.12f, 0.025f, 0.025f), ProductLook.Cigar),
            new ProductDefinition("Cave à cigares", 39.90f, 2, new Color(0.38f, 0.18f, 0.12f), new Vector3(0.18f, 0.10f, 0.12f), ProductLook.Box),
            new ProductDefinition("Humidificateur", 9.90f, 6, new Color(0.52f, 0.34f, 0.18f), new Vector3(0.07f, 0.07f, 0.05f), ProductLook.Box),

            // Articles pour fumeurs
            new ProductDefinition("Briquet", 2.50f, 25, new Color(0.70f, 0.18f, 0.18f), LighterSize, ProductLook.Lighter),
            new ProductDefinition("Zippo", 18.90f, 10, new Color(0.60f, 0.62f, 0.66f), new Vector3(0.045f, 0.075f, 0.02f), ProductLook.Lighter),
            new ProductDefinition("Briquet tempête", 4.90f, 20, new Color(0.80f, 0.42f, 0.12f), new Vector3(0.05f, 0.08f, 0.025f), ProductLook.Lighter),
            new ProductDefinition("Cendrier", 7.90f, 6, new Color(0.28f, 0.30f, 0.34f), AshtraySize, ProductLook.Ashtray),
            new ProductDefinition("Cendrier de poche", 3.50f, 12, new Color(0.65f, 0.68f, 0.72f), new Vector3(0.08f, 0.025f, 0.08f), ProductLook.Ashtray),
            new ProductDefinition("Grinder", 8.90f, 10, new Color(0.38f, 0.24f, 0.55f), GrinderSize, ProductLook.Grinder),
            new ProductDefinition("Grinder métal", 14.90f, 8, new Color(0.35f, 0.38f, 0.42f), new Vector3(0.10f, 0.04f, 0.10f), ProductLook.Grinder),
            new ProductDefinition("Filtres cigarettes", 3.20f, 25, new Color(0.78f, 0.78f, 0.74f), new Vector3(0.08f, 0.06f, 0.04f), ProductLook.Box),
            new ProductDefinition("Étui à cigarettes", 5.90f, 10, new Color(0.18f, 0.16f, 0.15f), new Vector3(0.10f, 0.07f, 0.04f), ProductLook.Box),
            new ProductDefinition("Porte-cigarette", 4.50f, 12, new Color(0.72f, 0.58f, 0.25f), new Vector3(0.09f, 0.05f, 0.03f), ProductLook.Box)
        };

        private static readonly Color GroupColor = new Color(0.55f, 0.12f, 0.12f);
        private static readonly Color DetailWhite = new Color(0.94f, 0.94f, 0.90f);
        private static readonly Color DetailLight = new Color(0.82f, 0.82f, 0.78f);
        private static readonly Color DetailDark = new Color(0.18f, 0.16f, 0.15f);
        private static readonly Color DetailGold = new Color(0.82f, 0.68f, 0.30f);

        private static readonly Dictionary<string, string> Localized =
            new Dictionary<string, string>(StringComparer.Ordinal);

        private static readonly List<GameObject> CreatedPrefabs = new List<GameObject>();
        private static readonly List<Material> CreatedMaterials = new List<Material>();
        private static readonly List<Sprite> CreatedSprites = new List<Sprite>();
        private static readonly List<Texture2D> CreatedTextures = new List<Texture2D>();

        // Les prefabs, icônes et matériaux ne dépendent pas de la sauvegarde :
        // on les construit une seule fois par processus et on les réutilise si le
        // ProductListing est recréé au chargement d'une autre partie.
        private static readonly GameObject[] CachedPrefabs = new GameObject[Catalog.Length];
        private static readonly Sprite[] CachedSprites = new Sprite[Catalog.Length];
        private static readonly Dictionary<int, Material> CachedMaterials =
            new Dictionary<int, Material>();

        private static ProductListing _configured;
        private static bool _nodeCreated;
        private static GameObject _nodeObject;
        private static bool _waitingForNode;
        private static int _newTierIndex = -1;
        private static int _seafoodTierIndex = -1;
        private static Sprite _nodeIcon;

        public static bool TrySetup(ProductListing listing, out bool changed)
        {
            changed = false;

            if (!Plugin.EnableTobacco.Value)
                return false;

            if (listing == null)
                listing = ProductListing.Instance;
            if (listing == null)
                return false;

            if (_configured == listing)
            {
                bool wasCreated = _nodeCreated;
                bool ready = EnsureNodeOrRetry(listing);
                changed = ready && !wasCreated;
                return ready;
            }

            if (listing.productsData == null ||
                listing.productPlayerPricing == null || listing.tiers == null ||
                listing.unlockedProductTiers == null || listing.tierInflation == null ||
                listing.productGroups == null || listing.groupsColors == null)
            {
                return false;
            }

            // Un client qui rejoint un hôte déjà moddé reçoit les SyncVar étendues
            // avant que son propre productsData local ne soit complété. On détecte
            // ce cas pour ne pas ajouter une seconde fois le catalogue.
            bool syncAlreadyExtended =
                listing.unlockedProductTiers.Length == listing.tiers.Length + 1 &&
                listing.tierInflation.Length == listing.tiers.Length + 1 &&
                listing.productPlayerPricing.Length == listing.productsData.Length + Catalog.Length;

            if (!syncAlreadyExtended)
            {
                if (listing.productsData.Length != listing.productPlayerPricing.Length)
                {
                    Plugin.Log.LogWarning("[Tabac] Tableaux de produits incohérents, ajout annulé.");
                    return false;
                }

                if (listing.tiers.Length != listing.unlockedProductTiers.Length ||
                    listing.tiers.Length != listing.tierInflation.Length)
                {
                    Plugin.Log.LogWarning("[Tabac] Tableaux de paliers incohérents, ajout annulé.");
                    return false;
                }
            }

            if (listing.tiers.Length != listing.productGroups.Length)
            {
                Plugin.Log.LogWarning("[Tabac] Tableau des groupes incohérent, ajout annulé.");
                return false;
            }

            int seafoodIndex = FindSeafoodTier(listing);
            if (seafoodIndex < 0)
            {
                Plugin.Log.LogWarning("[Tabac] Palier 'Fruits de mer I' introuvable, ajout annulé.");
                return false;
            }

            int baseProductId = FindBaseProduct(listing);
            if (baseProductId < 0)
            {
                Plugin.Log.LogWarning("[Tabac] Aucun produit de base exploitable, ajout annulé.");
                return false;
            }

            int oldProductCount = listing.productsData.Length;
            int newTierIndex = listing.tiers.Length;
            int newGroupIndex = FindNewGroupIndex(listing);

            Stopwatch setupTimer = Stopwatch.StartNew();

            try
            {
                ProductListing.ProductData baseData = listing.productsData[baseProductId];
                ProductListing.ProductData[] products =
                    new ProductListing.ProductData[oldProductCount + Catalog.Length];

                Array.Copy(listing.productsData, products, oldProductCount);
                for (int i = 0; i < Catalog.Length; i++)
                {
                    products[oldProductCount + i] = BuildProduct(
                        baseData, oldProductCount + i, newTierIndex, Catalog[i], i);
                }

                string[] tiers = new string[newTierIndex + 1];
                Array.Copy(listing.tiers, tiers, newTierIndex);
                tiers[newTierIndex] = oldProductCount + "-" + (oldProductCount + Catalog.Length - 1);

                int[] groups = new int[newTierIndex + 1];
                Array.Copy(listing.productGroups, groups, newTierIndex);
                groups[newTierIndex] = newGroupIndex;

                Color[] colors = new Color[Mathf.Max(listing.groupsColors.Length, newGroupIndex + 1)];
                Array.Copy(listing.groupsColors, colors, listing.groupsColors.Length);
                colors[newGroupIndex] = GroupColor;

                listing.productsData = products;
                listing.tiers = tiers;
                listing.productGroups = groups;
                listing.groupsColors = colors;

                if (!syncAlreadyExtended)
                {
                    float[] pricing = new float[oldProductCount + Catalog.Length];
                    Array.Copy(listing.productPlayerPricing, pricing, oldProductCount);
                    for (int i = 0; i < Catalog.Length; i++)
                        pricing[oldProductCount + i] = Catalog[i].Price;

                    bool[] unlocked = new bool[newTierIndex + 1];
                    Array.Copy(listing.unlockedProductTiers, unlocked, newTierIndex);
                    unlocked[newTierIndex] = false;

                    float[] inflation = new float[newTierIndex + 1];
                    Array.Copy(listing.tierInflation, inflation, newTierIndex);
                    inflation[newTierIndex] = 1f;

                    // Ces trois tableaux sont des SyncVar : on passe par les propriétés
                    // côté serveur pour que les clients reçoivent la nouvelle taille.
                    if (listing.isServer)
                    {
                        listing.NetworkproductPlayerPricing = pricing;
                        listing.NetworktierInflation = inflation;
                        listing.NetworkunlockedProductTiers = unlocked;
                    }
                    else
                    {
                        listing.productPlayerPricing = pricing;
                        listing.tierInflation = inflation;
                        listing.unlockedProductTiers = unlocked;
                    }
                }

                ExtendStatistics(listing, oldProductCount + Catalog.Length);
                RegisterLocalization(newTierIndex, newGroupIndex, oldProductCount);

                Plugin.Log.LogInfo(
                    $"[Tabac] Catalogue prêt en {setupTimer.ElapsedMilliseconds} ms " +
                    "(prefabs, icônes et matériaux réutilisés quand ils existent déjà).");

                _configured = listing;
                _newTierIndex = newTierIndex;
                _seafoodTierIndex = seafoodIndex;
                _nodeCreated = false;
                _nodeObject = null;
                _waitingForNode = false;
                _nodeIcon = products[oldProductCount].productSprite;

                Plugin.Log.LogInfo(
                    $"[Tabac] Rayon ajouté : {Catalog.Length} produits, palier {newTierIndex} ('{TobaccoName}'), " +
                    $"groupe {newGroupIndex}, produits {oldProductCount}-{oldProductCount + Catalog.Length - 1}. " +
                    $"Prérequis : palier {seafoodIndex} ('Fruits de mer I').");

                bool ready = EnsureNodeOrRetry(listing);
                changed = ready;
                return ready;
            }
            catch (Exception exception)
            {
                // Les prefabs déjà construits restent en cache : une nouvelle
                // tentative (ou une autre sauvegarde) les réutilisera.
                Plugin.Log.LogError($"[Tabac] Échec de l'ajout du rayon : {exception}");
                return false;
            }
        }

        public static bool IsExtended(ProductListing listing)
        {
            return listing != null && _configured == listing;
        }

        private static bool EnsureNodeOrRetry(ProductListing listing)
        {
            bool ready = EnsureNode(listing);
            if (ready || _waitingForNode)
                return ready;

            _waitingForNode = true;
            try
            {
                listing.StartCoroutine(WaitForNode(listing));
            }
            catch (Exception)
            {
                // GameObject encore inactif pendant l'Awake : OnStartClient retentera.
                _waitingForNode = false;
            }
            return false;
        }

        private static bool EnsureNode(ProductListing listing)
        {
            if (listing == null || _configured != listing)
                return false;
            if (_nodeCreated)
            {
                // L'arbre peut être rechargé (changement de disposition) : le nœud
                // cloné appartient alors à l'ancienne scène et doit être recréé.
                if (_nodeObject != null && listing.skillTreeOBJ != null &&
                    _nodeObject.transform.parent == listing.skillTreeOBJ.transform)
                {
                    return true;
                }

                _nodeCreated = false;
                _nodeObject = null;
            }

            if (listing.skillTreeOBJ == null)
                return false;

            try
            {
                GameObject node = CreateSkillNode(listing, _seafoodTierIndex, _newTierIndex);
                if (node == null)
                    return false;

                _nodeCreated = true;
                _nodeObject = node;
                Plugin.Log.LogInfo(
                    $"[Tabac] Nœud '{TobaccoName}' ajouté à l'arbre de compétences, " +
                    $"à côté du palier {_seafoodTierIndex}.");
                return true;
            }
            catch (Exception exception)
            {
                Plugin.Log.LogError($"[Tabac] Échec de l'ajout du nœud d'arbre : {exception}");
                return false;
            }
        }

        private static IEnumerator WaitForNode(ProductListing listing)
        {
            while (listing != null && _configured == listing && !_nodeCreated)
            {
                if (listing.skillTreeOBJ != null)
                {
                    if (EnsureNode(listing))
                    {
                        listing.updateSkillState();
                        listing.updateProductList();

                        try
                        {
                            ManagerBlackboard blackboard = listing.GetComponent<ManagerBlackboard>();
                            if (blackboard != null)
                                blackboard.UpdateUnlockedFranchises();
                        }
                        catch (Exception exception)
                        {
                            // Le ManagerBlackboard peut ne pas être initialisé si le nœud
                            // est créé très tôt ; OnStartClient refera la mise à jour.
                            Plugin.Log.LogDebug($"[Tabac] Mise à jour des raccourcis différée : {exception.Message}");
                        }
                    }
                    break;
                }

                yield return null;
            }

            _waitingForNode = false;
        }

        public static void Refresh(ProductListing listing)
        {
            if (listing == null)
                return;

            bool changed;
            if (!TrySetup(listing, out changed) && IsExtended(listing))
            {
                // Les tableaux sont étendus mais le nœud n'est pas encore prêt :
                // on évite updateSkillState() qui dépasserait la liste des enfants.
                listing.updateProductList();

                try
                {
                    ManagerBlackboard blackboardNoNode = listing.GetComponent<ManagerBlackboard>();
                    if (blackboardNoNode != null)
                        blackboardNoNode.UpdateUnlockedFranchises();
                }
                catch (Exception exception)
                {
                    Plugin.Log.LogDebug($"[Tabac] Mise à jour des raccourcis différée : {exception.Message}");
                }
                return;
            }

            listing.updateSkillState();
            listing.updateProductList();

            try
            {
                ManagerBlackboard blackboard = listing.GetComponent<ManagerBlackboard>();
                if (blackboard != null)
                    blackboard.UpdateUnlockedFranchises();
            }
            catch (Exception exception)
            {
                Plugin.Log.LogDebug($"[Tabac] Mise à jour des raccourcis différée : {exception.Message}");
            }

            Plugin.Log.LogInfo("[Tabac] Interface du magasin rafraîchie.");
        }

        public static bool TryGetLocalized(string key, out string value)
        {
            if (string.IsNullOrEmpty(key))
            {
                value = null;
                return false;
            }

            return Localized.TryGetValue(key, out value);
        }

        private static int FindSeafoodTier(ProductListing listing)
        {
            string[] needles = { "Fruits de mer I", "Seafood I", "Fruits de mer 1", "Seafood 1" };

            if (LocalizationManager.instance != null)
            {
                for (int i = 0; i < listing.tiers.Length; i++)
                {
                    string name = LocalizationManager.instance.GetLocalizationString("productCategory" + i);
                    if (string.IsNullOrEmpty(name) || name == "LocError")
                        continue;

                    for (int n = 0; n < needles.Length; n++)
                    {
                        if (name.IndexOf(needles[n], StringComparison.OrdinalIgnoreCase) >= 0)
                            return i;
                    }
                }
            }

            // Dans l'asset de localisation du jeu, "Fruits de mer I" est l'index 23.
            return listing.tiers.Length > 23 ? 23 : -1;
        }

        private static int FindBaseProduct(ProductListing listing)
        {
            string[] preferredNames = { "Piles AA", "AA Batteries", "Sel", "Salt", "Savon", "Soap" };

            int fallback = -1;

            for (int i = 0; i < listing.productsData.Length; i++)
            {
                ProductListing.ProductData data = listing.productsData[i];
                if (data == null || data.productPrefab == null)
                    continue;
                if (data.productPrefab.GetComponentInChildren<MeshRenderer>(true) == null)
                    continue;

                if (fallback < 0)
                    fallback = i;

                if (LocalizationManager.instance == null)
                    continue;

                string name = LocalizationManager.instance.GetLocalizationString("product" + i);
                if (string.IsNullOrEmpty(name) || name == "LocError")
                    continue;

                for (int n = 0; n < preferredNames.Length; n++)
                {
                    if (name.IndexOf(preferredNames[n], StringComparison.OrdinalIgnoreCase) >= 0)
                        return i;
                }
            }

            return fallback;
        }

        private static int FindNewGroupIndex(ProductListing listing)
        {
            int max = -1;
            for (int i = 0; i < listing.productGroups.Length; i++)
            {
                if (listing.productGroups[i] > max)
                    max = listing.productGroups[i];
            }

            int candidate = max + 1;
            if (candidate < listing.groupsColors.Length)
                candidate = listing.groupsColors.Length;

            return candidate;
        }

        private static ProductListing.ProductData BuildProduct(
            ProductListing.ProductData baseData, int productId, int tierIndex,
            ProductDefinition definition, int catalogIndex)
        {
            return new ProductListing.ProductData
            {
                productID = productId,
                productPrefab = GetOrCreatePrefab(baseData, definition, productId, catalogIndex),
                productSprite = GetOrCreateSprite(definition, catalogIndex),
                productContainerClass = baseData.productContainerClass,
                boxClass = baseData.boxClass,
                maxItemsPerBox = definition.BoxSize,
                basePricePerUnit = definition.Price,
                productBrand = Brand,
                productTier = tierIndex,
                isStackable = true,
                colliderSize = definition.Size,
                colliderCenter = Vector3.zero,
                hasTrueCollider = false,
                trueColliderSize = definition.Size,
                productColor = definition.Color
            };
        }

        private static GameObject GetOrCreatePrefab(
            ProductListing.ProductData baseData, ProductDefinition definition,
            int productId, int catalogIndex)
        {
            GameObject cached = CachedPrefabs[catalogIndex];
            if (cached != null)
                return cached;

            GameObject built = BuildProductPrefab(baseData, definition, productId);
            CachedPrefabs[catalogIndex] = built;
            return built;
        }

        private static Sprite GetOrCreateSprite(ProductDefinition definition, int catalogIndex)
        {
            Sprite cached = CachedSprites[catalogIndex];
            if (cached != null)
                return cached;

            Sprite built = BuildProductSprite(definition);
            CachedSprites[catalogIndex] = built;
            return built;
        }

        private static GameObject BuildProductPrefab(
            ProductListing.ProductData baseData, ProductDefinition definition, int productId)
        {
            GameObject pack = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pack.name = "Tobacco_" + productId + "_" + definition.Name.Replace(' ', '_');
            pack.transform.position = new Vector3(0f, -5000f, 0f);
            pack.transform.localScale = definition.Size;

            RemoveCollider(pack);
            ApplyBaseMetadata(pack, baseData);

            // Les feuilles à rouler sont blanches : la couleur du catalogue sert
            // uniquement de liseré sur l'emballage.
            Color bodyColor = definition.Look == ProductLook.Paper
                ? new Color(0.94f, 0.94f, 0.90f)
                : definition.Color;

            MeshRenderer renderer = pack.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = GetMaterial(baseData, bodyColor);

            if (UsesCylinderBody(definition.Look))
            {
                // Le cube racine reste le conteneur (taille et orientation stables)
                // mais il devient invisible : le visuel est un cylindre enfant.
                if (renderer != null)
                    renderer.enabled = false;

                GameObject body = AddPart(
                    pack, baseData, PrimitiveType.Cylinder, Vector3.zero,
                    new Vector3(1f, 0.5f, 1f), bodyColor, "Body");

                if (definition.Look == ProductLook.Cigar)
                    body.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }

            AddProductDetails(pack, baseData, definition);

            Object.DontDestroyOnLoad(pack);
            CreatedPrefabs.Add(pack);
            return pack;
        }

        private static bool UsesCylinderBody(ProductLook look)
        {
            return look == ProductLook.Bottle || look == ProductLook.Vape ||
                   look == ProductLook.Tin || look == ProductLook.Cigar ||
                   look == ProductLook.Ashtray || look == ProductLook.Grinder;
        }

        private static void AddProductDetails(
            GameObject root, ProductListing.ProductData baseData, ProductDefinition definition)
        {
            // Palette partagée : les détails (étiquettes, capsules, liserés)
            // n'ont pas besoin d'un matériau propre à chaque produit.
            Color light = DetailLight;
            Color dark = DetailDark;
            Color white = DetailWhite;
            Color gold = DetailGold;

            switch (definition.Look)
            {
                case ProductLook.Pack:
                    AddPart(root, baseData, PrimitiveType.Cube, new Vector3(0f, 0.15f, 0f),
                        new Vector3(1.02f, 0.30f, 1.02f), white, "Band");
                    AddPart(root, baseData, PrimitiveType.Cube, new Vector3(0f, 0.47f, 0f),
                        new Vector3(1.03f, 0.10f, 1.03f), gold, "Top");
                    break;

                case ProductLook.Pouch:
                    AddPart(root, baseData, PrimitiveType.Cube, new Vector3(0f, 0.42f, 0f),
                        new Vector3(1.01f, 0.14f, 1.01f), dark, "Seal");
                    AddPart(root, baseData, PrimitiveType.Cube, new Vector3(0f, 0f, 0f),
                        new Vector3(0.70f, 0.36f, 1.03f), white, "Label");
                    break;

                case ProductLook.Tube:
                    AddPart(root, baseData, PrimitiveType.Cube, new Vector3(0f, 0.20f, 0f),
                        new Vector3(1.02f, 0.26f, 1.02f), white, "Label");
                    AddPart(root, baseData, PrimitiveType.Cube, new Vector3(0f, 0.46f, 0f),
                        new Vector3(1.03f, 0.10f, 1.03f), light, "Lid");
                    break;

                case ProductLook.Bottle:
                    AddPart(root, baseData, PrimitiveType.Cube, new Vector3(0f, 0.46f, 0f),
                        new Vector3(0.64f, 0.28f, 0.64f), dark, "Cap");
                    AddPart(root, baseData, PrimitiveType.Cube, new Vector3(0f, -0.08f, 0f),
                        new Vector3(1.03f, 0.46f, 1.03f), white, "Label");
                    break;

                case ProductLook.Vape:
                    AddPart(root, baseData, PrimitiveType.Cube, new Vector3(0f, 0.46f, 0f),
                        new Vector3(0.80f, 0.18f, 0.80f), gold, "Mouthpiece");
                    AddPart(root, baseData, PrimitiveType.Cube, new Vector3(0f, -0.46f, 0f),
                        new Vector3(0.85f, 0.10f, 0.85f), light, "Tip");
                    AddPart(root, baseData, PrimitiveType.Cube, new Vector3(0f, -0.02f, 0f),
                        new Vector3(1.04f, 0.18f, 1.04f), white, "Label");
                    break;

                case ProductLook.Tin:
                    AddPart(root, baseData, PrimitiveType.Cylinder, new Vector3(0f, 0.38f, 0f),
                        new Vector3(1.02f, 0.12f, 1.02f), light, "Lid");
                    AddPart(root, baseData, PrimitiveType.Cylinder, new Vector3(0f, 0f, 0f),
                        new Vector3(1.04f, 0.20f, 1.04f), white, "Label");
                    break;

                case ProductLook.Cigar:
                    AddPart(root, baseData, PrimitiveType.Cube, new Vector3(0f, 0f, 0f),
                        new Vector3(0.14f, 1.06f, 1.06f), gold, "Band");
                    AddPart(root, baseData, PrimitiveType.Cube, new Vector3(0.47f, 0f, 0f),
                        new Vector3(0.06f, 1.04f, 1.04f), dark, "Tip");
                    break;

                case ProductLook.Lighter:
                    AddPart(root, baseData, PrimitiveType.Cube, new Vector3(0f, 0.34f, 0f),
                        new Vector3(1.02f, 0.34f, 1.02f), light, "Cap");
                    AddPart(root, baseData, PrimitiveType.Cube, new Vector3(0.18f, 0.33f, 0f),
                        new Vector3(0.30f, 0.26f, 1.06f), dark, "Wheel");
                    break;

                case ProductLook.Ashtray:
                    AddPart(root, baseData, PrimitiveType.Cylinder, new Vector3(0f, 0.48f, 0f),
                        new Vector3(0.72f, 0.10f, 0.72f), dark, "Bowl");
                    AddPart(root, baseData, PrimitiveType.Cube, new Vector3(-0.36f, 0.55f, 0f),
                        new Vector3(0.16f, 0.16f, 0.22f), light, "Notch");
                    AddPart(root, baseData, PrimitiveType.Cube, new Vector3(0.36f, 0.55f, 0f),
                        new Vector3(0.16f, 0.16f, 0.22f), light, "Notch");
                    break;

                case ProductLook.Grinder:
                    AddPart(root, baseData, PrimitiveType.Cylinder, new Vector3(0f, 0.45f, 0f),
                        new Vector3(1.02f, 0.14f, 1.02f), dark, "Lid");
                    AddPart(root, baseData, PrimitiveType.Cylinder, new Vector3(0f, 0.56f, 0f),
                        new Vector3(0.24f, 0.08f, 0.24f), gold, "Handle");
                    break;

                case ProductLook.Paper:
                    AddPart(root, baseData, PrimitiveType.Cube, new Vector3(0f, 0.22f, 0f),
                        new Vector3(1.02f, 0.24f, 1.02f), definition.Color, "Stripe");
                    AddPart(root, baseData, PrimitiveType.Cube, new Vector3(0f, -0.20f, 0f),
                        new Vector3(0.72f, 0.14f, 1.03f), light, "Mark");
                    break;

                default:
                    AddPart(root, baseData, PrimitiveType.Cube, new Vector3(0f, 0.34f, 0f),
                        new Vector3(1.02f, 0.10f, 1.02f), dark, "Lid");
                    AddPart(root, baseData, PrimitiveType.Cube, new Vector3(0f, 0f, 0f),
                        new Vector3(0.62f, 0.34f, 1.03f), white, "Label");
                    break;
            }
        }

        private static GameObject AddPart(
            GameObject root, ProductListing.ProductData baseData, PrimitiveType primitive,
            Vector3 localPosition, Vector3 localScale, Color color, string name)
        {
            GameObject part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;

            RemoveCollider(part);
            ApplyBaseMetadata(part, baseData);

            MeshRenderer renderer = part.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = GetMaterial(baseData, color);

            return part;
        }

        private static void RemoveCollider(GameObject value)
        {
            Collider collider = value.GetComponent<Collider>();
            if (collider == null)
                return;

            // Destroy est différé : on désactive tout de suite pour que les clones
            // instantanés dans la même frame n'héritent pas d'un collider actif.
            collider.enabled = false;
            Object.Destroy(collider);
        }

        private static void ApplyBaseMetadata(GameObject value, ProductListing.ProductData baseData)
        {
            if (baseData.productPrefab == null)
                return;

            value.layer = baseData.productPrefab.layer;
            try
            {
                value.tag = baseData.productPrefab.tag;
            }
            catch (Exception)
            {
                // Le tag du prefab de base n'existe pas forcément, on garde "Untagged".
            }
        }

        private static Material GetMaterial(ProductListing.ProductData baseData, Color color)
        {
            Color32 keyColor = color;
            int key = (keyColor.r << 16) | (keyColor.g << 8) | keyColor.b;

            if (CachedMaterials.TryGetValue(key, out Material cached) && cached != null)
                return cached;

            Material created = CreateMaterial(baseData, color);
            if (created != null)
                CachedMaterials[key] = created;

            return created;
        }

        private static Material CreateMaterial(ProductListing.ProductData baseData, Color color)
        {
            Material source = null;
            if (baseData.productPrefab != null)
            {
                MeshRenderer renderer = baseData.productPrefab.GetComponentInChildren<MeshRenderer>(true);
                if (renderer != null)
                    source = renderer.sharedMaterial;
            }

            Material material = null;
            if (source != null)
            {
                try
                {
                    material = new Material(source);
                }
                catch (Exception)
                {
                    material = null;
                }
            }

            if (material == null)
            {
                Shader shader = Shader.Find("Standard");
                if (shader == null)
                    shader = Shader.Find("Legacy Shaders/Diffuse");
                if (shader == null)
                    shader = Shader.Find("Unlit/Color");
                if (shader == null)
                    shader = Shader.Find("Sprites/Default");

                if (shader != null)
                    material = new Material(shader);
            }

            if (material == null)
            {
                Plugin.Log.LogWarning("[Tabac] Aucun shader disponible pour les paquets.");
                return null;
            }

            try
            {
                material.color = color;
            }
            catch (Exception)
            {
                // Certains shaders n'exposent pas _Color : la teinte restera celle du matériau source.
            }

            if (material.HasProperty("_MainTex"))
                material.mainTexture = null;
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", null);

            CreatedMaterials.Add(material);
            return material;
        }

        private static Sprite BuildProductSprite(ProductDefinition definition)
        {
            const int size = 64;

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "TobaccoIcon_" + definition.Name.Replace(' ', '_');
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            Color32 body = definition.Color;
            Color32 light = Color.Lerp(definition.Color, Color.white, 0.55f);
            Color32 dark = Color.Lerp(definition.Color, Color.black, 0.42f);
            Color32 white = new Color32(240, 240, 236, 255);
            Color32 gold = new Color32(210, 174, 74, 255);
            Color32 border = new Color32(28, 24, 24, 255);

            Color32[] pixels = new Color32[size * size];

            switch (definition.Look)
            {
                case ProductLook.Pack:
                    FillRect(pixels, size, 15, 5, 48, 58, border);
                    FillRect(pixels, size, 17, 7, 46, 56, body);
                    FillRect(pixels, size, 17, 7, 46, 12, gold);
                    FillRect(pixels, size, 17, 21, 46, 29, white);
                    FillRect(pixels, size, 17, 21, 46, 22, light);
                    FillRect(pixels, size, 44, 12, 46, 56, light);
                    break;

                case ProductLook.Pouch:
                    FillRect(pixels, size, 13, 10, 50, 56, border);
                    FillRect(pixels, size, 15, 13, 48, 54, body);
                    FillRect(pixels, size, 15, 13, 48, 18, dark);
                    FillRect(pixels, size, 21, 29, 42, 41, white);
                    FillRect(pixels, size, 24, 32, 39, 36, light);
                    break;

                case ProductLook.Tube:
                    FillRect(pixels, size, 17, 11, 46, 54, border);
                    FillRect(pixels, size, 19, 13, 44, 52, body);
                    FillRect(pixels, size, 19, 20, 44, 27, white);
                    FillRect(pixels, size, 19, 13, 44, 17, light);
                    FillRect(pixels, size, 24, 31, 26, 48, white);
                    FillRect(pixels, size, 31, 31, 33, 48, white);
                    FillRect(pixels, size, 38, 31, 40, 48, white);
                    break;

                case ProductLook.Bottle:
                    FillRect(pixels, size, 25, 4, 39, 12, border);
                    FillRect(pixels, size, 27, 6, 37, 10, dark);
                    FillRect(pixels, size, 27, 11, 37, 21, border);
                    FillRect(pixels, size, 29, 13, 35, 20, body);
                    FillRect(pixels, size, 20, 19, 44, 57, border);
                    FillRect(pixels, size, 22, 21, 42, 55, body);
                    FillRect(pixels, size, 22, 33, 42, 45, white);
                    FillRect(pixels, size, 25, 23, 28, 30, light);
                    break;

                case ProductLook.Vape:
                    FillRect(pixels, size, 25, 8, 39, 57, border);
                    FillRect(pixels, size, 27, 10, 37, 55, body);
                    FillRect(pixels, size, 27, 3, 37, 9, gold);
                    FillRect(pixels, size, 28, 50, 36, 54, light);
                    FillRect(pixels, size, 26, 28, 38, 36, white);
                    break;

                case ProductLook.Tin:
                    FillEllipse(pixels, size, 32, 34, 21, 15, border);
                    FillEllipse(pixels, size, 32, 34, 19, 13, body);
                    FillEllipse(pixels, size, 32, 35, 15, 9, light);
                    FillEllipse(pixels, size, 32, 36, 11, 6, white);
                    break;

                case ProductLook.Cigar:
                    FillRect(pixels, size, 7, 25, 57, 39, border);
                    FillEllipse(pixels, size, 10, 32, 4, 7, border);
                    FillEllipse(pixels, size, 54, 32, 4, 7, border);
                    FillRect(pixels, size, 9, 27, 55, 37, body);
                    FillEllipse(pixels, size, 10, 32, 3, 5, body);
                    FillEllipse(pixels, size, 54, 32, 3, 5, body);
                    FillRect(pixels, size, 12, 27, 52, 29, light);
                    FillRect(pixels, size, 33, 25, 39, 39, gold);
                    FillRect(pixels, size, 55, 27, 57, 37, dark);
                    break;

                case ProductLook.Lighter:
                    FillRect(pixels, size, 19, 10, 44, 56, border);
                    FillRect(pixels, size, 21, 12, 42, 54, body);
                    FillRect(pixels, size, 21, 12, 42, 19, light);
                    FillRect(pixels, size, 27, 18, 36, 25, dark);
                    FillRect(pixels, size, 21, 45, 42, 53, dark);
                    FillRect(pixels, size, 23, 22, 25, 43, light);
                    break;

                case ProductLook.Ashtray:
                    FillEllipse(pixels, size, 32, 35, 23, 15, border);
                    FillEllipse(pixels, size, 32, 35, 21, 13, body);
                    FillEllipse(pixels, size, 32, 37, 16, 8, dark);
                    FillEllipse(pixels, size, 32, 39, 13, 5, light);
                    break;

                case ProductLook.Grinder:
                    FillEllipse(pixels, size, 32, 32, 21, 21, border);
                    FillEllipse(pixels, size, 32, 32, 19, 19, body);
                    FillEllipse(pixels, size, 32, 32, 15, 15, dark);
                    FillEllipse(pixels, size, 32, 32, 12, 12, light);
                    for (int i = 0; i < 8; i++)
                    {
                        double angle = i * Math.PI / 4.0;
                        int x = 32 + (int)Math.Round(Math.Cos(angle) * 16.0);
                        int y = 32 + (int)Math.Round(Math.Sin(angle) * 16.0);
                        FillEllipse(pixels, size, x, y, 2, 2, dark);
                    }
                    FillEllipse(pixels, size, 32, 32, 4, 4, gold);
                    break;

                case ProductLook.Paper:
                    FillRect(pixels, size, 10, 18, 53, 46, border);
                    FillRect(pixels, size, 12, 20, 51, 44, white);
                    FillRect(pixels, size, 12, 20, 51, 25, body);
                    FillRect(pixels, size, 20, 28, 43, 31, light);
                    break;

                default:
                    FillRect(pixels, size, 13, 11, 50, 52, border);
                    FillRect(pixels, size, 15, 13, 48, 50, body);
                    FillRect(pixels, size, 15, 13, 48, 19, dark);
                    FillRect(pixels, size, 21, 28, 42, 40, white);
                    break;
            }

            // Les coordonnées ci-dessus sont dessinées de haut en bas ; les textures
            // Unity ont leur origine en bas à gauche : on inverse les lignes.
            for (int y = 0; y < size / 2; y++)
            {
                int opposite = size - 1 - y;
                for (int x = 0; x < size; x++)
                {
                    Color32 swap = pixels[y * size + x];
                    pixels[y * size + x] = pixels[opposite * size + x];
                    pixels[opposite * size + x] = swap;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                size);
            sprite.name = "TobaccoIcon_" + definition.Name.Replace(' ', '_');

            CreatedTextures.Add(texture);
            CreatedSprites.Add(sprite);
            return sprite;
        }

        private static void FillRect(
            Color32[] pixels, int size, int x0, int y0, int x1, int y1, Color32 color)
        {
            if (x0 > x1)
            {
                int swap = x0;
                x0 = x1;
                x1 = swap;
            }
            if (y0 > y1)
            {
                int swap = y0;
                y0 = y1;
                y1 = swap;
            }

            x0 = Math.Max(0, x0);
            y0 = Math.Max(0, y0);
            x1 = Math.Min(size - 1, x1);
            y1 = Math.Min(size - 1, y1);

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                    pixels[y * size + x] = color;
            }
        }

        private static void FillEllipse(
            Color32[] pixels, int size, int cx, int cy, int rx, int ry, Color32 color)
        {
            int minX = Math.Max(0, cx - rx);
            int maxX = Math.Min(size - 1, cx + rx);
            int minY = Math.Max(0, cy - ry);
            int maxY = Math.Min(size - 1, cy + ry);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    double dx = (double)(x - cx) / Math.Max(1, rx);
                    double dy = (double)(y - cy) / Math.Max(1, ry);
                    if (dx * dx + dy * dy <= 1.0)
                        pixels[y * size + x] = color;
                }
            }
        }

        private static GameObject CreateSkillNode(
            ProductListing listing, int seafoodIndex, int newTierIndex)
        {
            Transform root = listing.skillTreeOBJ.transform;
            if (seafoodIndex < 0 || seafoodIndex >= root.childCount)
            {
                Plugin.Log.LogWarning("[Tabac] Nœud 'Fruits de mer I' introuvable dans l'arbre.");
                return null;
            }

            Transform original = root.GetChild(seafoodIndex);

            GameObject node = Object.Instantiate(original.gameObject, root);
            if (node == null)
                return null;

            // On configure le clone à l'arrêt : les PlayMakerFSM du nœud original
            // ne doivent pas démarrer avant d'être retirés.
            node.SetActive(false);
            node.name = newTierIndex + "_Tabac_I";

            RectTransform originalRect = original as RectTransform;
            RectTransform nodeRect = node.transform as RectTransform;
            if (originalRect != null && nodeRect != null)
            {
                // L'arbre est un canvas UI : ce sont les anchoredPosition qui comptent.
                nodeRect.anchoredPosition = FindFreeAnchoredPosition(root, originalRect);
            }
            else
            {
                Vector3 originalPosition = original.localPosition;
                Vector3 previousPosition = seafoodIndex > 0
                    ? root.GetChild(seafoodIndex - 1).localPosition
                    : originalPosition - Vector3.down * 0.6f;
                Vector3 direction = originalPosition - previousPosition;
                if (direction.sqrMagnitude < 0.0001f)
                    direction = Vector3.down * 0.6f;
                node.transform.localPosition = originalPosition + direction;
            }

            node.transform.localRotation = original.localRotation;
            node.transform.localScale = original.localScale;
            node.transform.SetSiblingIndex(newTierIndex);

            if (newTierIndex >= root.childCount || root.GetChild(newTierIndex) != node.transform)
            {
                Plugin.Log.LogWarning("[Tabac] Impossible de placer le nœud au bon index dans l'arbre.");
                Object.Destroy(node);
                return null;
            }

            // On retire les FSM du nœud cloné : elles contiennent l'index du nœud
            // "Fruits de mer I" et déclencheraient un mauvais achat.
            MonoBehaviour[] behaviours = node.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null)
                    continue;
                if (behaviour.GetType().Name != "PlayMakerFSM")
                    continue;

                behaviour.enabled = false;
                Object.Destroy(behaviour);
            }

            CanvasGroup canvasGroup = node.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = node.AddComponent<CanvasGroup>();
            // Même alpha que les nœuds verrouillés du jeu ; updateSkillState()
            // passera à 1 quand le palier sera acheté.
            canvasGroup.alpha = 0.15f;

            try
            {
                node.tag = "Interactable";
            }
            catch (Exception)
            {
                // Le tag devrait exister dans le jeu ; sinon le tooltip ne s'affichera pas.
            }

            Transform highlight = node.transform.Find("Highlight2");
            if (highlight == null)
            {
                // updateSkillState() fait un Find("Highlight2") sans vérification :
                // on garantit que le child existe pour ne pas casser la boucle.
                GameObject placeholder = new GameObject("Highlight2");
                placeholder.transform.SetParent(node.transform, false);
                highlight = placeholder.transform;
            }

            highlight.gameObject.SetActive(false);

            string seafoodKey = "productCategory" + seafoodIndex;
            string seafoodName = LocalizationManager.instance != null
                ? LocalizationManager.instance.GetLocalizationString(seafoodKey)
                : string.Empty;

            TextMeshPro[] worldTexts = node.GetComponentsInChildren<TextMeshPro>(true);
            for (int i = 0; i < worldTexts.Length; i++)
            {
                if (ShouldReplaceNodeText(worldTexts[i].text, seafoodName, seafoodKey))
                    worldTexts[i].text = TobaccoName;
            }

            TextMeshProUGUI[] uiTexts = node.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < uiTexts.Length; i++)
            {
                if (ShouldReplaceNodeText(uiTexts[i].text, seafoodName, seafoodKey))
                    uiTexts[i].text = TobaccoName;
            }

            Transform icon = node.transform.Find("Icon");
            if (icon != null && _nodeIcon != null)
            {
                Image iconImage = icon.GetComponent<Image>();
                if (iconImage != null)
                    iconImage.sprite = _nodeIcon;
            }

            Transform background = node.transform.Find("BCK");
            if (background != null)
            {
                Image backgroundImage = background.GetComponent<Image>();
                if (backgroundImage != null)
                    backgroundImage.color = GroupColor;
            }

            // updateSkillState() lit l'InteractableData du GameObject racine du nœud.
            InteractableData data = node.GetComponent<InteractableData>();
            if (data == null)
                data = node.AddComponent<InteractableData>();

            ApplyInteractableData(data, newTierIndex, seafoodIndex);

            // Si le collider/tooltip est porté par un enfant, on met aussi ses données à jour.
            InteractableData[] childData = node.GetComponentsInChildren<InteractableData>(true);
            for (int i = 0; i < childData.Length; i++)
            {
                if (childData[i] != null && childData[i] != data)
                    ApplyInteractableData(childData[i], newTierIndex, seafoodIndex);
            }

            TobaccoSkillNode handler = node.AddComponent<TobaccoSkillNode>();
            handler.TierIndex = newTierIndex;
            handler.RequiredTierIndex = seafoodIndex;

            // Les FSM sont détruites et les données prêtes : on peut afficher le nœud.
            node.SetActive(true);
            return node;
        }

        private static Vector2 FindFreeAnchoredPosition(Transform root, RectTransform original)
        {
            Vector2 basePosition = original.anchoredPosition;

            // Le nœud "Fruits de mer I" est à gauche de l'étoile décorative :
            // on descend d'un cran pour placer Tabac I sous l'étoile, sans la masquer.
            Vector2[] offsets =
            {
                new Vector2(150f, -125f),
                new Vector2(150f, -250f),
                new Vector2(0f, -125f),
                new Vector2(250f, -125f),
                new Vector2(125f, 0f),
                new Vector2(0f, -250f),
                new Vector2(-125f, 0f),
                new Vector2(0f, 125f)
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                Vector2 candidate = basePosition + offsets[i];
                bool occupied = false;

                for (int c = 0; c < root.childCount; c++)
                {
                    RectTransform other = root.GetChild(c) as RectTransform;
                    if (other == null)
                        continue;
                    if ((other.anchoredPosition - candidate).sqrMagnitude < 1f)
                    {
                        occupied = true;
                        break;
                    }
                }

                if (!occupied)
                    return candidate;
            }

            return basePosition + new Vector2(125f, 0f);
        }

        private static void ApplyInteractableData(InteractableData data, int tierIndex, int requiredTierIndex)
        {
            data.hasInteractableData = true;
            data.thisSkillIndex = tierIndex;
            data.previousSkillRequirements = new[] { requiredTierIndex };
            data.franchisePointsCost = 1;
            data.mainTitleString = "productCategory" + tierIndex;
            data.unlockStrings = new[] { "tobaccoUnlock" };
            data.requireStrings = new[] { "productCategory" + requiredTierIndex };
        }

        private static bool ShouldReplaceNodeText(string text, string seafoodName, string seafoodKey)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            if (text.Equals(seafoodKey, StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.IsNullOrEmpty(seafoodName) &&
                text.Equals(seafoodName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return text.IndexOf("Fruits de mer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("Seafood", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void ExtendStatistics(ProductListing listing, int productCount)
        {
            StatisticsManager statistics = listing.GetComponent<StatisticsManager>();
            if (statistics == null)
                return;

            ExtendList(statistics.productsSold, productCount);
            ExtendList(statistics.revenuePerProductSold, productCount);
            ExtendList(statistics.productsAcquired, productCount);
            ExtendList(statistics.costPerProductAcquired, productCount);
        }

        private static void ExtendList(List<int> list, int count)
        {
            if (list == null || list.Count == 0)
                return;

            while (list.Count < count)
                list.Add(0);
        }

        private static void RegisterLocalization(int tierIndex, int groupIndex, int firstProductId)
        {
            Localized.Clear();
            Localized["productGroup" + groupIndex] = "Tabac";
            Localized["productCategory" + tierIndex] = TobaccoName;
            Localized["tobaccoUnlock"] =
                "Débloque le rayon tabac complet : cigarettes, tabac à rouler, vapes, snus, cigares et accessoires.";

            for (int i = 0; i < Catalog.Length; i++)
                Localized["product" + (firstProductId + i)] = Catalog[i].Name;
        }

        private static void DestroyCreatedAssets()
        {
            for (int i = 0; i < CreatedPrefabs.Count; i++)
            {
                if (CreatedPrefabs[i] != null)
                    Object.Destroy(CreatedPrefabs[i]);
            }
            CreatedPrefabs.Clear();

            for (int i = 0; i < CreatedSprites.Count; i++)
            {
                if (CreatedSprites[i] != null)
                    Object.Destroy(CreatedSprites[i]);
            }
            CreatedSprites.Clear();

            for (int i = 0; i < CreatedTextures.Count; i++)
            {
                if (CreatedTextures[i] != null)
                    Object.Destroy(CreatedTextures[i]);
            }
            CreatedTextures.Clear();

            for (int i = 0; i < CreatedMaterials.Count; i++)
            {
                if (CreatedMaterials[i] != null)
                    Object.Destroy(CreatedMaterials[i]);
            }
            CreatedMaterials.Clear();

            Array.Clear(CachedPrefabs, 0, CachedPrefabs.Length);
            Array.Clear(CachedSprites, 0, CachedSprites.Length);
            CachedMaterials.Clear();
        }
    }
}
