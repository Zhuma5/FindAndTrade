using System;
using System.Collections.Generic;
using System.Linq;
using MGAutoSell.Caravans;
using MGAutoSell.Filter;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace MGAutoSell
{
    public record TradeRoute(Settlement Settlement, float Distance, float Silver, float FuelRequirement, SettlementTradePriority Priority);

    public enum SettlementTradePriority
    {
        Backup,
        Low,
        Medium,
        High
    }
    public class TradeRulesGameComp : GameComponent
    {
        public TradeRulesGroup tradeRules = new();
        public HashSet<ITrader> traders = new();

        public bool autoTrade = false;
        public HashSet<int> autoTraderIDs = new();

        public Dictionary<Map, ItemsToSell> SellCache = [];
        public Dictionary<Map, Pawn> SellerOverride = [];
        public Dictionary<Map, TradeRoute> NextTradeRoute = [];

        private Queue<Map> cacheOrder = new();
        private int tickWait => 2500 / UnityEngine.Mathf.Max(SellCache.Count, 1);
        private int nextTick = -1;

        public TradeRulesGameComp(Game game)
        {
            tradeRules ??= new();
            traders ??= new();
        }

        public override void FinalizeInit()
        {
            LongEventHandler.ExecuteWhenFinished(() => tradeRules.GetPossibleItemsList([]));
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                foreach (var map in Find.Maps)
                {
                    var cache = CacheUtility.Cache(this, map, out _);
                    SellCache[map] = cache;
                    cacheOrder.Enqueue(map);
                }
            });
        }

        public ItemsToSell Fetch(Map map, bool refresh = false)
        {
            if (!refresh && SellCache.TryGetValue(map, out var cache))
                return cache;

            cache = CacheUtility.Cache(this, map, out _);
            SellCache[map] = cache;
            return cache;
        }

        public override void GameComponentTick()
        {
            if (nextTick > Find.TickManager.TicksGame)
                return;

            nextTick = Find.TickManager.TicksGame + tickWait;

            if (!cacheOrder.Any())
                return;

            var map = cacheOrder.Dequeue();

            if (map == null)
                return;


            try
            {
                SellerOverride.TryGetValue(map, out var seller);
                var cache = CacheUtility.Cache(this, map, out _, seller);
                SellCache[map] = cache;

                if (ModsConfig.OdysseyActive && Settings.FF_Shuttles)
                {
                    var comp = Find.World.GetComponent<SettlementTracker>();
                    var shuttle = comp.GetBestForMap(map);

                    var tradeRoute = GetTradeRoute(shuttle, cache, comp);
                    if (tradeRoute != null)
                    {
                        NextTradeRoute[map] = tradeRoute;
                    }
                }
            }
            finally
            {
                cacheOrder.Enqueue(map);
            }
        }

        private TradeRoute GetTradeRoute(Building_PassengerShuttle shuttle, ItemsToSell cache, SettlementTracker comp)
        {
            if (shuttle == null)
                return null;
            var minFuel = shuttle.LaunchableComp.Props.minFuelCost;

            if (shuttle.FuelLevel < minFuel + 5)
                return null;

            var total = cache.TotalSilver.Value;
            if (total < shuttle.LaunchableComp.Props.minFuelCost * 4)
                return null;

            var fuelWithBuffer = shuttle.FuelLevel - 5;

            var distances = comp.GetDistances(shuttle.Map,
                maxDistance: layer =>
                {
                    var maxDistance = shuttle.LaunchableComp.MaxLaunchDistanceAtFuelLevel(fuelWithBuffer, layer);
                    if (shuttle.LaunchableComp.Props.fixedLaunchDistanceMax >= 0)
                        maxDistance = Mathf.Min(maxDistance, shuttle.LaunchableComp.Props.fixedLaunchDistanceMax);
                    return maxDistance;
                },
                settlementPredicate: settlement => 
                    traders.Contains(settlement) || 
                    settlement.TradeCurrency == TradeCurrency.Favor || 
                    settlement.trader.StockListForReading.FirstOrDefault(x => x.def == ThingDefOf.Silver)?.stackCount < 200);

            if (!distances.Any())
                return null;

            var routes = new List<TradeRoute>();
            foreach (var (settlement, distance) in distances)
            {
                var traderTotal =
                    cache.Items.Sum(y => settlement.trader.TraderKind.WillTrade(y.Item) ? y.Total.Value : 0);
                var traderSilver = settlement.trader.StockListForReading.FirstOrDefault(x => x.def == ThingDefOf.Silver)
                    ?.stackCount ?? 0f;
                var sales = traderTotal > traderSilver ? traderSilver : total;

                var fuelCost = shuttle.LaunchableComp.FuelNeededToLaunchAtDist(distance, settlement.Tile.Layer);

                var ratio = sales / fuelCost;

                var priority = ratio switch
                {
                    > 4 when traderSilver > sales => SettlementTradePriority.High,
                    >= 4 => SettlementTradePriority.Medium,
                    > 2 => SettlementTradePriority.Low,
                    _ => SettlementTradePriority.Backup
                };

                routes.Add(new TradeRoute(settlement, distance, sales, fuelCost, priority));
            }

            routes = [.. routes
                .OrderByDescending(x => x.Priority)
                .ThenByDescending(x => x.Silver)];

            return routes.FirstOrDefault();
        }

        public override void ExposeData()
        {
            Scribe_Deep.Look(ref tradeRules, nameof(tradeRules));
            Scribe_Collections.Look(ref traders, nameof(traders), LookMode.Reference);

            Scribe_Values.Look(ref autoTrade, nameof(autoTrade));
            Scribe_Collections.Look(ref autoTraderIDs, nameof(autoTraderIDs));

            tradeRules ??= new();
            traders ??= new();
            autoTraderIDs ??= new();
        }
    }
}
