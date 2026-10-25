using TMPro;
using Unity.AI.Navigation;
using UnityEngine;

namespace ItemAdder.Underground
{
    /// <summary>
    /// Trappe du sous-sol : panneau ouvrable, trou et lien de navigation.
    /// L'entrée et la sortie se font par la touche d'interaction (E), pilotée
    /// par <see cref="UndergroundManager"/> : aucun déclencheur physique, donc
    /// plus de descente qui ne se déclenche pas quand le joueur est déjà dans
    /// la zone ou quand la trappe touche un mur.
    /// </summary>
    internal sealed class BasementDoor : MonoBehaviour
    {
        internal GameObject Panel;
        internal GameObject Hole;
        internal TextMeshPro Label;
        internal NavMeshLink Link;

        internal bool IsOpen { get; private set; } = true;

        internal void SetOpen(bool open)
        {
            IsOpen = open;

            if (Panel != null)
                Panel.SetActive(!open);
            if (Hole != null)
                Hole.SetActive(open);
            if (Link != null)
                Link.activated = open;

            if (Label != null)
                Label.text = open
                    ? "[E] Descendre\n[F] Fermer la trappe"
                    : "[E] Ouvrir la trappe";
        }
    }
}
