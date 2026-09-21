using System;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace UsableValuables
{
    // Hold a valuable that does something and press the Interact key (E) to use it.
    [BepInPlugin(Guid, Name, Version)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "vibez.UsableValuables";
        public const string Name = "UsableValuables";
        public const string Version = "0.1.4";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> ShowPrompt;

        private bool broken;
        private bool errorLogged;
        private bool reactivationErrorLogged;
        private bool watchErrorLogged;
        private bool selfStartErrorLogged;

        private void Awake()
        {
            Log = Logger;

            Enabled = Config.Bind("General", "Enabled", true, "Turns the whole mod on or off.");
            ShowPrompt = Config.Bind("General", "ShowPrompt", true,
                "Shows a hint (\"Press E to ...\") while you hold a valuable the key works on.");
            Kinds.Bind(Config);

            try
            {
                RuntimeHelpers.RunClassConstructor(typeof(Refs).TypeHandle);
            }
            catch (Exception ex)
            {
                broken = true;
                Log.LogError("The game changed in a way this version of " + Name + " does not understand; it will not work: " + ex);
                return;
            }

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

            try
            {
                MaskPatches.Apply(harmony);
            }
            catch (Exception ex)
            {
                Log.LogError("Could not patch the switchable valuables: " + ex);
            }

            Log.LogInfo(Name + " " + Version + " loaded.");
        }

        private void Update()
        {
            if (broken || !Enabled.Value)
            {
                return;
            }
            try
            {
                Controller.Tick();
            }
            catch (Exception ex)
            {
                if (!errorLogged)
                {
                    errorLogged = true;
                    Log.LogError("Reading the key failed (logged once): " + ex);
                }
            }

            try
            {
                Reactivation.Tick();
            }
            catch (Exception ex)
            {
                if (!reactivationErrorLogged)
                {
                    reactivationErrorLogged = true;
                    Log.LogError("Switching traps back on failed (logged once): " + ex);
                }
            }

            try
            {
                SelfStart.Tick();
            }
            catch (Exception ex)
            {
                if (!selfStartErrorLogged)
                {
                    selfStartErrorLogged = true;
                    Log.LogError("Setting the traps off failed (logged once): " + ex);
                }
            }

            try
            {
                TriggerWatch.Tick();
            }
            catch (Exception ex)
            {
                if (!watchErrorLogged)
                {
                    watchErrorLogged = true;
                    Log.LogError("Watching the flames failed (logged once): " + ex);
                }
            }
        }
    }
}
