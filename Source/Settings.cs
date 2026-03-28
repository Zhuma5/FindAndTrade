using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using MGAutoSell.Filter;
using RimWorld;
using TD_Find_Lib;
using UnityEngine;
using Verse;
using Color = UnityEngine.Color;

namespace MGAutoSell
{
    public class Settings : ModSettings
    {
        public Dictionary<string, Vector2> LabelSizeCache = [];
        
        public bool scanEveryStack = true;
        public bool rememberManualTrade = true;

        public bool showAllMatchingItems = true;

        public bool showQuantityInsteadOfLabel = true;
        public bool colorRuleCountsOnWork = true;
        public bool showMinMaxLabelWhereApplicable = true;

#if DEBUG
        public OpenSetting MenuToOpen = OpenSetting.None;
#endif

        private static string _benchmarkLabel;
        private static string _benchmarkLabelDisabled;
        private static string _benchmarkLabelDisabledNoDev;
        private static string _scanEveryStackLabel;
        private static string _scanEveryStackTooltip;
        private static string _rememberManualTradeLabel;
        private static string _rememberManualTradeTooltip;
        private static string _showAllMatchingItemsLabel;
        private static string _showAllMatchingItemsTooltip;
        private static string _showQuantityInsteadOfLabelLabel;
        private static string _showQuantityInsteadOfLabelTooltip;
        private static string _colorRuleCountsOnWorkLabel;
        private static string _colorRuleCountsOnWorkTooltip;
        private static string _showMinMaxLabelWhereApplicableLabel;
        private static string _showMinMaxLabelWhereApplicableTooltip;

        private static BenchmarkResults benchmarkResults = null;
        private static ItemsToSell _showAllMatchItemsEnabled;
        private static ItemsToSell _showAllMatchItemsDisabled;
        private static ItemsToSell _exampleTradeRulesCache;
        private static List<TradeRule> _exampleTradeRules = [];
        private static Vector2 sellScroll = Vector2.zero;
        private static float firstListingHeight;
        private static float maxHeightOfSides;

        public void Init()
        {
            _benchmarkLabel = "MGAutoSell.Settings.Benchmark".Translate();
            _benchmarkLabelDisabled = "MGAutoSell.Settings.BenchmarkDisabled".Translate();
            _benchmarkLabelDisabledNoDev = "MGAutoSell.Settings.BenchmarkDisabledNoDev".Translate();
            _scanEveryStackLabel = "MGAutoSell.Settings.scanEveryStackLabel".Translate();
            _scanEveryStackTooltip = "MGAutoSell.Settings.scanEveryStackTooltip".Translate();
            _rememberManualTradeLabel = "MGAutoSell.Settings.rememberManualTradeLabel".Translate();
            _rememberManualTradeTooltip = "MGAutoSell.Settings.rememberManualTradeTooltip".Translate();
            _showAllMatchingItemsLabel = "MGAutoSell.Settings.showAllMatchingItemsLabel".Translate();
            _showAllMatchingItemsTooltip = "MGAutoSell.Settings.showAllMatchingItemsTooltip".Translate();
            _showQuantityInsteadOfLabelLabel = "MGAutoSell.Settings.showQuantityInsteadOfLabelLabel".Translate();
            _showQuantityInsteadOfLabelTooltip = "MGAutoSell.Settings.showQuantityInsteadOfLabelTooltip".Translate();
            _colorRuleCountsOnWorkLabel = "MGAutoSell.Settings.colorRuleCountsOnWorkLabel".Translate();
            _colorRuleCountsOnWorkTooltip = "MGAutoSell.Settings.colorRuleCountsOnWorkTooltip".Translate();
            _showMinMaxLabelWhereApplicableLabel = "MGAutoSell.Settings.showMinMaxLabelWhereApplicableLabel".Translate();
            _showMinMaxLabelWhereApplicableTooltip = "MGAutoSell.Settings.showMinMaxLabelWhereApplicableTooltip".Translate();


            var test = Enum.GetValues(typeof(PriceType)).OfType<PriceType>().ToList();
            var search = new QuerySearch();
            search.name = "Drugs";
            var drugsQuery = ThingQueryMaker.MakeQuery<ThingQueryThingDefCategory>();
            drugsQuery.sel = "drugs";
            search.Children.Add(drugsQuery);

            var drugsPossible = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(x => x.FirstThingCategory == ThingCategoryDefOf.Drugs)
                .Select(x => new PotentialItem(x, "(" + search.name + ")"))
                .ToList();

            var yayo = ThingDefOf.Yayo;
            var totals = new ItemAndLabel<float>(yayo.BaseMarketValue, yayo.BaseMarketValue.ToStringMoney());
            var drugsActual = new List<SellRecord>()
            {
                new(ThingDefOf.Yayo, 1, totals, totals)
            };

            _showAllMatchItemsEnabled = new ItemsToSell(drugsActual, drugsPossible, drugsActual[0].Total, null, null);
            _showAllMatchItemsDisabled = new ItemsToSell(drugsActual, [], drugsActual[0].Total, null, null);

            firstListingHeight = 0;
            firstListingHeight += Text.CalcSize(_scanEveryStackLabel).y;
#if DEBUG
            firstListingHeight += 30;
#endif

            var itemRules = new Dictionary<TradeRule, (ItemAndLabel<int>, ItemAndLabel<int>)>();
            var steelTradeRule = new TradeRule("Steel")
            {
                Import = 1000,
                ImportBuffer = "1000",
                Mode = TradeMode.Import
            };
            itemRules[steelTradeRule] = (new ItemAndLabel<int>(900, "x900"), new ItemAndLabel<int>(900, "x900"));
            _exampleTradeRules.Add(steelTradeRule);

            var meals = new TradeRule("Meals")
            {
                Import = 20,
                ImportBuffer = "20",
                Mode = TradeMode.Import,
            };
            itemRules[meals] = (new ItemAndLabel<int>(24, "x24"), new ItemAndLabel<int>(24, "x24"));
            _exampleTradeRules.Add(meals);

            var pleasurableDrugs = new TradeRule("Pleasurable Drugs")
            {
                Import = 5,
                ImportBuffer = "5",
                Mode = TradeMode.Maintain,
                Export = 30,
                ExportBuffer = "30"
            };
            itemRules[pleasurableDrugs] = (new ItemAndLabel<int>(4, "x4"), new ItemAndLabel<int>(31, "x31"));
            _exampleTradeRules.Add(pleasurableDrugs);

            var organs = new TradeRule("Organs")
            {
                Export = 0,
                ExportBuffer = "0",
                Mode = TradeMode.Export
            };
            itemRules[organs] = (new ItemAndLabel<int>(2, "x2"), new ItemAndLabel<int>(2, "x2"));
            _exampleTradeRules.Add(organs);

            var lowQualityArt = new TradeRule("Low Quality Art")
            {
                Export = 0,
                ExportBuffer = "0",
                Mode = TradeMode.Export
            };
            itemRules[lowQualityArt] =  (new ItemAndLabel<int>(0, "x0"), new ItemAndLabel<int>(0, "x0"));
            _exampleTradeRules.Add(lowQualityArt);

            _exampleTradeRulesCache = new ItemsToSell(null, null, null, null, itemRules);
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref scanEveryStack, "scanEveryStack", true);
            Scribe_Values.Look(ref showAllMatchingItems, "showAllMatchingItems", true);
            Scribe_Values.Look(ref showQuantityInsteadOfLabel, "showQuantityInsteadOfLabel", true);
            Scribe_Values.Look(ref colorRuleCountsOnWork, "colorRuleCountsOnWork", true);
            Scribe_Values.Look(ref rememberManualTrade, "rememberManualTrade", true);

#if DEBUG
            Scribe_Values.Look(ref MenuToOpen, "MenuToOpen");
#endif
        }

        public void DoSettingsWindow(Rect rect)
        {
            var faded = new Color(1, 1, 1, 0.4f);
            var color = GUI.color;
            var benchmarkTray = Rect.zero;

            rect.SplitHorizontallyWithMargin(out var top, out var bottom, out _, topHeight: firstListingHeight, compressibleMargin: 20);
            bottom.SplitVerticallyWithMargin(out var left, out var right, 16f);
            var listing = new Listing_Standard();
            listing.Begin(top);

            listing.CheckboxLabeled(_scanEveryStackLabel, ref scanEveryStack, _scanEveryStackTooltip);
            listing.CheckboxLabeled(_rememberManualTradeLabel, ref rememberManualTrade, _rememberManualTradeTooltip);
#if DEBUG
            if (listing.ButtonTextLabeled("DEBUG: Open window on launch", MenuToOpen.ToString(), TextAnchor.MiddleLeft))
            {
                Find.WindowStack.Add(new FloatMenu([
                    new(OpenSetting.None.ToString(), () => MenuToOpen = OpenSetting.None),
                    new(OpenSetting.Settings.ToString(), () => MenuToOpen = OpenSetting.Settings),
                    new(OpenSetting.MainMenuTab.ToString().SplitCamelCase(), () => MenuToOpen = OpenSetting.MainMenuTab),

                ]));
            }
#endif
            listing.End();
            // Sell list
            listing.Begin(right);

            listing.CheckboxLabeled(_showAllMatchingItemsLabel, ref showAllMatchingItems, _showAllMatchingItemsTooltip);
            if(listing.CurHeight > maxHeightOfSides)
                maxHeightOfSides = listing.CurHeight;
            else if(listing.CurHeight < maxHeightOfSides)
                listing.Gap(maxHeightOfSides - listing.CurHeight);
            listing.GapLine(4);


            var sellList = listing.GetRect(150);
            TabUtility.DrawSellPanel(sellList, showAllMatchingItems ? _showAllMatchItemsEnabled : _showAllMatchItemsDisabled, ref sellScroll);

            listing.End();
            // Rules
            listing.Begin(left);

            listing.CheckboxLabeled(_showQuantityInsteadOfLabelLabel, ref showQuantityInsteadOfLabel, _showQuantityInsteadOfLabelTooltip);
            listing.CheckboxLabeled(_colorRuleCountsOnWorkLabel, ref colorRuleCountsOnWork, _colorRuleCountsOnWorkTooltip);

            if (!showQuantityInsteadOfLabel)
                GUI.color = faded;
            listing.CheckboxLabeled(_showMinMaxLabelWhereApplicableLabel, ref showMinMaxLabelWhereApplicable, _showMinMaxLabelWhereApplicableTooltip);
            GUI.color = color;

            var height = 300f;
            if (listing.CurHeight > maxHeightOfSides)
                maxHeightOfSides = listing.CurHeight;
            else if (listing.CurHeight < maxHeightOfSides)
                listing.Gap(maxHeightOfSides - listing.CurHeight);
            listing.GapLine(4);

            var body = listing.GetRect(height);
            var drawerListing = new Listing_Standard();
            drawerListing.Begin(body);

            for (var index = 0; index < _exampleTradeRules.Count; index++)
            {
                var tradeRule = _exampleTradeRules[index];
                TradeRuleDrawUtility.DrawRow(drawerListing.GetRect(30), tradeRule, index, _exampleTradeRulesCache, -1);
            }

            height = drawerListing.CurHeight;
            drawerListing.End();
            listing.End();

            var biggestHeight = height > 150f ? height : 150f;
            GUI.color = faded;
            Widgets.DrawLineVertical(body.width + 8f, body.y + bottom.y, biggestHeight);
            GUI.color = color;

            //if (benchmarkTray == Rect.zero)
            //    return;

            

            bottom.SplitHorizontallyWithMargin(out bottom, out benchmarkTray, out _, 20f, biggestHeight + maxHeightOfSides);

            if (!Prefs.DevMode)
            {
                listing.Begin(benchmarkTray.BottomPartPixels(8f + Text.LineHeight));
                GUI.color = faded;
                listing.GapLine(8f);
                listing.Label(_benchmarkLabelDisabledNoDev);
                GUI.color = color;
                listing.End();
                return;
            }

            listing.Begin(benchmarkTray);
            listing.GapLine(8f);

            if (Find.CurrentMap == null)
            {
                listing.Label(_benchmarkLabelDisabled);
                listing.End();
                return;
            }

            if (listing.ButtonText(_benchmarkLabel))
            {
                // Run it twice to get an accurate result
                if(benchmarkResults == null)
                    CacheUtility.Cache(Current.Game.GetComponent<TradeRulesGameComp>(), out benchmarkResults, withBenchmark: true);
                CacheUtility.Cache(Current.Game.GetComponent<TradeRulesGameComp>(), out benchmarkResults, withBenchmark: true);
            }

            if (benchmarkResults != null)
            {
                var index = 0;

                // AllItems is roughly 0.250ms on my PC, which works out to 1/4 runtime for 1ms. 
                // But should extend the length as needed if going past 1ms
                var max = TimeSpan.FromMilliseconds(
                    Math.Max(1, Math.Round((double)benchmarkResults.AllItems.Value * 4 / TimeSpan.TicksPerMillisecond, 0))).Ticks;
                DoBenchmarkLine(listing, "Searching map for items", benchmarkResults.AllItems, max, ref index);
                DoBenchmarkLine(listing, "Add junk to be sold", benchmarkResults.Junk, max, ref index);
                DoBenchmarkLine(listing, "Match items to rules", benchmarkResults.Sell, max, ref index);
                DoBenchmarkLine(listing, "Select traders", benchmarkResults.Traders, max, ref index);
                DoBenchmarkLine(listing, "Sell list", benchmarkResults.SellEntries, max, ref index);
                if(showAllMatchingItems)
                    DoBenchmarkLine(listing, $"Regenerate all rules' matching ThingDefs lists", benchmarkResults.PossibleItems, max, ref index);
                if(showQuantityInsteadOfLabel || colorRuleCountsOnWork)
                    DoBenchmarkLine(listing, $"Quantity per rule", benchmarkResults.Quantity, max, ref index);
                DoBenchmarkLine(listing, "Create cache", benchmarkResults.BuildCache, max, ref index);
            }

            listing.End();
        }

        
        private void DoBenchmarkLine(Listing listing, string label, ItemAndLabel<long> item, long max, ref int index)
        {
            var rect = listing.GetRect(26);

            if (index % 2 == 1)
                Widgets.DrawLightHighlight(rect);

            rect.SplitVertically(rect.width / 2, out var left, out var right);

            var textAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(left, label);
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(left, item.Label);
            Text.Anchor = textAnchor;

            var percent = Math.Clamp((float)item.Value / max, 0, 1);

            var color = GUI.color;
            GUI.color = Color.Lerp(ColoredText.FactionColor_Neutral, ColoredText.FactionColor_Hostile, percent * 4);
            GUI.DrawTexture(right.ContractedBy(4).LeftPart(percent), TexUI.FastFillTex);
            GUI.color = color;

            index++;
        }
    }
}
