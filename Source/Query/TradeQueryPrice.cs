using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using TD_Find_Lib;
using UnityEngine;
using Verse;

namespace MGAutoSell.Query
{
    public class TradeQueryPrice : ThingQueryWithOption<PriceTypeRange>, ITradeQuery
    {
        public TradeQueryPrice()
        {
            sel = new PriceTypeRange(PriceType.Normal, PriceType.Normal);
        }

        public bool AppliesDirectlyTo(TradeContext context)
        {
            var priceType = context.Tradeable.PriceTypeFor(context.Action);
            return priceType != PriceType.Undefined && sel.Includes(priceType);
        }

        public override bool AppliesDirectlyTo(Thing thing)
        {
            return true;
        }

        protected override bool DrawMain(Rect rect, bool locked, Rect fullRect)
        {
            base.DrawMain(rect, locked, fullRect);
            return PriceTypeRange.Widget((fullRect.RightHalfClamped(row.FinalX)), this.id, ref _sel);
        }
    }
}
