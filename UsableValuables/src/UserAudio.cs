using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace UsableValuables
{
    // The mod's own sounds, shipped as sound files in the audio folder next to the DLL (BepInEx/plugins/<package>/audio). MP3 and
    // WAV only: an OGG file (the piano's song, first shipped as the Wikimedia OGG) crashed the game's own decoder outright. They
    // are read once at startup by a coroutine on the plugin; until a clip is ready (or if its file is missing or cannot be decoded)
    // it is null and whatever would have played it stays silent.
    //
    // Decoding is done by the game's native audio code, and a file it dislikes can take the whole game down, which no exception
    // handler can catch. So a marker file is left beside a sound while it is being decoded and removed when that comes back; if
    // the marker is still there at the next start, the game did not come back from it, and that sound is skipped instead of
    // crashing the game on every launch.
    internal static class UserAudio
    {
        internal const string LaughFile = "horror_laugh.mp3";
        internal const string TwitchFile = "hand_twitch.mp3";
        internal const string WarningFile = "uh-oh_detonation.mp3";
        internal const string HorseFile = "horse-sound.mp3";
        internal const string BubblesFile = "bubbles.mp3";

        // Optional: with this file the dish sponge drips with it instead of with the sounds it makes up itself.
        internal const string DripFile = "sponge_drip.mp3";

        // A file with this in its name (mp3 or wav) is the piano's song.
        internal const string SongMarker = "lassan";

        private const string MarkerExtension = ".loading";

        // The handface's laugh.
        internal static AudioClip Laugh;

        // What the handface plays while somebody moves near it, ending with it being flung at them.
        internal static AudioClip Twitch;

        // The "uh-oh" the banana bow gives right before it blows up, to give whoever holds it a chance to react.
        internal static AudioClip Warning;

        // What the horse plays when somebody picks it up.
        internal static AudioClip Horse;

        // What the dish sponge plays the first time somebody picks it up.
        internal static AudioClip Bubbles;

        // The dish sponge's own drip, if the mod came with (or the user added) a file for it; otherwise null.
        internal static AudioClip Drip;

        // What the piano starts playing, now and then, when it is held by anything but its keys. Null when there is no such file.
        internal static AudioClip PianoSong;

        internal static IEnumerator Load(string pluginFolder)
        {
            string folder = Path.Combine(pluginFolder, "audio");
            yield return LoadOne(Path.Combine(folder, LaughFile), clip => Laugh = clip);
            yield return LoadOne(Path.Combine(folder, TwitchFile), clip => Twitch = clip);
            yield return LoadOne(Path.Combine(folder, WarningFile), clip => Warning = clip);
            yield return LoadOne(Path.Combine(folder, HorseFile), clip => Horse = clip);
            yield return LoadOne(Path.Combine(folder, BubblesFile), clip => Bubbles = Louder(clip, 0.9f));

            string drip = Path.Combine(folder, DripFile);
            if (File.Exists(drip))
            {
                yield return LoadOne(drip, clip => Drip = Louder(clip, 0.9f));
            }

            string song = FindSong(folder);
            if (song == null)
            {
                Plugin.Log.LogInfo("No piano song found (an mp3 or wav with \"" + SongMarker + "\" in its name in " + folder + "); the piano stays quiet.");
            }
            else
            {
                yield return LoadOne(song, clip => PianoSong = clip);
            }
        }

        // Only files of a format that can be read count (so neither a marker file nor an unsupported format is taken for the song).
        private static string FindSong(string folder)
        {
            if (!Directory.Exists(folder))
            {
                return null;
            }
            foreach (string path in Directory.GetFiles(folder))
            {
                if (TypeOf(path) != AudioType.UNKNOWN
                    && Path.GetFileName(path).IndexOf(SongMarker, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return path;
                }
            }
            return null;
        }

        private static AudioType TypeOf(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".mp3":
                    return AudioType.MPEG;
                case ".wav":
                    return AudioType.WAV;
                default:
                    return AudioType.UNKNOWN;
            }
        }

        private static IEnumerator LoadOne(string path, Action<AudioClip> done)
        {
            if (!File.Exists(path))
            {
                Plugin.Log.LogWarning("Sound file not found: " + path);
                yield break;
            }
            AudioType type = TypeOf(path);
            if (type == AudioType.UNKNOWN)
            {
                Plugin.Log.LogWarning("Sound file " + Path.GetFileName(path) + " is in a format the mod cannot read (use mp3 or wav).");
                yield break;
            }

            string marker = path + MarkerExtension;
            if (File.Exists(marker))
            {
                Plugin.Log.LogWarning("Skipping " + Path.GetFileName(path) + ": the game crashed while it was being loaded last time. "
                    + "Delete " + Path.GetFileName(marker) + " to try it again.");
                yield break;
            }
            bool marked = TryWrite(marker);

            AudioClip clip = null;
            string error = null;
            using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, type))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    error = request.error;
                }
                else
                {
                    // The call that can bring the game down; the marker is only removed once it has come back.
                    clip = DownloadHandlerAudioClip.GetContent(request);
                }
            }
            if (marked)
            {
                TryDelete(marker);
            }

            if (clip == null || clip.length <= 0f)
            {
                Plugin.Log.LogWarning("Could not load " + path + ": " + (error ?? "it could not be decoded"));
                yield break;
            }
            clip.name = "UsableValuables " + Path.GetFileNameWithoutExtension(path);
            Plugin.Log.LogInfo("Loaded " + Path.GetFileName(path) + " (" + clip.length.ToString("F2") + " s).");
            done(clip);
        }

        // The clip with its samples scaled up so that its loudest is at the given level, if it was recorded quieter than that: a sound
        // cannot be played louder than it was recorded, and a quiet recording is lost next to the game's own sounds (the sponge's
        // bubbles peak at 0.21, about -32 dB on average). Left as it was if the clip cannot be read or written.
        private static AudioClip Louder(AudioClip clip, float peak)
        {
            try
            {
                float[] samples = new float[clip.samples * clip.channels];
                if (!clip.GetData(samples, 0))
                {
                    return clip;
                }
                float loudest = 0f;
                for (int i = 0; i < samples.Length; i++)
                {
                    loudest = Mathf.Max(loudest, Mathf.Abs(samples[i]));
                }
                if (loudest < 0.0001f || loudest >= peak)
                {
                    return clip;
                }
                float gain = peak / loudest;
                for (int i = 0; i < samples.Length; i++)
                {
                    samples[i] *= gain;
                }
                clip.SetData(samples, 0);
                Plugin.Log.LogInfo(clip.name + " was quiet (peak " + loudest.ToString("F2") + "): made " + gain.ToString("F1") + " times louder.");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("Could not make " + clip.name + " louder: " + ex.Message);
            }
            return clip;
        }

        private static bool TryWrite(string marker)
        {
            try
            {
                File.WriteAllText(marker, "The game was reading the sound file next to this one. If this file is still here after a start, that read crashed the game.");
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void TryDelete(string marker)
        {
            try
            {
                File.Delete(marker);
            }
            catch (Exception)
            {
            }
        }
    }
}
