using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace MKMods;

/// <summary>
/// Replaces the vanilla targeting algorithm: tapping the targeting key selects
/// the next highest-priority target (replacing the current selection), holding
/// it (paint) adds targets to the selection instead.
/// </summary>
[HarmonyPatch(typeof(CombatHUD), "TargetSelect")]
internal static class TargetSelectPatch
{
    private static bool Prefix(
        CombatHUD __instance,
        bool paint,
        List<HUDUnitMarker> ___markers,
        List<Unit> ___targetList,
        WeaponStation ___currentWeaponStation,
        AudioClip ___selectSound)
    {
        if (!Plugin.AlternativeTargeting.Value)
            return true; // fall through to vanilla targeting

        GameObject designator = __instance.targetDesignator.gameObject;
        List<HUDUnitMarker> eligible = FindEligibleTargets(___markers, designator.transform.position);
        if (eligible.Count == 0)
            return false;

        SortByPriorityDescending(eligible, __instance.aircraft, designator, ___currentWeaponStation);

        Unit currentTarget = ___targetList.Count > 0 ? ___targetList[___targetList.Count - 1] : null;
        int currentIndex = currentTarget != null
            ? eligible.FindIndex(marker => marker.unit == currentTarget)
            : -1;

        bool replaceSelection = !paint || ___targetList.Count == 0;
        int nextIndex = FindNextTargetIndex(eligible, ___targetList, currentIndex, replaceSelection);
        if (nextIndex == -1)
            return false;

        HUDUnitMarker next = eligible[nextIndex];
        if (replaceSelection)
        {
            if (currentTarget != null)
            {
                if (___targetList.Count == 1)
                    __instance.DeSelectUnit(currentTarget);
                else
                    __instance.DeselectAll(withAudio: true);
            }
            ___targetList.Add(next.unit);
            if (currentTarget != next.unit)
                ConfirmSelection(__instance, next, ___selectSound);
        }
        else
        {
            ___targetList.Add(next.unit);
            ConfirmSelection(__instance, next, ___selectSound);
        }

        return false;
    }

    private static List<HUDUnitMarker> FindEligibleTargets(
        List<HUDUnitMarker> markers, Vector3 designatorPosition)
    {
        float threshold = Plugin.TargetSelectionThreshold.Value;
        var eligible = new List<HUDUnitMarker>();
        foreach (HUDUnitMarker marker in markers)
        {
            if (marker.image.enabled
                && !SceneSingleton<TargetListSelector>.i.CheckExclusions(marker.unit)
                && FastMath.Distance(designatorPosition, marker.image.transform.position) < threshold)
            {
                eligible.Add(marker);
            }
        }
        return eligible;
    }

    private static void SortByPriorityDescending(
        List<HUDUnitMarker> markers, Aircraft aircraft, GameObject designator, WeaponStation weaponStation)
    {
        var priority = new Dictionary<HUDUnitMarker, float>(markers.Count);
        foreach (HUDUnitMarker marker in markers)
            priority[marker] = marker.AssessPriority(aircraft, designator, weaponStation);
        markers.Sort((a, b) => priority[b].CompareTo(priority[a]));
    }

    private static int FindNextTargetIndex(
        List<HUDUnitMarker> eligible, List<Unit> targetList, int currentIndex, bool replaceSelection)
    {
        if (Plugin.NextTargetOrder.Value)
        {
            // Cycle through eligible targets in priority order.
            if (replaceSelection)
                return (currentIndex + 1) % eligible.Count;

            for (int step = 1; step <= eligible.Count; step++)
            {
                int candidate = (currentIndex + step) % eligible.Count;
                if (!targetList.Contains(eligible[candidate].unit))
                    return candidate;
            }
            return -1;
        }

        // Highest-priority mode.
        if (replaceSelection && Plugin.IgnoreAlreadySelected.Value)
            return 0;

        for (int i = 0; i < eligible.Count; i++)
        {
            if (!targetList.Contains(eligible[i].unit))
                return i;
        }
        return -1;
    }

    private static void ConfirmSelection(CombatHUD hud, HUDUnitMarker marker, AudioClip selectSound)
    {
        marker.SelectMarker();
        SoundManager.PlayInterfaceOneShot(selectSound);
        hud.aircraft.weaponManager.TargetListChanged();
    }
}
