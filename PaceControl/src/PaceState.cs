using UnityEngine;

namespace PaceControl
{
    // How many extraction points this level is to need, and the machinery that keeps that number in place.
    //
    // The whole mod rests on one number: RoundDirector.extractionPoints. The game reads it in four places, and every one of
    // them wants to mean "how many this level needs" - what each extraction asks for (the level's quota divided by it),
    // whether the truck will leave, whether it opens up to heal everyone, and the count on the HUD. Telling it there are 3
    // when 5 were built therefore does the whole job on its own: the two extra ones are still standing, still unlocked and
    // still worth the same, they just are not counted any more. Nothing indexes that number into the list of actual points
    // (that is a separate list, left alone), so nothing goes looking for points that are not there.
    internal static class PaceState
    {
        // 0 until the host has said otherwise for this level.
        internal static int need;

        // How many the level actually built, so the mod knows whether it made any of them optional at all.
        internal static int built;

        // The last quota this mod worked out. The host runs the quota through the same method twice - once when it decides
        // it, and again through its own broadcast of it - so without this the second pass would scale the already scaled
        // number all over again.
        internal static int lastScaled = -1;

        // Set for the one call in which the end of the level is genuinely wanted (see PaceWatcher).
        internal static bool allowEnd;

        private static bool endTriggered;
        private static RoundDirector director;

        // Whether this level has extraction points that are there but not needed.
        internal static bool Optional
        {
            get { return Plugin.Enabled.Value && need > 0 && built > need; }
        }

        internal static void Apply(int needed, int builtCount)
        {
            need = needed;
            built = builtCount;
        }

        // Each level gets its own RoundDirector, so a new one means a new level and nothing carried over from the last.
        internal static void Tick()
        {
            RoundDirector current = RoundDirector.instance;
            if (current == null)
            {
                return;
            }
            if (!ReferenceEquals(current, director))
            {
                director = current;
                need = 0;
                built = 0;
                lastScaled = -1;
                allowEnd = false;
                endTriggered = false;
                return;
            }
            if (need <= 0 || !Plugin.Enabled.Value || !SemiFunc.RunIsLevel())
            {
                return;
            }

            // Held rather than set once: extraction points count themselves in as they wake up, and on a slower machine one
            // of them can still be arriving after the host has already said how many the level needs.
            //
            // Never below what has actually been done, either. The truck only pulls out when completed and needed are the
            // same number, so doing an optional extraction on top would otherwise put the count past the target and strand
            // everyone in a level that could no longer end. Each one done simply counts itself in.
            int target = Mathf.Max(need, Refs.ExtractionPointsCompleted(current));
            if (Refs.ExtractionPoints(current) != target)
            {
                Refs.ExtractionPoints(current) = target;
            }

            // The moment the truck is ready to pull out is when the game's own end-of-level state is finally wanted - see
            // ExtractionCompletedAllCheckPatch for why it is held back until here. Same condition the truck itself leaves
            // on, so this lands right as it does, in time for the healing and reviving that state switches on.
            if (!endTriggered && Optional && SemiFunc.IsMasterClientOrSingleplayer()
                && Refs.ExtractionPointsCompleted(current) >= need && SemiFunc.PlayersAllInTruck())
            {
                endTriggered = true;
                allowEnd = true;
                try
                {
                    current.ExtractionCompletedAllCheck();
                }
                finally
                {
                    allowEnd = false;
                }
            }
        }
    }

    internal sealed class PaceWatcher : MonoBehaviour
    {
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            PaceState.Tick();
        }
    }
}
