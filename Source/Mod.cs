using HarmonyLib;
using MGAutoSell.Query;
using UnityEngine;
using Verse;

namespace MGAutoSell
{
    /*
    Ideas:
    - Special roof to allow drop pods to land through. 
    - Auto trading table
    - Mark as junk (Gizmo, hidden rule)
     */
    public class Mod : Verse.Mod
    {
        public static Settings Settings;
        public static Mod Instance;
        public Mod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<Settings>();
            Instance = this;
            ParseHelper.Parsers<PriceTypeRange>.Register(PriceTypeRange.FromString);

            LongEventHandler.ExecuteWhenFinished(() => Settings.Init());
            var harmony = new Harmony(content.PackageId);
            harmony.PatchAll();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DoSettingsWindow(inRect);
        }

        public override string SettingsCategory() => "MGAutoSell.Title".Translate();
    }
}
