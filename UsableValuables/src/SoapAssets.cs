using System.Collections.Generic;
using UnityEngine;

namespace UsableValuables
{
    // Everything the bar of soap is made of, made here in code when the game starts, the same way the sponge is: no file is loaded.
    internal static class SoapAssets
    {
        // The bar in metres, sitting on the floor (y = 0 is its underside): a soft, rounded pill, the shape a bar of soap wears
        // down to with use.
        internal const float Length = 0.095f;
        internal const float Depth = 0.062f;
        internal const float Height = 0.032f;
        private const float Radius = 0.014f;    // just under half the height: a soft edge all round, not a sharp box

        private const int AtlasSize = 64;

        internal static Mesh BuildMesh()
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();

            // A texel in from the edge, so nothing at the border of the texture bleeds round the corner of the bar.
            Rect atlas = new Rect(1f / AtlasSize, 1f / AtlasSize, (AtlasSize - 2f) / AtlasSize, (AtlasSize - 2f) / AtlasSize);
            SpongeAssets.AddRoundedBox(vertices, normals, uvs, triangles,
                new Vector3(0f, Height * 0.5f, 0f), new Vector3(Length, Height, Depth), Radius, atlas);

            Mesh mesh = new Mesh { name = "Soap" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return SpongeAssets.Keep(mesh);
        }

        // A pastel bar with a faint marbled swirl and a few thin, slightly deeper veins, the way a well-used bar of soap looks:
        // never perfectly flat in colour.
        internal static Texture2D BuildTexture()
        {
            Color light = new Color(0.98f, 0.85f, 0.90f);     // soft pink
            Color deep = new Color(0.90f, 0.62f, 0.72f);       // the veins, a shade deeper, not another colour

            Color[] pixels = new Color[AtlasSize * AtlasSize];
            for (int y = 0; y < AtlasSize; y++)
            {
                for (int x = 0; x < AtlasSize; x++)
                {
                    float marble = Mathf.PerlinNoise(x * 0.10f + 11f, y * 0.10f + 5f);
                    float vein = Mathf.PerlinNoise(x * 0.22f - 8f, y * 0.22f + 30f);
                    float wear = Mathf.PerlinNoise(x * 0.05f + 50f, y * 0.05f + 50f);       // slow, broad shading

                    float t = Mathf.Clamp01((vein - 0.62f) * 4f) * 0.6f + (1f - marble) * 0.15f;
                    Color colour = Color.Lerp(light, deep, t);
                    colour *= 0.94f + 0.10f * wear;
                    pixels[y * AtlasSize + x] = colour;
                }
            }

            Texture2D texture = new Texture2D(AtlasSize, AtlasSize, TextureFormat.RGBA32, false)
            {
                name = "Soap",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return SpongeAssets.Keep(texture);
        }

        // Slippery and bouncy: the soap skids away from under things and bounces instead of just stopping, the way a wet bar (or a
        // rubber duck) does. Applied to every collider that could touch the floor or another object, not just the primary one.
        internal static PhysicMaterial BuildPhysicMaterial()
        {
            PhysicMaterial material = new PhysicMaterial("Soap")
            {
                dynamicFriction = 0.04f,
                staticFriction = 0.05f,
                bounciness = 0.55f,
                frictionCombine = PhysicMaterialCombine.Minimum,
                bounceCombine = PhysicMaterialCombine.Maximum,
            };
            return SpongeAssets.Keep(material);
        }
    }
}
