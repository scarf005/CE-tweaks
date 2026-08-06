namespace CEOverPressure

open System.Collections.Generic
open System.Threading
open HarmonyLib
open Verse

module internal ExplosionDamagePropagation =
    let propagating = new ThreadLocal<bool>()

    let overpressureDamageDefs =
        lazy
            (HashSet<DamageDef>(
                DefDatabase<OverpressureSettingsDef>.AllDefsListForReading
                |> Seq.choose (fun settings -> if isNull settings.injuryDamageDef then None else Some settings.injuryDamageDef)
            ))

    let enabled () = LoadedModManager.GetMod<CEOverPressureMod>().GetSettings<CEOverPressureSettings>().EnableBlastPropagation

    let affects (damageDef: DamageDef) = damageDef.isExplosive || overpressureDamageDefs.Value.Contains(damageDef)

    let merge (target: DamageWorker.DamageResult) (source: DamageWorker.DamageResult) (pawn: Pawn) =
        target.wounded <- target.wounded || source.wounded
        target.headshot <- target.headshot || source.headshot
        target.deflected <- target.deflected || source.deflected
        target.stunned <- target.stunned || source.stunned
        target.deflectedByMetalArmor <- target.deflectedByMetalArmor || source.deflectedByMetalArmor
        target.diminished <- target.diminished || source.diminished
        target.diminishedByMetalArmor <- target.diminishedByMetalArmor || source.diminishedByMetalArmor
        target.totalDamageDealt <- target.totalDamageDealt + source.totalDamageDealt

        if not (isNull source.parts) then
            for part in source.parts do
                target.AddPart(pawn, part)

        if not (isNull source.hediffs) then
            for hediff in source.hediffs do
                target.AddHediff(hediff)

[<HarmonyPatch(typeof<DamageWorker_AddInjury>,
               "FinalizeAndAddInjury",
               [| typeof<Pawn>
                  typeof<float32>
                  typeof<DamageInfo>
                  typeof<DamageWorker.DamageResult> |])>]
module internal ExplosionDamagePropagationPatch =
    let Postfix
        (__instance: DamageWorker_AddInjury)
        (pawn: Pawn)
        (totalDamage: float32)
        (dinfo: DamageInfo)
        (result: DamageWorker.DamageResult)
        (__result: float32)
        =
        let hitPart = dinfo.HitPart

        if
            not ExplosionDamagePropagation.propagating.Value
            && ExplosionDamagePropagation.enabled ()
            && not (isNull dinfo.Def)
            && ExplosionDamagePropagation.affects dinfo.Def
            && not (__instance :? DamageWorker_Blunt)
            && not (isNull hitPart)
            && not (dinfo.Def.harmAllLayersUntilOutside && hitPart.depth = BodyPartDepth.Inside)
            && pawn.health.hediffSet.PartIsMissing(hitPart)
        then
            let mutable remainingDamage = totalDamage - __result
            let mutable currentPart = hitPart
            ExplosionDamagePropagation.propagating.Value <- true

            try
                while remainingDamage > 1.0f && not pawn.Dead && not (isNull currentPart.parent) do
                    let mutable propagatedDamage = DamageInfo(dinfo)
                    currentPart <- currentPart.parent
                    propagatedDamage.SetHitPart(currentPart)
                    propagatedDamage.SetAllowDamagePropagation(false)
                    propagatedDamage.SetIgnoreArmor(true)

                    let propagatedResult = pawn.TakeDamage(propagatedDamage)
                    ExplosionDamagePropagation.merge result propagatedResult pawn
                    remainingDamage <- remainingDamage - propagatedResult.totalDamageDealt
            finally
                ExplosionDamagePropagation.propagating.Value <- false
