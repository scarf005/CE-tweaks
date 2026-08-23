#load "../Source/CEOverPressure/PressureModel.fs"
#load "../Source/CEOverPressure/IdleReloadPriority.fs"

open System
open System.Globalization
open System.Xml.Linq
open CEOverPressure

let private settings =
    XDocument.Load("Defs/OverpressureSettings.xml").Root.Elements()
    |> Seq.exactlyOne

let private readFloat name =
    settings.Element(XName.Get name).Value
    |> fun value -> Single.Parse(value, CultureInfo.InvariantCulture)

let private parameters =
    { MetresPerCell = readFloat "metresPerCell"
      DamagePerKgTnt = readFloat "damagePerKgTnt"
      RadiusMetresPerKgTnt = readFloat "radiusMetresPerKgTnt"
      YieldExponent = readFloat "yieldExponent"
      DamageYieldWeight = readFloat "damageYieldWeight"
      MinimumYieldKg = readFloat "minimumYieldKg"
      MaximumYieldKg = readFloat "maximumYieldKg"
      MinimumPressureYieldKg = readFloat "minimumPressureYieldKg"
      RangeCoefficientMetres = readFloat "rangeCoefficientMetres"
      MinimumScaledDistance = readFloat "minimumScaledDistance"
      CloseRangeLimit = readFloat "closeRangeLimit"
      CloseCubicCoefficient = readFloat "closeCubicCoefficient"
      CloseOffset = readFloat "closeOffset"
      FarLinearCoefficient = readFloat "farLinearCoefficient"
      FarQuadraticCoefficient = readFloat "farQuadraticCoefficient"
      FarCubicCoefficient = readFloat "farCubicCoefficient"
      FarOffset = readFloat "farOffset"
      PressureUnitKPa = readFloat "pressureUnitKPa" }

let assertNear expected actual tolerance message =
    if abs (expected - actual) > tolerance then
        failwithf "%s: expected %f, got %f" message expected actual

assertNear 0.324f (PressureModel.estimateYieldKg parameters 100 2.5f) 0.001f "reference yield at 100 dmg, 2.5 rad"
assertNear 0.472f (PressureModel.estimateYieldKg parameters 100 5.0f) 0.001f "radius contribution at 5.0 rad"
assertNear 0.0f (PressureModel.estimateYieldKg parameters 0 2.5f) 0.0f "zero damage yield"
assertNear 65.2937f (PressureModel.peakPressureKPa parameters 4.0f 2.2f) 0.01f "pressure at four metres"

let assertEqual expected actual message =
    if expected <> actual then
        failwithf "%s: expected %A, got %A" message expected actual

assertEqual 2.0f (IdleReloadPriority.choose true false 9.1f 2.0f) "idle reload uses idle priority"
assertEqual 0.0f (IdleReloadPriority.choose true true 9.1f 2.0f) "drafted pawns do not reload while idle"
assertEqual 0.0f (IdleReloadPriority.choose false false 9.1f 2.0f) "disabled idle reloading issues no reload job"
assertEqual 0.0f (IdleReloadPriority.choose true false 0.0f 2.0f) "ineligible weapons do not receive a reload priority"
