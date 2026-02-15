using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace MGAutoSell
{
    public class TradeRulesMapComp(Map m) : MapComponent(m)
    {
        public TradeRulesGroup tradeRules = new TradeRulesGroup();

        public override void ExposeData()
        {
            Scribe_Deep.Look(ref tradeRules, nameof(tradeRules));
        }
    }
}
