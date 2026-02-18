using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TD_Find_Lib;
using UnityEngine;
using UnityEngine.UIElements;
using Verse;
using Verse.Sound;

namespace MGAutoSell
{
    [StaticConstructorOnStartup]
    public static class TradeRuleDrawUtility
    {
        private static readonly Texture2D Drag = ContentFinder<Texture2D>.Get("Drag");

        private static readonly Color
            Green,
            Blue,
            Yellow,
            Faded,
            Disabled;

        private const int ArrowSize = 8;
        private const int LabelSize = 60;
        private const int BoxSize = 35;

        private const int AnnoyingUnavoidableGap = 4;

        static TradeRuleDrawUtility()
        {
            ColorUtility.TryParseHtmlString("#60ba68", out Green);
            ColorUtility.TryParseHtmlString("#58a0d6", out Blue);
            ColorUtility.TryParseHtmlString("#d6b14a", out Yellow);
            Faded = new Color(1, 1, 1, 0.4f);
            Disabled = new Color(1, 0, 0, 0.4f);
        }

        public static TradeRuleAction DrawRow(Rect rowRect, TradeRule item, int i, ItemsToSell sellCache)
        {
            var response = TradeRuleAction.None;
            var ruleDisabled = !item.Enabled;
            var OGColor = GUI.color;
            var OGAnchor = Text.Anchor;
            var OGFieldAlignment = Text.CurTextFieldStyle.alignment;

            Text.CurTextFieldStyle.alignment = TextAnchor.MiddleCenter;

            if (i % 2 == 1)
                Widgets.DrawLightHighlight(rowRect);

            if (ruleDisabled)
                Widgets.DrawBoxSolid(rowRect, Disabled);

            var left = new WidgetRow(rowRect.x, rowRect.y + 3, UIDirection.RightThenDown);
            var right = new WidgetRow(rowRect.xMax, rowRect.y + 3, UIDirection.LeftThenDown);

            GUI.color = Faded;
            left.Icon(Drag);
            GUI.color = OGColor;
            Text.Anchor = TextAnchor.MiddleLeft;
            left.Label(item.search.name);
            Text.Anchor = OGAnchor;

            if (right.ButtonIcon(FindTex.Trash))
                response = TradeRuleAction.Delete;

            if (right.ButtonIcon(FindTex.Edit))
                response = TradeRuleAction.Edit;


            if (DrawGreyscaleIconButton(right.GetRect(24), TexButton.Suspend))
                //if (right.ButtonIcon(TexButton.Suspend))
                response = TradeRuleAction.Suspend;


            if (item.Mode is TradeMode.Export or TradeMode.Maintain)
            {
                string exportBuffer = null;
                right.TextFieldNumeric<int>(ref item.SellDownTo, ref exportBuffer, BoxSize);
                if (string.IsNullOrWhiteSpace(exportBuffer))
                    item.SellDownTo = 0;
            }
            else
                right.Gap(BoxSize + AnnoyingUnavoidableGap);

            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = item.Mode switch
            {
                TradeMode.Export => Green,
                TradeMode.Import => Blue,
                TradeMode.Maintain => Yellow,
                _ => throw new ArgumentOutOfRangeException()
            };
            var end = right.FinalX;

            if (item.Mode is TradeMode.Export or TradeMode.Maintain)
                right.Label(">", ArrowSize);
            else
                right.Gap(ArrowSize + AnnoyingUnavoidableGap);

            right.Label(item.Mode.ToString(), LabelSize);

            if (item.Mode is TradeMode.Import or TradeMode.Maintain)
                right.Label("<", ArrowSize);
            else
                right.Gap(ArrowSize + AnnoyingUnavoidableGap);
            var start = right.FinalX;
            var rect = new Rect(rowRect.x + start, rowRect.y, end - start, rowRect.height);
            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
                if (DoClickWithoutBlocking(rect, item.search.lastRemakeTick))
                    response = TradeRuleAction.Mode;
            }

            GUI.color = OGColor;

            if (item.Mode is TradeMode.Import or TradeMode.Maintain)
            {
                string importBuffer = null;
                right.TextFieldNumeric<int>(ref item.BuyUpTo, ref importBuffer, BoxSize);
                if (string.IsNullOrWhiteSpace(importBuffer))
                    item.BuyUpTo = 0;
            }
            else
                right.Gap(BoxSize + AnnoyingUnavoidableGap);

            GUI.color = OGColor;
            Text.Anchor = OGAnchor;
            Text.CurTextFieldStyle.alignment = OGFieldAlignment;

            return response;
        }

        static HashSet<int> mouseEvents = [];
        private static bool DoClickWithoutBlocking(Rect rect, int id)
        {
            var current = Event.current;
            switch (current.type)
            {
                case EventType.MouseDown:
                    if (Mouse.IsOver(rect))
                        mouseEvents.Add(id);
                    break;
                case EventType.MouseUp:
                    if (!mouseEvents.Contains(id))
                        break;

                    return Mouse.IsOver(rect) && mouseEvents.Remove(id);

                case EventType.MouseMove:
                    if (!mouseEvents.Contains(id))
                        break;

                    if (!Mouse.IsOver(rect))
                        mouseEvents.Remove(id);
                    break;
            }

            return false;
        }

        private static bool DrawGreyscaleIconButton(Rect rect, Texture2D texture, string tooltip = null, bool doMouseoverSound = true)
        {
            if (doMouseoverSound)
                MouseoverSounds.DoRegion(rect);
            var mouseOver = Mouse.IsOver(rect);

            var grayMat = MaterialPool.MatFrom(
                texture,
                ShaderDatabase.GrayscaleGUI,
                mouseOver ? GenUI.MouseoverColor : Color.white
            );

            grayMat.SetFloat("_Amount", 1f); // if your shader supports it

            Graphics.DrawTexture(rect, texture, grayMat);

            var clicked = DoClickWithoutBlocking(rect, texture.GetInstanceID());

            if (tooltip.NullOrEmpty())
                return clicked;
            TooltipHandler.TipRegion(rect, (TipSignal)tooltip);
            return clicked;
        }
    }

    public enum TradeRuleAction
    {
        None,
        Delete,
        Edit,
        Suspend,
        Mode
    }
}
