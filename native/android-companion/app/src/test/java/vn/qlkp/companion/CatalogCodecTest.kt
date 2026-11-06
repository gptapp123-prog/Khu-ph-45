package vn.qlkp.companion

import org.junit.Assert.*
import org.junit.Test
import java.nio.ByteBuffer
import java.nio.ByteOrder
import java.util.Base64
import javax.crypto.Cipher
import javax.crypto.SecretKeyFactory
import javax.crypto.spec.GCMParameterSpec
import javax.crypto.spec.PBEKeySpec
import javax.crypto.spec.SecretKeySpec

/** Executes the actual application codec on a JVM. Fixtures contain only public reference labels. */
class CatalogCodecTest {
 private val pass="FixtureOnly_Qlkp_2026!"
 private val row="LOAI_NHA\tNHA_RIENG\t"+Base64.getEncoder().encodeToString("Nhà riêng".toByteArray(Charsets.UTF_8))+"\tL\n"
 private fun packet(text:String):ByteArray {
  val header=ByteArray(40)
  "QLKPSY01".toByteArray(Charsets.US_ASCII).copyInto(header)
  for(i in 8..35)header[i]=i.toByte() // deterministic fixture only; production uses secure randomness
  ByteBuffer.wrap(header,36,4).order(ByteOrder.BIG_ENDIAN).putInt(310000)
  val spec=PBEKeySpec(pass.toCharArray(),header.copyOfRange(8,24),310000,256)
  val key=SecretKeyFactory.getInstance("PBKDF2WithHmacSHA256").generateSecret(spec).encoded
  val cipher=Cipher.getInstance("AES/GCM/NoPadding")
  try {
   cipher.init(Cipher.ENCRYPT_MODE,SecretKeySpec(key,"AES"),GCMParameterSpec(128,header.copyOfRange(24,36)))
   cipher.updateAAD(header)
   return header+cipher.doFinal(text.toByteArray(Charsets.UTF_8))
  } finally {key.fill(0);spec.clearPassword()}
 }
 private fun rejects(action:()->Unit) { assertNotNull("Invalid package accepted",runCatching(action).exceptionOrNull()) }
 @Test fun canonicalV2Decrypts() {
  assertEquals(listOf("LOAI_NHA/NHA_RIENG: Nhà riêng"),CatalogCodec.decrypt(packet("QLKP-CATALOG-V2\n"+row),pass))
 }
 @Test fun pythonGeneratedPCProtocolFixtureDecrypts() {
  val bytes=javaClass.getResourceAsStream("/pc-v2-reference.qlkpsync")!!.use {it.readBytes()}
  assertEquals(listOf("LOAI_NHA/NHA_RIENG: Nhà riêng"),CatalogCodec.decrypt(bytes,pass))
 }
 @Test fun wrongPasswordFailsAuthentication() {rejects {CatalogCodec.decrypt(packet("QLKP-CATALOG-V2\n"+row),"Incorrect_Key_12345!")}}
 @Test fun tamperedCiphertextAndHeaderFailAuthentication() {
  val bytes=packet("QLKP-CATALOG-V2\n"+row)
  for(i in listOf(8,24,44,bytes.lastIndex)) {
   val broken=bytes.copyOf();broken[i]=(broken[i].toInt() xor 1).toByte()
   rejects {CatalogCodec.decrypt(broken,pass)}
  }
 }
 @Test fun roundsAndMagicAreStrict() {
  for(i in listOf(0,39)) {
   val bytes=packet("QLKP-CATALOG-V2\n"+row);bytes[i]=(bytes[i].toInt() xor 1).toByte()
   rejects {CatalogCodec.decrypt(bytes,pass)}
  }
 }
 @Test fun sizeBoundsAreCheckedBeforeDecryption() {
  rejects {CatalogCodec.decrypt(ByteArray(55),pass)}
  rejects {CatalogCodec.decrypt(ByteArray(524345),pass)}
 }
 @Test fun nonCanonicalV2LabelsAreRejected() {
  val bad=row.replace(Base64.getEncoder().encodeToString("Nhà riêng".toByteArray(Charsets.UTF_8)),Base64.getEncoder().encodeToString("Nhãn tùy ý".toByteArray(Charsets.UTF_8)))
  rejects {CatalogCodec.decrypt(packet("QLKP-CATALOG-V2\n"+bad),pass)}
 }
 @Test fun duplicateRowsAndEmptyPayloadAreRejected() {
  rejects {CatalogCodec.decrypt(packet("QLKP-CATALOG-V2\n"+row+row),pass)}
  rejects {CatalogCodec.decrypt(packet("QLKP-CATALOG-V2\n"),pass)}
 }
 @Test fun groupScopeAndVersionAreRestricted() {
  for(text in listOf("QLKP-CATALOG-V2\n"+row.replace("\tL\n","\tX\n"),"QLKP-CATALOG-V3\n"+row,"QLKP-CATALOG-V2\n"+row.replace("LOAI_NHA","PEOPLE")))
   rejects {CatalogCodec.decrypt(packet(text),pass)}
 }
 @Test fun passphraseMustMatchPCAsciiContract() {
  for(value in listOf("short","Password with spaces!","Mật_khẩu_không_hợp_lệ!","a".repeat(129)))
   rejects {CatalogCodec.decrypt(ByteArray(56),value)}
 }
}
