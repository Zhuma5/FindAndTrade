using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using TD_Find_Lib;
using UnityEngine;
using Verse;

namespace MGAutoSell
{
    internal class MainTabWindow_FindAndAutoSell : MainTabWindow
    {
        private TradeRulesListDrawer drawer;
        private Vector2 _scroll = Vector2.zero;

        private TradeRulesGameComp comp;

        public MainTabWindow_FindAndAutoSell()
        {
            preventCameraMotion = false;
            draggable = true;
            resizeable = true;
            closeOnAccept = false;
            doCloseX = true;
        }

        public override void PreOpen()
        {
            base.PreOpen();
            comp = Current.Game.GetComponent<TradeRulesGameComp>();
            drawer = new TradeRulesListDrawer(comp.tradeRules);

        }

        public override void DoWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard(inRect, () => _scroll);
            listing.Begin(inRect);
            listing.Label("Find and (Auto) Sell");

            var controlsRect = inRect.LeftHalf().BottomPartPixels(Text.LineHeight);
            var controls = new WidgetRow(controlsRect.x, controlsRect.y);
            
            if(controls.ButtonIcon(FindTex.GreyPlus))
                CreateRule();

            var drawerListing = new Listing_StandardIndent();
            var rect = listing.GetRect(250);
            drawerListing.Begin(rect);
            drawer.DrawQuerySearchList(drawerListing);
            drawerListing.End();

            listing.End();
        }

        public void CreateRule()
        {
            Find.WindowStack.Add(new Dialog_Name("TD.NewAlert".Translate(), n =>
                {
                    TradeRule tradeRule = new(n);
                    comp.tradeRules.Add(tradeRule);

                    EditRule(tradeRule);
                },
                "TD.NameForNewAlert".Translate(),
                name => comp.tradeRules.Any(x => name == x.Search.name)));
        }

        public void EditRule(TradeRule tradeRule)
        {
            var editor = new TradeRuleEditor(tradeRule);

            Find.WindowStack.Add(editor);
            editor.windowRect.x = Window.StandardMargin;
            editor.windowRect.y = windowRect.yMin / 3;
            editor.windowRect.yMax = windowRect.yMin;
        }
    }
}
