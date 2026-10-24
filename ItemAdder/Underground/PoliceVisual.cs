using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ItemAdder.Underground
{
    internal sealed class PoliceVisual : MonoBehaviour
    {
        private static readonly Color Uniform = new Color(0.06f, 0.14f, 0.34f);
        private static readonly Color UniformLight = new Color(0.10f, 0.22f, 0.48f);
        private static readonly Color Badge = new Color(0.92f, 0.74f, 0.20f);

        private bool _applied;

        private void Start()
        {
            StartCoroutine(ApplyAfterSpawn());
        }

        private IEnumerator ApplyAfterSpawn()
        {
            for (int attempt = 0; attempt < 12 && !_applied; attempt++)
            {
                yield return new WaitForSeconds(0.5f);
                Apply();
            }
        }

        private void Apply()
        {
            if (_applied)
                return;

            Animator animator = GetComponentInChildren<Animator>(true);
            if (animator == null)
                return;

            GameObject character = animator.gameObject;
            Renderer[] renderers = character.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return;

            foreach (Renderer renderer in renderers)
            {
                Material[] materials = renderer.materials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material material = materials[i];
                    if (material == null)
                        continue;

                    string materialName = material.name.ToLowerInvariant();
                    if (materialName.Contains("skin") || materialName.Contains("head") ||
                        materialName.Contains("face") || materialName.Contains("hair"))
                        continue;

                    Color color = i == 0 ? Uniform : UniformLight;
                    try
                    {
                        material.color = color;
                    }
                    catch
                    {
                        // Certains matériaux n'exposent pas _Color.
                    }

                    if (material.HasProperty("_BaseColor"))
                        material.SetColor("_BaseColor", color);
                }

                renderer.materials = materials;
            }

            Transform head = FindBone(character.transform, "Bip01 Head");
            if (head == null)
                head = FindBone(character.transform, "Head");

            GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cap.name = "PoliceCap";
            Collider capCollider = cap.GetComponent<Collider>();
            if (capCollider != null)
            {
                capCollider.enabled = false;
                Destroy(capCollider);
            }

            if (head != null)
            {
                cap.transform.SetParent(head, false);
                cap.transform.localPosition = new Vector3(0f, 0.22f, 0.02f);
                cap.transform.localRotation = Quaternion.identity;
                cap.transform.localScale = new Vector3(0.24f, 0.08f, 0.26f);
            }
            else
            {
                cap.transform.SetParent(transform, false);
                cap.transform.localPosition = new Vector3(0f, 1.9f, 0f);
                cap.transform.localScale = new Vector3(0.28f, 0.08f, 0.30f);
            }

            MeshRenderer capRenderer = cap.GetComponent<MeshRenderer>();
            if (capRenderer != null)
            {
                Material material = new Material(Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("Standard")
                    ?? Shader.Find("Unlit/Color"));
                if (material.shader != null)
                {
                    material.color = Uniform;
                    if (material.HasProperty("_BaseColor"))
                        material.SetColor("_BaseColor", Uniform);
                    capRenderer.sharedMaterial = material;
                }
            }

            GameObject badge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            badge.name = "PoliceBadge";
            Collider badgeCollider = badge.GetComponent<Collider>();
            if (badgeCollider != null)
            {
                badgeCollider.enabled = false;
                Destroy(badgeCollider);
            }

            badge.transform.SetParent(character.transform, false);
            badge.transform.localPosition = new Vector3(0.16f, 1.18f, 0.22f);
            badge.transform.localScale = new Vector3(0.10f, 0.12f, 0.03f);
            MeshRenderer badgeRenderer = badge.GetComponent<MeshRenderer>();
            if (badgeRenderer != null)
            {
                Material material = new Material(Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("Standard")
                    ?? Shader.Find("Unlit/Color"));
                if (material.shader != null)
                {
                    material.color = Badge;
                    if (material.HasProperty("_BaseColor"))
                        material.SetColor("_BaseColor", Badge);
                    badgeRenderer.sharedMaterial = material;
                }
            }

            _applied = true;
        }

        private static Transform FindBone(Transform root, string name)
        {
            if (root.name == name)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindBone(root.GetChild(i), name);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
