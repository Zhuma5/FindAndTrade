using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;

namespace MGAutoSell
{
    public class TradeRulesGameComp : GameComponent
    {
        public TradeRulesGroup tradeRules = new();
        public HashSet<ITrader> traders = new();

        public bool autoTrade = false;
        public HashSet<int> autoTraderIDs = new();

        public TradeRulesGameComp(Game game)
        {
            tradeRules ??= new();
            traders ??= new();
        }

        public override void ExposeData()
        {
            Scribe_Deep.Look(ref tradeRules, nameof(tradeRules));
            Scribe_Collections.Look(ref traders, nameof(traders), LookMode.Reference);

            Scribe_Values.Look(ref autoTrade, nameof(autoTrade));
            Scribe_Collections.Look(ref autoTraderIDs, nameof(autoTraderIDs));

            tradeRules ??= new();
            traders ??= new();
            autoTraderIDs ??= new();
        }
    }
}
