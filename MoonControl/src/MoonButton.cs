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
        private const int MoonTextureSize = 128;

        private static PrefabRef buttonRef;
        private static GameObject currentInstance;

        internal static bool Ready
        {
            get { return buttonRef != null; }
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
            pedestal.GetComponent<Renderer>().sharedMaterial = BuildMaterial("Moon Pedestal", BuildPedestalTexture(), 0.35f, 0.4f);

            GameObject moon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            moon.name = "Moon";
            moon.transform.SetParent(prefab.transform, false);
            moon.transform.localPosition = new Vector3(0f, PedestalHeight + MoonDiameter * 0.5f, 0f);
            moon.transform.localScale = Vector3.one * MoonDiameter;
            UnityEngine.Object.Destroy(moon.GetComponent<Collider>());
            moon.GetComponent<Renderer>().sharedMaterial = BuildMaterial("Moon", BuildMoonTexture());
            moon.AddComponent<Spin>();

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
        internal static void SpawnFixed()
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
            if (instance != null)
            {
                // Local-only (does not reach other clients in multiplayer): a troubleshooting aid for finding the button, not
                // something that needs to be perfectly in sync between machines.
                float scale = Mathf.Max(0.05f, Plugin.ButtonScale.Value);
                instance.transform.localScale = Vector3.one * scale;
                Plugin.Log.LogInfo("Moon button placed at " + instance.transform.position.ToString("F2") + " (scale " + scale.ToString("0.##") + "x).");
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

        // Two noise frequencies (a slow one for the dark "maria" patches a real moon has, a fast one for fine surface
        // grain) plus craters that now have a raised bright rim around their dark bowl, not just a flat dark disc - and a
        // faint warm/cool tint instead of flat grey (a real moon reads as neutral at a glance but is never quite
        // colourless: warmer in the lit highlands, cooler in the maria).
        private static Texture2D BuildMoonTexture()
        {
            System.Random random = new System.Random(9001);
            int craterCount = 40;
            float[] cx = new float[craterCount];
            float[] cy = new float[craterCount];
            float[] cr = new float[craterCount];
            for (int i = 0; i < craterCount; i++)
            {
                cx[i] = (float)random.NextDouble() * MoonTextureSize;
                cy[i] = (float)random.NextDouble() * (MoonTextureSize * 0.5f);
                // Mostly small pockmarks, occasionally a noticeably bigger crater - closer to how a real lunar surface
                // reads than a field of same-sized dots.
                cr[i] = random.NextDouble() < 0.15 ? 7f + (float)random.NextDouble() * 7f : 1.5f + (float)random.NextDouble() * 3.5f;
            }

            Color[] pixels = new Color[MoonTextureSize * MoonTextureSize];
            for (int y = 0; y < MoonTextureSize; y++)
            {
                for (int x = 0; x < MoonTextureSize; x++)
                {
                    float maria = Mathf.PerlinNoise(x * 0.025f + 4f, y * 0.025f + 9f);
                    float grain = Mathf.PerlinNoise(x * 0.09f + 40f, y * 0.09f + 90f);
                    float shade = 0.80f - maria * 0.22f + grain * 0.08f;

                    float hole = 0f;
                    float rim = 0f;
                    for (int i = 0; i < craterCount; i++)
                    {
                        float dx = x - cx[i];
                        float dy = (y % MoonTextureSize) - cy[i];
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        hole = Mathf.Max(hole, 1f - Mathf.Clamp01((d - cr[i] * 0.5f) / (cr[i] * 0.4f + 0.6f)));
                        float ringDist = Mathf.Abs(d - cr[i] * 0.62f);
                        rim = Mathf.Max(rim, 1f - Mathf.Clamp01(ringDist / (cr[i] * 0.16f + 0.4f)));
                    }
                    shade = Mathf.Clamp01(shade * (1f - hole * 0.4f) + rim * 0.10f);

                    pixels[y * MoonTextureSize + x] = new Color(shade * 1.04f, shade * 0.99f, shade * 0.94f);
                }
            }
            return Keep(MakeTexture("Moon", pixels));
        }

        // Dark blue-grey brushed metal with a thin warm-gold trim band near the top rim (Unity's default cylinder UV puts
        // v=1 there, right where it meets the moon) - echoes the same gold the moon menu's own highlights use, instead of
        // the flat grey it had before.
        private static Texture2D BuildPedestalTexture()
        {
            const int size = 32;
            Color baseColor = new Color(0.10f, 0.14f, 0.19f);
            Color trimColor = new Color(0.86f, 0.63f, 0.20f);
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = y / (float)(size - 1);
                float trim = Mathf.Clamp01((v - 0.85f) / 0.12f);
                for (int x = 0; x < size; x++)
                {
                    float grain = Mathf.PerlinNoise(x * 0.25f + 20f, y * 0.25f + 40f);
                    float brushed = 0.88f + 0.14f * grain;
                    Color colour = Color.Lerp(baseColor, trimColor, trim) * brushed;
                    colour.a = 1f;
                    pixels[y * size + x] = colour;
                }
            }
            return Keep(MakeTexture("Moon Pedestal", pixels));
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
