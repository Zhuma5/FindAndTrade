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
    public class MainTabWindow_FindAndAutoSell : MainTabWindow
    {
        private TradeRulesListDrawer drawer;
        private Vector2 _scroll = Vector2.zero;

        private TradeRulesGameComp comp;

        public MainTabWindow_FindAndAutoSell()
        {
            preventCameraMotion = false;
            doCloseX = true;
        }

        public override void PreOpen()
        {
            base.PreOpen();
            comp = Current.Game.GetComponent<TradeRulesGameComp>();
            drawer = new TradeRulesListDrawer(comp.tradeRules, this);
        }

        public override void Close(bool doCloseSound = true)
        {
            editor?.PostClose();
            editor = null;
            SelectedTradeRule = null;
            base.Close(doCloseSound);
        }

        Vector2 listerScroll = Vector2.zero;
        public override void DoWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard(inRect, () => _scroll);
            listing.Begin(inRect);

            var drawerListing = new Listing_StandardIndent();
            var height = 300f;

            var topRect = listing.GetRect(height);

            var controlsRect = topRect.LeftHalf().BottomPartPixels(Text.LineHeight);
            var controls = new WidgetRow(controlsRect.x, controlsRect.y);
            if (controls.ButtonIcon(FindTex.GreyPlus))
                CreateRule();

            var rect = topRect.LeftHalf();
            drawerListing.BeginScrollView(rect, ref listerScroll, rect.LeftPartPixels(rect.width - 16).AtZero());
            var header = drawerListing.GetRect(30f);
            Widgets.Label(header.LeftHalf(), "Find and (Auto) Sell");

            var middle = 34;
            var left = header.RightHalf().LeftHalf();
            left.x += middle - (Text.CalcSize("Selling").x / 2);
            Widgets.Label(left, "Selling");
            Widgets.Label(header.RightPartPixels(middle + Text.CalcSize("Buying").x / 2), "Buying");
            
            drawer.DrawQuerySearchList(drawerListing);
            drawerListing.EndScrollView(ref height);
            listing.GapLine();

            if (editor != null)
            {
                var editRect = topRect.RightHalf();
                
                editor.DoWindowContents(editRect);
            }
            listing.End();
        }

        public TradeRule SelectedTradeRule;
        public void DoEdit(TradeRule tradeRule)
        {
            editor?.PostClose();

            if (SelectedTradeRule == tradeRule)
            {
                editor = null;
                SelectedTradeRule = null;
            }
            else
            {
                editor = new TradeRuleEditor(tradeRule);
                SelectedTradeRule = tradeRule;
            }

            
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

        private TradeRuleEditor editor;
        public void EditRule(TradeRule tradeRule)
        {
            editor = new TradeRuleEditor(tradeRule);

            //Find.WindowStack.Add(editor);
            //editor.windowRect.x = Window.StandardMargin;
            //editor.windowRect.y = windowRect.yMin / 3;
            //editor.windowRect.yMax = windowRect.yMin;
        }
    }
}
