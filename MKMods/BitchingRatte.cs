using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace MKMods;

/// <summary>Callout priority classes; lower value wins when the queue picks the next clip.</summary>
internal enum CalloutPriority
{
    Terrain = 0,
    Envelope = 1,
    Missile = 2,
    Combat = 3,
    Systems = 4,
    Fuel = 5,
    Advisory = 6,
}

/// <summary>
/// Serializes all voice output: one clip at a time, highest priority first,
/// per-callout cooldowns, stale entries dropped. Every warning producer in the
/// mod routes its audio through here so callouts never talk over each other.
/// </summary>
internal static class VoiceQueue
{
    private const float GapSeconds = 0.3f;
    private const float MaxAgeSeconds = 3f;
    private const float Volume = 3f;

    private struct Pending
    {
        public string Key;
        public AudioClip Clip;
        public CalloutPriority Priority;
        public float EnqueuedAt;
    }

    private static readonly List<Pending> Queue = new List<Pending>();
    private static readonly Dictionary<string, float> LastPlayed = new Dictionary<string, float>();
    private static float busyUntil;

    /// <summary>Request a callout. Ignored while its per-key cooldown is active.</summary>
    public static void Say(string key, AudioClip clip, CalloutPriority priority, float cooldownSeconds)
    {
        if (clip == null)
            return;
        if (LastPlayed.TryGetValue(key, out float last)
            && Time.unscaledTime - last < cooldownSeconds)
            return;
        for (int i = 0; i < Queue.Count; i++)
        {
            if (Queue[i].Key == key)
                return; // already pending
        }
        // Cooldown starts at enqueue so repeated Say() calls while queued don't stack.
        LastPlayed[key] = Time.unscaledTime;
        Queue.Add(new Pending
        {
            Key = key,
            Clip = clip,
            Priority = priority,
            EnqueuedAt = Time.unscaledTime,
        });
    }

    public static void Tick()
    {
        if (Queue.Count == 0 || Time.unscaledTime < busyUntil)
            return;

        Queue.RemoveAll(p => Time.unscaledTime - p.EnqueuedAt > MaxAgeSeconds);
        if (Queue.Count == 0)
            return;

        int best = 0;
        for (int i = 1; i < Queue.Count; i++)
        {
            if (Queue[i].Priority < Queue[best].Priority)
                best = i;
        }

        Pending next = Queue[best];
        Queue.RemoveAt(best);
        InterfaceAudio.PlayOneShot(next.Clip, Volume);
        LastPlayed[next.Key] = Time.unscaledTime;
        busyUntil = Time.unscaledTime + next.Clip.length + GapSeconds;
    }

    public static void Reset()
    {
        Queue.Clear();
        LastPlayed.Clear();
        busyUntil = 0f;
    }
}

/// <summary>
/// "Bitching Ratte" — the voice warning system. Polls the local aircraft's
/// flight envelope (terrain, attitude, stall, over-G) and hooks game events
/// (gear, RWR, countermeasures, damage) to produce prioritized voice callouts.
/// </summary>
internal static class BitchingRatte
{
    private const float PollIntervalSeconds = 0.2f;

    private static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();
    private static readonly string[] ClipNames =
    {
        "pull up", "altitude", "sink rate", "roll left", "roll right", "gear",
        "gear up", "gear down", "stall", "over g", "overspeed", "warning",
        "countermeasures low", "countermeasures out", "engine fire",
        "engine failure", "damage", "autopilot",
    };

    private static Aircraft aircraft;
    private static AircraftParameters parameters;
    private static TerrainWarningSystem terrainWarning;
    private static float nextPollTime;

    // Edge-trigger latches (armed again once the condition clears with hysteresis).
    private static bool altitudeLatched;
    private static bool sinkRateLatched;
    private static bool countermeasuresLowLatched;
    private static bool countermeasuresOutLatched;
    private static int lastCountermeasureAmmo = -1;
    private static int maxCountermeasureAmmo;

    public static void Initialize()
    {
        if (!Plugin.RatteEnabled.Value)
            return;
        foreach (string name in ClipNames)
        {
            AudioClip clip = AudioLoader.Load($"{name}.mp3");
            if (clip != null)
                Clips[name] = clip;
        }
    }

    public static bool Enabled => Plugin.RatteEnabled.Value && Clips.Count > 0;

    public static void Tick()
    {
        VoiceQueue.Tick();
        if (!Enabled || Time.unscaledTime < nextPollTime)
            return;
        nextPollTime = Time.unscaledTime + PollIntervalSeconds;
        PollEnvelope();
    }

    public static void AssignAircraft(Aircraft newAircraft)
    {
        if (aircraft != null)
        {
            aircraft.onSetGear -= OnSetGear;
            aircraft.onRadarWarning -= OnRadarWarning;
            aircraft.onSetFlightAssist -= OnFlightAssistToggle;
        }

        aircraft = newAircraft;
        parameters = null;
        terrainWarning = null;
        altitudeLatched = false;
        sinkRateLatched = false;
        countermeasuresLowLatched = false;
        countermeasuresOutLatched = false;
        lastCountermeasureAmmo = -1;
        maxCountermeasureAmmo = 0;
        VoiceQueue.Reset();
        MissileVoiceWarning.Reset();

        if (aircraft == null)
            return;
        parameters = aircraft.GetAircraftParameters();
        terrainWarning = new TerrainWarningSystem(aircraft);
        aircraft.onSetGear += OnSetGear;
        aircraft.onRadarWarning += OnRadarWarning;
        aircraft.onSetFlightAssist += OnFlightAssistToggle;
    }

    private static void Say(string clipName, CalloutPriority priority, float cooldownSeconds)
    {
        if (Clips.TryGetValue(clipName, out AudioClip clip))
            VoiceQueue.Say(clipName, clip, priority, cooldownSeconds);
    }

    // ---- Continuous envelope checks (level-triggered warnings repeat via cooldown,
    // ---- edge-triggered cautions latch until the condition clears). ----

    private static void PollEnvelope()
    {
        if (aircraft == null || aircraft.disabled || parameters == null)
            return;

        float altitude = aircraft.radarAlt;
        bool airborne = altitude > 5f;
        if (!airborne)
        {
            altitudeLatched = false;
            sinkRateLatched = false;
            return;
        }

        float verticalSpeed = aircraft.rb != null ? aircraft.rb.velocity.y : 0f;
        bool gearOut = aircraft.gearState == LandingGear.GearState.LockedExtended;
        // Gear out and slow: treat terrain proximity as an intentional landing.
        bool landingConfiguration = gearOut && aircraft.speed < parameters.approachSpeed * 1.2f;

        if (Plugin.RatteTerrainWarnings.Value)
        {
            PollTerrain(altitude, verticalSpeed, gearOut, landingConfiguration);
        }
        if (Plugin.RatteEnvelopeWarnings.Value)
        {
            PollFlightEnvelope();
        }
    }

    private static void PollTerrain(float altitude, float verticalSpeed, bool gearOut, bool landingConfiguration)
    {
        // Terrain closure — the game's own AI terrain avoidance does the math.
        terrainWarning.CheckTerrain();
        if (terrainWarning.urgency > Plugin.RattePullUpUrgency.Value && !landingConfiguration)
            Say("pull up", CalloutPriority.Terrain, 1.8f);

        // Inverted near the ground: call the shortest way back upright.
        if (altitude < 300f && aircraft.transform.up.y < -0.1f)
            Say(aircraft.transform.right.y > 0f ? "roll left" : "roll right", CalloutPriority.Terrain, 2.5f);

        // Altitude floor bust (edge + 25 % hysteresis).
        float floor = Plugin.RatteAltitudeFloor.Value;
        if (!gearOut && verticalSpeed < 0f && altitude < floor)
        {
            if (!altitudeLatched)
            {
                Say("altitude", CalloutPriority.Terrain, 0f);
                altitudeLatched = true;
            }
        }
        else if (altitude > floor * 1.25f)
        {
            altitudeLatched = false;
        }

        // Descent rate too high for the current altitude.
        if (altitude < 500f && verticalSpeed < -(altitude / 4f + 10f))
        {
            if (!sinkRateLatched)
            {
                Say("sink rate", CalloutPriority.Terrain, 0f);
                sinkRateLatched = true;
            }
        }
        else if (verticalSpeed > -(altitude / 8f + 5f))
        {
            sinkRateLatched = false;
        }

        // Descending, slow, low — and the gear is still up.
        if (!gearOut && altitude < 120f && verticalSpeed < -2f
            && aircraft.speed < parameters.approachSpeed * 1.5f)
            Say("gear", CalloutPriority.Terrain, 3f);
    }

    private static void PollFlightEnvelope()
    {
        if (aircraft.gForce > parameters.aircraftGLimit)
            Say("over g", CalloutPriority.Envelope, 2f);

        if (parameters.maxSpeed > 0f && aircraft.speed > parameters.maxSpeed)
            Say("overspeed", CalloutPriority.Envelope, 5f);

        // Angle of attack from velocity in aircraft space; per-aircraft stall
        // threshold comes from the game's own AoA buffet configuration.
        if (aircraft.rb != null)
        {
            Vector3 localVelocity = aircraft.transform.InverseTransformDirection(aircraft.rb.velocity);
            if (localVelocity.z > 10f)
            {
                float angleOfAttack = Mathf.Atan2(-localVelocity.y, localVelocity.z) * Mathf.Rad2Deg;
                float stallAngle = parameters.AoAEffects != null
                    ? parameters.AoAEffects.FullVolumeAlpha * 0.7f
                    : 30f;
                if (angleOfAttack > stallAngle)
                    Say("stall", CalloutPriority.Envelope, 2f);
            }
        }
    }

    // ---- Event-driven callouts. ----

    private static void OnSetGear(Aircraft.OnSetGear e)
    {
        if (!Enabled || !Plugin.RatteAdvisoryCallouts.Value)
            return;
        if (e.gearState == LandingGear.GearState.LockedExtended)
            Say("gear down", CalloutPriority.Advisory, 1f);
        else if (e.gearState == LandingGear.GearState.LockedRetracted)
            Say("gear up", CalloutPriority.Advisory, 1f);
    }

    private static void OnRadarWarning(Aircraft.OnRadarWarning e)
    {
        if (!Enabled || !Plugin.RatteCombatWarnings.Value)
            return;
        // A radar lock is background noise while a missile is already inbound.
        if (!MissileVoiceWarning.HasIncoming)
            Say("warning", CalloutPriority.Combat, 10f);
    }

    private static void OnFlightAssistToggle(Aircraft.OnFlightAssistToggle e)
    {
        if (!Enabled || !Plugin.RatteAdvisoryCallouts.Value)
            return;
        if (!e.enabled)
            Say("autopilot", CalloutPriority.Advisory, 5f);
    }

    public static void OnCountermeasureAmmo(int ammo)
    {
        if (!Enabled || !Plugin.RatteCombatWarnings.Value)
            return;

        if (ammo > lastCountermeasureAmmo)
        {
            // Rearm or first report: re-arm the latches, track full capacity.
            maxCountermeasureAmmo = Mathf.Max(maxCountermeasureAmmo, ammo);
            countermeasuresLowLatched = false;
            countermeasuresOutLatched = false;
        }
        lastCountermeasureAmmo = ammo;

        if (ammo == 0 && !countermeasuresOutLatched)
        {
            Say("countermeasures out", CalloutPriority.Combat, 0f);
            countermeasuresOutLatched = true;
        }
        else if (ammo > 0 && maxCountermeasureAmmo > 0
                 && ammo <= maxCountermeasureAmmo * 0.25f && !countermeasuresLowLatched)
        {
            Say("countermeasures low", CalloutPriority.Combat, 0f);
            countermeasuresLowLatched = true;
        }
    }

    public static bool IsLocalAircraft(Aircraft candidate)
    {
        return candidate != null && candidate == aircraft;
    }

    public static void OnFuelTankFire() => Say("engine fire", CalloutPriority.Systems, 30f);

    public static void OnFuelTankPunctured() => Say("damage", CalloutPriority.Systems, 10f);

    public static bool TrySayEngineFailure()
    {
        if (!Enabled || !Plugin.RatteSystemWarnings.Value)
            return false;
        Say("engine failure", CalloutPriority.Systems, 5f);
        return true;
    }
}

/// <summary>Tracks the local player's aircraft and feeds the countermeasure counter.</summary>
[HarmonyPatch(typeof(CombatHUD))]
internal static class RatteCombatHudPatches
{
    [HarmonyPostfix]
    [HarmonyPatch("SetAircraft")]
    private static void SetAircraft(Aircraft aircraft)
    {
        if (BitchingRatte.Enabled)
            BitchingRatte.AssignAircraft(aircraft);
    }

    [HarmonyPostfix]
    [HarmonyPatch("DisplayCountermeasureAmmo")]
    private static void DisplayCountermeasureAmmo(int ammo)
    {
        BitchingRatte.OnCountermeasureAmmo(ammo);
    }
}

/// <summary>Fuel tank battle damage: fire and leak callouts for the local aircraft.</summary>
[HarmonyPatch(typeof(FuelTank))]
internal static class RatteFuelTankPatches
{
    [HarmonyPostfix]
    [HarmonyPatch("UpdateStatus")]
    private static void UpdateStatus(bool onFire, Aircraft ___aircraft)
    {
        if (onFire && BitchingRatte.Enabled && Plugin.RatteSystemWarnings.Value
            && BitchingRatte.IsLocalAircraft(___aircraft))
            BitchingRatte.OnFuelTankFire();
    }

    [HarmonyPostfix]
    [HarmonyPatch("PunctureTank")]
    private static void PunctureTank(Aircraft ___aircraft)
    {
        if (BitchingRatte.Enabled && Plugin.RatteSystemWarnings.Value
            && BitchingRatte.IsLocalAircraft(___aircraft))
            BitchingRatte.OnFuelTankPunctured();
    }
}

/// <summary>
/// The game plays its own audio report when an engine dies. Replace it with
/// the Ratte voice so there is exactly one warning voice in the cockpit.
/// </summary>
[HarmonyPatch(typeof(StatusDisplay), "AddMessage")]
internal static class RatteStatusDisplayPatch
{
    private static bool Prefix()
    {
        // Returning false skips the native clip; Ratte announces instead.
        return !BitchingRatte.TrySayEngineFailure();
    }
}
