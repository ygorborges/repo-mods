using UnityEngine;

namespace UsableValuables
{
    // The trap-like valuables (boombox, ice saw, blender, jackhammer, scream doll) don't stay switched off for good while you
    // are still holding them: each second there is a chance they switch themselves back on, the way traps in the game go off
    // by themselves. Only the host rolls the dice; the result goes out like any other use of the key.
    internal static class Reactivation
    {
        internal static void Tick()
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer())
            {
                return;
            }

            foreach (var due in State.TakeDue())
            {
                Component component = due.Key;
                Kinds.Info info = Kinds.Get(due.Value);
                if (component == null || info.Reactivate == null || !info.Enabled.Value)
                {
                    continue;
                }

                // Only while it is held; put down, it is simply left alone (and goes back to normal when picked up again).
                PhysGrabObject body = component.GetComponentInParent<PhysGrabObject>();
                if (body == null || !body.grabbed)
                {
                    continue;
                }

                if (Random.value * 100f < info.Reactivate.Value)
                {
                    Plugin.Log.LogDebug(due.Value + " switched itself back on.");
                    Net.Announce(body, due.Value, false, Kinds.SwitchDelay);
                }
            }
        }
    }
}
