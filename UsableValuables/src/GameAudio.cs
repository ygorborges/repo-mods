using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace UsableValuables
{
    // Playing sounds the way the game plays its own: through its Sound class and its audio manager, so they follow the
    // player's volume settings and fade with distance like any other sound. Two kinds:
    //  - the mod's own clips (UserAudio), played with PlayClip;
    //  - the game's bomb-fuse sound (the Bang enemy's), found by name in that enemy's prefab, for the banana bow's wind-up.
    internal static class GameAudio
    {
        private const string BangPrefab = "Enemies/Enemy - Bang";
        private const string FuseIgniteClips = "enemy bang fuse ignite";     // ignite01..03
        private const string FuseLoopClip = "enemy bang fuse loop";

        private static Sound fuseIgnite;
        private static Sound fuseLoop;
        private static float retryAt;

        private static readonly Dictionary<AudioClip, Sound> wrappers = new Dictionary<AudioClip, Sound>();

        // One of the mod's own clips, at the given volume (0-1, 1 = as loud as a sound can be played) and falloff (1 = like
        // the game's own sounds, 2 = carries twice as far). The AudioSource comes back so the caller can fade it or stop it.
        internal static AudioSource PlayClip(AudioClip clip, Transform follow, Vector3 position, float volume, float falloff)
        {
            if (clip == null)
            {
                return null;
            }
            Sound sound;
            if (!wrappers.TryGetValue(clip, out sound) || sound == null)
            {
                sound = new Sound
                {
                    Sounds = new[] { clip },
                    Volume = 1f,
                    VolumeRandom = 0f,
                    Pitch = 1f,
                    PitchRandom = 0f,
                    SpatialBlend = 1f,
                };
                wrappers[clip] = sound;
            }
            return Play(sound, follow, position, volume, falloff);
        }

        // Changes the volume of a sound that is already playing (for a fade), the way the game's audio components allow: through
        // the copy of the volume that its AudioLowPassLogic keeps, as well as on the source itself.
        internal static void SetVolume(AudioSource source, float volume)
        {
            if (source == null)
            {
                return;
            }
            AudioLowPassLogic lowPass = source.GetComponent<AudioLowPassLogic>();
            if (lowPass != null)
            {
                Refs.LowPassVolume(lowPass) = volume;
            }
            source.volume = volume;
        }

        private static AudioSource Play(Sound sound, Transform follow, Vector3 position, float volume, float falloff)
        {
            if (sound == null)
            {
                return null;
            }
            return follow != null ? sound.Play(follow, volume, falloff) : sound.Play(position, volume, falloff);
        }

        // ---- the bomb fuse

        // A burning fuse: the ignite (one of the game's three) and then the fuse's own loop, until Stop. The game's loop is a
        // clip that repeats, so it is started again each time it runs out (Tick, every frame).
        internal sealed class Fuse
        {
            private readonly Transform follow;
            private readonly Vector3 position;
            private AudioSource loop;
            private bool stopped;

            internal Fuse(Transform follow, Vector3 position)
            {
                this.follow = follow;
                this.position = position;
                Play(fuseIgnite, follow, position, 1f, 1f);
            }

            internal void Tick()
            {
                if (stopped || fuseLoop == null)
                {
                    return;
                }
                if (loop == null || !loop.isPlaying)
                {
                    loop = Play(fuseLoop, follow, position, 1f, 1f);
                }
            }

            internal void Stop()
            {
                stopped = true;
                if (loop != null)
                {
                    loop.Stop();
                }
            }
        }

        // Null if the game's fuse sound cannot be found; the banana bow then winds up in silence.
        internal static Fuse StartFuse(Transform follow, Vector3 position)
        {
            EnsureFuse();
            if (fuseIgnite == null && fuseLoop == null)
            {
                return null;
            }
            return new Fuse(follow, position);
        }

        private static void EnsureFuse()
        {
            if ((fuseIgnite != null && fuseLoop != null) || Time.time < retryAt)
            {
                return;
            }
            // Finding it can load a prefab, so after a miss the next try is a while away.
            retryAt = Time.time + 30f;

            try
            {
                GameObject prefab = Resources.Load<GameObject>(BangPrefab);
                if (prefab != null)
                {
                    if (fuseIgnite == null)
                    {
                        fuseIgnite = Copy(FindSound(prefab, name => name.StartsWith(FuseIgniteClips, StringComparison.OrdinalIgnoreCase)));
                    }
                    if (fuseLoop == null)
                    {
                        fuseLoop = Copy(FindSound(prefab, name => string.Equals(name, FuseLoopClip, StringComparison.OrdinalIgnoreCase)));
                    }
                }
                if (fuseIgnite == null || fuseLoop == null)
                {
                    Plugin.Log.LogWarning("The game's bomb-fuse sound was not fully found (ignite: " + (fuseIgnite != null)
                        + ", loop: " + (fuseLoop != null) + "); the banana bow's wind-up will be quieter or silent.");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Looking for the game's fuse sound failed: " + ex);
            }
        }

        // The first Sound, on any script of the prefab and whatever the field is called, that has a clip with a matching name.
        private static Sound FindSound(GameObject prefab, Func<string, bool> matches)
        {
            foreach (MonoBehaviour script in prefab.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (script == null)
                {
                    continue;
                }
                foreach (FieldInfo field in script.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (field.FieldType != typeof(Sound))
                    {
                        continue;
                    }
                    Sound sound = field.GetValue(script) as Sound;
                    if (sound == null || sound.Sounds == null)
                    {
                        continue;
                    }
                    foreach (AudioClip clip in sound.Sounds)
                    {
                        if (clip != null && matches(clip.name))
                        {
                            return sound;
                        }
                    }
                }
            }
            return null;
        }

        // A sound of its own with the game's settings for it (its loudness, falloff and so on) but without the AudioSource of the
        // prefab it came from: Sound.Play plays through that source when there is one, which a prefab's is not fit for.
        private static Sound Copy(Sound source)
        {
            if (source == null)
            {
                return null;
            }
            return new Sound
            {
                Sounds = source.Sounds,
                Type = source.Type,
                Volume = source.Volume,
                VolumeRandom = source.VolumeRandom,
                Pitch = source.Pitch,
                PitchRandom = source.PitchRandom,
                SpatialBlend = source.SpatialBlend,
                Doppler = source.Doppler,
                ReverbMix = source.ReverbMix,
                FalloffMultiplier = source.FalloffMultiplier,
                OffscreenVolume = source.OffscreenVolume,
                OffscreenFalloff = source.OffscreenFalloff,
            };
        }
    }
}
