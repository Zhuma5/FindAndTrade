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

        public static TradeRuleAction DrawRow(Rect rowRect, TradeRule item, int i, ItemsToSell sellCache, int reorderId)
        {
            suspendTex ??= BakeGrayscale(TexButton.Suspend, Color.white);


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


            if (right.ButtonIcon(suspendTex))
                response = TradeRuleAction.Suspend;

            if (item.Mode is TradeMode.Export or TradeMode.Maintain)
            {
                right.TextFieldNumeric<int>(ref item.Export, ref item.ExportBuffer, BoxSize);
                if (string.IsNullOrWhiteSpace(item.ExportBuffer))
                {
                    item.Export = 0;
                    item.ExportBuffer = "0";
                }
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
            }
            if (DoClickWithoutBlocking(rect, item.Hash))
                response = TradeRuleAction.Mode;

            GUI.color = OGColor;

            if (item.Mode is TradeMode.Import or TradeMode.Maintain)
            {
                string importBuffer = null;
                right.TextFieldNumeric<int>(ref item.Import, ref item.ImportBuffer, BoxSize);
                if (string.IsNullOrWhiteSpace(item.ImportBuffer))
                {
                    item.Import = 0;
                    item.ImportBuffer = "0";
                }
            }
            else
                right.Gap(BoxSize + AnnoyingUnavoidableGap);

            GUI.color = OGColor;
            Text.Anchor = OGAnchor;
            Text.CurTextFieldStyle.alignment = OGFieldAlignment;

            ReorderableWidget.Reorderable(reorderId, rowRect);
            return response;
        }

        static HashSet<int> mouseEvents = [];
        private static bool DoClickWithoutBlocking(Rect rect, int id)
        {
            var current = Event.current;
            switch (current.type)
            {
                case EventType.MouseDown:
                    if (Mouse.IsOver(rect)) mouseEvents.Add(id);
                    break;
                case EventType.MouseUp:
                    if (!mouseEvents.Contains(id))
                        break;

                    return Mouse.IsOver(rect) && mouseEvents.Remove(id);

                case EventType.MouseMove:
                case EventType.MouseDrag:
                    if (!mouseEvents.Contains(id))
                        break;

                    if (!Mouse.IsOver(rect))
                        mouseEvents.Remove(id);
                    break;
            }

            return false;
        }

        private static Texture2D suspendTex;
        private static Texture2D grayTexOver;
        private static readonly int instanceID = TexButton.Suspend.GetInstanceID();

        private static Texture2D BakeGrayscale(Texture2D source, Color tint)
        {
            var mat = MaterialPool.MatFrom(source, ShaderDatabase.GrayscaleGUI, tint);
            var prev = RenderTexture.active;
            var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, rt, mat);
            RenderTexture.active = rt;
            var baked = new Texture2D(source.width, source.height, TextureFormat.ARGB32, false);
            baked.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            baked.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return baked;
        }

        private static bool DrawGreyscaleIconButton(Rect rect, string tooltip = null, bool doMouseoverSound = true)
        {
            if (doMouseoverSound)
                MouseoverSounds.DoRegion(rect);
            var mouseOver = Mouse.IsOver(rect);

            grayTexOver ??= BakeGrayscale(TexButton.Suspend, GenUI.MouseoverColor);

            GUI.DrawTexture(rect, mouseOver ? grayTexOver : suspendTex);

            var clicked = DoClickWithoutBlocking(rect, instanceID);

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
