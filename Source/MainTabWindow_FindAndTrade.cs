using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;
using LudeonTK;
using MGAutoSell.Extensions;
using MGAutoSell.Filter;
using RimWorld;
using TD_Find_Lib;
using UnityEngine;
using UnityEngine.UIElements;
using Verse;

namespace MGAutoSell
{
    public record ItemsToSell(
        List<SellRecord> Items,
        List<ThingDef> PotentialItems,

        float TotalSilver,
        string TotalSilverLabel,
        TraderRecord Trader,
        Dictionary<TradeRule, string> Rules);

    public record SellRecord(ThingDef Item, int Count, float Total, string PricePerLabel, string TotalLabel);

    public record TraderRecord(Pawn Pawn, string Name, Func<Texture> Icon, string ImprovementLabel, float Improvement, bool IsLeader);

    public record RuleRecord(ThingDef Item, int Count);

    public class MainTabWindow_FindAndTrade : MainTabWindow
    {
        public ItemsToSell sellCache;
        private List<TraderRecord> tradersCache;

        private TradeRulesGameComp comp;
        private TradeRuleEditor editor;

        private Pawn SellerOverride;

        private long nextCache = 0;
        private long nextQuickCache = 0;

        Vector2 listerScroll = Vector2.zero;
        Vector2 settingScroll = Vector2.zero;
        Vector2 sellScroll = Vector2.zero;
        private string previousRenderTime;

        private string title = $"<i>{"MGAutoSell.Title".Translate()}</i>";
        private string tradeAutomaticallyLabel = "MGAutoSell.AutoSellToggle".Translate();

#if DEBUG
        List<long> ticks = new List<long>();
        private long nextPerformance = 0;
#endif
        private List<Thing> itemCache;

        public override Vector2 RequestedTabSize => new(1010f, 300f);
        protected override float Margin => 8f;
        public bool SellListDirty => editor != null;
        private int reorderID;

        private WindowTab currentTab = WindowTab.Rules;

        private Color _fadedColor = new(1, 1, 1, 0.4f);

        public MainTabWindow_FindAndTrade()
        {
            preventCameraMotion = false;
            doCloseX = true;
        }

        public override void PreOpen()
        {
            base.PreOpen();
            comp = Current.Game.GetComponent<TradeRulesGameComp>();
        }

        public override void PostClose()
        {
            editor?.PostClose();
            editor = null;
            SelectedTradeRule = null;
            currentTab = WindowTab.Rules;
            tradersCache = null;
            nextCache = 0;
            base.PostClose();
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
            var font = Text.Font;
            Text.Font = GameFont.Small;

            var width = currentTab == WindowTab.Edit ? 600f : 400f;
            var leftPanel = inRect.LeftPartPixels(width);

            switch (currentTab)
            {
                case WindowTab.Edit:
                    DrawEditTab(leftPanel);
                    break;
                case WindowTab.Settings:
                {
                    leftPanel.SplitHorizontally(30, out var header, out var body);
                    DrawTitle(header);
                    DrawSettingsTab(body);
                }
                    break;
                case WindowTab.Rules:
                default:
                {
                    leftPanel.SplitHorizontally(30, out var header, out var body);
                    DrawTitle(header, true);
                    DrawRulesTab(body);
                }
                    break;
            }

            TryCacheItemsToSell();

            var rightPanel = inRect.RightPartPixels(inRect.width - leftPanel.width - 12 - 16);
            rightPanel.x -= 16;

            GUI.color = _fadedColor;
            Widgets.DrawLineVertical(leftPanel.width + 6, 0, 300f);
            GUI.color = color;

            DrawSellPanel(rightPanel);

            Text.Font = font;
#if DEBUG
            ticks.Add(Stopwatch.GetTimestamp() - timestamp);
#endif
        }

        private void DrawTitle(Rect headerRect, bool showSettingsIcon = false)
        {
            var color = GUI.color;
            headerRect.y -= 4;
            Text.Font = GameFont.Medium;
            GUI.color = _fadedColor;
            Widgets.Label(headerRect, title);
            Text.Font = GameFont.Small;
            GUI.color = color;
            headerRect.y += 4;

            GUI.color = _fadedColor;
            Widgets.DrawLineHorizontal(headerRect.x, headerRect.yMax - 2, headerRect.width);
            GUI.color = color;

            if (!showSettingsIcon || !Widgets.ButtonImage(
                    headerRect.TopPartPixels(Text.LineHeight).RightPartPixels(Text.LineHeight),
                    Textures.OptionsGeneral, _fadedColor)) return;

            tradersCache = GetTraders();
            currentTab = WindowTab.Settings;
        }

        private void DrawEditTab(Rect panel)
        {
            var buttonRect = panel.BottomPartPixels(30f).LeftPartPixels(30f);
            editor!.DoWindowContents(panel);
            if (!Widgets.ButtonImage(buttonRect, TexButton.Banish)) 
                return;

            currentTab = WindowTab.Rules;
            editor?.PostClose();
            editor = null;
            SelectedTradeRule = null;
        }

        private void DrawSettingsTab(Rect panel)
        {
            panel.SplitHorizontally(panel.height - 30, out var body, out var footer);
            var buttonRect = footer.LeftPartPixels(30f);
            if (Widgets.ButtonImage(buttonRect, TexButton.Banish))
            {
                tradersCache = null;
                currentTab = WindowTab.Rules;
                return;
            }

            body.SplitHorizontally(Text.LineHeight, out var autoSellRect, out body);
            body.SplitHorizontally(4f, out var gap, out var drawer);
            
            Widgets.CheckboxLabeled(autoSellRect, tradeAutomaticallyLabel, ref comp.autoTrade);

            var color = GUI.color;
            GUI.color = _fadedColor;
            Widgets.DrawLineHorizontal(gap.x + 5, gap.y + 2, gap.width - 10);
            GUI.color = color;

            var spacePerRow = 24;
            var totalHeight = tradersCache.Count * spacePerRow;
            var shouldScroll = totalHeight > drawer.height;
            var listing = new Listing_StandardIndent();
            if (shouldScroll)
                listing.BeginScrollView(drawer, ref settingScroll, drawer.LeftPartPixels(drawer.width - 16).TopPartPixels(totalHeight).AtZero());
            else
                listing.Begin(drawer);

            for (var i = 0; i < tradersCache.Count; i++)
            {
                var trader = tradersCache[i];
                var enabled = comp.autoTraderIDs.Contains(trader.Pawn.thingIDNumber);
                var prev = enabled;
                var rect = listing.GetRect(spacePerRow);
                if (i % 2 == 1)
                    Widgets.DrawLightHighlight(rect);

                rect.SplitVertically(spacePerRow, out var iconRect, out rect);
                rect.SplitVertically(rect.width / 2, out var labelRect, out rect);

                iconRect.y -= 4;
                GUI.DrawTexture(iconRect, trader.Icon.Invoke());

                Widgets.Label(labelRect, $"{trader.Name} ({trader.ImprovementLabel})");

                rect.SplitVertically(rect.width - spacePerRow, out rect, out var checkboxRect);
                Widgets.Checkbox(checkboxRect.x, checkboxRect.y, ref enabled, spacePerRow, paintable: true);
                if (enabled != prev)
                {
                    if (enabled)
                        comp.autoTraderIDs.Add(trader.Pawn.thingIDNumber);
                    else
                        comp.autoTraderIDs.Remove(trader.Pawn.thingIDNumber);
                }

                if (trader.IsLeader)
                {
                    var role = trader.Pawn.Ideo.GetRole(trader.Pawn);
                    if(role != null)
                    {
                        GUI.color = trader.Pawn.ideo.Ideo.Color;
                        rect.SplitVertically(rect.width - spacePerRow, out rect, out var leaderIconRect);
                        GUI.DrawTexture(leaderIconRect, role.Icon);
                        TooltipHandler.TipRegion(leaderIconRect, role.TipLabel);
                        GUI.color = color;
                    }
                }

                
            }

            var height = 0f;
            if(shouldScroll)
                listing.EndScrollView(ref height);
            else
                listing.End();
        }

        private void DrawRulesTab(Rect panel)
        {
            var height = 300f;
            var body = panel.TopPartPixels(panel.height - 30);

            var drawerListing = new Listing_StandardIndent();
            drawerListing.BeginScrollView(body, ref listerScroll,
                body.LeftPartPixels(body.width - 16).TopPartPixels(comp.tradeRules.Count * 30).AtZero());

            if (Event.current.type == EventType.Repaint)
                reorderID = ReorderableWidget.NewGroup(DoReorderSearch, ReorderableDirection.Vertical,
                    new Rect(0.0f, -30, drawerListing.ColumnWidth, height + 30), -1f,
                    (index, _) =>
                        DrawMouseAttachedQuerySearch(comp.tradeRules[index].Search, drawerListing.ColumnWidth));

            for (var index = 0; index < comp.tradeRules.Count; index++)
            {
                var tradeRule = comp.tradeRules[index];
                var action = TradeRuleDrawUtility.DrawRow(drawerListing.GetRect(30), tradeRule, index, sellCache,
                    reorderID);
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

            var controlsRect = panel.BottomPartPixels(Text.LineHeight);
            if (Widgets.ButtonImage(controlsRect.LeftPartPixels(Text.LineHeight), FindTex.GreyPlus))
                CreateRule();
#if DEBUG
            var color = GUI.color;
            GUI.color = _fadedColor;
            var controls = new WidgetRow(controlsRect.xMax, controlsRect.yMax - Text.LineHeight, UIDirection.LeftThenDown);
            controls.Label($"<i> Render: {previousRenderTime}</i>");
            GUI.color = color;
#endif
        }
        
        private void DrawSellPanel(Rect toSellRect)
        {
            toSellRect.SplitHorizontally(Text.LineHeight, out var itemHeader, out toSellRect);

            Widgets.DrawLightHighlight(itemHeader);
            Widgets.Label(itemHeader.RightPartPixels(itemHeader.width - Text.LineHeight - 10), "Items to Sell");
            GUI.DrawTexture(itemHeader.RightPartPixels(24f), ThingDefOf.Silver.uiIcon);

            toSellRect.SplitHorizontally(4, out var gapHeader, out toSellRect);
            Widgets.DrawLineHorizontal(gapHeader.x, gapHeader.y, gapHeader.width, _fadedColor);

            int i = 0;
            var anchor = Text.Anchor;
            var totalItems = sellCache.Items.Count + (Mod.Settings.showAllMatchingItems ? sellCache.PotentialItems.Count : 0);
            var viewRect = toSellRect.TopPartPixels(toSellRect.height - Text.LineHeight);
            var totalHeight = totalItems * Text.LineHeight;
            var shouldScroll = totalHeight > viewRect.height;
            var row = new Rect(0, 0, 0, 0);
            var listing = new Listing_StandardIndent();
            if(shouldScroll)
            {
                viewRect.width += 16;
                listing.BeginScrollView(viewRect, ref sellScroll, viewRect.LeftPartPixels(viewRect.width - 16).TopPartPixels(totalHeight).AtZero() );
            }

            foreach (var (thingDef, count, total, pricePerLabel, totalLabel) in sellCache.Items)
            {
                if (shouldScroll)
                    row = listing.GetRect(Text.LineHeight);
                else
                    viewRect.SplitHorizontally(Text.LineHeight, out row, out viewRect);

                if (i % 2 == 1)
                    Widgets.DrawLightHighlight(row);
                i++;
                var color = GUI.color;
                GUI.color = thingDef.uiIconColor;
                GUI.DrawTexture(row.LeftPartPixels(row.height), thingDef.uiIcon);
                GUI.color = color;

                row.x += row.height + 10;
                Widgets.Label(row, thingDef.GetLabel() + $" x{count}");
                row.x -= row.height + 10;

                if (currentTab != WindowTab.Edit)
                {
                    var middle = row.MiddlePartPixels(50, row.height);
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(middle, pricePerLabel);
                    Text.Anchor = anchor;
                }

                var size = Text.CalcSize(totalLabel);
                Widgets.Label(row.RightPartPixels(size.x + 4), totalLabel);
            }

            if (Mod.Settings.showAllMatchingItems)
            {
                foreach (var thingDef in sellCache.PotentialItems)
                {
                    if (shouldScroll)
                        row = listing.GetRect(Text.LineHeight);
                    else
                        viewRect.SplitHorizontally(Text.LineHeight, out row, out viewRect);

                    if (i % 2 == 1)
                        Widgets.DrawLightHighlight(row);
                    i++;
                    var color = GUI.color;
                    GUI.color = thingDef.uiIconColor;
                    GUI.DrawTexture(row.LeftPartPixels(row.height), thingDef.uiIcon);
                    GUI.color = _fadedColor;

                    row.x += row.height + 10;
                    Widgets.Label(row, thingDef.GetLabel());
                    row.x -= row.height + 10;
                    GUI.color = color;
                }
            }

            if (shouldScroll)
            {
                var height = 0f;
                listing.EndScrollView(ref height);
            }

            var footer = toSellRect.BottomPartPixels(Text.LineHeight);
            Widgets.DrawLightHighlight(footer);

            var iconRect = footer.LeftPartPixels(Text.LineHeight);
            iconRect.y -= 4;
            GUI.DrawTexture(iconRect, sellCache.Trader.Icon.Invoke());

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
                        SellerOverride = x.Pawn;
                        nextCache = 0;
                    }, x.Pawn, Color.white)).ToList();

                if (SellerOverride != null)
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
        }

        public List<TraderRecord> GetTraders(bool generatePictures = true)
        {
            var stat = StatDefOf.TradePriceImprovement;
            var pawns = Find.CurrentMap.mapPawns.FreeColonists
                .Where(pawn => pawn.RaceProps.Humanlike && !stat.Worker.IsDisabledFor(pawn))
                .Select(pawn =>
                {
                    var isLeader = ModsConfig.IdeologyActive && pawn == Faction.OfPlayer.leader;
                    var improvement = pawn.GetStatValue(stat);

                    return new TraderRecord(pawn, pawn.LabelShort,
                        generatePictures ? () => PortraitsCache.Get(pawn, new Vector2(24, 24), Rot4.South,
                            ColonistBarColonistDrawer.PawnTextureCameraOffset, 1.28205f) : () => null,
                        improvement.ToStringPercent(), improvement, isLeader);
                })
                .OrderByDescending(x => x.IsLeader)
                .ThenByDescending(x => x.Improvement)
                .ToList();
            return pawns;
        }

        public void TryCacheItemsToSell(bool force = false)
        {
            var shouldUpdate = force || (SellListDirty && nextQuickCache < DateTime.UtcNow.Ticks) ||
                               nextCache < Find.TickManager.TicksGame;

            if (!shouldUpdate)
                return;

            CacheItemsToSell();
        }

        public void CacheItemsToSell()
        {
            var timestamp = Stopwatch.GetTimestamp();
            var allItems = TradeUtility.AllLaunchableThingsForTrade(Find.CurrentMap).ToList();
            allItems.AddRange(TradeUtility.AllSellableColonyPawns(Find.CurrentMap, false).ToList());
            var sellDictionary = new Dictionary<ThingDef, int>();
            var thingDictionary = new Dictionary<ThingDef, List<Thing>>();
            var ruleDictionary = new Dictionary<TradeRule, List<Thing>>();

            var junk = allItems
                .Where(x => x.Map.designationManager.DesignationOn(x, MGDesignatorDefOf.MGAutoSell) != null).ToList();
            junk.ForEach(x => allItems.Remove(x));
            var junkGrouped = junk.GroupBy(x => x.def).ToList();

            thingDictionary.AddRange(junkGrouped.ToDictionary(x => x.Key,
                x => x.ToList()));

            foreach (var rule in comp.tradeRules.Where(x =>
                         x is { Enabled: true, AllowSell: true } && x.search.Children.queries.Any()))
            {
                var items = allItems.Where(x => rule.search.AppliesTo(x)).ToList();
                if(items.Any())
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

            var traders = GetTraders(false);
            if (comp.autoTrade)
            {
                var allowedTraders = traders.Where(x => comp.autoTraderIDs.Contains(x.Pawn.thingIDNumber)).ToList();
                if (allowedTraders.Any())
                    traders = allowedTraders;
            }

            var socialPawn = SellerOverride ?? traders.MaxBy(x => x.Improvement).Pawn;

            var traderPriceType = PriceType.Normal.PriceMultiplier();
            var playerNegotiator = socialPawn.GetStatValue(StatDefOf.TradePriceImprovement);
            var isLeader = ModsConfig.IdeologyActive && socialPawn == Faction.OfPlayer.leader;
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

            // TODO Hmmm ok, this is too dense...
            itemCache ??= DefDatabase<ThingDef>.AllDefsListForReading.Select(y => ThingMaker.MakeThing(y, y.MadeFromStuff ? GenStuff.DefaultStuffFor(y) : null)).ToList();
            var potentialItems = itemCache.Where(x => comp.tradeRules.Any(y => y.Enabled && y.search.Children.queries.Any() && y.AllowBuy &&  y.search.AppliesTo(x))).Select(x => x.def).ToList();
            potentialItems.RemoveAll(x => sellEntries.Any(y => y.Item == x));
            var totalSilver = (float)Math.Round(sellEntries.Sum(x => x.Total), 0);

            var ruleCounts = Mod.Settings.showQuanityInsteadOfLabel
                ? ruleDictionary.ToDictionary(x => x.Key,
                    x => "x" + (x.Key.Aggregation == TradeRuleAggregation.ThingDef
                        ? x.Value.GroupBy(y => y.def)
                            .Select(y => new RuleRecord(y.Key, y.ToList().Sum(z => z.stackCount)))
                            .Max(x => x.Count).ToString()
                        : x.Value.Sum(y => y.stackCount).ToString()))
                : [];

            sellCache = new ItemsToSell(
                Items: sellEntries,
                PotentialItems: potentialItems,
                TotalSilver: totalSilver,
                TotalSilverLabel: totalSilver.ToStringMoney(),

                Trader: new TraderRecord(socialPawn,
                    socialPawn.LabelShort,
                    () => PortraitsCache.Get(socialPawn, new Vector2(24, 24), Rot4.South,
                        ColonistBarColonistDrawer.PawnTextureCameraOffset, 1.28205f),
                    playerNegotiator.ToStringPercent(), playerNegotiator, isLeader),

                Rules: ruleCounts);
            sellCache.Rules.RemoveAll(x => string.IsNullOrWhiteSpace(x.Value));

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
                currentTab = WindowTab.Rules;
                SelectedTradeRule = null;
            }
            else
            {
                editor = new TradeRuleEditor(tradeRule);
                currentTab = WindowTab.Edit;
                SelectedTradeRule = tradeRule;
            }
        }

        public void CreateRule()
        {
            Find.WindowStack.Add(new Dialog_Name("MGAutoSell.NameForNew".Translate(), n =>
                {
                    TradeRule tradeRule = new(n);
                    comp.tradeRules.Add(tradeRule);

                    editor = new TradeRuleEditor(tradeRule);
                    currentTab = WindowTab.Edit;
                },
                "MGAutoSell.NewTradeRule".Translate(),
                name => comp.tradeRules.Any(x => name == x.Search.name)));
        }

        private protected virtual void DoReorderSearch(int from, int to)
        {
            var obj = comp.tradeRules[from];
            comp.tradeRules.RemoveAt(from);
            comp.tradeRules.Insert(from < to ? to - 1 : to, obj);
        }

        public static void DrawMouseAttachedQuerySearch(QuerySearch search, float width)
        {
            Find.WindowStack.ImmediateWindow(34003428,
                new Rect(Event.current.mousePosition + Vector2.one * 12f, new Vector2(width, Text.LineHeight)),
                WindowLayer.Super,
                (Action)(() => Widgets.Label(new Rect(0.0f, 0.0f, width, Text.LineHeight), search.name)), false,
                shadowAlpha: 0.0f);
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
#endif

    }

#if DEBUG
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
                var def = MainTabDefOf.FindAndTrade; // e.g. MainButtonDefOf.Assign, Architect, Research, etc.
                Find.MainTabsRoot.SetCurrentTab(def, playSound: false);

                //CloseDevConsole();

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
#endif

    [DefOf]
    public static class MainTabDefOf
    {
        public static MainButtonDef FindAndTrade;
    }

    public enum WindowTab
    {
        Rules = 0,
        Edit = 1,
        Settings = 2
    }
}