using System;
using System.Collections;
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
using UnityEngine.UIElements;
using Verse;
using Verse.Noise;
using static HarmonyLib.Code;

namespace MGAutoSell
{
    public record ItemsToSell(List<SellRecord> Items, float TotalSilver, string TotalSilverLabel, TraderRecord Trader, Dictionary<TradeRule, List<RuleRecord>> Rules);
    public record SellRecord(ThingDef Item, int Count, float Total, string PricePerLabel, string TotalLabel);
    public record TraderRecord(Pawn pawn, string Name, Texture2D Icon, string ImprovementLabel, float Improvement);
    public record RuleRecord(ThingDef Item, int Count);
    public class MainTabWindow_FindAndAutoSell : MainTabWindow
    {
        public ItemsToSell sellCache;

        private TradeRulesGameComp comp;
        private TradeRuleEditor editor;

        private Pawn SellerOverride;

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
        private int reorderID;
        private int reorderRectHeight;

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
            if (DateTimeOffset.Now.ToUnixTimeSeconds() > nextPerformance && ticks.Any())
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
                if (Event.current.type == EventType.Repaint)
                    reorderID = ReorderableWidget.NewGroup(DoReorderSearch, ReorderableDirection.Vertical,
                        new Rect(0.0f, -30, drawerListing.ColumnWidth, height + 30), 1f,
                        (index, _) =>
                            DrawMouseAttachedQuerySearch(comp.tradeRules[index].Search, drawerListing.ColumnWidth));

                for (var index = 0; index < comp.tradeRules.Count; index++)
                {
                    var tradeRule = comp.tradeRules[index];
                    var action = TradeRuleDrawUtility.DrawRow(drawerListing.GetRect(30), tradeRule, index, sellCache, reorderID);
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

            Widgets.Label(itemHeader.RightPartPixels(itemHeader.width - Text.LineHeight - 10), "Items to Sell");
            GUI.DrawTexture(itemHeader.RightPartPixels(24f), ThingDefOf.Silver.uiIcon);

            toSellRect.SplitHorizontally(4, out var gapHeader, out toSellRect);
            Widgets.DrawLineHorizontal(gapHeader.x, gapHeader.y, gapHeader.width, fadedColor);

            int i = 0;
            var anchor = Text.Anchor;
            foreach (var (thingDef, count, total, pricePerLabel, totalLabel) in sellCache.Items)
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

                var middle = row.MiddlePartPixels(50, row.height);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(middle, pricePerLabel);
                Text.Anchor = anchor;

                var size = Text.CalcSize(totalLabel);
                Widgets.Label(row.RightPartPixels(size.x + 4), totalLabel);
            }

            var footer = toSellRect.BottomPartPixels(Text.LineHeight);
            Widgets.DrawLightHighlight(footer);
            var iconRect = footer.LeftPartPixels(Text.LineHeight);
            iconRect.y -= 4;
            GUI.DrawTexture(iconRect, sellCache.Trader.Icon);

            var sellerLabel = sellCache.Trader.Name + $" ({sellCache.Trader.ImprovementLabel})";
            if (SellerOverride != null)
                sellerLabel = $"<i>{sellerLabel}</i>";
            var sellerLabelWidth = Text.CalcSize(sellerLabel);
            Widgets.Label(footer.RightPartPixels(footer.width - Text.LineHeight), sellerLabel);
            var sellerOverrideRect = footer.LeftPartPixels(iconRect.width + sellerLabelWidth.x + 8);
            Widgets.DrawHighlightIfMouseover(sellerOverrideRect);
            if (Widgets.ButtonInvisible(sellerOverrideRect) && Event.current.button == (int)MouseButton.RightMouse)
            {
                var pawns = GetTraders().Select(x => new FloatMenuOption(
                    x.Name + $" ({x.ImprovementLabel})", () =>
                    {
                        SellerOverride = x.pawn;
                        nextCache = 0;
                    }, x.pawn, Color.white)).ToList();

                if(SellerOverride != null)
                    pawns.Add(new FloatMenuOption("Auto", () =>
                    {
                        SellerOverride = null;
                        nextCache = 0;
                    }));

                Find.WindowStack.Add(new FloatMenu(pawns));
            }

            var footerRow = new WidgetRow(footer.xMax - 4, footer.y, UIDirection.LeftThenDown);
            footerRow.Label(sellCache.TotalSilverLabel);
            footerRow.Icon(ThingDefOf.Silver.uiIcon);
            footerRow.Label("Total:");
            Text.Font = font;
#if DEBUG
            ticks.Add(Stopwatch.GetTimestamp() - timestamp);
#endif
        }

        public List<TraderRecord> GetTraders()
        {
            var stat = StatDefOf.TradePriceImprovement;
            var pawns = Find.CurrentMap.mapPawns.FreeColonists
                .Where(pawn => pawn.RaceProps.Humanlike && !stat.Worker.IsDisabledFor(pawn))
                .Select(pawn =>
                {
                    var improvement = pawn.GetStatValue(stat) + (ModsConfig.IdeologyActive && pawn == Faction.OfPlayer.leader ? 0.02f : 0f);
                    return new TraderRecord(pawn, pawn.Name.ToStringFull,
                        PortraitsCache.Get(pawn, new Vector2(24, 24), Rot4.South,
                            ColonistBarColonistDrawer.PawnTextureCameraOffset, 1.28205f).CreateTexture2D(),
                        improvement.ToStringPercent(), improvement);
                })
                .OrderByDescending(x => x.Improvement)
                .ToList();
            return pawns;
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

                    sellDictionary.TryAdd(thingDef, rule.Export);
                }
            }

            var socialPawn = SellerOverride ?? GetTraders().MaxBy(x => x.Improvement).pawn;

            var traderPriceType = PriceType.Normal.PriceMultiplier();
            var playerNegotiator = socialPawn.GetStatValue(StatDefOf.TradePriceImprovement);
            var leaderBonus = socialPawn == Faction.OfPlayer.leader ? 0.02f : 0f;
            var settlement = socialPawn.TradePriceImprovementOffsetForPlayer;
            var drugBonusRaw = socialPawn.GetStatValue(StatDefOf.DrugSellPriceImprovement);
            var animalProduceBonusRaw = ModsConfig.IdeologyActive ? socialPawn.GetStatValue(StatDefOf.AnimalProductsSellImprovement) : 0f;
            var totalNegotiator = playerNegotiator + leaderBonus;
            thingDictionary.RemoveAll(x => !x.Value.Any());
            var sellEntries = thingDictionary.Select(x =>
            {
                var (thingDef, items) = x;
                var drugBonus = thingDef.IsNonMedicalDrug ? drugBonusRaw : 0f;
                var animalProduceBonus = (thingDef.IsLeather || thingDef.IsMeat || thingDef.IsWool) ? animalProduceBonusRaw : 0f;
                var humanPawn = ModsConfig.IdeologyActive && items.FirstOrDefault() is Pawn pawn && pawn.RaceProps.Humanlike ? 0.6f : 1f;
                var priceTotal = items.Select(y => TradeUtility.GetPricePlayerSell(y, traderPriceType, humanPawn, totalNegotiator, settlement, drugBonus, animalProduceBonus) * y.stackCount).Sum();
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

            // TODO Hmmm ok, this is too dense...
            var totalSilver = (float)Math.Round(sellEntries.Sum(x => x.Total), 0);
            sellCache = new ItemsToSell(
                Items: sellEntries,

                TotalSilver: totalSilver,
                TotalSilverLabel: totalSilver.ToStringMoney(),

                Trader: new TraderRecord(socialPawn,
                    socialPawn.Name.ToStringFull,
                    PortraitsCache.Get(socialPawn, new Vector2(24, 24), Rot4.South, ColonistBarColonistDrawer.PawnTextureCameraOffset, 1.28205f).CreateTexture2D(),
                    playerNegotiator.ToStringPercent(), playerNegotiator),

                Rules: ruleDictionary.ToDictionary(x => x.Key,
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

        private protected virtual void DoReorderSearch(int from, int to)
        {
            var obj = comp.tradeRules[from];
            comp.tradeRules.RemoveAt(from);
            comp.tradeRules.Insert(from < to ? to - 1 : to, obj);
        }

        public static void DrawMouseAttachedQuerySearch(QuerySearch search, float width)
        {
            Find.WindowStack.ImmediateWindow(34003428, new Rect(Event.current.mousePosition + Vector2.one * 12f, new Vector2(width, Text.LineHeight)), WindowLayer.Super, (Action)(() => Widgets.Label(new Rect(0.0f, 0.0f, width, Text.LineHeight), search.name)), false, shadowAlpha: 0.0f);
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
