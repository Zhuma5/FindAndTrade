using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace MGAutoSell
{
    [HarmonyPatch(typeof(Dialog_Trade), nameof(Dialog_Trade.PostOpen))]
    public static class DoTradeOnOpen
    {
        private static MethodInfo _propertySetter = AccessTools.PropertySetter(typeof(Tradeable), nameof(Tradeable.CountToTransfer));
        public static void Postfix()
        {
            try
            {
                var deal = TradeSession.deal;
                if (deal == null) return;

                var map = TradeSession.playerNegotiator?.Map;
                if(map == null) return;

                var autoTrade = map.GetComponent<TradeRulesMapComp>();

                if (!autoTrade.tradeRules.Any())
                    return;

                // Get tradeables list (property in most versions)
                var tradeables = deal.AllTradeables;
                if (tradeables == null) return;

                bool changedAnything = false;

                foreach (var tradeable in tradeables)
                {
                    if (tradeable == null) continue;

                    // Skip non-tradeable entries (some UI entries can exist depending on mods)

                    foreach (var rule in autoTrade.tradeRules)
                    {
                        if (rule.Search.AppliesTo(tradeable.AnyThing))
                        {
                            var count = map.resourceCounter.GetCount(tradeable.ThingDef);
                            if (count > rule.SellDownTo)
                            {
                                //tradeable.CountToTransfer = rule.SellDownTo - count;
                                Log.Warning($"{tradeable.ThingDef.label}: {rule.SellDownTo - count}");
                                _propertySetter.Invoke(tradeable, [rule.SellDownTo - count]);
                                continue;
                            }
                        }
                    }

                    //tradeable.CountHeldBy(Transactor.Trader);
                    //int traderCount = TradeableAccess.GetTraderSideCount(tradeable);
                    //if (traderCount <= 0) continue;

                    //// Set to buy all from trader (positive transfer count)
                    //int current = TradeableAccess.GetCountToTransfer(tradeable);
                    //if (current == traderCount) continue;

                    //TradeableAccess.SetCountToTransfer(tradeable, traderCount);
                    //changedAnything = true;
                }

                //if (changedAnything)
                //{
                //    DealAccess.NotifyTradeablesChanged(deal);
                //}
            }
            catch (Exception ex)
            {
                Log.Error($"[BuyEverythingOnOpen] Failed to auto-select tradeables: {ex}");
            }
        }
    }
}
