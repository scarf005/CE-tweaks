export RIMWORLD_DIR := env("RIMWORLD_DIR", "/media/scarf/@steam/SteamLibrary/steamapps/common/RimWorld")
export RIMWORLD_MODS_CONFIG := env("RIMWORLD_MODS_CONFIG", "/home/scarf/.config/unity3d/Ludeon Studios/RimWorld by Ludeon Studios/Config/ModsConfig.xml")
export COMBAT_EXTENDED_DIR := env("COMBAT_EXTENDED_DIR", "/media/scarf/@steam/SteamLibrary/steamapps/workshop/content/294100/2890901044")

mod_directory := "CE-tweaks"

# Run overpressure and generator tests
test:
    mise exec dotnet@8.0.422 -- dotnet fsi Tests/PressureModelTests.fsx
    deno test --allow-read scripts

# Benchmark prepared pressure evaluation against the previous per-pawn calculation
bench:
    DOTNET_TieredCompilation=0 mise exec dotnet@8.0.422 -- dotnet fsi --optimize+ Benchmarks/PressureModelBenchmarks.fsx

# Format F# and Deno sources with their tracked configuration
fmt:
    mise exec dotnet@8.0.422 -- dotnet tool restore
    mise exec dotnet@8.0.422 -- dotnet tool run fantomas Source/CEOverPressure Tests Benchmarks
    deno fmt

# Build the assembly and regenerate autocannon runtime patches
generate:
    deno check scripts/autocannon_patch.ts scripts/calculate_radius.ts scripts/ce_ammo.ts scripts/generate.ts
    deno test --allow-read scripts
    deno run --allow-read=.,"${COMBAT_EXTENDED_DIR}" --allow-write=. --allow-run=magick scripts/generate.ts "${COMBAT_EXTENDED_DIR}"
    deno fmt Patches/AutocannonExplosions.xml docs/AutocannonExplosions.md

build: generate
    mise exec dotnet@8.0.422 -- dotnet build Source/CEOverPressure/CEOverPressure.fsproj -c Release

# Build and install only the runtime mod tree locally
install: build
    #!/bin/sh
    set -eu
    mods_directory="${RIMWORLD_DIR}/Mods"
    if [ ! -d "$mods_directory" ]; then
        printf 'RimWorld Mods directory not found: %s\n' "$mods_directory" >&2
        exit 1
    fi
    destination="$mods_directory/{{mod_directory}}"
    staging="$(mktemp -d)"
    trap 'rm -rf "$staging"' EXIT
    rsync --archive About Defs Languages Patches LoadFolders.xml "$staging/"
    mkdir -p "$staging/1.6/Assemblies"
    rsync --archive 1.6/Assemblies/CETweaks.dll "$staging/1.6/Assemblies/"
    mkdir -p "$destination"
    rsync --archive --delete "$staging/" "$destination/"
    printf 'Installed {{mod_directory}} to %s\n' "$destination"

# Build, install, and enable the mod locally
enable: install
    #!/usr/bin/env python3
    import os
    import subprocess
    import xml.etree.ElementTree as ET
    from pathlib import Path

    if subprocess.run(
        ["pgrep", "-x", "RimWorldLinux"],
        check=False,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    ).returncode == 0:
        raise SystemExit("Close RimWorld before enabling the mod.")

    package_id = (ET.parse("About/About.xml").getroot().findtext("packageId") or "").strip()
    if not package_id:
        raise SystemExit("About/About.xml has no packageId.")

    config_path = Path(os.environ["RIMWORLD_MODS_CONFIG"])
    tree = ET.parse(config_path)
    active_mods = tree.getroot().find("activeMods")
    if active_mods is None:
        raise SystemExit(f"{config_path} has no activeMods element.")

    if package_id in ((node.text or "").strip() for node in active_mods.findall("li")):
        print(f"{package_id} is already enabled.")
        raise SystemExit(0)

    ET.SubElement(active_mods, "li").text = package_id
    ET.indent(tree, space="  ")
    temporary_path = config_path.with_name(f"{config_path.name}.tmp")
    tree.write(temporary_path, encoding="utf-8", xml_declaration=True)
    os.chmod(temporary_path, config_path.stat().st_mode)
    temporary_path.replace(config_path)
    print(f"Enabled {package_id} in {config_path}.")
