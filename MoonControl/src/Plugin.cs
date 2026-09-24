using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace MoonControl
{
    // The game advances its "moon" (RunManager.moonLevel) on its own, one step every 5 levels, and once it goes up it
    // never goes back down for the rest of the run. This mod takes that away from the game entirely: the moon stays at
    // "none" until you choose otherwise, by grabbing the moon button that stands at a fixed spot in the truck (the same way
    // you hold the extraction point's own button - no key is bound to it), and only a level your progress has actually
    // unlocked can be chosen. Applying one turns its real effects on (whatever the game itself ties to that moon level) and
    // gives every valuable a bonus to what it is worth.
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency("nickklmao.menulib")]
    [BepInDependency("REPOLib")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "vibez.MoonControl";
        public const string Name = "MoonControl";
        public const string Version = "1.0.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<float> ValueBonusPercent;
        internal static ConfigEntry<float> ButtonPositionX;
        internal static ConfigEntry<float> ButtonPositionY;
        internal static ConfigEntry<float> ButtonPositionZ;
        internal static ConfigEntry<float> ButtonYawDegrees;
        internal static ConfigEntry<float> ButtonScale;
        internal static ConfigEntry<float> ButtonGlow;
        internal static ConfigEntry<float> LiveTuneSeconds;
        internal static ConfigEntry<float> MenuScale;

        private void Awake()
        {
            Log = Logger;

            Enabled = Config.Bind("General", "Enabled", true,
                "Turns the whole mod on or off. While on, the moon never advances by itself: it stays at \"none\" until you "
                + "grab the moon button in the truck, and a new run starts back at \"none\" again. Turn this off to get the "
                + "game's own moon behaviour back (it advances on its own, permanently, every 5 levels).");
            ValueBonusPercent = Config.Bind("General", "ValueBonusPercent", 10f,
                new ConfigDescription("How much applying a moon adds to what every valuable is worth, in percent per level applied, "
                    + "added together (10 means moon 2 is +20%, moon 3 is +30%, and so on). Only the host's setting is used.",
                    new AcceptableValueRange<float>(0f, 100f)));
            ButtonPositionX = Config.Bind("General", "ButtonPositionX", -13.3964f,
                "The moon button's fixed world X position in the truck. Use DevTools' coordinate probe (if installed) to find a "
                + "good spot: aim at the floor where you want the button and read its \"World XYZ\" off the on-screen readout.");
            ButtonPositionY = Config.Bind("General", "ButtonPositionY", 0.6852542f,
                "The moon button's fixed world Y position (height) in the truck.");
            ButtonPositionZ = Config.Bind("General", "ButtonPositionZ", -1.899255f,
                "The moon button's fixed world Z position in the truck.");
            ButtonYawDegrees = Config.Bind("General", "ButtonYawDegrees", 0f,
                "Which way the moon button faces, in degrees around the vertical axis. Only cosmetic - grabbing does not care "
                + "which way it is turned.");
            ButtonScale = Config.Bind("General", "ButtonScale", 1.8f,
                new ConfigDescription("How big the moon button is, as a multiplier of its normal size (1 = normal, 5 = five times "
                    + "bigger). Handy while hunting for it: turn this way up so it is impossible to miss, confirm you can see it "
                    + "somewhere, then dial the position in and bring the size back down. Grabbing still works at any size "
                    + "(the hitbox scales with it) - the host is the one who sets this. A changed value only applies the next time "
                    + "the button is placed - either by leaving the truck and coming back, or automatically if LiveTuneSeconds is on.",
                    new AcceptableValueRange<float>(0.1f, 20f)));
            ButtonGlow = Config.Bind("General", "ButtonGlow", 1f,
                new ConfigDescription("How strongly the moon glows, as a multiplier (1 = normal, 0 = no glow at all - just a lit "
                    + "model). It gives off a pale white: the moon itself lights up, and it casts that light onto the pedestal and "
                    + "the floor around it. Local only, and applied the next time the button is placed (leave the truck and come "
                    + "back, or use LiveTuneSeconds).",
                    new AcceptableValueRange<float>(0f, 5f)));
            LiveTuneSeconds = Config.Bind("General", "LiveTuneSeconds", 0f,
                new ConfigDescription("Debug convenience: while above 0, the moon button re-places itself on this interval (in "
                    + "seconds) for as long as you stay in the truck, picking up any ButtonPositionX/Y/Z/ButtonYawDegrees/"
                    + "ButtonScale change made through REPOConfig without having to leave and come back. 0 = off, placed once per "
                    + "visit as usual. Leave this at 0 outside of a tuning session - re-placing pulls the button out from under "
                    + "anyone holding it or about to grab it.",
                    new AcceptableValueRange<float>(0f, 30f)));
            MenuScale = Config.Bind("General", "MenuScale", 0.9f,
                new ConfigDescription("How big the moon menu popup is, as a multiplier of its normal size (1 = the size MenuLib "
                    + "gives it, smaller than 1 shrinks it). Takes effect the next time you open the menu.",
                    new AcceptableValueRange<float>(0.3f, 1.5f)));

            try
            {
                Harmony harmony = new Harmony(Guid);
                foreach (Type type in typeof(Plugin).Assembly.GetTypes())
                {
                    if (type.GetCustomAttributes(typeof(HarmonyPatch), false).Length == 0)
                    {
                        continue;
                    }
                    try
                    {
                        harmony.CreateClassProcessor(type).Patch();
                    }
                    catch (Exception ex)
                    {
                        Log.LogError("Patch " + type.Name + " failed: " + ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogError("The game changed in a way this version of " + Name + " does not understand; it will not work: " + ex);
                return;
            }

            string buildStamp;
            try
            {
                buildStamp = " (dll built " + System.IO.File.GetLastWriteTime(typeof(Plugin).Assembly.Location).ToString("yyyy-MM-dd HH:mm:ss") + ")";
            }
            catch (Exception)
            {
                buildStamp = "";
            }
            Log.LogInfo(Name + " " + Version + " loaded." + buildStamp);
        }
    }
}
