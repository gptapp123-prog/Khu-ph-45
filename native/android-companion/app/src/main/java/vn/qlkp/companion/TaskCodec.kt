package vn.qlkp.companion

import java.nio.ByteBuffer
import java.util.Base64

internal data class OfflineTask(val token:String,val code:String,val due:String,val briefing:String)

internal object TaskCodec {
 const val fileLimit=16440
 private val tokenFormat=Regex("[0-9a-f]{32}")

 fun openDispatch(bytes:ByteArray,password:String):OfflineTask {
  val plain=PackageCrypto.open("QLKPTD01",bytes,password,fileLimit-56)
  try {
   val fields=decode(plain).split('\n')
   require(fields.size==4 && tokenFormat.matches(fields[0]))
   val code=unpack(fields[1]);val due=unpack(fields[2]);val briefing=unpack(fields[3])
   require(code.isNotBlank() && code.length<=50 && due.length<=10 && validText(briefing,2000))
   return OfflineTask(fields[0],code,due,briefing)
  } finally { plain.fill(0) }
 }

 fun sealResult(task:OfflineTask,result:String,password:String):ByteArray {
  val trimmed=result.trim()
  require(tokenFormat.matches(task.token) && validText(trimmed,500)) { "Kết quả cần từ 1 đến 500 ký tự hợp lệ." }
  val plain=(task.token+"\n"+Base64.getEncoder().encodeToString(trimmed.toByteArray(Charsets.UTF_8))).toByteArray(Charsets.UTF_8)
  try { return PackageCrypto.seal("QLKPTR01",plain,password) }
  finally { plain.fill(0) }
 }

 private fun validText(text:String,limit:Int)=text.isNotBlank() && text.length<=limit &&
  text.none { it.isISOControl() && it!='\n' && it!='\r' && it!='\t' }
 private fun unpack(value:String)=decode(Base64.getDecoder().decode(value))
 private fun decode(bytes:ByteArray)=Charsets.UTF_8.newDecoder().decode(ByteBuffer.wrap(bytes)).toString()
}
