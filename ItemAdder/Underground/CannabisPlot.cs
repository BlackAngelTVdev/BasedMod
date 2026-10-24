using TMPro;
using UnityEngine;

namespace ItemAdder.Underground
{
    internal sealed class CannabisPlot : MonoBehaviour
    {
        internal const byte Empty = 0;
        internal const byte Growing = 1;
        internal const byte Ready = 2;

        internal int Index;
        internal int ProductId;
        internal float SeedCost = 350f;

        internal byte State;
        internal float PlantedAt;
        internal bool Watered;
        internal bool Covered;

        internal GameObject PlantVisual;
        internal GameObject CoverVisual;
        internal TextMeshPro Label;
        internal GameObject Pot;

        private float _lastLabelUpdate;

        internal float Growth01
        {
            get
            {
                if (State == Empty)
                    return 0f;

                UndergroundManager manager = UndergroundManager.Instance;
                if (manager == null)
                    return State == Ready ? 1f : 0f;

                return manager.GetGrowth01(this);
            }
        }

        internal bool IsUncoveredAndGrowing => State != Empty && !Covered;

        internal void ApplyState(byte state, float plantedAt, bool watered, bool covered)
        {
            State = state;
            PlantedAt = plantedAt;
            Watered = watered;
            Covered = covered;
            RefreshVisual();
        }

        internal void RefreshVisual()
        {
            bool visible = State != Empty && !Covered;
            if (PlantVisual != null)
            {
                PlantVisual.SetActive(visible);
                if (visible)
                {
                    float growth = Mathf.Clamp01(Growth01);
                    float height = 0.22f + growth * 0.9f;
                    PlantVisual.transform.localScale = new Vector3(
                        0.65f + growth * 0.45f, height, 0.65f + growth * 0.45f);
                }
            }

            if (CoverVisual != null)
                CoverVisual.SetActive(Covered);

            if (Pot != null)
                Pot.SetActive(true);

            UpdateLabel();
        }

        private void Update()
        {
            if (Time.time - _lastLabelUpdate < 0.35f)
                return;

            _lastLabelUpdate = Time.time;
            RefreshVisual();
        }

        private void UpdateLabel()
        {
            if (Label == null)
                return;

            string stateText;
            switch (State)
            {
                case Empty:
                    stateText = $"Parcelle {Index + 1}\n[E] Planter ({SeedCost:0} $)";
                    break;
                case Growing:
                    float percent = Mathf.RoundToInt(Growth01 * 100f);
                    stateText = Watered
                        ? $"Parcelle {Index + 1}\nPousse {percent}%\n[F] {(Covered ? "Découvrir" : "Cacher")}"
                        : $"Parcelle {Index + 1}\nÀ arroser\n[E] Arroser";
                    break;
                case Ready:
                    stateText = $"Parcelle {Index + 1}\nPrête\n[E] Récolter  [F] {(Covered ? "Découvrir" : "Cacher")}";
                    break;
                default:
                    stateText = $"Parcelle {Index + 1}";
                    break;
            }

            if (Covered && State != Empty)
                stateText += "\n(cachée)";

            Label.text = stateText;
        }
    }
}
