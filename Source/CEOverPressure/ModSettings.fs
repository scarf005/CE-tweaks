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
        let mutable overpressure = settings.EnableOverpressure
        let mutable autocannonExplosions = settings.EnableAutocannonExplosions
        let mutable blastPropagation = settings.EnableBlastPropagation
        let mutable idleWeaponReloading = settings.EnableIdleWeaponReloading
        let mutable logging = settings.EnableLogging

        listing.CheckboxLabeled(
            "CE_Tweaks_EnableOverpressure_Label".Translate().ToString(),
            &overpressure,
            "CE_Tweaks_EnableOverpressure_Tooltip".Translate().ToString()
        )

        listing.CheckboxLabeled(
            "CE_Tweaks_EnableAutocannonExplosions_Label".Translate().ToString(),
            &autocannonExplosions,
            "CE_Tweaks_EnableAutocannonExplosions_Tooltip".Translate().ToString()
        )

        listing.CheckboxLabeled(
            "CE_Tweaks_EnableBlastPropagation_Label".Translate().ToString(),
            &blastPropagation,
            "CE_Tweaks_EnableBlastPropagation_Tooltip".Translate().ToString()
        )

        listing.CheckboxLabeled(
            "CE_Tweaks_EnableIdleWeaponReloading_Label".Translate().ToString(),
            &idleWeaponReloading,
            "CE_Tweaks_EnableIdleWeaponReloading_Tooltip".Translate().ToString()
        )

        listing.CheckboxLabeled("CE_Tweaks_EnableLogging_Label".Translate().ToString(), &logging, "CE_Tweaks_EnableLogging_Tooltip".Translate().ToString())

        if
            overpressure <> settings.EnableOverpressure
            || autocannonExplosions <> settings.EnableAutocannonExplosions
            || blastPropagation <> settings.EnableBlastPropagation
            || idleWeaponReloading <> settings.EnableIdleWeaponReloading
            || logging <> settings.EnableLogging
        then
            settings.EnableOverpressure <- overpressure
            settings.EnableAutocannonExplosions <- autocannonExplosions
            settings.EnableBlastPropagation <- blastPropagation
            settings.EnableIdleWeaponReloading <- idleWeaponReloading
            settings.EnableLogging <- logging
            settings.Write()

        listing.End()
