export type AutocannonExplosionTarget = {
  defName: string
  damage: number
  radius: number
}

const escapeXml = (value: string) =>
  value.replaceAll("&", "&amp;").replaceAll('"', "&quot;").replaceAll(
    "<",
    "&lt;",
  )

export const autocannonPatch = (targets: AutocannonExplosionTarget[]) =>
  `<?xml version="1.0" encoding="utf-8"?>
<Patch><Operation Class="PatchOperationFindMod"><mods><li>Combat Extended</li></mods><match Class="PatchOperationSequence"><operations>
${
    targets.map((ammo) =>
      `  <li Class="PatchOperationAdd"><xpath>Defs/ThingDef[defName="${
        escapeXml(ammo.defName)
      }"]</xpath><value><comps><li Class="CombatExtended.CompProperties_ExplosiveCE"><damageAmountBase>${ammo.damage}</damageAmountBase><explosiveDamageType>Bomb</explosiveDamageType><explosiveRadius>${ammo.radius}</explosiveRadius><applyDamageToExplosionCellsNeighbors>true</applyDamageToExplosionCellsNeighbors></li></comps><modExtensions><li Class="CEOverPressure.AutocannonExplosionExtension" /></modExtensions></value></li>`
    ).join("\n")
  }
</operations></match></Operation></Patch>
`
