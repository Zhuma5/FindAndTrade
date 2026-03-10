using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace MGAutoSell.Extensions
{
    public static class WidgetRowExtensions
    {
        private static Dictionary<string, Vector2> LabelSizeCache = new();
        public static Rect TextFieldInt(this WidgetRow row, ref int val, ref string buffer, int width)
        {
            row.Gap(width + TradeRuleDrawUtility.AnnoyingUnavoidableGap);
            var rect = new Rect(row.FinalX + 2f, row.FinalY, width, 24f);

            var str = Widgets.TextField(rect, buffer);
            if (str == buffer)
                return rect;

            if (int.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                val = Mathf.Clamp(parsed, 0, (int)1E+09);
                buffer = val.ToString(CultureInfo.InvariantCulture);
            }
            else if (str.Length == 0 || str == "-")
            {
                buffer = str;
            }

            return rect;
        }

        public static Rect LabelFast(this WidgetRow row, string text, string tooltip = null, float height = -1f)
        {
            if (LabelSizeCache.TryGetValue(text, out var size)) 
                return row.Label(text, size.x, tooltip, height);


            size = Text.CalcSize(text);
            LabelSizeCache.Add(text, size);

            return row.Label(text, size.x, tooltip, height);
        }
    }
}
