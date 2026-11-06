package vn.qlkp.companion

import android.app.Activity
import android.content.Intent
import android.graphics.Color
import android.os.Bundle
import android.view.Gravity
import android.widget.Button
import android.widget.EditText
import android.widget.LinearLayout
import android.widget.TextView
import javax.crypto.Cipher
import javax.crypto.SecretKeyFactory
import javax.crypto.spec.GCMParameterSpec
import javax.crypto.spec.PBEKeySpec
import javax.crypto.spec.SecretKeySpec
import java.util.Base64
import java.nio.ByteBuffer
import java.nio.ByteOrder
import java.nio.charset.Charset

/** I06 PILOT: offline catalog viewer; no LAN, automatic sync, PII storage or network access. */
class MainActivity : Activity() {
 private lateinit var status: TextView
 private lateinit var phrase: EditText
 private val requestCatalog=104
 override fun onCreate(savedInstanceState:Bundle?) {
  super.onCreate(savedInstanceState)
  val wrap=LinearLayout(this).apply {
   orientation=LinearLayout.VERTICAL;setPadding(30,65,30,30);setBackgroundColor(Color.rgb(244,247,252))
   gravity=Gravity.CENTER_HORIZONTAL
  }
  val title=TextView(this).apply {text="QUẢN LÝ KHU PHỐ";textSize=23f;setTextColor(Color.rgb(16,45,89))}
  val about=TextView(this).apply {
   text="Trình đọc danh mục ngoại tuyến\nKHÔNG đồng bộ LAN/hai chiều; không lưu dữ liệu công dân, mật khẩu hoặc nội dung danh mục trên điện thoại."
   textSize=15f
  }
  phrase=EditText(this).apply {hint="Mật khẩu giải mã gói (≥14 ký tự)";inputType=129}
  val pick=Button(this).apply {
   text="Chọn gói .qlkpsync từ máy tính"
   setOnClickListener {
    val intent=Intent(Intent.ACTION_OPEN_DOCUMENT).apply {
     addCategory(Intent.CATEGORY_OPENABLE);type="*/*"
    }
    startActivityForResult(intent,requestCatalog)
   }
  }
  status=TextView(this).apply {text="Chưa chọn dữ liệu ngoại tuyến.";textSize=15f;setTextColor(Color.DKGRAY)}
  wrap.addView(title);wrap.addView(about);wrap.addView(phrase);wrap.addView(pick);wrap.addView(status)
  setContentView(wrap)
 }
 @Deprecated("Uses documented Android activity result API without AndroidX dependencies")
 override fun onActivityResult(requestCode:Int,resultCode:Int,data:Intent?) {
  super.onActivityResult(requestCode,resultCode,data)
  if(requestCode!=requestCatalog || resultCode!=RESULT_OK || data?.data==null)return
  var bytes:ByteArray?=null
  try {
   val pass=phrase.text.toString();if(pass.length<14 || pass.length>128 || pass.any { it.code<33 || it.code>126 })error("Mật khẩu cần 14–128 ký tự ASCII không dấu, không khoảng trắng")
   val uri=data.data!!
   if(!uri.toString().endsWith(".qlkpsync",ignoreCase=true) && uri.scheme!="content")error("Tệp không hợp lệ")
   bytes=contentResolver.openInputStream(uri)?.use { stream ->
    val buffer=ByteArray(524345);val out=java.io.ByteArrayOutputStream();var n:Int
    while(true){n=stream.read(buffer);if(n<0)break;out.write(buffer,0,n);if(out.size()>524344)error("Gói vượt giới hạn 512 KB")}
    out.toByteArray()
   } ?: error("Không đọc được tệp")
   val lines=decrypt(bytes,pass)
   status.text="Danh mục đã giải mã (chỉ xem, không lưu):\n"+lines.take(30).joinToString("\n")+(if(lines.size>30)"\n... và ${lines.size-30} nhãn khác" else "")
  } catch(ex:Exception){status.text="Từ chối gói: sai mật khẩu, tệp hỏng hoặc sai định dạng. "+ex.javaClass.simpleName}
  finally {bytes?.fill(0);phrase.text.clear()}
 }
 private fun referenceEntry(group:String,code:String,label:String):Boolean {
  val housing=mapOf("NHA_RIENG" to "Nhà riêng","NHA_CAP4" to "Nhà cấp 4","CAN_HO" to "Căn hộ","PHONG_TRO" to "Phòng trọ","NHA_THUE" to "Nhà thuê","NHA_TAP_THE" to "Nhà tập thể","O_CUNG_THAN_NHAN" to "Ở cùng thân nhân","CHUA_XAC_MINH" to "Chưa xác minh")
  val residency=mapOf("THUONG_TRU" to "Thường trú","TAM_TRU" to "Tạm trú","LUU_TRU" to "Lưu trú","TAM_VANG" to "Tạm vắng","CHUA_XAC_MINH" to "Chưa đối chiếu")
  return (if(group=="LOAI_NHA")housing[code] else if(group=="NHOM_CU_TRU")residency[code] else null)==label
 }
 private fun decrypt(bytes:ByteArray,pass:String):List<String> {
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
   val result=mutableListOf<String>()
   for (line in rows.drop(1)) {
    if(line.isEmpty())continue
    if(result.size>=1000)error("Gói quá nhiều danh mục")
    val fields=line.split('\t');if(fields.size!=4)error("Sai cấu trúc danh mục")
    if(!fields[0].matches(Regex("[A-Z_]{2,32}")) || !fields[1].matches(Regex("[A-Z0-9_]{2,32}")))error("Mã không hợp lệ")
    if(fields[3]!="G" && fields[3]!="L")error("Sai phạm vi")
    val label=String(Base64.getDecoder().decode(fields[2]),Charsets.UTF_8)
    if(label.isBlank() || label.length>80)error("Nhãn không hợp lệ")
    if(version=="QLKP-CATALOG-V2" && !referenceEntry(fields[0],fields[1],label))error("Danh mục ngoài mã chuẩn")
    result.add(fields[0]+"/"+fields[1]+": "+label)
   }
   return result
  } finally {plain?.fill(0);key.fill(0);salt.fill(0);nonce.fill(0)}
 }
}
