package vn.qlkp.companion

import java.nio.ByteBuffer
import java.security.SecureRandom
import javax.crypto.Cipher
import javax.crypto.SecretKeyFactory
import javax.crypto.spec.GCMParameterSpec
import javax.crypto.spec.PBEKeySpec
import javax.crypto.spec.SecretKeySpec

internal object PackageCrypto {
 private const val rounds=310000
 fun open(magic:String,packet:ByteArray,password:String,limit:Int):ByteArray {
  require(packet.size in 56..(limit+56) && String(packet,0,8,Charsets.US_ASCII)==magic &&
   ByteBuffer.wrap(packet,36,4).int==rounds) { "Sai định dạng hoặc kích thước gói." }
  return transform(Cipher.DECRYPT_MODE,packet.copyOfRange(0,40),packet.copyOfRange(40,packet.size),password)
 }

 fun seal(magic:String,plain:ByteArray,password:String):ByteArray {
  val random=ByteArray(28).also { SecureRandom().nextBytes(it) }
  val header=ByteBuffer.allocate(40).put(magic.toByteArray(Charsets.US_ASCII)).put(random).putInt(rounds).array()
  return header+transform(Cipher.ENCRYPT_MODE,header,plain,password)
 }

 private fun transform(mode:Int,header:ByteArray,input:ByteArray,password:String):ByteArray {
  require(password.length in 14..128 && password.all { it.code in 33..126 }) { "Mật khẩu không hợp lệ." }
  val chars=password.toCharArray()
  val spec=PBEKeySpec(chars,header.copyOfRange(8,24),rounds,256)
  val key=try { SecretKeyFactory.getInstance("PBKDF2WithHmacSHA256").generateSecret(spec).encoded }
  finally { spec.clearPassword();chars.fill('\u0000') }
  try {
   val cipher=Cipher.getInstance("AES/GCM/NoPadding")
   cipher.init(mode,SecretKeySpec(key,"AES"),GCMParameterSpec(128,header.copyOfRange(24,36)))
   cipher.updateAAD(header)
   return cipher.doFinal(input)
  } finally { key.fill(0) }
 }
}
