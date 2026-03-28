using HarmonyLib;
using RimWorld;
using Verse;

namespace MGAutoSell.HarmonyPatches
{
    [HarmonyPatch(typeof(PassingShipManager), nameof(PassingShipManager.RemoveShip))]
    public static class TradeShipCleanup
    {
        public static void Postfix(PassingShip vis)
        {
            if (vis is not ITrader trader)
                return;

            Current.Game.GetComponent<TradeRulesGameComp>().traders.Remove(trader);
        }
    }
}
