using MGAutoSell.Filter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using TD_Find_Lib;
using Verse;

namespace MGAutoSell
{
    public static class CacheUtility
    {
        private static List<Thing> itemCache;
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
                         x is { Enabled: true, AllowSell: true } && x.search.Children.queries.Any()))
            {
                var items = allItems.Where(x => rule.search.AppliesTo(x)).ToList();
                if (items.Any())
                    ruleDictionary[rule] = items;

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
            var playerNegotiator = socialPawn.GetStatValue(StatDefOf.TradePriceImprovement);
            var settlement = socialPawn.TradePriceImprovementOffsetForPlayer;
            var drugBonusRaw = socialPawn.GetStatValue(StatDefOf.DrugSellPriceImprovement);
            var animalProduceBonusRaw = ModsConfig.IdeologyActive
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

                    var total = (float)Math.Round(priceTotal, 0);

                    var pricePerLabel = (priceTotal / itemsTotal).ToStringMoney();
                    var totalLabel = total.ToStringMoney();


                    return new SellRecord(thingDef, itemsTotal, total, pricePerLabel, totalLabel);
                })
                .Where(x => x != null)
                .OrderByDescending(x => x.Total)
                .ToList();
            return sellEntries;
        }

        public static Dictionary<TradeRule, string> GetRuleCounts(
            this Dictionary<TradeRule, List<Thing>> ruleDictionary)
        {
            if (!Mod.Settings.showQuanityInsteadOfLabel)
                return [];

            return ruleDictionary.ToDictionary(x => x.Key,
                    x => "x" + (x.Key.Aggregation == TradeRuleAggregation.ThingDef
                        ? x.Value.GroupBy(y => y.def)
                            .Select(y => new RuleRecord(y.Key, y.ToList().Sum(z => z.stackCount)))
                            .Max(x => x.Count).ToString()
                        : x.Value.Sum(y => y.stackCount).ToString()));
        }

        public static List<PotentialItem> GetPossibleItemsList(this TradeRulesGroup rules, List<SellRecord> sellEntries)
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

            return itemCache.Where(x => search.AppliesTo(x)).Select(x => x.def).ToList();
        }

        public static List<Thing> GenerateItemCache()
        {
            var traders = DefDatabase<TraderKindDef>.AllDefsListForReading;

            return DefDatabase<ThingDef>.AllDefsListForReading
                .Where(x => traders.Any(trader => trader.WillTrade(x)) && x.uiIcon != null)
                .Select(y => y.race != null ? PawnGenerator.GeneratePawn(y.race.AnyPawnKind) : ThingMaker.MakeThing(y, y.MadeFromStuff ? GenStuff.DefaultStuffFor(y) : null)).ToList();
        }
    }
}
