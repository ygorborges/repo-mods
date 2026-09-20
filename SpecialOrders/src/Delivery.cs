using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace SpecialOrders
{
    // Host only. Puts ordered items into the shop before the regular stock is rolled and prices them.
    // An order is used up by its delivery: the item is offered in that one shop visit, and if it isn't bought
    // the order is over (the deposit is not refunded). The order book entry is removed on delivery; the record
    // below only lives for the visit, so a shop that is left and re-entered without a save still delivers it again.
    internal static class Delivery
    {
        private sealed class Delivered
        {
            public OrderEntry Order;
            public GameObject Object;
        }

        private static readonly Dictionary<int, Delivered> delivered = new Dictionary<int, Delivered>();

        // True while the ordered copy of this item is physically in the shop (it hasn't been bought or destroyed yet).
        internal static bool IsInShop(string key)
        {
            OrderEntry order;
            return TryGetInShop(key, out order);
        }

        internal static bool TryGetInShop(string key, out OrderEntry order)
        {
            foreach (Delivered d in delivered.Values)
            {
                if (d.Object != null && d.Order.Key == key)
                {
                    order = d.Order;
                    return true;
                }
            }
            order = null;
            return false;
        }

        internal static void Run()
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.RunIsShop())
            {
                return;
            }

            delivered.Clear();
            ShopManager shop = ShopManager.instance;
            StatsManager stats = StatsManager.instance;
            if (shop == null || stats == null)
            {
                return;
            }

            List<OrderEntry> orders = OrderStore.All();
            if (orders.Count == 0)
            {
                return;
            }

            List<string> arrived = new List<string>();
            foreach (OrderEntry order in orders)
            {
                Item item;
                if (!stats.itemDictionary.TryGetValue(order.Key, out item) || !ItemRules.IsListed(item))
                {
                    Plugin.Log.LogWarning("Order '" + order.Key + "' can't be delivered: item no longer exists. Keeping it.");
                    continue;
                }

                ItemState state = ItemRules.Evaluate(item, null);
                if (state != ItemState.Available)
                {
                    Plugin.Log.LogInfo("Order '" + order.Key + "' waits: cannot be delivered right now (" + state + ").");
                    continue;
                }

                ItemVolume volume = FindVolume(shop, item);
                if (volume == null)
                {
                    Plugin.Log.LogInfo("Order '" + order.Key + "' waits: no free " + item.itemVolume + " slot in this shop.");
                    continue;
                }

                GameObject spawned = Spawn(shop, item, volume);
                ItemAttributes attributes = spawned != null ? spawned.GetComponentInChildren<ItemAttributes>(true) : null;
                if (attributes == null)
                {
                    Plugin.Log.LogWarning("Order '" + order.Key + "' could not be spawned" + (spawned != null ? " (no ItemAttributes on the prefab)." : "."));
                    if (spawned != null)
                    {
                        UnityEngine.Object.Destroy(spawned);
                    }
                    continue;
                }

                shop.itemVolumes.Remove(volume);
                UnityEngine.Object.Destroy(volume.gameObject);
                shop.potentialItems.Remove(item);
                shop.potentialItemConsumables.Remove(item);
                shop.potentialItemUpgrades.Remove(item);
                shop.potentialItemHealthPacks.Remove(item);

                delivered[attributes.gameObject.GetInstanceID()] = new Delivered { Order = order, Object = attributes.gameObject };
                OrderStore.Remove(order.Key);
                arrived.Add(order.Key);
                Plugin.Log.LogInfo("Order delivered: " + order.Key + " at $" + order.Remaining + "K. The order is used up; if it isn't bought in this shop it is lost.");
            }

            Net.AnnounceDelivered(arrived.ToArray());
        }

        private static ItemVolume FindVolume(ShopManager shop, Item item)
        {
            foreach (ItemVolume volume in shop.itemVolumes)
            {
                if (volume != null && volume.itemVolume == item.itemVolume)
                {
                    return volume;
                }
            }
            return null;
        }

        // Same placement logic as PunManager.SpawnShopItem.
        private static GameObject Spawn(ShopManager shop, Item item, ItemVolume volume)
        {
            Transform helper = shop.itemRotateHelper;
            helper.parent = volume.transform;
            helper.localRotation = item.spawnRotationOffset;
            Quaternion rotation = helper.rotation;
            helper.parent = shop.transform;

            if (SemiFunc.IsMultiplayer())
            {
                return PhotonNetwork.InstantiateRoomObject(item.prefab.ResourcePath, volume.transform.position, rotation, 0);
            }

            GameObject prefab = item.prefab.Prefab;
            return prefab != null ? UnityEngine.Object.Instantiate(prefab, volume.transform.position, rotation) : null;
        }

        internal static void ApplyOrderPrice(ItemAttributes attributes)
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer() || attributes == null)
            {
                return;
            }
            Delivered d;
            if (!delivered.TryGetValue(attributes.gameObject.GetInstanceID(), out d))
            {
                return;
            }

            int price = d.Order.Remaining;
            Refs.ItemValue(attributes) = price;
            if (GameManager.Multiplayer())
            {
                PhotonView view = attributes.GetComponent<PhotonView>();
                if (view != null && view.ViewID != 0)
                {
                    view.RPC("GetValueRPC", RpcTarget.Others, price);
                }
            }
        }

        // Called right after the game charges for an item. If the ordered copy is what was bought, the visit's record of it is dropped.
        internal static void OnPurchase(string itemName)
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer() || delivered.Count == 0)
            {
                return;
            }

            foreach (ItemAttributes attributes in Refs.ShoppingList(ShopManager.instance))
            {
                if (attributes != null && attributes.item != null && attributes.item.name == itemName
                    && delivered.Remove(attributes.gameObject.GetInstanceID()))
                {
                    Plugin.Log.LogInfo("Order collected and paid: " + itemName + ".");
                    Net.BroadcastNeutral();
                    ClientState.Apply(Master.Build(ResultCode.None, ""));
                    return;
                }
            }
        }
    }
}
