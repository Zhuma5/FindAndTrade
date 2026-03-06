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
    public class JobDriver_Barter : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            pawn.Reserve(job.GetTarget(TargetIndex.A), job);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            var autoSell = this;
            autoSell.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            var failOn = () => pawn.IsPrisoner || pawn.Dead || pawn.IsBrokenDown() ||
                               TargetThingA is Building_CommsConsole { CanUseCommsNow: false } ||
                               (TargetThingA is Pawn { Spawned: false }) ||
                               !Current.Game.GetComponent<TradeRulesGameComp>().tradeRules.Any();
            autoSell.FailOn(failOn);

            yield return Toils_Goto.GotoThing(TargetIndex.A, TargetThingA is Building_CommsConsole ? PathEndMode.InteractionCell : PathEndMode.Touch)
                .FailOn(failOn);

           
            yield return DoTrade(failOn);
        }

        private Toil DoTrade(Func<bool> failOn)
        {
            var trade = ToilMaker.MakeToil();
            trade.defaultCompleteMode = ToilCompleteMode.Delay;
            trade.activeSkill = () => SkillDefOf.Social;
            if(TargetThingA is Building_CommsConsole)
                trade.defaultDuration = 600;
            trade.FailOn(failOn);
            trade.finishActions =
            [
                () =>
                {
                    var actor = trade.actor;
                    switch (TargetThingA)
                    {
                        case Building_CommsConsole:
                            TradeDealProcessor.DoTradeShips(actor);
                            break;
                        case Pawn trader:
                            TradeDealProcessor.DoTrade(actor, trader);
                            break;
                    }
                }
            ];

            return trade.WithProgressBarToilDelay(TargetIndex.B, 600);
        }
    }
}
