using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace PaceControl
{
    // How long a level takes comes down to one number: how much of the map you have to carry to the extraction points. The
    // game works that out as a share of everything the level is worth, then splits it evenly between however many extraction
    // points the level got - and it gets more of those the bigger it is, which is to say the further into a run you are. By
    // level 15 that is five of them and most of the map on your back.
    //
    // This makes some of them optional. Every extraction point is still built, still works and is still worth exactly what
    // it always was - the level just stops needing all of them. Ask for 3 of the 5 and the level is over after the third,
    // with the two you skipped still standing there for anyone who wants to keep going for the money.
    [BepInPlugin(Guid, Name, Version)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "vibez.PaceControl";
        public const string Name = "PaceControl";
        public const string Version = "0.1.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<int> RequiredExtractions;
        internal static ConfigEntry<float> HaulGoalPercent;

        private void Awake()
        {
            Log = Logger;

            Enabled = Config.Bind("General", "Enabled", true,
                "Turns the whole mod on or off. Off gives you the game's own pacing back. The host's setting is the one that "
                + "counts - it tells everyone else how many the level needs.");
            RequiredExtractions = Config.Bind("Extractions", "RequiredExtractions", 0,
                new ConfigDescription("How many of a level's extraction points you actually have to complete before the level is "
                    + "done. 0 means all of them, the way the game plays normally (it builds one more every time levels get "
                    + "bigger, reaching 5 by level 15). Every extraction point is still built and still works: the ones past "
                    + "this number are simply optional, there to keep hauling if you want the money. Asking for more than the "
                    + "level has changes nothing.",
                    new AcceptableValueRange<int>(0, 6)));
            HaulGoalPercent = Config.Bind("General", "HaulGoalPercent", 100f,
                new ConfigDescription("A further multiplier on what each extraction asks for, in percent (60 means every "
                    + "extraction wants 60% of what it otherwise would). Separate from the setting above, and useful on its own "
                    + "if you would rather keep every extraction but make each one lighter. It lowers what you MUST bring in, "
                    + "never what you can: anything beyond the quota is still money.",
                    new AcceptableValueRange<float>(10f, 200f)));

            new Harmony(Guid).PatchAll();
            Log.LogInfo(Name + " " + Version + " loaded.");
        }
    }
}
