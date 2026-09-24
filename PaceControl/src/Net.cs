using System;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;

namespace PaceControl
{
    // The host decides how many extraction points a level needs and tells everyone, because every machine works some of this
    // out for itself from that same number: the truck's healer opens purely locally when the count is reached (no RPC of its
    // own anywhere in it), and the HUD counter is drawn locally too. Leaving clients on the real count would mean the level
    // ending for everyone while their own truck never opened to heal them.
    //
    // Raw Photon event, the same way the other mods in this collection do it - code 201, clear of SpecialOrders (171-173),
    // UsableValuables (181-183) and MoonControl (191-192), so they can all share a room.
    //
    //   needed: host -> all   [extraction points this level needs, extraction points it built]
    internal static class Net
    {
        private const byte EvNeeded = 201;

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

        internal static void Announce(int needed, int built)
        {
            PaceState.Apply(needed, built);
            if (SemiFunc.IsMultiplayer() && SemiFunc.IsMasterClient())
            {
                PhotonNetwork.RaiseEvent(
                    EvNeeded,
                    new int[] { needed, built },
                    new RaiseEventOptions { Receivers = ReceiverGroup.Others },
                    SendOptions.SendReliable);
            }
        }

        private static void OnEvent(EventData data)
        {
            try
            {
                if (data.Code != EvNeeded)
                {
                    return;
                }
                // Only the host gets to say so.
                if (PhotonNetwork.MasterClient == null || data.Sender != PhotonNetwork.MasterClient.ActorNumber)
                {
                    return;
                }
                int[] payload = (int[])data.CustomData;
                PaceState.Apply(payload[0], payload.Length > 1 ? payload[1] : payload[0]);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Handling a pace-control network event failed: " + ex);
            }
        }
    }
}
