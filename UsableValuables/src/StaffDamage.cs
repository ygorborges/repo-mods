using System;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace UsableValuables
{
    // The wizard staff's laser hurts enemies with a fixed number the game sets on the laser's hurt collider (enemyDamage, once every
    // enemyDamageCooldown seconds), whether the game fires the laser or the key does; nothing in this mod lowers it. Some players find
    // that little, so WizardStaff/EnemyDamageMultiplier scales it when the staff appears (1 = the game's own number, the default).
    // Only the host's number counts: the host is the one that hurts enemies.
    internal static class StaffDamage
    {
        private static ConfigEntry<float> multiplier;
        private static bool logged;

        internal static void Bind(ConfigFile config)
        {
            multiplier = config.Bind("WizardStaff", "EnemyDamageMultiplier", 1f,
                new ConfigDescription("How much harder the wizard staff's laser hurts enemies than in the game: 1 = the game's own damage, 3 = three "
                    + "times as much. Applies to the laser however it is fired (by the game or with the key). Only the host's setting is used. "
                    + "The game's damage per hit is written to the log when a staff appears.", new AcceptableValueRange<float>(0.1f, 20f)));
        }

        internal static void Apply(ValuableWizardStaff staff)
        {
            HurtCollider hurt = staff == null || staff.semiLaser == null ? null : staff.semiLaser.GetComponentInChildren<HurtCollider>(true);
            if (hurt == null)
            {
                return;
            }

            int original = hurt.enemyDamage;
            float factor = multiplier.Value;
            int scaled = Math.Abs(factor - 1f) < 0.001f ? original : Mathf.Max(1, Mathf.RoundToInt(original * factor));
            if (!logged)
            {
                logged = true;
                Plugin.Log.LogInfo("The wizard staff's laser does " + original + " damage to enemies every " + hurt.enemyDamageCooldown.ToString("F2")
                    + " s in the game" + (scaled != original ? "; with the multiplier of " + factor.ToString("F1") + " it does " + scaled + "." : "."));
            }
            hurt.enemyDamage = scaled;
        }
    }

    // The staff's laser exists from the moment the staff does.
    [HarmonyPatch(typeof(ValuableWizardStaff), "Start")]
    internal static class WizardStaffStartPatch
    {
        private static void Postfix(ValuableWizardStaff __instance)
        {
            try
            {
                StaffDamage.Apply(__instance);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Setting the wizard staff's damage failed: " + ex);
            }
        }
    }
}
