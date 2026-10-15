namespace CEOverPressure

open System.Collections.Generic
open RimWorld
open Verse

/// Mod extension for projectile defs. Allows overriding the TNT-equivalent yield,
/// pressure multiplier, or disabling overpressure for a specific projectile.
type OverpressureExtension() as this =
    inherit DefModExtension()

    [<Microsoft.FSharp.Core.DefaultValue>]
    val mutable tntEquivalentKg: float32

    [<Microsoft.FSharp.Core.DefaultValue>]
    val mutable pressureMultiplier: float32

    [<Microsoft.FSharp.Core.DefaultValue>]
    val mutable disable: bool

    do
        this.tntEquivalentKg <- -1.0f
        this.pressureMultiplier <- 1.0f

/// Marks projectile defs whose CE explosive component is added by this mod.
type AutocannonExplosionExtension() =
    inherit DefModExtension()

/// Defines a pressure threshold and the maximum number of internal body parts
/// to damage when peak overpressure exceeds that threshold.
type InjuryBand() =
    [<Microsoft.FSharp.Core.DefaultValue>]
    val mutable minimumPressureKPa: float32

    [<Microsoft.FSharp.Core.DefaultValue>]
    val mutable maximumParts: int

/// Def that configures all overpressure model parameters, damage settings,
/// room-reflection multipliers, and injury-band thresholds.
type OverpressureSettingsDef() =
    inherit Def()

    [<Microsoft.FSharp.Core.DefaultValue>]
    val mutable metresPerCell: float32

    [<Microsoft.FSharp.Core.DefaultValue>]
    val mutable damagePerKgTnt: float32

    [<Microsoft.FSharp.Core.DefaultValue>]
    val mutable radiusMetresPerKgTnt: float32

    [<Microsoft.FSharp.Core.DefaultValue>]
    val mutable yieldExponent: float32

    [<Microsoft.FSharp.Core.DefaultValue>]
    val mutable damageYieldWeight: float32

    [<Microsoft.FSharp.Core.DefaultValue>]
    val mutable minimumYieldKg: float32

    [<Microsoft.FSharp.Core.DefaultValue>]
    val mutable maximumYieldKg: float32

    [<Microsoft.FSharp.Core.DefaultValue>]
    val mutable minimumPressureYieldKg: float32

    [<Microsoft.FSharp.Core.DefaultValue>]
    val mutable rangeCoefficientMetres: float32

    [<Microsoft.FSharp.Core.DefaultValue>]
    val mutable minimumScaledDistance: float32

    [<Microsoft.FSharp.Core.DefaultValue>]
    val mutable closeRangeLimit: float32

    [<Microsoft.FSharp.Core.DefaultValue>]
    val mutable closeCubicCoefficient: float32

    [<Microsoft.FSharp.Core.DefaultValue>]
    val mutable closeOffset: float32

    [<Microsoft.FSharp.Core.DefaultValue>]
    val mutable farLinearCoefficient: float32

    [<Microsoft.FSharp.Core.DefaultValue>]
    val mutable farQuadraticCoefficient: float32

    [<Microsoft.FSharp.Core.DefaultValue>]
    val mutable farCubicCoefficient: float32

    [<Microsoft.FSharp.Core.DefaultValue>]
    val mutable farOffset: float32

    [<Microsoft.FSharp.Core.DefaultValue>]
    val mutable pressureUnitKPa: float32

    [<Microsoft.FSharp.Core.DefaultValue>]
    val mutable scanRadiusPadding: float32

    [<Microsoft.FSharp.Core.DefaultValue>]
    val mutable minimumDistanceCells: float32

    [<Microsoft.FSharp.Core.DefaultValue>]
    val mutable groundReflectionMaximumHeight: float32

    [<Microsoft.FSharp.Core.DefaultValue>]
    val mutable groundReflectionMultiplier: float32

    [<Microsoft.FSharp.Core.DefaultValue>]
    val mutable maximumEnclosedReflectionMultiplier: float32

    [<Microsoft.FSharp.Core.DefaultValue>]
    val mutable enclosedReferenceRoomCellCount: float32

    [<Microsoft.FSharp.Core.DefaultValue>]
    val mutable thermobaricPressureMultiplier: float32

    [<Microsoft.FSharp.Core.DefaultValue>]
    val mutable stunThresholdKPa: float32

    [<Microsoft.FSharp.Core.DefaultValue>]
    val mutable injuryThresholdKPa: float32

    [<Microsoft.FSharp.Core.DefaultValue>]
    val mutable minimumStunTicks: float32

    [<Microsoft.FSharp.Core.DefaultValue>]
    val mutable maximumStunTicks: float32

    [<Microsoft.FSharp.Core.DefaultValue>]
    val mutable suppressionPerStunTick: float32

    [<Microsoft.FSharp.Core.DefaultValue>]
    val mutable damagePressureOffsetKPa: float32

    [<Microsoft.FSharp.Core.DefaultValue>]
    val mutable damagePressureDivisor: float32

    [<Microsoft.FSharp.Core.DefaultValue>]
    val mutable minimumDamagePerPart: float32

    [<Microsoft.FSharp.Core.DefaultValue>]
    val mutable maximumDamagePerPart: float32

    [<Microsoft.FSharp.Core.DefaultValue>]
    val mutable armorPenetrationReferencePressureKPa: float32

    [<Microsoft.FSharp.Core.DefaultValue>]
    val mutable injuryDamageDef: DamageDef | null

    [<Microsoft.FSharp.Core.DefaultValue>]
    val mutable injuryBands: List<InjuryBand | null> | null

    member internal this.ModelParameters =
        { MetresPerCell = this.metresPerCell
          DamagePerKgTnt = this.damagePerKgTnt
          RadiusMetresPerKgTnt = this.radiusMetresPerKgTnt
          YieldExponent = this.yieldExponent
          DamageYieldWeight = this.damageYieldWeight
          MinimumYieldKg = this.minimumYieldKg
          MaximumYieldKg = this.maximumYieldKg
          MinimumPressureYieldKg = this.minimumPressureYieldKg
          RangeCoefficientMetres = this.rangeCoefficientMetres
          MinimumScaledDistance = this.minimumScaledDistance
          CloseRangeLimit = this.closeRangeLimit
          CloseCubicCoefficient = this.closeCubicCoefficient
          CloseOffset = this.closeOffset
          FarLinearCoefficient = this.farLinearCoefficient
          FarQuadraticCoefficient = this.farQuadraticCoefficient
          FarCubicCoefficient = this.farCubicCoefficient
          FarOffset = this.farOffset
          PressureUnitKPa = this.pressureUnitKPa }
