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
    [BepInDependency("REPOLib")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "vibez.UsableValuables";
        public const string Name = "UsableValuables";
        public const string Version = "0.2.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> ShowPrompt;

        private bool broken;
        private bool errorLogged;
        private bool reactivationErrorLogged;
        private bool watchErrorLogged;
        private bool selfStartErrorLogged;
        private bool mischiefErrorLogged;
        private bool shakeErrorLogged;
        private bool clientErrorLogged;
        private bool spongeErrorLogged;
        private bool soapErrorLogged;

        private void Awake()
        {
            Log = Logger;

            Enabled = Config.Bind("General", "Enabled", true, "Turns the whole mod on or off.");
            ShowPrompt = Config.Bind("General", "ShowPrompt", true,
                "Shows a hint (\"Press E to ...\") while you hold a valuable the key works on.");
            Kinds.Bind(Config);
            Mischief.Bind(Config);
            Sponge.Bind(Config);
            Soap.Bind(Config);
            StaffDamage.Bind(Config);

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

            // The mod's own sounds live in the audio folder next to the DLL; they are read in the background.
            StartCoroutine(UserAudio.Load(System.IO.Path.GetDirectoryName(Info.Location)));

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
                Mischief.Tick();
            }
            catch (Exception ex)
            {
                if (!mischiefErrorLogged)
                {
                    mischiefErrorLogged = true;
                    Log.LogError("Making the banana bow and the handface act up failed (logged once): " + ex);
                }
            }

            try
            {
                Mischief.ClientTick();
            }
            catch (Exception ex)
            {
                if (!clientErrorLogged)
                {
                    clientErrorLogged = true;
                    Log.LogError("Playing the banana bow's and the handface's sounds failed (logged once): " + ex);
                }
            }

            try
            {
                Sponge.Tick();
            }
            catch (Exception ex)
            {
                if (!spongeErrorLogged)
                {
                    spongeErrorLogged = true;
                    Log.LogError("Telling the other players about the dish sponge failed (logged once): " + ex);
                }
            }

            try
            {
                Soap.Tick();
            }
            catch (Exception ex)
            {
                if (!soapErrorLogged)
                {
                    soapErrorLogged = true;
                    Log.LogError("Telling the other players about the soap failed (logged once): " + ex);
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

        // The handface's shaking is physics.
        private void FixedUpdate()
        {
            if (broken || !Enabled.Value)
            {
                return;
            }
            try
            {
                Mischief.FixedTick();
            }
            catch (Exception ex)
            {
                if (!shakeErrorLogged)
                {
                    shakeErrorLogged = true;
                    Log.LogError("Shaking the handface failed (logged once): " + ex);
                }
            }
        }
    }
}
