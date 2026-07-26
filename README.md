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

### Fuel time readout

Displays the estimated remaining fuel time next to the fuel gauge, calculated
from the fuel burned between samples so it follows your actual throttle setting.

The matching "fuel low" and "bingo fuel" voice callouts live in
[Bitching Ratte](https://github.com/struckl/BitchingRatte).

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
to modify these — press ``F1`` in game to open it.

## How to build

Requires the .NET SDK. Point ``GameDirectory`` at your Nuclear Option install
(the default is set in ``MKMods/MKMods.csproj``):

```
dotnet build -c Release -p:GameDirectory="C:\path\to\Nuclear Option"
```

A successful build automatically copies the plugin into
``<GameDirectory>/BepInEx/plugins/MKMods``.

## Related plugins

The features that grew their own personality now live in their own plugins.
Each installs on its own, and they stay out of each other's way.

- [Bitching Ratte](https://github.com/struckl/BitchingRatte) — the cockpit voice
  warning system.
- [Ratten Chatter](https://github.com/struckl/RattenChatter) — ambient radio
  chatter.
- [Cleared Ratte](https://github.com/struckl/ClearedRatte) — approach assist.

## Version 5.0 notes

Approach assist moved out into its own standalone plugin,
[Cleared Ratte](https://github.com/struckl/ClearedRatte).

With it went the last thing MKMods did every frame, so the plugin is now purely
Harmony patches. What is left is the original trio: target selection, the climb
rate nudge and the fuel time readout.

## Version 4.0 notes

The whole voice warning system moved out into its own standalone plugin,
[Bitching Ratte](https://github.com/struckl/BitchingRatte) — terrain, flight
envelope, combat, systems and advisory callouts, plus the missile countermeasure
calls and the "fuel low" / "bingo fuel" warnings that shared its priority queue.
MKMods keeps the fuel *time* readout in the HUD; only the spoken part left.

MKMods now ships no audio assets at all, and the "Warnings" and "Callouts (…)"
config sections are gone. Install Bitching Ratte alongside it to get the voices
back.

## Version 3.0 notes

The ambient radio chatter moved out into its own standalone plugin,
[Ratten Chatter](https://github.com/struckl/RattenChatter), so it can be
installed without the rest of MKMods. Chatter still keeps out of the way of
the Bitching Ratte callouts when both plugins are installed.

## Version 2.0 notes

The mod was updated for the current game version (mid-2026). The dynamic loadout
selection and the HUD notch line features were removed: recent game updates
reworked the loadout screen and added a built-in notch indicator, which made
both features obsolete.
