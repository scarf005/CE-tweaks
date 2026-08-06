# [scarf] Combat Extended Tweaks

Optional gameplay tweaks for [Combat Extended](https://github.com/CombatExtended-Continued/CombatExtended) on RimWorld 1.6

## Features

- blast overpressure adds wall-aware, enclosed-space pressure effects, stuns, and internal injuries
- realistic autocannon explosions add impact-triggered blast effects to selected AP-HE ammunition

Both features are disabled by default and can be enabled independently in Mod Settings

## Configuration

`Defs/OverpressureSettings.xml` controls overpressure yield weighting, pressure behavior, reflection, injury thresholds, and injury bands

Other mods can override yield, pressure, or disable overpressure on a projectile `ThingDef`

```xml
<modExtensions>
  <li Class="CEOverPressure.OverpressureExtension">
    <tntEquivalentKg>2.2</tntEquivalentKg>
    <pressureMultiplier>1.0</pressureMultiplier>
    <disable>false</disable>
  </li>
</modExtensions>
```

A positive explicit yield also enables overpressure for projectile definitions that would otherwise be filtered by damage type

Generated autocannon ammunition data: [docs/AutocannonExplosions.md](docs/AutocannonExplosions.md)

## Build

Set `COMBAT_EXTENDED_DIR` if Combat Extended is installed outside the default Steam Workshop location

```sh
just test
just fmt
just build
just install
```
