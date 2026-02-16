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
            pawn.Map.listerBuildings.AllBuildingsColonistOfClass<Building_CommsConsole>();
        public override bool ShouldSkip(Pawn pawn, bool forced = false) => 
            !Current.Game.GetComponent<TradeRulesGameComp>().tradeRules.Any();
        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false) => 
            new(AIDefOf.MGJobDriver_Barter, t);

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (t.Faction != pawn.Faction || t.IsBurning() || t.IsForbidden(pawn) ||
                !pawn.CanReserveAndReach((LocalTargetInfo)t, PathEndMode.InteractionCell, Danger.Some) ||
                t is not Building_CommsConsole { CanUseCommsNow: true } ||
                t.Map.passingShipManager.passingShips == null ||
                !t.Map.passingShipManager.passingShips.Any())
                return false;

            var comp = Current.Game.GetComponent<TradeRulesGameComp>();
            var map = t.Map;
            var hasShip = map.passingShipManager.passingShips.Any(x => x is TradeShip tradeShip && pawn.CanTradeWith(x.Faction, ((ITrader)x).TraderKind) && !comp.traders.Contains((ITrader)x));

            if(!hasShip) 
                return false;

            if (!Current.Game.GetComponent<TradeRulesGameComp>().tradeRules.Any())
                return false;

            return true;
        }
    }
}
