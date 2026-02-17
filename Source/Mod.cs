using HarmonyLib;
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
        public Mod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<Settings>();
            var harmony = new Harmony(content.PackageId);
            harmony.PatchAll();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory() => "MGAutoSell.Title".Translate();
    }
}
