using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TD_Find_Lib;
using Verse;

namespace MGAutoSell
{
    public class TradeRule : IExposable, IQuerySearch
    {
        public int BuyWhenBelow;
        public int BuyUpTo;
        public int SellDownTo;
        public bool Enabled;
        public QuerySearch search;
        public QuerySearch Search => search;

        public void ExposeData()
        {
            Scribe_Deep.Look(ref search, "search");
            Scribe_Values.Look(ref BuyWhenBelow, "BuyWhenBelow");
            Scribe_Values.Look(ref BuyUpTo, "BuyUpTo");
            Scribe_Values.Look(ref SellDownTo, "SellDownTo");
            Scribe_Values.Look(ref Enabled, "Enabled");
        }

        public TradeRule(string name)
        {
            search = new QuerySearch()
            {
                name = name,
            };
        }

        public TradeRule()
        {
            
        }
    }
}
