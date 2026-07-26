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
    public const string Version = "4.0.0";

    internal static new ManualLogSource Logger;

    // Target selection
    internal static ConfigEntry<bool> AlternativeTargeting;
    internal static ConfigEntry<bool> NextTargetOrder;
    internal static ConfigEntry<float> TargetSelectionThreshold;
    internal static ConfigEntry<bool> IgnoreAlreadySelected;

    // HUD
    internal static ConfigEntry<float> ClimbRateVerticalOffset;
    internal static ConfigEntry<bool> FuelTimeReadout;
    internal static ConfigEntry<float> FuelTimeUpdateRate;

    // Approach assist
    internal static ConfigEntry<bool> ApproachAssistEnabled;
    internal static ConfigEntry<bool> ApproachAutoSelect;
    internal static ConfigEntry<KeyboardShortcut> ApproachSelectKey;

    private void Awake()
    {
        Logger = base.Logger;

        BindConfig();
        FuelTime.Initialize();

        new Harmony(Guid).PatchAll();
        Logger.LogInfo($"{Name} {Version} loaded.");
    }

    private void Update()
    {
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

        ClimbRateVerticalOffset = Config.Bind(
            "HUD", "ClimbRateVerticalOffset", 40f,
            "Moves the climb rate (+/- m) readout up by this many pixels (negative moves it down, 0 disables).");
        FuelTimeReadout = Config.Bind(
            "HUD", "FuelTimeReadout", true,
            "Show the estimated remaining fuel time next to the fuel gauge.");
        FuelTimeUpdateRate = Config.Bind(
            "HUD", "FuelTimeUpdateRate", 10f,
            "Seconds between fuel level samples used to estimate the remaining time.");

        ApproachAssistEnabled = Config.Bind(
            "Approach Assist", "Enabled", true,
            "Select a friendly airbase for landing guidance at any range.");
        ApproachAutoSelect = Config.Bind(
            "Approach Assist", "AutoSelectOnGearDown", true,
            "Lowering the gear automatically selects the nearest friendly airbase (no keybind needed).");
        ApproachSelectKey = Config.Bind(
            "Approach Assist", "SelectKey", new KeyboardShortcut(UnityEngine.KeyCode.L),
            "Optional: cycles friendly airbases nearest-first; one press past the last turns guidance off. Also accepts JoystickButton0-19 for HOTAS/controller. Clicking an airbase icon on the maximized map works too.");
    }
}
