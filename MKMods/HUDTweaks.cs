using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace MKMods;

/// <summary>
/// Moves the climb rate readout (the "+10 m / -5 m" text next to the altitude
/// display) up by a configurable amount; its default position sits too low.
/// </summary>
[HarmonyPatch(typeof(Climbrate), "Initialize")]
internal static class ClimbRatePositionPatch
{
    private static readonly HashSet<int> Adjusted = new HashSet<int>();

    private static void Postfix(Climbrate __instance)
    {
        float offset = Plugin.ClimbRateVerticalOffset.Value;
        if (offset == 0f || !Adjusted.Add(__instance.GetInstanceID()))
            return;
        if (__instance.transform is RectTransform rect)
            rect.anchoredPosition += new Vector2(0f, offset);
    }
}
