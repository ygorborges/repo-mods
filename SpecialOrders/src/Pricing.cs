using UnityEngine;

namespace SpecialOrders
{
    internal static class Pricing
    {
        // Mirrors ItemAttributes.GetValue, but with the midpoint of the value range instead of a random roll.
        internal static int BasePrice(Item item)
        {
            ShopManager shop = ShopManager.instance;
            float multiplier = shop != null ? Refs.ItemValueMultiplier(shop) : 4f;

            float min = item.value != null ? item.value.valueMin : 1000f;
            float max = item.value != null ? item.value.valueMax : 1000f;
            float v = (min + max) * 0.5f * multiplier;
            if (v < 1000f)
            {
                v = 1000f;
            }
            v = Mathf.Ceil(v / 1000f);

            if (shop != null)
            {
                switch (item.itemType)
                {
                    case SemiFunc.itemType.item_upgrade:
                        v = shop.UpgradeValueGet(v, item);
                        break;
                    case SemiFunc.itemType.healthPack:
                        v = shop.HealthPackValueGet(v);
                        break;
                    case SemiFunc.itemType.power_crystal:
                        v = shop.CrystalValueGet(v);
                        break;
                }
            }
            return Mathf.Max(1, Mathf.RoundToInt(v));
        }

        internal static void Quote(Item item, out int price, out int deposit)
        {
            int basePrice = BasePrice(item);
            price = Mathf.Max(1, Mathf.CeilToInt(basePrice * (1f + Plugin.MarkupPercent.Value / 100f)));
            deposit = Mathf.CeilToInt(price * Plugin.DepositPercent.Value / 100f);
            deposit = Mathf.Clamp(deposit, 0, price - 1);
        }
    }
}
