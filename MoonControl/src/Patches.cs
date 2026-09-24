using System;
using HarmonyLib;
using UnityEngine;

namespace MoonControl
{
    // Photon's client only exists safely once the game is up; register the network handler the same way the other two mods
    // in this profile do (REPOLib's own pattern too).
    [HarmonyPatch(typeof(RunManager), "Awake")]
    internal static class RunManagerAwakePatch
    {
        private static void Postfix()
        {
            try
            {
                Net.Init();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Could not register the network handler: " + ex);
            }

            try
            {
                MoonButton.Register();
                if (UnityEngine.Object.FindObjectOfType<MoonButtonSpawner>() == null)
                {
                    new GameObject("MoonControl watcher").AddComponent<MoonButtonSpawner>();
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Could not add the moon button: " + ex);
            }
        }
    }

    // The game recalculates the moon level here, every time levelsCompleted might have changed - by this point in
    // RunManager.ChangeLevel, levelCurrent already points at wherever the game is transitioning TO, so SemiFunc.RunIsLevel()
    // here genuinely means "a level is starting right now". Skipped: the level this mod tracks replaces it every time, on
    // every machine (host and clients both run this patch locally, so nothing needs a network message just to stay at the
    // value it was last told) - except in Random mode, where entering a level is exactly the moment a fresh surprise pick
    // gets rolled (the whole point of it staying a surprise until then).
    [HarmonyPatch(typeof(RunManager), nameof(RunManager.UpdateMoonLevel))]
    internal static class UpdateMoonLevelPatch
    {
        private static bool Prefix()
        {
            if (!Plugin.Enabled.Value)
            {
                return true;
            }
            try
            {
                if (MoonState.RandomMode && SemiFunc.RunIsLevel())
                {
                    MoonState.RollRandom();
                }
                else
                {
                    MoonState.SetApplied(MoonState.Applied);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Keeping the moon level failed (letting the game's own calculation run instead): " + ex);
                return true;
            }
            return false;
        }
    }

    // The panel that shows what is coming up when you hold Tab in a level (LevelUI) does not read RunManager.moonLevel at
    // all - it recalculates its own idea of the moon straight from levelsCompleted (RunManager.CalculateMoonLevel), the same
    // formula the game's own automatic moon uses, completely bypassing whatever level this mod actually has in effect. A
    // postfix (running every frame, same as the original) overrides just the moon-related pieces it touched, so what this
    // panel shows always matches what Moon Control says is applied instead of what would have been unlocked naturally.
    [HarmonyPatch(typeof(LevelUI), "Update")]
    internal static class LevelUIUpdatePatch
    {
        private static void Postfix(LevelUI __instance)
        {
            if (!Plugin.Enabled.Value || __instance == null || __instance.objectMoon == null)
            {
                return;
            }
            try
            {
                RunManager run = RunManager.instance;
                int level = MoonState.Applied;
                bool show = run != null && level > 0 && level <= run.moons.Count;
                __instance.objectMoon.SetActive(show);
                if (show)
                {
                    __instance.textMoon.text = run.MoonGetName(level);
                    __instance.iconMoon.texture = run.MoonGetIcon(level);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Could not correct the level panel's moon indicator: " + ex);
            }
        }
    }

    // Where a valuable's price is decided (a random roll within its preset, or a fixed override). Only ever runs on the
    // machine deciding the value for everyone (the host, or singleplayer): a non-host client is just told the finished
    // number over the network, so this never needs a guard for who is host.
    //
    // DollarValueSetLogic is called TWICE for every ordinary valuable during level generation - once directly by
    // ValuableDirector.SpawnValuable right after it is instantiated, and again a little later by the valuable's own Awake
    // (a coroutine it always starts). The method itself is idempotent about this (its own body only rolls a price the first
    // time, guarded by the dollarValueSet flag) - but a Harmony postfix runs after EVERY call regardless, so without this
    // fix the bonus was being multiplied in twice (a moon 2 run showing +69% instead of the intended +30%). The prefix reads
    // dollarValueSet before the original body can touch it, so the postfix can tell "this call just rolled a fresh price"
    // apart from "this call found one already set and did nothing".
    [HarmonyPatch(typeof(ValuableObject), nameof(ValuableObject.DollarValueSetLogic))]
    internal static class DollarValueSetLogicPatch
    {
        private static void Prefix(ValuableObject __instance, out bool __state)
        {
            __state = Refs.DollarValueSet(__instance);
        }

        private static void Postfix(ValuableObject __instance, bool __state)
        {
            if (!Plugin.Enabled.Value || __state)
            {
                return;
            }
            int level = MoonState.Applied;
            if (level <= 0)
            {
                return;
            }
            try
            {
                float multiplier = 1f + Plugin.ValueBonusPercent.Value / 100f * level;
                float baseValue = Refs.DollarValueCurrent(__instance);
                MoonBonus.Record(__instance, baseValue);
                Refs.DollarValueOriginal(__instance) = Round(Refs.DollarValueOriginal(__instance) * multiplier);
                Refs.DollarValueCurrent(__instance) = Round(baseValue * multiplier);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Applying the moon's value bonus to a valuable failed: " + ex);
            }
        }

        private static float Round(float value)
        {
            return Mathf.Round(value / 100f) * 100f;
        }
    }

    // The "$X" that floats over a valuable while you hold it (WorldSpaceUIValue.Show) otherwise just shows the
    // already-combined total (base price times the moon's multiplier), with no sign a bonus is even in effect. Rewrites it
    // to the base price plus the bonus amount in red whenever this specific valuable has one on record.
    [HarmonyPatch(typeof(WorldSpaceUIValue), nameof(WorldSpaceUIValue.Show), typeof(PhysGrabObject), typeof(int), typeof(bool), typeof(Vector3))]
    internal static class WorldSpaceUIValuePatch
    {
        private static void Postfix(WorldSpaceUIValue __instance, PhysGrabObject _grabObject, bool _cost)
        {
            if (!Plugin.Enabled.Value || _cost || _grabObject == null)
            {
                return;
            }
            try
            {
                ValuableObject valuable = _grabObject.GetComponent<ValuableObject>();
                float baseValue;
                if (valuable == null || !MoonBonus.TryGetBase(valuable, out baseValue))
                {
                    return;
                }
                int bonus = Mathf.RoundToInt(Refs.DollarValueCurrent(valuable) - baseValue);
                if (bonus <= 0)
                {
                    return;
                }
                TMPro.TextMeshProUGUI text = Refs.WorldSpaceUIText(__instance);
                if (text == null)
                {
                    return;
                }
                text.text = "$" + SemiFunc.DollarGetString((int)baseValue) + " <color=red>+" + SemiFunc.DollarGetString(bonus) + "</color>";
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Could not show the moon bonus on a held valuable's price: " + ex);
            }
        }
    }
}
