#load "../Source/CEOverPressure/PressureModel.fs"

open System
open System.Diagnostics
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

module private Baseline =
    let private power value exponent =
        Math.Pow(float value, float exponent) |> float32

    let private cubeRoot value = power value (1.0f / 3.0f)

    let peakPressureKPa parameters distanceMetres tntEquivalentKg =
        let cubeRootYield = cubeRoot (max tntEquivalentKg parameters.MinimumPressureYieldKg)

        let scaledDistance =
            max parameters.MinimumScaledDistance (distanceMetres / cubeRootYield)

        let z2 = scaledDistance * scaledDistance
        let z3 = z2 * scaledDistance

        let pressure =
            if scaledDistance < parameters.CloseRangeLimit then
                parameters.CloseCubicCoefficient / z3 + parameters.CloseOffset
            else
                parameters.FarLinearCoefficient / scaledDistance
                + parameters.FarQuadraticCoefficient / z2
                + parameters.FarCubicCoefficient / z3
                + parameters.FarOffset

        max 0.0f (pressure * parameters.PressureUnitKPa)

let private distances =
    Array.init 4096 (fun index -> 0.4f + float32 (index % 128) * 0.125f)

let private iterations = 2048
let private sampleCount = 15
let private evaluationCount = int64 iterations * int64 distances.Length

type private Sample =
    { ElapsedMilliseconds: float
      AllocatedBytes: int64
      Checksum: float }

type private Result =
    { Name: string
      MedianMilliseconds: float
      EvaluationsPerSecond: float
      AllocatedBytes: int64
      Checksum: float }

let private run evaluator =
    let stopwatch = Stopwatch()
    GC.Collect()
    GC.WaitForPendingFinalizers()
    GC.Collect()
    let allocatedBefore = GC.GetAllocatedBytesForCurrentThread()
    let mutable checksum = 0.0

    stopwatch.Start()

    for _ = 1 to iterations do
        for distance in distances do
            checksum <- checksum + float (evaluator distance)

    stopwatch.Stop()

    { ElapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds
      AllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore
      Checksum = checksum }

let private summarize name samples =
    let elapsed =
        samples |> Array.map (fun sample -> sample.ElapsedMilliseconds) |> Array.sort

    let allocated =
        samples |> Array.map (fun sample -> sample.AllocatedBytes) |> Array.sort

    let medianMilliseconds = elapsed[elapsed.Length / 2]

    { Name = name
      MedianMilliseconds = medianMilliseconds
      EvaluationsPerSecond = float evaluationCount / (medianMilliseconds / 1000.0)
      AllocatedBytes = allocated[allocated.Length / 2]
      Checksum = samples[0].Checksum }

let private yieldKg = 2.2f
let private preparedPressure = PressureModel.prepare parameters yieldKg

let private baselineEvaluation distance =
    Baseline.peakPressureKPa parameters distance yieldKg

let private optimizedEvaluation = PressureModel.peakPressureKPa preparedPressure

run baselineEvaluation |> ignore
run optimizedEvaluation |> ignore

let private samples =
    Array.init sampleCount (fun index ->
        if index % 2 = 0 then
            run baselineEvaluation, run optimizedEvaluation
        else
            let optimizedSample = run optimizedEvaluation
            let baselineSample = run baselineEvaluation
            baselineSample, optimizedSample)

let private baseline = samples |> Array.map fst |> summarize "Per-pawn cube root"
let private optimized = samples |> Array.map snd |> summarize "Prepared blast"

if abs (baseline.Checksum - optimized.Checksum) > 0.001 then
    failwithf
        "Benchmark implementations produced different checksums: %.6f and %.6f"
        baseline.Checksum
        optimized.Checksum

let private timeReduction =
    (baseline.MedianMilliseconds - optimized.MedianMilliseconds)
    / baseline.MedianMilliseconds
    * 100.0

let private speedup = baseline.MedianMilliseconds / optimized.MedianMilliseconds

printfn "Pressure evaluation benchmark (%d evaluations, median of %d samples)" evaluationCount sampleCount
printfn "| Path | Median (ms) | Evaluations/s | Allocated bytes |"
printfn "| --- | ---: | ---: | ---: |"

printfn
    "| %s | %.3f | %.0f | %d |"
    baseline.Name
    baseline.MedianMilliseconds
    baseline.EvaluationsPerSecond
    baseline.AllocatedBytes

printfn
    "| %s | %.3f | %.0f | %d |"
    optimized.Name
    optimized.MedianMilliseconds
    optimized.EvaluationsPerSecond
    optimized.AllocatedBytes

printfn "Prepared blast: %.1f%% less time, %.2fx throughput" timeReduction speedup
printfn "Checksum: %.6f" optimized.Checksum
