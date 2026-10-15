namespace CEOverPressure

open System

/// Parameters for the Friedlander-equation pressure model used to compute
/// peak overpressure at a given distance from an explosion.
/// See https://en.wikipedia.org/wiki/Friedlander_equation
[<Struct>]
type internal PressureModelParameters =
    { MetresPerCell: float32
      DamagePerKgTnt: float32
      RadiusMetresPerKgTnt: float32
      YieldExponent: float32
      DamageYieldWeight: float32
      MinimumYieldKg: float32
      MaximumYieldKg: float32
      MinimumPressureYieldKg: float32
      RangeCoefficientMetres: float32
      MinimumScaledDistance: float32
      CloseRangeLimit: float32
      CloseCubicCoefficient: float32
      CloseOffset: float32
      FarLinearCoefficient: float32
      FarQuadraticCoefficient: float32
      FarCubicCoefficient: float32
      FarOffset: float32
      PressureUnitKPa: float32 }

type internal PreparedPressureModel =
    private
        { PressureRangeCells: float32
          PressureCubeRootYield: float32
          MinimumScaledDistance: float32
          CloseRangeLimit: float32
          CloseCubicCoefficient: float32
          CloseOffset: float32
          FarLinearCoefficient: float32
          FarQuadraticCoefficient: float32
          FarCubicCoefficient: float32
          FarOffset: float32
          PressureUnitKPa: float32 }

module internal PressureModel =
    let private power value exponent = Math.Pow(float value, float exponent) |> float32
    let private cubeRoot value = power value (1.0f / 3.0f)

    let prepare parameters tntEquivalentKg =
        let rangeCubeRootYield = cubeRoot tntEquivalentKg

        { PressureRangeCells = parameters.RangeCoefficientMetres * rangeCubeRootYield / parameters.MetresPerCell
          PressureCubeRootYield =
            if tntEquivalentKg >= parameters.MinimumPressureYieldKg then rangeCubeRootYield else cubeRoot parameters.MinimumPressureYieldKg
          MinimumScaledDistance = parameters.MinimumScaledDistance
          CloseRangeLimit = parameters.CloseRangeLimit
          CloseCubicCoefficient = parameters.CloseCubicCoefficient
          CloseOffset = parameters.CloseOffset
          FarLinearCoefficient = parameters.FarLinearCoefficient
          FarQuadraticCoefficient = parameters.FarQuadraticCoefficient
          FarCubicCoefficient = parameters.FarCubicCoefficient
          FarOffset = parameters.FarOffset
          PressureUnitKPa = parameters.PressureUnitKPa }

    /// Estimates the TNT-equivalent yield (kg) from the observed damage and blast radius.
    /// Uses a weighted geometric mean of damage-derived and radius-derived yields,
    /// clamped to the configured minimum and maximum yield bounds.
    let estimateYieldKg parameters damage radiusCells =
        if damage <= 0 || radiusCells <= 0.0f then
            0.0f
        else
            let damageYield = power (float32 damage / parameters.DamagePerKgTnt) parameters.YieldExponent
            let radiusMetres = radiusCells * parameters.MetresPerCell
            let radiusYield = power (radiusMetres / parameters.RadiusMetresPerKgTnt) 3.0f
            let damageWeight = parameters.DamageYieldWeight |> max 0.0f |> min 1.0f

            power damageYield damageWeight * power radiusYield (1.0f - damageWeight) |> max parameters.MinimumYieldKg |> min parameters.MaximumYieldKg

    let pressureRangeCells prepared = prepared.PressureRangeCells

    /// Computes peak overpressure (kPa) at a given distance from an explosion
    /// using blast values prepared once for all affected pawns.
    /// See https://en.wikipedia.org/wiki/Friedlander_equation
    let peakPressureKPa prepared distanceMetres =
        let scaledDistance = max prepared.MinimumScaledDistance (distanceMetres / prepared.PressureCubeRootYield)
        let z2 = scaledDistance * scaledDistance
        let z3 = z2 * scaledDistance

        let pressure =
            if scaledDistance < prepared.CloseRangeLimit then
                prepared.CloseCubicCoefficient / z3 + prepared.CloseOffset
            else
                prepared.FarLinearCoefficient / scaledDistance + prepared.FarQuadraticCoefficient / z2 + prepared.FarCubicCoefficient / z3 + prepared.FarOffset

        max 0.0f (pressure * prepared.PressureUnitKPa)
