using System.Collections.Generic;
using UnityEngine;

namespace MoonControl
{
    // The moon level this mod is in charge of. Normally the game recalculates RunManager.moonLevel itself, every time
    // UpdateMoonLevel runs, from levelsCompleted (moonLevel = (levelsCompleted + 1) / 5 - see Refs and Patches): it only ever
    // goes up, automatically, as you finish levels. With this mod that recalculation is skipped; instead the level stays at
    // whatever was last applied (0 - no moon effects - until you choose otherwise).
    //
    // The applied level itself lives in StatsManager's own per-run dictionary (the same storage SpecialOrders keeps its
    // order book in), not a plain static field: a static only lives as long as the game process stays open, so a level
    // picked mid-run would quietly vanish the moment the game was closed and reopened, or read back as "none" on a later
    // visit that turned out to be a fresh process. The game already saves and loads this dictionary with the run, and wipes
    // it on a new run (ResetAllStats, called by RunManager.ResetProgress before it recalculates the moon level) - so the
    // level applied here persists exactly as long as the run's own save does, and needs no reset code of its own.
    internal static class MoonState
    {
        private const string DictionaryName = "vibezMoonControl";
        private const string AppliedKey = "applied";
        private const string RandomModeKey = "random";

        internal static int Applied
        {
            get
            {
                Dictionary<string, int> store = Store();
                int value;
                return store != null && store.TryGetValue(AppliedKey, out value) ? value : 0;
            }
        }

        // Whether "Random" is the selected strategy, independent of Applied (which still holds whatever concrete level is
        // actually in effect for gameplay/value purposes - RandomMode just means the UI shows "Random" as the pick instead
        // of that specific number, and UpdateMoonLevelPatch rerolls Applied to a fresh one each time a level starts).
        internal static bool RandomMode
        {
            get
            {
                Dictionary<string, int> store = Store();
                int value;
                return store != null && store.TryGetValue(RandomModeKey, out value) && value != 0;
            }
        }

        // How many moons your progress in this run has actually unlocked - the same sum the game itself would use
        // (RunManager.CalculateMoonLevel), read only, never written: the ceiling on what you are allowed to apply.
        internal static int Unlocked(RunManager run)
        {
            if (run == null)
            {
                return 0;
            }
            return (run.levelsCompleted + 1) / 5;
        }

        // Sets the level in effect right now, on this machine: persisted storage, RunManager's own field, and the change
        // flag, so the game's own moon banner and sound react exactly as they would to a natural moon change. Does not
        // touch RandomMode either way - SetRandomMode is the only thing that changes that.
        internal static void SetApplied(int level)
        {
            int clamped = Mathf.Max(0, level);
            Dictionary<string, int> store = Store();
            if (store != null)
            {
                store[AppliedKey] = clamped;
            }

            RunManager run = RunManager.instance;
            if (run == null)
            {
                return;
            }
            if (Refs.MoonLevel(run) != clamped)
            {
                Refs.MoonLevel(run) = clamped;
                Refs.MoonLevelChanged(run) = true;
            }
        }

        internal static void SetRandomMode(bool value)
        {
            Dictionary<string, int> store = Store();
            if (store != null)
            {
                store[RandomModeKey] = value ? 1 : 0;
            }
        }

        // Rolls a fresh concrete level among whatever is currently unlocked and applies it, without touching RandomMode -
        // called only when a level is actually starting (see UpdateMoonLevelPatch), so the pick stays a surprise right up
        // to the moment it takes effect, and changes again next level.
        internal static void RollRandom()
        {
            int unlocked = Unlocked(RunManager.instance);
            SetApplied(unlocked < 1 ? 0 : UnityEngine.Random.Range(1, unlocked + 1));
        }

        private static Dictionary<string, int> Store()
        {
            if (StatsManager.instance == null)
            {
                return null;
            }
            SortedDictionary<string, Dictionary<string, int>> all = Refs.StatDictionaries(StatsManager.instance);
            Dictionary<string, int> store;
            if (!all.TryGetValue(DictionaryName, out store))
            {
                store = new Dictionary<string, int>();
                all[DictionaryName] = store;
            }
            return store;
        }
    }
}
