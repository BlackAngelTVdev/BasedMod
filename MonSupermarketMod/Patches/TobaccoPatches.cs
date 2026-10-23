using HarmonyLib;
using MonSupermarketMod.Tobacco;

namespace MonSupermarketMod.Patches
{
    /// <summary>
    /// Étend les tableaux dès l'Awake du ProductListing, avant que SaveBehaviour
    /// ne recharge une sauvegarde contenant le palier Tabac (sinon la boucle de
    /// chargement écrirait 56 entrées dans un tableau de 55).
    /// </summary>
    [HarmonyPatch(typeof(ProductListing), "Awake")]
    internal static class ProductListing_Awake_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(ProductListing __instance)
        {
            bool changed;
            if (TobaccoManager.TrySetup(__instance, out changed) && changed)
                TobaccoManager.Refresh(__instance);
        }
    }

    [HarmonyPatch(typeof(ProductListing), nameof(ProductListing.OnStartClient))]
    internal static class ProductListing_OnStartClient_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(ProductListing __instance)
        {
            bool changed;
            if (TobaccoManager.TrySetup(__instance, out changed) && changed)
                TobaccoManager.Refresh(__instance);
        }
    }

    [HarmonyPatch(typeof(ProductListing), nameof(ProductListing.updateSkillState))]
    internal static class ProductListing_UpdateSkillState_Patch
    {
        [HarmonyPrefix]
        private static bool Prefix(ProductListing __instance)
        {
            bool changed;
            if (!TobaccoManager.TrySetup(__instance, out changed) &&
                TobaccoManager.IsExtended(__instance))
            {
                // Tableaux étendus mais nœud pas encore créé : ne pas dépasser
                // la liste des enfants de skillTreeOBJ.
                return false;
            }

            // Garde-fou supplémentaire : si une synchronisation laisse
            // unlockedProductTiers plus long que la liste des enfants, on
            // saute la mise à jour au lieu de lever une IndexOutOfRangeException.
            if (__instance.skillTreeOBJ != null && __instance.unlockedProductTiers != null &&
                __instance.unlockedProductTiers.Length > __instance.skillTreeOBJ.transform.childCount)
            {
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(ProductListing), nameof(ProductListing.updateProductList))]
    internal static class ProductListing_UpdateProductList_Patch
    {
        [HarmonyPrefix]
        private static bool Prefix(ProductListing __instance)
        {
            bool changed;
            TobaccoManager.TrySetup(__instance, out changed);

            if (__instance.unlockedProductTiers != null && __instance.tiers != null &&
                __instance.unlockedProductTiers.Length > __instance.tiers.Length)
            {
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(ManagerBlackboard), nameof(ManagerBlackboard.UpdateUnlockedFranchises))]
    internal static class ManagerBlackboard_UpdateUnlockedFranchises_Patch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            ProductListing listing = ProductListing.Instance;
            if (listing == null || listing.unlockedProductTiers == null ||
                listing.productGroups == null)
            {
                return true;
            }

            // Pendant une synchronisation, unlockedProductTiers peut être plus
            // long que productGroups : on évite un accès hors tableau.
            return listing.unlockedProductTiers.Length <= listing.productGroups.Length;
        }
    }

    [HarmonyPatch(typeof(LocalizationManager), nameof(LocalizationManager.GetLocalizationString))]
    internal static class LocalizationManager_GetLocalizationString_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(string key, ref string __result)
        {
            string value;
            if (TobaccoManager.TryGetLocalized(key, out value))
                __result = value;
        }
    }

    /// <summary>
    /// Garde-fou : un même palier ne peut pas être acheté deux fois dans la même frame
    /// (le clic du nœud Tabac et un éventuel FSM résiduel pourraient sinon tous deux
    /// consommer un point de franchise).
    /// </summary>
    [HarmonyPatch(typeof(GameData), "UserCode_CmdAcquireFranchise__Int32")]
    internal static class GameData_AcquireFranchise_Patch
    {
        [HarmonyPrefix]
        private static bool Prefix(GameData __instance, int franchiseIndex)
        {
            ProductListing listing = __instance.GetComponent<ProductListing>();
            if (listing == null || listing.unlockedProductTiers == null)
                return true;

            if (franchiseIndex < 0 || franchiseIndex >= listing.unlockedProductTiers.Length)
                return false;

            return !listing.unlockedProductTiers[franchiseIndex];
        }
    }
}
