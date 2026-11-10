package vn.qlkp.companion

import android.app.Activity
import android.content.Intent
import android.graphics.Color
import android.net.Uri
import android.os.Bundle
import android.text.Editable
import android.text.InputFilter
import android.text.TextWatcher
import android.text.InputType
import android.view.Gravity
import android.view.View
import android.view.inputmethod.EditorInfo
import android.widget.Button
import android.widget.EditText
import android.widget.LinearLayout
import android.widget.ScrollView
import android.widget.TextView
import java.io.ByteArrayOutputStream

class MainActivity : Activity() {
 private lateinit var status:TextView
 private lateinit var phrase:EditText
 private lateinit var catalogSearch:EditText
 private lateinit var catalogActions:LinearLayout
 private lateinit var catalogPrevious:Button
 private lateinit var catalogNext:Button
 private lateinit var catalogRows:TextView
 private var catalogEntries=emptyList<String>()
 private var catalogIndex=0
 private lateinit var taskStatus:TextView
 private lateinit var taskSummary:TextView
 private lateinit var taskResult:EditText
 private lateinit var taskActions:LinearLayout
 private var task:OfflineTask?=null
 private var taskPassword:String?=null
 private var pendingResult:ByteArray?=null
 private lateinit var retryResult:Button
 private val requestCatalog=104
 private val requestTask=105
 private val requestResult=106

 override fun onCreate(savedInstanceState:Bundle?) {
  super.onCreate(savedInstanceState)
  pendingResult=savedInstanceState?.getByteArray("pendingResult")
  val wrap=LinearLayout(this).apply {
   orientation=LinearLayout.VERTICAL
   setPadding(30,65,30,30)
   setBackgroundColor(Color.rgb(244,247,252))
  }
  val title=TextView(this).apply { text="QUẢN LÝ KHU PHỐ";textSize=23f;setTextColor(Color.rgb(16,45,89)) }
  val about=TextView(this).apply { text="Nhập mật khẩu rồi chọn gói nhận từ máy tính.";textSize=15f }
  phrase=EditText(this).apply {
   id=R.id.package_password
   hint="Mật khẩu giải mã gói (≥14 ký tự)"
   inputType=InputType.TYPE_CLASS_TEXT or InputType.TYPE_TEXT_VARIATION_PASSWORD
   imeOptions=EditorInfo.IME_ACTION_DONE
   isSaveEnabled=false
   filters=arrayOf(InputFilter.LengthFilter(128))
  }
  val catalogButton=action("Chọn gói .qlkpsync từ máy tính") { openDocument(requestCatalog) }.apply { id=R.id.import_catalog }
  status=TextView(this).apply { id=R.id.catalog_status;text="Chưa chọn dữ liệu ngoại tuyến.";textSize=15f;setTextColor(Color.DKGRAY) }
  taskStatus=TextView(this).apply { text="Chưa mở nhiệm vụ.";textSize=15f }
  taskSummary=TextView(this).apply { textSize=17f }
  taskResult=EditText(this).apply {
   hint="Kết quả thực hiện (tối đa 500 ký tự)"
   inputType=InputType.TYPE_CLASS_TEXT or InputType.TYPE_TEXT_FLAG_MULTI_LINE
   gravity=Gravity.TOP
   minLines=3
   filters=arrayOf(InputFilter.LengthFilter(500))
   isSaveEnabled=false
  }
  taskActions=LinearLayout(this).apply {
   orientation=LinearLayout.VERTICAL
   visibility=View.GONE
   addView(taskSummary)
   addView(taskResult)
   addView(action("Lưu tệp kết quả để gửi PC") { createResult() })
   addView(action("Đóng nhiệm vụ") { clearTask();taskStatus.text="Đã đóng nhiệm vụ." })
  }
  wrap.addView(title)
  wrap.addView(action("Đọc ảnh và mã QR") { startActivity(Intent(this,DocumentScanActivity::class.java)) })
  wrap.addView(about);wrap.addView(phrase)
  wrap.addView(action("Mở nhiệm vụ .qlkptask") { openDocument(requestTask) })
  retryResult=action("Lưu lại tệp kết quả") { chooseResultLocation() }.apply { visibility=View.GONE }
  if(pendingResult!=null)retryResult.visibility=View.VISIBLE
  wrap.addView(taskStatus);wrap.addView(retryResult);wrap.addView(taskActions)
  catalogSearch=EditText(this).apply { hint="Tìm theo mã hoặc nhãn";isSingleLine=true;isSaveEnabled=false }
  catalogRows=TextView(this).apply { textSize=15f;setTextColor(Color.DKGRAY) }
  catalogPrevious=action("Trang trước") { catalogIndex--;showCatalog() }
  catalogNext=action("Trang sau") { catalogIndex++;showCatalog() }
  val paging=LinearLayout(this).apply {
   orientation=LinearLayout.HORIZONTAL
   addView(catalogPrevious);addView(catalogNext)
  }
  catalogActions=LinearLayout(this).apply {
   orientation=LinearLayout.VERTICAL;visibility=View.GONE
   addView(catalogSearch);addView(paging);addView(catalogRows)
   addView(action("Đóng danh mục") { clearCatalog();status.text="Đã đóng danh mục." })
  }
  catalogSearch.addTextChangedListener(object:TextWatcher {
   override fun beforeTextChanged(s:CharSequence?,start:Int,count:Int,after:Int) {}
   override fun onTextChanged(s:CharSequence?,start:Int,before:Int,count:Int) { catalogIndex=0;showCatalog() }
   override fun afterTextChanged(s:Editable?) {}
  })
  wrap.addView(catalogButton);wrap.addView(status);wrap.addView(catalogActions)
  setContentView(ScrollView(this).apply { isFillViewport=true;addView(wrap) })
 }

 private fun action(label:String,click:()->Unit)=Button(this).apply {
  text=label;isAllCaps=false;setOnClickListener { click() }
 }

 @Suppress("DEPRECATION")
 private fun openDocument(request:Int) {
  if(request==requestTask && task!=null) {
   taskStatus.text="Đóng nhiệm vụ hiện tại trước khi mở gói mới."
   return
  }
  startActivityForResult(Intent(Intent.ACTION_OPEN_DOCUMENT).apply {
   addCategory(Intent.CATEGORY_OPENABLE);type="*/*"
  },request)
 }

 override fun onSaveInstanceState(state:Bundle) {
  super.onSaveInstanceState(state)
  // Only the encrypted PC exchange package is saved across activity recreation.
  state.putByteArray("pendingResult",pendingResult)
 }

 private fun createResult() {
  val current=task ?: return
  val password=taskPassword ?: return
  try {
   pendingResult=TaskCodec.sealResult(current,taskResult.text.toString(),password)
   chooseResultLocation()
  } catch(ex:Exception) {
   taskStatus.text=if(taskResult.text.isBlank())"Hãy nhập kết quả thực hiện." else "Không tạo được tệp kết quả."
  }
 }

 @Suppress("DEPRECATION")
 private fun chooseResultLocation() {
  if(pendingResult==null)return
  try {
   startActivityForResult(Intent(Intent.ACTION_CREATE_DOCUMENT).apply {
    addCategory(Intent.CATEGORY_OPENABLE);type="application/octet-stream"
    putExtra(Intent.EXTRA_TITLE,"ket-qua.qlkpresult")
   },requestResult)
   retryResult.visibility=View.GONE
  } catch(ex:Exception) {
   retryResult.visibility=View.VISIBLE
   taskStatus.text="Không mở được trình lưu tệp kết quả."
  }
 }

 @Deprecated("Uses the platform document picker without AndroidX dependencies")
 override fun onActivityResult(requestCode:Int,resultCode:Int,data:Intent?) {
  super.onActivityResult(requestCode,resultCode,data)
  val uri=data?.data
  if(requestCode==requestResult) {
   val bytes=pendingResult
   if(resultCode!=RESULT_OK || uri==null) {
    retryResult.visibility=if(bytes!=null)View.VISIBLE else View.GONE
    taskStatus.text="Đã hủy lưu tệp kết quả."
    return
   }
   try {
    require(bytes!=null)
    contentResolver.openOutputStream(uri,"wt")?.use { it.write(bytes) } ?: error("Không mở được tệp.")
    pendingResult=null
    retryResult.visibility=View.GONE
    taskStatus.text="Đã lưu kết quả. Chuyển tệp .qlkpresult về PC để xác nhận."
   } catch(ex:Exception) {
    retryResult.visibility=if(bytes!=null)View.VISIBLE else View.GONE
    taskStatus.text=if(bytes!=null)"Chưa lưu được tệp kết quả. Hãy thử lại." else "Hãy mở nhiệm vụ và tạo lại tệp kết quả."
   }
   return
  }
  if(resultCode!=RESULT_OK || uri==null || (requestCode!=requestTask && requestCode!=requestCatalog))return
  val pass=phrase.text.toString()
  phrase.text.clear()
  if(requestCode==requestTask) {
   try {
    val opened=TaskCodec.openDispatch(readPackage(uri,TaskCodec.fileLimit),pass)
    task=opened;taskPassword=pass
    taskSummary.text="Mã: ${opened.code}\nHạn: ${opened.due.ifBlank { "Chưa đặt" }}\n\n${opened.briefing}"
    taskActions.visibility=View.VISIBLE
    taskStatus.text="Đã mở nhiệm vụ. Nhập kết quả rồi lưu tệp gửi về PC."
   } catch(ex:Exception) { taskStatus.text="Không mở được nhiệm vụ. Kiểm tra mật khẩu và tệp .qlkptask." }
   return
  }
  try {
   val lines=CatalogCodec.decrypt(readPackage(uri,524344),pass)
   clearCatalog()
   catalogEntries=lines
   catalogActions.visibility=View.VISIBLE
   showCatalog()
  } catch(ex:Exception) {
   clearCatalog()
   status.text="Không mở được gói danh mục. Kiểm tra mật khẩu và tệp đã chọn."
  }
 }

 private fun showCatalog() {
  if(catalogEntries.isEmpty())return
  val result=catalogPage(catalogEntries,catalogSearch.text.toString(),catalogIndex)
  catalogIndex=result.index
  catalogPrevious.isEnabled=catalogIndex>0
  catalogNext.isEnabled=catalogIndex+1<result.pageCount
  status.text=if(result.matchCount==0)"Không có nhãn phù hợp."
   else "Trang ${result.index+1}/${result.pageCount} · ${result.matchCount} nhãn"
  catalogRows.text=result.rows.joinToString("\n")
 }

 private fun clearCatalog() {
  catalogEntries=emptyList();catalogIndex=0
  catalogSearch.text.clear();catalogRows.text=""
  catalogActions.visibility=View.GONE
 }

 private fun readPackage(uri:Uri,limit:Int):ByteArray {
  return contentResolver.openInputStream(uri)?.use { stream ->
   val buffer=ByteArray(8192)
   val out=ByteArrayOutputStream()
   while(true) {
    val n=stream.read(buffer)
    if(n<0)break
    if(out.size()+n>limit)error("Gói vượt giới hạn.")
    out.write(buffer,0,n)
   }
   out.toByteArray()
  } ?: error("Không đọc được tệp.")
 }

 private fun clearTask() {
  task=null;taskPassword=null;pendingResult=null
  retryResult.visibility=View.GONE
  taskResult.text.clear();taskSummary.text=""
  taskActions.visibility=View.GONE
 }
}
