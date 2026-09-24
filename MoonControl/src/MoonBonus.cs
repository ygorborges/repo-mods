using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace MoonControl
{
    // Applies the applied moon's bonus to a valuable's price, and remembers what it was worth beforehand - so the price can
    // be shown broken apart later (base + bonus in red) instead of just the already-combined total. A ConditionalWeakTable
    // needs no cleanup of its own: an entry disappears on its own once the valuable it is about does.
    internal static class MoonBonus
    {
        private static readonly ConditionalWeakTable<ValuableObject, object> baseValues = new ConditionalWeakTable<ValuableObject, object>();

        // Returns how much was added (0 if no moon is applied, or it could not be applied).
        internal static float Apply(ValuableObject valuable)
        {
            int level = MoonState.Applied;
            if (valuable == null || level <= 0)
            {
                return 0f;
            }
            try
            {
                float multiplier = 1f + Plugin.ValueBonusPercent.Value / 100f * level;
                float baseValue = Refs.DollarValueCurrent(valuable);
                float bonused = Round(baseValue * multiplier);
                Record(valuable, baseValue);
                Refs.DollarValueOriginal(valuable) = Round(Refs.DollarValueOriginal(valuable) * multiplier);
                Refs.DollarValueCurrent(valuable) = bonused;
                return bonused - baseValue;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Applying the moon's value bonus to a valuable failed: " + ex);
                return 0f;
            }
        }

        // Prices in this game are whole hundreds.
        private static float Round(float value)
        {
            return Mathf.Round(value / 100f) * 100f;
        }

        internal static void Record(ValuableObject valuable, float baseValue)
        {
            baseValues.Remove(valuable);
            baseValues.Add(valuable, baseValue);
        }

        internal static bool TryGetBase(ValuableObject valuable, out float baseValue)
        {
            object boxed;
            if (valuable != null && baseValues.TryGetValue(valuable, out boxed))
            {
                baseValue = (float)boxed;
                return true;
            }
            baseValue = 0f;
            return false;
        }
    }
}
