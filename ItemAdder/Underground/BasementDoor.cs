using StarterAssets;
using TMPro;
using Unity.AI.Navigation;
using UnityEngine;

namespace ItemAdder.Underground
{
    internal sealed class BasementDoor : MonoBehaviour
    {
        internal GameObject Panel;
        internal GameObject Hole;
        internal Collider EntryCollider;
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
            if (EntryCollider != null)
                EntryCollider.enabled = open;
            if (Link != null)
                Link.activated = open;

            if (Label != null)
                Label.text = open
                    ? "[E] Fermer la trappe\nMonter dessus pour descendre"
                    : "[E] Ouvrir la trappe";
        }
    }

    internal sealed class BasementTeleport : MonoBehaviour
    {
        internal Vector3 Target;
        internal bool RequireOpen;
        internal BasementDoor Door;
        internal float HorizontalYaw = 180f;

        private static float _lastTeleport = -100f;

        private void OnTriggerEnter(Collider other)
        {
            if (Time.time - _lastTeleport < 1.5f)
                return;

            if (RequireOpen && Door != null && !Door.IsOpen)
                return;

            FirstPersonController controller = other.GetComponentInParent<FirstPersonController>();
            if (controller == null || controller != FirstPersonController.Instance)
                return;

            _lastTeleport = Time.time;

            FirstPersonTransform transformComponent = controller.GetComponent<FirstPersonTransform>();
            if (transformComponent != null)
            {
                transformComponent.coroutineActivator(Target, HorizontalYaw);
            }
            else
            {
                controller.transform.position = Target;
            }
        }
    }
}
