using System;

namespace SpecialOrders
{
    internal sealed class OrderEntry
    {
        public string Key;
        public int Price;
        public int Deposit;

        public int Remaining
        {
            get { return Math.Max(1, Price - Deposit); }
        }
    }

    internal enum ItemState : byte
    {
        Available = 0,
        Ordered = 1,
        InStock = 2,
        MaxOwned = 3,
        MaxPurchased = 4,
        NeedsPlayers = 5,
        Ready = 6,
    }

    internal enum RequestType
    {
        Sync = 0,
        Place = 1,
        Cancel = 2,
    }

    internal enum ResultCode
    {
        None = 0,
        Placed = 1,
        Cancelled = 2,
        NoMoney = 3,
        Unavailable = 4,
        NotFound = 5,
        NotInShop = 6,
    }

    // What the host tells every client: the full order listing plus the outcome of the last request.
    internal sealed class ListingSnapshot
    {
        public ResultCode Result;
        public string ResultKey = "";
        public int Money;
        public int MarkupPercent;
        public int DepositPercent;
        public int RefundPercent;
        public string[] Keys = new string[0];
        public int[] Prices = new int[0];
        public int[] Deposits = new int[0];
        public byte[] States = new byte[0];

        public int IndexOf(string key)
        {
            for (int i = 0; i < Keys.Length; i++)
            {
                if (Keys[i] == key)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
