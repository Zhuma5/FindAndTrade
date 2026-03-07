using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;
using Verse.AI;

namespace MGAutoSell.AI
{
    public class WorkGiver_Barter : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.InteractionCell;
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn) => 
            pawn.Map.listerBuildings.AllBuildingsColonistOfClass<Building_CommsConsole>().Concat<Thing>(pawn.Map.mapPawns.AllPawnsSpawned.Where(x => x.trader != null));
        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            var comp = Current.Game.GetComponent<TradeRulesGameComp>();
            return !comp.autoTrade || !comp.tradeRules.Any() || !comp.autoTraderIDs.Contains(pawn.thingIDNumber);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false) => 
            new(AIDefOf.MGJobDriver_Barter, t);

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            var map = t.Map;
            var comp = Current.Game.GetComponent<TradeRulesGameComp>();
            switch (t)
            {
                case Building_CommsConsole commsConsole:
                    if (commsConsole.Faction != pawn.Faction || commsConsole.IsBurning() ||
                        commsConsole.IsForbidden(pawn) || !commsConsole.CanUseCommsNow ||
                        !pawn.CanReserveAndReach((LocalTargetInfo)commsConsole, PathEndMode.InteractionCell,
                            Danger.Some) || commsConsole.Map.passingShipManager.passingShips?.Any() is not true)
                        return false;

                    return map.passingShipManager.passingShips.Any(x =>
                        x is TradeShip tradeShip &&
                        pawn.CanTradeWith(tradeShip.Faction, ((ITrader)tradeShip).TraderKind) &&
                        !comp.traders.Contains(tradeShip));
                case Pawn trader:
                    return pawn.CanTradeWith(trader.Faction, trader.TraderKind) && !comp.traders.Contains(trader);
                default:
                    return false;
            }
        }
    }
}
