using Rewired;
using UnityEngine;

namespace MonSupermarketMod.Tobacco
{
    /// <summary>
    /// Gère le clic sur le nœud "Tabac I" ajouté à l'arbre de compétences.
    /// Le nœud vanilla cloné est fourni avec ses PlayMakerFSM désactivés :
    /// on gère donc l'achat nous-mêmes pour ne pas dépendre de variables FSM codées en dur.
    /// </summary>
    public class TobaccoSkillNode : MonoBehaviour
    {
        public int TierIndex;
        public int RequiredTierIndex;

        private Player _player;
        private float _cooldown;

        private void Start()
        {
            _player = ReInput.players.GetPlayer(0);
        }

        private void Update()
        {
            if (_cooldown > 0f)
            {
                _cooldown -= Time.deltaTime;
                return;
            }

            GameData data = GameData.Instance;
            if (data == null)
                return;

            // Le jeu récupère ProductListing sur le GameObject de GameData :
            // on fait pareil pour éviter de viser une ancienne instance.
            ProductListing listing = data.GetComponent<ProductListing>();
            if (listing == null)
                listing = ProductListing.Instance;
            if (listing == null || listing.unlockedProductTiers == null)
                return;

            if (TierIndex < 0 || TierIndex >= listing.unlockedProductTiers.Length)
                return;

            // Déjà acheté : plus rien à faire.
            if (listing.unlockedProductTiers[TierIndex])
                return;

            // Le palier "Fruits de mer I" doit être débloqué avant "Tabac I".
            if (RequiredTierIndex < 0 || RequiredTierIndex >= listing.unlockedProductTiers.Length)
                return;
            if (!listing.unlockedProductTiers[RequiredTierIndex])
                return;

            if (data.gameFranchisePoints < 1)
                return;

            if (_player == null)
                _player = ReInput.players.GetPlayer(0);
            if (_player == null || !_player.GetButtonDown("Main Action"))
                return;

            Camera camera = Camera.main;
            if (camera == null)
                return;

            RaycastHit hit;
            if (!Physics.Raycast(camera.transform.position, camera.transform.forward, out hit, 4f))
                return;

            Transform hitTransform = hit.transform;
            if (hitTransform != transform && !hitTransform.IsChildOf(transform))
                return;

            data.CmdAcquireFranchise(TierIndex);
            _cooldown = 0.5f;

            Plugin.Log.LogInfo("[Tabac] Achat du palier Tabac I demandé.");
        }
    }
}
