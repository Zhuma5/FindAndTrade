using System.Collections.Generic;
using HarmonyLib;
using RimWorld;

namespace MGAutoSell.HarmonyPatches
{
    [HarmonyPatch]
    public static class AddRuleLabelsToTradeUI
    {
        public static Dictionary<Tradeable, string> ExtraLabels = new();
        public static Dialog_Trade TradeWindow;

        [HarmonyPatch(typeof(Dialog_Trade), nameof(Dialog_Trade.PostOpen))]
        [HarmonyPostfix]
        public static void Init(Dialog_Trade __instance)
        {
            TradeWindow = __instance;
        }

        [HarmonyPatch(typeof(Dialog_Trade), nameof(Dialog_Trade.Close))]
        [HarmonyPostfix]
        public static void Cleanup(Dialog_Trade __instance)
        {
            ExtraLabels.Clear();
            TradeWindow = null;
        }

        [HarmonyPatch(typeof(Tradeable), nameof(Tradeable.Label), MethodType.Getter)]
        [HarmonyPostfix]
        public static void AddLabel(Tradeable __instance, ref string __result)
        {
            if (!Mod.Settings.scanEveryStack)
                return;

            if (!ExtraLabels.TryGetValue(__instance, out var extraLabel))
                return;

            __result += extraLabel;
        }
    }
}
