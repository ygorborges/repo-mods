using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace MoonControl
{
    // Fast accessors for members the game marks internal/private.
    internal static class Refs
    {
        // The moon level the game itself would have computed from levelsCompleted, and the flag that tells the moon's own
        // banner/sound (MoonUI) that it just changed - setting it ourselves makes an applied moon look and sound exactly like
        // the game's own moon change.
        internal static readonly AccessTools.FieldRef<RunManager, int> MoonLevel =
            AccessTools.FieldRefAccess<RunManager, int>("moonLevel");

        internal static readonly AccessTools.FieldRef<RunManager, bool> MoonLevelChanged =
            AccessTools.FieldRefAccess<RunManager, bool>("moonLevelChanged");

        // The same per-run storage SpecialOrders keeps its order book in: the game saves and loads it with the run, and
        // wipes it itself on a new run (ResetAllStats) - so the level applied here needs no reset code of its own.
        internal static readonly AccessTools.FieldRef<StatsManager, SortedDictionary<string, Dictionary<string, int>>> StatDictionaries =
            AccessTools.FieldRefAccess<StatsManager, SortedDictionary<string, Dictionary<string, int>>>("dictionaryOfDictionaries");

        // Where a MenuLib popup page will finish sliding in to, and its own rect - used to work out how far off-centre it
        // ended up so it can be shifted to the middle of the screen (same technique SpecialOrders' order list uses).
        internal static readonly AccessTools.FieldRef<MenuPage, Vector2> PageOriginalPosition =
            AccessTools.FieldRefAccess<MenuPage, Vector2>("originalPosition");

        internal static readonly AccessTools.FieldRef<MenuPage, RectTransform> PageRect =
            AccessTools.FieldRefAccess<MenuPage, RectTransform>("rectTransform");

        // Whether a menu page is already open (so ours does not fight the pause menu, the map, etc.), the same way
        // SpecialOrders reads it.
        internal static readonly AccessTools.FieldRef<MenuManager, MenuPage> CurrentMenuPage =
            AccessTools.FieldRefAccess<MenuManager, MenuPage>("currentMenuPage");

        // A valuable's price, after the game rolls it (or takes a fixed override) - what the bonus for the applied moon
        // level multiplies.
        internal static readonly AccessTools.FieldRef<ValuableObject, float> DollarValueOriginal =
            AccessTools.FieldRefAccess<ValuableObject, float>("dollarValueOriginal");

        internal static readonly AccessTools.FieldRef<ValuableObject, float> DollarValueCurrent =
            AccessTools.FieldRefAccess<ValuableObject, float>("dollarValueCurrent");

        // Whether a valuable's price has already been rolled once - DollarValueSetLogic is idempotent by design (it checks
        // this itself and no-ops on a repeat call), but a Harmony postfix runs every time regardless of whether the original
        // body actually did anything; reading this beforehand is how the postfix tells a genuine roll from a harmless repeat.
        internal static readonly AccessTools.FieldRef<ValuableObject, bool> DollarValueSet =
            AccessTools.FieldRefAccess<ValuableObject, bool>("dollarValueSet");

        // The text under the "$X" that floats over a held valuable (WorldSpaceUIValue) - rewritten to show the pre-moon
        // base price plus the bonus in red instead of just the already-combined total.
        internal static readonly AccessTools.FieldRef<WorldSpaceUIValue, TMPro.TextMeshProUGUI> WorldSpaceUIText =
            AccessTools.FieldRefAccess<WorldSpaceUIValue, TMPro.TextMeshProUGUI>("text");

        // Whether the cursor is currently over a menu button (MenuButton is the game's own base type, not MenuLib's - the
        // same field SpecialOrders' order list reads to drive its preview panel from whichever row is under the mouse).
        internal static readonly AccessTools.FieldRef<MenuButton, bool> ButtonHovering =
            AccessTools.FieldRefAccess<MenuButton, bool>("hovering");

        // Whether the local player is dead/spectating - not a time to open the menu.
        internal static readonly AccessTools.FieldRef<PlayerAvatar, bool> PlayerDisabled =
            AccessTools.FieldRefAccess<PlayerAvatar, bool>("isDisabled");

        // What a StaticGrabObject-based prop is grabbed by holding, the same way the extraction point's own button - and
        // the truck's own controls - are. Set the moment the local player starts grabbing one, read locally, so this needs
        // no reflection into anything the network has to agree on.
        internal static readonly AccessTools.FieldRef<PhysGrabber, StaticGrabObject> GrabbedStaticGrabObject =
            AccessTools.FieldRefAccess<PhysGrabber, StaticGrabObject>("grabbedStaticGrabObject");
    }
}
