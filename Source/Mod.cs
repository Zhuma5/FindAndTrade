using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace RimMod
{
    /*
    Ideas:
    - Special roof to allow drop pods to land through. 
    - Auto trading table
    - Mark as junk (Gizmo, hidden rule)
     */
    public class Mod : Verse.Mod
    {
        public Mod(ModContentPack content) : base(content)
        {
            var harmony = new Harmony(content.PackageId);
            harmony.PatchAll();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory() => "RimMod.Settings".Translate();
    }
}
