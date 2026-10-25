namespace ItemAdder.Products
{
    /// <summary>
    /// Contenance minimale d'un colis commandé. Seul le nombre de pièces
    /// change : les prix unitaires, les paliers et les rayons restent ceux
    /// du jeu. Le prix d'un colis vaut prix unitaire x contenance, il suit
    /// donc mécaniquement la nouvelle contenance.
    /// </summary>
    internal static class BoxSizes
    {
        internal const int MinimumItemsPerBox = 50;

        /// <summary>
        /// Remonte à <see cref="MinimumItemsPerBox"/> la contenance de tous les
        /// produits qui en comptent moins (vanilla, tabac et herbes).
        /// Idempotent : peut être appelé à chaque chargement.
        /// </summary>
        internal static void EnsureMinimum(ProductListing listing)
        {
            if (listing == null)
                listing = ProductListing.Instance;
            if (listing == null)
                return;

            EnsureMinimum(listing.productsData);
        }

        /// <summary>
        /// Variante sans objet Unity : applique la contenance minimale à un
        /// tableau de produits et renvoie le nombre de produits modifiés.
        /// </summary>
        internal static int EnsureMinimum(ProductListing.ProductData[] products)
        {
            if (products == null)
                return 0;

            int changed = 0;

            for (int i = 0; i < products.Length; i++)
            {
                ProductListing.ProductData product = products[i];
                if (product == null || product.maxItemsPerBox >= MinimumItemsPerBox)
                    continue;

                product.maxItemsPerBox = MinimumItemsPerBox;
                changed++;
            }

            if (changed > 0)
            {
                Plugin.Log.LogInfo(
                    $"[Colis] {changed} produit(s) passés à {MinimumItemsPerBox} pièces par colis " +
                    "(prix unitaires et paliers inchangés).");
            }

            return changed;
        }
    }
}
