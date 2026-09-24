using System;
using Photon.Pun;
using UnityEngine;

namespace MoonControl
{
    // The little moon on a pedestal that opens the menu when you grab it, the same way the extraction point's own button (and the
    // truck's own controls) work: not the Interact key, but the grab key, held on an object tagged and layered exactly like the
    // game's own StaticGrabObject-based buttons (PhysGrabber only ever looks for one on the "StaticGrabObject" layer, tagged
    // "Phys Grab Object" - see PhysGrabber's own raycast and SemiFunc.LayerMaskGetVisionObstruct).
    //
    // Because grabbing has to go through the game's own networked grab protocol to look and feel right (the hand actually
    // locking onto it, other players seeing the same), this is a real registered, networked prefab (REPOLib), spawned by the
    // host at a fixed spot in the truck each time it appears - not a purely decorative local object like the sponge or the
    // soap.
    internal static class MoonButton
    {
        private const string PrefabName = "MoonControl Button";
        private const float MoonDiameter = 0.3f;
        private const float PedestalHeight = 0.6f;
        private const float PedestalRadius = 0.14f;
        private const int MoonTextureSize = 256;
        // The pale, slightly cool white the moon gives off.
        private static readonly Color GlowColour = new Color(0.84f, 0.88f, 1f);

        // The pedestal's palette.
        private static readonly Color Steel = new Color(0.30f, 0.34f, 0.41f);
        private static readonly Color SteelLight = new Color(0.47f, 0.51f, 0.58f);
        private static readonly Color GoldTrim = new Color(0.86f, 0.66f, 0.26f);
        private static readonly Color GoldDark = new Color(0.48f, 0.35f, 0.12f);
        private static readonly Color GrooveShadow = new Color(0.07f, 0.08f, 0.10f);
        private static readonly Color Grime = new Color(0.16f, 0.13f, 0.10f);

        private static PrefabRef buttonRef;
        private static GameObject currentInstance;
        private static Material moonMaterial;

        internal static bool Ready
        {
            get { return buttonRef != null; }
        }

        // Whether a placed button is actually still there (Unity's own null check covers one destroyed along with a scene).
        internal static bool Placed
        {
            get { return currentInstance != null; }
        }

        // ---- the prefab (built once)

        internal static void Register()
        {
            if (buttonRef != null)
            {
                return;
            }
            try
            {
                GameObject prefab = Build();
                PrefabRef reference = REPOLib.Modules.NetworkPrefabs.RegisterNetworkPrefab("MoonControl/" + PrefabName, prefab);
                if (reference == null)
                {
                    throw new InvalidOperationException("REPOLib did not accept the prefab");
                }
                buttonRef = reference;
                Plugin.Log.LogInfo("The moon button is registered.");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("The moon button was not added: " + ex);
            }
        }

        private static GameObject Build()
        {
            GameObject holder = new GameObject("MoonControl prefabs");
            holder.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(holder);

            GameObject prefab = new GameObject(PrefabName);
            prefab.transform.SetParent(holder.transform, false);
            prefab.tag = "Phys Grab Object";
            prefab.layer = LayerMask.NameToLayer("StaticGrabObject");

            GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "Pedestal";
            pedestal.transform.SetParent(prefab.transform, false);
            pedestal.transform.localPosition = new Vector3(0f, PedestalHeight * 0.5f, 0f);
            pedestal.transform.localScale = new Vector3(PedestalRadius * 2f, PedestalHeight * 0.5f, PedestalRadius * 2f);
            UnityEngine.Object.Destroy(pedestal.GetComponent<Collider>());
            pedestal.GetComponent<Renderer>().sharedMaterial = BuildMaterial("Moon Pedestal", BuildPedestalTexture(), 0.55f, 0.5f);

            GameObject moon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            moon.name = "Moon";
            moon.transform.SetParent(prefab.transform, false);
            moon.transform.localPosition = new Vector3(0f, PedestalHeight + MoonDiameter * 0.5f, 0f);
            moon.transform.localScale = Vector3.one * MoonDiameter;
            UnityEngine.Object.Destroy(moon.GetComponent<Collider>());

            Texture2D moonTexture = BuildMoonTexture();
            moonMaterial = BuildMaterial("Moon", moonTexture);
            // Lit from within as well as from outside: the surface itself gives off a pale white (its own texture as the
            // emission map, so the dark plains stay dark instead of the whole ball turning into a flat white blob). With
            // the game's bloom on top this is what actually reads as a glow.
            moonMaterial.EnableKeyword("_EMISSION");
            moonMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            moonMaterial.SetTexture("_EmissionMap", moonTexture);
            moon.GetComponent<Renderer>().sharedMaterial = moonMaterial;
            moon.AddComponent<Spin>();

            // And a real light, so it also casts that pale white onto the pedestal and the floor of a dark truck - the
            // part of the glow that does not depend on the game's post-processing being on.
            GameObject glow = new GameObject("Glow");
            glow.transform.SetParent(moon.transform, false);
            Light light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = GlowColour;
            light.shadows = LightShadows.None;
            light.range = 4.5f;

            // One collider around the whole thing: what the grab raycast actually hits, on the layer and tag the game
            // reserves for StaticGrabObject buttons.
            BoxCollider collider = prefab.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, (PedestalHeight + MoonDiameter) * 0.5f, 0f);
            collider.size = new Vector3(PedestalRadius * 2.4f, PedestalHeight + MoonDiameter, PedestalRadius * 2.4f);

            prefab.AddComponent<PhotonView>();
            StaticGrabObject grab = prefab.AddComponent<StaticGrabObject>();
            grab.colliderTransform = prefab.transform;
            grab.hoverText = "Grab to open moon control";

            prefab.AddComponent<MoonButtonWatcher>();
            return prefab;
        }

        // ---- spawning in the truck (host only, one at a time)

        // A fixed world position (ButtonPositionX/Y/Z) instead of an offset from some landmark in the truck: the level-number
        // screen this used to anchor to only shows a number once the next level has actually started - during the
        // intermission itself (the moment this button actually needs to exist) it sits there blank, so it was never a
        // reliable thing to search for. The truck's own place in the world stays put, so a plain fixed spot works and needs
        // no raycast or search at all.
        internal static void SpawnFixed(bool announce = true)
        {
            if (!Ready)
            {
                return;
            }
            DespawnCurrent();

            Vector3 position = new Vector3(Plugin.ButtonPositionX.Value, Plugin.ButtonPositionY.Value, Plugin.ButtonPositionZ.Value);
            Quaternion rotation = Quaternion.Euler(0f, Plugin.ButtonYawDegrees.Value, 0f);
            GameObject instance = REPOLib.Modules.NetworkPrefabs.SpawnNetworkPrefab(buttonRef, position, rotation);
            currentInstance = instance;
            if (instance == null)
            {
                return;
            }
            // Local-only (does not reach other clients in multiplayer): a troubleshooting aid for finding the button, not
            // something that needs to be perfectly in sync between machines.
            float scale = Mathf.Max(0.05f, Plugin.ButtonScale.Value);
            instance.transform.localScale = Vector3.one * scale;
            ApplyGlow(instance);
            string message = "Moon button placed at " + instance.transform.position.ToString("F2") + " (scale " + scale.ToString("0.##") + "x).";
            // Only the first placement of a visit is worth a line in the log; re-placements (live tuning, or replacing one
            // that went missing) would otherwise fill it up.
            if (announce)
            {
                Plugin.Log.LogInfo(message);
            }
            else
            {
                Plugin.Log.LogDebug(message);
            }
        }

        // Applied when the button is placed rather than baked into the prefab, so ButtonGlow can be turned up or down (or
        // off) from the config without restarting the game.
        private static void ApplyGlow(GameObject instance)
        {
            float glow = Mathf.Max(0f, Plugin.ButtonGlow.Value);
            if (moonMaterial != null)
            {
                // Above 1 so the game's bloom picks it up and it reads as glowing, not just as a light-coloured ball.
                moonMaterial.SetColor("_EmissionColor", GlowColour * (1.5f * glow));
            }
            Light light = instance.GetComponentInChildren<Light>();
            if (light != null)
            {
                light.intensity = 2.2f * glow;
                light.enabled = glow > 0f;
            }
        }

        internal static void DespawnCurrent()
        {
            if (currentInstance == null)
            {
                return;
            }
            if (SemiFunc.IsMultiplayer())
            {
                PhotonView view = currentInstance.GetComponent<PhotonView>();
                if (view != null && view.IsMine)
                {
                    PhotonNetwork.Destroy(currentInstance);
                }
            }
            else
            {
                UnityEngine.Object.Destroy(currentInstance);
            }
            currentInstance = null;
        }

        // A slow, steady turn, just so the button reads as something alive rather than a static decoration.
        private sealed class Spin : MonoBehaviour
        {
            private void Update()
            {
                transform.Rotate(Vector3.up, 12f * Time.deltaTime, Space.Self);
            }
        }

        // ---- assets (same recipe as the dish sponge and the soap: nothing loaded from a file)

        // Drawn in the sphere's own coordinates rather than as a flat image: the texture is wrapped around a sphere by
        // longitude and latitude, so a crater drawn as a plain circle here comes out stretched into a smear near the poles
        // and cut in half at the seam. Every crater is therefore measured in real angular distance across the surface
        // (the horizontal part shrunk by cos(latitude), the seam wrapped around), placed by equal surface area so they do
        // not bunch up at the poles, and given a lit rim and a shadowed floor on opposite sides so the surface reads as
        // pitted rather than stained. On top of that: dark "maria" plains, faint bright rays thrown out by the largest
        // craters, and fine grain.
        private static Texture2D BuildMoonTexture()
        {
            System.Random random = new System.Random(9001);

            // The dark plains: a few big soft blobs, the way a real moon has a handful of large seas rather than an even
            // speckle of dark noise.
            const int seaCount = 7;
            float[] su = new float[seaCount];
            float[] sv = new float[seaCount];
            float[] sr = new float[seaCount];
            for (int i = 0; i < seaCount; i++)
            {
                su[i] = (float)random.NextDouble();
                // Equal-area latitude: uniform v would crowd everything towards the poles, where the wrap squeezes it
                // together.
                sv[i] = Mathf.Acos(1f - 2f * (float)random.NextDouble()) / Mathf.PI;
                sr[i] = 0.10f + (float)random.NextDouble() * 0.13f;
            }

            const int craterCount = 55;
            float[] cu = new float[craterCount];        // longitude, 0-1
            float[] cv = new float[craterCount];        // latitude, 0-1
            float[] cr = new float[craterCount];        // radius, as a fraction of the texture height
            for (int i = 0; i < craterCount; i++)
            {
                cu[i] = (float)random.NextDouble();
                cv[i] = Mathf.Acos(1f - 2f * (float)random.NextDouble()) / Mathf.PI;
                double roll = random.NextDouble();
                cr[i] = roll < 0.05 ? 0.035f + (float)random.NextDouble() * 0.03f       // a few big ones
                    : roll < 0.3 ? 0.013f + (float)random.NextDouble() * 0.014f         // some mid-sized ones
                    : 0.004f + (float)random.NextDouble() * 0.008f;                     // mostly small pockmarks
            }

            // The light the baked relief pretends to come from (up and to the left), so rims catch a highlight on one
            // side and floors keep a shadow on the other.
            Vector2 sun = new Vector2(-0.7f, 0.7f).normalized;

            Color[] pixels = new Color[MoonTextureSize * MoonTextureSize];
            for (int y = 0; y < MoonTextureSize; y++)
            {
                float v = (y + 0.5f) / MoonTextureSize;
                float cosLat = Mathf.Max(Mathf.Cos((v - 0.5f) * Mathf.PI), 0.12f);
                for (int x = 0; x < MoonTextureSize; x++)
                {
                    float u = (x + 0.5f) / MoonTextureSize;

                    float maria = 0f;
                    for (int i = 0; i < seaCount; i++)
                    {
                        float dv = v - sv[i];
                        if (dv > sr[i] * 1.6f || dv < -sr[i] * 1.6f)
                        {
                            continue;
                        }
                        float du = u - su[i];
                        du -= Mathf.Round(du);         // across the seam, take the short way round
                        float dx = du * cosLat;
                        // A wobble on the radius keeps a sea from being an obvious circle.
                        float wobble = 0.72f + 0.5f * Mathf.PerlinNoise(x * 0.05f + i * 7f, y * 0.05f + i * 13f);
                        float d = Mathf.Sqrt(dx * dx + dv * dv) / (sr[i] * wobble);
                        maria = Mathf.Max(maria, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - d) / 0.32f)));
                    }
                    float shade = Mathf.Lerp(0.80f, 0.44f, maria);
                    shade += (Mathf.PerlinNoise(x * 0.20f + 60f, y * 0.20f + 130f) - 0.5f) * 0.07f;

                    float relief = 0f;                 // >0 sunlit rim, <0 shadowed floor
                    float floorDarkening = 0f;
                    float rays = 0f;
                    for (int i = 0; i < craterCount; i++)
                    {
                        float dv = v - cv[i];
                        if (dv > cr[i] * 2.6f || dv < -cr[i] * 2.6f)
                        {
                            continue;                  // nowhere near this row: skip the expensive part
                        }
                        float du = u - cu[i];
                        du -= Mathf.Round(du);
                        float dx = du * cosLat;
                        float d = Mathf.Sqrt(dx * dx + dv * dv) / cr[i];
                        if (d > 2.6f)
                        {
                            continue;
                        }

                        float lit = d > 0.001f ? (dx * sun.x + dv * sun.y) / (d * cr[i]) : 0f;
                        if (d < 1f)
                        {
                            // Inside: a bowl that darkens towards the middle, shaded across the sun's direction.
                            float depth = Mathf.Sqrt(1f - d * d);
                            floorDarkening = Mathf.Max(floorDarkening, depth * 0.20f);
                            relief += -lit * depth * 0.22f;
                        }
                        else if (d < 1.28f)
                        {
                            // The raised rim around it.
                            float ring = 1f - Mathf.Abs(d - 1.12f) / 0.16f;
                            relief += Mathf.Clamp01(ring) * lit * 0.30f;
                        }
                        else if (cr[i] > 0.05f)
                        {
                            // Only the big ones throw rays, and only faintly.
                            float streak = Mathf.PerlinNoise(Mathf.Atan2(dv, dx) * 3.2f + i * 10f, 0.5f);
                            rays = Mathf.Max(rays, Mathf.Clamp01(streak - 0.45f) * Mathf.Clamp01(1f - (d - 1.28f) / 1.3f) * 0.5f);
                        }
                    }

                    shade = Mathf.Clamp01(shade * (1f - floorDarkening) + relief * 0.4f + rays * 0.10f);
                    // Never quite colourless: the highlands read a touch warm, the plains a touch cool.
                    pixels[y * MoonTextureSize + x] = new Color(shade * (1.03f - maria * 0.05f), shade, shade * (0.95f + maria * 0.07f));
                }
            }
            return Keep(MakeTexture("Moon", pixels));
        }

        // Brushed steel rather than the near-black it used to be (in a dim truck that just read as a black tube): fine
        // vertical brushing, panel seams around it, a gold band with bolts at about two thirds up, a thin pinstripe
        // echoing it lower down, a polished collar where the moon sits, and grime collecting around the foot. Unity's
        // cylinder wraps u around the column and runs v from its foot to its top, so everything here is placed by v.
        private static Texture2D BuildPedestalTexture()
        {
            const int size = 256;
            const int facets = 8;              // panel seams, and a bolt centred between each pair
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;

                    // Brushing: near-random around the column, but barely changing up it, which is what turns it into
                    // fine vertical streaks rather than plain noise.
                    float brushed = 0.90f + 0.18f * Mathf.PerlinNoise(x * 0.37f, y * 0.04f);
                    Color colour = Color.Lerp(Steel, SteelLight,
                        Mathf.Clamp01(Mathf.PerlinNoise(x * 0.25f + 40f, y * 0.05f + 12f) * 0.6f)) * brushed;
                    colour *= 0.66f + 0.46f * v;   // darker at the foot, brighter up near the moon

                    float gold = Band(v, 0.60f, 0.72f, 0.012f);
                    if (gold > 0f)
                    {
                        Color brass = Color.Lerp(GoldDark, GoldTrim,
                            Mathf.Clamp01(0.45f + 0.7f * Mathf.PerlinNoise(x * 0.8f + 5f, y * 0.05f + 3f)));
                        colour = Color.Lerp(colour, brass, gold);
                    }
                    colour = Color.Lerp(colour, GoldDark, Band(v, 0.145f, 0.165f, 0.008f) * 0.85f);

                    float groove = Mathf.Max(Band(v, 0.578f, 0.592f, 0.006f),
                        Mathf.Max(Band(v, 0.728f, 0.742f, 0.006f), Band(v, 0.888f, 0.900f, 0.006f)));
                    colour = Color.Lerp(colour, GrooveShadow, groove * 0.9f);
                    colour = Color.Lerp(colour, SteelLight * 1.15f, Band(v, 0.905f, 1f, 0.01f) * 0.8f);

                    float seam = 0f;
                    float bolt = 0f;
                    for (int k = 0; k < facets; k++)
                    {
                        float du = Mathf.Abs(u - (float)k / facets);
                        du = Mathf.Min(du, 1f - du);
                        seam = Mathf.Max(seam, Mathf.Clamp01(1f - du / 0.006f));

                        float bu = Mathf.Abs(u - (k + 0.5f) / facets);
                        bu = Mathf.Min(bu, 1f - bu) * 1.5f;
                        float dv = v - 0.66f;
                        bolt = Mathf.Max(bolt, Mathf.Clamp01(1f - Mathf.Sqrt(bu * bu + dv * dv) / 0.026f));
                    }
                    colour = Color.Lerp(colour, GrooveShadow, seam * 0.55f * (1f - gold));
                    if (bolt > 0f)
                    {
                        float shade = bolt > 0.55f ? 1.35f : 0.55f;   // a lit head with a shadowed edge
                        colour = Color.Lerp(colour, GoldTrim * shade, Mathf.Clamp01(bolt * 1.6f));
                    }

                    colour *= 0.90f + 0.16f * Mathf.PerlinNoise(x * 0.05f + 70f, y * 0.05f + 30f);
                    float dirt = Mathf.Clamp01(1f - v / 0.30f) * (0.35f + 0.5f * Mathf.PerlinNoise(x * 0.12f + 11f, y * 0.12f + 91f));
                    colour = Color.Lerp(colour, Grime, dirt * 0.5f);

                    colour.a = 1f;
                    pixels[y * size + x] = colour;
                }
            }
            return Keep(MakeTexture("Moon Pedestal", pixels));
        }

        // 1 inside [lo, hi], fading out over `feather` on each side - the horizontal bands the pedestal is made of.
        private static float Band(float v, float lo, float hi, float feather)
        {
            if (v < lo)
            {
                return Mathf.Clamp01(1f - (lo - v) / feather);
            }
            if (v > hi)
            {
                return Mathf.Clamp01(1f - (v - hi) / feather);
            }
            return 1f;
        }

        private static Texture2D MakeTexture(string name, Color[] pixels)
        {
            int size = Mathf.RoundToInt(Mathf.Sqrt(pixels.Length));
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        // The same lesson the dish sponge learned the hard way: pick a shader that actually shows a texture and a colour as
        // they are, lit like any object, instead of copying a donor's material.
        private static readonly string[] surfaceShaders = { "Standard", "Legacy Shaders/Diffuse", "Mobile/Diffuse" };

        private static Material BuildMaterial(string name, Texture2D texture, float metallic = 0f, float glossiness = 0.1f)
        {
            Material material = null;
            foreach (string candidate in surfaceShaders)
            {
                Shader shader = Shader.Find(candidate);
                if (shader != null && shader.isSupported)
                {
                    material = new Material(shader);
                    break;
                }
            }
            if (material == null)
            {
                material = new Material(Shader.Find("Sprites/Default"));
            }
            material.name = name;
            material.mainTexture = texture;
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }
            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", glossiness);
            }
            return Keep(material);
        }

        private static T Keep<T>(T asset) where T : UnityEngine.Object
        {
            if (asset != null)
            {
                asset.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            }
            return asset;
        }
    }

    // On the button's own prefab: watches the local player's grab, entirely client-side (grabbedStaticGrabObject is set the
    // moment the local PhysGrabber starts grabbing something, on whichever machine that is - no need to wait on the network
    // just to know "I grabbed it").
    internal sealed class MoonButtonWatcher : MonoBehaviour
    {
        private StaticGrabObject grab;
        private bool wasGrabbing;

        private void Awake()
        {
            grab = GetComponent<StaticGrabObject>();
        }

        private void Update()
        {
            if (!Plugin.Enabled.Value || grab == null)
            {
                return;
            }
            PhysGrabber local = PhysGrabber.instance;
            bool grabbingNow = local != null && Refs.GrabbedStaticGrabObject(local) == grab;
            if (grabbingNow && !wasGrabbing)
            {
                try
                {
                    MoonMenu.Open();
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError("Opening the moon menu failed: " + ex);
                }
            }
            wasGrabbing = grabbingNow;
        }
    }
}
