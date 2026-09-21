using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace UsableValuables
{
    // Fast accessors for members the game marks internal/private.
    internal static class Refs
    {
        // What the local player is holding.
        internal static readonly AccessTools.FieldRef<PhysGrabber, PhysGrabObject> Grabbed =
            AccessTools.FieldRefAccess<PhysGrabber, PhysGrabObject>("grabbedPhysGrabObject");

        // Set to a delay while menus or the chat are open; the game's own item toggle checks it before reading the key.
        internal static readonly AccessTools.FieldRef<PlayerController, float> InputDisableTimer =
            AccessTools.FieldRefAccess<PlayerController, float>("InputDisableTimer");

        // The physics body each of the switchable valuables looks at to know whether it is being held.
        internal static readonly AccessTools.FieldRef<ValuableFlashlight, PhysGrabObject> FlashlightBody =
            AccessTools.FieldRefAccess<ValuableFlashlight, PhysGrabObject>("physGrabObject");

        internal static readonly AccessTools.FieldRef<Trap, PhysGrabObject> TrapBody =
            AccessTools.FieldRefAccess<Trap, PhysGrabObject>("physGrabObject");

        // The two "power-on" valuables that are not traps keep their own private reference.
        internal static readonly AccessTools.FieldRef<JackhammerValuable, PhysGrabObject> JackhammerBody =
            AccessTools.FieldRefAccess<JackhammerValuable, PhysGrabObject>("physGrabObject");

        internal static readonly AccessTools.FieldRef<ScreamDollValuable, PhysGrabObject> ScreamDollBody =
            AccessTools.FieldRefAccess<ScreamDollValuable, PhysGrabObject>("physGrabObject");

        // The flamethrower and the fire extinguisher share the same code: a trigger that starts the flames (or the spray) and
        // burns fuel until the valuable is empty. fuelCountdownActive is true exactly while the flames are on.
        internal static readonly AccessTools.FieldRef<FlamethrowerValuable, bool> FlamethrowerFiring =
            AccessTools.FieldRefAccess<FlamethrowerValuable, bool>("fuelCountdownActive");

        internal static readonly AccessTools.FieldRef<FlamethrowerValuable, FlamethrowerValuable.States> FlamethrowerState =
            AccessTools.FieldRefAccess<FlamethrowerValuable, FlamethrowerValuable.States>("currentState");

        internal static readonly AccessTools.FieldRef<FireExtinguisherValuable, bool> ExtinguisherFiring =
            AccessTools.FieldRefAccess<FireExtinguisherValuable, bool>("fuelCountdownActive");

        internal static readonly AccessTools.FieldRef<FireExtinguisherValuable, FireExtinguisherValuable.States> ExtinguisherState =
            AccessTools.FieldRefAccess<FireExtinguisherValuable, FireExtinguisherValuable.States>("currentState");

        // Candle flame lights: the light manager fades a light towards originalIntensity, so that is what "off" has to change.
        internal static readonly AccessTools.FieldRef<PropLight, float> OriginalIntensity =
            AccessTools.FieldRefAccess<PropLight, float>("originalIntensity");

        internal static readonly AccessTools.FieldRef<PropLight, Light> LightComponent =
            AccessTools.FieldRefAccess<PropLight, Light>("lightComponent");

        // Star wand: the private spell and the swing bookkeeping the game itself resets after a cast.
        internal static readonly MethodInfo WandCastSpell =
            AccessTools.Method(typeof(ValuableStarWand), "CastSpell", new[] { typeof(bool) });

        internal static readonly AccessTools.FieldRef<ValuableStarWand, float> WandShootTimer =
            AccessTools.FieldRefAccess<ValuableStarWand, float>("shootTimer");

        internal static readonly AccessTools.FieldRef<ValuableStarWand, bool> WandCanShoot =
            AccessTools.FieldRefAccess<ValuableStarWand, bool>("canShoot");

        internal static readonly AccessTools.FieldRef<ValuableStarWand, bool> WandReadyToShoot =
            AccessTools.FieldRefAccess<ValuableStarWand, bool>("readyToShoot");

        // What the game runs when the dice say a held trap goes off: in multiplayer only the host acts (it sends the RPC that
        // sets trapStart on everybody), in singleplayer it acts directly, and the holder's screen glitches.
        internal static readonly MethodInfo TrapActivateSync =
            AccessTools.Method(typeof(Trap), "TrapActivateSync")
            ?? throw new MissingMethodException("Trap.TrapActivateSync");

        // Every physics body of the round: the game adds each PhysGrabObject here when it is enabled.
        internal static readonly AccessTools.FieldRef<RoundDirector, List<PhysGrabObject>> RoundBodies =
            AccessTools.FieldRefAccess<RoundDirector, List<PhysGrabObject>>("physGrabObjects");
    }
}
