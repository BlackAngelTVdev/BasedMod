using HarmonyLib;

namespace MonSupermarketMod.Patches
{
    [HarmonyPatch(typeof(GameData))]
    internal static class GameDataPatch
    {
        // Postfix sur une méthode Unity privée : on utilise donc son nom sous forme de chaîne.
        [HarmonyPatch("Awake")]
        [HarmonyPostfix]
        private static void Awake_Postfix(GameData __instance)
        {
            Plugin.Log.LogInfo(
                $"[GameData] Initialisé — jour {__instance.gameDay}, fonds {__instance.gameFunds:0.00}$.");
        }

        // Exemple d'interception avec modification d'un paramètre (ref).
        [HarmonyPatch(nameof(GameData.CmdAlterFunds))]
        [HarmonyPrefix]
        private static void CmdAlterFunds_Prefix(ref float fundsToAdd)
        {
            float multiplier = Plugin.FundsMultiplier.Value;
            Plugin.Log.LogDebug($"[GameData] CmdAlterFunds({fundsToAdd:0.00}$)");

            if (multiplier == 1f)
                return;

            fundsToAdd *= multiplier;
            Plugin.Log.LogInfo(
                $"[GameData] CmdAlterFunds modifié par x{multiplier:0.##} -> {fundsToAdd:0.00}$.");
        }
    }
}
