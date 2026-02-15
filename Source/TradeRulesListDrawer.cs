using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TD_Find_Lib;
using UnityEngine;
using Verse;

namespace MGAutoSell
{
    public class TradeRulesListDrawer : SearchGroupDrawerBase<TradeRulesGroup,TradeRule>
    {
        private TradeRulesMapComp comp;
        public TradeRulesListDrawer(TradeRulesGroup list) : base(list)
        {
            comp = Find.CurrentMap.GetComponent<TradeRulesMapComp>();
        }

        public override void DrawRowButtons(WidgetRow row, TradeRule item, int i)
        {
            if (row.ButtonIcon(FindTex.Edit, "TD.EditThisSearch".Translate()))
                Find.WindowStack.Add(new TradeRuleEditor(item));

            if (row.ButtonIcon(FindTex.Trash))
                comp.tradeRules.Remove(item);

        }

        public override void DrawExtraRowRect(Rect rowRect, TradeRule item, int i)
        {
            var row = new WidgetRow(rowRect.xMax, rowRect.y, UIDirection.LeftThenDown);

            row.Checkbox(ref item.Enabled);

            var buyWhenBelowRect = row.GetRect(100);
            buyWhenBelowRect.height -= 4;
            buyWhenBelowRect.width -= 4;

            string buyWhenBelowBuffer = null;
            Widgets.TextFieldNumeric(buyWhenBelowRect, ref item.BuyWhenBelow, ref buyWhenBelowBuffer, 0, 10000);
            row.Label("Buy when below");


            var buyUpToRect = row.GetRect(100);
            buyUpToRect.height -= 4;
            buyUpToRect.width -= 4;

            string buyUpToBuffer = null;
            Widgets.TextFieldNumeric(buyUpToRect, ref item.BuyUpTo, ref buyUpToBuffer);
            row.Label("Buy up to");


            var sellDownToRect = row.GetRect(100);
            sellDownToRect.height -= 4;
            sellDownToRect.width -= 4;

            string sellDownToBuffer = null;
            Widgets.TextFieldNumeric(sellDownToRect, ref item.SellDownTo, ref sellDownToBuffer);
            row.Label("Sell down to");
        }

        public override string Name => "TD.ActiveSearches".Translate();
    }
}
