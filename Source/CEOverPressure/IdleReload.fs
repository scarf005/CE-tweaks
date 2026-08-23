namespace CEOverPressure

open CombatExtended
open HarmonyLib
open Verse
open Verse.AI

[<HarmonyPatch(typeof<JobGiver_CheckReload>, "GetPriority")>]
module internal IdleReloadPatch =
    let Postfix (pawn: Pawn) (__result: byref<float32>) =
        let settings = LoadedModManager.GetMod<CEOverPressureMod>().GetSettings<CEOverPressureSettings>()
        __result <- IdleReloadPriority.choose settings.EnableIdleWeaponReloading pawn.Drafted __result (float32 ThinkNodePriority.AvoidIdle)
