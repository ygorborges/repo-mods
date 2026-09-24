using System.Collections.Generic;
using UnityEngine;

namespace UsableValuables
{
    // Everything the dish sponge is made of is made here, in code, when the game starts: its model (a yellow sponge with a green
    // scrubbing layer), its texture, the bubble and droplet sprites and the drip sounds. Nothing is loaded from a file.
    internal static class SpongeAssets
    {
        // The sponge in metres, sitting on the floor (y = 0 is its underside).
        internal const float Length = 0.16f;
        internal const float Depth = 0.10f;
        private const float BodyHeight = 0.042f;
        private const float ScrubHeight = 0.024f;
        private const float Overlap = 0.006f;      // the scrubber sits this far into the body: no gap where their rounded edges meet
        internal const float Height = BodyHeight + ScrubHeight - Overlap;

        // One texture holds both layers: the yellow body on the left half, the green scrubber on the right.
        private const int HalfWidth = 64;
        private const int AtlasWidth = HalfWidth * 2;
        private const int AtlasHeight = 64;

        // The same texture density on every face (so the pores are as round on the thin sides as on top): the 62 usable texels of a
        // half cover the sponge's length.
        private const float TexelsPerMeter = (HalfWidth - 2f) / Length;

        private const int SampleRate = 44100;

        // Runtime-made assets are only referenced from code, and the game unloads assets nothing in a scene refers to.
        internal static T Keep<T>(T asset) where T : Object
        {
            if (asset != null)
            {
                asset.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            }
            return asset;
        }

        // ---- the model

        internal static Mesh BuildMesh()
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();

            // A texel in from the edge of each half, so the two never bleed into each other.
            Rect body = new Rect(1f / AtlasWidth, 1f / AtlasHeight, (HalfWidth - 2f) / AtlasWidth, (AtlasHeight - 2f) / AtlasHeight);
            Rect scrub = new Rect((HalfWidth + 1f) / AtlasWidth, 1f / AtlasHeight, (HalfWidth - 2f) / AtlasWidth, (AtlasHeight - 2f) / AtlasHeight);

            AddRoundedBox(vertices, normals, uvs, triangles,
                new Vector3(0f, BodyHeight * 0.5f, 0f), new Vector3(Length, BodyHeight, Depth), 0.007f, body);
            AddRoundedBox(vertices, normals, uvs, triangles,
                new Vector3(0f, BodyHeight - Overlap + ScrubHeight * 0.5f, 0f), new Vector3(Length, ScrubHeight, Depth), 0.005f, scrub);

            Mesh mesh = new Mesh { name = "Dish Sponge" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return Keep(mesh);
        }

        // The grid lines across one side of a rounded box: dense where the edge curves, one span across the flat part.
        private static float[] GridLines(float half, float radius)
        {
            float flat = half - radius;
            return new[]
            {
                -half, -flat - radius * 0.75f, -flat - radius * 0.5f, -flat - radius * 0.25f, -flat,
                flat, flat + radius * 0.25f, flat + radius * 0.5f, flat + radius * 0.75f, half,
            };
        }

        // Shared with SoapAssets: a rounded box is the base shape of both the sponge (two of them) and the soap bar (one, with a
        // bigger radius for its softer, pill-like silhouette).
        internal static void AddRoundedBox(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles,
            Vector3 center, Vector3 size, float radius, Rect atlas)
        {
            Vector3 half = size * 0.5f;
            Vector3 flat = half - new Vector3(radius, radius, radius);
            Vector2 middle = new Vector2(atlas.x + atlas.width * 0.5f, atlas.y + atlas.height * 0.5f);

            for (int axis = 0; axis < 3; axis++)
            {
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    int u = (axis + 1) % 3;
                    int v = (axis + 2) % 3;
                    float[] us = GridLines(half[u], radius);
                    float[] vs = GridLines(half[v], radius);
                    int first = vertices.Count;

                    for (int j = 0; j < vs.Length; j++)
                    {
                        for (int i = 0; i < us.Length; i++)
                        {
                            // A point on the surface of the plain box, and the nearest point of the box shrunk by the radius: the
                            // rounded surface is that point pushed out by the radius, and its normal is the way it was pushed.
                            Vector3 surface = Vector3.zero;
                            surface[axis] = sign * half[axis];
                            surface[u] = us[i];
                            surface[v] = vs[j];
                            Vector3 core = new Vector3(
                                Mathf.Clamp(surface.x, -flat.x, flat.x),
                                Mathf.Clamp(surface.y, -flat.y, flat.y),
                                Mathf.Clamp(surface.z, -flat.z, flat.z));
                            Vector3 away = surface - core;
                            Vector3 normal = away.normalized;
                            vertices.Add(center + core + normal * radius);
                            normals.Add(normal);
                            uvs.Add(new Vector2(
                                middle.x + us[i] * TexelsPerMeter / AtlasWidth,
                                middle.y + vs[j] * TexelsPerMeter / AtlasHeight));
                        }
                    }

                    int row = us.Length;

                    // Which way the triangles must run to face outwards, read off the middle of the face (whose normal is exact).
                    int mid = us.Length / 2 - 1;
                    int a0 = first + mid * row + mid;
                    Vector3 geometric = Vector3.Cross(vertices[a0 + 1] - vertices[a0], vertices[a0 + row] - vertices[a0]);
                    bool forward = Vector3.Dot(geometric, normals[a0]) > 0f;

                    for (int j = 0; j < vs.Length - 1; j++)
                    {
                        for (int i = 0; i < us.Length - 1; i++)
                        {
                            int a = first + j * row + i;
                            int b = a + 1;
                            int c = a + row;
                            int d = c + 1;
                            if (forward)
                            {
                                triangles.Add(a); triangles.Add(b); triangles.Add(c);
                                triangles.Add(b); triangles.Add(d); triangles.Add(c);
                            }
                            else
                            {
                                triangles.Add(a); triangles.Add(c); triangles.Add(b);
                                triangles.Add(b); triangles.Add(c); triangles.Add(d);
                            }
                        }
                    }
                }
            }
        }

        // ---- the texture

        internal static Texture2D BuildTexture()
        {
            System.Random random = new System.Random(4242);
            int poreCount = 90;
            float[] poreX = new float[poreCount];
            float[] poreY = new float[poreCount];
            float[] poreR = new float[poreCount];
            for (int k = 0; k < poreCount; k++)
            {
                poreX[k] = 1f + (float)random.NextDouble() * (HalfWidth - 2f);
                poreY[k] = 1f + (float)random.NextDouble() * (AtlasHeight - 2f);
                poreR[k] = 0.7f + (float)random.NextDouble() * 1.4f;
            }

            Color[] pixels = new Color[AtlasWidth * AtlasHeight];
            for (int y = 0; y < AtlasHeight; y++)
            {
                for (int x = 0; x < AtlasWidth; x++)
                {
                    pixels[y * AtlasWidth + x] = x < HalfWidth ? BodyPixel(x, y, poreX, poreY, poreR) : ScrubPixel(x - HalfWidth, y);
                }
            }

            Texture2D texture = new Texture2D(AtlasWidth, AtlasHeight, TextureFormat.RGBA32, false)
            {
                name = "Dish Sponge",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return Keep(texture);
        }

        private static Color BodyPixel(int x, int y, float[] poreX, float[] poreY, float[] poreR)
        {
            float grain = Mathf.PerlinNoise(x * 0.25f + 3f, y * 0.25f + 7f);
            float shade = 0.94f + 0.12f * grain;
            Color color = new Color(0.98f * shade, 0.84f * shade, 0.22f * shade, 1f);

            float hole = 0f;
            for (int k = 0; k < poreX.Length; k++)
            {
                float dx = x - poreX[k];
                float dy = y - poreY[k];
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                hole = Mathf.Max(hole, 1f - Mathf.Clamp01((distance - poreR[k] * 0.7f) / (poreR[k] * 0.3f + 0.6f)));
            }
            return Color.Lerp(color, new Color(0.55f, 0.38f, 0.03f, 1f), hole * 0.9f);
        }

        private static Color ScrubPixel(int x, int y)
        {
            float fibre = Mathf.PerlinNoise(x * 0.10f + 20f, y * 1.4f + 5f);     // streaks along the sponge
            float speck = Mathf.PerlinNoise(x * 0.6f + 40f, y * 0.6f + 40f);
            Color color = Color.Lerp(new Color(0.09f, 0.42f, 0.17f, 1f), new Color(0.24f, 0.70f, 0.30f, 1f), fibre);
            if (speck > 0.72f)
            {
                color = Color.Lerp(color, new Color(0.05f, 0.25f, 0.10f, 1f), 0.6f);
            }
            return color;
        }

        // Shaders that show a texture and a colour as they are, lit like any object. The first one the game has is the sponge's.
        private static readonly string[] surfaceShaders = { "Standard", "Legacy Shaders/Diffuse", "Mobile/Diffuse" };

        // The sponge's material. The donor valuable's own is the last resort: the first version copied it, and the Arctic Eraser's
        // is the game's "ShinyMetal", which turned the sponge into dark metal with none of its colours.
        internal static Material BuildMaterial(Material donor, Texture2D texture, out string shaderName)
        {
            Material material = null;
            shaderName = null;
            foreach (string candidate in surfaceShaders)
            {
                Shader shader = Shader.Find(candidate);
                if (shader != null && shader.isSupported)
                {
                    material = new Material(shader);
                    shaderName = candidate;
                    break;
                }
            }
            if (material == null && donor != null)
            {
                material = new Material(donor);
                shaderName = donor.shader.name + " (the base valuable's)";
                foreach (string property in material.GetTexturePropertyNames())
                {
                    material.SetTexture(property, null);
                }
            }
            if (material == null)
            {
                material = new Material(Shader.Find("Sprites/Default"));
                shaderName = "Sprites/Default";
            }
            material.name = "Dish Sponge";
            material.mainTexture = texture;
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0f);
            }
            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", 0.2f);
            }
            foreach (string property in new[] { "_MainTex", "_BaseMap", "_BaseColorMap", "_AlbedoMap", "_Albedo" })
            {
                if (material.HasProperty(property))
                {
                    material.SetTexture(property, texture);
                }
            }
            foreach (string property in new[] { "_Color", "_BaseColor", "_AlbedoColor" })
            {
                if (material.HasProperty(property))
                {
                    material.SetColor(property, Color.white);
                }
            }
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", Color.black);
            }
            return Keep(material);
        }

        // ---- the bubbles

        // A soap bubble: a thin bright rim with a faint rainbow tint, a clear middle and a highlight.
        internal static Texture2D BuildBubbleTexture()
        {
            const int size = 64;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size * 2f - 1f;
                    float ny = (y + 0.5f) / size * 2f - 1f;
                    float distance = Mathf.Sqrt(nx * nx + ny * ny);

                    float rim = Smooth(0.70f, 0.93f, distance) * (1f - Smooth(0.93f, 1f, distance));
                    float fill = 0.16f * (1f - Smooth(0.93f, 1f, distance));
                    float hx = nx + 0.38f;
                    float hy = ny - 0.38f;
                    float highlight = 1f - Smooth(0f, 0.22f, Mathf.Sqrt(hx * hx + hy * hy));

                    Color tint = Color.HSVToRGB(Mathf.Repeat(Mathf.Atan2(ny, nx) / (2f * Mathf.PI) + 0.5f, 1f), 0.35f, 1f);
                    Color color = Color.Lerp(Color.white, tint, rim * 0.6f);
                    color.a = Mathf.Clamp01(rim + fill + highlight * 0.9f);
                    pixels[y * size + x] = color;
                }
            }
            return Keep(MakeSprite("Sponge Bubble", size, pixels));
        }

        // A droplet: a soft round dot, tinted by the particle's own colour.
        internal static Texture2D BuildDropTexture()
        {
            const int size = 32;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size * 2f - 1f;
                    float ny = (y + 0.5f) / size * 2f - 1f;
                    float distance = Mathf.Sqrt(nx * nx + ny * ny);
                    float alpha = 1f - Smooth(0.45f, 1f, distance);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            return Keep(MakeSprite("Sponge Drop", size, pixels));
        }

        private static Texture2D MakeSprite(string name, int size, Color[] pixels)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static float Smooth(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        // Particle materials, from the first of the shaders the game has: the bubbles only need a plain alpha-blended one.
        private static readonly string[] particleShaders =
        {
            "Sprites/Default", "Legacy Shaders/Particles/Alpha Blended", "Particles/Standard Unlit", "UI/Default",
        };

        // Null if the game has none of them (the sponge then makes its sounds without the bubbles).
        internal static Material BuildParticleMaterial(Texture2D texture, string name, out string shaderName)
        {
            foreach (string candidate in particleShaders)
            {
                Shader shader = Shader.Find(candidate);
                if (shader == null || !shader.isSupported)
                {
                    continue;
                }
                Material material = new Material(shader) { name = name, mainTexture = texture };
                if (material.HasProperty("_Color"))
                {
                    material.SetColor("_Color", Color.white);
                }
                if (material.HasProperty("_TintColor"))
                {
                    material.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
                }
                shaderName = candidate;
                return Keep(material);
            }
            shaderName = null;
            return null;
        }

        // ---- the sounds

        // A drip: a drop of water is a brief bright "plink", not a slide. The first version climbed a whole octave over a quarter of a
        // second and sounded like a spring. This one slides a third of the way up in the first few milliseconds and stays there, with
        // a second, inharmonic partial that dies even faster (what makes it ring like water or glass rather than a plain beep) and
        // a tick of noise at the very start; the whole thing is over in about a tenth of a second.
        internal static AudioClip[] BuildDrips()
        {
            float[] pitches = { 1500f, 1850f, 2200f, 2600f, 3000f };
            float[] slides = { 0.28f, 0.34f, 0.30f, 0.36f, 0.32f };
            float[] decays = { 0.018f, 0.015f, 0.017f, 0.013f, 0.015f };
            AudioClip[] clips = new AudioClip[pitches.Length];
            System.Random random = new System.Random(77);
            for (int c = 0; c < clips.Length; c++)
            {
                int count = (int)(SampleRate * 0.12f);
                float[] samples = new float[count];
                float phase = 0f;
                for (int i = 0; i < count; i++)
                {
                    float t = i / (float)SampleRate;
                    float frequency = pitches[c] * (1f + slides[c] * (1f - Mathf.Exp(-t / 0.006f)));
                    phase += 2f * Mathf.PI * frequency / SampleRate;
                    float envelope = (1f - Mathf.Exp(-t / 0.0008f)) * Mathf.Exp(-t / decays[c]);
                    float partial = 0.25f * Mathf.Sin(phase * 2.37f) * Mathf.Exp(-t / 0.008f);
                    float tick = i < 90 ? ((float)random.NextDouble() * 2f - 1f) * Mathf.Exp(-i / 18f) * 0.22f : 0f;
                    samples[i] = (Mathf.Sin(phase) + partial) * envelope * 0.55f + tick;
                }
                FadeOut(samples, 0.008f);
                clips[c] = MakeClip("Sponge Drip " + (c + 1), samples);
            }
            return clips;
        }

        // A bubble bursting: a very short soft crackle over a low tick.
        internal static AudioClip BuildPop()
        {
            System.Random random = new System.Random(99);
            int count = (int)(SampleRate * 0.07f);
            float[] samples = new float[count];
            float smooth = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float noise = (float)random.NextDouble() * 2f - 1f;
                smooth += (noise - smooth) * 0.45f;
                float tick = Mathf.Sin(2f * Mathf.PI * 620f * t);
                samples[i] = (smooth * 0.8f + tick * 0.45f) * Mathf.Exp(-t / 0.010f) * 0.7f;
            }
            FadeOut(samples, 0.01f);
            return MakeClip("Sponge Pop", samples);
        }

        private static void FadeOut(float[] samples, float seconds)
        {
            int length = Mathf.Min(samples.Length, (int)(seconds * SampleRate));
            for (int i = 0; i < length; i++)
            {
                samples[samples.Length - 1 - i] *= i / (float)length;
            }
        }

        private static AudioClip MakeClip(string name, float[] samples)
        {
            AudioClip clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return Keep(clip);
        }
    }
}
