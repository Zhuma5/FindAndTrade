using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;

namespace MGAutoSell
{
    public class TradeRulesGameComp(Game game) : GameComponent
    {
        public TradeRulesGroup tradeRules = new();
        public HashSet<ITrader> traders = new();

        public override void ExposeData()
        {
            Scribe_Deep.Look(ref tradeRules, nameof(tradeRules));
            Scribe_Collections.Look(ref traders, nameof(traders), LookMode.Reference);

            tradeRules ??= new();
            traders ??= new();
        }
    }
}
