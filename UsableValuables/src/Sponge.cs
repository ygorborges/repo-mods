using System;
using System.Collections.Generic;
using System.Text;
using BepInEx.Configuration;
using ExitGames.Client.Photon;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UsableValuables
{
    // The dish sponge: a valuable this mod adds. It is registered with REPOLib like any modded valuable, and it turns up in the levels
    // like any other small valuable; the bubbles and the drips when it is moved are SpongeFx.
    //
    // There is no model to load, so the prefab is a copy of one of the game's own small valuables (whatever has the fewest extras on
    // it, so it carries all the physics, networking and price logic and nothing that would make it act like something else) with
    // its looks and its shape swapped for the sponge's.
    //
    // A valuable that the host spawns has to exist on every machine: a player who cannot create it waits at the loading screen for
    // ever. So every player who has the sponge says so (a Photon player property), and the host only lets the sponge into a
    // level's valuables when everybody in the room has said it.
    internal static class Sponge
    {
        internal const string PrefabName = "Valuable Dish Sponge";
        private const string PropertyKey = "vibez.UsableValuables.sponge";
        private const float Mass = 0.5f;

        // What every plain valuable carries; a donor with more than this is a worse pick.
        private static readonly HashSet<string> plainScripts = new HashSet<string>
        {
            "PhotonView", "PhotonTransformView", "PhysGrabObject", "PhysGrabObjectImpactDetector", "ValuableObject",
            "RoomVolumeCheck", "PhysGrabObjectCollider",
        };

        // Impact sounds that suit a sponge better than glass or china, if the game has them.
        private static readonly string[] softSounds =
        {
            "plush", "soft", "cloth", "fabric", "rubber", "foam", "toy", "paper", "cardboard", "sponge", "pillow",
        };

        private static ConfigEntry<bool> enabled;
        private static ConfigEntry<int> minValue;
        private static ConfigEntry<int> maxValue;
        private static ConfigEntry<float> minSpeed;
        private static ConfigEntry<float> bubbleAmount;
        private static ConfigEntry<float> dripVolume;
        private static ConfigEntry<float> dripFalloff;
        private static ConfigEntry<float> firstGrabVolume;
        private static ConfigEntry<float> firstGrabFalloff;

        private static PrefabRef spongeRef;
        private static bool published;

        internal static bool Ready { get { return spongeRef != null; } }
        internal static float MinSpeed { get { return minSpeed.Value; } }
        internal static float BubbleAmount { get { return bubbleAmount.Value; } }
        internal static float DripVolume { get { return dripVolume.Value; } }
        internal static float DripFalloff { get { return dripFalloff.Value; } }
        internal static float FirstGrabVolume { get { return firstGrabVolume.Value; } }
        internal static float FirstGrabFalloff { get { return firstGrabFalloff.Value; } }

        internal static void Bind(ConfigFile config)
        {
            enabled = config.Bind("Sponge", "Enabled", true,
                "Adds the dish sponge, a small valuable that turns up in levels like any other and that blows bubbles and drips when it is moved. "
                + "It only turns up when every player in the room has this mod. Needs a restart of the game to take effect.");
            minValue = config.Bind("Sponge", "MinValue", 100,
                new ConfigDescription("The least a dish sponge is worth, in dollars (the game rounds prices to hundreds). Needs a restart. "
                    + "Only the host's setting decides the price.", new AcceptableValueRange<int>(0, 5000)));
            maxValue = config.Bind("Sponge", "MaxValue", 300,
                new ConfigDescription("The most a dish sponge is worth, in dollars. Needs a restart. Only the host's setting decides the price.",
                    new AcceptableValueRange<int>(0, 5000)));
            minSpeed = config.Bind("Sponge", "MinSpeed", 0.5f,
                new ConfigDescription("How fast the sponge has to be moved (metres per second, turning it counts too) for it to blow bubbles and drip.",
                    new AcceptableValueRange<float>(0.05f, 5f)));
            bubbleAmount = config.Bind("Sponge", "BubbleAmount", 1f,
                new ConfigDescription("How many bubbles it blows (1 = the default amount, 0 = none). On your machine only.",
                    new AcceptableValueRange<float>(0f, 3f)));
            dripVolume = config.Bind("Sponge", "DripVolume", 0.7f,
                new ConfigDescription("How loud the drips are on your machine (1 = as loud as a sound can be played).",
                    new AcceptableValueRange<float>(0f, 1f)));
            dripFalloff = config.Bind("Sponge", "DripFalloff", 1.2f,
                new ConfigDescription("How far the drips carry on your machine (1 = like the game's own sounds).",
                    new AcceptableValueRange<float>(1f, 5f)));
            firstGrabVolume = config.Bind("Sponge", "FirstGrabVolume", 1f,
                new ConfigDescription("How loud the bubbling sound is that plays the first time somebody picks a sponge up, on your machine "
                    + "(1 = as loud as a sound can be played).", new AcceptableValueRange<float>(0f, 1f)));
            firstGrabFalloff = config.Bind("Sponge", "FirstGrabFalloff", 1.5f,
                new ConfigDescription("How far that sound carries on your machine (1 = like the game's own sounds).",
                    new AcceptableValueRange<float>(1f, 5f)));
        }

        // ---- the prefab

        // Called once, when the game's run manager wakes (before REPOLib adds the modded valuables to the levels).
        internal static void Register()
        {
            if (spongeRef != null || !Plugin.Enabled.Value || !enabled.Value)
            {
                return;
            }

            List<GameObject> scanned;
            GameObject donor = FindDonor(out scanned);
            if (donor == null)
            {
                Plugin.Log.LogWarning("The dish sponge was not added: no small valuable of the game's could be used as its base.");
                return;
            }

            GameObject prefab = null;
            try
            {
                prefab = Build(donor, scanned);
                PrefabRef reference = REPOLib.Modules.Valuables.RegisterValuable(prefab, new List<string>());
                if (reference == null)
                {
                    throw new InvalidOperationException("REPOLib did not accept the valuable");
                }
                spongeRef = reference;
                Plugin.Log.LogInfo("The dish sponge is registered (built on \"" + donor.name + "\").");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("The dish sponge was not added: " + ex);
                if (prefab != null)
                {
                    Object.Destroy(prefab);
                }
            }
        }

        // The game's small valuable with the least extras on it. Only vanilla ones (not the ones other mods registered), and in a
        // fixed order, so every machine picks the same one.
        private static GameObject FindDonor(out List<GameObject> scanned)
        {
            scanned = new List<GameObject>();
            RunManager run = RunManager.instance;
            if (run == null)
            {
                return null;
            }

            GameObject best = null;
            int bestScore = int.MaxValue;
            HashSet<string> seen = new HashSet<string>();
            StringBuilder report = new StringBuilder();
            System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();

            List<Level>[] groups = { run.levels, run.levelArena };
            foreach (List<Level> group in groups)
            {
                if (group == null)
                {
                    continue;
                }
                foreach (Level level in group)
                {
                    if (level == null || level.ValuablePresets == null)
                    {
                        continue;
                    }
                    foreach (LevelValuables preset in level.ValuablePresets)
                    {
                        if (preset == null)
                        {
                            continue;
                        }
                        Consider(preset.tiny, seen, scanned, ref best, ref bestScore, report);
                        Consider(preset.small, seen, scanned, ref best, ref bestScore, report);
                    }
                }
            }

            Plugin.Log.LogInfo("Small valuables considered as the dish sponge's base (score, lower is better; " + clock.ElapsedMilliseconds
                + " ms to load them): " + report);
            return best;
        }

        private static void Consider(List<PrefabRef> list, HashSet<string> seen, List<GameObject> scanned, ref GameObject best,
            ref int bestScore, StringBuilder report)
        {
            if (list == null)
            {
                return;
            }
            foreach (PrefabRef reference in list)
            {
                if (reference == null || !reference.IsValid() || !seen.Add(reference.ResourcePath)
                    || REPOLib.Modules.NetworkPrefabs.HasNetworkPrefab(reference.ResourcePath))
                {
                    continue;
                }
                GameObject prefab = reference.Prefab;
                if (prefab == null)
                {
                    continue;
                }
                scanned.Add(prefab);

                int score = Score(prefab);
                report.Append(prefab.name).Append('=').Append(score == int.MaxValue ? "x" : score.ToString()).Append("; ");
                bool better = score < bestScore
                    || (score == bestScore && best != null && string.CompareOrdinal(prefab.name, best.name) < 0);
                if (score != int.MaxValue && better)
                {
                    best = prefab;
                    bestScore = score;
                }
            }
        }

        private static int Score(GameObject prefab)
        {
            ValuableObject valuable = prefab.GetComponent<ValuableObject>();
            if (valuable == null || prefab.GetComponent<PhysGrabObject>() == null || prefab.GetComponent<Rigidbody>() == null
                || prefab.GetComponent<PhotonView>() == null || valuable.physAttributePreset == null
                || valuable.durabilityPreset == null || valuable.audioPreset == null)
            {
                return int.MaxValue;
            }

            int score = 0;
            foreach (MonoBehaviour script in prefab.GetComponentsInChildren<MonoBehaviour>(true))
            {
                score += script == null || !plainScripts.Contains(script.GetType().Name) ? 10 : 0;
            }
            score += 8 * prefab.GetComponentsInChildren<Animator>(true).Length;
            score += 3 * prefab.GetComponentsInChildren<AudioSource>(true).Length;
            score += 3 * prefab.GetComponentsInChildren<Light>(true).Length;
            score += 3 * prefab.GetComponentsInChildren<ParticleSystem>(true).Length;

            int solid = 0;
            foreach (Collider collider in prefab.GetComponentsInChildren<Collider>(true))
            {
                solid += collider.isTrigger ? 0 : 1;
            }
            if (solid == 0)
            {
                return int.MaxValue;
            }
            score += 2 * (solid - 1);
            score += valuable.volumeType == ValuableVolume.Type.Tiny ? 0 : 2;
            return score;
        }

        private static GameObject Build(GameObject donor, List<GameObject> scanned)
        {
            // An inactive parent: nothing on the copy wakes up until the game spawns it.
            GameObject holder = new GameObject("UsableValuables prefabs");
            holder.SetActive(false);
            Object.DontDestroyOnLoad(holder);

            GameObject prefab = Object.Instantiate(donor, holder.transform);
            prefab.name = PrefabName;

            Plugin.Log.LogInfo("The dish sponge's base, \"" + donor.name + "\":\n" + Describe(prefab));

            // What to keep of the donor's looks: the material (it knows the game's lighting) and the layer it renders on.
            Renderer main = LargestRenderer(prefab);
            Material donorMaterial = main != null ? main.sharedMaterial : null;
            int visualLayer = main != null ? main.gameObject.layer : prefab.layer;
            List<string> donorMaterials = new List<string>();
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null)
                    {
                        donorMaterials.Add(material.name + " (shader \"" + material.shader.name + "\")");
                    }
                }
            }
            Plugin.Log.LogInfo("Its materials: " + string.Join(", ", donorMaterials.ToArray()));

            // The donor's looks go.
            foreach (LODGroup lod in prefab.GetComponentsInChildren<LODGroup>(true))
            {
                Object.DestroyImmediate(lod);
            }
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                {
                    Object.DestroyImmediate(renderer);
                }
            }
            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                Object.DestroyImmediate(filter);
            }

            // Its shape goes too: one box, the size of the sponge.
            Vector3 size = new Vector3(SpongeAssets.Length, SpongeAssets.Height, SpongeAssets.Depth);
            Vector3 middle = new Vector3(0f, size.y * 0.5f, 0f);
            ReplaceColliders(prefab, size);
            prefab.transform.localScale = Vector3.one;
            foreach (string point in new[] { "Force Grab Point", "ForceCenterPoint" })
            {
                Transform found = prefab.transform.Find(point);
                if (found != null)
                {
                    found.localPosition = middle;
                }
            }

            // The sponge's looks.
            GameObject model = new GameObject("Sponge Model") { layer = visualLayer };
            model.transform.SetParent(prefab.transform, false);
            model.AddComponent<MeshFilter>().sharedMesh = SpongeAssets.BuildMesh();
            MeshRenderer meshRenderer = model.AddComponent<MeshRenderer>();
            string shaderName;
            meshRenderer.sharedMaterial = SpongeAssets.BuildMaterial(donorMaterial, SpongeAssets.BuildTexture(), out shaderName);
            Plugin.Log.LogInfo("The dish sponge's material uses the shader \"" + shaderName + "\".");

            // What it is worth, how heavy, how tough and what it sounds like when it is knocked about.
            ValuableObject valuable = prefab.GetComponent<ValuableObject>();
            valuable.volumeType = ValuableVolume.Type.Tiny;

            Value value = SpongeAssets.Keep(ScriptableObject.CreateInstance<Value>());
            value.name = "Value - Dish Sponge";
            value.valueMin = Mathf.Min(minValue.Value, maxValue.Value);
            value.valueMax = Mathf.Max(minValue.Value, maxValue.Value);
            valuable.valuePreset = value;

            PhysAttribute attribute = SpongeAssets.Keep(Object.Instantiate(valuable.physAttributePreset));
            attribute.name = "PhysAttribute - Dish Sponge";
            attribute.mass = Mass;
            valuable.physAttributePreset = attribute;

            Durability durability = SpongeAssets.Keep(Object.Instantiate(valuable.durabilityPreset));
            durability.name = "Durability - Dish Sponge";
            durability.fragility = 20f;
            durability.durability = 100f;
            valuable.durabilityPreset = durability;

            PhysAudio soft = FindSoftSounds(scanned);
            if (soft != null)
            {
                valuable.audioPreset = soft;
            }

            Gradient colours = new Gradient();
            colours.SetKeys(
                new[] { new GradientColorKey(new Color(0.98f, 0.84f, 0.22f), 0f), new GradientColorKey(new Color(0.24f, 0.70f, 0.30f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            valuable.particleColors = colours;

            prefab.AddComponent<SpongeFx>();
            return prefab;
        }

        private static Renderer LargestRenderer(GameObject prefab)
        {
            Renderer best = null;
            float bestSize = -1f;
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
                {
                    continue;
                }
                float size = renderer.bounds.size.sqrMagnitude;
                if (best == null || size > bestSize)
                {
                    best = renderer;
                    bestSize = size;
                }
            }
            return best;
        }

        // The donor's solid colliders become one box. The one that stays keeps its object (its tag and layer are what make the game
        // treat it as part of the valuable) and its physics material.
        private static void ReplaceColliders(GameObject prefab, Vector3 size)
        {
            Collider[] all = prefab.GetComponentsInChildren<Collider>(true);
            Collider primary = null;
            foreach (Collider collider in all)
            {
                if (!collider.isTrigger && collider.CompareTag("Phys Grab Object"))
                {
                    primary = collider;
                    break;
                }
            }
            if (primary == null)
            {
                foreach (Collider collider in all)
                {
                    if (!collider.isTrigger)
                    {
                        primary = collider;
                        break;
                    }
                }
            }
            if (primary == null)
            {
                throw new InvalidOperationException("the base valuable has no solid collider");
            }

            PhysicMaterial material = primary.sharedMaterial;
            GameObject host = primary.gameObject;

            foreach (Collider collider in all)
            {
                if (collider == primary || collider.isTrigger)
                {
                    continue;
                }
                PhysGrabObjectCollider marker = collider.GetComponent<PhysGrabObjectCollider>();
                if (marker != null)
                {
                    Object.DestroyImmediate(marker);
                }
                Object.DestroyImmediate(collider);
            }

            BoxCollider box = primary as BoxCollider;
            if (box == null)
            {
                Object.DestroyImmediate(primary);
                box = host.AddComponent<BoxCollider>();
            }
            box.isTrigger = false;
            box.size = size;
            box.center = new Vector3(0f, size.y * 0.5f, 0f);
            if (material != null)
            {
                box.sharedMaterial = material;
            }

            if (host != prefab)
            {
                host.transform.SetParent(prefab.transform, false);
                host.transform.localPosition = Vector3.zero;
                host.transform.localRotation = Quaternion.identity;
                host.transform.localScale = Vector3.one;
            }
            if (host.GetComponent<PhysGrabObjectCollider>() == null)
            {
                host.AddComponent<PhysGrabObjectCollider>();
            }
        }

        // The game's impact sounds of the softest material among the valuables looked at, if any is named like one.
        private static PhysAudio FindSoftSounds(List<GameObject> scanned)
        {
            HashSet<string> names = new HashSet<string>();
            PhysAudio found = null;
            foreach (GameObject prefab in scanned)
            {
                ValuableObject valuable = prefab.GetComponent<ValuableObject>();
                PhysAudio audio = valuable != null ? valuable.audioPreset : null;
                if (audio == null || !names.Add(audio.name))
                {
                    continue;
                }
                if (found == null)
                {
                    foreach (string word in softSounds)
                    {
                        if (audio.name.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            found = audio;
                            break;
                        }
                    }
                }
            }
            Plugin.Log.LogInfo("Impact sound presets of the valuables looked at: " + string.Join(", ", new List<string>(names).ToArray())
                + (found != null ? " (the sponge uses \"" + found.name + "\")" : " (none is soft: the sponge keeps its base's)"));
            return found;
        }

        private static string Describe(GameObject prefab)
        {
            StringBuilder text = new StringBuilder();
            Walk(prefab.transform, 0, text);
            return text.ToString();
        }

        private static void Walk(Transform node, int depth, StringBuilder text)
        {
            text.Append(' ', depth * 2).Append(node.name)
                .Append(" [layer ").Append(LayerMask.LayerToName(node.gameObject.layer)).Append(", tag ").Append(node.tag).Append("]");
            foreach (Component component in node.GetComponents<Component>())
            {
                if (component != null && !(component is Transform))
                {
                    text.Append(' ').Append(component.GetType().Name);
                }
            }
            text.AppendLine();
            foreach (Transform child in node)
            {
                Walk(child, depth + 1, text);
            }
        }

        // ---- who has it

        // Says so, once per room, so the host can tell (called every frame; cheap).
        internal static void Tick()
        {
            if (spongeRef == null)
            {
                return;
            }
            if (!PhotonNetwork.InRoom)
            {
                published = false;
                return;
            }
            if (published)
            {
                return;
            }
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { PropertyKey, Plugin.Version } });
            published = true;
        }

        // Before the host lists a level's valuables: the sponge is in (with everybody's consent) or out.
        internal static void ApplySpawnRules()
        {
            if (spongeRef == null || LevelGenerator.Instance == null || LevelGenerator.Instance.Level == null)
            {
                return;
            }
            List<LevelValuables> presets = LevelGenerator.Instance.Level.ValuablePresets;
            if (presets == null || presets.Count == 0)
            {
                return;
            }

            List<string> missing = PlayersWithoutIt();
            bool allow = missing.Count == 0;
            string path = spongeRef.ResourcePath;

            bool present = false;
            foreach (LevelValuables preset in presets)
            {
                if (preset != null && preset.tiny != null && preset.tiny.Exists(p => p != null && p.ResourcePath == path))
                {
                    present = true;
                }
            }

            if (allow && !present)
            {
                LevelValuables target = REPOLib.Modules.ValuablePresets.GenericValuablePreset;
                if (target == null || !presets.Contains(target))
                {
                    target = presets[0];
                }
                target.tiny.Add(spongeRef);
                Plugin.Log.LogInfo("The dish sponge can turn up in this level.");
            }
            else if (!allow)
            {
                foreach (LevelValuables preset in presets)
                {
                    if (preset != null && preset.tiny != null)
                    {
                        preset.tiny.RemoveAll(p => p != null && p.ResourcePath == path);
                    }
                }
                Plugin.Log.LogInfo("The dish sponge stays out of this level: " + string.Join(", ", missing.ToArray())
                    + " does not have UsableValuables 0.2 or later (a player who cannot create a valuable waits at the loading screen for ever).");
            }
        }

        private static List<string> PlayersWithoutIt()
        {
            List<string> missing = new List<string>();
            if (!SemiFunc.IsMultiplayer())
            {
                return missing;
            }
            foreach (Player player in PhotonNetwork.PlayerList)
            {
                if (player.CustomProperties == null || !player.CustomProperties.ContainsKey(PropertyKey))
                {
                    missing.Add(string.IsNullOrEmpty(player.NickName) ? "a player" : player.NickName);
                }
            }
            return missing;
        }
    }

    // The host lists a level's valuables when the level starts to generate.
    [HarmonyPatch(typeof(ValuableDirector), nameof(ValuableDirector.SetupHost))]
    internal static class SpongeSpawnPatch
    {
        private static void Prefix()
        {
            try
            {
                Sponge.ApplySpawnRules();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Deciding whether the dish sponge turns up failed: " + ex);
            }
        }
    }
}
