using System;
using System.Collections.Generic;
using UnityEngine;

namespace ItemAdder.Products
{
    /// <summary>
    /// Fabrique de vrais maillages 3D pour les produits ajoutés par le mod.
    /// Aucune primitive Unity n'est utilisée : chaque forme est générée sommet
    /// par sommet (coques biseautées, pièces tournées, bourgeons bosselés).
    /// Tous les modèles remplissent un cube unitaire [-0,5 ; 0,5] ; le prefab
    /// racine est ensuite mis à l'échelle de la taille réelle du produit.
    /// </summary>
    internal static class ProductMeshes
    {
        private static readonly Dictionary<string, Mesh> Cache =
            new Dictionary<string, Mesh>(StringComparer.Ordinal);

        private static Mesh Cached(string key, Func<Mesh> build)
        {
            if (Cache.TryGetValue(key, out Mesh cached) && cached != null)
                return cached;

            Mesh created = build();
            if (created == null)
                return null;

            created.name = key;
            Cache[key] = created;
            return created;
        }

        // ---------------------------------------------------------------- coque

        /// <summary>
        /// Coque arrondie (paquet, sachet, boîtier, briquet) : bords verticaux
        /// arrondis, chant biseauté en haut et en bas, ventre et pincement
        /// optionnels. C'est la forme de base de la plupart des emballages.
        /// </summary>
        internal static Mesh Slab(
            string key, float cornerRadius, float bevel, float bulge = 0f,
            float pinch = 0f, float lumpiness = 0f, int cornerSegments = 6,
            int bodyLayers = 3, int bevelLayers = 3)
        {
            return Cached(key, () => BuildSlab(
                cornerRadius, bevel, bulge, pinch, lumpiness,
                cornerSegments, bodyLayers, bevelLayers));
        }

        private static Mesh BuildSlab(
            float cornerRadius, float bevel, float bulge, float pinch, float lumpiness,
            int cornerSegments, int bodyLayers, int bevelLayers)
        {
            bevel = Mathf.Clamp(bevel, 0.001f, 0.45f);
            cornerRadius = Mathf.Clamp(cornerRadius, 0.001f, 0.5f);
            cornerSegments = Mathf.Max(2, cornerSegments);
            bodyLayers = Mathf.Max(1, bodyLayers);
            bevelLayers = Mathf.Max(1, bevelLayers);

            List<float> ys = new List<float>();
            List<float> insets = new List<float>();

            ys.Add(-0.5f);
            insets.Add(bevel);
            for (int i = 1; i <= bevelLayers; i++)
            {
                float d = bevel * i / bevelLayers;
                ys.Add(-0.5f + d);
                insets.Add(BevelInset(bevel, d));
            }

            for (int i = 1; i < bodyLayers; i++)
            {
                ys.Add(-0.5f + bevel + (1f - 2f * bevel) * i / bodyLayers);
                insets.Add(0f);
            }

            for (int i = bevelLayers; i >= 1; i--)
            {
                float d = bevel * i / bevelLayers;
                ys.Add(0.5f - d);
                insets.Add(BevelInset(bevel, d));
            }

            ys.Add(0.5f);
            insets.Add(bevel);

            int rowCount = ys.Count;
            List<Vector3>[] rings = new List<Vector3>[rowCount];
            List<Vector3>[] ringNormals = new List<Vector3>[rowCount];

            for (int i = 0; i < rowCount; i++)
            {
                float inset = insets[i];
                float half = Mathf.Max(0.02f, 0.5f - inset);
                float radius = Mathf.Max(0.001f, cornerRadius - inset);

                float t = Mathf.Clamp01(ys[i] + 0.5f);
                float scale = 1f + bulge * (1f - Mathf.Pow(t * 2f - 1f, 2f));
                scale *= 1f - pinch * t;

                rings[i] = new List<Vector3>();
                ringNormals[i] = new List<Vector3>();
                BuildRing(rings[i], ringNormals[i], half, radius, cornerSegments,
                    scale, ys[i], lumpiness, 0.37f * i);
            }

            MeshBuilder builder = new MeshBuilder();
            int cols = rings[0].Count;

            int[] wallIndex = new int[rowCount * cols];
            for (int i = 0; i < rowCount; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    wallIndex[i * cols + j] = builder.Add(
                        rings[i][j], new Vector2(j / (float)cols, (ys[i] + 0.5f)));
                }
            }

            for (int i = 0; i < rowCount - 1; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    int next = (j + 1) % cols;
                    Vector3 outward = ringNormals[i][j] + ringNormals[i][next];
                    builder.Quad(
                        wallIndex[i * cols + j],
                        wallIndex[i * cols + next],
                        wallIndex[(i + 1) * cols + next],
                        wallIndex[(i + 1) * cols + j],
                        outward);
                }
            }

            // Les bouchons reprennent exactement la position de l'anneau
            // extrême : le maillage est fermé, sans fente visible.
            AddCap(builder, rings[0], Vector3.down, -0.5f, 0f);
            AddCap(builder, rings[rowCount - 1], Vector3.up, 0.5f, 1f);

            return builder.Build();
        }

        private static float BevelInset(float bevel, float distance)
        {
            // Profil circulaire du chant : l'arrondi rejoint la paroi à
            // distance = bevel, et le chant est plein à distance nulle.
            float remaining = Mathf.Max(0f, bevel - distance);
            return bevel - Mathf.Sqrt(Mathf.Max(0f, bevel * bevel - remaining * remaining));
        }

        private static void BuildRing(
            List<Vector3> points, List<Vector3> normals, float half, float radius,
            int cornerSegments, float scale, float y, float lumpiness, float seed)
        {
            radius = Mathf.Clamp(radius, 0.0005f, half * 0.999f);
            float center = half - radius;

            Vector2[] corners =
            {
                new Vector2(center, center),
                new Vector2(-center, center),
                new Vector2(-center, -center),
                new Vector2(center, -center)
            };
            float[] starts = { 0f, 90f, 180f, 270f };

            for (int c = 0; c < 4; c++)
            {
                for (int s = 0; s <= cornerSegments; s++)
                {
                    float angle = (starts[c] + 90f * s / cornerSegments) * Mathf.Deg2Rad;
                    float nx = Mathf.Cos(angle);
                    float nz = Mathf.Sin(angle);

                    Vector3 point = new Vector3(
                        (corners[c].x + nx * radius) * scale,
                        y,
                        (corners[c].y + nz * radius) * scale);

                    Vector3 normal = new Vector3(nx, 0f, nz);
                    if (lumpiness > 0f)
                    {
                        // Bosselure uniquement dans le plan horizontal : les
                        // bouchons restent ainsi parfaitement jointifs.
                        point += normal * (Lump(point, seed) * lumpiness);
                    }

                    points.Add(point);
                    normals.Add(normal);
                }
            }
        }

        private static void AddCap(
            MeshBuilder builder, List<Vector3> ring, Vector3 outward, float y, float v)
        {
            int center = builder.Add(new Vector3(0f, y, 0f), new Vector2(0.5f, v));
            int count = ring.Count;
            int[] copy = new int[count];

            for (int j = 0; j < count; j++)
            {
                Vector3 position = new Vector3(ring[j].x, y, ring[j].z);
                copy[j] = builder.Add(position, new Vector2(j / (float)count, v));
            }

            for (int j = 0; j < count; j++)
                builder.Triangle(center, copy[j], copy[(j + 1) % count], outward);
        }

        // ------------------------------------------------------------ pièces tournées

        /// <summary>
        /// Pièce de révolution (bouteille, boîte métallique, cigare, cendrier,
        /// broyeur). Le profil va du bas vers le haut ; un rayon nul ferme
        /// l'extrémité. <paramref name="orientation"/> permet de coucher la
        /// pièce sur l'axe X (cigare).
        /// </summary>
        internal static Mesh Lathe(
            string key, Vector2[] profile, int segments = 20,
            float knurlDepth = 0f, int knurlRidges = 0,
            Quaternion? orientation = null)
        {
            return Cached(key, () => BuildLathe(profile, segments, knurlDepth, knurlRidges, orientation));
        }

        private static Mesh BuildLathe(
            Vector2[] profile, int segments, float knurlDepth, int knurlRidges,
            Quaternion? orientation)
        {
            if (profile == null || profile.Length < 2)
                return null;

            segments = Mathf.Max(6, segments);
            Quaternion rotation = orientation ?? Quaternion.identity;

            MeshBuilder builder = new MeshBuilder();
            List<int[]> rows = new List<int[]>();

            for (int i = 0; i < profile.Length; i++)
            {
                float radius = profile[i].x;
                float y = profile[i].y;

                if (radius <= 0.0005f)
                {
                    rows.Add(new[] { builder.Add(rotation * new Vector3(0f, y, 0f), new Vector2(0.5f, y + 0.5f)) });
                    continue;
                }

                int[] indices = new int[segments];
                for (int j = 0; j < segments; j++)
                {
                    float angle = j / (float)segments * Mathf.PI * 2f;
                    float localRadius = radius;
                    if (knurlDepth > 0f && knurlRidges > 0)
                    {
                        float ridge = 0.5f + 0.5f * Mathf.Cos(angle * knurlRidges);
                        localRadius *= 1f + knurlDepth * (ridge - 0.5f);
                    }

                    Vector3 position = new Vector3(
                        Mathf.Cos(angle) * localRadius, y, Mathf.Sin(angle) * localRadius);
                    indices[j] = builder.Add(rotation * position,
                        new Vector2(j / (float)segments, y + 0.5f));
                }

                rows.Add(indices);
            }

            for (int i = 0; i < rows.Count - 1; i++)
            {
                int[] lower = rows[i];
                int[] upper = rows[i + 1];
                float dr = profile[i + 1].x - profile[i].x;
                float dy = profile[i + 1].y - profile[i].y;
                float length = Mathf.Sqrt(dr * dr + dy * dy);
                if (length <= 0.00001f)
                    continue;

                // Normale du profil, dirigée vers l'extérieur de la pièce.
                Vector3 profileNormal = new Vector3(dy, -dr, 0f) / length;

                for (int j = 0; j < segments; j++)
                {
                    int next = (j + 1) % segments;
                    float angle = (j + 0.5f) / segments * Mathf.PI * 2f;
                    // La normale doit être tournée comme les sommets (cigare
                    // couché), sinon le sens des faces est inversé.
                    Vector3 outward = rotation * new Vector3(
                        Mathf.Cos(angle) * profileNormal.x,
                        profileNormal.y,
                        Mathf.Sin(angle) * profileNormal.x);

                    if (lower.Length == 1 && upper.Length == 1)
                        continue;

                    if (lower.Length == 1)
                    {
                        builder.Triangle(lower[0], upper[j], upper[next], outward);
                    }
                    else if (upper.Length == 1)
                    {
                        builder.Triangle(lower[j], lower[next], upper[0], outward);
                    }
                    else
                    {
                        builder.Quad(lower[j], lower[next], upper[next], upper[j], outward);
                    }
                }
            }

            return builder.Build();
        }

        // --------------------------------------------------------------- bourgeons

        /// <summary>
        /// Amas bosselé (bourgeon, tête de weed) : sphère déformée par un bruit
        /// déterministe, inscrite dans un cube unitaire.
        /// </summary>
        internal static Mesh Blob(string key, float lumpiness, int seed, int slices = 16, int stacks = 12)
        {
            return Cached(key, () => BuildBlob(lumpiness, seed, slices, stacks));
        }

        private static Mesh BuildBlob(float lumpiness, int seed, int slices, int stacks)
        {
            slices = Mathf.Max(6, slices);
            stacks = Mathf.Max(4, stacks);

            MeshBuilder builder = new MeshBuilder();
            float offset = seed * 1.37f;

            // Le bruit peut dépasser le rayon de départ : on remet l'amas à
            // l'échelle du cube unitaire pour qu'aucun sommet n'en sorte.
            float scale = 0.5f / (1f + lumpiness * 1.5f);

            float RadiusAt(Vector3 direction, out Vector3 point)
            {
                float radius = 1f + lumpiness * (Lump(direction * 2.4f, offset) +
                                                 Lump(direction * 5.7f, offset + 11.3f) * 0.5f);
                point = direction * (radius * scale);
                return radius;
            }

            Vector3 polePoint;
            RadiusAt(Vector3.up, out polePoint);
            int topPole = builder.Add(polePoint, new Vector2(0.5f, 1f));

            RadiusAt(Vector3.down, out polePoint);
            int bottomPole = builder.Add(polePoint, new Vector2(0.5f, 0f));

            int ringRows = stacks - 1;
            int[][] rings = new int[ringRows][];

            for (int stack = 1; stack <= ringRows; stack++)
            {
                float v = stack / (float)stacks;
                float polar = v * Mathf.PI;
                float y = Mathf.Cos(polar);
                float ringRadius = Mathf.Sin(polar);
                rings[stack - 1] = new int[slices];

                for (int slice = 0; slice < slices; slice++)
                {
                    float angle = slice / (float)slices * Mathf.PI * 2f;
                    Vector3 direction = new Vector3(
                        Mathf.Cos(angle) * ringRadius, y, Mathf.Sin(angle) * ringRadius);

                    Vector3 point;
                    RadiusAt(direction, out point);
                    rings[stack - 1][slice] = builder.Add(
                        point, new Vector2(slice / (float)slices, v));
                }
            }

            for (int slice = 0; slice < slices; slice++)
            {
                int next = (slice + 1) % slices;
                Vector3 outward = builder.Position(rings[0][slice]) +
                                  builder.Position(topPole);
                builder.Triangle(topPole, rings[0][slice], rings[0][next], outward);
            }

            for (int row = 0; row < ringRows - 1; row++)
            {
                for (int slice = 0; slice < slices; slice++)
                {
                    int next = (slice + 1) % slices;
                    int a = rings[row][slice];
                    int b = rings[row][next];
                    int c = rings[row + 1][next];
                    int d = rings[row + 1][slice];
                    Vector3 outward = builder.Position(a) + builder.Position(c);
                    builder.Quad(a, b, c, d, outward);
                }
            }

            for (int slice = 0; slice < slices; slice++)
            {
                int next = (slice + 1) % slices;
                Vector3 outward = builder.Position(rings[ringRows - 1][slice]) +
                                  builder.Position(bottomPole);
                builder.Triangle(rings[ringRows - 1][slice], rings[ringRows - 1][next],
                    bottomPole, outward);
            }

            return builder.Build();
        }

        private static float Lump(Vector3 point, float seed)
        {
            return Mathf.Sin(point.x * 11.7f + point.y * 7.9f + point.z * 5.3f + seed) * 0.5f +
                   Mathf.Sin(point.x * 3.1f + point.y * 9.4f + point.z * 8.6f + seed * 1.7f) * 0.5f;
        }

        // ------------------------------------------------------------------ outils

        /// <summary>
        /// Construit un maillage en garantissant le sens des faces : Unity
        /// affiche le devant d'un triangle quand ses sommets tournent dans le
        /// sens horaire vu de l'extérieur, ce qui revient à un produit vectoriel
        /// opposé à la normale sortante.
        /// </summary>
        private sealed class MeshBuilder
        {
            private readonly List<Vector3> _vertices = new List<Vector3>();
            private readonly List<Vector2> _uvs = new List<Vector2>();
            private readonly List<int> _triangles = new List<int>();

            internal int Add(Vector3 position, Vector2 uv)
            {
                _vertices.Add(position);
                _uvs.Add(uv);
                return _vertices.Count - 1;
            }

            internal Vector3 Position(int index)
            {
                return _vertices[index];
            }

            internal void Triangle(int a, int b, int c, Vector3 outward)
            {
                Vector3 cross = Vector3.Cross(_vertices[b] - _vertices[a], _vertices[c] - _vertices[a]);
                if (Vector3.Dot(cross, outward) > 0f)
                {
                    _triangles.Add(a);
                    _triangles.Add(c);
                    _triangles.Add(b);
                }
                else
                {
                    _triangles.Add(a);
                    _triangles.Add(b);
                    _triangles.Add(c);
                }
            }

            internal void Quad(int a, int b, int c, int d, Vector3 outward)
            {
                Triangle(a, b, c, outward);
                Triangle(a, c, d, outward);
            }

            internal Mesh Build()
            {
                Mesh mesh = new Mesh();
                mesh.SetVertices(_vertices);
                mesh.SetUVs(0, _uvs);
                mesh.SetTriangles(_triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
