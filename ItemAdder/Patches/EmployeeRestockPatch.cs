using System.Collections.Generic;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace ItemAdder.Patches
{
    /// <summary>
    /// Rend les employés de réassort plus logiques : le jeu ne leur demande de
    /// ranger que les produits déjà affectés à une rangée d'étagère. Un produit
    /// qui traîne en réserve sans avoir jamais été posé n'intéresse donc
    /// personne et les employés restent inactifs.
    ///
    /// Ici, quand un employé de réassort n'a rien à faire, on lui confie le
    /// premier produit en stock absent de toutes les étagères : il réserve une
    /// rangée libre de l'étagère compatible la plus proche et va le ranger.
    /// </summary>
    [HarmonyPatch(typeof(NPC_Manager))]
    internal static class EmployeeRestockPatch
    {
        [HarmonyPatch("ReturnWeightedRestockerTask")]
        [HarmonyPostfix]
        private static void ReturnWeightedRestockerTask_Postfix(
            NPC_Manager __instance, GameObject employeeRequesterOBJ, int[] __result)
        {
            if (!Plugin.EnableSmartRestock.Value)
                return;

            // Les PNJ sont autoritaires sur l'hôte : seul le serveur décide.
            if (!NetworkServer.active)
                return;

            if (__result == null || __result.Length < 6)
                return;

            // Un vrai besoin de réassort existe déjà : on ne s'en mêle pas.
            if (__result[0] != -1)
                return;

            try
            {
                TryAssignOrphanProduct(__instance, employeeRequesterOBJ, __result);
            }
            catch (System.Exception exception)
            {
                // Un employé qui reste sans tâche est moins grave qu'une
                // exception dans sa boucle d'état.
                Plugin.Log.LogWarning("[Employés] Réassort intelligent ignoré : " + exception);
            }
        }

        private static bool TryAssignOrphanProduct(
            NPC_Manager manager, GameObject employee, int[] result)
        {
            GameObject shelvesRoot = manager.shelvesOBJ;
            GameObject storageRoot = manager.storageOBJ;

            if (shelvesRoot == null || storageRoot == null)
                return false;
            if (shelvesRoot.transform.childCount == 0 || storageRoot.transform.childCount == 0)
                return false;

            ProductListing listing = ProductListing.Instance;
            if (listing == null || listing.productsData == null)
                return false;

            Vector3 employeePosition = employee != null
                ? employee.transform.position
                : shelvesRoot.transform.position;

            HashSet<int> shelvedProducts = CollectShelvedProducts(shelvesRoot);

            for (int s = 0; s < storageRoot.transform.childCount; s++)
            {
                Data_Container storage = storageRoot.transform.GetChild(s).GetComponent<Data_Container>();
                if (storage == null || storage.productInfoArray == null)
                    continue;

                for (int row = 0; row < storage.productInfoArray.Length / 2; row++)
                {
                    int productId = storage.productInfoArray[row * 2];
                    int quantity = storage.productInfoArray[row * 2 + 1];

                    if (productId < 0 || quantity <= 0)
                        continue;
                    if (shelvedProducts.Contains(productId))
                        continue;
                    if (productId >= listing.productsData.Length)
                        continue;

                    ProductListing.ProductData data = listing.productsData[productId];
                    if (data == null)
                        continue;

                    int shelfIndex = -1;
                    int productIndex = -1;
                    if (!TryFindFreeRow(shelvesRoot, data, employeePosition, ref shelfIndex, ref productIndex))
                        continue;

                    Data_Container shelf = shelvesRoot.transform
                        .GetChild(shelfIndex).GetComponent<Data_Container>();
                    if (shelf == null || shelf.productInfoArray == null)
                        continue;

                    // Le jeu refuse de ranger un produit qui n'est pas déjà
                    // affecté à la rangée : on réserve la rangée avant de
                    // confier la tâche à l'employé.
                    int[] updated = (int[])shelf.productInfoArray.Clone();
                    if (productIndex + 1 >= updated.Length)
                        continue;

                    updated[productIndex] = productId;
                    updated[productIndex + 1] = 0;
                    shelf.NetworkproductInfoArray = updated;

                    result[0] = shelfIndex;
                    result[1] = productIndex;
                    result[2] = s;
                    result[3] = row * 2;
                    result[4] = productId;
                    result[5] = productId;

                    Plugin.Log.LogInfo(
                        $"[Employés] {ProductName(listing, productId)} : rangée libre réservée " +
                        $"sur l'étagère la plus proche, l'employé va la remplir " +
                        $"({quantity} en réserve).");
                    return true;
                }
            }

            return false;
        }

        private static HashSet<int> CollectShelvedProducts(GameObject shelvesRoot)
        {
            HashSet<int> shelved = new HashSet<int>();

            for (int i = 0; i < shelvesRoot.transform.childCount; i++)
            {
                Data_Container shelf = shelvesRoot.transform.GetChild(i).GetComponent<Data_Container>();
                if (shelf == null || shelf.productInfoArray == null)
                    continue;

                for (int row = 0; row < shelf.productInfoArray.Length / 2; row++)
                {
                    int productId = shelf.productInfoArray[row * 2];
                    if (productId >= 0)
                        shelved.Add(productId);
                }
            }

            return shelved;
        }

        private static bool TryFindFreeRow(
            GameObject shelvesRoot, ProductListing.ProductData data, Vector3 from,
            ref int shelfIndex, ref int productIndex)
        {
            float bestDistance = float.MaxValue;

            for (int i = 0; i < shelvesRoot.transform.childCount; i++)
            {
                Transform child = shelvesRoot.transform.GetChild(i);
                Data_Container shelf = child.GetComponent<Data_Container>();

                if (shelf == null || shelf.productInfoArray == null)
                    continue;

                // Seule une étagère du même type que le produit peut l'accueillir.
                if (shelf.containerClass != data.productContainerClass)
                    continue;

                // Ces étagères refusent les petits volumes : rangement impossible.
                if (shelf.isVolumeRestricted)
                    continue;

                float distance = (child.position - from).sqrMagnitude;
                if (distance >= bestDistance)
                    continue;

                for (int row = 0; row < shelf.productInfoArray.Length / 2; row++)
                {
                    if (shelf.productInfoArray[row * 2] != -1)
                        continue;

                    shelfIndex = i;
                    productIndex = row * 2;
                    bestDistance = distance;
                    break;
                }
            }

            return shelfIndex >= 0;
        }

        private static string ProductName(ProductListing listing, int productId)
        {
            LocalizationManager localization = LocalizationManager.instance;
            if (localization != null)
            {
                string name = localization.GetLocalizationString("product" + productId);
                if (!string.IsNullOrEmpty(name) && name != "LocError")
                    return name;
            }

            return "Produit " + productId;
        }
    }
}
