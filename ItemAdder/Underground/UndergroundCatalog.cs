using System;
using System.Collections.Generic;
using ItemAdder.Products;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ItemAdder.Underground
{
    /// <summary>
    /// Ajoute deux produits illicites à la fin des tableaux de ProductListing.
    /// Ils ne sont dans aucun palier : ils ne peuvent donc pas être commandés
    /// normalement. Seule la récolte du sous-sol alimente leur rayon.
    /// </summary>
    internal static class UndergroundCatalog
    {
        internal const string Brand = "Souterrain";
        internal const string ClassicName = "Herbe classique";
        internal const string PremiumName = "Herbe premium";

        internal const float ClassicBasePrice = 85f;
        internal const float PremiumBasePrice = 190f;

        private const float ClassicSeedCost = 350f;
        private const float PremiumSeedCost = 700f;

        private static ProductListing _configured;
        private static GameObject _classicPrefab;
        private static GameObject _premiumPrefab;
        private static Sprite _classicSprite;
        private static Sprite _premiumSprite;

        internal static int ClassicProductId { get; private set; } = -1;
        internal static int PremiumProductId { get; private set; } = -1;
        internal static float ClassicSeedPrice => ClassicSeedCost;
        internal static float PremiumSeedPrice => PremiumSeedCost;
        internal static bool IsReady => ClassicProductId >= 0 && PremiumProductId >= 0;

        internal static bool TrySetup(ProductListing listing)
        {
            try
            {
                return TrySetupInternal(listing);
            }
            catch (Exception exception)
            {
                // Une erreur ici ne doit jamais interrompre le chargement d'une
                // sauvegarde ou l'initialisation du jeu. Le prochain appel
                // retrouvera les produits déjà ajoutés grâce à leur marque.
                Plugin.Log.LogError("[Souterrain] Catalogue impossible : " + exception);
                return false;
            }
        }

        private static bool TrySetupInternal(ProductListing listing)
        {
            if (!Plugin.EnableUnderground.Value)
                return false;

            if (listing == null)
                listing = ProductListing.Instance;
            if (listing == null)
                return false;

            if (listing.productsData == null || listing.productPlayerPricing == null)
                return false;

            if (IsReady && _configured == listing &&
                ClassicProductId < listing.productsData.Length &&
                PremiumProductId < listing.productsData.Length &&
                listing.productsData[ClassicProductId] != null &&
                listing.productsData[ClassicProductId].productBrand == Brand)
            {
                EnsurePricingLength(listing);
                ExtendStatistics(listing, listing.productsData.Length);
                return true;
            }

            // Une sauvegarde ou une synchronisation peut avoir déjà ajouté les
            // produits : on retrouve leurs indices au lieu de les dupliquer.
            for (int i = 0; i < listing.productsData.Length; i++)
            {
                ProductListing.ProductData data = listing.productsData[i];
                if (data == null || data.productBrand != Brand)
                    continue;

                if (data.basePricePerUnit >= PremiumBasePrice - 1f)
                {
                    PremiumProductId = i;
                }
                else
                {
                    ClassicProductId = i;
                }
            }

            if (ClassicProductId >= 0 && PremiumProductId >= 0)
            {
                EnsurePricingLength(listing);
                ExtendStatistics(listing, listing.productsData.Length);
                _configured = listing;
                return true;
            }

            ProductListing.ProductData baseData = FindBaseData(listing);
            if (baseData == null)
            {
                Plugin.Log.LogWarning("[Souterrain] Aucun produit de base exploitable, catalogue annulé.");
                return false;
            }

            int firstId = listing.productsData.Length;
            int secondId = firstId + 1;

            ProductListing.ProductData classic = BuildData(
                baseData, firstId, ClassicName, ClassicBasePrice,
                new Color(0.20f, 0.58f, 0.24f), 0.10f, false);

            ProductListing.ProductData premium = BuildData(
                baseData, secondId, PremiumName, PremiumBasePrice,
                new Color(0.42f, 0.78f, 0.30f), 0.12f, true);

            ProductListing.ProductData[] products =
                new ProductListing.ProductData[firstId + 2];
            Array.Copy(listing.productsData, products, firstId);
            products[firstId] = classic;
            products[secondId] = premium;
            listing.productsData = products;

            // Les statistiques du jeu sont des listes indexées par ID produit.
            // Sans cette extension, une vente d'herbe peut dépasser la liste.
            ExtendStatistics(listing, firstId + 2);

            int pricingLength = Math.Max(listing.productPlayerPricing.Length, firstId + 2);
            float[] pricing = new float[pricingLength];
            Array.Copy(listing.productPlayerPricing, pricing,
                Math.Min(listing.productPlayerPricing.Length, pricingLength));
            pricing[firstId] = ClassicBasePrice;
            pricing[secondId] = PremiumBasePrice;

            if (listing.isServer)
                listing.NetworkproductPlayerPricing = pricing;
            else
                listing.productPlayerPricing = pricing;

            AddLocalization("product" + firstId, ClassicName);
            AddLocalization("product" + secondId, PremiumName);
            AddLocalization("undergroundbrand", Brand);

            _configured = listing;
            ClassicProductId = firstId;
            PremiumProductId = secondId;

            Plugin.Log.LogInfo(
                $"[Souterrain] Produits illicites ajoutés : {ClassicName} (id {firstId}, " +
                $"{ClassicBasePrice:0.00} $) et {PremiumName} (id {secondId}, " +
                $"{PremiumBasePrice:0.00} $).");
            return true;
        }

        /// <summary>
        /// Les herbes ne sont plus vendues sur un rayon du magasin : elles
        /// s'écoulent au contact, dans le sous-sol. Elles ne doivent donc
        /// jamais entrer dans la liste de courses des clients, sinon ceux-ci
        /// les chercheraient dans les rayons et se plaindraient qu'elles
        /// manquent. La méthode reste appelée par les patches du catalogue
        /// mais ne fait plus rien.
        /// </summary>
        internal static void RefreshAvailability()
        {
        }

        /// <summary>
        /// Un client peut recevoir les deux prix supplémentaires avant que ses
        /// produits locaux ne soient étendus (ou l'inverse). On complète le
        /// tableau manquant pour éviter un index hors limites dans l'interface.
        /// </summary>
        private static void EnsurePricingLength(ProductListing listing)
        {
            int needed = listing.productsData.Length;
            if (listing.productPlayerPricing == null ||
                listing.productPlayerPricing.Length >= needed)
                return;

            float[] pricing = new float[needed];
            Array.Copy(listing.productPlayerPricing, pricing,
                listing.productPlayerPricing.Length);

            for (int i = listing.productPlayerPricing.Length; i < needed; i++)
            {
                pricing[i] = i == PremiumProductId
                    ? PremiumBasePrice
                    : ClassicBasePrice;
            }

            if (listing.isServer)
                listing.NetworkproductPlayerPricing = pricing;
            else
                listing.productPlayerPricing = pricing;
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

        private static void SetAvailable(ProductListing listing, int productId, bool available)
        {
            if (productId < 0 || productId >= listing.productsData.Length)
                return;

            bool contains = listing.availableProducts.Contains(productId);
            if (available && !contains)
            {
                listing.availableProducts.Add(productId);
            }
            else if (!available && contains)
            {
                listing.availableProducts.Remove(productId);
            }
        }

        private static ProductListing.ProductData FindBaseData(ProductListing listing)
        {
            for (int i = 0; i < listing.productsData.Length; i++)
            {
                ProductListing.ProductData data = listing.productsData[i];
                if (data == null || data.productPrefab == null)
                    continue;
                if (data.productPrefab.GetComponentInChildren<MeshRenderer>(true) == null)
                    continue;
                return data;
            }

            return null;
        }

        private static ProductListing.ProductData BuildData(
            ProductListing.ProductData baseData, int productId, string name,
            float price, Color color, float size, bool premium)
        {
            GameObject prefab = premium
                ? GetPremiumPrefab(baseData, color, size)
                : GetClassicPrefab(baseData, color, size);

            Sprite sprite = premium
                ? GetPremiumSprite(color)
                : GetClassicSprite(color);

            return new ProductListing.ProductData
            {
                productID = productId,
                productPrefab = prefab,
                productSprite = sprite,
                productContainerClass = baseData.productContainerClass,
                boxClass = baseData.boxClass,
                maxItemsPerBox = BoxSizes.MinimumItemsPerBox,
                basePricePerUnit = price,
                productBrand = Brand,
                productTier = 0,
                isStackable = true,
                colliderSize = new Vector3(size, size * 0.8f, size),
                colliderCenter = Vector3.zero,
                hasTrueCollider = false,
                trueColliderSize = new Vector3(size, size * 0.8f, size),
                productColor = color
            };
        }

        private static GameObject GetClassicPrefab(
            ProductListing.ProductData baseData, Color color, float size)
        {
            if (_classicPrefab == null)
                _classicPrefab = CreatePrefab(baseData, color, size, "HerbeClassique", 0.8f);
            return _classicPrefab;
        }

        private static GameObject GetPremiumPrefab(
            ProductListing.ProductData baseData, Color color, float size)
        {
            if (_premiumPrefab == null)
                _premiumPrefab = CreatePrefab(baseData, color, size, "HerbePremium", 1.15f);
            return _premiumPrefab;
        }

        private static Sprite GetClassicSprite(Color color)
        {
            if (_classicSprite == null)
                _classicSprite = CreateSprite(color, false);
            return _classicSprite;
        }

        private static Sprite GetPremiumSprite(Color color)
        {
            if (_premiumSprite == null)
                _premiumSprite = CreateSprite(color, true);
            return _premiumSprite;
        }

        /// <summary>
        /// Bourgeon en vrai volume : une tête bosselée générée sommet par sommet,
        /// des renflements secondaires et quelques feuilles, au lieu d'une sphère
        /// et de cubes.
        /// </summary>
        private static GameObject CreatePrefab(
            ProductListing.ProductData baseData, Color color, float size,
            string name, float leafScale)
        {
            GameObject root = new GameObject("Underground_" + name);
            root.transform.position = new Vector3(0f, -5000f, 0f);
            root.transform.localScale = new Vector3(size, size * 0.8f, size);

            CopyMetadata(root, baseData);

            MeshFilter filter = root.AddComponent<MeshFilter>();
            filter.sharedMesh = ProductMeshes.Blob("herb_core", 0.20f, 3);
            root.AddComponent<MeshRenderer>();
            SetMaterial(root, color);

            Vector3[] bumps =
            {
                new Vector3(0.30f, 0.22f, 0.10f),
                new Vector3(-0.26f, 0.26f, -0.12f),
                new Vector3(0.16f, -0.28f, 0.22f),
                new Vector3(-0.18f, -0.10f, -0.30f),
                new Vector3(0.02f, 0.34f, 0.26f)
            };
            float[] bumpSize = { 0.52f, 0.48f, 0.54f, 0.46f, 0.44f };

            for (int i = 0; i < bumps.Length; i++)
            {
                AddPart(root, baseData,
                    ProductMeshes.Blob("herb_bump", 0.24f, 11 + i * 7),
                    bumps[i], Vector3.one * bumpSize[i],
                    Quaternion.Euler(i * 47f, i * 63f, i * 29f),
                    Color.Lerp(color, Color.white, i % 2 == 0 ? 0.10f : 0.02f));
            }

            for (int i = 0; i < 3; i++)
            {
                AddPart(root, baseData,
                    ProductMeshes.Blob("herb_leaf", 0.07f, 41 + i * 5),
                    new Vector3(
                        Mathf.Cos(i * 2.1f) * 0.38f,
                        0.30f + i * 0.05f,
                        Mathf.Sin(i * 2.1f) * 0.38f),
                    new Vector3(0.50f * leafScale, 0.08f, 0.30f * leafScale),
                    Quaternion.Euler(0f, i * 70f, 22f * leafScale),
                    Color.Lerp(color, Color.white, 0.30f));
            }

            Object.DontDestroyOnLoad(root);
            return root;
        }

        private static Sprite CreateSprite(Color color, bool premium)
        {
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = premium ? "UndergroundIcon_Premium" : "UndergroundIcon_Classic";
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            Color32[] pixels = new Color32[size * size];
            Color32 clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = clear;

            Color32 body = color;
            Color32 dark = Color.Lerp(color, Color.black, 0.45f);
            Color32 light = Color.Lerp(color, Color.white, 0.35f);
            Color32 outline = new Color32(24, 34, 20, 255);

            FillEllipse(pixels, size, 32, 26, 19, 17, outline);
            FillEllipse(pixels, size, 32, 26, 17, 15, body);
            FillEllipse(pixels, size, 27, 31, 7, 6, light);
            FillEllipse(pixels, size, 38, 22, 6, 5, dark);

            for (int i = 0; i < 6; i++)
            {
                int x = 18 + i * 6;
                int y = 42 + (i % 2) * 4;
                FillEllipse(pixels, size, x, y, 6, 4, outline);
                FillEllipse(pixels, size, x, y, 5, 3, i % 2 == 0 ? light : body);
            }

            if (premium)
            {
                FillRect(pixels, size, 27, 50, 37, 53, new Color32(232, 202, 94, 255));
                FillRect(pixels, size, 30, 47, 34, 50, new Color32(232, 202, 94, 255));
            }

            // Unity utilise l'origine en bas à gauche.
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
            sprite.name = texture.name;
            return sprite;
        }

        private static void AddLocalization(string key, string value)
        {
            LocalizationManager localization = LocalizationManager.instance;
            if (localization == null || string.IsNullOrEmpty(key))
                return;

            if (localization.LocalizationDictionary == null ||
                localization.LocalizationDictionary.ContainsKey(key))
                return;

            try
            {
                localization.AddDictionaryEntry(key, value);
            }
            catch (Exception exception)
            {
                Plugin.Log.LogWarning("[Souterrain] Localisation non ajoutée : " + exception.Message);
            }
        }

        private static void AddPart(
            GameObject root, ProductListing.ProductData baseData, Mesh mesh,
            Vector3 localPosition, Vector3 localScale, Quaternion localRotation,
            Color color)
        {
            if (mesh == null)
                return;

            GameObject part = new GameObject("Bud");
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.transform.localRotation = localRotation;

            MeshFilter filter = part.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            part.AddComponent<MeshRenderer>();
            SetMaterial(part, color);

            CopyMetadata(part, baseData);
        }

        private static void CopyMetadata(GameObject value, ProductListing.ProductData baseData)
        {
            if (baseData == null || baseData.productPrefab == null)
                return;

            value.layer = baseData.productPrefab.layer;
            try
            {
                value.tag = baseData.productPrefab.tag;
            }
            catch
            {
                // Le tag du prefab de base n'est pas obligatoirement défini.
            }
        }

        private static void SetMaterial(GameObject value, Color color)
        {
            MeshRenderer renderer = value.GetComponent<MeshRenderer>();
            if (renderer == null)
                return;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            if (shader == null)
                return;

            Material material = new Material(shader);
            try
            {
                material.color = color;
            }
            catch
            {
                // Certains shaders n'exposent pas _Color.
            }

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_MainTex"))
                material.mainTexture = null;
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", null);

            // Bourgeon bosselé : les deux faces sont rendues pour éviter qu'un
            // renflement mal orienté ne crée un trou visuel.
            if (material.HasProperty("_Cull"))
                material.SetFloat("_Cull", 0f);

            renderer.sharedMaterial = material;
            Object.DontDestroyOnLoad(material);
        }

        private static void FillRect(
            Color32[] pixels, int size, int x0, int y0, int x1, int y1, Color32 color)
        {
            x0 = Math.Max(0, Math.Min(x0, x1));
            y0 = Math.Max(0, Math.Min(y0, y1));
            x1 = Math.Min(size - 1, Math.Max(x0, x1));
            y1 = Math.Min(size - 1, Math.Max(y0, y1));

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
    }
}
