using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using LudeonTK;
using RimWorld;
using TD_Find_Lib;
using UnityEngine;
using Verse;
using Verse.Noise;
using static HarmonyLib.Code;

namespace MGAutoSell
{
    public record ItemsToSell(List<SellRecord> Items, float TotalSilver, TraderRecord Trader, Dictionary<TradeRule, List<RuleRecord>> Rules);
    public record SellRecord(ThingDef Item, int Count, float Total, string PricePer);
    public record TraderRecord(string Name, Texture Icon, string Improvement);
    public record RuleRecord(ThingDef Item, int Count);
    public class MainTabWindow_FindAndAutoSell : MainTabWindow
    {
        public ItemsToSell sellCache;

        private TradeRulesGameComp comp;
        private TradeRuleEditor editor;

        private long nextCache = 0;
        private long nextQuickCache = 0;

        Vector2 listerScroll = Vector2.zero;
        Vector2? SellingSize, BuyingSize;
        private string previousRenderTime;

        private string title = $"<i>{"MGAutoSell.Title".Translate()}</i>";

#if DEBUG
        List<long> ticks = new List<long>();
        private long nextPerformance = 0;
#endif

        public override Vector2 RequestedTabSize => new(1010f, 300f);
        protected override float Margin => 8f;
        public bool SellListDirty => editor != null;

        public MainTabWindow_FindAndAutoSell()
        {
            preventCameraMotion = false;
            doCloseX = true;
        }

        public override void PreOpen()
        {
            base.PreOpen();
            comp = Current.Game.GetComponent<TradeRulesGameComp>();
        }

        public override void Close(bool doCloseSound = true)
        {
            editor?.PostClose();
            editor = null;
            SelectedTradeRule = null;
            base.Close(doCloseSound);
        }

        
        public override void DoWindowContents(Rect inRect)
        {
#if DEBUG
            if (nextPerformance == 0)
                nextPerformance = DateTimeOffset.Now.AddSeconds(1).ToUnixTimeSeconds();
            if (DateTimeOffset.Now.ToUnixTimeSeconds() > nextPerformance)
            {
                previousRenderTime = $"{ticks.Min()}~{ticks.Max()} | {Median(ticks)}";
                ticks.Clear();
                nextPerformance = DateTimeOffset.Now.AddSeconds(1).ToUnixTimeSeconds();
            }
            var timestamp = Stopwatch.GetTimestamp();
#endif
            var color = GUI.color;
            var fadedColor = new Color(1, 1, 1, 0.4f);
            var font = Text.Font;
            Text.Font = GameFont.Small;

            var width = editor != null ? 600f : 400f;

            var rulesRect = inRect.LeftPartPixels(width);

            if (editor != null)
            {
                editor.DoWindowContents(rulesRect);
                var buttonRect = rulesRect.BottomPartPixels(30f).LeftPartPixels(30f);
                if (Widgets.ButtonImage(buttonRect, TexButton.Banish))
                {
                    editor?.PostClose();
                    editor = null;
                    SelectedTradeRule = null;
                }
            }
            else
            {
                inRect.y -= 4;
                Text.Font = GameFont.Medium;
                GUI.color = fadedColor;

                Widgets.Label(inRect, title);
                Text.Font = GameFont.Small;
                GUI.color = color;
                inRect.y += 4;

                var height = 300f;
                //var header = rulesRect.TopPartPixels(30).LeftPartPixels(rulesRect.width - 16);
                var body = rulesRect.MiddlePartPixels(rulesRect.width, rulesRect.height - 60);


                GUI.color = fadedColor;
                Widgets.DrawLineHorizontal(body.x, body.y, body.width);
                GUI.color = color;
                var drawerListing = new Listing_StandardIndent();
                drawerListing.BeginScrollView(body, ref listerScroll, body.LeftPartPixels(body.width - 16).TopPartPixels(comp.tradeRules.Count * 30).AtZero());
                for (var index = 0; index < comp.tradeRules.Count; index++)
                {
                    var tradeRule = comp.tradeRules[index];
                    var action = TradeRuleDrawUtility.DrawRow(drawerListing.GetRect(30), tradeRule, index, sellCache);
                    switch (action)
                    {
                        case TradeRuleAction.None:
                            break;
                        case TradeRuleAction.Delete:
                            comp.tradeRules.RemoveAt(index);
                            index--;
                            break;
                        case TradeRuleAction.Edit:
                            DoEdit(tradeRule);
                            break;
                        case TradeRuleAction.Suspend:
                            tradeRule.Enabled = !tradeRule.Enabled;
                            break;
                        case TradeRuleAction.Mode:
                            tradeRule.Mode = tradeRule.Mode.Next();
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }

                drawerListing.EndScrollView(ref height);

                var controlsRect = rulesRect.BottomPartPixels(Text.LineHeight);
                if (Widgets.ButtonImage(controlsRect.LeftPartPixels(Text.LineHeight), FindTex.GreyPlus))
                    CreateRule();
#if DEBUG
                GUI.color = fadedColor;
                var controls = new WidgetRow(controlsRect.xMax, controlsRect.y, UIDirection.LeftThenDown);
                controls.Label($"<i> Render: {previousRenderTime}</i>");
                GUI.color = color;
#endif
            }


            var toSellRect = inRect.RightPartPixels(inRect.width - rulesRect.width - 12 - 16);
            toSellRect.x -= 16;

            GUI.color = fadedColor;
            Widgets.DrawLineVertical(rulesRect.width + 6, 0, 300f);
            GUI.color = color;

            CacheItemsToSell();

            toSellRect.SplitHorizontally(Text.LineHeight, out var itemHeader, out toSellRect);

            Widgets.DrawLightHighlight(itemHeader);

            Widgets.Label(itemHeader.RightPartPixels(itemHeader.width - 40f), "Item");
            GUI.DrawTexture(itemHeader.RightPartPixels(24f), ThingDefOf.Silver.uiIcon);

            toSellRect.SplitHorizontally(4, out var gapHeader, out toSellRect);
            Widgets.DrawLineHorizontal(gapHeader.x, gapHeader.y, gapHeader.width, fadedColor);

            int i = 0;
            foreach (var (thingDef, count, total, _) in sellCache.Items)
            {
                toSellRect.SplitHorizontally(Text.LineHeight, out var row, out toSellRect);

                if (i % 2 == 1)
                    Widgets.DrawLightHighlight(row);
                i++;

                GUI.color = thingDef.uiIconColor;
                GUI.DrawTexture(row.LeftPartPixels(row.height), thingDef.uiIcon);
                GUI.color = color;

                row.x += row.height + 10;
                Widgets.Label(row, thingDef.GetLabel() + $" x{count}");
                row.x -= row.height + 10;

                var totalLabel = total.ToStringMoney();
                var size = Text.CalcSize(totalLabel);
                Widgets.Label(row.RightPartPixels(size.x + 4), totalLabel);
            }

            var footer = toSellRect.BottomPartPixels(Text.LineHeight);
            Widgets.DrawLightHighlight(footer);
            var iconRect = footer.LeftPartPixels(Text.LineHeight);
            iconRect.y -= 4;
            GUI.DrawTexture(iconRect, sellCache.Trader.Icon);
            //TradeSession.playerNegotiator.GetStatValue(StatDefOf.TradePriceImprovement).ToStringPercent()
            Widgets.Label(footer.RightPartPixels(footer.width - Text.LineHeight), sellCache.Trader.Name + $" ({sellCache.Trader.Improvement})");

            var footerRow = new WidgetRow(footer.xMax - 4, footer.y, UIDirection.LeftThenDown);
            footerRow.Label(sellCache.TotalSilver.ToStringMoney());
            footerRow.Icon(ThingDefOf.Silver.uiIcon);
            footerRow.Label("Total:");
            Text.Font = font;
#if DEBUG
            ticks.Add(Stopwatch.GetTimestamp() - timestamp);
#endif
        }

        public void CacheItemsToSell(bool force = false)
        {
            var shouldUpdate = force || (SellListDirty && nextQuickCache < DateTime.UtcNow.Ticks) ||
                               nextCache < Find.TickManager.TicksGame;

            if (!shouldUpdate)
                return;

            var timestamp = Stopwatch.GetTimestamp();
            var allItems = TradeUtility.AllLaunchableThingsForTrade(Find.CurrentMap).ToList();
            allItems.AddRange(TradeUtility.AllSellableColonyPawns(Find.CurrentMap, false).ToList());
            var sellDictionary = new Dictionary<ThingDef, int>();
            var thingDictionary = new Dictionary<ThingDef, List<Thing>>();
            var ruleDictionary = new Dictionary<TradeRule, List<Thing>>();

            var junk = allItems.Where(x => x.Map.designationManager.DesignationOn(x, MGDesignatorDefOf.MGAutoSell) != null).ToList();
            junk.ForEach(x => allItems.Remove(x));
            var junkGrouped = junk.GroupBy(x => x.def).ToList();

            thingDictionary.AddRange(junkGrouped.ToDictionary(x => x.Key,
                x => x.ToList()));

            foreach (var rule in comp.tradeRules.Where(x => x is { Enabled: true, AllowSell: true } && x.search.Children.queries.Any()))
            {
                var items = allItems.Where(x => rule.search.AppliesTo(x)).ToList();
                ruleDictionary[rule] = items;

                items.ForEach(x =>
                {
                    allItems.Remove(x);
                });

                var itemsGrouped = items
                    .GroupBy(x => x.def).ToList();

                foreach (var (thingDef, list) in itemsGrouped.ToDictionary(x => x.Key, x => x.ToList()))
                {
                    if(!thingDictionary.TryAdd(thingDef, list))
                        thingDictionary[thingDef].AddRange(list);

                    sellDictionary.TryAdd(thingDef, rule.SellDownTo);
                }
            }

            var stat = StatDefOf.TradePriceImprovement;
            var socialPawn = Find.CurrentMap.mapPawns.FreeColonists
                .Where(x => x.RaceProps.Humanlike && !stat.Worker.IsDisabledFor(x))
                .MaxBy(x => x.GetStatValue(stat));

            var traderPriceType = PriceType.Normal.PriceMultiplier();
            var playerNegotiator = socialPawn.GetStatValue(StatDefOf.TradePriceImprovement);
            var settlement = socialPawn.TradePriceImprovementOffsetForPlayer;
            var drugBonus = socialPawn.GetStatValue(StatDefOf.DrugSellPriceImprovement);
            var animalProduceBonus = socialPawn.GetStatValue(StatDefOf.AnimalProductsSellImprovement);
            thingDictionary.RemoveAll(x => !x.Value.Any());
            var sellEntries = thingDictionary.Select(x =>
            {
                var (thingDef, items) = x;
                var humanPawn = items.FirstOrDefault() is Pawn pawn && pawn.RaceProps.Humanlike ? 0.6f : 1f;
                var priceTotal = items.Select(y => TradeUtility.GetPricePlayerSell(y, traderPriceType, humanPawn, playerNegotiator, settlement, drugBonus, animalProduceBonus) * y.stackCount).Sum();
                var itemsTotal = items.Sum(x => x.stackCount);
                var pricePer = priceTotal / itemsTotal;
                var sellDown = sellDictionary.TryGetValue(thingDef);

                if (itemsTotal <= sellDown)
                    return null;

                priceTotal -= pricePer * sellDown;
                itemsTotal -= sellDown;

                return new SellRecord(thingDef, itemsTotal, (float)Math.Round(priceTotal, 0), (priceTotal/itemsTotal).ToStringMoney());
            })
            .Where(x => x != null)
            .OrderByDescending(x => x.Total)
            .ToList();


            // TODO Hmmm ok, this is too dense...
            sellCache = new ItemsToSell(sellEntries, (float)Math.Round(sellEntries.Sum(x => x.Total), 0),
                new TraderRecord(socialPawn.Name.ToStringFull,
                    PortraitsCache.Get(socialPawn, new Vector2(24, 24), Rot4.South,
                        ColonistBarColonistDrawer.PawnTextureCameraOffset, 1.28205f),
                    playerNegotiator.ToStringPercent()),
                ruleDictionary.ToDictionary(x => x.Key,
                    x => x.Value.GroupBy(y => y.def)
                        .Select(y => new RuleRecord(y.Key, y.ToList().Sum(z => z.stackCount))).ToList()));

            nextCache = Find.TickManager.TicksGame + 3600;
            nextQuickCache = DateTime.UtcNow.AddSeconds(1).Ticks;

            var duration = Stopwatch.GetTimestamp() - timestamp;
            Log.Message($"Generated list in {duration}ts");
        }

        public TradeRule SelectedTradeRule;
        public void DoEdit(TradeRule tradeRule)
        {
            editor?.PostClose();

            if (SelectedTradeRule == tradeRule)
            {
                editor = null;
                SelectedTradeRule = null;
            }
            else
            {
                editor = new TradeRuleEditor(tradeRule);
                SelectedTradeRule = tradeRule;
            }
        }

        public void CreateRule()
        {
            Find.WindowStack.Add(new Dialog_Name("TD.NewAlert".Translate(), n =>
                {
                    TradeRule tradeRule = new(n);
                    comp.tradeRules.Add(tradeRule);

                    editor = new TradeRuleEditor(tradeRule);
                },
                "TD.NameForNewAlert".Translate(),
                name => comp.tradeRules.Any(x => name == x.Search.name)));
        }

#if DEBUG
        public static float Median(List<long> values)
        {
            if (values == null || values.Count == 0)
                throw new System.InvalidOperationException("Cannot compute median for empty list.");

            var sorted = values.OrderBy(x => x).ToList();
            int count = sorted.Count;

            if (count % 2 == 1)
            {
                // Odd
                return sorted[count / 2];
            }
            else
            {
                // Even
                return (sorted[(count / 2) - 1] + sorted[count / 2]) / 2f;
            }
        }
    }

    [HarmonyPatch(typeof(Game), nameof(Game.LoadGame))]
    public static class Patch_OpenTabAfterLoadGame
    {
        public static void Postfix()
        {
            if (!Prefs.DevMode) return; // only in dev
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                // Make sure we actually have a map and the UI is initialized.
                if (Current.ProgramState != ProgramState.Playing) return;
                if (Find.CurrentMap == null) return;

                // Pick the tab you want.
                var def = MainTabDefOf.FindAndAutoSell; // e.g. MainButtonDefOf.Assign, Architect, Research, etc.
                Find.MainTabsRoot.SetCurrentTab(def, playSound: false);

                CloseDevConsole();

                // Optional: if you want the actual window instance:
                // var window = Find.MainTabsRoot.OpenTab?.TabWindow;
            });
        }

        private static void CloseDevConsole()
        {
            if (Find.WindowStack == null) return;

            var logWindow = Find.WindowStack.Windows
                .FirstOrDefault(w => w is EditWindow_Log);

            if (logWindow != null)
            {
                Find.WindowStack.TryRemove(logWindow);
            }
        }
    }

    [DefOf]
    public static class MainTabDefOf
    {
        public static MainButtonDef FindAndAutoSell;
    }
#endif
}
