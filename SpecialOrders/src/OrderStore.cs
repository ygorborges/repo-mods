using System;
using System.Collections.Generic;

namespace SpecialOrders
{
    // Host-side order book. It lives inside StatsManager.dictionaryOfDictionaries, so the game itself
    // saves it with the run, restores it on load and wipes it when a new run starts (ResetAllStats).
    internal static class OrderStore
    {
        private const string DictionaryName = "vibezSpecialOrders";
        private const string DepositSuffix = "|deposit";

        private static Dictionary<string, int> Store()
        {
            SortedDictionary<string, Dictionary<string, int>> all = Refs.StatDictionaries(StatsManager.instance);
            Dictionary<string, int> store;
            if (!all.TryGetValue(DictionaryName, out store))
            {
                store = new Dictionary<string, int>();
                all[DictionaryName] = store;
            }
            return store;
        }

        internal static List<OrderEntry> All()
        {
            Dictionary<string, int> store = Store();
            List<OrderEntry> orders = new List<OrderEntry>();
            foreach (KeyValuePair<string, int> pair in store)
            {
                if (pair.Key.EndsWith(DepositSuffix, StringComparison.Ordinal))
                {
                    continue;
                }
                int deposit;
                store.TryGetValue(pair.Key + DepositSuffix, out deposit);
                orders.Add(new OrderEntry { Key = pair.Key, Price = pair.Value, Deposit = deposit });
            }
            orders.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            return orders;
        }

        internal static bool TryGet(string key, out OrderEntry order)
        {
            order = null;
            Dictionary<string, int> store = Store();
            int price;
            if (string.IsNullOrEmpty(key) || !store.TryGetValue(key, out price))
            {
                return false;
            }
            int deposit;
            store.TryGetValue(key + DepositSuffix, out deposit);
            order = new OrderEntry { Key = key, Price = price, Deposit = deposit };
            return true;
        }

        internal static void Add(OrderEntry order)
        {
            Dictionary<string, int> store = Store();
            store[order.Key] = order.Price;
            store[order.Key + DepositSuffix] = order.Deposit;
        }

        internal static void Remove(string key)
        {
            Dictionary<string, int> store = Store();
            store.Remove(key);
            store.Remove(key + DepositSuffix);
        }
    }
}
