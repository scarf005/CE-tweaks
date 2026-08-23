namespace CEOverPressure

module internal IdleReloadPriority =
    let choose enabled drafted priority idlePriority = if enabled && not drafted && priority > 0.0f then idlePriority else 0.0f
