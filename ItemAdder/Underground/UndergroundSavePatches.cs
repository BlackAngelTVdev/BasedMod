using System;
using HarmonyLib;

namespace ItemAdder.Underground
{
    /// <summary>
    /// Le jeu copie les tableaux de prix sauvegardés dans ceux du ProductListing
    /// sans vérifier leurs tailles. Comme le mod ajoute des produits, une
    /// sauvegarde peut contenir un tableau plus long que la cible au moment du
    /// chargement, ce qui provoque un IndexOutOfRangeException.
    ///
    /// On copie seulement ce qui rentre dans le tableau actuel. Il ne faut
    /// surtout pas créer les produits du sous-sol ici : ce hook s'exécute avant
    /// que les catalogues (Tabac puis sous-sol) ne soient prêts, et le faire
    /// décalerait l'ordre des produits et des prix de la sauvegarde.
    /// </summary>
    [HarmonyPatch(typeof(SaveBehaviour), nameof(SaveBehaviour.LoadProductValues))]
    internal static class SaveBehaviour_Underground_LoadProductValues_Patch
    {
        [HarmonyPrefix]
        private static bool Prefix(
            SaveBehaviour __instance,
            float[] pPlayerPricingArray,
            float[] tInflationArray,
            bool[] uProductTiersArray)
        {
            ProductListing listing = null;
            if (__instance != null && __instance.gameDataOBJ != null)
                listing = __instance.gameDataOBJ.GetComponent<ProductListing>();
            if (listing == null)
                listing = ProductListing.Instance;
            if (listing == null)
                return true;

            Copy(pPlayerPricingArray, listing.productPlayerPricing);
            Copy(tInflationArray, listing.tierInflation);
            Copy(uProductTiersArray, listing.unlockedProductTiers);
            return false;
        }

        private static void Copy(float[] source, float[] target)
        {
            if (source == null || target == null)
                return;

            Array.Copy(source, target, Math.Min(source.Length, target.Length));
        }

        private static void Copy(bool[] source, bool[] target)
        {
            if (source == null || target == null)
                return;

            Array.Copy(source, target, Math.Min(source.Length, target.Length));
        }
    }
}
