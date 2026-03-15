using System;
using System.Collections.Generic;
using MGAutoSell.Filter;
using TD_Find_Lib;
using UnityEngine;
using Verse;
using Verse.Sound;
using MGAutoSell.Extensions;

namespace MGAutoSell
{
    [StaticConstructorOnStartup]
    public static class TradeRuleDrawUtility
    {

        private static readonly Color
            Green,
            Blue,
            Yellow,
            Faded,
            Disabled,
            Invalid;

        private const int ArrowSize = 8;
        private const int LabelSize = 60;
        private const int BoxSize = 40;

        public const int AnnoyingUnavoidableGap = 4;

        private static TaggedString TagInvalid, TagRange, TagBuy, TagBasically, TagExport, TagImport, TagMaintain;

        static TradeRuleDrawUtility()
        {
            ColorUtility.TryParseHtmlString("#60ba68", out Green);
            ColorUtility.TryParseHtmlString("#58a0d6", out Blue);
            ColorUtility.TryParseHtmlString("#d6b14a", out Yellow);
            Faded = new Color(1, 1, 1, 0.4f);
            Disabled = new Color(0.3f, 0, 0, 0.4f);
            Invalid = ColoredText.ThreatColor;

            TagInvalid = "MGAutoSell.Invalid".Translate();
            TagRange = "MGAutoSell.InvalidRange".Translate();
            TagBuy = "MGAutoSell.InvalidBuy".Translate();
            TagBasically = "MGAutoSell.Basically".Translate();
            TagExport = "MGAutoSell.Mode.Export".Translate();
            TagImport = "MGAutoSell.Mode.Import".Translate();
            TagMaintain = "MGAutoSell.Mode.Maintain".Translate();
        }

        public static TradeRuleAction DrawRow(Rect rowRect, TradeRule item, int i, ItemsToSell sellCache, int reorderId)
        {
            var doMouseEvents = Mouse.IsOver(rowRect);
            suspendTex ??= BakeGrayscale(TexButton.Suspend, Color.white);
            var invalidSell = item.Invalid || !item.AllowSell;
            var invalidBuy = item.Invalid || !item.AllowBuy;

            var response = TradeRuleAction.None;
            var ruleDisabled = !item.Enabled;
            var OGColor = GUI.color;
            var OGAnchor = Text.Anchor;
            var OGFieldAlignment = Text.CurTextFieldStyle.alignment;
            Text.CurTextFieldStyle.alignment = TextAnchor.MiddleCenter;
            if (GUI.enabled && i % 2 == 1)
                Widgets.DrawLightHighlight(rowRect);
            if (ruleDisabled)
                Widgets.DrawBoxSolid(rowRect, Disabled);

            var left = new WidgetRow(rowRect.x, rowRect.y + 3, UIDirection.RightThenDown);
            var right = new WidgetRow(rowRect.xMax, rowRect.y + 3, UIDirection.LeftThenDown);

            if (GUI.enabled)
            {
                GUI.color = Faded;
                left.Icon(Textures.Drag);
                GUI.color = OGColor;
                Text.Anchor = TextAnchor.MiddleLeft;
                left.LabelFast(item.search.name);
                Text.Anchor = OGAnchor;

                if (doMouseEvents)
                {
                    if (right.ButtonIcon(FindTex.Trash))
                        response = TradeRuleAction.Delete;

                    if (right.ButtonIcon(FindTex.Edit))
                        response = TradeRuleAction.Edit;

                    if (right.ButtonIcon(suspendTex))
                        response = TradeRuleAction.Suspend;
                }
                else
                {
                    var gap = right.CellGap;
                    right.CellGap = 0;
                    right.Icon(FindTex.Trash);
                    right.Icon(FindTex.Edit);
                    right.Icon(suspendTex);
                    right.CellGap = gap;
                }
            }

            #region Export
            if (item.Mode is TradeMode.Export or TradeMode.Maintain)
            {
                if (invalidSell)
                    GUI.color = Invalid;
                var before = item.Export;

                var tooltip = right.TextFieldInt(ref item.Export, ref item.ExportBuffer, BoxSize);

                if (invalidSell)
                    TooltipHandler.TipRegion(tooltip, () => TagInvalid.Formatted(GetInvalidMessage(item.Import, item.Export, item.Mode)), item.Hash);
                if (string.IsNullOrWhiteSpace(item.ExportBuffer))
                {
                    item.Export = 0;
                    item.ExportBuffer = "";
                }

                var after = item.Export;
                if (before != after)
                    response = TradeRuleAction.Refresh;

                GUI.color = OGColor;
            }
            else
                right.Gap(BoxSize + AnnoyingUnavoidableGap);
            #endregion

            #region Mode Switcher

            if (GUI.enabled)
            {
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
                    right.Label("<", ArrowSize);
                else
                    right.Gap(ArrowSize + AnnoyingUnavoidableGap);

                string label = null;
                if (Mod.Settings.showQuantityInsteadOfLabel && sellCache != null)
                    sellCache.Rules.TryGetValue(item, out label);
                label ??= item.Mode.ToString();
                right.Label(label, LabelSize);

                if (item.Mode is TradeMode.Import or TradeMode.Maintain)
                    right.Label(">", ArrowSize);
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
            }

            #endregion

            #region Import
            if (item.Mode is TradeMode.Import or TradeMode.Maintain)
            {
                if (invalidBuy)
                    GUI.color = Invalid;

                var before = item.Import;
                var tooltip = right.TextFieldInt(ref item.Import, ref item.ImportBuffer, BoxSize);

                if (invalidBuy)
                    TooltipHandler.TipRegion(tooltip, () => TagInvalid.Formatted(GetInvalidMessage(item.Import, item.Export, item.Mode)), item.Hash);
                if (string.IsNullOrWhiteSpace(item.ImportBuffer))
                {
                    item.Import = 0;
                    item.ImportBuffer = "";
                }

                var after = item.Import;
                if (before != after)
                    response = TradeRuleAction.Refresh;

                GUI.color = OGColor;
            }
            else
                right.Gap(BoxSize + AnnoyingUnavoidableGap);
            #endregion

            GUI.color = OGColor;
            Text.Anchor = OGAnchor;
            Text.CurTextFieldStyle.alignment = OGFieldAlignment;

            ReorderableWidget.Reorderable(reorderId, rowRect);
            return response;
        }

        private static string GetInvalidMessage(int import, int export, TradeMode mode)
        {
            switch (mode)
            {
                case TradeMode.Maintain when import == 0:
                    return TagBasically.Formatted(TagExport, TagImport);
                case TradeMode.Maintain when export == 0:
                    return TagBasically.Formatted(TagImport, TagExport);
                case TradeMode.Maintain when import > export:
                    return TagRange.Formatted(import, export);

                case TradeMode.Import when import < 1:
                    return TagBuy.Formatted(import);
                default:
                    Log.WarningOnce($"Attempted to fetch a tooltip message for an invalid row, however none are defined for this case.\nImport: {import}\nExport: {export}\nMode: {mode}", 54987);
                    return "*shrugs*";
            }


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

    }

    public enum TradeRuleAction
    {
        None,
        Delete,
        Edit,
        Suspend,
        Mode,
        Refresh
    }
}
