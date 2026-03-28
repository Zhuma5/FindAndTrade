using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using static RimWorld.PriceType;

namespace MGAutoSell.Query
{
    public struct PriceTypeRange : IEquatable<PriceTypeRange>
    {
        private enum RangeEnd : byte
        {
            None,
            Min,
            Max,
        }
        private static int draggingId = 0;
        private static Color RangeControlTextColor = new(0.6f, 0.6f, 0.6f);
        private static RangeEnd curDragEnd = RangeEnd.None;

        public PriceType min;
        public PriceType max;
        private static Dictionary<PriceType, string> labelCache;

        public PriceTypeRange(PriceType min, PriceType max)
        {
            if (min == Undefined)
                min = VeryCheap;
            if (max == Undefined)
                max = Exorbitant;
            this.min = min;
            this.max = max;
        }

        public static PriceTypeRange All => new(VeryCheap, Exorbitant);

        public readonly bool Equals(PriceTypeRange other) => other.min == min && other.max == max;

        public override string ToString() => min + "~" + max;

        public override bool Equals(object obj) => obj is PriceTypeRange other && Equals(other);

        public override int GetHashCode() => Gen.HashCombineStruct<PriceType>(min.GetHashCode(), max);

        public static bool operator ==(PriceTypeRange a, PriceTypeRange b) => a.min == b.min && a.max == b.max;

        public static bool operator !=(PriceTypeRange a, PriceTypeRange b) => !(a == b);
        public bool Includes(PriceType p) => p >= min && p <= max;

        public static PriceTypeRange FromString(string s)
        {
            var strArray = s.Split('~');
            return new PriceTypeRange(ParseHelper.FromString<PriceType>(strArray[0]), ParseHelper.FromString<PriceType>(strArray[1]));
        }

        public static bool Widget(Rect rect, int id, ref PriceTypeRange range)
        {
            labelCache ??= new Dictionary<PriceType, string>
            {
                { VeryCheap, GenText.ToTitleCaseSmart(("PriceType" + VeryCheap).Translate())},
                { Cheap, GenText.ToTitleCaseSmart(("PriceType" + Cheap).Translate()) },
                { Normal, GenText.ToTitleCaseSmart(("PriceType" + Normal).Translate()) },
                { Expensive, GenText.ToTitleCaseSmart(("PriceType" + Expensive).Translate()) },
                { Exorbitant, GenText.ToTitleCaseSmart(("PriceType" + Exorbitant).Translate()) },
            };
            if (range.min == Undefined)
                range.min = VeryCheap;
            if (range.max == Undefined) 
                range.max = Exorbitant;

            var changed = false;

            var sliderRect = rect;
            sliderRect.xMin += 8f;
            sliderRect.xMax -= 8f;
            GUI.color = RangeControlTextColor;
            string label = !(range == All) ? (range.max != range.min ? labelCache[range.min] + " - " + labelCache[range.max] :  "OnlyQuality".Translate(labelCache[range.min])) : "TD.AnyValue".Translate();
            var previousFont = (int)Text.Font;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperCenter;
            var labelRect = sliderRect;
            labelRect.yMin -= 2f;
            Widgets.Label(labelRect, label);
            Text.Anchor = TextAnchor.UpperLeft;
            var trackRect = new Rect(sliderRect.x, (float)(sliderRect.yMax - 8.0 - 1.0), sliderRect.width, 2f);
            GUI.DrawTexture(trackRect, BaseContent.WhiteTex);
            var selectableCount = Enum.GetNames(typeof(PriceType)).Length - 1; // exclude Undefined
            var minHandleX = sliderRect.x + sliderRect.width / (selectableCount - 1) * (range.min - VeryCheap);
            var maxHandleX = sliderRect.x + sliderRect.width / (selectableCount - 1) * (range.max - VeryCheap);
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(minHandleX, (float)(sliderRect.yMax - 8.0 - 2.0), maxHandleX - minHandleX, 4f), BaseContent.WhiteTex);
            var minThumbRect = new Rect(minHandleX - 16f, trackRect.center.y - 8f, 16f, 16f);
            GUI.DrawTexture(minThumbRect, Textures.FloatRangeSliderTex);
            var maxThumbRect = new Rect(maxHandleX + 16f, trackRect.center.y - 8f, -16f, 16f);
            GUI.DrawTexture(maxThumbRect, Textures.FloatRangeSliderTex);
            if (curDragEnd != RangeEnd.None && (Event.current.type == EventType.MouseUp || Event.current.type == EventType.MouseDown))
            {
                draggingId = 0;
                curDragEnd = RangeEnd.None;
                SoundDefOf.DragSlider.PlayOneShotOnCamera();
            }
            var justStartedDrag = false;
            if (Mouse.IsOver(rect) || id == draggingId)
            {
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && id != draggingId)
                {
                    draggingId = id;
                    var mouseX = Event.current.mousePosition.x;
                    curDragEnd = mouseX >= (double)minThumbRect.xMax ? (mouseX <= (double)maxThumbRect.xMin ? (Mathf.Abs(mouseX - minThumbRect.xMax) < (double)Mathf.Abs(mouseX - (maxThumbRect.x - 16f)) ? RangeEnd.Min : RangeEnd.Max) : RangeEnd.Max) : RangeEnd.Min;
                    justStartedDrag = true;
                    Event.current.Use();
                    SoundDefOf.DragSlider.PlayOneShotOnCamera();
                }
                if (justStartedDrag || curDragEnd != RangeEnd.None && UnityGUIBugsFixer.MouseDrag())
                {
                    var snappedIndex = Mathf.Clamp(Mathf.RoundToInt((Event.current.mousePosition.x - sliderRect.x) / sliderRect.width * (selectableCount - 1)), 0, selectableCount - 1);
                    var snappedValue = (PriceType)(snappedIndex + (int)VeryCheap);
                    switch (curDragEnd)
                    {
                        case RangeEnd.Min:
                            if (range.min != snappedValue)
                            {
                                range.min = snappedValue;
                                if (range.max < range.min)
                                    range.max = range.min;
                                SoundDefOf.DragSlider.PlayOneShotOnCamera();
                                changed = true;
                                break;
                            }
                            break;
                        case RangeEnd.Max:
                            if (range.max != snappedValue)
                            {
                                range.max = snappedValue;
                                if (range.min > range.max)
                                    range.min = range.max;
                                SoundDefOf.DragSlider.PlayOneShotOnCamera();
                                changed = true;
                                break;
                            }
                            break;
                    }
                    if (Event.current.type == EventType.MouseDrag)
                        Event.current.Use();
                }
            }
            Text.Font = (GameFont)previousFont;
            return changed;
        }
    }
}
