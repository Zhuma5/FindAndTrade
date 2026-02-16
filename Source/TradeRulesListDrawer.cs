using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TD_Find_Lib;
using UnityEngine;
using Verse;

namespace MGAutoSell
{
    public class TradeRulesListDrawer : SearchGroupDrawerBase<TradeRulesGroup,TradeRule>
    {
        private readonly MainTabWindow_FindAndAutoSell _parent;
        private TradeRulesGameComp comp;

        private Color altBackground = new(0.3f, 0.3f, 0.3f, 0.5f);

        public TradeRulesListDrawer(TradeRulesGroup list, MainTabWindow_FindAndAutoSell parent) : base(list)
        {
            _parent = parent;
            comp = Current.Game.GetComponent<TradeRulesGameComp>();
        }

        public override void DrawPreRow(Listing_StandardIndent listing, int i)
        {
           
        }

        public override void DrawRowButtons(WidgetRow row, TradeRule item, int i)
        {
            row.Checkbox(ref item.Enabled);

            if (row.ButtonIcon(FindTex.Edit, "TD.EditThisSearch".Translate()))
                _parent.DoEdit(item);

            if (row.ButtonIcon(FindTex.Trash))
                comp.tradeRules.Remove(item);
        }

        public override void DrawExtraRowRect(Rect rowRect, TradeRule item, int i)
        {
            if (item == _parent.SelectedTradeRule)
                Widgets.DrawHighlightSelected(rowRect);

            var rowBuy = new WidgetRow(rowRect.xMax, rowRect.y, UIDirection.LeftThenDown);

            var alignment = Text.CurTextFieldStyle.alignment;
            Text.CurTextFieldStyle.alignment = TextAnchor.MiddleCenter;
            var buyWhenBelowRect = rowBuy.GetRect(30);
            buyWhenBelowRect.height -= 4;
            buyWhenBelowRect.y += 3;

            string buyWhenBelowBuffer = null;
            Widgets.TextFieldNumeric(buyWhenBelowRect, ref item.BuyWhenBelow, ref buyWhenBelowBuffer);
            rowBuy.Label("-");


            var buyUpToRect = rowBuy.GetRect(30);
            buyUpToRect.height -= 4;
            buyUpToRect.y += 3;

            string buyUpToBuffer = null;
            Widgets.TextFieldNumeric(buyUpToRect, ref item.BuyUpTo, ref buyUpToBuffer);
            if (string.IsNullOrWhiteSpace(buyUpToBuffer))
                item.BuyUpTo = 0;

            //rowBuy.Gap(60);

            var rowSellRect = rowRect.RightHalf();
            var rowSell = new WidgetRow(rowSellRect.x, rowSellRect.y, UIDirection.RightThenDown);

            var sellWhenOverRect = rowSell.GetRect(30);
            sellWhenOverRect.height -= 4;
            sellWhenOverRect.y += 3;

            string sellWhenOverBuffer = null;
            Widgets.TextFieldNumeric(sellWhenOverRect, ref item.SellWhenOver, ref sellWhenOverBuffer);
            rowSell.Label("-");

            var sellDownToRect = rowSell.GetRect(30);
            sellDownToRect.height -= 4;
            sellDownToRect.y += 3;

            string sellDownToBuffer = null;
            Widgets.TextFieldNumeric(sellDownToRect, ref item.SellDownTo, ref sellDownToBuffer);
            rowSell.Gap(4);
            //row.Label("Selling");
            Text.CurTextFieldStyle.alignment = alignment;

            
        }

        public override string Name => "TD.ActiveSearches".Translate();
    }
}
