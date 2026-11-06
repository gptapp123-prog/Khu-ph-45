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
import android.widget.ScrollView
import android.view.inputmethod.EditorInfo

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
  phrase=EditText(this).apply {hint="Mật khẩu giải mã gói (≥14 ký tự)";inputType=129;imeOptions=EditorInfo.IME_ACTION_DONE;isSaveEnabled=false}
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
  setContentView(ScrollView(this).apply { isFillViewport=true;addView(wrap) })
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
 private fun decrypt(bytes:ByteArray,pass:String):List<String> = CatalogCodec.decrypt(bytes,pass)
}
