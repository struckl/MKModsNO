using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace MKMods;

/// <summary>
/// Plays a voice warning while missiles are locked onto the player, naming the
/// appropriate countermeasure for the seeker type. Multiple simultaneous threat
/// types are announced in rotation, one per cooldown interval.
/// </summary>
internal static class MissileVoiceWarning
{
    // Seeker ids as reported by MissileSeeker.GetSeekerType().
    private static readonly (string type, string file)[] Warnings =
    {
        ("IR", "flare.mp3"),
        ("Optical", "hide.mp3"),
        ("ARH", "notch.mp3"),
        ("SARH", "notch.mp3"),
        ("ARAD", "radar.mp3"),
    };

    private const float VoiceCooldownSeconds = 1f;
    private const float Volume = 3f;

    private static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();
    private static readonly Dictionary<string, int> IncomingCounts = new Dictionary<string, int>();
    private static int rotationIndex;
    private static float lastPlayedTime;

    public static void Initialize()
    {
        if (!Plugin.MissileVoiceWarnings.Value)
            return;

        foreach ((string type, string file) in Warnings)
        {
            AudioClip clip = AudioLoader.Load(file);
            if (clip != null)
                Clips[type] = clip;
        }
        Reset();
    }

    public static void Reset()
    {
        IncomingCounts.Clear();
        foreach ((string type, _) in Warnings)
            IncomingCounts[type] = 0;
        rotationIndex = 0;
        lastPlayedTime = 0f;
    }

    public static void OnMissileDetected(string seekerType)
    {
        // Unknown seeker types (new game versions, no audio mapping) are ignored.
        if (IncomingCounts.TryGetValue(seekerType, out int count))
            IncomingCounts[seekerType] = count + 1;
    }

    public static void OnMissileLost(string seekerType)
    {
        if (IncomingCounts.TryGetValue(seekerType, out int count) && count > 0)
            IncomingCounts[seekerType] = count - 1;
    }

    public static void PlayPendingWarning()
    {
        if (Time.timeSinceLevelLoad - lastPlayedTime < VoiceCooldownSeconds)
            return;

        for (int step = 1; step <= Warnings.Length; step++)
        {
            int candidate = (rotationIndex + step) % Warnings.Length;
            string type = Warnings[candidate].type;
            if (IncomingCounts[type] > 0 && Clips.TryGetValue(type, out AudioClip clip))
            {
                InterfaceAudio.PlayOneShot(clip, Volume);
                lastPlayedTime = Time.timeSinceLevelLoad;
                rotationIndex = candidate;
                return;
            }
        }
    }
}

/// <summary>
/// Hooks the HUD threat list to track how many missiles of each seeker type are
/// currently locked onto the player.
/// </summary>
[HarmonyPatch(typeof(ThreatList))]
internal static class ThreatListPatches
{
    [HarmonyPrefix]
    [HarmonyPatch("SetAircraft")]
    private static void SetAircraft()
    {
        if (Plugin.MissileVoiceWarnings.Value)
            MissileVoiceWarning.Reset();
    }

    [HarmonyPrefix]
    [HarmonyPatch("ThreatList_OnMissileWarning")]
    private static void OnMissileWarning(
        ref MissileWarning.OnMissileWarning e,
        Dictionary<PersistentID, ThreatItem> ___itemLookup)
    {
        // Only count missiles the threat list does not know about yet; the
        // original method adds them to the lookup after this prefix runs.
        if (Plugin.MissileVoiceWarnings.Value && !___itemLookup.ContainsKey(e.missile.persistentID))
            MissileVoiceWarning.OnMissileDetected(e.missile.GetSeekerType());
    }

    [HarmonyPrefix]
    [HarmonyPatch("ThreatList_OffMissileWarning")]
    private static void OffMissileWarning(
        ref MissileWarning.OffMissileWarning e,
        Dictionary<PersistentID, ThreatItem> ___itemLookup)
    {
        if (Plugin.MissileVoiceWarnings.Value && ___itemLookup.ContainsKey(e.missile.persistentID))
            MissileVoiceWarning.OnMissileLost(e.missile.GetSeekerType());
    }

    [HarmonyPostfix]
    [HarmonyPatch("Update")]
    private static void Update()
    {
        if (Plugin.MissileVoiceWarnings.Value)
            MissileVoiceWarning.PlayPendingWarning();
    }
}
