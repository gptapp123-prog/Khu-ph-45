package vn.qlkp.companion

import java.util.Base64
import java.nio.ByteBuffer

/** Shared JVM/Android codec; UI never substitutes a successful result for invalid input. */
internal object CatalogCodec {
 private fun referenceEntry(group:String,code:String,label:String):Boolean {
  val housing=mapOf("NHA_RIENG" to "Nhà riêng","NHA_CAP4" to "Nhà cấp 4","CAN_HO" to "Căn hộ","PHONG_TRO" to "Phòng trọ","NHA_THUE" to "Nhà thuê","NHA_TAP_THE" to "Nhà tập thể","O_CUNG_THAN_NHAN" to "Ở cùng thân nhân","CHUA_XAC_MINH" to "Chưa xác minh")
  val residency=mapOf("THUONG_TRU" to "Thường trú","TAM_TRU" to "Tạm trú","LUU_TRU" to "Lưu trú","TAM_VANG" to "Tạm vắng","CHUA_XAC_MINH" to "Chưa đối chiếu")
  return (if(group=="LOAI_NHA")housing[code] else if(group=="NHOM_CU_TRU")residency[code] else null)==label
 }
 fun decrypt(bytes:ByteArray,pass:String):List<String> {
  val plain=PackageCrypto.open("QLKPSY01",bytes,pass,524288)
  try {
   val raw=Charsets.UTF_8.newDecoder().decode(ByteBuffer.wrap(plain)).toString()
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
  } finally {plain.fill(0)}
 }
}
