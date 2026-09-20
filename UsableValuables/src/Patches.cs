using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace UsableValuables
{
    // Photon's client only exists safely once the game is up, so hook it the same way REPOLib does.
    [HarmonyPatch(typeof(RunManager), "Awake")]
    internal static class RunManagerAwakePatch
    {
        private static void Postfix()
        {
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

    // The flashlight, the boombox, the ice saw, the blender, the jackhammer and the scream doll have no switch: they run
    // themselves from PhysGrabObject.grabbed - on when it is true, off when it is false, each with all of the game's sounds,
    // animation, flicker and fade (the powered ones decide on the host and tell everybody with an RPC). So "switched off" is done
    // by letting them see "not grabbed" for the length of the one Update / FixedUpdate that makes that decision, and putting the
    // real value back straight afterwards. Nothing else ever sees it.
    internal static class Mask
    {
        // Returns true if grabbed was hidden (and has to be restored).
        internal static bool Begin(Component owner, PhysGrabObject body)
        {
            if (body == null || !State.IsOff(owner))
            {
                return false;
            }
            if (!body.grabbed)
            {
                // Let go of: the next time it is picked up it behaves as usual again.
                State.SetOff(owner, false);
                return false;
            }
            body.grabbed = false;
            return true;
        }

        internal static void End(PhysGrabObject body)
        {
            if (body != null)
            {
                body.grabbed = true;
            }
        }
    }

    // Applies that mask around the methods that read grabbed. One prefix/finalizer pair serves every target; the table says
    // where each valuable keeps its reference to its physics body.
    internal static class MaskPatches
    {
        private static readonly Dictionary<Type, Func<Component, PhysGrabObject>> bodies = new Dictionary<Type, Func<Component, PhysGrabObject>>();

        internal static void Apply(Harmony harmony)
        {
            Func<Component, PhysGrabObject> trapBody = c => Refs.TrapBody((Trap)c);

            Add(harmony, typeof(ValuableFlashlight), "Update", c => Refs.FlashlightBody((ValuableFlashlight)c));
            Add(harmony, typeof(ValuableBoombox), "Update", trapBody);
            // The ice saw and the blender decide in FixedUpdate; their Update also runs the trap's own timer, which only
            // counts while the valuable is held.
            Add(harmony, typeof(IceSawValuable), "Update", trapBody);
            Add(harmony, typeof(IceSawValuable), "FixedUpdate", trapBody);
            Add(harmony, typeof(BlenderValuable), "Update", trapBody);
            Add(harmony, typeof(BlenderValuable), "FixedUpdate", trapBody);
            Add(harmony, typeof(JackhammerValuable), "Update", c => Refs.JackhammerBody((JackhammerValuable)c));
            Add(harmony, typeof(ScreamDollValuable), "FixedUpdate", c => Refs.ScreamDollBody((ScreamDollValuable)c));
        }

        private static void Add(Harmony harmony, Type type, string method, Func<Component, PhysGrabObject> body)
        {
            MethodInfo original = AccessTools.DeclaredMethod(type, method);
            if (original == null)
            {
                Plugin.Log.LogError("Could not find " + type.Name + "." + method + "; that valuable can't be switched off.");
                return;
            }
            bodies[type] = body;
            try
            {
                harmony.Patch(original,
                    prefix: new HarmonyMethod(typeof(MaskPatches), nameof(Prefix)),
                    finalizer: new HarmonyMethod(typeof(MaskPatches), nameof(Finalizer)));
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Patching " + type.Name + "." + method + " failed: " + ex);
            }
        }

        private static PhysGrabObject BodyOf(Component owner)
        {
            Func<Component, PhysGrabObject> body;
            return owner != null && bodies.TryGetValue(owner.GetType(), out body) ? body(owner) : null;
        }

        private static void Prefix(Component __instance, out bool __state)
        {
            __state = Mask.Begin(__instance, BodyOf(__instance));
        }

        private static void Finalizer(Component __instance, bool __state)
        {
            if (__state)
            {
                Mask.End(BodyOf(__instance));
            }
        }
    }

    // A knock makes the flashlight flicker back on while it is held; one that was switched off stays off.
    [HarmonyPatch(typeof(ValuableFlashlight), nameof(ValuableFlashlight.ImpactFlicker))]
    internal static class FlashlightFlickerPatch
    {
        private static bool Prefix(ValuableFlashlight __instance)
        {
            return !State.IsOff(__instance);
        }
    }
}
