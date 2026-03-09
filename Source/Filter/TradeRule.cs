using TD_Find_Lib;
using Verse;

namespace MGAutoSell.Filter
{
    public class TradeRule : IExposable, IQuerySearch
    {
        public int Import;
        public int Export;
        public bool Enabled = true;
        public TradeRuleAggregation Aggregation = TradeRuleAggregation.ThingDef;
        public QuerySearch search;
        public QuerySearch Search => search;
        public TradeMode Mode = TradeMode.Export;

        private int? _hash;
        public int Hash
        {
            get
            {
                _hash ??= GetHashCode();
                return _hash.Value;
            }
        }

        public string ExportBuffer;
        public string ImportBuffer;

        public bool AllowSell => (Export > 0 || NoConfig) && Mode is TradeMode.Export or TradeMode.Maintain;
        public bool AllowBuy => Import > 0 && Mode is TradeMode.Import or TradeMode.Maintain;

        public bool NoConfig => Export == 0 && (Mode == TradeMode.Export || Import == 0);
        public bool Invalid => Export > 0 && Import > 0 && Import > Export || Import < 0 || Export < 0;

        public void ExposeData()
        {
            Scribe_Deep.Look(ref search, nameof(search));
            Scribe_Values.Look(ref Import, nameof(Import));
            Scribe_Values.Look(ref Export, nameof(Export));
            Scribe_Values.Look(ref Enabled, nameof(Enabled));
            Scribe_Values.Look(ref Aggregation, nameof(Aggregation));
            Scribe_Values.Look(ref Mode, nameof(Mode));

            ExportBuffer = Export.ToString();
            ImportBuffer = Import.ToString();
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

    public enum TradeMode
    {
        Export,
        Import,
        Maintain
    }
}
