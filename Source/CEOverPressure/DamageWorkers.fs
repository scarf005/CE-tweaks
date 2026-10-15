namespace CEOverPressure

open System.Collections.Generic
open System.Threading
open HarmonyLib
open Verse

module internal ExplosionDamagePropagation =
    let propagating = new ThreadLocal<bool>()

    let overpressureDamageDefs =
        lazy (HashSet<DamageDef>(DefDatabase<OverpressureSettingsDef>.AllDefsListForReading |> Seq.choose (_.injuryDamageDef >> Option.ofObj)))

    let enabled () = (Settings.current ()).EnableBlastPropagation

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

        match source.parts with
        | null -> ()
        | parts ->
            for part in parts do
                target.AddPart(pawn, part)

        match source.hediffs with
        | null -> ()
        | hediffs ->
            for hediff in hediffs do
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
        match dinfo.Def, dinfo.HitPart with
        | NonNull damageDef, NonNull hitPart when
            not ExplosionDamagePropagation.propagating.Value
            && ExplosionDamagePropagation.enabled ()
            && ExplosionDamagePropagation.affects damageDef
            && not (__instance :? DamageWorker_Blunt)
            && not (damageDef.harmAllLayersUntilOutside && hitPart.depth = BodyPartDepth.Inside)
            && pawn.health.hediffSet.PartIsMissing(hitPart)
            ->
            let mutable remainingDamage = totalDamage - __result
            let mutable currentPart = hitPart
            ExplosionDamagePropagation.propagating.Value <- true

            try
                while remainingDamage > 1.0f && not pawn.Dead && currentPart.parent <> null do
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
        | _ -> ()
