using UnityEngine;
using Verse;

namespace MGAutoSell
{
    [StaticConstructorOnStartup]
    internal class Textures
    {
        internal static readonly Texture2D Drag = ContentFinder<Texture2D>.Get("Drag");
        internal static readonly Texture2D OptionsGeneral = ContentFinder<Texture2D>.Get("UI/Icons/Options/OptionsGeneral");
        internal static readonly Texture2D JunkGizmo = ContentFinder<Texture2D>.Get("JunkGizmo");
        internal static readonly Texture2D FloatRangeSliderTex = ContentFinder<Texture2D>.Get("UI/Widgets/RangeSlider");

    }
}
