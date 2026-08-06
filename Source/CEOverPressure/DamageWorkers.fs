namespace CEOverPressure

open Verse

type DamageWorker_BlastPropagation() =
    inherit DamageWorker_AddInjury()

    override this.ApplySpecialEffectsToPart(pawn, totalDamage, dinfo, result) =
        let settings = LoadedModManager.GetMod<CEOverPressureMod>().GetSettings<CEOverPressureSettings>()

        if not settings.EnableBlastPropagation then
            base.ApplySpecialEffectsToPart(pawn, totalDamage, dinfo, result)
        else
            let mutable remainingDamage = totalDamage
            let mutable currentDamageInfo = dinfo
            let mutable applyDamage = true

            while applyDamage && not pawn.Dead do
                let damageDealt = base.FinalizeAndAddInjury(pawn, remainingDamage, currentDamageInfo, result)
                remainingDamage <- remainingDamage - damageDealt

                let hitPart = currentDamageInfo.HitPart

                if remainingDamage <= 1.0f || isNull hitPart || not (pawn.health.hediffSet.PartIsMissing(hitPart)) || isNull hitPart.parent then
                    applyDamage <- false
                else
                    currentDamageInfo.SetHitPart(hitPart.parent)
