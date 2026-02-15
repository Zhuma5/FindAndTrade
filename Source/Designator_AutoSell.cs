using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MGAutoSell
{
    public class Designator_AutoSell : Designator
    {
        public override bool DragDrawMeasurements => true;

        protected override DesignationDef Designation => MGDesignatorDefOf.MGAutoSell;
        public override DrawStyleCategoryDef DrawStyleCategory => DrawStyleCategoryDefOf.FilledRectangle;

        public Designator_AutoSell()
        {
            defaultLabel = "Sell";
            useMouseIcon = true;
            soundSucceeded = SoundDefOf.Designate_Haul;
            icon = ContentFinder<Texture2D>.Get("RecycleThisGizmo");
            
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            if (!c.InBounds(Map) || c.Fogged(Map))
                return false;
            foreach (var thing in c.GetThingList(Map))
            {
                if (thing != null && CanDesignateThing(thing))
                    return true;
            }
            return false;
        }

        public override AcceptanceReport CanDesignateThing(Thing t)
        {
            if (Map.designationManager.DesignationOn(t, this.Designation) != null)
                return false;
            return t.def.tradeability is Tradeability.Sellable or Tradeability.All && t.def.EverHaulable;
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            var thingList = Map.thingGrid.ThingsListAt(c);
            foreach (var t in thingList.Where(t => CanDesignateThing(t)))
            {
                DesignateThing(t);
            }
        }

        public override void DesignateThing(Thing t)
        {
            Map.designationManager.RemoveAllDesignationsOn(t);
            Map.designationManager.AddDesignation(new Designation((LocalTargetInfo)t, Designation));
        }

        public override void SelectedUpdate() => GenUI.RenderMouseoverBracket();

    }

    [DefOf]
    public class MGDesignatorDefOf
    {
        public static DesignationDef MGAutoSell;
    }
}
