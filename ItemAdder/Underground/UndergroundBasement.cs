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
        internal Vector3 StairExit;
        internal Vector3 ContactPosition;
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
        // Salle placée 7 m sous le plancher du magasin : plus profond qu'avant
        // (4,2 m), avec le plafond maintenu juste sous la dalle du magasin.
        // La hauteur de la salle découle des deux, pour que murs, briques,
        // rampe et éclairage restent cohérents.
        private const float FloorY = -7f;
        private const float RoomWidth = 18f;
        private const float RoomDepth = 16f;
        private const float CeilingY = -0.9f;
        private const float RoomHeight = CeilingY - FloorY;

        private static readonly Color Concrete = new Color(0.28f, 0.29f, 0.30f);
        private static readonly Color ConcreteDark = new Color(0.19f, 0.20f, 0.21f);
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
                Color.white, true, BasementSurface.Concrete, new Vector2(6f, 5.3f));
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
            // Éclairage de l'escalier : sans lui, la moitié de la salle restait
            // dans le noir et le moindre mur ressemblait à une cloison.
            CreateLamp(root.transform, new Vector3(hatch.x, 0f, hatch.z - 3.4f), 1.0f);
            CreateLamp(root.transform, new Vector3(hatch.x, 0f, hatch.z - 8.3f), 0.9f);

            CreateRoomDetails(root.transform, center, hatch);
            CreateContactStand(root.transform, center, layout);

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
            Plugin.Log.LogInfo(
                $"[Souterrain] Emprise de la salle : X {layout.MinX:0.0} → {layout.MaxX:0.0}, " +
                $"Z {layout.MinZ:0.0} → {layout.MaxZ:0.0}, sol y {layout.FloorY:0.0}.");
            return layout;
        }

        private static void CreateWalls(Transform parent, Vector3 center)
        {
            float halfW = RoomWidth * 0.5f;
            float halfD = RoomDepth * 0.5f;
            float wallY = FloorY + RoomHeight * 0.5f;

            Vector2 wideTiling = new Vector2(6f, 1.1f);
            Vector2 deepTiling = new Vector2(5.4f, 1.1f);

            CreateBox("WallNorth", parent,
                new Vector3(center.x, wallY, center.z + halfD),
                new Vector3(RoomWidth, RoomHeight, 0.45f), Color.white, true,
                BasementSurface.Concrete, wideTiling);
            CreateBox("WallSouth", parent,
                new Vector3(center.x, wallY, center.z - halfD),
                new Vector3(RoomWidth, RoomHeight, 0.45f), Color.white, true,
                BasementSurface.Concrete, wideTiling);
            CreateBox("WallWest", parent,
                new Vector3(center.x - halfW, wallY, center.z),
                new Vector3(0.45f, RoomHeight, RoomDepth), Color.white, true,
                BasementSurface.Concrete, deepTiling);
            CreateBox("WallEast", parent,
                new Vector3(center.x + halfW, wallY, center.z),
                new Vector3(0.45f, RoomHeight, RoomDepth), Color.white, true,
                BasementSurface.Concrete, deepTiling);

            // Partie basse en briques : on sort du « cube gris » et on lit tout
            // de suite un local technique plutôt qu'une boîte de test.
            // Bardage brique sur les deux tiers de la hauteur : sur 1,36 m, la
            // bande ressemblait à un bloc flottant dans le noir dès qu'une
            // lampe n'éclairait qu'une partie du mur.
            float brickY = FloorY + 1.15f;
            float brickDepth = 0.25f;
            const float brickHeight = 2.3f;

            CreateBox("BrickNorth", parent,
                new Vector3(center.x, brickY, center.z + halfD - brickDepth),
                new Vector3(RoomWidth - 0.5f, brickHeight, 0.06f), Color.white, false,
                BasementSurface.Brick, new Vector2(16f, 1.8f));
            CreateBox("BrickSouth", parent,
                new Vector3(center.x, brickY, center.z - halfD + brickDepth),
                new Vector3(RoomWidth - 0.5f, brickHeight, 0.06f), Color.white, false,
                BasementSurface.Brick, new Vector2(16f, 1.8f));
            CreateBox("BrickWest", parent,
                new Vector3(center.x - halfW + brickDepth, brickY, center.z),
                new Vector3(0.06f, brickHeight, RoomDepth - 0.5f), Color.white, false,
                BasementSurface.Brick, new Vector2(14f, 1.8f));
            CreateBox("BrickEast", parent,
                new Vector3(center.x + halfW - brickDepth, brickY, center.z),
                new Vector3(0.06f, brickHeight, RoomDepth - 0.5f), Color.white, false,
                BasementSurface.Brick, new Vector2(14f, 1.8f));

            // Plinthes métalliques et piliers d'angle.
            float skirtingY = FloorY + 0.07f;
            CreateBox("SkirtingNorth", parent,
                new Vector3(center.x, skirtingY, center.z + halfD - 0.3f),
                new Vector3(RoomWidth - 0.2f, 0.14f, 0.1f), Color.white, false,
                BasementSurface.Metal, new Vector2(8f, 0.2f));
            CreateBox("SkirtingSouth", parent,
                new Vector3(center.x, skirtingY, center.z - halfD + 0.3f),
                new Vector3(RoomWidth - 0.2f, 0.14f, 0.1f), Color.white, false,
                BasementSurface.Metal, new Vector2(8f, 0.2f));
            CreateBox("SkirtingWest", parent,
                new Vector3(center.x - halfW + 0.3f, skirtingY, center.z),
                new Vector3(0.1f, 0.14f, RoomDepth - 0.2f), Color.white, false,
                BasementSurface.Metal, new Vector2(7f, 0.2f));
            CreateBox("SkirtingEast", parent,
                new Vector3(center.x + halfW - 0.3f, skirtingY, center.z),
                new Vector3(0.1f, 0.14f, RoomDepth - 0.2f), Color.white, false,
                BasementSurface.Metal, new Vector2(7f, 0.2f));

            float[] cornersX = { -8.15f, 8.15f };
            float[] cornersZ = { -7.25f, 7.25f };
            for (int i = 0; i < cornersX.Length; i++)
            {
                for (int j = 0; j < cornersZ.Length; j++)
                {
                    CreateBox($"Pilaster{i}_{j}", parent,
                        new Vector3(center.x + cornersX[i], wallY, center.z + cornersZ[j]),
                        new Vector3(0.55f, RoomHeight, 0.55f), Color.white, false,
                        BasementSurface.Concrete, new Vector2(0.4f, 1.2f));
                }
            }
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
                new Vector3(halfW - openingHalf, 0.3f, RoomDepth), Color.white, true,
                BasementSurface.Concrete, new Vector2(3f, 5f));
            CreateBox("CeilingEast", parent,
                new Vector3(center.x + (halfW + openingHalf) * 0.5f, y, center.z),
                new Vector3(halfW - openingHalf, 0.3f, RoomDepth), Color.white, true,
                BasementSurface.Concrete, new Vector2(3f, 5f));

            float frontDepth = Math.Max(0.2f, center.z + halfD - openingFront);
            CreateBox("CeilingFront", parent,
                new Vector3(center.x, y, openingFront + frontDepth * 0.5f),
                new Vector3(openingHalf * 2f, 0.3f, frontDepth), Color.white, true,
                BasementSurface.Concrete, new Vector2(1f, 2f));

            float backDepth = Math.Max(0.2f, openingBack - (center.z - halfD));
            CreateBox("CeilingBack", parent,
                new Vector3(center.x, y, openingBack - backDepth * 0.5f),
                new Vector3(openingHalf * 2f, 0.3f, backDepth), Color.white, true,
                BasementSurface.Concrete, new Vector2(1f, 2f));

            // Poutres de soutènement apparentes.
            for (int i = -1; i <= 1; i++)
            {
                CreateBox($"CeilingBeam{i + 1}", parent,
                    new Vector3(center.x, y - 0.15f, center.z + i * 5f),
                    new Vector3(RoomWidth, 0.26f, 0.32f), Color.white, false,
                    BasementSurface.Concrete, new Vector2(5f, 0.4f));
            }
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
                new Vector3(3.8f, 0.2f, length), Concrete, true);
            ramp.transform.rotation = Quaternion.Euler(angle, 0f, 0f);
            layout.RampCollider = ramp.GetComponent<Collider>();

            // Les marches sont purement visuelles : le joueur et les PNJ marchent
            // sur la rampe, ce qui évite les blocages de navigation.
            const int steps = 26;
            for (int i = 0; i < steps; i++)
            {
                float t = i / (float)(steps - 1);
                Vector3 position = Vector3.Lerp(top, bottom, t);
                position.y += 0.06f;
                CreateBox("Step" + i, parent, position,
                    new Vector3(3.6f, 0.14f, 0.42f), Concrete, false);
            }

            // Garde-corps bas qui suivent la pente. Les deux murs de 3,3 m de
            // haut qui tenaient ce rôle coupaient la salle en deux au milieu
            // (la trappe est au centre de la salle en X).
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject parapet = CreateBox($"StairParapet{side}", parent,
                    new Vector3(top.x + side * 2.05f,
                        (top.y + bottom.y) * 0.5f + 0.35f,
                        (top.z + bottom.z) * 0.5f),
                    new Vector3(0.16f, 1.25f, length + 0.4f), ConcreteDark, true);
                parapet.transform.rotation = Quaternion.Euler(angle, 0f, 0f);
            }

            CreateBox("StairLanding", parent,
                new Vector3(bottom.x, FloorY - 0.15f, bottom.z - 1.3f),
                new Vector3(3.8f, 0.3f, 2.6f), Concrete, true);

            // Arrivée de la trappe et point de remontée : le joueur est déposé
            // ici (et non au milieu de la rampe, où il pouvait se coincer), et
            // remonte avec E en s'y tenant.
            Vector3 exitPoint = new Vector3(bottom.x, FloorY + 0.05f, bottom.z - 1.9f);
            layout.StairExit = exitPoint;
            CreateLabel(parent, exitPoint + Vector3.up * 1.55f, "[E] Remonter");

            CreateBox("ExitGlow", parent, exitPoint + Vector3.up * 0.03f,
                new Vector3(1.6f, 0.04f, 1.6f), new Color(0.30f, 0.42f, 0.30f), false);

            // Garde-corps : purement visuels, seule la rampe porte la collision.
            for (int side = -1; side <= 1; side += 2)
            {                    Vector3 railCenter = new Vector3(
                        top.x + side * 1.9f,
                    (top.y + bottom.y) * 0.5f + 0.95f,
                    (top.z + bottom.z) * 0.5f);

                GameObject rail = CreateBox($"Handrail{side}", parent, railCenter,
                    new Vector3(0.07f, 0.07f, length), Color.white, false,
                    BasementSurface.Metal, new Vector2(0.3f, 8f));
                rail.transform.rotation = Quaternion.Euler(angle, 0f, 0f);

                for (int post = 0; post <= 3; post++)
                {
                    float t = post / 3f;
                    Vector3 position = Vector3.Lerp(top, bottom, t);
                    CreateBox($"HandrailPost{side}_{post}", parent,
                        new Vector3(railCenter.x, position.y + 0.45f, position.z),
                        new Vector3(0.06f, 0.95f, 0.06f), Color.white, false,
                        BasementSurface.Metal, new Vector2(0.2f, 1f));
                }
            }
        }

        private static void CreateLamp(Transform parent, Vector3 position, float intensity = 1.15f)
        {
            Vector3 center = new Vector3(position.x, CeilingY - 0.28f, position.z);

            CreateBox("LampHousing", parent, center + Vector3.up * 0.1f,
                new Vector3(1.7f, 0.12f, 0.42f), Color.white, false,
                BasementSurface.Metal, new Vector2(1.6f, 0.3f));

            GameObject tube = CreateBox("LampTube", parent, center,
                new Vector3(1.5f, 0.07f, 0.22f), new Color(0.95f, 0.93f, 0.82f), false);

            MeshRenderer renderer = tube.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Material material = MakeMaterial(new Color(0.95f, 0.92f, 0.80f), 0.2f, 0f, true);
                if (material != null)
                    renderer.sharedMaterial = material;
            }

            Light light = tube.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.90f, 0.74f);
            // La salle est bien plus haute qu'avant : portée et puissance
            // augmentées pour que le sol reste éclairé.
            light.intensity = intensity * 1.3f;
            light.range = 14f;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForceVertex;
        }

        private static void CreateRoomDetails(Transform parent, Vector3 center, Vector3 hatch)
        {
            CreatePalletRack(parent, center);
            CreateWorkbench(parent, center);
            CreateDryingRack(parent, center);
            CreateIrrigation(parent, center);
            CreateVentilation(parent, center);
            CreateGrowLights(parent, center);
            CreateFloorDetails(parent, center, hatch);
            CreateOddsAndEnds(parent, center);
        }

        /// <summary>
        /// Contact du marché noir : une table discrète avec une mallette. Le
        /// joueur y écoule sa récolte avec E, au lieu de la mettre en rayon et
        /// de laisser les clients se servir.
        /// </summary>
        private static void CreateContactStand(
            Transform parent, Vector3 center, BasementLayout layout)
        {
            Vector3 position = new Vector3(center.x + 5.4f, FloorY, center.z - 4.2f);
            layout.ContactPosition = position + Vector3.up * 0.9f;

            CreateBox("ContactTable", parent, position + Vector3.up * 0.45f,
                new Vector3(2.0f, 0.9f, 1.0f), new Color(0.20f, 0.22f, 0.24f), true,
                BasementSurface.Metal, new Vector2(2f, 1f));

            CreateBox("ContactCase", parent, position + Vector3.up * 1.05f,
                new Vector3(0.7f, 0.26f, 0.48f), new Color(0.13f, 0.12f, 0.10f), false);

            GameObject contactLamp = CreateBox("ContactLamp", parent, position + Vector3.up * 1.5f,
                new Vector3(0.34f, 0.08f, 0.22f), new Color(0.95f, 0.90f, 0.66f), false);

            Light lamp = contactLamp.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, 0.93f, 0.80f);
            lamp.intensity = 0.9f;
            lamp.range = 5f;
            lamp.shadows = LightShadows.None;

            CreateBox("ContactPad", parent, position + Vector3.up * 0.02f,
                new Vector3(2.6f, 0.04f, 1.7f), new Color(0.30f, 0.25f, 0.15f), false);

            CreateLabel(parent, position + Vector3.up * 1.95f,
                "Contact — [E] Vendre la récolte");
        }

        /// <summary>
        /// Rack de stockage au fond de la salle : montants métalliques,
        /// tablettes en bois et cartons empilés.
        /// </summary>
        private static void CreatePalletRack(Transform parent, Vector3 center)
        {
            float z = center.z - 7.25f;
            float[] posts = { 0.2f, 2.1f, 4.0f, 5.9f };

            for (int i = 0; i < posts.Length; i++)
            {
                CreateBox("RackPost" + i, parent,
                    new Vector3(center.x + posts[i], FloorY + 1.25f, z),
                    new Vector3(0.12f, 2.5f, 0.12f), Color.white, true,
                    BasementSurface.Metal, new Vector2(0.2f, 2f));
            }

            for (int level = 0; level < 2; level++)
            {
                float y = FloorY + 0.9f + level * 0.95f;
                CreateBox("RackShelf" + level, parent,
                    new Vector3(center.x + 3.05f, y, z),
                    new Vector3(5.9f, 0.09f, 0.75f), Color.white, true,
                    BasementSurface.Wood, new Vector2(6f, 0.8f));

                for (int boxIndex = 0; boxIndex < 5; boxIndex++)
                {
                    float x = center.x + 0.7f + boxIndex * 1.15f;
                    float height = 0.42f + (boxIndex % 2) * 0.12f;
                    CreateBox($"RackBox{level}_{boxIndex}", parent,
                        new Vector3(x, y + height * 0.5f + 0.05f, z),
                        new Vector3(0.72f, height, 0.6f), Color.white, false,
                        BasementSurface.Cardboard, new Vector2(0.7f, 0.7f));
                }
            }

            // Bâche qui recouvre le dessus du rack.
            CreateBox("RackTarp", parent,
                new Vector3(center.x + 3.05f, FloorY + 2.32f, z),
                new Vector3(6.0f, 0.05f, 0.85f), Tarp, false);
        }

        /// <summary>
        /// Établi de conditionnement contre le mur est : balance, pots et bocaux.
        /// </summary>
        private static void CreateWorkbench(Transform parent, Vector3 center)
        {
            float x = center.x + 7.85f;
            float z = center.z + 1.4f;

            CreateBox("BenchTop", parent,
                new Vector3(x, FloorY + 0.85f, z),
                new Vector3(0.72f, 0.09f, 2.6f), Color.white, true,
                BasementSurface.Wood, new Vector2(2.4f, 0.8f));

            float[] offsets = { -1.15f, 1.15f };
            for (int i = 0; i < offsets.Length; i++)
            {
                CreateBox("BenchLeg" + i, parent,
                    new Vector3(x, FloorY + 0.42f, z + offsets[i]),
                    new Vector3(0.62f, 0.84f, 0.09f), Color.white, false,
                    BasementSurface.Metal, new Vector2(0.6f, 1f));
            }

            CreateBox("BenchScale", parent,
                new Vector3(x - 0.05f, FloorY + 0.99f, z - 0.85f),
                new Vector3(0.34f, 0.2f, 0.34f), new Color(0.75f, 0.76f, 0.7f), true);
            CreateBox("BenchScreen", parent,
                new Vector3(x - 0.05f, FloorY + 1.15f, z - 0.98f),
                new Vector3(0.24f, 0.16f, 0.04f), new Color(0.05f, 0.12f, 0.08f), false);

            for (int i = 0; i < 3; i++)
            {
                CreateCylinder("BenchJar" + i, parent,
                    new Vector3(x, FloorY + 1.06f, z + 0.3f + i * 0.3f),
                    new Vector3(0.22f, 0.17f, 0.22f), Leaf, true);
            }

            CreateCylinder("BenchPot", parent,
                new Vector3(x, FloorY + 1.02f, z - 0.25f),
                new Vector3(0.3f, 0.13f, 0.3f), new Color(0.22f, 0.12f, 0.08f), true);
        }

        /// <summary>
        /// Séchoir à l'air libre : cadre en bois et têtes suspendues.
        /// </summary>
        private static void CreateDryingRack(Transform parent, Vector3 center)
        {
            float z = center.z + 7.1f;
            float[] framesX = { 0.8f, 4.2f };

            for (int i = 0; i < framesX.Length; i++)
            {
                CreateBox("DryingFrame" + i, parent,
                    new Vector3(center.x + framesX[i], FloorY + 1.05f, z),
                    new Vector3(0.1f, 2.1f, 0.1f), Color.white, true,
                    BasementSurface.Wood, new Vector2(0.4f, 2f));
            }

            for (int bar = 0; bar < 3; bar++)
            {
                float y = FloorY + 1.35f + bar * 0.32f;
                CreateBox("DryingBar" + bar, parent,
                    new Vector3(center.x + 2.5f, y, z),
                    new Vector3(3.5f, 0.05f, 0.05f), Color.white, false,
                    BasementSurface.Wood, new Vector2(3f, 0.2f));

                for (int bud = 0; bud < 5; bud++)
                {
                    float x = center.x + 0.95f + bud * 0.78f;
                    CreateBox($"DryingString{bar}_{bud}", parent,
                        new Vector3(x, y - 0.12f, z),
                        new Vector3(0.015f, 0.24f, 0.015f),
                        new Color(0.78f, 0.76f, 0.68f), false);
                    CreateCylinder($"DryingBud{bar}_{bud}", parent,
                        new Vector3(x, y - 0.38f, z),
                        new Vector3(0.16f, 0.13f, 0.16f),
                        bar == 0 ? Leaf : LeafLight, false);
                }
            }
        }

        /// <summary>
        /// Cuve d'eau, tuyau d'arrosage et arrosoir le long du mur ouest.
        /// </summary>
        private static void CreateIrrigation(Transform parent, Vector3 center)
        {
            CreateCylinder("WaterTank", parent,
                new Vector3(center.x - 7.55f, FloorY + 1.05f, center.z + 4.3f),
                new Vector3(1.25f, 1.05f, 1.25f), new Color(0.32f, 0.36f, 0.38f), true);
            CreateCylinder("WaterTankLid", parent,
                new Vector3(center.x - 7.55f, FloorY + 2.14f, center.z + 4.3f),
                new Vector3(1.32f, 0.05f, 1.32f), Color.white, false,
                BasementSurface.Metal, new Vector2(1f, 1f));

            CreateBox("WaterTap", parent,
                new Vector3(center.x - 6.9f, FloorY + 0.55f, center.z + 4.3f),
                new Vector3(0.18f, 0.16f, 0.16f), Color.white, false,
                BasementSurface.Metal, new Vector2(0.2f, 0.2f));

            CreateBox("Hose", parent,
                new Vector3(center.x - 7.05f, FloorY + 0.05f, center.z + 0.35f),
                new Vector3(0.07f, 0.1f, 6.9f), new Color(0.10f, 0.22f, 0.12f), false);
            CreateBox("HoseElbow", parent,
                new Vector3(center.x - 5.9f, FloorY + 0.05f, center.z + 3.75f),
                new Vector3(2.3f, 0.1f, 0.07f), new Color(0.10f, 0.22f, 0.12f), false);

            CreateCylinder("WateringCan", parent,
                new Vector3(center.x - 2.15f, FloorY + 0.2f, center.z + 4.4f),
                new Vector3(0.3f, 0.2f, 0.3f), new Color(0.16f, 0.35f, 0.22f), true);
        }

        /// <summary>
        /// Gaine de ventilation et extracteur mural.
        /// </summary>
        private static void CreateVentilation(Transform parent, Vector3 center)
        {
            float y = CeilingY - 0.62f;

            CreateBox("DuctMain", parent,
                new Vector3(center.x + 0.4f, y, center.z + 6.5f),
                new Vector3(7.4f, 0.46f, 0.46f), Color.white, false,
                BasementSurface.Metal, new Vector2(8f, 0.6f));

            for (int i = 0; i < 4; i++)
            {
                CreateBox("DuctGrille" + i, parent,
                    new Vector3(center.x - 2.4f + i * 1.7f, y - 0.24f, center.z + 6.5f),
                    new Vector3(0.6f, 0.03f, 0.4f), new Color(0.12f, 0.13f, 0.14f), false);
            }

            GameObject fan = CreateCylinder("FanBox", parent,
                new Vector3(center.x + 8.6f, FloorY + 2.45f, center.z + 3.2f),
                new Vector3(0.6f, 0.1f, 0.6f), Color.white, false,
                BasementSurface.Metal, new Vector2(1f, 1f));
            fan.transform.rotation = Quaternion.Euler(0f, 0f, 90f);

            for (int i = 0; i < 4; i++)
            {
                CreateBox("FanBar" + i, parent,
                    new Vector3(center.x + 8.54f, FloorY + 2.45f, center.z + 2.95f + i * 0.16f),
                    new Vector3(0.02f, 0.9f, 0.04f), new Color(0.08f, 0.08f, 0.09f), false);
            }
        }

        /// <summary>
        /// Trois lampes de croissance mauves au-dessus des parcelles, sur rail.
        /// </summary>
        private static void CreateGrowLights(Transform parent, Vector3 center)
        {
            float[] rows = { -3.4f, -0.4f, 2.6f };
            for (int i = 0; i < rows.Length; i++)
            {
                // Les lampes de croissance restent à hauteur de plantes, même
                // avec une salle bien plus haute qu'avant.
                Vector3 position = new Vector3(
                    center.x - 4.95f, FloorY + 2.7f, center.z + rows[i]);

                CreateBox("GrowRod" + i, parent,
                    new Vector3(position.x, (position.y + CeilingY) * 0.5f, position.z),
                    new Vector3(0.05f, CeilingY - position.y, 0.05f), Color.white, false,
                    BasementSurface.Metal, new Vector2(0.3f, 0.5f));

                CreateBox("GrowShade" + i, parent, position,
                    new Vector3(1.7f, 0.14f, 0.55f), Color.white, false,
                    BasementSurface.Metal, new Vector2(1.4f, 0.3f));

                GameObject tube = CreateBox("GrowTube" + i, parent,
                    new Vector3(position.x, position.y - 0.1f, position.z),
                    new Vector3(1.45f, 0.06f, 0.3f), new Color(0.86f, 0.45f, 0.95f), false);

                MeshRenderer renderer = tube.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    Material glow = MakeMaterial(new Color(0.72f, 0.30f, 0.90f), 0.2f, 0f, true);
                    if (glow != null)
                        renderer.sharedMaterial = glow;
                }

                Light light = tube.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(0.82f, 0.42f, 1f);
                light.intensity = 0.9f;
                light.range = 6.5f;
                light.shadows = LightShadows.None;
                light.renderMode = LightRenderMode.ForceVertex;
            }
        }

        /// <summary>
        /// Détails de sol : tapis, grille d'évacuation, marquage et flaque.
        /// </summary>
        private static void CreateFloorDetails(
            Transform parent, Vector3 center, Vector3 hatch)
        {
            float stairX = hatch.x;

            CreateBox("StairMat", parent,
                new Vector3(stairX, FloorY + 0.02f, center.z - 4.7f),
                new Vector3(3.0f, 0.03f, 1.7f), new Color(0.08f, 0.085f, 0.09f), false);

            CreateBox("DrainFrame", parent,
                new Vector3(stairX + 2.6f, FloorY + 0.01f, center.z - 5.3f),
                new Vector3(0.9f, 0.03f, 0.9f), Color.white, false,
                BasementSurface.Metal, new Vector2(1f, 1f));

            for (int i = 0; i < 4; i++)
            {
                CreateBox("DrainBar" + i, parent,
                    new Vector3(stairX + 2.6f, FloorY + 0.03f, center.z - 5.62f + i * 0.21f),
                    new Vector3(0.78f, 0.02f, 0.06f), new Color(0.06f, 0.06f, 0.07f), false);
            }

            Color paint = new Color(0.62f, 0.55f, 0.18f);
            CreateBox("LineGrowAisle", parent,
                new Vector3(center.x - 2.35f, FloorY + 0.02f, center.z - 0.4f),
                new Vector3(0.09f, 0.02f, 7.6f), paint, false);
            CreateBox("LineGrowBack", parent,
                new Vector3(center.x - 4.9f, FloorY + 0.02f, center.z - 4.2f),
                new Vector3(5.2f, 0.02f, 0.09f), paint, false);
            CreateBox("LineGrowFront", parent,
                new Vector3(center.x - 4.9f, FloorY + 0.02f, center.z + 3.4f),
                new Vector3(5.2f, 0.02f, 0.09f), paint, false);

            GameObject puddle = CreateBox("Puddle", parent,
                new Vector3(center.x - 5.6f, FloorY + 0.012f, center.z + 3.9f),
                new Vector3(1.9f, 0.01f, 1.2f), new Color(0.05f, 0.07f, 0.09f), false);
            MeshRenderer puddleRenderer = puddle.GetComponent<MeshRenderer>();
            if (puddleRenderer != null)
            {
                Material wet = MakeMaterial(new Color(0.05f, 0.07f, 0.09f), 0.92f, 0.1f);
                if (wet != null)
                    puddleRenderer.sharedMaterial = wet;
            }
        }

        /// <summary>
        /// Sacs de terre, bidons, seau, tableau électrique et balai.
        /// </summary>
        private static void CreateOddsAndEnds(Transform parent, Vector3 center)
        {
            for (int i = 0; i < 4; i++)
            {
                float z = center.z - 1.6f + i * 0.75f;
                bool stacked = i % 2 == 1;
                CreateBox("SoilBag" + i, parent,
                    new Vector3(center.x - 7.85f, FloorY + (stacked ? 0.75f : 0.22f), z),
                    new Vector3(0.62f, stacked ? 0.3f : 0.44f, 0.9f), Soil, false);
            }

            CreateBox("JerrycanA", parent,
                new Vector3(center.x + 8.15f, FloorY + 0.28f, center.z + 3.9f),
                new Vector3(0.32f, 0.55f, 0.26f), new Color(0.45f, 0.16f, 0.10f), false);
            CreateBox("JerrycanB", parent,
                new Vector3(center.x + 7.95f, FloorY + 0.24f, center.z + 4.35f),
                new Vector3(0.3f, 0.48f, 0.24f), new Color(0.35f, 0.35f, 0.12f), false);
            CreateCylinder("Bucket", parent,
                new Vector3(center.x + 7.9f, FloorY + 0.2f, center.z + 5.1f),
                new Vector3(0.42f, 0.2f, 0.42f), new Color(0.22f, 0.26f, 0.30f), false);

            CreateBox("Panel", parent,
                new Vector3(center.x - 0.9f, FloorY + 1.6f, center.z + 7.78f),
                new Vector3(0.75f, 1.0f, 0.12f), new Color(0.42f, 0.43f, 0.45f), true);

            for (int i = 0; i < 3; i++)
            {
                CreateBox("Conduit" + i, parent,
                    new Vector3(center.x - 1.15f + i * 0.22f, FloorY + 2.55f, center.z + 7.8f),
                    new Vector3(0.06f, 0.9f, 0.06f), new Color(0.2f, 0.2f, 0.2f), false);
            }

            GameObject mop = CreateBox("MopHandle", parent,
                new Vector3(center.x + 6.6f, FloorY + 0.75f, center.z + 7.55f),
                new Vector3(0.06f, 1.5f, 0.06f), Color.white, false,
                BasementSurface.Wood, new Vector2(0.2f, 2f));
            mop.transform.rotation = Quaternion.Euler(0f, 0f, 14f);
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

            // Plus aucun déclencheur physique : la descente et la remontée se
            // font par la touche d'interaction (E) devant la trappe et sur le
            // palier du bas. Un déclencheur ne se déclenchait pas quand le
            // joueur était déjà dedans, ni quand la trappe touchait un mur.
            TextMeshPro label = CreateLabel(root.transform, hatch + Vector3.up * 1.55f, "");

            var door = root.AddComponent<BasementDoor>();
            door.Panel = panel;
            door.Hole = hole;
            door.Label = label;

            return door;
        }

        internal static GameObject CreateBox(
            string name, Transform parent, Vector3 position, Vector3 scale,
            Color color, bool collider, BasementSurface surface = BasementSurface.Plain,
            Vector2? tiling = null)
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
                Material material = surface == BasementSurface.Plain
                    ? MakeMaterial(color)
                    : MakeMaterial(color, surface, tiling ?? Vector2.one);
                if (material != null)
                    renderer.sharedMaterial = material;
            }

            return box;
        }

        private static GameObject CreateCylinder(
            string name, Transform parent, Vector3 position, Vector3 scale,
            Color color, bool collider, BasementSurface surface = BasementSurface.Plain,
            Vector2? tiling = null)
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
                Material material = surface == BasementSurface.Plain
                    ? MakeMaterial(color)
                    : MakeMaterial(color, surface, tiling ?? Vector2.one);
                if (material != null)
                    renderer.sharedMaterial = material;
            }

            return cylinder;
        }

        internal enum BasementSurface
        {
            Plain,
            Concrete,
            Brick,
            Wood,
            Metal,
            Cardboard
        }

        private static readonly Dictionary<BasementSurface, Texture2D> Textures =
            new Dictionary<BasementSurface, Texture2D>();

        /// <summary>
        /// Textures générées à la volée : aucune ressource externe, mais un vrai
        /// grain de béton, de brique, de bois et de métal au lieu d'aplats.
        /// </summary>
        internal static Material MakeMaterial(
            Color color, BasementSurface surface, Vector2 tiling,
            float smoothness = 0.08f, float metallic = 0f, bool emission = false)
        {
            Material material = MakeMaterial(color, smoothness, metallic, emission);
            if (material == null)
                return null;

            Texture2D texture = GetTexture(surface);
            if (texture == null)
                return material;

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
                material.SetTextureScale("_BaseMap", tiling);
            }
            if (material.HasProperty("_MainTex"))
            {
                material.mainTexture = texture;
                material.mainTextureScale = tiling;
            }

            // Souffle d'auto-illumination : URP ne rend qu'un nombre limité de
            // lumières additionnelles par objet, donc les grands murs restaient
            // noirs par plaques (seuls les morceaux éclairés se voyaient, ce qui
            // donnait des blocs de briques isolés au milieu de la salle).
            if (!emission)
                AddAmbientGlow(material, color, 0.09f);

            return material;
        }

        private static void AddAmbientGlow(Material material, Color color, float scale)
        {
            if (material == null || !material.HasProperty("_EmissionColor"))
                return;

            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            material.SetColor("_EmissionColor", color * scale);
        }

        private static Texture2D GetTexture(BasementSurface surface)
        {
            if (surface == BasementSurface.Plain)
                return null;

            if (Textures.TryGetValue(surface, out Texture2D cached) && cached != null)
                return cached;

            Texture2D created;
            switch (surface)
            {
                case BasementSurface.Brick:
                    created = BuildBrickTexture();
                    break;
                case BasementSurface.Concrete:
                    created = BuildConcreteTexture();
                    break;
                case BasementSurface.Wood:
                    created = BuildWoodTexture();
                    break;
                case BasementSurface.Metal:
                    created = BuildMetalTexture();
                    break;
                case BasementSurface.Cardboard:
                    created = BuildCardboardTexture();
                    break;
                default:
                    return null;
            }

            Textures[surface] = created;
            return created;
        }

        private static Texture2D NewTexture(int size)
        {
            return new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 4
            };
        }

        private static Texture2D BuildBrickTexture()
        {
            const int size = 256;
            const int brickWidth = 64;
            const int brickHeight = 32;
            const int mortar = 3;

            var random = new System.Random(20261024);
            var tones = new float[64];
            for (int i = 0; i < tones.Length; i++)
                tones[i] = 0.78f + (float)random.NextDouble() * 0.42f;

            Texture2D texture = NewTexture(size);
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                int row = y / brickHeight;
                int offset = (row & 1) == 0 ? 0 : brickWidth / 2;

                for (int x = 0; x < size; x++)
                {
                    int localX = (x + offset) % brickWidth;
                    int localY = y % brickHeight;
                    int index = y * size + x;

                    if (localX < mortar || localY < mortar)
                    {
                        float grain = Mathf.PerlinNoise(x * 0.15f, y * 0.15f) * 0.08f;
                        pixels[index] = new Color(
                            0.30f + grain, 0.29f + grain, 0.27f + grain);
                        continue;
                    }

                    int brick = ((x + offset) / brickWidth) + row * 13;
                    float tone = tones[brick % tones.Length];
                    float stain = Mathf.PerlinNoise(x * 0.02f, y * 0.02f) * 0.14f;

                    pixels[index] = new Color(
                        (0.40f - stain) * tone,
                        (0.20f - stain * 0.6f) * tone,
                        (0.16f - stain * 0.5f) * tone);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(true, true);
            return texture;
        }

        private static Texture2D BuildConcreteTexture()
        {
            const int size = 256;
            Color baseColor = new Color(0.26f, 0.26f, 0.27f);

            Texture2D texture = NewTexture(size);
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float coarse = Mathf.PerlinNoise(x * 0.018f, y * 0.018f);
                    float fine = Mathf.PerlinNoise(x * 0.27f, y * 0.27f);
                    float shade = 0.84f + coarse * 0.34f + (fine - 0.5f) * 0.14f;
                    float crack = fine > 0.93f ? 0.74f : 1f;
                    pixels[y * size + x] = baseColor * (shade * crack);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(true, true);
            return texture;
        }

        private static Texture2D BuildWoodTexture()
        {
            const int size = 256;
            const int plank = 32;
            Color baseColor = new Color(0.36f, 0.24f, 0.14f);

            Texture2D texture = NewTexture(size);
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int index = x / plank;
                    float tone = 0.88f + ((index * 37) % 11) / 44f;
                    float grain = Mathf.PerlinNoise(x * 1.3f, y * 0.06f);
                    float seam = (x % plank) < 2 ? 0.6f : 1f;

                    pixels[y * size + x] =
                        baseColor * (tone * (0.82f + grain * 0.3f) * seam);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(true, true);
            return texture;
        }

        private static Texture2D BuildMetalTexture()
        {
            const int size = 256;
            Color baseColor = new Color(0.34f, 0.36f, 0.39f);

            Texture2D texture = NewTexture(size);
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float brush = Mathf.PerlinNoise(x * 0.55f, y * 0.03f);
                    float shade = 0.86f + brush * 0.28f;
                    pixels[y * size + x] = baseColor * shade;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(true, true);
            return texture;
        }

        private static Texture2D BuildCardboardTexture()
        {
            const int size = 256;
            Color baseColor = new Color(0.55f, 0.41f, 0.25f);

            Texture2D texture = NewTexture(size);
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float fibres = Mathf.PerlinNoise(x * 0.09f, y * 0.09f);
                    float shade = 0.9f + fibres * 0.2f;
                    pixels[y * size + x] = baseColor * shade;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(true, true);
            return texture;
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
