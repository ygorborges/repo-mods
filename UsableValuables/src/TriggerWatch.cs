using UnityEngine;

namespace UsableValuables
{
    // In the game a flamethrower stops the moment the trigger is let go of. With the key the flames run until you press it
    // again (or the fuel is gone), so the host also stops them if the player lets go of the valuable while it is firing.
    internal static class TriggerWatch
    {
        // Starting the flames reaches everyone with a short delay in multiplayer; don't judge "it is not firing" before then.
        private const float Grace = 1.5f;

        internal static void Tick()
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer())
            {
                return;
            }

            foreach (State.WatchedTrigger item in State.WatchedTriggers())
            {
                bool firing = Kinds.TriggerFiring(item.Owner);
                bool settled = Time.time - item.StartedAt > Grace;

                PhysGrabObject body = item.Owner.GetComponentInParent<PhysGrabObject>();
                if (body != null && !body.grabbed)
                {
                    // Let go of while firing (or just after starting it): the flames stop.
                    State.Unwatch(item.Owner);
                    if (firing || !settled)
                    {
                        Plugin.Log.LogDebug(item.Kind + " was let go of while firing; stopping it.");
                        Net.Announce(body, item.Kind, true, Kinds.SwitchDelay);
                    }
                    continue;
                }

                if (!firing && settled)
                {
                    // Out of fuel, or stopped some other way: nothing left to watch.
                    State.Unwatch(item.Owner);
                }
            }
        }
    }
}
