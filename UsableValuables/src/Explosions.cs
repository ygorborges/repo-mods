using System;
using System.Collections.Generic;
using UnityEngine;

namespace UsableValuables
{
    // An explosion made with the game's own ParticleScriptExplosion (the one the barrel and the clown use), so it looks and
    // sounds like the game's, hurts players and enemies and throws objects around. That component needs an ExplosionPreset
    // (its colours and sounds), which is borrowed from one of the valuables that already explode.
    internal static class Explosions
    {
        // In order of preference: the valuables whose prefab supplies the preset.
        private static readonly string[] Donors = { "arctic barrel", "propane", "clown" };

        private static ExplosionPreset preset;
        private static float retryAt;

        // Whether an explosion can be made (and, the first time, which preset it will use).
        internal static bool Ready()
        {
            if (preset != null)
            {
                return true;
            }
            if (Time.time < retryAt)
            {
                return false;
            }
            // Finding the preset can load a prefab, so after a miss the next try is a while away.
            retryAt = Time.time + 30f;

            try
            {
                preset = FromDonor();
                if (preset == null)
                {
                    foreach (ExplosionPreset loaded in Resources.FindObjectsOfTypeAll<ExplosionPreset>())
                    {
                        if (loaded != null)
                        {
                            preset = loaded;
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Looking for the game's explosion failed: " + ex);
            }
            if (preset == null)
            {
                Plugin.Log.LogWarning("The game's explosion effect was not found; the banana bow will not blow up.");
            }
            return preset != null;
        }

        // The hurt is blamed on whoever last held source, if it still exists.
        internal static void Spawn(PhysGrabObject source, Vector3 position, float size, int damage, int enemyDamage)
        {
            if (!Ready())
            {
                return;
            }

            GameObject holder = new GameObject("UsableValuables explosion");
            holder.transform.position = position;
            ParticleScriptExplosion explosion = holder.AddComponent<ParticleScriptExplosion>();
            explosion.explosionPreset = preset;
            Refs.ExplosionPrefab(explosion) = Resources.Load<GameObject>("Effects/Part Prefab Explosion");
            if (source != null)
            {
                Refs.ExplosionCauser(explosion) = Refs.LastGrabber(source);
            }
            explosion.Spawn(position, size, damage, enemyDamage);
            UnityEngine.Object.Destroy(holder, 3f);
        }

        private static ExplosionPreset FromDonor()
        {
            RunManager run = RunManager.instance;
            if (run == null || run.levels == null)
            {
                return null;
            }

            // Every valuable the levels can spawn; only their names are read until one is picked.
            List<string> paths = new List<string>();
            foreach (Level level in run.levels)
            {
                if (level == null || level.ValuablePresets == null)
                {
                    continue;
                }
                foreach (LevelValuables valuables in level.ValuablePresets)
                {
                    if (valuables == null)
                    {
                        continue;
                    }
                    List<PrefabRef>[] lists = { valuables.tiny, valuables.small, valuables.medium, valuables.big, valuables.wide, valuables.tall, valuables.veryTall };
                    foreach (List<PrefabRef> list in lists)
                    {
                        if (list == null)
                        {
                            continue;
                        }
                        foreach (PrefabRef prefab in list)
                        {
                            if (prefab != null && prefab.IsValid() && prefab.Bundle == null && !paths.Contains(prefab.ResourcePath))
                            {
                                paths.Add(prefab.ResourcePath);
                            }
                        }
                    }
                }
            }

            foreach (string donor in Donors)
            {
                foreach (string path in paths)
                {
                    if (path.IndexOf(donor, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }
                    GameObject go = Resources.Load<GameObject>(path);
                    ParticleScriptExplosion script = go == null ? null : go.GetComponentInChildren<ParticleScriptExplosion>(true);
                    if (script != null && script.explosionPreset != null)
                    {
                        return script.explosionPreset;
                    }
                }
            }
            return null;
        }
    }
}
