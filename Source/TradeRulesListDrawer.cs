using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TD_Find_Lib;
using Unity.Mathematics;
using UnityEngine;
using Verse;

namespace MGAutoSell
{
    public class TradeRulesListDrawer : SearchGroupDrawerBase<TradeRulesGroup,TradeRule>
    {
        private readonly MainTabWindow_FindAndAutoSell _parent;
        private TradeRulesGameComp comp;

        private Color altBackground = new(0.3f, 0.3f, 0.3f, 0.5f);

        private static readonly Color
            DeepRed = new(1f, 0.0f, 0.0f, 0.6f),
            DeepGreen = new(0.0f, 1f, 0.0f, 0.6f);

        private static readonly string ruleInvalid = "MGAutoSell.RuleInvalid".Translate();

        public TradeRulesListDrawer(TradeRulesGroup list, MainTabWindow_FindAndAutoSell parent) : base(list)
        {
            _parent = parent;
            comp = Current.Game.GetComponent<TradeRulesGameComp>();
        }

        private string editThiSearch = "TD.EditThisSearch".Translate();
        public override void DrawRowButtons(WidgetRow row, TradeRule item, int i)
        {
            if(row.Checkbox(ref item.Enabled))
                item.search.Changed();

            if (row.ButtonIcon(FindTex.Edit, editThiSearch))
                _parent.DoEdit(item);

            if (row.ButtonIcon(FindTex.Trash))
                comp.tradeRules.Remove(item);
        }

        public override void DrawExtraRowRect(Rect rowRect, TradeRule item, int i)
        {
            var second = DateTimeOffset.Now.ToUnixTimeSeconds();
            var original = GUI.color;
            var font = Text.Font;
            var fadedColor = new Color(1, 1, 1, 0.4f);
            var lightFadedColor = new Color(1, 1, 1, 0.6f);
            var invalid = item.Invalid;


            if (item == _parent.SelectedTradeRule)
                Widgets.DrawHighlightSelected(rowRect);

            var rowSell = new WidgetRow(rowRect.xMax, rowRect.y, UIDirection.LeftThenDown);

            var alignment = Text.CurTextFieldStyle.alignment;
            Text.CurTextFieldStyle.alignment = TextAnchor.MiddleCenter;

            var sellDownToRect = rowSell.GetRect(60);
            sellDownToRect.height -= 4;
            sellDownToRect.y += 3;

            var prevSellDown = item.SellDownTo;
            string sellDownToBuffer = null;
            if (invalid)
                GUI.color = DeepRed;
            else if (!item.AllowSell)
                GUI.color = fadedColor;
            Widgets.TextFieldNumeric(sellDownToRect, ref item.SellDownTo, ref sellDownToBuffer);
            if (string.IsNullOrWhiteSpace(sellDownToBuffer))
                item.SellDownTo = 0;
            GUI.color = original;
            if (item.SellDownTo != prevSellDown)
                item.search.changed = true;

            var rowBuyRect = rowRect.RightHalf();
            if(invalid)
                TooltipHandler.TipRegion(rowBuyRect, () => ruleInvalid, 6320498 + i);
            rowBuyRect = rowBuyRect.RightPartPixels(rowBuyRect.width - 20);

            var rowBuy = new WidgetRow(rowBuyRect.x, rowBuyRect.y, UIDirection.RightThenDown);

            var buyUpToRect = rowBuy.GetRect(60);
            buyUpToRect.height -= 4;
            buyUpToRect.y += 3;

            string buyUpToBuffer = null;
            if (invalid)
                GUI.color = DeepRed;
            else if (!item.AllowBuy) 
                GUI.color = fadedColor;
            Widgets.TextFieldNumeric(buyUpToRect, ref item.BuyUpTo, ref buyUpToBuffer);
            if (string.IsNullOrWhiteSpace(buyUpToBuffer))
                item.BuyUpTo = 0;
            GUI.color = original;

            if ((_parent.sellCache?.Rules?.TryGetValue(item, out var entry) ?? false) && entry.Any())
            {
                var outer = rowBuyRect.MiddlePartPixels(rowBuyRect.width - 120, rowBuyRect.height);
                var iconRect = outer.MiddlePartPixels(Text.LineHeight, Text.LineHeight);
                var length = entry.Count;
                var index = (int)(second % length);
                var ruleRecord = entry[index];

                var uiColor = ruleRecord.Item.uiIconColor;
                var mergedColor = new Color(uiColor.r, uiColor.g, uiColor.b, fadedColor.a);
                GUI.color = mergedColor;
                GUI.DrawTexture(iconRect, ruleRecord.Item.uiIcon, ScaleMode.StretchToFill);
                GUI.color = original;

                var countLabel = $"x{(ruleRecord.Count >= 1000 
                    ? (int)Math.Round(ruleRecord.Count / 1000f, 0) + "k" 
                    : ruleRecord.Count.ToString())}";
                Text.Font = GameFont.Tiny;

                float spread;
                if (invalid)
                    spread = 0;
                else if (item.AllowSell && item.SellDownTo == 0)
                    spread = 1;
                else if (item.AllowBuy && !item.AllowSell)
                    spread = -1;
                else
                    spread = ruleRecord.Count switch
                    {
                        var v when v < item.SellDownTo && v > item.BuyUpTo => 0,
                        var v when v > item.SellDownTo => Math.Abs((ruleRecord.Count - (float)item.SellDownTo) / Math.Max(item.SellDownTo, 1f)),
                        var v when v < item.BuyUpTo => -Math.Abs((ruleRecord.Count - item.BuyUpTo / 2f) / Math.Max(item.BuyUpTo, 1f))
                    };

                GUI.color = spread == 0
                    ? lightFadedColor
                    : Color.Lerp(lightFadedColor, spread > 0 ? DeepGreen : DeepRed, 
                        math.clamp(Math.Abs(spread), 0.25f, 1f));

                var labelSize = Text.CalcSize(countLabel);

                var labelRect = outer.RightPartPixels(labelSize.x).BottomPartPixels(labelSize.y);

                Widgets.Label(labelRect, countLabel);
                GUI.color = original;
                Text.Font = font;
            }

            
            Text.CurTextFieldStyle.alignment = alignment;
        }

        public override string Name => "TD.ActiveSearches".Translate();
    }
}
