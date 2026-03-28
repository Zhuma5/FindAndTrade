using HarmonyLib;
using Verse;

namespace MGAutoSell.HarmonyPatches
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.DeSpawn))]
    public static class TradePawnCleanup
    {
        public static void Postfix(Pawn __instance)
        {
            Current.Game.GetComponent<TradeRulesGameComp>().traders.Remove(__instance);
        }
    }
}
