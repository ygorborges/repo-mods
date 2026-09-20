using System.Collections.Generic;
using UnityEngine;

namespace UsableValuables
{
    // Per-valuable bookkeeping, kept on every client: whether a switchable valuable is currently switched off
    // and when an action valuable is ready again. Keyed by the local component instance, so nothing is added to the game's objects.
    internal static class State
    {
        // A switched-off trap gets a chance to switch itself back on once every this many seconds while it is held.
        internal const float RollInterval = 1f;

        private sealed class Entry
        {
            public bool Off;
            public float ReadyAt;
            public float NextRoll;        // host only: when the switched-off valuable next gets its chance to switch itself on
            public KindId Kind;
            public Component Owner;
        }

        private static readonly Dictionary<int, Entry> entries = new Dictionary<int, Entry>();
        private static readonly Dictionary<int, float> candleIntensity = new Dictionary<int, float>();

        private static Entry Get(Component owner, bool create)
        {
            if (owner == null)
            {
                return null;
            }
            Entry entry;
            int id = owner.GetInstanceID();
            if (!entries.TryGetValue(id, out entry) && create)
            {
                entry = new Entry { Owner = owner };
                entries[id] = entry;
            }
            return entry;
        }

        internal static bool IsOff(Component owner)
        {
            Entry entry = Get(owner, false);
            return entry != null && entry.Off;
        }

        internal static void SetOff(Component owner, bool off, KindId kind = 0)
        {
            Entry entry = Get(owner, off);
            if (entry == null)
            {
                return;
            }
            if (off && !entry.Off)
            {
                // The first chance to switch itself back on comes a second after it was switched off.
                entry.NextRoll = Time.time + RollInterval;
            }
            entry.Off = off;
            if (off && kind != 0)
            {
                entry.Kind = kind;
            }
        }

        // The switched-off valuables whose next chance has come, with what kind they are. Each one's next chance is set a
        // second further on, so a valuable that stays off is offered exactly one chance per second.
        internal static List<KeyValuePair<Component, KindId>> TakeDue()
        {
            List<KeyValuePair<Component, KindId>> due = new List<KeyValuePair<Component, KindId>>();
            float now = Time.time;
            foreach (Entry entry in entries.Values)
            {
                if (!entry.Off || entry.Owner == null || entry.Kind == 0 || entry.NextRoll > now)
                {
                    continue;
                }
                entry.NextRoll = now + RollInterval;
                due.Add(new KeyValuePair<Component, KindId>(entry.Owner, entry.Kind));
            }
            return due;
        }

        // Flamethrowers and extinguishers that the key started: the host keeps an eye on them, to stop the flames if the
        // player lets go of the valuable while it is firing.
        internal struct WatchedTrigger
        {
            public Component Owner;
            public KindId Kind;
            public float StartedAt;
        }

        private static readonly Dictionary<int, WatchedTrigger> watched = new Dictionary<int, WatchedTrigger>();

        internal static void Watch(Component owner, KindId kind)
        {
            if (owner != null)
            {
                watched[owner.GetInstanceID()] = new WatchedTrigger { Owner = owner, Kind = kind, StartedAt = Time.time };
            }
        }

        internal static void Unwatch(Component owner)
        {
            if (owner != null)
            {
                watched.Remove(owner.GetInstanceID());
            }
        }

        // A copy of the list (it is changed while it is walked); valuables that no longer exist are dropped.
        internal static List<WatchedTrigger> WatchedTriggers()
        {
            List<WatchedTrigger> list = new List<WatchedTrigger>();
            List<int> gone = null;
            foreach (KeyValuePair<int, WatchedTrigger> pair in watched)
            {
                if (pair.Value.Owner == null)
                {
                    if (gone == null)
                    {
                        gone = new List<int>();
                    }
                    gone.Add(pair.Key);
                    continue;
                }
                list.Add(pair.Value);
            }
            if (gone != null)
            {
                foreach (int id in gone)
                {
                    watched.Remove(id);
                }
            }
            return list;
        }

        // Seconds until the valuable can be used again (0 = ready).
        internal static float Remaining(Component owner)
        {
            Entry entry = Get(owner, false);
            return entry == null ? 0f : Mathf.Max(0f, entry.ReadyAt - Time.time);
        }

        internal static void SetCooldown(Component owner, float seconds)
        {
            Entry entry = Get(owner, true);
            if (entry != null)
            {
                entry.ReadyAt = Time.time + Mathf.Max(0f, seconds);
            }
        }

        // The intensity a candle's light had before it was blown out, to put back when it is lit again.
        internal static bool TryTakeCandleIntensity(PropLight light, out float intensity)
        {
            int id = light.GetInstanceID();
            if (candleIntensity.TryGetValue(id, out intensity))
            {
                candleIntensity.Remove(id);
                return true;
            }
            return false;
        }

        internal static void RememberCandleIntensity(PropLight light, float intensity)
        {
            int id = light.GetInstanceID();
            if (!candleIntensity.ContainsKey(id))
            {
                candleIntensity[id] = intensity;
            }
        }
    }
}
