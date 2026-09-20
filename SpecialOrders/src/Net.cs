using System;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;

namespace SpecialOrders
{
    internal static class ClientState
    {
        internal static ListingSnapshot Current;
        internal static event Action<ListingSnapshot> Changed;

        internal static void Apply(ListingSnapshot snapshot)
        {
            Current = snapshot;
            Action<ListingSnapshot> handler = Changed;
            if (handler != null)
            {
                handler(snapshot);
            }
        }
    }

    // Client -> host requests and host -> client answers over raw Photon events, so no extra library is needed.
    // Singleplayer takes the same path without touching the network.
    internal static class Net
    {
        private const byte EvRequest = 171;
        private const byte EvSnapshot = 172;
        private const byte EvDelivered = 173;

        internal static void Init()
        {
            PhotonNetwork.NetworkingClient.EventReceived += OnEvent;
        }

        internal static void Request(RequestType type, string key)
        {
            if (SemiFunc.IsMasterClientOrSingleplayer())
            {
                ResultCode result = Master.Execute(type, key);
                if (IsChange(result))
                {
                    BroadcastNeutral();
                }
                ClientState.Apply(Master.Build(result, key));
                return;
            }

            PhotonNetwork.RaiseEvent(
                EvRequest,
                new object[] { (int)type, key ?? "" },
                new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient },
                SendOptions.SendReliable);
        }

        // Host only: something changed outside of a request (an order was fulfilled), so everyone gets fresh data.
        internal static void BroadcastNeutral()
        {
            if (!SemiFunc.IsMultiplayer())
            {
                return;
            }
            PhotonNetwork.RaiseEvent(
                EvSnapshot,
                Pack(Master.Build(ResultCode.None, "")),
                new RaiseEventOptions { Receivers = ReceiverGroup.Others },
                SendOptions.SendReliable);
        }

        internal static void AnnounceDelivered(string[] keys)
        {
            if (keys.Length == 0)
            {
                return;
            }
            if (SemiFunc.IsMultiplayer())
            {
                PhotonNetwork.RaiseEvent(
                    EvDelivered,
                    new object[] { keys },
                    new RaiseEventOptions { Receivers = ReceiverGroup.Others },
                    SendOptions.SendReliable);
            }
            Notifier.Arrived(keys);
        }

        private static bool IsChange(ResultCode result)
        {
            return result == ResultCode.Placed || result == ResultCode.Cancelled;
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
                    case EvSnapshot:
                        object[] snapshotContent = e.CustomData as object[];
                        if (snapshotContent != null)
                        {
                            ClientState.Apply(Unpack(snapshotContent));
                        }
                        break;
                    case EvDelivered:
                        object[] deliveredContent = e.CustomData as object[];
                        if (deliveredContent != null && deliveredContent.Length > 0)
                        {
                            Notifier.Arrived((string[])deliveredContent[0]);
                        }
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

            RequestType type = (RequestType)(int)content[0];
            string key = (string)content[1];
            ResultCode result = Master.Execute(type, key);

            if (IsChange(result))
            {
                BroadcastNeutral();
                ClientState.Apply(Master.Build(ResultCode.None, ""));
            }

            PhotonNetwork.RaiseEvent(
                EvSnapshot,
                Pack(Master.Build(result, key)),
                new RaiseEventOptions { TargetActors = new[] { e.Sender } },
                SendOptions.SendReliable);
        }

        private static object[] Pack(ListingSnapshot s)
        {
            return new object[]
            {
                (int)s.Result, s.ResultKey, s.Money, s.MarkupPercent, s.DepositPercent, s.RefundPercent,
                s.Keys, s.Prices, s.Deposits, s.States,
            };
        }

        private static ListingSnapshot Unpack(object[] c)
        {
            return new ListingSnapshot
            {
                Result = (ResultCode)(int)c[0],
                ResultKey = (string)c[1],
                Money = (int)c[2],
                MarkupPercent = (int)c[3],
                DepositPercent = (int)c[4],
                RefundPercent = (int)c[5],
                Keys = (string[])c[6],
                Prices = (int[])c[7],
                Deposits = (int[])c[8],
                States = (byte[])c[9],
            };
        }
    }
}
