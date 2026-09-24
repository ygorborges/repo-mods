using System.Runtime.CompilerServices;

namespace MoonControl
{
    // Remembers, per valuable, what it was worth before the moon's bonus multiplied it - so the price can be shown broken
    // apart later (base + bonus in red) instead of just the already-combined total DollarValueSetLogic leaves behind. A
    // ConditionalWeakTable needs no cleanup of its own: an entry disappears on its own once the valuable it is about does.
    internal static class MoonBonus
    {
        private static readonly ConditionalWeakTable<ValuableObject, object> baseValues = new ConditionalWeakTable<ValuableObject, object>();

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
