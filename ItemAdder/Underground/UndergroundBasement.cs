using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace ItemAdder.Underground
{
    internal sealed class BasementLayout
    {
        internal GameObject Root;
        internal Vector3 Hatch;
        internal Vector3 RampTop;
        internal Vector3 RampBottom;
        internal Vector3 RoomCenter;
        internal Vector3 ShelfPosition;
        internal CannabisPlot[] Plots;
        internal BasementDoor Door;
        internal Collider FloorCollider;
        internal Collider RampCollider;
        internal NavMeshLink Link;
        internal float FloorY;
        internal float MinX;
        internal float MaxX;
        internal float MinZ;
        internal float MaxZ;
    }

    internal static class UndergroundBasement
    {
        private const float FloorY = -4.2f;
        private const float RoomWidth = 18f;
        private const float RoomDepth = 16f;
        private const float RoomHeight = 3.3f;
        private const float CeilingY = -0.9f;

        private static readonly Color Concrete = new Color(0.20f, 0.21f, 0.22f);
        private static readonly Color ConcreteDark = new Color(0.12f, 0.13f, 0.14f);
        private static readonly Color Metal = new Color(0.25f, 0.27f, 0.30f);
        private static readonly Color Wood = new Color(0.32f, 0.22f, 0.13f);
        private static readonly Color Soil = new Color(0.16f, 0.10f, 0.06f);
        private static readonly Color Leaf = new Color(0.18f, 0.56f, 0.22f);
        private static readonly Color LeafLight = new Color(0.30f, 0.72f, 0.30f);
        private static readonly Color Tarp = new Color(0.07f, 0.075f, 0.08f);

        internal static BasementLayout Build(
            Vector3 hatch, int agentTypeId, string floorTag, int floorLayer,
            float seedClassic, float seedPremium, int classicProductId, int premiumProductId)
        {
            var layout = new BasementLayout
            {
                Hatch = hatch,
                FloorY = FloorY,
                Root = new GameObject("ItemAdder_Underground")
            };

            GameObject root = layout.Root;
            Vector3 center = new Vector3(hatch.x, FloorY, hatch.z - 3.5f);
            layout.RoomCenter = center;
            layout.MinX = center.x - RoomWidth * 0.5f;
            layout.MaxX = center.x + RoomWidth * 0.5f;
            layout.MinZ = center.z - RoomDepth * 0.5f;
            layout.MaxZ = center.z + RoomDepth * 0.5f;

            // Sol
            GameObject floor = CreateBox(
                "Floor", root.transform,
                new Vector3(center.x, FloorY - 0.2f, center.z),
                new Vector3(RoomWidth, 0.4f, RoomDepth),
                Concrete, true);
            layout.FloorCollider = floor.GetComponent<Collider>();
            if (!string.IsNullOrEmpty(floorTag))
            {
                try
                {
                    floor.tag = floorTag;
                }
                catch
                {
                    // Le tag de sol du magasin n'est pas toujours déclaré.
                }
            }
            if (floorLayer >= 0)
                floor.layer = floorLayer;

            CreateWalls(root.transform, center);
            CreateCeiling(root.transform, center, hatch);
            CreateStaircase(root.transform, hatch, center, layout);

            // Lumière et ambiance
            CreateLamp(root.transform, center + new Vector3(-5.5f, 0f, -3.5f));
            CreateLamp(root.transform, center + new Vector3(5.5f, 0f, -3.5f));
            CreateLamp(root.transform, center + new Vector3(-5.5f, 0f, 3.5f));
            CreateLamp(root.transform, center + new Vector3(5.5f, 0f, 3.5f));
            CreateLamp(root.transform, center + new Vector3(0f, 0f, 4.8f), 0.8f);

            CreateRoomDetails(root.transform, center);

            // Emplacements
            layout.ShelfPosition = new Vector3(center.x + 5.7f, FloorY + 0.02f, center.z - 2.2f);

            var plots = new List<CannabisPlot>();
            float[] plotX = { -6.3f, -3.6f };
            float[] plotZ = { -3.4f, -0.4f, 2.6f };
            int index = 0;
            for (int row = 0; row < plotZ.Length; row++)
            {
                for (int column = 0; column < plotX.Length; column++)
                {
                    Vector3 position = new Vector3(
                        center.x + plotX[column], FloorY + 0.01f, center.z + plotZ[row]);
                    CannabisPlot plot = CreatePlot(
                        root.transform, position, index,
                        index < 3 ? classicProductId : premiumProductId,
                        index < 3 ? seedClassic : seedPremium);
                    plots.Add(plot);
                    index++;
                }
            }
            layout.Plots = plots.ToArray();

            // Porte / trappe
            layout.Door = CreateDoor(root.transform, hatch, layout);

            // NavMesh du sous-sol
            NavMeshSurface surface = root.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;
            surface.agentTypeID = agentTypeId;
            surface.BuildNavMesh();

            // Lien entre le navmesh du magasin et celui du sous-sol. Le point
            // d'arrivée est au pied de l'escalier : sous le plancher, il n'y a
            // pas assez de hauteur pour que les PNJ y soient navigables.
            GameObject linkObject = new GameObject("Underground_NavMeshLink");
            linkObject.transform.SetParent(root.transform, false);
            linkObject.transform.position = Vector3.zero;

            NavMeshLink link = linkObject.AddComponent<NavMeshLink>();
            link.startPoint = hatch + Vector3.up * 0.12f;
            link.endPoint = layout.RampBottom + Vector3.up * 0.12f;
            link.width = 1.4f;
            link.bidirectional = true;
            link.activated = true;
            link.autoUpdate = false;
            link.costModifier = 1f;
            layout.Link = link;
            layout.Door.Link = link;

            layout.Door.SetOpen(true);

            Plugin.Log.LogInfo(
                $"[Souterrain] Sous-sol construit sous le magasin : trappe ({hatch.x:0.0}, {hatch.z:0.0}), " +
                $"salle {RoomWidth:0}x{RoomDepth:0}, {layout.Plots.Length} parcelles.");
            return layout;
        }

        private static void CreateWalls(Transform parent, Vector3 center)
        {
            float halfW = RoomWidth * 0.5f;
            float halfD = RoomDepth * 0.5f;
            float wallY = FloorY + RoomHeight * 0.5f;

            CreateBox("WallNorth", parent,
                new Vector3(center.x, wallY, center.z + halfD),
                new Vector3(RoomWidth, RoomHeight, 0.45f), ConcreteDark, true);
            CreateBox("WallSouth", parent,
                new Vector3(center.x, wallY, center.z - halfD),
                new Vector3(RoomWidth, RoomHeight, 0.45f), ConcreteDark, true);
            CreateBox("WallWest", parent,
                new Vector3(center.x - halfW, wallY, center.z),
                new Vector3(0.45f, RoomHeight, RoomDepth), ConcreteDark, true);
            CreateBox("WallEast", parent,
                new Vector3(center.x + halfW, wallY, center.z),
                new Vector3(0.45f, RoomHeight, RoomDepth), ConcreteDark, true);
        }

        private static void CreateCeiling(Transform parent, Vector3 center, Vector3 hatch)
        {
            float halfW = RoomWidth * 0.5f;
            float halfD = RoomDepth * 0.5f;
            float openingHalf = 1.8f;
            float openingFront = hatch.z - 0.8f;
            float openingBack = hatch.z - 8.4f;
            float y = CeilingY;

            CreateBox("CeilingWest", parent,
                new Vector3(center.x - (halfW + openingHalf) * 0.5f, y, center.z),
                new Vector3(halfW - openingHalf, 0.3f, RoomDepth), ConcreteDark, true);
            CreateBox("CeilingEast", parent,
                new Vector3(center.x + (halfW + openingHalf) * 0.5f, y, center.z),
                new Vector3(halfW - openingHalf, 0.3f, RoomDepth), ConcreteDark, true);

            float frontDepth = Math.Max(0.2f, center.z + halfD - openingFront);
            CreateBox("CeilingFront", parent,
                new Vector3(center.x, y, openingFront + frontDepth * 0.5f),
                new Vector3(openingHalf * 2f, 0.3f, frontDepth), ConcreteDark, true);

            float backDepth = Math.Max(0.2f, openingBack - (center.z - halfD));
            CreateBox("CeilingBack", parent,
                new Vector3(center.x, y, openingBack - backDepth * 0.5f),
                new Vector3(openingHalf * 2f, 0.3f, backDepth), ConcreteDark, true);
        }

        private static void CreateStaircase(
            Transform parent, Vector3 hatch, Vector3 center, BasementLayout layout)
        {
            Vector3 top = new Vector3(hatch.x, -0.25f, hatch.z - 1.0f);
            Vector3 bottom = new Vector3(hatch.x, FloorY, hatch.z - 7.6f);
            layout.RampTop = top;
            layout.RampBottom = bottom;

            float horizontal = Mathf.Abs(top.z - bottom.z);
            float drop = Mathf.Abs(top.y - bottom.y);
            float length = Mathf.Sqrt(horizontal * horizontal + drop * drop);
            float angle = Mathf.Atan2(drop, horizontal) * Mathf.Rad2Deg;

            GameObject ramp = CreateBox(
                "Ramp", parent,
                new Vector3(top.x, (top.y + bottom.y) * 0.5f - 0.1f, (top.z + bottom.z) * 0.5f),
                new Vector3(3.2f, 0.2f, length), Concrete, true);
            ramp.transform.rotation = Quaternion.Euler(angle, 0f, 0f);
            layout.RampCollider = ramp.GetComponent<Collider>();

            // Les marches sont purement visuelles : le joueur et les PNJ marchent
            // sur la rampe, ce qui évite les blocages de navigation.
            const int steps = 14;
            for (int i = 0; i < steps; i++)
            {
                float t = i / (float)(steps - 1);
                Vector3 position = Vector3.Lerp(top, bottom, t);
                position.y += 0.06f;
                CreateBox("Step" + i, parent, position,
                    new Vector3(3.0f, 0.14f, 0.42f), Concrete, false);
            }

            CreateBox("StairWallWest", parent,
                new Vector3(top.x - 1.8f, (top.y + bottom.y) * 0.5f + 0.2f, (top.z + bottom.z) * 0.5f),
                new Vector3(0.25f, RoomHeight, length + 0.6f), ConcreteDark, true);
            CreateBox("StairWallEast", parent,
                new Vector3(top.x + 1.8f, (top.y + bottom.y) * 0.5f + 0.2f, (top.z + bottom.z) * 0.5f),
                new Vector3(0.25f, RoomHeight, length + 0.6f), ConcreteDark, true);

            CreateBox("StairLanding", parent,
                new Vector3(bottom.x, FloorY - 0.15f, bottom.z - 0.8f),
                new Vector3(3.2f, 0.3f, 1.6f), Concrete, true);
        }

        private static void CreateLamp(Transform parent, Vector3 position, float intensity = 1.15f)
        {
            GameObject lamp = CreateBox(
                "Lamp", parent,
                new Vector3(position.x, CeilingY - 0.2f, position.z),
                new Vector3(1.2f, 0.08f, 1.2f), new Color(0.85f, 0.78f, 0.54f), false);
            MeshRenderer renderer = lamp.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Material material = MakeMaterial(new Color(0.85f, 0.78f, 0.54f), 0.2f, 0f, true);
                if (material != null)
                    renderer.sharedMaterial = material;
            }

            Light light = lamp.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.88f, 0.68f);
            light.intensity = intensity;
            light.range = 8.5f;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForceVertex;
        }

        private static void CreateRoomDetails(Transform parent, Vector3 center)
        {
            // Étagères vides contre le mur du fond, pour l'ambiance.
            for (int i = 0; i < 3; i++)
            {
                CreateBox("Shelf" + i, parent,
                    new Vector3(center.x - 6.5f + i * 2.4f, FloorY + 0.9f, center.z - 7.15f),
                    new Vector3(2.0f, 1.8f, 0.35f), Wood, true);
                CreateBox("ShelfTop" + i, parent,
                    new Vector3(center.x - 6.5f + i * 2.4f, FloorY + 1.82f, center.z - 7.15f),
                    new Vector3(2.1f, 0.08f, 0.4f), Metal, false);
            }

            // Caisses et bâches.
            CreateBox("CrateA", parent,
                new Vector3(center.x + 6.8f, FloorY + 0.35f, center.z - 6.4f),
                new Vector3(1.0f, 0.7f, 1.0f), Wood, true);
            CreateBox("CrateB", parent,
                new Vector3(center.x + 6.0f, FloorY + 0.25f, center.z - 6.6f),
                new Vector3(0.8f, 0.5f, 0.8f), Wood, true);
            CreateBox("CrateC", parent,
                new Vector3(center.x - 7.4f, FloorY + 0.45f, center.z + 5.8f),
                new Vector3(1.3f, 0.9f, 1.0f), Metal, true);

            // Tuyaux au plafond.
            for (int i = 0; i < 4; i++)
            {
                CreateBox("Pipe" + i, parent,
                    new Vector3(center.x - 7f + i * 4.6f, CeilingY - 0.35f, center.z + 7.6f),
                    new Vector3(0.18f, 0.18f, 14.8f), Metal, false);
            }
        }

        private static CannabisPlot CreatePlot(
            Transform parent, Vector3 position, int index, int productId, float seedCost)
        {
            GameObject root = new GameObject("Underground_Plot_" + index);
            root.transform.SetParent(parent, false);
            root.transform.position = position;

            GameObject pot = CreateCylinder(
                "Pot", root.transform, position + Vector3.up * 0.25f,
                new Vector3(0.95f, 0.5f, 0.95f), new Color(0.22f, 0.12f, 0.08f), true);
            CreateCylinder(
                "Soil", root.transform, position + Vector3.up * 0.52f,
                new Vector3(0.82f, 0.08f, 0.82f), Soil, false);

            GameObject plant = new GameObject("Plant");
            plant.transform.SetParent(root.transform, false);
            plant.transform.position = position + Vector3.up * 0.58f;

            CreateCylinder("Stem", plant.transform, plant.transform.position + Vector3.up * 0.35f,
                new Vector3(0.08f, 0.7f, 0.08f), new Color(0.22f, 0.40f, 0.16f), false);

            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI * 2f / 6f;
                GameObject leaf = CreateBox(
                    "Leaf" + i, plant.transform,
                    plant.transform.position + new Vector3(
                        Mathf.Cos(angle) * 0.28f, 0.35f + (i % 3) * 0.16f,
                        Mathf.Sin(angle) * 0.28f),
                    new Vector3(0.38f, 0.05f, 0.18f),
                    i % 2 == 0 ? Leaf : LeafLight, false);
                leaf.transform.rotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 24f);
            }

            GameObject cover = CreateBox(
                "Cover", root.transform, position + Vector3.up * 0.7f,
                new Vector3(1.35f, 1.15f, 1.35f), Tarp, false);
            cover.SetActive(false);

            TextMeshPro label = CreateLabel(root.transform, position + Vector3.up * 1.9f, "");
            var plot = root.AddComponent<CannabisPlot>();
            plot.Index = index;
            plot.ProductId = productId;
            plot.SeedCost = seedCost;
            plot.PlantVisual = plant;
            plot.CoverVisual = cover;
            plot.Label = label;
            plot.Pot = pot;
            plot.RefreshVisual();

            return plot;
        }

        private static BasementDoor CreateDoor(
            Transform parent, Vector3 hatch, BasementLayout layout)
        {
            GameObject root = new GameObject("Underground_Door");
            root.transform.SetParent(parent, false);
            root.transform.position = hatch;

            CreateBox("FrameNorth", root.transform,
                new Vector3(hatch.x, 0.05f, hatch.z + 1.55f),
                new Vector3(3.3f, 0.12f, 0.18f), Metal, false);
            CreateBox("FrameSouth", root.transform,
                new Vector3(hatch.x, 0.05f, hatch.z - 1.55f),
                new Vector3(3.3f, 0.12f, 0.18f), Metal, false);
            CreateBox("FrameWest", root.transform,
                new Vector3(hatch.x - 1.55f, 0.05f, hatch.z),
                new Vector3(0.18f, 0.12f, 3.3f), Metal, false);
            CreateBox("FrameEast", root.transform,
                new Vector3(hatch.x + 1.55f, 0.05f, hatch.z),
                new Vector3(0.18f, 0.12f, 3.3f), Metal, false);

            GameObject hole = CreateBox("Hole", root.transform,
                new Vector3(hatch.x, 0.02f, hatch.z),
                new Vector3(2.9f, 0.02f, 2.9f), new Color(0.01f, 0.01f, 0.012f), false);

            GameObject panel = CreateBox("Panel", root.transform,
                new Vector3(hatch.x, 0.09f, hatch.z),
                new Vector3(3.0f, 0.08f, 3.0f), Metal, true);

            GameObject entryTrigger = new GameObject("EntryTrigger");
            entryTrigger.transform.SetParent(root.transform, false);
            entryTrigger.transform.position = hatch + Vector3.up * 0.7f;
            BoxCollider entryCollider = entryTrigger.AddComponent<BoxCollider>();
            entryCollider.isTrigger = true;
            entryCollider.size = new Vector3(2.5f, 1.4f, 2.5f);

            GameObject exitTrigger = new GameObject("ExitTrigger");
            exitTrigger.transform.SetParent(root.transform, false);
            exitTrigger.transform.position =
                Vector3.Lerp(layout.RampTop, layout.RampBottom, 0.34f) + Vector3.up * 0.9f;
            BoxCollider exitCollider = exitTrigger.AddComponent<BoxCollider>();
            exitCollider.isTrigger = true;
            exitCollider.size = new Vector3(2.6f, 1.6f, 1.4f);

            TextMeshPro label = CreateLabel(root.transform, hatch + Vector3.up * 1.55f, "");

            var door = root.AddComponent<BasementDoor>();
            door.Panel = panel;
            door.Hole = hole;
            door.EntryCollider = entryCollider;
            door.Label = label;

            var entry = entryTrigger.AddComponent<BasementTeleport>();
            entry.Target = Vector3.Lerp(layout.RampTop, layout.RampBottom, 0.50f);
            entry.RequireOpen = true;
            entry.Door = door;
            entry.HorizontalYaw = 180f;

            var exit = exitTrigger.AddComponent<BasementTeleport>();
            exit.Target = hatch + Vector3.up * 0.25f;
            exit.RequireOpen = false;
            exit.Door = door;
            exit.HorizontalYaw = 0f;

            return door;
        }

        internal static GameObject CreateBox(
            string name, Transform parent, Vector3 position, Vector3 scale,
            Color color, bool collider)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, true);
            box.transform.position = position;
            box.transform.localScale = scale;

            if (!collider)
            {
                Collider existing = box.GetComponent<Collider>();
                if (existing != null)
                {
                    existing.enabled = false;
                    Object.Destroy(existing);
                }
            }

            MeshRenderer renderer = box.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Material material = MakeMaterial(color);
                if (material != null)
                    renderer.sharedMaterial = material;
            }

            return box;
        }

        private static GameObject CreateCylinder(
            string name, Transform parent, Vector3 position, Vector3 scale,
            Color color, bool collider)
        {
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = name;
            cylinder.transform.SetParent(parent, true);
            cylinder.transform.position = position;
            cylinder.transform.localScale = scale;

            if (!collider)
            {
                Collider existing = cylinder.GetComponent<Collider>();
                if (existing != null)
                {
                    existing.enabled = false;
                    Object.Destroy(existing);
                }
            }

            MeshRenderer renderer = cylinder.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Material material = MakeMaterial(color);
                if (material != null)
                    renderer.sharedMaterial = material;
            }

            return cylinder;
        }

        internal static Material MakeMaterial(
            Color color, float smoothness = 0.08f, float metallic = 0f, bool emission = false)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            if (shader == null)
                return null;

            Material material = new Material(shader);
            try
            {
                material.color = color;
            }
            catch
            {
                // Certains shaders n'exposent pas _Color.
            }

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness"))
                material.SetFloat("_Glossiness", smoothness);
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_MainTex"))
                material.mainTexture = null;
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", null);

            if (emission)
            {
                material.EnableKeyword("_EMISSION");
                if (material.HasProperty("_EmissionColor"))
                    material.SetColor("_EmissionColor", color * 1.8f);
            }

            Object.DontDestroyOnLoad(material);
            return material;
        }

        internal static TextMeshPro CreateLabel(
            Transform parent, Vector3 position, string text)
        {
            GameObject labelObject = new GameObject("Label");
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.position = position;

            TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            if (font == null)
                font = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault();
            if (font != null)
                label.font = font;

            label.fontSize = 2.4f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.92f, 0.94f, 0.88f);
            label.text = text;
            label.rectTransform.sizeDelta = new Vector2(4f, 1.2f);
            labelObject.transform.localScale = Vector3.one * 0.18f;
            return label;
        }
    }
}
