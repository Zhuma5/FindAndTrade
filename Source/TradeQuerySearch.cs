using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using MGAutoSell.Query;
using RimWorld;
using TD_Find_Lib;
using Verse;

namespace MGAutoSell
{
    public class TradeQuerySearch : QuerySearch
    {
        public List<ThingDef> AllItems;
        [CanBeNull] private ITradeQuery[] _tradeQueries;

        public ITradeQuery[] TradeQueries => _tradeQueries ??= children.queries
            .OfType<ITradeQuery>()
            .ToArray();

        public TradeQuerySearch()
        {

        }

        public override void Changed()
        {
            base.Changed();
            AllItems = this.GetPossibleItems();
            _tradeQueries = children.queries
                .OfType<ITradeQuery>()
                .ToArray();
        }

        public bool AppliesTo(TradeContext context)
        {
            _tradeQueries ??= children.queries
                .OfType<ITradeQuery>()
                .ToArray();

            return !MatchAllQueries
                ? TradeQueries.AnyX(x => (x as ThingQuery)?.Enabled is not false && x.AppliesDirectlyTo(context),
                    Children.anyMin)
                : TradeQueries.All(x => x is ThingQuery { Enabled: false } || x.AppliesDirectlyTo(context));
        }
    }
}
