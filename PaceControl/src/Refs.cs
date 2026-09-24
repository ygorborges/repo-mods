using HarmonyLib;

namespace PaceControl
{
    // Fast accessors for members the game marks internal.
    internal static class Refs
    {
        // How many extraction points the level is taken to have. Every ExtractionPoint adds itself to this as it wakes up,
        // so it starts out as the real count - and this mod then holds it at however many the level actually needs.
        internal static readonly AccessTools.FieldRef<RoundDirector, int> ExtractionPoints =
            AccessTools.FieldRefAccess<RoundDirector, int>("extractionPoints");

        // How many have been completed so far.
        internal static readonly AccessTools.FieldRef<RoundDirector, int> ExtractionPointsCompleted =
            AccessTools.FieldRefAccess<RoundDirector, int>("extractionPointsCompleted");

        // Whether the level has already been called done (the lights-out, everything-comes-for-you state).
        internal static readonly AccessTools.FieldRef<RoundDirector, bool> AllExtractionPointsCompleted =
            AccessTools.FieldRefAccess<RoundDirector, bool>("allExtractionPointsCompleted");
    }
}
