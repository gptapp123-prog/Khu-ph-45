package vn.qlkp.companion

import java.util.Base64
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.fail
import org.junit.Test

class TaskCodecTest {
 private val password="TaskFixture_2026!"
 private val token="1234567890124abc8abc123456789012"
 // Independent wire fixture generated with Python hashlib and cryptography.
 private fun fixture()=Base64.getDecoder().decode("UUxLUFREMDEAAQIDBAUGBwgJCgsMDQ4PEBESExQVFhcYGRobAAS68Gikn9CL+bSFKwQvg1jgHnX3eK91woBVNqyiA4Dupaz/rMHQ8zakNCJqbBPrB0xCPaYHh/9ahVVhjXrQgi+1JEqte8sRzk4JY4PnhYNVa5TFLhUQ3X5RHvtAowsKXPKrLDFU3V2hqOTAcEYlaSwnPw==")

 @Test fun opensWindowsFormatAndReturnsVietnameseResult() {
  val task=TaskCodec.openDispatch(fixture(),password)
  assertEquals(OfflineTask(token,"NV-001","10-11-2026","Kiểm tra đèn đường."),task)
  val first=TaskCodec.sealResult(task,"Đã kiểm tra.",password)
  val second=TaskCodec.sealResult(task,"Đã kiểm tra.",password)
  assertFalse(first.contentEquals(second))
  val plain=PackageCrypto.open("QLKPTR01",first,password,16384).toString(Charsets.UTF_8)
  assertEquals(token+"\nxJDDoyBraeG7g20gdHJhLg==",plain)
 }

 @Test fun rejectsWrongPasswordTamperingAndWrongDirection() {
  rejected { TaskCodec.openDispatch(fixture(),"wrong_password_123") }
  rejected { TaskCodec.openDispatch(fixture().also { it[it.lastIndex]=(it.last().toInt() xor 1).toByte() },password) }
  val task=TaskCodec.openDispatch(fixture(),password)
  rejected { TaskCodec.openDispatch(TaskCodec.sealResult(task,"Đã kiểm tra.",password),password) }
  rejected { TaskCodec.sealResult(task," ",password) }
  rejected { TaskCodec.sealResult(task,"x".repeat(501),password) }
 }

 private fun rejected(action:()->Unit) {
  try { action() } catch(ex:Exception) { return }
  fail("Invalid input was accepted")
 }
}
