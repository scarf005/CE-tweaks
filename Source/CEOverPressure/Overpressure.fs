namespace CEOverPressure

open System
open System.Collections.Generic
open CombatExtended
open HarmonyLib
open RimWorld
open UnityEngine
open Verse
open Utils

type BattleLogEntry_OverpressureImpact(initialInitiator: Thing, initialRecipient: Pawn, initialWeapon: ThingDef, damageDef: DamageDef) =
    inherit BattleLogEntry_ExplosionImpact(initialInitiator, initialRecipient, initialWeapon, initialWeapon, damageDef)

    let mutable initiator = initialInitiator
    let mutable recipient = initialRecipient
    let mutable weapon = initialWeapon

    new() = BattleLogEntry_OverpressureImpact(null, null, null, null)

    override this.ToGameStringFromPOV_Worker(pov, forceLog) =
        if isNull initiator || isNull recipient then base.ToGameStringFromPOV_Worker(pov, forceLog)
        elif isNull weapon then "CEOP_CombatLogImpact".Translate(initiator.Named("initiator"), recipient.Named("recipient")).Resolve()
        else "CEOP_CombatLogImpactWithWeapon".Translate(initiator.Named("initiator"), recipient.Named("recipient"), weapon.Named("weapon")).Resolve()

    override this.ExposeData() =
        base.ExposeData()
        Scribe_References.Look(&initiator, "ceopInitiator", true)
        Scribe_References.Look(&recipient, "ceopRecipient", true)
        Scribe_Defs.Look(&weapon, "ceopWeapon")

/// Applies blast overpressure effects to pawns near an explosion.
/// Computes peak pressure via the Friedlander model, scales it by room enclosure
/// and ground reflection, then applies stuns and internal injuries to affected pawns.
/// See https://en.wikipedia.org/wiki/Friedlander_equation
module internal Overpressure =
    let private settings = lazy DefDatabase<OverpressureSettingsDef>.AllDefsListForReading[0]

    let private extensionFor (projectile: ThingDef) =
        if isNull projectile then
            None
        else
            match projectile.GetModExtension<OverpressureExtension>() with
            | null -> None
            | extension -> Some extension

    /// Estimates TNT-equivalent yield, preferring an explicit extension value
    /// over the damage-and-radius-based model estimate.
    let private estimateYieldKg parameters damage radius (extension: OverpressureExtension option) =
        match extension with
        | Some value when value.tntEquivalentKg > 0.0f -> value.tntEquivalentKg
        | _ -> PressureModel.estimateYieldKg parameters damage radius

    let private isThermobaric (damageDef: DamageDef) =
        damageDef.isExplosive && not (isNull damageDef.workerClass) && typeof<DamageWorker_Flame>.IsAssignableFrom damageDef.workerClass

    /// Computes the enclosed-room pressure multiplier based on cell count and roof coverage.
    /// More enclosed rooms amplify overpressure through wave reflection.
    let private roomMultiplier (configuration: OverpressureSettingsDef) (room: Room) =
        if isNull room || not room.ProperRoom || room.UsesOutdoorTemperature || room.CellCount <= 0 then
            1.0f
        else
            let cellCount = float32 room.CellCount
            let roofFraction = 1.0f - float32 room.OpenRoofCount / cellCount
            let sizeFactor = Mathf.Sqrt(Mathf.Clamp01(configuration.enclosedReferenceRoomCellCount / cellCount))
            1.0f + (configuration.maximumEnclosedReflectionMultiplier - 1.0f) * roofFraction * sizeFactor

    let private modSettings () = LoadedModManager.GetMod<CEOverPressureMod>().GetSettings<CEOverPressureSettings>()

    let private overpressureEnabled () = (modSettings ()).EnableOverpressure

    let private log message =
        if (modSettings ()).EnableLogging then
            Log.Message message

    let private stunTicks (configuration: OverpressureSettingsDef) peakKPa =
        Mathf.RoundToInt(Mathf.Clamp(peakKPa, configuration.minimumStunTicks, configuration.maximumStunTicks))

    let private stun (pawn: Pawn) ticks (instigator: Thing) =
        if not pawn.Dead && not (isNull pawn.stances) && not (isNull pawn.stances.stunner) then
            pawn.stances.stunner.StunFor(ticks, instigator)

    let private blastWeightMap =
        lazy
            (dict
                [ BodyPartTagDefOf.BreathingSource, 4.0f
                  BodyPartTagDefOf.HearingSource, 3.0f
                  BodyPartTagDefOf.EatingSource, 2.5f
                  BodyPartTagDefOf.ConsciousnessSource, 2.0f
                  BodyPartTagDefOf.BloodPumpingSource, 1.5f ])

    let private blastWeight (part: BodyPartRecord) =
        let dict = blastWeightMap.Value

        part.def.tags |> Seq.choose (tryGet dict) |> tryMax |> Option.defaultValue 2.0f

    let private partsToDamage (configuration: OverpressureSettingsDef) peakKPa partCount =
        let mutable bestThreshold = Single.MinValue
        let mutable result = 0
        let bands = configuration.injuryBands

        for index = 0 to bands.Count - 1 do
            let band = bands[index]

            if not (isNull band) && peakKPa >= band.minimumPressureKPa && band.minimumPressureKPa > bestThreshold then
                bestThreshold <- band.minimumPressureKPa
                result <- if band.maximumParts < 0 then partCount else min band.maximumParts partCount

        result

    let private resolveInitiator (instigator: Thing) =
        match instigator with
        | :? CombatExtended.ProjectileCE as proj when not (isNull proj.launcher) -> proj.launcher
        | _ -> instigator

    let private suppress (configuration: OverpressureSettingsDef) (pawn: Pawn) (origin: IntVec3) (initiator: Thing) ticks =
        let sourceFaction = if isNull initiator then null else initiator.Faction
        let suppressable = pawn.TryGetComp<CompSuppressable>()

        if
            configuration.suppressionPerStunTick > 0.0f
            && not (isNull suppressable)
            && not (obj.ReferenceEquals(pawn.Faction, sourceFaction))
            && not (suppressable.IgnoreSuppresion(origin))
        then
            suppressable.AddSuppression(float32 ticks * configuration.suppressionPerStunTick, origin)

    let private associateCombatLog
        (battleLogEntry: BattleLogEntry_ExplosionImpact)
        (damageResult: DamageWorker.DamageResult)
        (pawn: Pawn)
        (part: BodyPartRecord)
        =
        damageResult.AssociateWithLog(battleLogEntry)

        if pawn.health.hediffSet.PartIsMissing(part) then
            for hediff in pawn.health.hediffSet.hediffs do
                match hediff with
                | :? Hediff_MissingPart as mp when mp.Part = part && isNull mp.combatLogEntry ->
                    mp.combatLogEntry <- WeakReference<LogEntry>(battleLogEntry)
                    mp.combatLogText <- battleLogEntry.ToGameStringFromPOV(null)
                | _ -> ()

    /// Applies overpressure injuries to a pawn: stuns at moderate pressure,
    /// damages randomly-selected internal body parts at high pressure.
    let private applyInjuries
        (configuration: OverpressureSettingsDef)
        (pawn: Pawn)
        peakKPa
        armorPenetration
        (origin: IntVec3)
        (instigator: Thing)
        (projectile: ThingDef)
        =
        if peakKPa >= configuration.stunThresholdKPa then
            let initiator = resolveInitiator instigator
            let ticks = stunTicks configuration peakKPa
            stun pawn ticks initiator
            suppress configuration pawn origin initiator ticks

            if peakKPa < configuration.injuryThresholdKPa then
                log (sprintf "[CE-Tweaks] stunned %O at %.1f kPa" pawn peakKPa)
            else
                let weightedParts = SimplePool<List<BodyPartRecord>>.Get()

                try
                    let mutable uniqueCount = 0

                    for part in pawn.health.hediffSet.GetNotMissingParts() do
                        if part.depth = BodyPartDepth.Inside then
                            uniqueCount <- uniqueCount + 1
                            let w = Mathf.RoundToInt(blastWeight part)

                            for _ = 1 to w do
                                weightedParts.Add part

                    let damagePerPart =
                        Mathf.Clamp(
                            (peakKPa - configuration.damagePressureOffsetKPa) / configuration.damagePressureDivisor,
                            configuration.minimumDamagePerPart,
                            configuration.maximumDamagePerPart
                        )

                    let count = partsToDamage configuration peakKPa uniqueCount

                    log (
                        sprintf
                            "[CE-Tweaks] injured %O at %.1f kPa, %d/%d parts, %.1f dmg/part, ap=%.2f"
                            pawn
                            peakKPa
                            count
                            uniqueCount
                            damagePerPart
                            armorPenetration
                    )

                    let battleLogEntry = BattleLogEntry_OverpressureImpact(initiator, pawn, projectile, configuration.injuryDamageDef)

                    Find.BattleLog.Add(battleLogEntry)

                    let mutable index = 0

                    while index < count && not pawn.Dead do
                        let selectedIndex = Rand.Range(index, weightedParts.Count)
                        let part = weightedParts[selectedIndex]
                        weightedParts[selectedIndex] <- weightedParts[index]
                        weightedParts[index] <- part

                        let damageResult =
                            pawn.TakeDamage(DamageInfo(configuration.injuryDamageDef, damagePerPart, armorPenetration, -1.0f, initiator, part, projectile))

                        associateCombatLog battleLogEntry damageResult pawn part

                        index <- index + 1
                finally
                    weightedParts.Clear()
                    SimplePool<List<BodyPartRecord>>.Return weightedParts

    let private applyToPawn
        (configuration: OverpressureSettingsDef)
        pressureModel
        center
        (map: Map)
        scanRadius
        height
        pressureMultiplier
        (blastRoom: Room)
        enclosedMultiplier
        (instigator: Thing)
        armorPenetration
        (pawn: Pawn)
        (projectile: ThingDef)
        =
        if not pawn.Dead && pawn.Spawned && pawn.Position.InHorDistOf(center, scanRadius) && GenSight.LineOfSight(center, pawn.Position, map, true) then
            let distanceCells = max configuration.minimumDistanceCells (pawn.Position.DistanceTo center)
            let mutable peakKPa = PressureModel.peakPressureKPa pressureModel (distanceCells * configuration.metresPerCell)

            if height < configuration.groundReflectionMaximumHeight then
                peakKPa <- peakKPa * configuration.groundReflectionMultiplier

            if enclosedMultiplier > 1.0f && obj.ReferenceEquals(blastRoom, pawn.GetRoom()) then
                peakKPa <- peakKPa * enclosedMultiplier

            let scaledAP = armorPenetration * Mathf.Clamp01(peakKPa / configuration.armorPenetrationReferencePressureKPa)

            applyInjuries configuration pawn (peakKPa * pressureMultiplier) scaledAP center instigator projectile

    /// Main entry point for blast overpressure effects.
    /// Traverses the map region graph from the explosion center, collects pawns,
    /// and applies pressure-based stuns and internal injuries to each affected pawn.
    let apply
        (center: IntVec3)
        (map: Map)
        (radius: float32)
        (damageDef: DamageDef)
        (instigator: Thing)
        (damage: int)
        (armorPenetration: float32)
        (projectile: ThingDef)
        (height: float32)
        =
        if overpressureEnabled () && not (isNull map) && center.InBounds map && not (isNull damageDef) then
            let configuration = settings.Value
            let extension = extensionFor projectile

            if not (extension |> Option.exists (fun value -> value.disable)) then
                let hasExplicitYield = extension |> Option.exists (fun value -> value.tntEquivalentKg > 0.0f)

                if hasExplicitYield || (damage > 0 && damageDef.isExplosive) then
                    let parameters = configuration.ModelParameters
                    let yieldKg = estimateYieldKg parameters damage radius extension

                    if yieldKg > 0.0f then
                        let extensionMultiplier =
                            match extension with
                            | Some value when value.pressureMultiplier > 0.0f -> value.pressureMultiplier
                            | _ -> 1.0f

                        let pressureMultiplier = extensionMultiplier * (if isThermobaric damageDef then configuration.thermobaricPressureMultiplier else 1.0f)
                        let pressureModel = PressureModel.prepare parameters yieldKg
                        let scanRadius = max (radius + configuration.scanRadiusPadding) (PressureModel.pressureRangeCells pressureModel)
                        let blastRoom = RegionAndRoomQuery.RoomAt(center, map, RegionType.Set_Passable)
                        let enclosedMultiplier = roomMultiplier configuration blastRoom

                        log (
                            sprintf
                                "[CE-Tweaks] %O caused %.3fkg TNT blast at %O, pressureMult=%.2f, enclosedMult=%.2f, scanRadius=%.1f"
                                instigator
                                yieldKg
                                center
                                pressureMultiplier
                                enclosedMultiplier
                                scanRadius
                        )

                        let maximumRegions = Mathf.CeilToInt(Mathf.PI * scanRadius * scanRadius) + 1

                        let processedPawns = SimplePool<HashSet<Pawn>>.Get()

                        try
                            RegionTraverser.BreadthFirstTraverse(
                                center,
                                map,
                                (fun _ region ->
                                    (isNull region.door || region.door.Open) && center.InHorDistOf(region.extentsClose.ClosestCellTo center, scanRadius)),
                                (fun region ->
                                    let pawns = region.ListerThings.ThingsInGroup(ThingRequestGroup.Pawn)

                                    for index = pawns.Count - 1 downto 0 do
                                        match pawns[index] with
                                        | :? Pawn as pawn -> processedPawns.Add pawn |> ignore
                                        | _ -> ()

                                    false),
                                maximumRegions
                            )

                            for pawn in processedPawns do
                                applyToPawn
                                    configuration
                                    pressureModel
                                    center
                                    map
                                    scanRadius
                                    height
                                    pressureMultiplier
                                    blastRoom
                                    enclosedMultiplier
                                    instigator
                                    armorPenetration
                                    pawn
                                    projectile
                        finally
                            processedPawns.Clear()
                            SimplePool<HashSet<Pawn>>.Return processedPawns

[<HarmonyPatch(typeof<Explosion>, "ExplosionEnded")>]
module internal ExplosionPatch =
    let Postfix (__instance: Explosion) =
        match __instance with
        | :? ExplosionCE as explosion ->
            Overpressure.apply
                explosion.Position
                explosion.Map
                explosion.radius
                explosion.damType
                explosion.instigator
                explosion.damAmount
                explosion.armorPenetration
                explosion.projectile
                explosion.height
        | _ -> ()

[<HarmonyPatch(typeof<CompExplosiveCE>, "Explode")>]
module internal AutocannonExplosionPatch =
    let Prefix (__instance: CompExplosiveCE) =
        let parent = __instance.parent
        let settings = LoadedModManager.GetMod<CEOverPressureMod>().GetSettings<CEOverPressureSettings>()

        settings.EnableAutocannonExplosions || isNull parent || isNull parent.def || isNull (parent.def.GetModExtension<AutocannonExplosionExtension>())

[<StaticConstructorOnStartup>]
type CEOverPressureBootstrap() =
    static do Harmony("scarf.CombatExtended.tweaks").PatchAll()
