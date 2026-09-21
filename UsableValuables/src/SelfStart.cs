using System.Collections.Generic;
using UnityEngine;

namespace UsableValuables
{
    // The one-shot traps (radio, gramophone, television, toy monkey, grandfather clock) go off by themselves while somebody
    // holds them: each second there is a chance, the way the dice the game rolls on held traps do. Only the host rolls; a
    // trap that goes off is set off through the game's own trigger, which tells everybody.
    internal static class SelfStart
    {
        private static float nextRoll;

        internal static void Tick()
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer())
            {
                return;
            }
            float now = Time.time;
            if (now < nextRoll)
            {
                return;
            }
            nextRoll = now + State.RollInterval;

            RoundDirector round = RoundDirector.instance;
            List<PhysGrabObject> bodies = round == null ? null : Refs.RoundBodies(round);
            if (bodies == null)
            {
                return;
            }

            // Only the few bodies somebody is holding right now are looked at any further.
            for (int i = 0; i < bodies.Count; i++)
            {
                PhysGrabObject body = bodies[i];
                if (body == null || !body.grabbed)
                {
                    continue;
                }

                KindId id;
                Component component;
                if (!Kinds.TryResolve(body, out id, out component))
                {
                    continue;
                }
                Kinds.Info info = Kinds.Get(id);
                if (!info.OneShot || !info.Enabled.Value || Kinds.TrapSpent(component))
                {
                    continue;
                }

                if (Random.value * 100f < info.SelfStart.Value)
                {
                    Plugin.Log.LogDebug(id + " went off by itself.");
                    Kinds.Apply(id, component, false);
                }
            }
        }
    }
}
