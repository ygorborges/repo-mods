using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using Random = UnityEngine.Random;

namespace UsableValuables
{
    // Two more valuables that do nothing in the game and get a behaviour of their own, with no key involved:
    //
    //  - the horse whinnies (a sound file) the first time somebody picks it up, and never again (like the game's one-shot traps);
    //  - the piano, when somebody holds it by anything but its keys, has a chance each second of starting to play a song
    //    (the Lassan of Liszt's Hungarian Rhapsody No. 2), and stops 2 seconds after it was let go of.
    //
    // Both come with the screen glitch the game shows when a trap goes off. Only the host decides; everybody plays the sound.
    internal static class Haunts
    {
        private const float PianoFadeOut = 0.6f;

        private static ConfigEntry<bool> horseEnabled;
        private static ConfigEntry<float> horseVolume;
        private static ConfigEntry<float> horseFalloff;
        private static ConfigEntry<bool> horseGlitch;

        private static ConfigEntry<bool> pianoEnabled;
        private static ConfigEntry<float> pianoChance;
        private static ConfigEntry<float> pianoStopAfterDrop;
        private static ConfigEntry<float> pianoVolume;
        private static ConfigEntry<float> pianoFalloff;
        private static ConfigEntry<bool> pianoGlitch;

        // A horse (host only), keyed by the physics body's instance id.
        private sealed class HorseState
        {
            public PhysGrabObject Body;
            public bool WasGrabbed;
            public bool Played;     // it has whinnied: once is all it does
        }

        // A piano (host only).
        private sealed class PianoState
        {
            public PhysGrabObject Body;
            public PhysGrabObjectGrabArea Keys;    // the grab area of its keys: whoever grabbed it there is playing it, not holding it
            public bool Playing;
            public float ReleasedAt = -1f;         // when it was let go of; -1 while somebody holds it
        }

        // On every machine: the song that is playing, and its fade out.
        private sealed class Song
        {
            public PhysGrabObject Body;
            public AudioSource Source;
            public float Level = 1f;
            public bool Fading;
        }

        private static readonly Dictionary<int, HorseState> horses = new Dictionary<int, HorseState>();
        private static readonly Dictionary<int, PianoState> pianos = new Dictionary<int, PianoState>();
        private static readonly Dictionary<int, Song> songs = new Dictionary<int, Song>();

        private static bool songWarningLogged;

        internal static bool HorseGlitch { get { return horseGlitch.Value; } }
        internal static bool PianoGlitch { get { return pianoGlitch.Value; } }

        internal static void Bind(ConfigFile config)
        {
            horseEnabled = config.Bind("Horse", "Enabled", true,
                "The horse valuable does nothing in the game. With this on, it whinnies the first time somebody picks it up, and never again. "
                + "Only the host's setting is used.");
            horseVolume = config.Bind("Horse", "Volume", 1f,
                new ConfigDescription("How loud the whinny is on your machine (1 = as loud as a sound can be played).",
                    new AcceptableValueRange<float>(0f, 1f)));
            horseFalloff = config.Bind("Horse", "Falloff", 1.5f,
                new ConfigDescription("How far the whinny carries on your machine (1 = like the game's own sounds).",
                    new AcceptableValueRange<float>(1f, 5f)));
            horseGlitch = config.Bind("Horse", "ScreenGlitch", true,
                "Whoever picks the horse up gets the screen glitch the game shows when a trap goes off (the one you get when the TV turns on). "
                + "On your machine only.");

            pianoEnabled = config.Bind("Piano", "Enabled", true,
                "The piano valuable plays notes when you grab its keys, and nothing else. With this on, while somebody holds it by anything but the "
                + "keys there is a chance each second that it starts playing a song by itself. Only the host's setting is used.");
            pianoChance = config.Bind("Piano", "StartChancePerSecond", 10f,
                new ConfigDescription("Chance, in percent, that a piano held by anything but its keys starts playing its song, each second. "
                    + "0 = never. Only the host's setting is used.", new AcceptableValueRange<float>(0f, 100f)));
            pianoStopAfterDrop = config.Bind("Piano", "StopSecondsAfterDrop", 2f,
                new ConfigDescription("The song plays for as long as somebody holds the piano (by the keys or not) and stops this many seconds "
                    + "after it was let go of. Only the host's setting is used.", new AcceptableValueRange<float>(0f, 30f)));
            pianoVolume = config.Bind("Piano", "Volume", 0.8f,
                new ConfigDescription("How loud the song is on your machine (1 = as loud as a sound can be played).",
                    new AcceptableValueRange<float>(0f, 1f)));
            pianoFalloff = config.Bind("Piano", "Falloff", 1.5f,
                new ConfigDescription("How far the song carries on your machine (1 = like the game's own sounds).",
                    new AcceptableValueRange<float>(1f, 5f)));
            pianoGlitch = config.Bind("Piano", "ScreenGlitch", true,
                "Whoever holds the piano when the song starts gets the screen glitch the game shows when a trap goes off. On your machine only.");
        }

        // ---- host

        internal static void RegisterHorse(PhysGrabObject body)
        {
            int id = body.GetInstanceID();
            if (!horses.ContainsKey(id))
            {
                // A horse somebody already holds when it is first seen is not "picked up" now.
                horses[id] = new HorseState { Body = body, WasGrabbed = body.grabbed };
            }
        }

        internal static void RegisterPiano(PhysGrabObject body)
        {
            int id = body.GetInstanceID();
            if (!pianos.ContainsKey(id))
            {
                pianos[id] = new PianoState { Body = body, Keys = body.GetComponentInChildren<PhysGrabObjectGrabArea>(true) };
            }
        }

        // Rolled once a second for a piano that somebody holds.
        internal static void RollPiano(PhysGrabObject body)
        {
            PianoState piano;
            if (!pianoEnabled.Value || !pianos.TryGetValue(body.GetInstanceID(), out piano) || piano.Playing)
            {
                return;
            }
            if (!HeldOutsideKeys(piano) || !Mischief.Chance(pianoChance.Value))
            {
                return;
            }
            if (UserAudio.PianoSong == null)
            {
                if (!songWarningLogged)
                {
                    songWarningLogged = true;
                    Plugin.Log.LogWarning("The piano has no song to play (no file with \"" + UserAudio.SongMarker + "\" in its name in the audio folder).");
                }
                return;
            }

            Plugin.Log.LogDebug("A piano started playing its song.");
            piano.Playing = true;
            piano.ReleasedAt = -1f;
            Net.AnnounceQuirk(body, Quirk.Piano, QuirkPhase.Start);
        }

        // Is somebody holding it by something other than its keys? The grab area lists who grabbed it by the keys.
        private static bool HeldOutsideKeys(PianoState piano)
        {
            foreach (PhysGrabber grabber in piano.Body.playerGrabbing)
            {
                if (grabber == null)
                {
                    continue;
                }
                if (piano.Keys == null || !piano.Keys.listOfAllGrabbers.Contains(grabber))
                {
                    return true;
                }
            }
            return false;
        }

        internal static void Tick(float now)
        {
            if (horses.Count > 0)
            {
                List<int> gone = null;
                foreach (KeyValuePair<int, HorseState> pair in horses)
                {
                    HorseState horse = pair.Value;
                    if (horse.Body == null || horse.Body.dead)
                    {
                        if (gone == null)
                        {
                            gone = new List<int>();
                        }
                        gone.Add(pair.Key);
                        continue;
                    }

                    bool grabbed = horse.Body.grabbed;
                    if (grabbed && !horse.WasGrabbed && !horse.Played && horseEnabled.Value)
                    {
                        horse.Played = true;
                        Net.AnnounceQuirk(horse.Body, Quirk.Horse, QuirkPhase.Start);
                    }
                    horse.WasGrabbed = grabbed;
                }
                Remove(horses, gone);
            }

            if (pianos.Count > 0)
            {
                List<int> gone = null;
                foreach (KeyValuePair<int, PianoState> pair in pianos)
                {
                    PianoState piano = pair.Value;
                    if (piano.Body == null || piano.Body.dead)
                    {
                        if (gone == null)
                        {
                            gone = new List<int>();
                        }
                        gone.Add(pair.Key);
                        continue;
                    }
                    if (!piano.Playing)
                    {
                        continue;
                    }

                    if (piano.Body.grabbed)
                    {
                        piano.ReleasedAt = -1f;
                    }
                    else if (piano.ReleasedAt < 0f)
                    {
                        piano.ReleasedAt = now;
                    }
                    else if (now - piano.ReleasedAt >= pianoStopAfterDrop.Value)
                    {
                        piano.Playing = false;
                        Net.AnnounceQuirk(piano.Body, Quirk.Piano, QuirkPhase.Settle);
                    }
                }
                Remove(pianos, gone);
            }
        }

        private static void Remove<T>(Dictionary<int, T> dictionary, List<int> keys)
        {
            if (keys == null)
            {
                return;
            }
            foreach (int key in keys)
            {
                dictionary.Remove(key);
            }
        }

        // ---- every machine

        internal static void ClientHorse(PhysGrabObject body, Vector3 position)
        {
            GameAudio.PlayClip(UserAudio.Horse, body != null ? body.transform : null, position, horseVolume.Value, horseFalloff.Value);
            if (horseGlitch.Value)
            {
                ScreenGlitch.PlayIfHeldHere(body);
            }
        }

        internal static void ClientPianoStart(PhysGrabObject body, Vector3 position)
        {
            int key = body != null ? body.GetInstanceID() : 0;
            Stop(key);

            AudioSource source = GameAudio.PlayClip(UserAudio.PianoSong, body != null ? body.transform : null, position,
                pianoVolume.Value, pianoFalloff.Value);
            if (source != null)
            {
                songs[key] = new Song { Body = body, Source = source };
            }
            if (pianoGlitch.Value)
            {
                ScreenGlitch.PlayIfHeldHere(body);
            }
        }

        // 2 seconds after it was let go of: the song fades out.
        internal static void ClientPianoStop(PhysGrabObject body)
        {
            Song song;
            if (songs.TryGetValue(body != null ? body.GetInstanceID() : 0, out song))
            {
                song.Fading = true;
            }
        }

        private static void Stop(int key)
        {
            Song song;
            if (songs.TryGetValue(key, out song))
            {
                if (song.Source != null)
                {
                    song.Source.Stop();
                    Object.Destroy(song.Source.gameObject);
                }
                songs.Remove(key);
            }
        }

        internal static void ClientTick()
        {
            if (songs.Count == 0)
            {
                return;
            }

            float dt = Time.deltaTime;
            List<int> done = null;
            foreach (KeyValuePair<int, Song> pair in songs)
            {
                Song song = pair.Value;
                // The song ended by itself, or its piano is gone.
                if (song.Source == null)
                {
                    if (done == null)
                    {
                        done = new List<int>();
                    }
                    done.Add(pair.Key);
                    continue;
                }
                if (song.Body == null)
                {
                    song.Fading = true;
                }

                if (song.Fading)
                {
                    song.Level = Mathf.MoveTowards(song.Level, 0f, dt / PianoFadeOut);
                    GameAudio.SetVolume(song.Source, song.Level * pianoVolume.Value);
                    if (song.Level <= 0.001f)
                    {
                        if (done == null)
                        {
                            done = new List<int>();
                        }
                        done.Add(pair.Key);
                    }
                }
            }

            if (done != null)
            {
                foreach (int key in done)
                {
                    Stop(key);
                }
            }
        }
    }
}
