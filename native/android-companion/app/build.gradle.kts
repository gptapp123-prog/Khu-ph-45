import java.util.Properties

plugins { id("com.android.application"); id("org.jetbrains.kotlin.android") }
val releaseInfo = Properties().apply {
  rootProject.file("../release.properties").inputStream().use { load(it) }
}
android {
  namespace = "vn.qlkp.companion"
  compileSdk = 35
  defaultConfig {
    applicationId = releaseInfo.getProperty("androidApplicationId")
    minSdk = 26; targetSdk = 35
    versionCode = releaseInfo.getProperty("versionCode").toInt()
    versionName = releaseInfo.getProperty("versionName")
  }
  buildTypes.getByName("debug").applicationIdSuffix = ".test"
  compileOptions {
    sourceCompatibility = JavaVersion.VERSION_17
    targetCompatibility = JavaVersion.VERSION_17
  }
  kotlinOptions { jvmTarget = "17" }
  lint { abortOnError = true }
}
dependencies {
  implementation("androidx.core:core:1.13.1")
  implementation("androidx.exifinterface:exifinterface:1.3.7")
  implementation("com.google.mlkit:text-recognition:16.0.1")
  implementation("com.google.mlkit:barcode-scanning:17.3.0")
  testImplementation("junit:junit:4.13.2")
}
