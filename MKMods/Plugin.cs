using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace MKMods;

[BepInPlugin(Guid, Name, Version)]
public class Plugin : BaseUnityPlugin
{
    public const string Guid = "xyz.huestudios.mk.mkmods";
    public const string Name = "MK Mods";
    public const string Version = "2.1.0";

    internal static new ManualLogSource Logger;
    internal static string AssetsPath;

    // Target selection
    internal static ConfigEntry<bool> AlternativeTargeting;
    internal static ConfigEntry<bool> NextTargetOrder;
    internal static ConfigEntry<float> TargetSelectionThreshold;
    internal static ConfigEntry<bool> IgnoreAlreadySelected;

    // Warnings
    internal static ConfigEntry<bool> MissileVoiceWarnings;
    internal static ConfigEntry<bool> FuelWarnings;
    internal static ConfigEntry<float> FuelWarningMinutes;
    internal static ConfigEntry<float> BingoFuelMinutes;
    internal static ConfigEntry<float> FuelWarningUpdateRate;

    // HUD
    internal static ConfigEntry<float> ClimbRateVerticalOffset;

    // Radio chatter
    internal static ConfigEntry<bool> RadioChatterEnabled;
    internal static ConfigEntry<float> RadioChatterVolume;
    internal static ConfigEntry<float> RadioChatterMinPause;
    internal static ConfigEntry<float> RadioChatterMaxPause;

    // Approach assist
    internal static ConfigEntry<bool> ApproachAssistEnabled;
    internal static ConfigEntry<bool> ApproachAutoSelect;
    internal static ConfigEntry<KeyboardShortcut> ApproachSelectKey;

    // Bitching Ratte
    internal static ConfigEntry<bool> RatteEnabled;
    internal static ConfigEntry<bool> RatteTerrainWarnings;
    internal static ConfigEntry<bool> RatteEnvelopeWarnings;
    internal static ConfigEntry<bool> RatteCombatWarnings;
    internal static ConfigEntry<bool> RatteSystemWarnings;
    internal static ConfigEntry<bool> RatteAdvisoryCallouts;
    internal static ConfigEntry<float> RatteAltitudeFloor;
    internal static ConfigEntry<float> RattePullUpUrgency;

    private void Awake()
    {
        Logger = base.Logger;
        AssetsPath = Path.Combine(Paths.PluginPath, "MKMods", "Assets");

        BindConfig();
        MissileVoiceWarning.Initialize();
        FuelWarning.Initialize();
        BitchingRatte.Initialize();
        RadioChatter.Initialize();

        new Harmony(Guid).PatchAll();
        Logger.LogInfo($"{Name} {Version} loaded.");
    }

    private void Update()
    {
        BitchingRatte.Tick();
        RadioChatter.Tick();
        ApproachAssist.Tick();
    }

    private void BindConfig()
    {
        AlternativeTargeting = Config.Bind(
            "General", "AlternativeTargeting", true,
            "Use the alternative target selection algorithm.");
        NextTargetOrder = Config.Bind(
            "Target selection", "NextTargetOrder", false,
            "Select the next target rather than the highest priority one.");
        TargetSelectionThreshold = Config.Bind(
            "Target selection", "TargetSelectionThreshold", 200f,
            "The maximum distance from the target designator to consider a target.");
        IgnoreAlreadySelected = Config.Bind(
            "Target selection", "IgnoreAlreadySelected", false,
            "If NextTargetOrder is false, always select the highest priority target even if it is already selected.");

        MissileVoiceWarnings = Config.Bind(
            "Warnings", "MissileVoiceWarnings", true,
            "Enable missile voice warnings.");
        FuelWarnings = Config.Bind(
            "Warnings", "FuelWarnings", true,
            "Enable fuel warnings.");
        FuelWarningMinutes = Config.Bind(
            "Warnings", "FuelWarningMinutes", 7f,
            "Remaining fuel minutes below which the low fuel warning plays.");
        BingoFuelMinutes = Config.Bind(
            "Warnings", "BingoFuelMinutes", 3f,
            "Remaining fuel minutes below which the bingo fuel warning plays.");
        FuelWarningUpdateRate = Config.Bind(
            "Warnings", "FuelWarningUpdateRate", 10f,
            "Seconds between fuel level samples.");

        ClimbRateVerticalOffset = Config.Bind(
            "HUD", "ClimbRateVerticalOffset", 40f,
            "Moves the climb rate (+/- m) readout up by this many pixels (negative moves it down, 0 disables).");

        RadioChatterEnabled = Config.Bind(
            "Radio Chatter", "Enabled", true,
            "Play ambient radio chatter in the background while flying.");
        RadioChatterVolume = Config.Bind(
            "Radio Chatter", "Volume", 0.5f,
            "Chatter volume relative to other interface audio (warnings play at 3.0).");
        RadioChatterMinPause = Config.Bind(
            "Radio Chatter", "MinPauseSeconds", 45f,
            "Minimum silence between chatter conversations.");
        RadioChatterMaxPause = Config.Bind(
            "Radio Chatter", "MaxPauseSeconds", 180f,
            "Maximum silence between chatter conversations.");

        ApproachAssistEnabled = Config.Bind(
            "Approach Assist", "Enabled", true,
            "Select a friendly airbase for landing guidance at any range.");
        ApproachAutoSelect = Config.Bind(
            "Approach Assist", "AutoSelectOnGearDown", true,
            "Lowering the gear automatically selects the nearest friendly airbase (no keybind needed).");
        ApproachSelectKey = Config.Bind(
            "Approach Assist", "SelectKey", new KeyboardShortcut(UnityEngine.KeyCode.L),
            "Optional: cycles friendly airbases nearest-first; one press past the last turns guidance off. Also accepts JoystickButton0-19 for HOTAS/controller. Clicking an airbase icon on the maximized map works too.");

        RatteEnabled = Config.Bind(
            "Bitching Ratte", "Enabled", true,
            "Enable the Bitching Ratte voice warning system.");
        RatteTerrainWarnings = Config.Bind(
            "Bitching Ratte", "TerrainWarnings", true,
            "Pull up, altitude, sink rate, roll and gear-up-landing warnings.");
        RatteEnvelopeWarnings = Config.Bind(
            "Bitching Ratte", "EnvelopeWarnings", true,
            "Stall, over-G and overspeed warnings.");
        RatteCombatWarnings = Config.Bind(
            "Bitching Ratte", "CombatWarnings", true,
            "Radar lock warning plus flare and electronic warfare low/out callouts.");
        RatteSystemWarnings = Config.Bind(
            "Bitching Ratte", "SystemWarnings", true,
            "Engine failure, fire and damage callouts (replaces the native engine failure audio).");
        RatteAdvisoryCallouts = Config.Bind(
            "Bitching Ratte", "AdvisoryCallouts", true,
            "Gear up/down and flight assist callouts.");
        RatteAltitudeFloor = Config.Bind(
            "Bitching Ratte", "AltitudeFloorMeters", 150f,
            "Radar altitude below which the altitude warning plays (gear up, descending).");
        RattePullUpUrgency = Config.Bind(
            "Bitching Ratte", "PullUpUrgency", 1.5f,
            "Terrain closure urgency required to trigger the pull up warning (lower = earlier).");
    }
}
