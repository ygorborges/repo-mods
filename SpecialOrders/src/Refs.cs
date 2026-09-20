using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SpecialOrders
{
    // Fast accessors for members the game marks internal/private.
    internal static class Refs
    {
        internal static readonly AccessTools.FieldRef<StatsManager, SortedDictionary<string, Dictionary<string, int>>> StatDictionaries =
            AccessTools.FieldRefAccess<StatsManager, SortedDictionary<string, Dictionary<string, int>>>("dictionaryOfDictionaries");

        internal static readonly AccessTools.FieldRef<ShopManager, List<ItemAttributes>> ShoppingList =
            AccessTools.FieldRefAccess<ShopManager, List<ItemAttributes>>("shoppingList");

        internal static readonly AccessTools.FieldRef<ShopManager, float> ItemValueMultiplier =
            AccessTools.FieldRefAccess<ShopManager, float>("itemValueMultiplier");

        internal static readonly AccessTools.FieldRef<ItemAttributes, int> ItemValue =
            AccessTools.FieldRefAccess<ItemAttributes, int>("value");

        internal static readonly AccessTools.FieldRef<PlayerAvatar, bool> PlayerDisabled =
            AccessTools.FieldRefAccess<PlayerAvatar, bool>("isDisabled");

        internal static readonly AccessTools.FieldRef<MenuManager, MenuPage> CurrentMenuPage =
            AccessTools.FieldRefAccess<MenuManager, MenuPage>("currentMenuPage");

        internal static readonly AccessTools.FieldRef<MenuButton, bool> ButtonHovering =
            AccessTools.FieldRefAccess<MenuButton, bool>("hovering");

        // Where a menu page will finish sliding in to, and whether it has started (both are set in MenuPage.Start).
        internal static readonly AccessTools.FieldRef<MenuPage, Vector2> PageOriginalPosition =
            AccessTools.FieldRefAccess<MenuPage, Vector2>("originalPosition");

        internal static readonly AccessTools.FieldRef<MenuPage, RectTransform> PageRect =
            AccessTools.FieldRefAccess<MenuPage, RectTransform>("rectTransform");
    }
}
