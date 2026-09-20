using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Photon.Pun;
using UnityEngine;

namespace DevTools
{
    // Puts one of the valuables the UsableValuables mod works on in front of the player, to test it without hunting for them.
    // It does what the game's own "/spawn valuable" command does: finds the valuable's prefab among the level presets
    // (every level's, so the ones that only exist on one map are included) and instantiates it - as a networked room object
    // in multiplayer.
    internal static class Spawner
    {
        // The scripts UsableValuables reacts to. Kept here on purpose: this mod does not depend on the other one.
        private static readonly Type[] usableScripts =
        {
            typeof(ValuableFlashlight), typeof(ValuableBoombox), typeof(ValuableForeverCandle),
            typeof(IceSawValuable), typeof(BlenderValuable), typeof(JackhammerValuable), typeof(ScreamDollValuable),
            typeof(FlamethrowerValuable), typeof(FireExtinguisherValuable),
            typeof(ValuableStarWand), typeof(ValuableWizardStaff), typeof(ValuableCamera), typeof(ValuableLevitationPotion),
        };

        private static List<PrefabRef> candidates;

        private static bool IsUsable(GameObject prefab)
        {
            foreach (Type script in usableScripts)
            {
                if (prefab.GetComponentInChildren(script, true) != null)
                {
                    return true;
                }
            }
            return false;
        }

        private static List<PrefabRef> FindCandidates(ManualLogSource log)
        {
            List<PrefabRef> found = new List<PrefabRef>();
            HashSet<string> seen = new HashSet<string>();

            foreach (Level level in RunManager.instance.levels)
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
                    List<PrefabRef>[] lists = { preset.tiny, preset.small, preset.medium, preset.big, preset.wide, preset.tall, preset.veryTall };
                    foreach (List<PrefabRef> list in lists)
                    {
                        if (list == null)
                        {
                            continue;
                        }
                        foreach (PrefabRef prefab in list)
                        {
                            if (prefab == null || !prefab.IsValid() || !seen.Add(prefab.ResourcePath))
                            {
                                continue;
                            }
                            GameObject go = prefab.Prefab;
                            if (go != null && IsUsable(go))
                            {
                                found.Add(prefab);
                            }
                        }
                    }
                }
            }

            List<string> names = new List<string>();
            foreach (PrefabRef prefab in found)
            {
                names.Add(prefab.PrefabName);
            }
            log.LogInfo("Valuables UsableValuables works on (" + found.Count + "): " + string.Join(", ", names.ToArray()));
            return found;
        }

        // Returns the name of what was spawned, or null if there is nothing to spawn.
        internal static string SpawnRandom(ManualLogSource log)
        {
            if (candidates == null || candidates.Count == 0)
            {
                candidates = FindCandidates(log);
            }
            if (candidates.Count == 0)
            {
                return null;
            }

            PlayerController player = PlayerController.instance;
            if (player == null || player.playerAvatarScript == null || player.playerAvatarScript.localCamera == null)
            {
                return null;
            }

            PrefabRef chosen = candidates[UnityEngine.Random.Range(0, candidates.Count)];

            // About two steps in front of you, at chest height (it drops from there); closer if a wall is in the way.
            Transform view = player.playerAvatarScript.localCamera.GetOverrideTransform();
            Vector3 forward = view.forward;
            float distance = 1.8f;
            RaycastHit hit;
            if (Physics.Raycast(view.position, forward, out hit, distance, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore))
            {
                distance = Mathf.Max(0.5f, hit.distance - 0.5f);
            }
            Vector3 position = view.position + forward * distance + Vector3.down * 0.25f;
            Vector3 flat = new Vector3(forward.x, 0f, forward.z);
            Quaternion rotation = flat.sqrMagnitude > 0.001f ? Quaternion.LookRotation(-flat) : Quaternion.identity;

            GameObject spawned = GameManager.Multiplayer()
                ? PhotonNetwork.InstantiateRoomObject(chosen.ResourcePath, position, rotation, 0)
                : UnityEngine.Object.Instantiate(chosen.Prefab, position, rotation);

            // Valuables normally get their price while the level is generated.
            ValuableObject valuable = spawned != null ? spawned.GetComponent<ValuableObject>() : null;
            if (valuable != null)
            {
                valuable.DollarValueSetLogic();
            }
            return chosen.PrefabName;
        }
    }
}
