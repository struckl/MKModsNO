using System.Collections.Generic;
using BepInEx.Configuration;

namespace MKMods;

/// <summary>
/// One config toggle per voice clip, checked by <see cref="VoiceQueue"/> before
/// anything is queued. These sit below the per-group switches: a callout plays
/// only if both its group and its own toggle are on.
/// </summary>
internal static class SoundToggles
{
    // Section, config name, description, and the VoiceQueue keys it covers.
    // A sound shared by several triggers (notch for both radar seekers) lists
    // every key so one toggle governs all of them.
    private static readonly (string section, string name, string description, string[] keys)[] Sounds =
    {
        ("Callouts (Terrain)", "Pull up", "Terrain closure warning.", new[] { "pull up" }),
        ("Callouts (Terrain)", "Altitude", "Altitude floor warning.", new[] { "altitude" }),
        ("Callouts (Terrain)", "Sink rate", "Excessive descent rate warning.", new[] { "sink rate" }),
        ("Callouts (Terrain)", "Roll left / roll right", "Inverted near the ground.", new[] { "roll left", "roll right" }),
        ("Callouts (Terrain)", "Gear", "Descending low and slow with the gear up.", new[] { "gear" }),

        ("Callouts (Envelope)", "Stall", "High angle of attack warning.", new[] { "stall" }),
        ("Callouts (Envelope)", "Over G", "Exceeding the airframe G limit.", new[] { "over g" }),
        ("Callouts (Envelope)", "Overspeed", "Exceeding maximum speed.", new[] { "overspeed" }),

        ("Callouts (Combat)", "Warning (radar lock)", "A hostile radar has locked on.", new[] { "warning" }),
        ("Callouts (Combat)", "Flare (IR missile)", "Infrared missile inbound.", new[] { "missile IR" }),
        ("Callouts (Combat)", "Hide (optical missile)", "Optically guided missile inbound.", new[] { "missile Optical" }),
        ("Callouts (Combat)", "Notch (radar missile)", "Active or semi-active radar missile inbound.", new[] { "missile ARH", "missile SARH" }),
        ("Callouts (Combat)", "Radar (anti-radiation missile)", "Anti-radiation missile inbound.", new[] { "missile ARAD" }),
        ("Callouts (Combat)", "Flares low", "Flare stock down to a quarter.", new[] { "flares low" }),
        ("Callouts (Combat)", "Flares out", "Flares exhausted.", new[] { "flares out" }),
        ("Callouts (Combat)", "Electronic warfare low", "Electronic countermeasures down to a quarter.", new[] { "electronic warfare low" }),
        ("Callouts (Combat)", "Electronic warfare out", "Electronic countermeasures exhausted.", new[] { "electronic warfare out" }),

        ("Callouts (Systems)", "Engine failure", "An engine has died (replaces the native audio report).", new[] { "engine failure" }),
        ("Callouts (Systems)", "Engine fire", "A fuel tank is on fire.", new[] { "engine fire" }),
        ("Callouts (Systems)", "Damage", "A fuel tank has been punctured.", new[] { "damage" }),

        ("Callouts (Advisory)", "Gear up", "Gear locked up.", new[] { "gear up" }),
        ("Callouts (Advisory)", "Gear down", "Gear locked down.", new[] { "gear down" }),
        ("Callouts (Advisory)", "Radar armed", "Radar switched on.", new[] { "radar armed" }),
        ("Callouts (Advisory)", "Radar disarmed", "Radar switched off.", new[] { "radar disarmed" }),
        ("Callouts (Advisory)", "Autopilot", "Flight assist disabled.", new[] { "autopilot" }),

        ("Callouts (Fuel)", "Fuel low", "Remaining fuel below the low fuel threshold.", new[] { "fuel low" }),
        ("Callouts (Fuel)", "Bingo fuel", "Remaining fuel below the bingo threshold.", new[] { "bingo fuel" }),
    };

    private static readonly Dictionary<string, ConfigEntry<bool>> Toggles =
        new Dictionary<string, ConfigEntry<bool>>();

    public static void Bind(ConfigFile config)
    {
        foreach ((string section, string name, string description, string[] keys) in Sounds)
        {
            ConfigEntry<bool> entry = config.Bind(section, name, true, description);
            foreach (string key in keys)
                Toggles[key] = entry;
        }
    }

    /// <summary>Unknown keys default to enabled so new callouts are never silently muted.</summary>
    public static bool IsEnabled(string key)
    {
        return !Toggles.TryGetValue(key, out ConfigEntry<bool> entry) || entry.Value;
    }
}
