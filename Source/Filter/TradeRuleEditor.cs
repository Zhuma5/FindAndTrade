using System;
using System.Collections.Generic;
using MGAutoSell.Extensions;
using TD_Find_Lib;
using UnityEngine;
using Verse;

namespace MGAutoSell.Filter
{
    internal class TradeRuleEditor : SearchEditorRevertableWindow
    {
        private readonly TradeRule _tradeRule;
        private static string _aggByDef = "MGAutoSell.AggByDef".Translate(), _aggByRule = "MGAutoSell.AggByRule".Translate();

        public TradeRuleEditor(TradeRule tradeRule) : base(tradeRule.Search, "Trade Rule")
        {
            _tradeRule = tradeRule;
        }

        public override void DoWindowContents(Rect fillRect)
        {
            if (!this.locked && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.V && Event.current.control)
            {
                ClipboardTransfer clipboardTransfer = new ClipboardTransfer();
                if (clipboardTransfer.ProvideMethod() == ISearchProvider.Method.Single)
                {
                    this.Import(clipboardTransfer.ProvideSingle().Clone(this.ImportArgs));
                    Event.current.Use();
                }
            }
            Draw(fillRect);
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.C && Event.current.control)
            {
                new ClipboardTransfer().Receive(this.filter);
                Event.current.Use();
            }
        }

        protected override void DrawHeader(Rect headerRect)
        {
            var search = filter as QuerySearch;
            var rect1 = headerRect.LeftPart(0.4f);
            if (search != null)
            {
                Widgets.Label(rect1, "TD.Listing".Translate() + search.ListType.TranslateEnum());
                if (!locked)
                {
                    Widgets.DrawHighlightIfMouseover(rect1);
                    if (Widgets.ButtonInvisible(rect1))
                    {
                        var options = new List<FloatMenuOption>();
                        foreach (SearchListType searchListType in DebugSettings.godMode
                                     ? Enum.GetValues(typeof(SearchListType))
                                     : SearchListNormalTypes.normalTypes)
                        {
                            var type = searchListType;
                            if (DebugSettings.godMode || type < SearchListType.Haulables)
                            {
                                if (Event.current.control)
                                    options.Add(new FloatMenuOption(type.TranslateEnum(), () =>
                                        {
                                            if (Event.current.shift)
                                                search.SetListType(type);
                                            else
                                                search.ToggleListType(type);
                                        },
                                        search.ListType.HasFlag(type)
                                            ? Widgets.CheckboxOnTex
                                            : Widgets.CheckboxOffTex, Color.white));
                                else
                                    options.Add(new FloatMenuOption(type.TranslateEnum(),
                                        () => search.SetListType(type)));
                            }
                        }

                        Find.WindowStack.Add(new FloatMenu(options));
                    }
                }
            }

            var rect2 = headerRect.RightPart(0.6f).LeftPart(0.5f);
            Widgets.Label(rect2,
                filter.MatchAllQueries ? "TD.MatchingAllFilters".Translate() : "TD.MatchingAnyFilter".Translate());
            if (!locked)
            {
                Widgets.DrawHighlightIfMouseover(rect2);
                if (Widgets.ButtonInvisible(rect2))
                    filter.MatchAllQueries = !filter.MatchAllQueries;
            }

            if (search == null)
                return;

            var rect3 = headerRect.RightPart(0.3f);
            var label = _tradeRule.Aggregation switch
            {
                TradeRuleAggregation.ThingDef => _aggByDef,
                TradeRuleAggregation.Rule => _aggByRule,
                _ => throw new ArgumentOutOfRangeException()
            };

            Widgets.Label(rect3, label);
            if (!locked)
            {
                Widgets.DrawHighlightIfMouseover(rect3);
                if (Widgets.ButtonInvisible(rect3))
                    _tradeRule.Aggregation = _tradeRule.Aggregation.Next();
            }
        }
    }
}
