using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using TD_Find_Lib;
using Verse;

namespace MGAutoSell.Query
{
    internal class TradeQueryTraderOrbital : ThingQuery, ITradeQuery
    {
        public override bool AppliesDirectlyTo(Thing thing)
        {
            return true;
        }

        public bool AppliesDirectlyTo(TradeContext context)
        {
            return TradeSession.trader.TraderKind.orbital;
        }
    }
}
