using System;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine.InputSystem;

namespace SpecialOrders
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency("nickklmao.menulib")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "vibez.SpecialOrders";
        public const string Name = "SpecialOrders";
        public const string Version = "0.1.5";

        internal static Plugin Instance;
        internal static ManualLogSource Log;

        internal static ConfigEntry<int> MarkupPercent;
        internal static ConfigEntry<int> DepositPercent;
        internal static ConfigEntry<int> CancelRefundPercent;
        internal static ConfigEntry<float> InteractDistance;
        internal static ConfigEntry<bool> ShowItemPreview;
        internal static ConfigEntry<float> PreviewBrightness;
        internal static ConfigEntry<Key> DebugHotkey;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            MarkupPercent = Config.Bind("Pricing", "MarkupPercent", 50,
                new ConfigDescription("How much more an ordered item costs than its normal shop price, in percent. Only the host's value is used.",
                    new AcceptableValueRange<int>(0, 500)));
            DepositPercent = Config.Bind("Pricing", "DepositPercent", 25,
                new ConfigDescription("Share of the order price paid up front when the order is placed. It is deducted from the price when the item arrives, and it is lost if the item is not bought on arrival. Only the host's value is used.",
                    new AcceptableValueRange<int>(0, 100)));
            CancelRefundPercent = Config.Bind("Pricing", "CancelRefundPercent", 100,
                new ConfigDescription("Share of the deposit that is given back when an order is cancelled. Only the host's value is used.",
                    new AcceptableValueRange<int>(0, 100)));
            InteractDistance = Config.Bind("General", "InteractDistance", 2f,
                new ConfigDescription("How close, in meters, you have to be to the shopkeeper's body (not its center) to place orders. You also have to look at it.",
                    new AcceptableValueRange<float>(0.5f, 8f)));
            PreviewBrightness = Config.Bind("General", "PreviewBrightness", 0.8f,
                new ConfigDescription("Brightness of the item preview lights. Lower it if the preview looks washed out.",
                    new AcceptableValueRange<float>(0.2f, 2f)));
            ShowItemPreview = Config.Bind("General", "ShowItemPreview", true,
                "Shows a rotating 3D preview of the selected item next to the order list. Turn it off if it causes trouble.");
            DebugHotkey = Config.Bind("Debug", "DebugHotkey", Key.None,
                "For testing only: opens the order menu from anywhere in the shop, ignoring distance and whether the shopkeeper is awake.");

            try
            {
                RuntimeHelpers.RunClassConstructor(typeof(Refs).TypeHandle);
            }
            catch (Exception ex)
            {
                Log.LogError("The game changed in a way this version of " + Name + " does not understand; it will not work: " + ex);
                return;
            }

            Harmony harmony = new Harmony(Guid);
            foreach (Type type in typeof(Plugin).Assembly.GetTypes())
            {
                if (type.GetCustomAttributes(typeof(HarmonyPatch), false).Length == 0)
                {
                    continue;
                }
                try
                {
                    harmony.CreateClassProcessor(type).Patch();
                }
                catch (Exception ex)
                {
                    Log.LogError("Patch " + type.Name + " failed: " + ex);
                }
            }

            Log.LogInfo(Name + " " + Version + " loaded.");
        }

        private void Update()
        {
            Key key = DebugHotkey.Value;
            if (key == Key.None)
            {
                return;
            }
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[key].wasPressedThisFrame)
            {
                OrdersMenu.Open(true);
            }
        }
    }
}
