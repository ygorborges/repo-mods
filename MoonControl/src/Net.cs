using System;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;

namespace MoonControl
{
    // Only the host can change what moon is applied - everyone else's copy of the menu is informational only (Moon Menu
    // does not even wire up its rows to Request/RequestRandom for a non-host viewer, and this is the backstop in case
    // something ever called it anyway). So there is only one direction of network traffic: the host tells everyone what is
    // now in effect. Raw Photon event, the same way UsableValuables and SpecialOrders do it - code 192, outside both of
    // their ranges (171-173 and 181-183), so all three mods can run together in the same room without clashing.
    //
    //   applied: host -> all   [level, randomMode]
    internal static class Net
    {
        private const byte EvApplied = 192;

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

        // The host picked a specific moon in the menu - leaves Random mode (if it was on).
        internal static void Request(int level)
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer())
            {
                return;
            }
            RunManager run = RunManager.instance;
            int max = MoonState.Unlocked(run);
            int clamped = level < 0 ? 0 : level > max ? max : level;
            Broadcast(clamped, false);
        }

        // The host picked "Random" - what is actually applied does not change yet (RollRandom decides that, right as
        // each level starts), only the strategy does.
        internal static void RequestRandom()
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer())
            {
                return;
            }
            Broadcast(MoonState.Applied, true);
        }

        private static void Broadcast(int level, bool randomMode)
        {
            if (SemiFunc.IsMultiplayer())
            {
                PhotonNetwork.RaiseEvent(
                    EvApplied,
                    new int[] { level, randomMode ? 1 : 0 },
                    new RaiseEventOptions { Receivers = ReceiverGroup.All },
                    SendOptions.SendReliable);
            }
            else
            {
                MoonState.SetApplied(level);
                MoonState.SetRandomMode(randomMode);
            }
        }

        private static void OnEvent(EventData data)
        {
            try
            {
                if (data.Code == EvApplied)
                {
                    int[] payload = (int[])data.CustomData;
                    MoonState.SetApplied(payload[0]);
                    MoonState.SetRandomMode(payload.Length > 1 && payload[1] != 0);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Handling a moon-control network event failed: " + ex);
            }
        }
    }
}
