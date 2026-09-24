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
    // A bar of soap: another valuable this mod adds, registered with REPOLib the same way the dish sponge is (built on a copy of one
    // of the game's own small valuables, and only let into a level once everybody in the room has this mod - see Sponge.cs, which
    // this mirrors closely).
    //
    // It looks and blows bubbles like the sponge (SpongeFx, added to it too), but it behaves like nothing else in the game: slippery
    // and bouncy, so it skids away and bounces instead of sitting still, and every so often it gives itself a little hop while it
    // rests, the way the game's own rubber duck toy fidgets when nobody is touching it. Whatever it touches, player or enemy, finds
    // it hard to keep its feet, and an enemy that walks into it is knocked down for a moment (SoapFx).
    internal static class Soap
    {
        internal const string PrefabName = "Valuable Soap";
        private const string PropertyKey = "vibez.UsableValuables.soap";
        private const float Mass = 0.15f;

        private static readonly HashSet<string> plainScripts = new HashSet<string>
        {
            "PhotonView", "PhotonTransformView", "PhysGrabObject", "PhysGrabObjectImpactDetector", "ValuableObject",
            "RoomVolumeCheck", "PhysGrabObjectCollider",
        };

        private static ConfigEntry<bool> enabled;
        private static ConfigEntry<int> minValue;
        private static ConfigEntry<int> maxValue;
        private static ConfigEntry<float> hopMinInterval;
        private static ConfigEntry<float> hopMaxInterval;
        private static ConfigEntry<float> hopStrength;
        private static ConfigEntry<float> stunSeconds;

        private static PrefabRef soapRef;
        private static bool published;

        internal static bool Ready { get { return soapRef != null; } }
        internal static float HopMinInterval { get { return hopMinInterval.Value; } }
        internal static float HopMaxInterval { get { return hopMaxInterval.Value; } }
        internal static float HopStrength { get { return hopStrength.Value; } }
        internal static float StunSeconds { get { return stunSeconds.Value; } }

        internal static void Bind(ConfigFile config)
        {
            enabled = config.Bind("Soap", "Enabled", true,
                "Adds a bar of soap, a small valuable that turns up in levels like any other. It is slippery and bouncy, gives itself "
                + "a little hop now and then while it rests, and knocks an enemy down for a moment if it touches one. It only turns up "
                + "when every player in the room has this mod. Needs a restart of the game to take effect.");
            minValue = config.Bind("Soap", "MinValue", 20,
                new ConfigDescription("The least a bar of soap is worth, in dollars (the game rounds prices to hundreds, so under 50 "
                    + "it is worth nothing). Needs a restart. Only the host's setting decides the price.", new AcceptableValueRange<int>(0, 2000)));
            maxValue = config.Bind("Soap", "MaxValue", 100,
                new ConfigDescription("The most a bar of soap is worth, in dollars. Needs a restart. Only the host's setting decides the price.",
                    new AcceptableValueRange<int>(0, 2000)));
            hopMinInterval = config.Bind("Soap", "HopMinInterval", 1f,
                new ConfigDescription("While it rests on the ground (not held, not in the cart), the soonest it can give itself another "
                    + "little hop, in seconds.", new AcceptableValueRange<float>(0.2f, 30f)));
            hopMaxInterval = config.Bind("Soap", "HopMaxInterval", 3f,
                new ConfigDescription("The longest it waits between hops, in seconds.", new AcceptableValueRange<float>(0.2f, 60f)));
            hopStrength = config.Bind("Soap", "HopStrength", 1f,
                new ConfigDescription("How big its little hops are (1 = the default, 0 = it stays put and only moves when knocked). "
                    + "Only the host's setting is used: the host gives it the push, and everybody sees the result.",
                    new AcceptableValueRange<float>(0f, 3f)));
            stunSeconds = config.Bind("Soap", "StunSeconds", 1.5f,
                new ConfigDescription("How long an enemy that touches the soap is knocked down for. 0 = it does not knock enemies down. "
                    + "Only the host's setting is used.", new AcceptableValueRange<float>(0f, 10f)));
        }

        // ---- the prefab

        internal static void Register()
        {
            if (soapRef != null || !Plugin.Enabled.Value || !enabled.Value)
            {
                return;
            }

            GameObject donor = FindDonor();
            if (donor == null)
            {
                Plugin.Log.LogWarning("The soap was not added: no small valuable of the game's could be used as its base.");
                return;
            }

            GameObject prefab = null;
            try
            {
                prefab = Build(donor);
                PrefabRef reference = REPOLib.Modules.Valuables.RegisterValuable(prefab, new List<string>());
                if (reference == null)
                {
                    throw new InvalidOperationException("REPOLib did not accept the valuable");
                }
                soapRef = reference;
                Plugin.Log.LogInfo("The soap is registered (built on \"" + donor.name + "\").");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("The soap was not added: " + ex);
                if (prefab != null)
                {
                    Object.Destroy(prefab);
                }
            }
        }

        // Scored the same way Sponge.FindDonor scores candidates; nothing stops the soap and the sponge picking the same donor
        // (each ends up its own separate registered prefab, "Valuable Soap" and "Valuable Dish Sponge").
        private static GameObject FindDonor()
        {
            RunManager run = RunManager.instance;
            if (run == null)
            {
                return null;
            }

            GameObject best = null;
            int bestScore = int.MaxValue;
            HashSet<string> seen = new HashSet<string>();
            StringBuilder report = new StringBuilder();

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
                        Consider(preset.tiny, seen, ref best, ref bestScore, report);
                        Consider(preset.small, seen, ref best, ref bestScore, report);
                    }
                }
            }

            Plugin.Log.LogInfo("Small valuables considered as the soap's base (score, lower is better): " + report);
            return best;
        }

        private static void Consider(List<PrefabRef> list, HashSet<string> seen, ref GameObject best, ref int bestScore, StringBuilder report)
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

        private static GameObject Build(GameObject donor)
        {
            GameObject holder = new GameObject("UsableValuables prefabs (soap)");
            holder.SetActive(false);
            Object.DontDestroyOnLoad(holder);

            GameObject prefab = Object.Instantiate(donor, holder.transform);
            prefab.name = PrefabName;

            Renderer main = LargestRenderer(prefab);
            int visualLayer = main != null ? main.gameObject.layer : prefab.layer;

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

            Vector3 size = new Vector3(SoapAssets.Length, SoapAssets.Height, SoapAssets.Depth);
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

            GameObject model = new GameObject("Soap Model") { layer = visualLayer };
            model.transform.SetParent(prefab.transform, false);
            model.AddComponent<MeshFilter>().sharedMesh = SoapAssets.BuildMesh();
            MeshRenderer meshRenderer = model.AddComponent<MeshRenderer>();
            string shaderName;
            meshRenderer.sharedMaterial = SpongeAssets.BuildMaterial(null, SoapAssets.BuildTexture(), out shaderName);
            Plugin.Log.LogInfo("The soap's material uses the shader \"" + shaderName + "\".");

            ValuableObject valuable = prefab.GetComponent<ValuableObject>();
            valuable.volumeType = ValuableVolume.Type.Tiny;

            Value value = SpongeAssets.Keep(ScriptableObject.CreateInstance<Value>());
            value.name = "Value - Soap";
            value.valueMin = Mathf.Min(minValue.Value, maxValue.Value);
            value.valueMax = Mathf.Max(minValue.Value, maxValue.Value);
            valuable.valuePreset = value;

            PhysAttribute attribute = SpongeAssets.Keep(Object.Instantiate(valuable.physAttributePreset));
            attribute.name = "PhysAttribute - Soap";
            attribute.mass = Mass;
            valuable.physAttributePreset = attribute;

            Durability durability = SpongeAssets.Keep(Object.Instantiate(valuable.durabilityPreset));
            durability.name = "Durability - Soap";
            durability.fragility = 30f;
            durability.durability = 100f;
            valuable.durabilityPreset = durability;

            Gradient colours = new Gradient();
            colours.SetKeys(
                new[] { new GradientColorKey(new Color(0.98f, 0.85f, 0.90f), 0f), new GradientColorKey(new Color(0.90f, 0.62f, 0.72f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            valuable.particleColors = colours;

            prefab.AddComponent<SpongeFx>();
            prefab.AddComponent<SoapFx>();
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

        // One box, sized to the bar, on a collider that keeps the tag and layer that make the game treat it as part of the
        // valuable - and, unlike the sponge, with the soap's own slippery physic material instead of the donor's.
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
            box.sharedMaterial = SoapAssets.BuildPhysicMaterial();

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

        // ---- who has it (same idea as the sponge: a valuable the host can spawn has to exist on every machine, or a player
        // without it waits at the loading screen for ever)

        internal static void Tick()
        {
            if (soapRef == null)
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

        internal static void ApplySpawnRules()
        {
            if (soapRef == null || LevelGenerator.Instance == null || LevelGenerator.Instance.Level == null)
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
            string path = soapRef.ResourcePath;

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
                target.tiny.Add(soapRef);
                Plugin.Log.LogInfo("The soap can turn up in this level.");
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
                Plugin.Log.LogInfo("The soap stays out of this level: " + string.Join(", ", missing.ToArray())
                    + " does not have UsableValuables 0.2 or later.");
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

    [HarmonyPatch(typeof(ValuableDirector), nameof(ValuableDirector.SetupHost))]
    internal static class SoapSpawnPatch
    {
        private static void Prefix()
        {
            try
            {
                Soap.ApplySpawnRules();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Deciding whether the soap turns up failed: " + ex);
            }
        }
    }
}
