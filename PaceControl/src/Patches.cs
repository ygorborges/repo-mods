using System;
using HarmonyLib;
using UnityEngine;

namespace PaceControl
{
    // Photon's client only exists safely once the game is up, the same place the other mods in this collection hook their
    // own network handlers.
    [HarmonyPatch(typeof(RunManager), "Awake")]
    internal static class RunManagerAwakePatch
    {
        private static void Postfix()
        {
            try
            {
                Net.Init();
                if (UnityEngine.Object.FindObjectOfType<PaceWatcher>() == null)
                {
                    new GameObject("PaceControl watcher").AddComponent<PaceWatcher>();
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Could not start up: " + ex);
            }
        }
    }

    // Where a level's quota lands, already worked out as a share of everything the level is worth, and before any extraction
    // point has been pressed. By now every extraction point has counted itself in, so this is also the first moment the real
    // count is known - which is where the level's needed count is settled and sent out.
    //
    // The quota comes down in the same proportion as the points no longer needed: 3 of 5 leaves 60% of it. Since each
    // extraction asks for the quota divided by the count, and both have been cut by the same proportion, one extraction
    // still asks for exactly what it always did. You just do fewer of them.
    [HarmonyPatch(typeof(RoundDirector), "StartRoundLogic")]
    internal static class StartRoundLogicPatch
    {
        private static void Prefix(RoundDirector __instance, ref int value)
        {
            if (!Plugin.Enabled.Value || value <= 0 || !SemiFunc.RunIsLevel())
            {
                return;
            }
            // A client is only ever told the finished number by the host, and the host's own broadcast comes back round to
            // it - scaling either of those would be scaling twice.
            if (!SemiFunc.IsMasterClientOrSingleplayer() || value == PaceState.lastScaled)
            {
                return;
            }
            try
            {
                int built = Refs.ExtractionPoints(__instance);
                if (built <= 0)
                {
                    return;
                }
                int wanted = Plugin.RequiredExtractions.Value;
                int needed = wanted > 0 ? Mathf.Clamp(wanted, 1, built) : built;

                float factor = (float)needed / built * (Plugin.HaulGoalPercent.Value / 100f);
                int scaled = Mathf.Max(1, Mathf.RoundToInt(value * factor));
                Plugin.Log.LogInfo("Level needs " + needed + " of its " + built + " extraction point(s); haul goal "
                    + value + " -> " + scaled + " (" + (scaled / needed) + " each).");
                PaceState.lastScaled = scaled;
                value = scaled;

                Net.Announce(needed, built);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Could not set this level's pace: " + ex);
            }
        }
    }

    // Finishing the last needed extraction is what normally switches the level into its endgame: a scare, the lights going
    // out across the whole map, enemies spawning between you and the truck and all of them coming for you. That is a fine
    // way to end a level you are done with - and no way at all to go and do the extraction points this mod just made
    // optional, which was the whole point of leaving them there.
    //
    // So on a level with optional points left over, that state is held back until the truck is actually about to pull out
    // (PaceWatcher asks for it then). It is worth holding rather than picking apart because two things worth keeping hang
    // off the same flag: the full heal everyone gets in the truck at the end of a level, and anyone dead being revived
    // there. Both want it on, both want it late, and both get their moment this way.
    [HarmonyPatch(typeof(RoundDirector), nameof(RoundDirector.ExtractionCompletedAllCheck))]
    internal static class ExtractionCompletedAllCheckPatch
    {
        private static bool Prefix(RoundDirector __instance)
        {
            if (!Plugin.Enabled.Value)
            {
                return true;
            }
            // Never announce the end of the level twice (an optional extraction done afterwards would).
            if (Refs.AllExtractionPointsCompleted(__instance))
            {
                return false;
            }
            return !PaceState.Optional || PaceState.allowEnd;
        }
    }
}
