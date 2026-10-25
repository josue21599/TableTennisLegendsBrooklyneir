using System;
using Brooklyneir.Config;
using Brooklyneir.Rackets;
using Brooklyneir.SaveSystem;
using Brooklyneir.Tables;

namespace Brooklyneir.Progression
{
    public enum PurchaseResult { Success, AlreadyOwned, NotEnoughCoins, NotForSale, UnknownItem }

    /// <summary>
    /// Local, offline economy. All balance changes go through here, so the balance can never go
    /// negative or above the cap, and every change raises <see cref="CoinsChanged"/> for the UI.
    /// No real money, ads or accounts.
    /// </summary>
    public class Economy
    {
        readonly SaveData _data;
        readonly EconomyTuning _t;

        public event Action<int, int> CoinsChanged; // new balance, delta
        public event Action<string> ItemUnlocked;   // id

        public Economy(SaveData data, EconomyTuning tuning)
        {
            _data = data;
            _t = tuning;
        }

        public int Coins { get { return _data.coins; } }

        public void AddCoins(int amount)
        {
            if (amount <= 0) return;
            long next = (long)_data.coins + amount;
            if (next > _t.maxCoins) next = _t.maxCoins;
            int delta = (int)next - _data.coins;
            _data.coins = (int)next;
            _data.stats.coinsEarned += delta;
            if (CoinsChanged != null) CoinsChanged(_data.coins, delta);
        }

        public bool CanAfford(int price)
        {
            return price >= 0 && _data.coins >= price;
        }

        bool Spend(int price)
        {
            if (price < 0 || _data.coins < price) return false;
            _data.coins -= price;
            if (CoinsChanged != null) CoinsChanged(_data.coins, -price);
            return true;
        }

        public bool OwnsRacket(string id) { return _data.ownedRackets.Contains(id); }
        public bool OwnsTable(string id) { return _data.ownedTables.Contains(id); }

        public PurchaseResult BuyRacket(string id)
        {
            if (!RacketCatalog.Exists(id)) return PurchaseResult.UnknownItem;
            RacketDefinition def = RacketCatalog.Get(id);
            if (OwnsRacket(id)) return PurchaseResult.AlreadyOwned;
            if (def.Unlock != UnlockRule.Coins) return PurchaseResult.NotForSale;
            if (!Spend(def.Price)) return PurchaseResult.NotEnoughCoins;
            _data.ownedRackets.Add(id);
            if (ItemUnlocked != null) ItemUnlocked(id);
            return PurchaseResult.Success;
        }

        public PurchaseResult BuyTable(string id)
        {
            if (!TableCatalog.Exists(id)) return PurchaseResult.UnknownItem;
            TableDefinition def = TableCatalog.Get(id);
            if (OwnsTable(id)) return PurchaseResult.AlreadyOwned;
            if (def.Unlock != UnlockRule.Coins) return PurchaseResult.NotForSale;
            if (!Spend(def.Price)) return PurchaseResult.NotEnoughCoins;
            _data.ownedTables.Add(id);
            if (ItemUnlocked != null) ItemUnlocked(id);
            return PurchaseResult.Success;
        }

        /// <summary>Grants an item without payment (tournament prizes). Returns false if already owned.</summary>
        public bool GrantTable(string id)
        {
            if (!TableCatalog.Exists(id) || OwnsTable(id)) return false;
            _data.ownedTables.Add(id);
            if (ItemUnlocked != null) ItemUnlocked(id);
            return true;
        }

        public bool GrantRacket(string id)
        {
            if (!RacketCatalog.Exists(id) || OwnsRacket(id)) return false;
            _data.ownedRackets.Add(id);
            if (ItemUnlocked != null) ItemUnlocked(id);
            return true;
        }

        public int RewardForCpuWin(int difficulty)
        {
            switch (difficulty)
            {
                case 0: return _t.winVsEasy;
                case 2: return _t.winVsHard;
                default: return _t.winVsNormal;
            }
        }

        public EconomyTuning Tuning { get { return _t; } }
    }
}
