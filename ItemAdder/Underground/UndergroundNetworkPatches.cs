using HarmonyLib;
using Mirror;

namespace ItemAdder.Underground
{
    /// <summary>
    /// NetworkClient.Shutdown() et NetworkServer.Shutdown() vident les handlers
    /// au chargement de scène. Ces postfixes réinstallent les nôtres juste après
    /// l'enregistrement des handlers vanilla, à chaque connexion / écoute.
    /// </summary>
    [HarmonyPatch(typeof(NetworkClient), "RegisterMessageHandlers")]
    internal static class UndergroundClientHandlersPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            UndergroundNetwork.InstallClientHandlers();
        }
    }

    [HarmonyPatch(typeof(NetworkServer), "RegisterMessageHandlers")]
    internal static class UndergroundServerHandlersPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            UndergroundNetwork.InstallServerHandlers();
        }
    }
}
