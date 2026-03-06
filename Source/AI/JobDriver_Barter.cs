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
                               TargetThingA is not Building_CommsConsole { CanUseCommsNow: true } ||
                               !Enumerable.Any(Current.Game.GetComponent<TradeRulesGameComp>().tradeRules);
            autoSell.FailOn(failOn);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell)
                .FailOn(failOn);

           
            yield return DoTrade();
        }

        private Toil DoTrade()
        {
            var trade = ToilMaker.MakeToil();
            trade.defaultCompleteMode = ToilCompleteMode.Delay;
            trade.activeSkill = () => SkillDefOf.Social;
            trade.defaultDuration = 600;
            trade.finishActions =
            [
                () =>
                {
                    var actor = trade.actor;
                    TradeDealProcessor.DoTradeShips(actor);
                }
            ];

            return trade.WithProgressBarToilDelay(TargetIndex.B, 600);
        }
    }
}
