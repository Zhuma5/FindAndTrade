using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
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
            return !comp.tradeRules.Any() || (!forced && (!comp.autoTrade || !comp.autoTraderIDs.Contains(pawn.thingIDNumber)));
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false) => 
            new(AIDefOf.MGJob_Barter, t);

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

                    var hasPassingShip = map.passingShipManager.passingShips.Any(x =>
                        x is TradeShip tradeShip &&
                        pawn.CanTradeWith(tradeShip.Faction, ((ITrader)tradeShip).TraderKind) &&
                        (forced || !comp.traders.Contains(tradeShip)));

                    if(hasPassingShip && forced)
                        map.passingShipManager.passingShips.ForEach(x => comp.traders.Remove(x as ITrader));
                    return hasPassingShip;
                case Pawn trader:
                    return trader.CanTradeNow && pawn.CanTradeWith(trader.Faction, trader.TraderKind) && pawn.CanReserveAndReach(trader, PathEndMode.Touch, Danger.Some) && (forced || !comp.traders.Contains(trader));
                default:
                    return false;
            }
        }
    }
}
