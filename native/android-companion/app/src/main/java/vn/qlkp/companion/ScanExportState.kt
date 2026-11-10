package vn.qlkp.companion

import android.os.Bundle
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import java.security.KeyStore
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec

// Saved activity state contains ciphertext, because OCR output may include personal data.
internal object ScanExportState {
 private const val alias="qlkp_pending_scan_export"
 private const val cipherName="AES/GCM/NoPadding"

 private fun key(create:Boolean):SecretKey {
  val store=KeyStore.getInstance("AndroidKeyStore").apply { load(null) }
  (store.getKey(alias,null) as? SecretKey)?.let { return it }
  check(create) { "Pending export key unavailable" }
  return KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES,"AndroidKeyStore").run {
   init(KeyGenParameterSpec.Builder(alias,KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT)
    .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
    .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
    .build())
   generateKey()
  }
 }

 @Synchronized
 fun seal(text:String):Bundle {
  val cipher=Cipher.getInstance(cipherName).apply { init(Cipher.ENCRYPT_MODE,key(true)) }
  return Bundle().apply {
   putByteArray("iv",cipher.iv)
   putByteArray("ciphertext",cipher.doFinal(text.toByteArray(Charsets.UTF_8)))
  }
 }

 fun open(state:Bundle):ByteArray {
  val iv=checkNotNull(state.getByteArray("iv"))
  val payload=checkNotNull(state.getByteArray("ciphertext"))
  return Cipher.getInstance(cipherName).run {
   init(Cipher.DECRYPT_MODE,key(false),GCMParameterSpec(128,iv))
   doFinal(payload)
  }
 }
}
