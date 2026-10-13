# [scarf] Combat Extended Tweaks

Optional gameplay tweaks for [Combat Extended](https://github.com/CombatExtended-Continued/CombatExtended) on RimWorld 1.6 and 1.7

## Features

- blast overpressure adds wall-aware, enclosed-space pressure effects, stuns, and internal injuries
- realistic autocannon explosions add impact-triggered blast effects to selected AP-HE ammunition
- idle colonists automatically reload their weapons after higher-priority work

The explosion features are disabled by default and can be enabled independently in Mod Settings. Idle weapon reloading is enabled by default and can be disabled there.

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

`Source/` targets RimWorld 1.7 and references the local game from `RIMWORLD_DIR`; the 1.6 assembly is built from the frozen `rimworld-1.6` branch.
RimWorld 1.7 reference assemblies are not public yet, so the release workflow stops before building.

```sh
just test
just fmt
just build
just install
```
