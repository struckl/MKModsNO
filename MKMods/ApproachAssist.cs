using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace MKMods;

/// <summary>
/// Approach assist: pick a friendly airbase (keybind cycles by distance, or
/// click its map icon in flight) and the game's native AirbaseOverlay draws
/// the landing glideslope for it at any range — not just when stumbling into
/// the 5 km auto-trigger radius. Lower the gear to see the glideslope line.
/// </summary>
internal static class ApproachAssist
{
    private static Aircraft aircraft;
    private static Airbase chosenAirbase;

    public static Airbase ChosenAirbase => chosenAirbase;

    public static void SetAircraft(Aircraft newAircraft)
    {
        if (aircraft != null)
            aircraft.onSetGear -= OnSetGear;
        aircraft = newAircraft;
        chosenAirbase = null;
        if (aircraft != null)
            aircraft.onSetGear += OnSetGear;
    }

    /// <summary>Gear down anywhere = intent to land: pick the nearest base, no button needed.</summary>
    private static void OnSetGear(Aircraft.OnSetGear e)
    {
        if (!Plugin.ApproachAssistEnabled.Value || !Plugin.ApproachAutoSelect.Value)
            return;
        if (e.gearState != LandingGear.GearState.Extending
            && e.gearState != LandingGear.GearState.LockedExtended)
            return;
        if (chosenAirbase != null || !HasLiveAircraft() || aircraft.NetworkHQ == null
            || aircraft.radarAlt < 5f)
            return;

        List<Airbase> bases = GetFriendlyLandingBases();
        if (bases.Count > 0)
            Select(bases[0]);
    }

    public static void Tick()
    {
        if (!Plugin.ApproachAssistEnabled.Value || !Plugin.ApproachSelectKey.Value.IsDown())
            return;
        if (aircraft == null || aircraft.disabled || aircraft.NetworkHQ == null)
            return;
        CycleAirbase();
    }

    /// <summary>Key press: nearest base first, then cycle outwards, then off.</summary>
    private static void CycleAirbase()
    {
        List<Airbase> bases = GetFriendlyLandingBases();
        if (bases.Count == 0)
        {
            chosenAirbase = null;
            Report("No friendly airbase with a runway available");
            return;
        }

        int next = chosenAirbase != null ? bases.IndexOf(chosenAirbase) + 1 : 0;
        if (next >= bases.Count)
        {
            chosenAirbase = null;
            SceneSingleton<DynamicMap>.i?.DeselectAllIcons();
            Report("Approach guidance off");
            return;
        }

        Select(bases[next]);
    }

    public static void Select(Airbase airbase)
    {
        chosenAirbase = airbase;
        string runway = "";
        Airbase.Runway.RunwayUsage? usage = RequestLanding(airbase);
        if (usage.HasValue)
            runway = ", runway " + usage.Value.GetName();
        Report($"Approach: {airbase.SavedAirbase.DisplayName}{runway} — lower gear for glideslope");

        // Highlight the chosen base on the map, same as spawn selection does.
        DynamicMap map = SceneSingleton<DynamicMap>.i;
        if (map != null)
        {
            map.DeselectAllIcons();
            map.SelectIcon(airbase);
        }
    }

    private static List<Airbase> GetFriendlyLandingBases()
    {
        var bases = new List<Airbase>();
        foreach (Airbase airbase in aircraft.NetworkHQ.GetAirbases())
        {
            if (airbase != null && airbase.GetLandingRunway() != null)
                bases.Add(airbase);
        }
        Vector3 position = aircraft.transform.position;
        bases.Sort((a, b) =>
            (a.center.position - position).sqrMagnitude.CompareTo(
                (b.center.position - position).sqrMagnitude));
        return bases;
    }

    /// <summary>Same runway query the native overlay builds for auto-landing.</summary>
    public static Airbase.Runway.RunwayUsage? RequestLanding(Airbase airbase)
    {
        AircraftParameters parameters = aircraft.GetAircraftParameters();
        float landingSpeed = Mathf.Sqrt(
            aircraft.GetMass() / aircraft.definition.aircraftInfo.maxWeight) * parameters.takeoffSpeed;
        RunwayQuery query = new RunwayQuery
        {
            RunwayType = RunwayQueryType.Any,
            MinSize = parameters.takeoffDistance,
            TailHook = aircraft.weaponManager.HasTailHook(),
            LandingSpeed = landingSpeed,
        };
        return airbase.RequestLanding(aircraft, query);
    }

    public static bool HasLiveAircraft()
    {
        return aircraft != null && !aircraft.disabled;
    }

    public static bool IsReadyToForce()
    {
        return Plugin.ApproachAssistEnabled.Value && chosenAirbase != null
            && HasLiveAircraft() && aircraft.pilots.Length > 0
            && aircraft.pilots[0].flightInfo.HasTakenOff;
    }

    private static void Report(string text)
    {
        SceneSingleton<AircraftActionsReport>.i?.ReportText(text, 6f);
    }
}

/// <summary>
/// After the native overlay evaluated its own nearest-airbase pass (every 2 s),
/// overwrite its target with the chosen base; the native LateUpdate then
/// renders the glideslope and markers without further help.
/// </summary>
[HarmonyPatch(typeof(AirbaseOverlay), "UpdateNearestAirbase")]
internal static class ApproachAssistOverlayPatch
{
    private static readonly FieldInfo NearestAirbaseField =
        AccessTools.Field(typeof(AirbaseOverlay), "nearestAirbase");
    private static readonly FieldInfo RunwayUsageField =
        AccessTools.Field(typeof(AirbaseOverlay), "runwayUsage");

    private static void Postfix(AirbaseOverlay __instance)
    {
        if (!ApproachAssist.IsReadyToForce()
            || NearestAirbaseField == null || RunwayUsageField == null)
            return;

        Airbase.Runway.RunwayUsage? usage = ApproachAssist.RequestLanding(ApproachAssist.ChosenAirbase);
        if (!usage.HasValue)
            return;
        NearestAirbaseField.SetValue(__instance, ApproachAssist.ChosenAirbase);
        RunwayUsageField.SetValue(__instance, usage.Value);
    }
}

/// <summary>Track the local aircraft for the assist.</summary>
[HarmonyPatch(typeof(CombatHUD), "SetAircraft")]
internal static class ApproachAssistAircraftPatch
{
    private static void Postfix(Aircraft aircraft)
    {
        ApproachAssist.SetAircraft(aircraft);
    }
}

/// <summary>
/// Airbase map icons are click-dead while flying (the native handler only
/// serves spawn selection). In flight, a click picks the approach base instead.
/// </summary>
[HarmonyPatch(typeof(AirbaseMapIcon), "ClickIcon")]
internal static class ApproachAssistMapClickPatch
{
    private static void Postfix(AirbaseMapIcon __instance)
    {
        if (!Plugin.ApproachAssistEnabled.Value || !ApproachAssist.HasLiveAircraft())
            return;
        if (__instance.airbase != null)
            ApproachAssist.Select(__instance.airbase);
    }
}
