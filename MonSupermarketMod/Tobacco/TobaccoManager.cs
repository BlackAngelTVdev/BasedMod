using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MonSupermarketMod.Tobacco
{
    /// <summary>
    /// Ajoute un rayon "Tabac" au jeu, sur le modèle des rayons déjà présents :
    /// - quatre produits (cigarettes, menthol, cigarillos, tabac à rouler) ;
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

        private static readonly Vector3 PackSize = new Vector3(0.09f, 0.12f, 0.03f);

        private static readonly string[] ProductNames =
        {
            "Cigarettes Classic",
            "Cigarettes Menthol",
            "Cigarillos",
            "Tabac à rouler"
        };

        private static readonly float[] ProductPrices = { 9.50f, 9.90f, 7.50f, 14.00f };
        private static readonly int[] BoxSizes = { 10, 10, 10, 5 };

        private static readonly Color[] PackColors =
        {
            new Color(0.72f, 0.08f, 0.10f),
            new Color(0.05f, 0.45f, 0.25f),
            new Color(0.35f, 0.18f, 0.08f),
            new Color(0.10f, 0.25f, 0.60f)
        };

        private static readonly Color GroupColor = new Color(0.55f, 0.12f, 0.12f);

        private static readonly Dictionary<string, string> Localized =
            new Dictionary<string, string>(StringComparer.Ordinal);

        private static readonly List<GameObject> CreatedPrefabs = new List<GameObject>();
        private static readonly List<Material> CreatedMaterials = new List<Material>();
        private static readonly List<Sprite> CreatedSprites = new List<Sprite>();
        private static readonly List<Texture2D> CreatedTextures = new List<Texture2D>();

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
            // ce cas pour ne pas ajouter une seconde fois les quatre produits.
            bool syncAlreadyExtended =
                listing.unlockedProductTiers.Length == listing.tiers.Length + 1 &&
                listing.tierInflation.Length == listing.tiers.Length + 1 &&
                listing.productPlayerPricing.Length == listing.productsData.Length + ProductNames.Length;

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

            DestroyCreatedAssets();

            try
            {
                ProductListing.ProductData baseData = listing.productsData[baseProductId];
                ProductListing.ProductData[] products =
                    new ProductListing.ProductData[oldProductCount + ProductNames.Length];

                Array.Copy(listing.productsData, products, oldProductCount);
                for (int i = 0; i < ProductNames.Length; i++)
                {
                    products[oldProductCount + i] = BuildProduct(
                        baseData, oldProductCount + i, newTierIndex, i);
                }

                string[] tiers = new string[newTierIndex + 1];
                Array.Copy(listing.tiers, tiers, newTierIndex);
                tiers[newTierIndex] = oldProductCount + "-" + (oldProductCount + ProductNames.Length - 1);

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
                    float[] pricing = new float[oldProductCount + ProductNames.Length];
                    Array.Copy(listing.productPlayerPricing, pricing, oldProductCount);
                    for (int i = 0; i < ProductNames.Length; i++)
                        pricing[oldProductCount + i] = ProductPrices[i];

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

                ExtendStatistics(listing, oldProductCount + ProductNames.Length);
                RegisterLocalization(newTierIndex, newGroupIndex, oldProductCount);

                _configured = listing;
                _newTierIndex = newTierIndex;
                _seafoodTierIndex = seafoodIndex;
                _nodeCreated = false;
                _nodeObject = null;
                _waitingForNode = false;
                _nodeIcon = products[oldProductCount].productSprite;

                Plugin.Log.LogInfo(
                    $"[Tabac] Rayon ajouté : palier {newTierIndex} ('{TobaccoName}'), " +
                    $"groupe {newGroupIndex}, produits {oldProductCount}-{oldProductCount + ProductNames.Length - 1}. " +
                    $"Prérequis : palier {seafoodIndex} ('Fruits de mer I').");

                bool ready = EnsureNodeOrRetry(listing);
                changed = ready;
                return ready;
            }
            catch (Exception exception)
            {
                Plugin.Log.LogError($"[Tabac] Échec de l'ajout du rayon : {exception}");
                DestroyCreatedAssets();
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
            ProductListing.ProductData baseData, int productId, int tierIndex, int variant)
        {
            return new ProductListing.ProductData
            {
                productID = productId,
                productPrefab = BuildPackPrefab(baseData, PackColors[variant], ProductNames[variant]),
                productSprite = BuildPackSprite(PackColors[variant], ProductNames[variant]),
                productContainerClass = baseData.productContainerClass,
                boxClass = baseData.boxClass,
                maxItemsPerBox = BoxSizes[variant],
                basePricePerUnit = ProductPrices[variant],
                productBrand = Brand,
                productTier = tierIndex,
                isStackable = true,
                colliderSize = PackSize,
                colliderCenter = Vector3.zero,
                hasTrueCollider = false,
                trueColliderSize = PackSize,
                productColor = PackColors[variant]
            };
        }

        private static GameObject BuildPackPrefab(
            ProductListing.ProductData baseData, Color color, string name)
        {
            GameObject pack = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pack.name = "TobaccoPack_" + name.Replace(' ', '_');
            pack.transform.position = new Vector3(0f, -5000f, 0f);
            pack.transform.localScale = PackSize;

            Collider packCollider = pack.GetComponent<Collider>();
            if (packCollider != null)
            {
                // Destroy est différé : on désactive tout de suite pour que les clones
                // instantanés dans la même frame n'héritent pas d'un collider actif.
                packCollider.enabled = false;
                Object.Destroy(packCollider);
            }

            MeshRenderer renderer = pack.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = CreateMaterial(baseData, color);

            if (baseData.productPrefab != null)
            {
                pack.layer = baseData.productPrefab.layer;
                try
                {
                    pack.tag = baseData.productPrefab.tag;
                }
                catch (Exception)
                {
                    // Le tag du prefab de base n'existe pas forcément, on garde "Untagged".
                }
            }

            GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stripe.name = "Stripe";
            stripe.transform.SetParent(pack.transform, false);
            stripe.transform.localPosition = new Vector3(0f, 0.32f, 0f);
            stripe.transform.localScale = new Vector3(1.02f, 0.16f, 1.02f);
            stripe.layer = pack.layer;

            Collider stripeCollider = stripe.GetComponent<Collider>();
            if (stripeCollider != null)
            {
                stripeCollider.enabled = false;
                Object.Destroy(stripeCollider);
            }

            MeshRenderer stripeRenderer = stripe.GetComponent<MeshRenderer>();
            if (stripeRenderer != null)
                stripeRenderer.sharedMaterial = CreateMaterial(baseData, new Color(0.94f, 0.94f, 0.90f));

            Object.DontDestroyOnLoad(pack);
            CreatedPrefabs.Add(pack);
            return pack;
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

        private static Sprite BuildPackSprite(Color color, string name)
        {
            const int size = 64;

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "TobaccoIcon_" + name.Replace(' ', '_');
            texture.filterMode = FilterMode.Point;

            Color32 body = color;
            Color32 white = new Color32(240, 240, 236, 255);
            Color32 gold = new Color32(210, 174, 74, 255);
            Color32 border = new Color32(28, 24, 24, 255);

            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Color32 pixel = body;

                    if (y >= 44 && y <= 52)
                        pixel = white;
                    if (y >= 60)
                        pixel = gold;
                    if (x < 2 || x > size - 3 || y < 2 || y > size - 3)
                        pixel = border;

                    pixels[y * size + x] = pixel;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                size);
            sprite.name = "TobaccoIcon_" + name.Replace(' ', '_');

            CreatedTextures.Add(texture);
            CreatedSprites.Add(sprite);
            return sprite;
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
            Vector2[] offsets =
            {
                new Vector2(125f, 0f),
                new Vector2(0f, -125f),
                new Vector2(250f, 0f),
                new Vector2(0f, -250f),
                new Vector2(-125f, 0f),
                new Vector2(0f, 125f),
                new Vector2(375f, 0f),
                new Vector2(-250f, 0f)
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
                "Débloque le rayon tabac : cigarettes, cigarillos et tabac à rouler.";

            for (int i = 0; i < ProductNames.Length; i++)
                Localized["product" + (firstProductId + i)] = ProductNames[i];
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
        }
    }
}
