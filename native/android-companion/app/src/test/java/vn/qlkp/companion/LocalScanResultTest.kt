package vn.qlkp.companion

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class LocalScanResultTest {
 @Test fun normalizesOneValidInternalReference() {
  val payload="QLKP1|B02|"+"A".repeat(32)
  assertEquals("QLKP1|B02|"+"a".repeat(32),localQrReference(listOf(" "+payload+" ",payload)))
 }

 @Test fun rejectsExternalMalformedAndAmbiguousReferences() {
  assertNull(localQrReference(listOf("https://example.invalid","QLKP1|I01|"+"a".repeat(32))))
  assertNull(localQrReference(listOf("QLKP1|B01|"+"a".repeat(32),"QLKP1|B02|"+"b".repeat(32))))
 }
}
