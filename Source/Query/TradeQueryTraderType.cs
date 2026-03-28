using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TD_Find_Lib;
using Verse;

namespace MGAutoSell.Query
{
    internal class TradeQueryTraderType : ThingQueryDropDown<TraderKindDef>, ITradeQuery
    {
        public override bool AppliesDirectlyTo(Thing thing)
        {
            return true;
        }

        public bool AppliesDirectlyTo(TradeContext context)
        {
            return TradeSession.trader.TraderKind.label == sel.label;
        }

        public override string TipForSel() => 
            null;

        private List<TraderKindDef> allOptions;
        public override IEnumerable<TraderKindDef> AllOptions() => allOptions = allOptions ?? base.AllOptions().Where(x => !string.IsNullOrWhiteSpace(x.label))
            .GroupBy(x => x.label)
            .Select(x => x.FirstOrDefault())
            .ToList();
    }
}
