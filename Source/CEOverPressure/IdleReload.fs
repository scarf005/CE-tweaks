namespace CEOverPressure

open CombatExtended
open HarmonyLib
open Verse
open Verse.AI

[<HarmonyPatch(typeof<JobGiver_CheckReload>, "GetPriority")>]
module internal IdleReloadPatch =
    let Postfix (pawn: Pawn) (__result: byref<float32>) =
        __result <- IdleReloadPriority.choose (Settings.current ()).EnableIdleWeaponReloading pawn.Drafted __result (float32 ThinkNodePriority.AvoidIdle)
