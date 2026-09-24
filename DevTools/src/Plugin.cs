using System;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DevTools
{
    // Shortcuts for testing my other mods. Not published; keep it out of any exported profile.
    [BepInPlugin("vibez.DevTools", "DevTools", "0.1.6")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private ConfigEntry<Key> winAndCashKey;
        private ConfigEntry<Key> spawnKey;
        private ConfigEntry<Key> spongeKey;
        private ConfigEntry<Key> soapKey;
        private ConfigEntry<Key> coordProbeKey;
        private ConfigEntry<Key> coordCaptureKey;
        private ConfigEntry<int> cashK;
        private CoordProbe coordProbe;

        private void Awake()
        {
            winAndCashKey = Config.Bind("Keys", "WinLevelAndCash", Key.F9,
                "Adds cash and, when you are inside a level, wins it (goes to the shop). Host or singleplayer only. Set to None to disable.");
            spawnKey = Config.Bind("Keys", "SpawnUsableValuable", Key.F10,
                "Spawns a random valuable that the UsableValuables mod works on (flashlight, boombox, candle, ice saw, wands...) in front of you. Host or singleplayer only. Set to None to disable.");
            spongeKey = Config.Bind("Keys", "SpawnDishSponge", Key.F8,
                "Spawns the dish sponge (the valuable UsableValuables adds) in front of you. Host or singleplayer only. Set to None to disable.");
            soapKey = Config.Bind("Keys", "SpawnSoap", Key.F7,
                "Spawns a bar of soap (the valuable UsableValuables adds) in front of you. Host or singleplayer only. Set to None to disable.");
            coordProbeKey = Config.Bind("Keys", "ToggleCoordProbe", Key.F6,
                "Toggles an on-screen readout of the world XYZ position wherever you are looking - useful for tuning a fixed "
                + "position config (like MoonControl's ButtonPositionX/Y/Z) without guessing and reloading. Set to None to "
                + "disable.");
            coordCaptureKey = Config.Bind("Keys", "CaptureCoordProbe", Key.F5,
                "While the coordinate probe (ToggleCoordProbe) is on, writes the exact position it is showing to this log as a "
                + "single \"" + CoordProbe.CaptureTag + "\" line - so it can be read back from the log and applied to a .cfg "
                + "without having to be typed in by hand. Set to None to disable.");
            cashK = Config.Bind("Cheats", "CashK", 100,
                "Cash added on each press, in thousands ($K).");
            coordProbe = gameObject.AddComponent<CoordProbe>();
            coordProbe.Log = Logger;
            Logger.LogInfo("DevTools loaded. " + winAndCashKey.Value + " = win level + $" + cashK.Value + "K, " + spawnKey.Value + " = spawn a usable valuable, " + spongeKey.Value + " = spawn the dish sponge, " + soapKey.Value + " = spawn the soap, " + coordProbeKey.Value + " = toggle the coordinate probe, " + coordCaptureKey.Value + " = capture its reading to the log.");
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }
            OnKey(winAndCashKey, keyboard, WinLevelAndCash);
            OnKey(spawnKey, keyboard, SpawnUsableValuable);
            OnKey(spongeKey, keyboard, SpawnDishSponge);
            OnKey(soapKey, keyboard, SpawnSoap);
            OnKey(coordProbeKey, keyboard, ToggleCoordProbe);
            OnKey(coordCaptureKey, keyboard, coordProbe.Capture);
        }

        private void ToggleCoordProbe()
        {
            coordProbe.Visible = !coordProbe.Visible;
            Logger.LogInfo("Coordinate probe " + (coordProbe.Visible ? "on" : "off") + ".");
        }

        private void OnKey(ConfigEntry<Key> entry, Keyboard keyboard, Action action)
        {
            Key key = entry.Value;
            if (key == Key.None || !keyboard[key].wasPressedThisFrame)
            {
                return;
            }
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Logger.LogError("Shortcut failed: " + ex);
            }
        }

        // Shortcuts only make sense in a run, with no menu or text field taking the keyboard, and only the host may change the game.
        private bool CanRun()
        {
            if (RunManager.instance == null || SemiFunc.MenuLevel() || !SemiFunc.NoTextInputsActive())
            {
                return false;
            }
            if (!SemiFunc.IsMasterClientOrSingleplayer())
            {
                Logger.LogInfo("Only the host can use this shortcut.");
                return false;
            }
            return true;
        }

        private void WinLevelAndCash()
        {
            if (!CanRun())
            {
                return;
            }

            int before = SemiFunc.StatGetRunCurrency();
            SemiFunc.StatSetRunCurrency(before + cashK.Value);
            Logger.LogInfo("Cash $" + before + "K -> $" + SemiFunc.StatGetRunCurrency() + "K.");

            if (SemiFunc.RunIsLevel())
            {
                Logger.LogInfo("Winning the level.");
                RunManager.instance.ChangeLevel(_completedLevel: true, _levelFailed: false);
            }
            else
            {
                SemiFunc.UIFocusText("DEV: +$" + cashK.Value + "K", new Color(1f, 0.82f, 0.29f), Color.white, 2f);
            }
        }

        private void SpawnDishSponge()
        {
            if (!CanRun())
            {
                return;
            }

            string spawned = Spawner.SpawnByPath("Valuables/Valuable Dish Sponge");
            if (spawned == null)
            {
                Logger.LogWarning("The dish sponge was not spawned: UsableValuables did not register it (see its log lines), or there is no player camera yet.");
                SemiFunc.UIFocusText("DEV: no dish sponge available", new Color(1f, 0.4f, 0.3f), Color.white, 2.5f);
                return;
            }
            Logger.LogInfo("Spawned " + spawned + ".");
            SemiFunc.UIFocusText("DEV: spawned " + spawned, new Color(1f, 0.82f, 0.29f), Color.white, 2.5f);
        }

        private void SpawnSoap()
        {
            if (!CanRun())
            {
                return;
            }

            string spawned = Spawner.SpawnByPath("Valuables/Valuable Soap");
            if (spawned == null)
            {
                Logger.LogWarning("The soap was not spawned: UsableValuables did not register it (see its log lines), or there is no player camera yet.");
                SemiFunc.UIFocusText("DEV: no soap available", new Color(1f, 0.4f, 0.3f), Color.white, 2.5f);
                return;
            }
            Logger.LogInfo("Spawned " + spawned + ".");
            SemiFunc.UIFocusText("DEV: spawned " + spawned, new Color(1f, 0.82f, 0.29f), Color.white, 2.5f);
        }

        private void SpawnUsableValuable()
        {
            if (!CanRun())
            {
                return;
            }

            string spawned = Spawner.SpawnRandom(Logger);
            if (spawned == null)
            {
                Logger.LogWarning("Nothing to spawn: no valuable that UsableValuables works on was found in the level presets.");
                SemiFunc.UIFocusText("DEV: no usable valuable found", new Color(1f, 0.4f, 0.3f), Color.white, 2.5f);
                return;
            }
            Logger.LogInfo("Spawned " + spawned + ".");
            SemiFunc.UIFocusText("DEV: spawned " + spawned, new Color(1f, 0.82f, 0.29f), Color.white, 2.5f);
        }
    }
}
