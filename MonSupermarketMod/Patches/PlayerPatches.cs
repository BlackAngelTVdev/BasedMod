using HarmonyLib;
using StarterAssets;

namespace MonSupermarketMod.Patches
{
    [HarmonyPatch(typeof(FirstPersonController))]
    internal static class PlayerPatches
    {
        // Start() est privée dans Unity.StarterAssets : on la cible par son nom.
        // Postfix : les valeurs sérialisées par Unity sont déjà appliquées.
        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        private static void Start_Postfix(FirstPersonController __instance)
        {
            if (!Plugin.EnablePlayerTweaks.Value)
            {
                Plugin.Log.LogInfo("[Joueur] Modifications vitesse/saut désactivées.");
                return;
            }

            float speedMultiplier = Plugin.SpeedMultiplier.Value;
            float jumpMultiplier = Plugin.JumpMultiplier.Value;

            // Move() lit ces champs à chaque frame et borne _speed à SprintSpeed :
            // les multiplier ensemble garde un comportement cohérent (pas de bug de clamp).
            __instance.MoveSpeed *= speedMultiplier;
            __instance.SprintSpeed *= speedMultiplier;
            __instance.CrouchSpeed *= speedMultiplier;

            // JumpAndGravity() calcule _verticalVelocity = sqrt(JumpHeight * -2 * Gravity) :
            // JumpHeight est donc directement la hauteur d'apex, le multiplicateur est exact.
            __instance.JumpHeight *= jumpMultiplier;

            Plugin.Log.LogInfo(
                $"[Joueur] Vitesse x{speedMultiplier:0.##} — marche {__instance.MoveSpeed:0.##}, " +
                $"sprint {__instance.SprintSpeed:0.##}, accroupi {__instance.CrouchSpeed:0.##} | " +
                $"saut x{jumpMultiplier:0.##} — hauteur {__instance.JumpHeight:0.##} m.");
        }
    }
}
