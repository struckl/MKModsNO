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
    public const string Version = "2.0.0";

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

    private void Awake()
    {
        Logger = base.Logger;
        AssetsPath = Path.Combine(Paths.PluginPath, "MKMods", "Assets");

        BindConfig();
        MissileVoiceWarning.Initialize();
        FuelWarning.Initialize();

        new Harmony(Guid).PatchAll();
        Logger.LogInfo($"{Name} {Version} loaded.");
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
    }
}
