package vn.qlkp.companion

private val referencePattern=Regex("QLKP1\\|(B01|B02)\\|([0-9a-fA-F]{32})")

internal fun localQrReference(values:List<String>):String? {
 val valid=values.mapNotNull { value ->
  val match=referencePattern.matchEntire(value.trim()) ?: return@mapNotNull null
  "QLKP1|${match.groupValues[1]}|${match.groupValues[2].lowercase()}"
 }.distinct()
 return valid.singleOrNull()
}
