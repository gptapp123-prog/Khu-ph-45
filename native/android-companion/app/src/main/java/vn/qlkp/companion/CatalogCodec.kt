package vn.qlkp.companion

import javax.crypto.Cipher
import javax.crypto.SecretKeyFactory
import javax.crypto.spec.GCMParameterSpec
import javax.crypto.spec.PBEKeySpec
import javax.crypto.spec.SecretKeySpec
import java.util.Base64
import java.nio.ByteBuffer
import java.nio.ByteOrder
import java.nio.charset.Charset

/** Shared JVM/Android codec; UI never substitutes a successful result for invalid input. */
internal object CatalogCodec {
 private fun referenceEntry(group:String,code:String,label:String):Boolean {
  val housing=mapOf("NHA_RIENG" to "Nhà riêng","NHA_CAP4" to "Nhà cấp 4","CAN_HO" to "Căn hộ","PHONG_TRO" to "Phòng trọ","NHA_THUE" to "Nhà thuê","NHA_TAP_THE" to "Nhà tập thể","O_CUNG_THAN_NHAN" to "Ở cùng thân nhân","CHUA_XAC_MINH" to "Chưa xác minh")
  val residency=mapOf("THUONG_TRU" to "Thường trú","TAM_TRU" to "Tạm trú","LUU_TRU" to "Lưu trú","TAM_VANG" to "Tạm vắng","CHUA_XAC_MINH" to "Chưa đối chiếu")
  return (if(group=="LOAI_NHA")housing[code] else if(group=="NHOM_CU_TRU")residency[code] else null)==label
 }
 fun decrypt(bytes:ByteArray,pass:String):List<String> {
  require(pass.length in 14..128 && pass.all { it.code in 33..126 }) { "Mật khẩu không hợp lệ" }
  if(bytes.size<56 || bytes.size>524344)error("Độ dài gói không hợp lệ")
  if(String(bytes,0,8,Charsets.US_ASCII)!="QLKPSY01")error("Sai định dạng")
  val salt=bytes.copyOfRange(8,24);val nonce=bytes.copyOfRange(24,36)
  val rounds=ByteBuffer.wrap(bytes,36,4).order(ByteOrder.BIG_ENDIAN).int
  if(rounds!=310000)error("Thông số khóa không được hỗ trợ")
  val passChars=pass.toCharArray();val keySpec=PBEKeySpec(passChars,salt,rounds,256)
  val key=SecretKeyFactory.getInstance("PBKDF2WithHmacSHA256").generateSecret(keySpec).encoded
  keySpec.clearPassword();passChars.fill('\u0000')
  val cipher=Cipher.getInstance("AES/GCM/NoPadding")
  var plain:ByteArray?=null
  try {
   cipher.init(Cipher.DECRYPT_MODE,SecretKeySpec(key,"AES"),GCMParameterSpec(128,nonce))
   cipher.updateAAD(bytes.copyOfRange(0,40))
   plain=cipher.doFinal(bytes.copyOfRange(40,bytes.size))
   val raw=Charset.forName("UTF-8").newDecoder().decode(ByteBuffer.wrap(plain)).toString()
   val rows=raw.split('\n');val version=rows.firstOrNull()
   if(version!="QLKP-CATALOG-V1" && version!="QLKP-CATALOG-V2")error("Sai loại gói")
   val result=mutableListOf<String>();val seen=mutableSetOf<String>()
   for (line in rows.drop(1)) {
    if(line.isEmpty())continue
    if(result.size>=1000)error("Gói quá nhiều danh mục")
    val fields=line.split('\t');if(fields.size!=4)error("Sai cấu trúc danh mục")
    if(!fields[0].matches(Regex("[A-Z_]{2,32}")) || !fields[1].matches(Regex("[A-Z0-9_]{2,32}")))error("Mã không hợp lệ")
    if(fields[3]!="G" && fields[3]!="L")error("Sai phạm vi")
    val label=Charsets.UTF_8.newDecoder().decode(ByteBuffer.wrap(Base64.getDecoder().decode(fields[2]))).toString()
    if(label.isBlank() || label.length>80 || label.any { it.isISOControl() })error("Nhãn không hợp lệ")
    if(version=="QLKP-CATALOG-V2" && !referenceEntry(fields[0],fields[1],label))error("Danh mục ngoài mã chuẩn")
    if(!seen.add(fields[0]+"\t"+fields[1]+"\t"+fields[3]))error("Trùng danh mục")
    result.add(fields[0]+"/"+fields[1]+": "+label)
   }
   if(result.isEmpty())error("Gói không có danh mục")
   return result
  } finally {plain?.fill(0);key.fill(0);salt.fill(0);nonce.fill(0)}
 }
}
