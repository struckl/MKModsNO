using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace MKMods;

/// <summary>
/// Shows the estimated remaining fuel time next to the fuel gauge and plays
/// low fuel / bingo fuel voice warnings when it drops below the configured
/// thresholds. Fuel time is estimated from consumption between samples.
/// </summary>
internal static class FuelWarning
{
    private const string LabelName = "MKModsFuelTime";
    private const float LabelCreationDelaySeconds = 3f;
    private const float LabelVerticalOffset = -20f;

    private static AudioClip lowFuelClip;
    private static AudioClip bingoFuelClip;

    private static float lowFuelSeconds;
    private static float bingoFuelSeconds;
    private static float sampleIntervalSeconds;

    private static Text fuelTimeLabel;
    private static float labelReadyTime = float.PositiveInfinity;
    private static float lastFuelLevel;
    private static float lastSampleTime;

    public static void Initialize()
    {
        if (!Plugin.FuelWarnings.Value)
            return;

        lowFuelSeconds = Plugin.FuelWarningMinutes.Value * 60f;
        bingoFuelSeconds = Plugin.BingoFuelMinutes.Value * 60f;
        sampleIntervalSeconds = Plugin.FuelWarningUpdateRate.Value;
        lowFuelClip = AudioLoader.Load("fuel low.mp3");
        bingoFuelClip = AudioLoader.Load("bingo fuel.mp3");
    }

    public static void OnGaugeInitialized(Aircraft aircraft)
    {
        fuelTimeLabel = null;
        lastFuelLevel = aircraft.GetFuelLevel();
        lastSampleTime = Time.timeSinceLevelLoad;
        // Give the HUD a moment to finish its own layout before cloning a label.
        labelReadyTime = Time.timeSinceLevelLoad + LabelCreationDelaySeconds;
    }

    public static void OnGaugeRefreshed(FuelGauge gauge, Aircraft aircraft, Text fuelLabel)
    {
        if (aircraft == null)
            return;
        if (fuelTimeLabel == null && !TryCreateLabel(gauge, fuelLabel))
            return;

        float elapsed = Time.timeSinceLevelLoad - lastSampleTime;
        if (elapsed < sampleIntervalSeconds)
            return;

        float fuelLevel = aircraft.GetFuelLevel();
        float burnPerSecond = (lastFuelLevel - fuelLevel) / elapsed;
        lastSampleTime = Time.timeSinceLevelLoad;
        lastFuelLevel = fuelLevel;

        float secondsRemaining = burnPerSecond > 0f ? fuelLevel / burnPerSecond : float.PositiveInfinity;
        fuelTimeLabel.text = float.IsInfinity(secondsRemaining)
            ? "(...)"
            : $"({Mathf.FloorToInt(secondsRemaining / 60f)}m)";

        if (secondsRemaining < bingoFuelSeconds)
            VoiceQueue.Say("bingo fuel", bingoFuelClip, CalloutPriority.Fuel, 0f);
        else if (secondsRemaining < lowFuelSeconds)
            VoiceQueue.Say("fuel low", lowFuelClip, CalloutPriority.Fuel, 0f);
    }

    private static bool TryCreateLabel(FuelGauge gauge, Text fuelLabel)
    {
        if (Time.timeSinceLevelLoad < labelReadyTime || fuelLabel == null)
            return false;

        Transform existing = gauge.transform.Find(LabelName);
        GameObject labelObject;
        if (existing != null)
        {
            labelObject = existing.gameObject;
        }
        else
        {
            labelObject = Object.Instantiate(fuelLabel.gameObject, fuelLabel.transform.parent);
            labelObject.name = LabelName;
            var rectTransform = labelObject.GetComponent<RectTransform>();
            rectTransform.anchoredPosition += new Vector2(0f, LabelVerticalOffset);
        }

        fuelTimeLabel = labelObject.GetComponent<Text>();
        if (fuelTimeLabel == null)
        {
            Plugin.Logger.LogError("Fuel time label has no Text component; fuel readout disabled.");
            labelReadyTime = float.PositiveInfinity;
            return false;
        }

        fuelTimeLabel.text = "(...)";
        return true;
    }
}

[HarmonyPatch(typeof(FuelGauge))]
internal static class FuelGaugePatches
{
    [HarmonyPostfix]
    [HarmonyPatch("Initialize")]
    private static void Initialize(Aircraft aircraft)
    {
        if (Plugin.FuelWarnings.Value && aircraft != null)
            FuelWarning.OnGaugeInitialized(aircraft);
    }

    [HarmonyPostfix]
    [HarmonyPatch("Refresh")]
    private static void Refresh(FuelGauge __instance, Aircraft ___aircraft, Text ___fuelLabel)
    {
        if (Plugin.FuelWarnings.Value)
            FuelWarning.OnGaugeRefreshed(__instance, ___aircraft, ___fuelLabel);
    }
}
