using System;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DevTools
{
    // Shortcuts for testing my other mods. Not published; keep it out of any exported profile.
    [BepInPlugin("vibez.DevTools", "DevTools", "0.1.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private ConfigEntry<Key> winAndCashKey;
        private ConfigEntry<int> cashK;

        private void Awake()
        {
            winAndCashKey = Config.Bind("Keys", "WinLevelAndCash", Key.F9,
                "Adds cash and, when you are inside a level, wins it (goes to the shop). Host or singleplayer only. Set to None to disable.");
            cashK = Config.Bind("Cheats", "CashK", 100,
                "Cash added on each press, in thousands ($K).");
            Logger.LogInfo("DevTools loaded. " + winAndCashKey.Value + " = win level + $" + cashK.Value + "K.");
        }

        private void Update()
        {
            Key key = winAndCashKey.Value;
            if (key == Key.None)
            {
                return;
            }
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard[key].wasPressedThisFrame)
            {
                return;
            }

            try
            {
                WinLevelAndCash();
            }
            catch (Exception ex)
            {
                Logger.LogError("Shortcut failed: " + ex);
            }
        }

        private void WinLevelAndCash()
        {
            if (RunManager.instance == null || SemiFunc.MenuLevel() || !SemiFunc.NoTextInputsActive())
            {
                return;
            }
            if (!SemiFunc.IsMasterClientOrSingleplayer())
            {
                Logger.LogInfo("Only the host can use this shortcut.");
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
    }
}
