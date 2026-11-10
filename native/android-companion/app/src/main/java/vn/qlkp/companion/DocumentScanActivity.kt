package vn.qlkp.companion

import android.app.Activity
import android.content.ClipData
import android.content.Intent
import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.graphics.Matrix
import android.net.Uri
import android.os.Bundle
import android.provider.MediaStore
import android.text.InputType
import android.view.View
import android.view.WindowManager
import android.widget.Button
import android.widget.EditText
import android.widget.LinearLayout
import android.widget.RadioButton
import android.widget.RadioGroup
import android.widget.ScrollView
import android.widget.TextView
import androidx.core.content.FileProvider
import androidx.exifinterface.media.ExifInterface
import com.google.mlkit.vision.barcode.BarcodeScannerOptions
import com.google.mlkit.vision.barcode.BarcodeScanning
import com.google.mlkit.vision.barcode.common.Barcode
import com.google.mlkit.vision.common.InputImage
import com.google.mlkit.vision.text.TextRecognition
import com.google.mlkit.vision.text.latin.TextRecognizerOptions
import java.io.ByteArrayInputStream
import java.io.ByteArrayOutputStream
import java.io.File
import java.util.concurrent.Executors

class DocumentScanActivity : Activity() {
 private lateinit var status:TextView
 private lateinit var result:EditText
 private lateinit var photo:Button
 private lateinit var pick:Button
 private lateinit var save:Button
 private lateinit var modes:RadioGroup
 private lateinit var qrOption:RadioButton
 private val decoder=Executors.newSingleThreadExecutor()
 private var capture:File?=null
 private var qr=false
 private var exportText:String?=null
 private val pickImage=301
 private val takeImage=302
 private val saveText=303

 override fun onCreate(savedInstanceState:Bundle?) {
  super.onCreate(savedInstanceState)
  window.addFlags(WindowManager.LayoutParams.FLAG_SECURE)
  qr=savedInstanceState?.getBoolean("qr") ?: false
  savedInstanceState?.getString("capture")?.takeIf { it.matches(Regex("capture-[A-Za-z0-9-]+\\.jpg")) }
   ?.let { capture=File(File(cacheDir,"scan-images"),it) }
  if(capture==null)File(cacheDir,"scan-images").listFiles()?.forEach { it.delete() }
  val page=LinearLayout(this).apply {
   orientation=LinearLayout.VERTICAL
   setPadding(24,54,24,28)
  }
  page.addView(TextView(this).apply { text="Đọc ảnh và mã QR";textSize=23f })
  val ocrOption=RadioButton(this).apply { id=View.generateViewId();text="Văn bản OCR" }
  qrOption=RadioButton(this).apply { id=View.generateViewId();text="QR hồ sơ" }
  modes=RadioGroup(this).apply {
   orientation=LinearLayout.HORIZONTAL
   addView(ocrOption);addView(qrOption)
   check(if(qr)qrOption.id else ocrOption.id)
  }
  photo=button("Chụp ảnh") { startCapture() }
  pick=button("Chọn ảnh có sẵn") { selectImage() }
  save=button("Lưu kết quả TXT") { export() }.apply { isEnabled=false }
  status=TextView(this).apply { text="Chọn loại dữ liệu và chụp hoặc mở ảnh." }
  result=EditText(this).apply {
   inputType=InputType.TYPE_CLASS_TEXT or InputType.TYPE_TEXT_FLAG_MULTI_LINE
   minLines=5
   isSaveEnabled=false
   hint="Kết quả nhận dạng"
  }
  modes.setOnCheckedChangeListener { _,_ -> result.text.clear();save.isEnabled=false }
  page.addView(modes);page.addView(photo);page.addView(pick)
  page.addView(status);page.addView(result);page.addView(save)
  page.addView(button("Quay lại") { finish() })
  setContentView(ScrollView(this).apply { addView(page) })
 }

 override fun onSaveInstanceState(state:Bundle) {
  super.onSaveInstanceState(state)
  state.putBoolean("qr",qr)
  state.putString("capture",capture?.name)
 }

 override fun onDestroy() {
  if(!isChangingConfigurations)capture?.delete()
  decoder.shutdown()
  super.onDestroy()
 }

 private fun button(label:String, action:()->Unit)=Button(this).apply {
  text=label;isAllCaps=false;setOnClickListener { action() }
 }

 @Suppress("DEPRECATION")
 private fun selectImage() {
  qr=modes.checkedRadioButtonId==qrOption.id
  try {
   startActivityForResult(Intent(Intent.ACTION_OPEN_DOCUMENT).apply {
    addCategory(Intent.CATEGORY_OPENABLE);type="image/*"
   },pickImage)
  } catch(ex:Exception) { status.text="Không mở được trình chọn ảnh." }
 }

 @Suppress("DEPRECATION")
 private fun startCapture() {
  qr=modes.checkedRadioButtonId==qrOption.id
  val folder=File(cacheDir,"scan-images")
  try {
   folder.mkdirs()
   val target=File.createTempFile("capture-",".jpg",folder)
   capture=target
   val output=FileProvider.getUriForFile(this,packageName+".scan-files",target)
   startActivityForResult(Intent(MediaStore.ACTION_IMAGE_CAPTURE).apply {
    putExtra(MediaStore.EXTRA_OUTPUT,output)
    clipData=ClipData.newRawUri("ảnh tạm",output)
    addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION or Intent.FLAG_GRANT_WRITE_URI_PERMISSION)
   },takeImage)
  } catch(ex:Exception) {
   capture?.delete();capture=null
   status.text="Không mở được camera trên thiết bị."
  }
 }

 @Suppress("DEPRECATION")
 private fun export() {
  val value=result.text.toString().trim()
  if(value.isEmpty()||(qr&&localQrReference(listOf(value))==null)||value.toByteArray(Charsets.UTF_8).size>65536) {
   status.text="Kết quả trống, sai định dạng hoặc vượt 64 KB."
   return
  }
  exportText=value
  try {
   startActivityForResult(Intent(Intent.ACTION_CREATE_DOCUMENT).apply {
    addCategory(Intent.CATEGORY_OPENABLE);type="text/plain"
    putExtra(Intent.EXTRA_TITLE,if(qr)"qlkp-qr.txt" else "qlkp-ocr.txt")
   },saveText)
  } catch(ex:Exception) { exportText=null;status.text="Không mở được trình lưu tệp." }
 }

 @Deprecated("Uses platform activity results for delegated camera capture and document selection")
 override fun onActivityResult(requestCode:Int,resultCode:Int,data:Intent?) {
  super.onActivityResult(requestCode,resultCode,data)
  val uri=data?.data
  when(requestCode) {
   saveText -> {
    val text=exportText
    exportText=null
    if(resultCode==RESULT_OK&&uri!=null&&text!=null)try {
     contentResolver.openOutputStream(uri,"w")?.use { it.write(text.toByteArray(Charsets.UTF_8)) }
      ?: error("Không mở được tệp.")
     status.text="Đã lưu tệp TXT."
    } catch(ex:Exception) { status.text="Không lưu được tệp TXT." }
   }
   pickImage -> if(resultCode==RESULT_OK&&uri!=null)recognize(uri,null)
   takeImage -> {
    val file=capture
    capture=null
    if(resultCode==RESULT_OK&&file!=null) {
     val localUri=FileProvider.getUriForFile(this,packageName+".scan-files",file)
     revokeUriPermission(localUri,Intent.FLAG_GRANT_READ_URI_PERMISSION or Intent.FLAG_GRANT_WRITE_URI_PERMISSION)
     recognize(localUri,file)
    } else { file?.delete();status.text="Đã hủy chụp ảnh." }
   }
  }
 }

 private fun recognize(uri:Uri,temporary:File?) {
  val scanQr=qr
  result.text.clear();save.isEnabled=false
  setInputEnabled(false)
  status.text="Đang đọc ảnh trên thiết bị…"
  decoder.execute {
   try {
    val bitmap=loadImage(uri)
    runOnUiThread {
     if(isDestroyed||isFinishing)bitmap.recycle()
     else try { processImage(bitmap,scanQr) } catch(ex:Exception) {
      bitmap.recycle()
      complete(null,"Không khởi động được bộ nhận dạng ảnh.")
     }
    }
   } catch(ex:Exception) { runOnUiThread { complete(null,"Không đọc được ảnh. Kiểm tra định dạng và kích thước tệp.") } }
   finally { temporary?.delete() }
  }
 }

 private fun processImage(bitmap:Bitmap,scanQr:Boolean) {
  val input=InputImage.fromBitmap(bitmap,0)
  if(scanQr) {
   val scanner=BarcodeScanning.getClient(BarcodeScannerOptions.Builder().setBarcodeFormats(Barcode.FORMAT_QR_CODE).build())
   scanner.process(input).addOnCompleteListener { task ->
    try {
     val reference=if(task.isSuccessful)localQrReference(task.result.orEmpty().mapNotNull { it.rawValue }) else null
     complete(reference,"Ảnh cần có đúng một mã QR hồ sơ QLKP.")
    } finally { scanner.close();bitmap.recycle() }
   }
  } else {
   val recognizer=TextRecognition.getClient(TextRecognizerOptions.DEFAULT_OPTIONS)
   recognizer.process(input).addOnCompleteListener { task ->
    try {
     val value=if(task.isSuccessful)task.result?.text.orEmpty().trim().takeIf { it.isNotEmpty() } else null
     complete(value,"Không nhận dạng được văn bản rõ trong ảnh.")
    } finally { recognizer.close();bitmap.recycle() }
   }
  }
 }

 private fun setInputEnabled(enabled:Boolean) {
  photo.isEnabled=enabled;pick.isEnabled=enabled
  for(i in 0 until modes.childCount)modes.getChildAt(i).isEnabled=enabled
 }

 private fun complete(value:String?,error:String) {
  if(isDestroyed||isFinishing)return
  setInputEnabled(true)
  result.setText(value.orEmpty())
  save.isEnabled=value!=null
  status.text=if(value==null)error else if(qr)"Đã đọc mã hồ sơ. Lưu TXT và mở bằng I05 trên PC."
    else "Đã đọc văn bản. Đối chiếu kết quả trước khi lưu."
 }

 private fun loadImage(uri:Uri):Bitmap {
  val bytes=contentResolver.openInputStream(uri)?.use { source ->
   val out=ByteArrayOutputStream()
   val buffer=ByteArray(8192)
   while(true) {
    val count=source.read(buffer)
    if(count<0)break
    require(out.size()+count<=16*1024*1024)
    out.write(buffer,0,count)
   }
   out.toByteArray()
  } ?: error("Không mở được ảnh.")
  val options=BitmapFactory.Options().apply { inJustDecodeBounds=true }
  BitmapFactory.decodeByteArray(bytes,0,bytes.size,options)
  require(options.outWidth in 1..16000&&options.outHeight in 1..16000)
  val size=maxOf(options.outWidth,options.outHeight)
  var sample=1
  while(size/sample>2600)sample*=2
  val original=BitmapFactory.decodeByteArray(bytes,0,bytes.size,BitmapFactory.Options().apply { inSampleSize=sample })
   ?: error("Không giải mã được ảnh.")
  val exif=try { ExifInterface(ByteArrayInputStream(bytes)) } catch(ex:java.io.IOException) { null }
  val rotation=exif?.rotationDegrees ?: 0
  val flipped=exif?.isFlipped ?: false
  if(rotation==0&&!flipped)return original
  val transform=Matrix()
  if(flipped)transform.postScale(-1f,1f)
  transform.postRotate(rotation.toFloat())
  val corrected=Bitmap.createBitmap(original,0,0,original.width,original.height,transform,true)
  if(corrected!==original)original.recycle()
  return corrected
 }
}
