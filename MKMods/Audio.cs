using System.IO;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

namespace MKMods;

/// <summary>Loads audio clips from the plugin's Assets folder.</summary>
internal static class AudioLoader
{
    public static AudioClip Load(string fileName)
    {
        string path = Path.Combine(Plugin.AssetsPath, fileName);
        using var request = UnityWebRequestMultimedia.GetAudioClip($"file://{path}", AudioType.MPEG);
        request.SendWebRequest();
        while (!request.isDone)
        {
            // Busy-wait is acceptable here: only runs once during plugin startup
            // and the files are small local reads.
        }

        if (request.result != UnityWebRequest.Result.Success)
        {
            Plugin.Logger.LogError($"Failed to load audio clip '{path}': {request.error}");
            return null;
        }
        return DownloadHandlerAudioClip.GetContent(request);
    }
}

/// <summary>Plays clips on the game's interface audio source with a volume boost.</summary>
internal static class InterfaceAudio
{
    private static readonly AccessTools.FieldRef<SoundManager, AudioSource> InterfaceSource =
        AccessTools.FieldRefAccess<SoundManager, AudioSource>("interfaceSource");

    public static void PlayOneShot(AudioClip clip, float volume = 1f)
    {
        if (clip == null)
            return;
        InterfaceSource(SoundManager.i).PlayOneShot(clip, volume);
    }
}
