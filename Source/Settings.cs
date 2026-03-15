using System.Collections.Generic;
using System.Linq;
using RimWorld;
using TD_Find_Lib;
using UnityEngine;
using Verse;

namespace MGAutoSell
{
    public class Settings : ModSettings
    {
        // TODO Icons in Menu (true)
        public bool scanEveryStack = true;
        public bool showAllMatchingItems = true;
        public bool showQuantityInsteadOfLabel = true;

        private static string _benchmarkLabel;
        private static string _benchmarkLabelDisabled;
        private static string _scanEveryStackLabel;
        private static string _scanEveryStackTooltip;
        private static string _showAllMatchingItemsLabel;
        private static string _showAllMatchingItemsTooltip;

        private static ItemsToSell _showAllMatchItemsEnabled;
        private static ItemsToSell _showAllMatchItemsDisabled;
        private static Vector2 sellScroll = Vector2.zero;

        public void Init()
        {
            _benchmarkLabel = "MGAutoSell.Settings.Benchmark".Translate();
            _benchmarkLabelDisabled = "MGAutoSell.Settings.BenchmarkDisabled".Translate();
            _scanEveryStackLabel = "MGAutoSell.Settings.scanEveryStackLabel".Translate();
            _scanEveryStackTooltip = "MGAutoSell.Settings.scanEveryStackTooltip".Translate();
            _showAllMatchingItemsLabel = "MGAutoSell.Settings.showAllMatchingItemsLabel".Translate();
            _showAllMatchingItemsTooltip = "MGAutoSell.Settings.showAllMatchingItemsTooltip".Translate();

            var search = new QuerySearch();
            search.name = "Drugs";
            var drugsQuery = ThingQueryMaker.MakeQuery<ThingQueryThingDefCategory>();
            drugsQuery.sel = "drugs";
            search.Children.Add(drugsQuery);

            var drugsPossible = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(x => x.FirstThingCategory == ThingCategoryDefOf.Drugs)
                .Select(x => new PotentialItem(x, search.name))
                .ToList();

            var yayo = ThingDefOf.Yayo;
            var drugsActual = new List<SellRecord>()
            {
                new(ThingDefOf.Yayo, 1, yayo.BaseMarketValue, yayo.BaseMarketValue.ToStringMoney(),
                    yayo.BaseMarketValue.ToStringMoney())
            };

            _showAllMatchItemsEnabled = new ItemsToSell(drugsActual, drugsPossible, drugsActual[0].Total,
                drugsActual[0].TotalLabel, null, null);
            _showAllMatchItemsDisabled = new ItemsToSell(drugsActual, [], drugsActual[0].Total,
                drugsActual[0].TotalLabel, null, null);
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref scanEveryStack, "scanEveryStack", true);
            Scribe_Values.Look(ref showAllMatchingItems, "showAllMatchingItems", true);
            Scribe_Values.Look(ref showQuantityInsteadOfLabel, "showQuantityInsteadOfLabel", true);
        }

        public void DoSettingsWindow(Rect rect)
        {
            var listing = new Listing_Standard();
            listing.Begin(rect);

            listing.CheckboxLabeled(_scanEveryStackLabel, ref scanEveryStack, _scanEveryStackTooltip);

            listing.CheckboxLabeled(_showAllMatchingItemsLabel, ref showAllMatchingItems, _showAllMatchingItemsTooltip);
            var sellList = listing.GetRect(150).LeftPartPixels((rect.width / 2) - 16);
            TabUtility.DrawSellPanel(sellList, showAllMatchingItems ? _showAllMatchItemsEnabled : _showAllMatchItemsDisabled, ref sellScroll);

            listing.End();
        }
    }
}
