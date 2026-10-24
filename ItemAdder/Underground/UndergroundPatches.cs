using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ItemAdder.Underground
{
    [HarmonyPatch(typeof(ProductListing), nameof(ProductListing.OnStartClient))]
    internal static class ProductListing_Underground_StartClient_Patch
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Low)]
        private static void Postfix(ProductListing __instance)
        {
            UndergroundCatalog.TrySetup(__instance);
            UndergroundCatalog.RefreshAvailability();
        }
    }

    [HarmonyPatch(typeof(ProductListing), nameof(ProductListing.updateProductList))]
    internal static class ProductListing_Underground_ProductList_Patch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            UndergroundCatalog.RefreshAvailability();
        }
    }

    [HarmonyPatch(typeof(Builder_Main), "InCorrectBounds")]
    internal static class Builder_Main_Underground_Bounds_Patch
    {
        private static readonly FieldInfo DummyField =
            AccessTools.Field(typeof(Builder_Main), "dummyOBJ");

        [HarmonyPrefix]
        private static bool Prefix(Builder_Main __instance, ref bool __result)
        {
            GameObject dummy = DummyField?.GetValue(__instance) as GameObject;
            if (dummy == null || !UndergroundManager.IsInBasement(dummy.transform.position))
                return true;

            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(Builder_Main), "CheckCorrectGround")]
    internal static class Builder_Main_Underground_Ground_Patch
    {
        private static readonly FieldInfo DummyField =
            AccessTools.Field(typeof(Builder_Main), "dummyOBJ");

        [HarmonyPrefix]
        private static bool Prefix(Builder_Main __instance, ref bool __result)
        {
            GameObject dummy = DummyField?.GetValue(__instance) as GameObject;
            if (dummy == null || !UndergroundManager.IsInBasement(dummy.transform.position))
                return true;

            __result = UndergroundManager.HasBasementFloorBelow(dummy.transform.position);
            return false;
        }
    }

    [HarmonyPatch(typeof(Builder_Main), "RaycastCheck")]
    internal static class Builder_Main_Underground_Raycast_Patch
    {
        private static readonly FieldInfo DummyField =
            AccessTools.Field(typeof(Builder_Main), "dummyOBJ");

        [HarmonyPrefix]
        private static bool Prefix(Builder_Main __instance, ref bool __result)
        {
            GameObject dummy = DummyField?.GetValue(__instance) as GameObject;
            if (dummy == null || !UndergroundManager.IsInBasement(dummy.transform.position))
                return true;

            __result = true;
            return false;
        }
    }
}
