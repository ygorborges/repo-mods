using System.Collections.Generic;

namespace SpecialOrders
{
    internal static class ItemRules
    {
        // Items that are never offered by the regular shop (disabled, secret-room only, or without a prefab) can't be ordered either.
        internal static bool IsListed(Item item)
        {
            return item != null
                && !item.disabled
                && item.maxAmountInShop > 0
                && item.itemSecretShopType == SemiFunc.itemSecretShopType.none
                && item.prefab != null
                && item.prefab.IsValid();
        }

        internal static ItemState Evaluate(Item item, HashSet<string> stock)
        {
            StatsManager stats = StatsManager.instance;

            if (SemiFunc.StatGetItemsPurchased(item.name) >= item.maxAmountInShop)
            {
                return ItemState.MaxOwned;
            }
            if (item.maxPurchase && stats.GetItemsUpgradesPurchasedTotal(item.name) >= item.maxPurchaseAmount)
            {
                return ItemState.MaxPurchased;
            }
            if (item.minPlayerCount > 1 && GameDirector.instance.PlayerList.Count < item.minPlayerCount)
            {
                return ItemState.NeedsPlayers;
            }
            if (stock != null && stock.Contains(item.name))
            {
                return ItemState.InStock;
            }
            return ItemState.Available;
        }

        internal static HashSet<string> CurrentStock()
        {
            HashSet<string> stock = new HashSet<string>();
            if (!SemiFunc.RunIsShop())
            {
                return stock;
            }
            foreach (ItemAttributes attributes in UnityEngine.Object.FindObjectsOfType<ItemAttributes>())
            {
                if (attributes != null && attributes.item != null)
                {
                    stock.Add(attributes.item.name);
                }
            }
            return stock;
        }
    }
}
