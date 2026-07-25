# MKMods

Implements various client-side improvements for the game
[Nuclear Option](https://store.steampowered.com/app/2168680/Nuclear_Option/).

## Features

### Improved target selection algorithm

Tapping the targeting key will unselect all existing targets and switch to the
next highest priority target. Holding the targeting key will add the next highest
priority target to the selected targets, without removing existing targets.

The targeting radius has also been increased from 100 to 200 canvas units.

In simpler terms, this system prioritizes quickly selecting new individual targets
in a logical way. It's similar to the targeting system of
[Arma 3](https://store.steampowered.com/agecheck/app/107410).

### HUD tweaks

Moves the climb rate readout (the "+10 m / -5 m" text) up from its awkwardly
low default position. Offset is configurable.

### Bitching Ratte — voice warning system

A complete cockpit voice warning system in the style of the F/A-18's
"Bitching Betty". One voice, one priority queue — callouts never talk over
each other. Covers:

- **Terrain**: "Pull up" (uses the game's own terrain prediction), "Altitude",
  "Sink rate", "Roll left/right" when inverted near the ground, and "Gear"
  when descending low and slow with the gear still up.
- **Flight envelope**: "Stall" (per-aircraft AoA threshold), "Over G"
  (per-aircraft G limit), "Overspeed".
- **Combat**: "Warning" on a hostile radar lock, "Countermeasures low/out".
- **Systems**: "Engine failure" (replaces the native engine failure audio),
  "Engine fire", "Damage" on fuel tank hits.
- **Advisories**: "Gear up/down", "Autopilot" when flight assist is disabled.

Missile and fuel warnings (below) are routed through the same queue.
Every group can be toggled individually in the config.

### Audible missile warnings

Plays a warning sound when a missile is locked onto the player's plane. The specific
sound played corresponds to the countermeasure to the type of missile.

- IR: "Flare"
- ARH, SARH: "Notch"
- ARAD: "Radar"
- Optical: "Hide"

### Fuel time and low fuel warning

Displays the remaining fuel time in the HUD, plays a "low fuel" warning sound
when the fuel is below 7 minutes, and a "bingo fuel" warning sound when the fuel
is below 3 minutes.

![Fuel time example](https://github.com/mkualquiera/MKModsNO/blob/main/images/fueltime.png?raw=true)

## How to install

- Install [BepInEx](https://docs.bepinex.dev/articles/user_guide/installation/index.html#where-to-download-bepinex).
- Download the latest release from the [releases page](https://github.com/mkualquiera/MKModsNO/releases).
- Unzip the download into the BepInEx ``plugins`` folder (located in
``Nuclear Option/BepInEx/plugins``).
- Run the game.

### Configs

This mod supports various config settings like disabling each feature and finetuning
some values. Use the [BepInEx configuration manager](https://github.com/BepInEx/BepInEx.ConfigurationManager)
to modify these.

## How to build

Requires the .NET SDK. Point ``GameDirectory`` at your Nuclear Option install
(the default is set in ``MKMods/MKMods.csproj``):

```
dotnet build -c Release -p:GameDirectory="C:\path\to\Nuclear Option"
```

A successful build automatically copies the plugin into
``<GameDirectory>/BepInEx/plugins/MKMods``.

## Version 2.0 notes

The mod was updated for the current game version (mid-2026). The dynamic loadout
selection and the HUD notch line features were removed: recent game updates
reworked the loadout screen and added a built-in notch indicator, which made
both features obsolete.
