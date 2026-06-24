using MGAutoSell.Filter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using TD_Find_Lib;
using Verse;
using System.Diagnostics;
using System.Threading;
using JetBrains.Annotations;
using UnityEngine;

namespace MGAutoSell
{
    public record BenchmarkResults(
        ItemAndLabel<long> AllItems,
        ItemAndLabel<long> Junk,
        ItemAndLabel<long> Sell,
        ItemAndLabel<long> Traders,
        ItemAndLabel<long> SellEntries,
        ItemAndLabel<long> PossibleItems,
        ItemAndLabel<long> Quantity,
        ItemAndLabel<long> BuildCache,
        ItemAndLabel<long> Total);
    public static class CacheUtility
    {
        internal static List<Thing> itemCache;
        
        public static List<IGrouping<ThingDef, Thing>> GetJunk(this List<Thing> allItems)
        {
            var junk = allItems
                .Where(x => x.Map.designationManager.DesignationOn(x, MGDesignatorDefOf.MGAutoSell) != null).ToList();
            junk.ForEach(x => allItems.Remove(x));
            var junkGrouped = junk.GroupBy(x => x.def).ToList();
            return junkGrouped;
        }

        public static Dictionary<ThingDef, int> DoRules(this List<Thing> allItems, TradeRulesGroup rules, ref Dictionary<ThingDef, List<Thing>> thingDictionary, ref Dictionary<TradeRule, List<Thing>> ruleDictionary)
        {
            var sellDictionary = new Dictionary<ThingDef, int>();

            foreach (var rule in rules.Where(x =>
                         x is { Enabled: true } && x.search.Children.queries.Any()))
            {
                var items = allItems.Where(x => rule.search.AppliesTo(x)).ToList();
                if (items.Any())
                    ruleDictionary[rule] = items.ToList();

                if(!rule.AllowSell)
                    continue;

                items.RemoveAll(x => !TradeUtility.EverPlayerSellable(x.def));

                items.ForEach(x => { allItems.Remove(x); });

                var itemsGrouped = items
                    .GroupBy(x => x.def).ToList();

                foreach (var (thingDef, list) in itemsGrouped.ToDictionary(x => x.Key, x => x.ToList()))
                {
                    if (!thingDictionary.TryAdd(thingDef, list))
                        thingDictionary[thingDef].AddRange(list);

                    sellDictionary.TryAdd(thingDef, rule.Export);
                }
            }

            return sellDictionary;
        }

        public static List<SellRecord> GetEntries(this Dictionary<ThingDef, int> sellDictionary, Pawn socialPawn, ref Dictionary<ThingDef, List<Thing>> thingDictionary)
        {
            var traderPriceType = PriceType.Normal.PriceMultiplier();
            var playerNegotiator = socialPawn?.GetStatValue(StatDefOf.TradePriceImprovement) ?? 0.1f;
            var settlement = 0f;
            var drugBonusRaw = socialPawn?.GetStatValue(StatDefOf.DrugSellPriceImprovement) ?? 0f;
            var animalProduceBonusRaw = ModsConfig.IdeologyActive && socialPawn != null
                ? socialPawn.GetStatValue(StatDefOf.AnimalProductsSellImprovement)
                : 0f;
            thingDictionary.RemoveAll(x => !x.Value.Any());
            var sellEntries = thingDictionary.Select(x =>
                {
                    var (thingDef, items) = x;
                    var drugBonus = thingDef.IsNonMedicalDrug ? drugBonusRaw : 0f;
                    var animalProduceBonus = (thingDef.IsLeather || thingDef.IsMeat || thingDef.IsWool)
                        ? animalProduceBonusRaw
                        : 0f;
                    var humanPawn = ModsConfig.IdeologyActive && items.FirstOrDefault() is Pawn pawn &&
                                    pawn.RaceProps.Humanlike
                        ? 0.6f
                        : 1f;
                    var priceTotal = items.Select(y => TradeUtility.GetPricePlayerSell(y, traderPriceType, humanPawn,
                        playerNegotiator, settlement, drugBonus, animalProduceBonus) * y.stackCount).Sum();
                    var itemsTotal = items.Sum(x => x.stackCount);
                    var pricePer = priceTotal / itemsTotal;
                    var sellDown = sellDictionary.TryGetValue(thingDef);

                    if (itemsTotal <= sellDown)
                        return null;

                    priceTotal -= pricePer * sellDown;
                    itemsTotal -= sellDown;
                    priceTotal = (float)Math.Round(priceTotal, 0);
                    

                    var total = new ItemAndLabel<float>(priceTotal, priceTotal.ToStringMoney());
                    var pricePerItem = new ItemAndLabel<float>((priceTotal / itemsTotal),
                        (priceTotal / itemsTotal).ToStringMoney());


                    return new SellRecord(thingDef, itemsTotal, total, pricePerItem);
                })
                .Where(x => x != null)
                .OrderByDescending(x => x.Total.Value)
                .ToList();
            return sellEntries;
        }

        public static Dictionary<TradeRule, (ItemAndLabel<int>, ItemAndLabel<int>)> GetRuleCounts(
            this Dictionary<TradeRule, List<Thing>> ruleDictionary)
        {
            if (!Mod.Settings.showQuantityInsteadOfLabel && !Mod.Settings.colorRuleCountsOnWork)
                return [];

            return ruleDictionary.ToDictionary(x => x.Key,
                    x =>
                    {
                        var grouped = x.Value.GroupBy(y => y.def)
                            .Select(y => new RuleRecord(y.Key, y.ToList().Sum(z => z.stackCount))).ToList();
                        var max = x.Key.Aggregation == TradeRuleAggregation.ThingDef
                            ? grouped
                                .Max(x => x.Count)
                            : x.Value.Sum(y => y.stackCount);

                        var min = x.Key.Aggregation == TradeRuleAggregation.Rule 
                            ? max
                            : grouped.Min(x => x.Count);

                        return (new ItemAndLabel<int>(min, "x" + min), new ItemAndLabel<int>(max, "x" + max));
                    });
        }

        public static List<PotentialItem> GetPossibleItemsList(this List<TradeRule> rules, List<SellRecord> sellEntries)
        {
            if (!Mod.Settings.showAllMatchingItems)
                return [];

            itemCache ??= GenerateItemCache();
            var potentialItems = rules.Where(x => x.Enabled && x.AllowBuy)
                .SelectMany(x => (x.search.AllItems ??= x.search.GetPossibleItems()).Select(y => new PotentialItem(y, $"<i>({x.search.name})</i>")))
                .Distinct()
                .ToList();
            potentialItems.RemoveAll(x => sellEntries.Any(y => y.Item == x.Item));
            return potentialItems;
        }

        public static List<ThingDef> GetPossibleItems(this QuerySearch search)
        {
            itemCache ??= GenerateItemCache(); 

            if (!search.Children.queries.Any(x => x.Enabled))
                return [];

            var removals = new List<Thing>();
            var possibleItems = itemCache.Where(x =>
            {
                try
                {
                    return search.AppliesTo(x);
                }
                catch
                {
                    removals.Add(x);
                    return false;
                }
            }).Select(x => x.def).ToList();

            foreach (var removal in removals)
            {
                Log.Warning($"An error occured while matching ThingDef {removal.def.defName} for a rule, removing it from the list.\nIt's possible this item has a complex creation process, hence the error.");
                itemCache.Remove(removal);
            }

            return possibleItems;
        }

        public static List<Thing> GenerateItemCache()
        {
            var traders = DefDatabase<TraderKindDef>.AllDefsListForReading;

            return DefDatabase<ThingDef>.AllDefsListForReading
                .Where(x => traders.Any(trader => trader.WillTrade(x)) && x.uiIcon != null)
                .Select(y => y.race != null ? TryGenPawn(y) : TryGenThing(y))
                .Where(x => x != null)
                .ToList();
        }

        [CanBeNull]
        private static Pawn TryGenPawn(ThingDef thing)
        {
            try
            {
                return PawnGenerator.GeneratePawn(thing.race?.AnyPawnKind ?? null);
            }
            catch (Exception ex)
            {
                Log.Warning($"Failed to PawnGen '{thing.defName}' def\n{ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        [CanBeNull]
        private static Thing TryGenThing(ThingDef thing)
        {
            try
            {
                return ThingMaker.MakeThing(thing, thing.MadeFromStuff ? GenStuff.DefaultStuffFor(thing) : null);
            }
            catch (Exception ex)
            {
                Log.Warning($"Failed to ThingGen '{thing.defName}' def\n{ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        public static ItemsToSell Cache(TradeRulesGameComp comp, Map map, out BenchmarkResults benchmark, Pawn SellerOverride = null, bool withBenchmark = false)
        {
            long benchmarkAllItems = 0, benchmarkJunk = 0, benchmarkSell = 0, benchmarkTraders = 0, benchmarkSellEntries = 0, benchmarkPossibleItems = 0, benchmarkSilver = 0, benchmarkQuantity = 0, benchmarkBuildCache = 0;

            if(withBenchmark)
            {
                comp.tradeRules.ForEach(x => x.search.AllItems = null);
                itemCache ??= GenerateItemCache();
            }

            var timestamp = Stopwatch.GetTimestamp();
            //var allItems = TradeUtility.AllLaunchableThingsForTrade(Find.CurrentMap).ToList();
            var allItems = map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver).Where(x =>
                (!x.IsForbidden(Faction.OfPlayer) || x.Map.zoneManager.ZoneAt(x.Position) != null) &&
                !x.Position.Fogged(x.Map))
                .ToList();
            allItems.AddRange(TradeUtility.AllSellableColonyPawns(map, false).ToList());

            if (withBenchmark)
                RecordTime(ref timestamp, ref benchmarkAllItems);

            var thingDictionary = new Dictionary<ThingDef, List<Thing>>();
            var ruleDictionary = new Dictionary<TradeRule, List<Thing>>();

            var junk = allItems.GetJunk();

            thingDictionary.AddRange(junk.ToDictionary(x => x.Key,
                x => x.ToList()));

            if (withBenchmark)
                RecordTime(ref timestamp, ref benchmarkJunk);

            var sellDictionary = allItems.DoRules(comp.tradeRules, ref thingDictionary, ref ruleDictionary);

            if (withBenchmark)
                RecordTime(ref timestamp, ref benchmarkSell);

            var traders = TabUtility.GetTraders(false);
            if (comp.autoTrade)
            {
                var allowedTraders = traders.Where(x => comp.autoTraderIDs.Contains(x.Pawn.thingIDNumber)).ToList();
                if (allowedTraders.Any())
                    traders = allowedTraders;

            }

            if (withBenchmark)
                RecordTime(ref timestamp, ref benchmarkTraders);

            var socialPawn = SellerOverride ?? (traders.Any() ? traders.MaxBy(x => x.Improvement).Pawn : null);
            var playerNegotiator = socialPawn?.GetStatValue(StatDefOf.TradePriceImprovement) ?? 0.1f;
            var isLeader = ModsConfig.IdeologyActive && socialPawn == Faction.OfPlayer.leader;

            var sellEntries = sellDictionary.GetEntries(socialPawn, ref thingDictionary);

            if (withBenchmark)
                RecordTime(ref timestamp, ref benchmarkSellEntries);

            var potentialItems = comp.tradeRules.GetPossibleItemsList(sellEntries);

            if (withBenchmark)
                RecordTime(ref timestamp, ref benchmarkPossibleItems);

            var totalSilver = (float)Math.Round(sellEntries.Sum(x => x.Total.Value), 0);

            if(withBenchmark)
                RecordTime(ref timestamp, ref benchmarkSilver);

            var ruleCounts = ruleDictionary.GetRuleCounts();

            if (withBenchmark)
                RecordTime(ref timestamp, ref benchmarkQuantity);

            var trader = new TraderRecord(socialPawn,
                socialPawn?.LabelShort ?? "No trader",
                () => socialPawn != null ? PortraitsCache.Get(socialPawn, new Vector2(24, 24), Rot4.South,
                    ColonistBarColonistDrawer.PawnTextureCameraOffset, 1.28205f) : null,
                playerNegotiator.ToStringPercent(), playerNegotiator, isLeader);

            var sellCache = new ItemsToSell(
                Items: sellEntries,
                PotentialItems: potentialItems,
                TotalSilver: new ItemAndLabel<float>(totalSilver, totalSilver.ToStringMoney()),
                Trader: trader,
                Rules: ruleCounts);
            sellCache.Rules.RemoveAll(x => x.Value.max.Value == 0);

            if (withBenchmark)
                RecordTime(ref timestamp, ref benchmarkBuildCache);

            ItemAndLabel<long> BM(long v) => new(v, TimeSpan.FromTicks(v).TotalMilliseconds + "ms");

            var total = benchmarkAllItems + benchmarkJunk + benchmarkSell + benchmarkTraders + benchmarkSellEntries +
                        benchmarkPossibleItems + benchmarkQuantity + benchmarkBuildCache;

            benchmark = new BenchmarkResults(BM(benchmarkAllItems), BM(benchmarkJunk), BM(benchmarkSell), BM(benchmarkTraders),
                    BM(benchmarkSellEntries), BM(benchmarkPossibleItems), BM(benchmarkQuantity), BM(benchmarkBuildCache), BM(total));

            return sellCache;
        }

        public static void RecordTime(ref long startTime, ref long duration)
        {
            var benchmarkNewTime = Stopwatch.GetTimestamp();
            duration = benchmarkNewTime - startTime;
            startTime = benchmarkNewTime;
        }
    }
}
