using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;

namespace MKMods;

/// <summary>
/// Ambient low-volume radio chatter while flying: plays random short
/// transmissions from Assets/chatter with random pauses in between.
/// </summary>
internal static class RadioChatter
{
    private static readonly List<AudioClip> Clips = new List<AudioClip>();
    private static Aircraft aircraft;
    private static float nextChatterTime;
    private static int lastClipIndex = -1;

    public static void Initialize()
    {
        if (!Plugin.RadioChatterEnabled.Value)
            return;
        string chatterPath = Path.Combine(Plugin.AssetsPath, "chatter");
        if (!Directory.Exists(chatterPath))
            return;
        foreach (string file in Directory.GetFiles(chatterPath, "*.mp3"))
        {
            AudioClip clip = AudioLoader.Load(Path.Combine("chatter", Path.GetFileName(file)));
            if (clip != null)
                Clips.Add(clip);
        }
    }

    public static void SetAircraft(Aircraft newAircraft)
    {
        aircraft = newAircraft;
        ScheduleNext();
    }

    public static void Tick()
    {
        if (Clips.Count == 0 || aircraft == null || aircraft.disabled)
            return;
        if (Time.unscaledTime < nextChatterTime)
            return;

        int index;
        do
        {
            index = Random.Range(0, Clips.Count);
        } while (Clips.Count > 1 && index == lastClipIndex);
        lastClipIndex = index;

        InterfaceAudio.PlayOneShot(Clips[index], Plugin.RadioChatterVolume.Value);
        ScheduleNext(Clips[index].length);
    }

    private static void ScheduleNext(float clipLength = 0f)
    {
        nextChatterTime = Time.unscaledTime + clipLength + Random.Range(
            Plugin.RadioChatterMinPause.Value,
            Plugin.RadioChatterMaxPause.Value);
    }
}

[HarmonyPatch(typeof(CombatHUD), "SetAircraft")]
internal static class RadioChatterAircraftPatch
{
    private static void Postfix(Aircraft aircraft)
    {
        RadioChatter.SetAircraft(aircraft);
    }
}
