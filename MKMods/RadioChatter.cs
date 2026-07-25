using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;

namespace MKMods;

/// <summary>
/// Ambient low-volume radio chatter while flying. Clips are grouped into
/// conversations (pilot and controller trading transmissions): files named
/// chatter01a/chatter01b/chatter01c form one exchange played in sequence with
/// short radio pauses, then a long random silence until the next exchange.
/// </summary>
internal static class RadioChatter
{
    private const float MinLineGapSeconds = 0.8f;
    private const float MaxLineGapSeconds = 1.8f;
    private const float WarningBackoffSeconds = 5f;

    private static readonly List<List<AudioClip>> Exchanges = new List<List<AudioClip>>();
    private static Aircraft aircraft;
    private static float nextLineTime;
    private static List<AudioClip> currentExchange;
    private static int lineIndex;
    private static int lastExchangeIndex = -1;

    public static void Initialize()
    {
        if (!Plugin.RadioChatterEnabled.Value)
            return;
        string chatterPath = Path.Combine(Plugin.AssetsPath, "chatter");
        if (!Directory.Exists(chatterPath))
            return;

        // "chatter01a" -> exchange "chatter01", line "a"; sorted names keep line order.
        var grouped = new SortedDictionary<string, List<AudioClip>>();
        List<string> files = new List<string>(Directory.GetFiles(chatterPath, "*.mp3"));
        files.Sort();
        foreach (string file in files)
        {
            string name = Path.GetFileNameWithoutExtension(file);
            if (name.Length < 2)
                continue;
            string exchangeKey = name.Substring(0, name.Length - 1);
            AudioClip clip = AudioLoader.Load(Path.Combine("chatter", Path.GetFileName(file)));
            if (clip == null)
                continue;
            if (!grouped.TryGetValue(exchangeKey, out List<AudioClip> lines))
            {
                lines = new List<AudioClip>();
                grouped[exchangeKey] = lines;
            }
            lines.Add(clip);
        }
        foreach (List<AudioClip> lines in grouped.Values)
            Exchanges.Add(lines);
    }

    public static void SetAircraft(Aircraft newAircraft)
    {
        aircraft = newAircraft;
        currentExchange = null;
        ScheduleNextExchange(0f);
    }

    public static void Tick()
    {
        if (Exchanges.Count == 0 || aircraft == null || aircraft.disabled)
            return;
        if (Time.unscaledTime < nextLineTime)
            return;

        if (currentExchange == null)
        {
            // Don't start a conversation while a warning is talking or queued.
            if (VoiceQueue.IsBusy)
            {
                nextLineTime = Time.unscaledTime + WarningBackoffSeconds;
                return;
            }
            int index;
            do
            {
                index = Random.Range(0, Exchanges.Count);
            } while (Exchanges.Count > 1 && index == lastExchangeIndex);
            lastExchangeIndex = index;
            currentExchange = Exchanges[index];
            lineIndex = 0;
        }

        AudioClip line = currentExchange[lineIndex];
        InterfaceAudio.PlayOneShot(line, Plugin.RadioChatterVolume.Value);
        lineIndex++;

        if (lineIndex >= currentExchange.Count)
        {
            currentExchange = null;
            ScheduleNextExchange(line.length);
        }
        else
        {
            nextLineTime = Time.unscaledTime + line.length
                + Random.Range(MinLineGapSeconds, MaxLineGapSeconds);
        }
    }

    private static void ScheduleNextExchange(float clipLength)
    {
        nextLineTime = Time.unscaledTime + clipLength + Random.Range(
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
