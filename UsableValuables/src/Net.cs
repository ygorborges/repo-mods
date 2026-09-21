using System;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace UsableValuables
{
    // The player who holds a valuable asks the host; the host checks and tells everyone (itself included) to apply it.
    // Raw Photon events, so no extra library is needed. Singleplayer takes the same path without the network.
    //
    //   request:  client -> host   [viewId, kind]
    //   apply:    host   -> all    [viewId, kind, off, cooldown]
    //   quirk:    host   -> all    [viewId, quirk, phase, x, y, z, size, damage, enemyDamage, extra1, extra2]   (see Mischief)
    internal static class Net
    {
        private const byte EvRequest = 181;
        private const byte EvApply = 182;
        private const byte EvQuirk = 183;

        private static bool initialized;

        internal static void Init()
        {
            if (initialized)
            {
                return;
            }
            initialized = true;
            PhotonNetwork.NetworkingClient.EventReceived += OnEvent;
        }

        // The local player pressed the key on the valuable they hold.
        internal static void Use(PhysGrabObject body, KindId id)
        {
            if (!SemiFunc.IsMultiplayer())
            {
                Host.Handle(body, id, -1);
                return;
            }

            PhotonView view = body.GetComponent<PhotonView>();
            if (view == null || view.ViewID == 0)
            {
                return;
            }
            if (SemiFunc.IsMasterClientOrSingleplayer())
            {
                Host.Handle(body, id, PhotonNetwork.LocalPlayer.ActorNumber);
                return;
            }
            PhotonNetwork.RaiseEvent(
                EvRequest,
                new object[] { view.ViewID, (byte)id },
                new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient },
                SendOptions.SendReliable);
        }

        // Host only.
        internal static void Announce(PhysGrabObject body, KindId id, bool off, float cooldown)
        {
            if (!SemiFunc.IsMultiplayer())
            {
                Apply(body, id, off, cooldown);
                return;
            }

            PhotonView view = body.GetComponent<PhotonView>();
            if (view == null || view.ViewID == 0)
            {
                return;
            }
            PhotonNetwork.RaiseEvent(
                EvApply,
                new object[] { view.ViewID, (byte)id, off, cooldown },
                new RaiseEventOptions { Receivers = ReceiverGroup.All },
                SendOptions.SendReliable);
        }

        // Host only: a valuable is acting up (see Mischief). Everybody, the host included, plays it out.
        internal static void AnnounceQuirk(PhysGrabObject body, Quirk quirk, QuirkPhase phase, float size = 0f, int damage = 0, int enemyDamage = 0,
            float extra1 = 0f, float extra2 = 0f)
        {
            Vector3 position = body.centerPoint;
            if (!SemiFunc.IsMultiplayer())
            {
                Mischief.Handle(body, quirk, phase, position, size, damage, enemyDamage, extra1, extra2);
                return;
            }

            PhotonView view = body.GetComponent<PhotonView>();
            if (view == null || view.ViewID == 0)
            {
                return;
            }
            PhotonNetwork.RaiseEvent(
                EvQuirk,
                new object[] { view.ViewID, (byte)quirk, (byte)phase, position.x, position.y, position.z, size, damage, enemyDamage, extra1, extra2 },
                new RaiseEventOptions { Receivers = ReceiverGroup.All },
                SendOptions.SendReliable);
        }

        private static void Apply(PhysGrabObject body, KindId id, bool off, float cooldown)
        {
            KindId actual;
            Component component;
            if (!Kinds.TryResolve(body, out actual, out component) || actual != id)
            {
                return;
            }
            State.SetCooldown(component, cooldown);
            Kinds.Apply(id, component, off);
        }

        private static void OnEvent(EventData e)
        {
            try
            {
                switch (e.Code)
                {
                    case EvRequest:
                        OnRequest(e);
                        break;
                    case EvApply:
                        OnApply(e);
                        break;
                    case EvQuirk:
                        Mischief.OnEvent(e);
                        break;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Failed to handle network event " + e.Code + ": " + ex);
            }
        }

        private static void OnRequest(EventData e)
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer())
            {
                return;
            }
            object[] content = e.CustomData as object[];
            if (content == null || content.Length < 2)
            {
                return;
            }
            PhotonView view = PhotonView.Find((int)content[0]);
            PhysGrabObject body = view == null ? null : view.GetComponent<PhysGrabObject>();
            if (body != null)
            {
                Host.Handle(body, (KindId)(byte)content[1], e.Sender);
            }
        }

        private static void OnApply(EventData e)
        {
            // Only the host decides what happens; anything else is ignored.
            Player master = PhotonNetwork.MasterClient;
            if (master == null || e.Sender != master.ActorNumber)
            {
                return;
            }
            object[] content = e.CustomData as object[];
            if (content == null || content.Length < 4)
            {
                return;
            }
            PhotonView view = PhotonView.Find((int)content[0]);
            PhysGrabObject body = view == null ? null : view.GetComponent<PhysGrabObject>();
            if (body != null)
            {
                Apply(body, (KindId)(byte)content[1], (bool)content[2], (float)content[3]);
            }
        }
    }

    // The rules, run on the host (or in singleplayer): is this valuable usable, is the sender really holding it, is it ready.
    internal static class Host
    {
        // sender = the Photon actor number of whoever pressed the key, or -1 in singleplayer.
        internal static void Handle(PhysGrabObject body, KindId id, int sender)
        {
            Kinds.Info info = Kinds.Get(id);
            if (!info.Enabled.Value)
            {
                return;
            }

            KindId actual;
            Component component;
            if (!Kinds.TryResolve(body, out actual, out component) || actual != id)
            {
                return;
            }
            if (sender >= 0 && !IsHeldBy(body, sender))
            {
                Plugin.Log.LogDebug("Ignored a request for " + id + ": the sender is not holding it.");
                return;
            }
            if (State.Remaining(component) > 0f)
            {
                return;
            }

            // A one-shot trap: the game's own trigger does the rest (it tells everybody), so there is nothing to announce here.
            if (info.OneShot)
            {
                if (Kinds.TrapSpent(component))
                {
                    return;
                }
                State.SetCooldown(component, Kinds.SwitchDelay);
                Kinds.Apply(id, component, false);
                return;
            }

            // "off" is what the key does to a switch (turn it off) or a trigger (let it go, because it is firing now).
            bool off = info.Trigger ? Kinds.TriggerFiring(component) : info.Switch && !State.IsOff(component);
            float cooldown = info.Switch || info.Trigger ? Kinds.SwitchDelay : info.Cooldown.Value;
            Net.Announce(body, id, off, cooldown);
        }

        private static bool IsHeldBy(PhysGrabObject body, int actor)
        {
            foreach (PhysGrabber grabber in body.playerGrabbing)
            {
                if (grabber == null)
                {
                    continue;
                }
                // Each grabber belongs to a player; its own view, or failing that its avatar's, tells whose.
                PhotonView view = grabber.photonView != null
                    ? grabber.photonView
                    : (grabber.playerAvatar != null ? grabber.playerAvatar.photonView : null);
                if (view != null && view.OwnerActorNr == actor)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
