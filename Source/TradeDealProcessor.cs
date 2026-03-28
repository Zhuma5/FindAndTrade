using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using LudeonTK;
using MGAutoSell.Extensions;
using MGAutoSell.Filter;
using MGAutoSell.HarmonyPatches;
using MGAutoSell.Query;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace MGAutoSell
{
    public record TradeEntry(Tradeable Tradeable, ThingDef ThingDef, int ColonyCount, int TraderCount);

    public record SellItem(Tradeable Tradeable, int Count)
    {
        public Tradeable Tradeable { get; set; } = Tradeable;
        public int Count { get; set; } = Count;
    }

    [StaticConstructorOnStartup]
    public static class TradeDealProcessor
    {
        private static MethodInfo _propertySetter = AccessTools.PropertySetter(typeof(Tradeable), nameof(Tradeable.CountToTransfer));
        private static Dictionary<ThingDef, int> ThingDefAggregations = new();
        private static Dictionary<ThingDef, int> ExtrasOnMap = new();
        private static Dictionary<TradeRule, int> TradeRuleAggregations = new();

        private static bool GroupedTrading = false;
        private static string Gray = ColorUtility.ToHtmlStringRGB(TradeUI.NoTradeColor);

        public static void StartGroupedTrading()
        {
            if (GroupedTrading)
            {
                Log.Error("Attempted to start a trading session while one is already running");
                return;
            }

            ThingDefAggregations.Clear();
            TradeRuleAggregations.Clear();
            ExtrasOnMap.Clear();
            GroupedTrading = true;
        }

        public static void EndGroupedTrading()
        {
            if (!GroupedTrading)
            {
                Log.Error("Attempted to end a trading session while one is not running.");
                return;
            }

            ThingDefAggregations.Clear();
            TradeRuleAggregations.Clear();
            ExtrasOnMap.Clear();
            GroupedTrading = false;
        }

        [DebugAction("FindAndTrade", "DoTrade")]
        public static void DEBUG_DoTrade()
        {
            var pawn = Find.CurrentMap.mapPawns.PawnsInFaction(Faction.OfPlayer);
            var socialPawn = pawn.MaxBy(x => x.skills?.GetSkill(SkillDefOf.Social).Level);
            var ships = Find.CurrentMap.passingShipManager.passingShips;
            StartGroupedTrading();
            foreach (var passingShip in ships)
            {
                Log.Message($"Trading with {(passingShip as TradeShip).TraderName}");
                TradeSession.SetupWith(passingShip as TradeShip, socialPawn, false);
                var deal = TradeSession.deal;
                DoTradeDeal(deal);

                var silver = deal.CurrencyTradeable.CountToTransfer;

                var buy = deal.AllTradeables
                    .Where(x => x.CountToTransfer > 0 && !x.IsCurrency)
                    .Select(x => (x.ThingDef.label, x.CountToTransfer))
                    .ToList();
                var sell = deal.AllTradeables
                    .Where(x => x.CountToTransfer < 0 && !x.IsCurrency)
                    .Select(x => (x.ThingDef.label, x.CountToTransfer))
                    .ToList();

                if(!buy.Any() && !sell.Any())
                    continue;

                if (!deal.TryExecute(out var actuallyTraded)) 
                    continue;

                if(!actuallyTraded)
                    Debugger.Break();

                DoLetter(passingShip as ITrader, socialPawn, deal, socialPawn.Position, buy, sell, silver);

            }
            EndGroupedTrading();
        }

        public static void DoTrade(Pawn socialPawn, ITrader trader)
        {
            if (!trader.CanTradeNow)
                return;

            var comp = Current.Game.GetComponent<TradeRulesGameComp>();

            if (comp.traders.Contains(trader))
                return;

            TradeSession.SetupWith(trader, socialPawn, false);
            var deal = TradeSession.deal;
            DoTradeDeal(deal);
            comp.traders.Add(trader);
            var silver = deal.CurrencyTradeable.CountToTransfer;

            var buy = deal.AllTradeables
                .Where(x => x.CountToTransfer > 0 && !x.IsCurrency)
                .Select(x => (x.ThingDef.label, x.CountToTransfer))
                .ToList();
            var sell = deal.AllTradeables
                .Where(x => x.CountToTransfer < 0 && !x.IsCurrency)
                .Select(x => (x.ThingDef.label, x.CountToTransfer))
                .ToList();

            if (!buy.Any() && !sell.Any())
                return;

            if (!deal.TryExecute(out var actuallyTraded))
                return;

            DoLetter(trader, socialPawn, deal, socialPawn.Position, buy, sell, silver);

        }

        public static void DoTradeShips(Pawn socialPawn)
        {
            var comp = Current.Game.GetComponent<TradeRulesGameComp>();
            var ships = socialPawn.Map.passingShipManager.passingShips.ToList();

            ships.RemoveAll(x =>
            {
                if (x is not ITrader trader)
                    return true;

                return comp.traders.Contains(trader) ||
                       !socialPawn.CanTradeWith(trader.Faction, trader.TraderKind);
            });

            if(!ships.Any()) 
                return;

            try
            {
                StartGroupedTrading();

                foreach (var passingShip in ships)
                {
                    var ship = (TradeShip)passingShip;

                    if(ship == null)
                        Log.Warning($"Ship was null? Perhaps not a ship? '{passingShip.GetType()}'. Skipping.");

                    TradeSession.SetupWith(ship, socialPawn, false);
                    var deal = TradeSession.deal;
                    DoTradeDeal(deal);

                    comp.traders.Add(passingShip as ITrader);

                    var silver = deal.CurrencyTradeable.CountToTransfer;

                    var buy = deal.AllTradeables
                        .Where(x => x.CountToTransfer > 0 && !x.IsCurrency)
                        .Select(x => (x.ThingDef.label, x.CountToTransfer))
                        .ToList();
                    var sell = deal.AllTradeables
                        .Where(x => x.CountToTransfer < 0 && !x.IsCurrency)
                        .Select(x => (x.ThingDef.label, x.CountToTransfer))
                        .ToList();

                    if (!buy.Any() && !sell.Any())
                        continue;

                    if (!deal.TryExecute(out var actuallyTraded))
                        continue;

                    DoLetter(passingShip as ITrader, socialPawn, deal, socialPawn.Position, buy, sell, silver);
                }
            }
            finally
            {
                EndGroupedTrading();
            }
        }

        public static void DoTradeDeal(TradeDeal deal)
        {
            var map = TradeSession.playerNegotiator?.Map;
            if (map == null) return;

            var autoTrade = Current.Game.GetComponent<TradeRulesGameComp>();

            if (!autoTrade.tradeRules.Any())
                return;

            var tradeables = deal.AllTradeables;
            if (tradeables == null) return;

            var sellDictionary = new Dictionary<Tradeable, int>();
            var buyDictionary = new Dictionary<Tradeable, int>();
            var itemCache = tradeables.Where(x => x.TraderWillTrade && x.ThingDef != null).ToList();

            if (!GroupedTrading)
            {
                ThingDefAggregations.Clear();
                TradeRuleAggregations.Clear();
                ExtrasOnMap.Clear();
            }

            if(!itemCache.Any())
                return;

            var itemsToAdd = itemCache.GroupBy(x => x.ThingDef).ToDictionary(x => x.Key,
                x => x.ToList().Sum(x => x.CountHeldBy(Transactor.Colony)));

            foreach (var item in itemsToAdd)
            {
                ThingDefAggregations.TryAdd(item.Key, item.Value);
            }
            // Don't buy more medicine if there's literally heaps in the Hospital already...
            var itemsOnMap = map.listerThings.AllThings.Where(x => (!x.IsForbidden(Faction.OfPlayer) || x.Map.zoneManager.ZoneAt(x.Position) != null) & !x.Position.Fogged(x.Map)).ToList();
            tradeables.ForEach(x => x.thingsColony.ForEach(y => itemsOnMap.Remove(y)));
            //var countsOnMap = itemsOnMap
            //    .GroupBy(x => x.def)
            //    .ToDictionary(x => x.Key, x => x.ToList().Sum(y => y.stackCount));


            foreach (var tradeable in tradeables)
            {
                var junk = tradeable.thingsColony.Where(x =>
                    x.Map.designationManager.DesignationOn(x, MGDesignatorDefOf.MGAutoSell) != null).ToList();

                if(!junk.Any())
                    continue;

                var total = junk.Sum(x => x.stackCount);
                AddCount(null, tradeable.ThingDef, -total);
                sellDictionary[tradeable] = total;
            }
            itemCache.RemoveAll(x => !x.thingsColony.Any() && !x.thingsTrader.Any());

            var pairings = new Dictionary<Tradeable, TradeRule>();
            foreach (var rule in autoTrade.tradeRules.Where(x => x.Enabled && x.search.Children.queries.Any()))
            {
                
                Log.Message($"Processing rule {rule.search.name}");
                var items = new List<TradeEntry>();
                if (Mod.Settings.scanEveryStack)
                {
                    items = new List<TradeEntry>();
                    foreach (var tradeable in itemCache.Where(x => !x.IsCurrency))
                    {
                        if(AddRuleLabelsToTradeUI.ExtraLabels.ContainsKey(tradeable))
                            continue;

                        var stacksColony = tradeable.thingsColony
                            .GroupBy(x => rule.search.AppliesTo(x, x.Map))
                            .ToDictionary(x => x.Key, x => x.ToList());

                        var stacksTrader = tradeable.thingsTrader
                            .GroupBy(x => rule.search.AppliesTo(x, x.Map))
                            .ToDictionary(x => x.Key, x => x.ToList());

                        var matchedColony = stacksColony.TryGetValue(true, out var matchedColonyStacks);
                        var unmatchedColony = stacksColony.TryGetValue(false, out var unmatchedColonyStacks);

                        var matchedTrader = stacksTrader.TryGetValue(true,out var matchedTraderStacks);
                        var unmatchedTrader = stacksTrader.TryGetValue(false,out var unmatchedTraderStacks);

                        // No Op
                        if (!matchedColony && unmatchedColony && !matchedTrader && unmatchedTrader)
                            continue;

                        // Claim full
                        if ((matchedColony || matchedTrader) && (!unmatchedColony && !unmatchedTrader))
                        {
                            AddRuleLabelsToTradeUI.ExtraLabels[tradeable] =
                                $"<i><color=#{Gray}> - ({rule.search.name})</color></i>";
                            items.Add(new TradeEntry(tradeable, tradeable.ThingDef,
                                tradeable.CountHeldBy(Transactor.Colony),
                                tradeable.CountHeldBy(Transactor.Trader)));
                        }
                        // Split
                        else if (matchedColony is var v &&
                                 ((v || matchedTrader) && (unmatchedColony || unmatchedTrader)))
                        {
                            var claimedTradeable = new Tradeable
                            {
                                thingsColony = matchedColonyStacks ?? new(),
                                thingsTrader = matchedTraderStacks ?? new()
                            };

                            tradeable.thingsColony = unmatchedColonyStacks ?? new();
                            tradeable.thingsTrader = unmatchedTraderStacks ?? new();

                            if (!claimedTradeable.HasAnyThing || !tradeable.HasAnyThing)
                                throw new Exception("Empty tradeable");

                            AddRuleLabelsToTradeUI.ExtraLabels[claimedTradeable] =
                                $"<i><color=#{Gray}> - ({rule.search.name})</color></i>";

                            deal.AllTradeables.Add(claimedTradeable);
                            items.Add(new TradeEntry(claimedTradeable, claimedTradeable.ThingDef,
                                claimedTradeable.CountHeldBy(Transactor.Colony),
                                claimedTradeable.CountHeldBy(Transactor.Trader)));
                        }
                    }
                }
                else
                    items = itemCache
                    .Where(x => 
                        !x.IsCurrency && 
                        x.AnyThingNotJunk(out var thing) && 
                        rule.search.AppliesTo(thing)
                        )
                    .Select(x => new TradeEntry(x, x.ThingDef, x.CountHeldBy(Transactor.Colony), x.CountHeldBy(Transactor.Trader)))
                    .ToList();


                var toSell = rule.AllowSell
                    ? items
                        .Where(x => 
                            GetCount(rule, x.ThingDef) > rule.Export &&
                           (!rule.search.TradeQueries.Any() ||
                            rule.search.AppliesTo(new TradeContext(deal, x.Tradeable, TradeAction.PlayerSells))))
                        .ToList()
                    : [];
                toSell.ForEach(x =>
                {
                    items.Remove(x);
                    itemCache.Remove(x.Tradeable);
                    pairings.TryAdd(x.Tradeable, rule);
                });

                var sellOrders = toSell.Select(x =>
                {
                    var sellOrder = (x.Tradeable,
                        Math.Min(GetCount(rule, x.ThingDef) - rule.Export, x.ColonyCount));
                    AddCount(rule, x.ThingDef, -sellOrder.Item2);
                    return sellOrder;
                }).ToList();


                foreach (var (tradeable, count) in sellOrders)
                {
                    if(sellDictionary.TryAdd(tradeable, count))
                        continue;

                    sellDictionary[tradeable] += count;
                }

                foreach (var matchedItem in itemsOnMap
                             .Where(x => rule.search.AppliesTo(x, x.Map))
                             .GroupBy(x => x.def))
                {
                    var totalStock = matchedItem.Sum(x => x.stackCount);
                    AddCount(rule, matchedItem.Key, totalStock);
                }

                var toBuy =
                    rule.AllowBuy
                        ? items
                            .Where(x =>
                                GetCount(rule, x.ThingDef) < rule.Import &&
                          (!rule.search.TradeQueries.Any() || rule.search.AppliesTo(new TradeContext(deal, x.Tradeable, TradeAction.PlayerBuys))))
                            .ToList()
                        : [];
                toBuy.ForEach(x => itemCache.Remove(x.Tradeable));


                var buyOrders = toBuy.Select(x =>
                {
                    var buyOrder = (x.Tradeable, Math.Min(rule.Import - GetCount(rule, x.ThingDef), x.TraderCount));
                    AddCount(rule, x.ThingDef, buyOrder.Item2);
                    return buyOrder;
                }).ToList();

                foreach (var (tradeable, count) in buyOrders)
                {
                    if (buyDictionary.TryAdd(tradeable, count))
                        continue;

                    buyDictionary[tradeable] += count;
                }

                items
                    .Where(trade =>
                    sellOrders.All(x => x.Tradeable != trade.Tradeable) &&
                    buyOrders.All(x => x.Tradeable != trade.Tradeable))
                    .ToList()
                    .ForEach(x => AddRuleLabelsToTradeUI.ExtraLabels.Remove(x.Tradeable));
            }

            foreach (var (tradeable, toSell) in sellDictionary)
                SetTradeCount(tradeable, -toSell);

            foreach (var (tradeable, toBuy) in buyDictionary)
                SetTradeCount(tradeable, toBuy);

            deal.UpdateCurrencyCount();
            AddRuleLabelsToTradeUI.TradeWindow?.Notify_CommonSearchChanged();

            // Buying too much
            var buyReversed = buyDictionary.Select(x => new SellItem(x.Key, x.Value)).ToList();
            buyReversed.Reverse();
            while (deal.CurrencyTradeable.CountToTransfer < 0 && deal.CurrencyTradeable.CountToTransfer * -1 > deal.CurrencyTradeable.CountHeldBy(Transactor.Colony))
            {
                var gap = deal.CurrencyTradeable.CountToTransfer * -1 -
                          deal.CurrencyTradeable.CountHeldBy(Transactor.Colony);
                deal.NormalizeWith(buyReversed, pairings, gap);
            }

            // Selling too much
            var sellReversed = sellDictionary.Select(x => new SellItem(x.Key, x.Value)).ToList();
            sellReversed.Reverse();
            while (deal.CurrencyTradeable.CountToTransfer > deal.CurrencyTradeable.CountHeldBy(Transactor.Trader))
            {
                var gap = deal.CurrencyTradeable.CountToTransfer -
                          deal.CurrencyTradeable.CountHeldBy(Transactor.Trader);
                deal.NormalizeWith(sellReversed, pairings, gap);
            }

            // Sell marked items first
            deal.AllTradeables.ForEach(x => x.thingsColony = x.thingsColony.OrderBy(x => x.Map.designationManager.DesignationOn(x, MGDesignatorDefOf.MGAutoSell) == null).ToList());
        }

        private static void NormalizeWith(this TradeDeal deal, List<SellItem> list, Dictionary<Tradeable, TradeRule> pairings, int gap)
        {
            var item = list.FirstOrDefault();
            var cost = Math.Max(item.Tradeable.CurTotalCurrencyCostForSource,
                item.Tradeable.CurTotalCurrencyCostForDestination);
            if (item.Count == 1 || cost < gap)
            {
                AddCount(pairings.TryGetValue(item.Tradeable), item.Tradeable.ThingDef, -1);
                SetTradeCount(item.Tradeable, 0);
                list.Remove(item);
            }
            else
            {
                var costPer = cost / item.Tradeable.CountToTransfer;
                var reduction = (int)(item.Tradeable.CountToTransfer < 0 ? Math.Floor(gap / costPer) : Math.Ceiling(gap / costPer));
                item.Count = item.Tradeable.CountToTransfer - reduction;
                AddCount(pairings.TryGetValue(item.Tradeable), item.Tradeable.ThingDef, -reduction);
                SetTradeCount(item.Tradeable, item.Count);
            }
            deal.UpdateCurrencyCount();
        }

        private static void SetTradeCount(Tradeable tradeable, int count)
        {
            _propertySetter.Invoke(tradeable, [count]);

        }

        private static int GetCount(TradeRule rule, ThingDef def, Dictionary<ThingDef, int> recentlyTraded = null)
        {
            return rule.Aggregation switch
            {
                TradeRuleAggregation.ThingDef => ThingDefAggregations.GetValueOrDefault(def, 0),
                TradeRuleAggregation.Rule => TradeRuleAggregations.GetValueOrDefault(rule, 0),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private static void AddCount(TradeRule rule, ThingDef def, int amount)
        {
            switch (rule?.Aggregation ?? TradeRuleAggregation.ThingDef)
            {
                case TradeRuleAggregation.ThingDef:
                    if (!ThingDefAggregations.TryAdd(def, amount))
                        ThingDefAggregations[def] += amount;
                    break;
                case TradeRuleAggregation.Rule:
                    if (!TradeRuleAggregations.TryAdd(rule, amount))
                        TradeRuleAggregations[rule] += amount;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static readonly string 
            spent = "MGAutoSell.Spent".Translate(), 
            earned = "MGAutoSell.Earned".Translate(),
            bought = "MGAutoSell.Bought".Translate(),
            sold = "MGAutoSell.Sold".Translate();
        public static void DoLetter(
            ITrader trader,
            Pawn pawn,
            TradeDeal deal,
            IntVec3 location,
            List<(string label, int count)> buy,
            List<(string label, int count)> sell,
            int silver)
        {
            if (!buy.Any() && !sell.Any())
                return;

            var word = silver > 0 ? earned : spent;
            float absSilver = Math.Abs(silver);
            

            var buyStringBuilder = new StringBuilder();
            buyStringBuilder.AppendLine(bought.Colorize(ColoredText.TipSectionTitleColor));
            buyStringBuilder.AppendJoin("\n", buy.Select(x => $"  {x.label} x{Math.Abs(x.count)}"));

            var sellStringBuilder = new StringBuilder();
            sellStringBuilder.AppendLine(sold.Colorize(ColoredText.TipSectionTitleColor));
            sellStringBuilder.AppendJoin("\n", sell.Select(x => $"  {x.label} x{Math.Abs(x.count)}"));

            var pawnName = pawn.Name.ToStringShort.Colorize(ColoredText.NameColor);
            var traderColor = (trader.Faction?.PlayerRelationKind ?? FactionRelationKind.Neutral).GetColor();
            var traderName = trader?.TraderName?.Colorize(traderColor) ?? "someone".Colorize(Color.magenta);
            var silverLabel = absSilver.ToStringMoney().Colorize(ColoredText.CurrencyColor);
            var buySection = buy.Any()
                ? buyStringBuilder.ToString()
                : string.Empty;
            var sellSection = sell.Any() ? sellStringBuilder.ToString() : string.Empty;


            var body = "MGAutoSell.Letter".Translate(
                pawnName,
                traderName, 
                silverLabel,
                word, 
                buySection, 
                sellSection
                );

            var globalTargetInfo = new GlobalTargetInfo(location, pawn.Map);
            
            Find.LetterStack.ReceiveLetter("MGAutoSell.LetterTitle".Translate(traderName), body,
                LetterDefOf.PositiveEvent, globalTargetInfo);
        }
    }
}
