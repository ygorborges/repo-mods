using System;
using HarmonyLib;

namespace SpecialOrders
{
    // Photon's client only exists safely once the game is up, so hook it the same way REPOLib does.
    [HarmonyPatch(typeof(RunManager), "Awake")]
    internal static class RunManagerAwakePatch
    {
        private static bool initialized;

        private static void Postfix()
        {
            if (initialized)
            {
                return;
            }
            initialized = true;
            try
            {
                Net.Init();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Could not register the network handler: " + ex);
            }
        }
    }

    [HarmonyPatch(typeof(ShopKeeper), "Start")]
    internal static class ShopKeeperStartPatch
    {
        private static void Postfix(ShopKeeper __instance)
        {
            try
            {
                ShopkeeperTrigger.Attach(__instance);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Could not attach the order trigger to the shopkeeper: " + ex);
            }
        }
    }

    [HarmonyPatch(typeof(PunManager), nameof(PunManager.ShopPopulateItemVolumes))]
    internal static class ShopPopulatePatch
    {
        private static void Prefix()
        {
            try
            {
                Delivery.Run();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Delivering special orders failed: " + ex);
            }
        }
    }

    [HarmonyPatch(typeof(ItemAttributes), nameof(ItemAttributes.GetValue))]
    internal static class ItemValuePatch
    {
        private static void Postfix(ItemAttributes __instance)
        {
            try
            {
                Delivery.ApplyOrderPrice(__instance);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Pricing a special order failed: " + ex);
            }
        }
    }

    [HarmonyPatch(typeof(StatsManager), nameof(StatsManager.ItemPurchase))]
    internal static class ItemPurchasePatch
    {
        private static void Postfix(string itemName)
        {
            try
            {
                Delivery.OnPurchase(itemName);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Closing a special order failed: " + ex);
            }
        }
    }
}
