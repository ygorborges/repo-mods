using System.Collections.Generic;
using UnityEngine;

namespace SpecialOrders
{
    // All order rules run here, on the host (or in singleplayer). Clients only ask and display.
    internal static class Master
    {
        internal static ResultCode Execute(RequestType type, string key)
        {
            switch (type)
            {
                case RequestType.Place:
                    return Place(key);
                case RequestType.Cancel:
                    return Cancel(key);
                default:
                    return ResultCode.None;
            }
        }

        private static ResultCode Place(string key)
        {
            if (!SemiFunc.RunIsShop())
            {
                return ResultCode.NotInShop;
            }

            Item item;
            if (string.IsNullOrEmpty(key) || !StatsManager.instance.itemDictionary.TryGetValue(key, out item) || !ItemRules.IsListed(item))
            {
                return ResultCode.NotFound;
            }

            OrderEntry existing;
            if (OrderStore.TryGet(key, out existing))
            {
                return ResultCode.Unavailable;
            }
            if (ItemRules.Evaluate(item, ItemRules.CurrentStock()) != ItemState.Available)
            {
                return ResultCode.Unavailable;
            }

            int price;
            int deposit;
            Pricing.Quote(item, out price, out deposit);

            int money = SemiFunc.StatGetRunCurrency();
            if (money < deposit)
            {
                return ResultCode.NoMoney;
            }

            if (deposit > 0)
            {
                SemiFunc.StatSetRunCurrency(money - deposit);
                RefreshCurrencyUi();
            }
            OrderStore.Add(new OrderEntry { Key = key, Price = price, Deposit = deposit });
            Plugin.Log.LogInfo("Order placed: " + key + " for $" + price + "K (deposit $" + deposit + "K).");
            return ResultCode.Placed;
        }

        private static ResultCode Cancel(string key)
        {
            if (Delivery.IsInShop(key))
            {
                return ResultCode.Unavailable;
            }
            OrderEntry order;
            if (!OrderStore.TryGet(key, out order))
            {
                return ResultCode.NotFound;
            }

            int refund = Mathf.FloorToInt(order.Deposit * Plugin.CancelRefundPercent.Value / 100f);
            if (refund > 0)
            {
                SemiFunc.StatSetRunCurrency(SemiFunc.StatGetRunCurrency() + refund);
                RefreshCurrencyUi();
            }
            OrderStore.Remove(key);
            Plugin.Log.LogInfo("Order cancelled: " + key + " (refunded $" + refund + "K of $" + order.Deposit + "K).");
            return ResultCode.Cancelled;
        }

        internal static ListingSnapshot Build(ResultCode result, string resultKey)
        {
            HashSet<string> stock = ItemRules.CurrentStock();
            List<string> keys = new List<string>();
            List<int> prices = new List<int>();
            List<int> deposits = new List<int>();
            List<byte> states = new List<byte>();

            foreach (KeyValuePair<string, Item> pair in StatsManager.instance.itemDictionary)
            {
                Item item = pair.Value;
                // A delivered order is no longer in the order book, only in the shop; either way the row shows what was paid.
                OrderEntry order;
                bool inShop = Delivery.TryGetInShop(pair.Key, out order);
                bool ordered = inShop || OrderStore.TryGet(pair.Key, out order);
                if (!ordered && !ItemRules.IsListed(item))
                {
                    continue;
                }

                int price;
                int deposit;
                ItemState state;
                if (ordered)
                {
                    price = order.Price;
                    deposit = order.Deposit;
                    state = inShop ? ItemState.Ready : ItemState.Ordered;
                }
                else
                {
                    Pricing.Quote(item, out price, out deposit);
                    state = ItemRules.Evaluate(item, stock);
                }

                keys.Add(pair.Key);
                prices.Add(price);
                deposits.Add(deposit);
                states.Add((byte)state);
            }

            return new ListingSnapshot
            {
                Result = result,
                ResultKey = resultKey ?? "",
                Money = SemiFunc.StatGetRunCurrency(),
                MarkupPercent = Plugin.MarkupPercent.Value,
                DepositPercent = Plugin.DepositPercent.Value,
                RefundPercent = Plugin.CancelRefundPercent.Value,
                Keys = keys.ToArray(),
                Prices = prices.ToArray(),
                Deposits = deposits.ToArray(),
                States = states.ToArray(),
            };
        }

        private static void RefreshCurrencyUi()
        {
            if (CurrencyUI.instance != null)
            {
                CurrencyUI.instance.FetchCurrency();
            }
        }
    }
}
