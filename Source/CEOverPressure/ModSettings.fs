namespace CEOverPressure

open RimWorld
open UnityEngine
open Verse

type CEOverPressureSettings() =
    inherit ModSettings()

    let mutable enableOverpressure = false
    let mutable enableAutocannonExplosions = false
    let mutable enableBlastPropagation = true
    let mutable enableIdleWeaponReloading = true
    let mutable enableLogging = false

    member this.EnableOverpressure
        with get () = enableOverpressure
        and set v = enableOverpressure <- v

    member this.EnableAutocannonExplosions
        with get () = enableAutocannonExplosions
        and set v = enableAutocannonExplosions <- v

    member this.EnableBlastPropagation
        with get () = enableBlastPropagation
        and set v = enableBlastPropagation <- v

    member this.EnableIdleWeaponReloading
        with get () = enableIdleWeaponReloading
        and set v = enableIdleWeaponReloading <- v

    member this.EnableLogging
        with get () = enableLogging
        and set v = enableLogging <- v

    override this.ExposeData() =
        Scribe_Values.Look<bool>(&enableOverpressure, "enableOverpressure", false, false)
        Scribe_Values.Look<bool>(&enableAutocannonExplosions, "enableAutocannonExplosions", false, false)
        Scribe_Values.Look<bool>(&enableBlastPropagation, "enableBlastPropagation", true, false)
        Scribe_Values.Look<bool>(&enableIdleWeaponReloading, "enableIdleWeaponReloading", true, false)
        Scribe_Values.Look<bool>(&enableLogging, "enableLogging", false, false)


type CEOverPressureMod(contents: ModContentPack) as this =
    inherit Mod(contents)

    do LongEventHandler.ExecuteWhenFinished(fun () -> this.GetSettings<CEOverPressureSettings>() |> ignore)

    override this.SettingsCategory() = "CE_Tweaks_SettingsCategory".Translate().ToString()

    override this.DoSettingsWindowContents(canvas: Rect) =
        let listing = Listing_Standard()
        listing.Begin(canvas)

        let settings = this.GetSettings<CEOverPressureSettings>()
        let changed = ref false

        let toggle key (current: bool) (update: bool -> unit) =
            let mutable value = current
            listing.CheckboxLabeled($"CE_Tweaks_{key}_Label".Translate().ToString(), &value, $"CE_Tweaks_{key}_Tooltip".Translate().ToString())

            if value <> current then
                update value
                changed.Value <- true

        toggle "EnableOverpressure" settings.EnableOverpressure (fun value -> settings.EnableOverpressure <- value)
        toggle "EnableAutocannonExplosions" settings.EnableAutocannonExplosions (fun value -> settings.EnableAutocannonExplosions <- value)
        toggle "EnableBlastPropagation" settings.EnableBlastPropagation (fun value -> settings.EnableBlastPropagation <- value)
        toggle "EnableIdleWeaponReloading" settings.EnableIdleWeaponReloading (fun value -> settings.EnableIdleWeaponReloading <- value)
        toggle "EnableLogging" settings.EnableLogging (fun value -> settings.EnableLogging <- value)

        if changed.Value then
            settings.Write()

        listing.End()

module internal Settings =
    let current () = LoadedModManager.GetMod<CEOverPressureMod>().GetSettings<CEOverPressureSettings>()

    /// Logs only when enabled, so disabled logging never formats the message.
    let inline log ([<InlineIfLambda>] message: unit -> string) =
        if (current ()).EnableLogging then
            Log.Message(message ())
