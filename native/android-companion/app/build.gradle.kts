plugins { id("com.android.application"); id("org.jetbrains.kotlin.android") }
val releaseInfo = java.util.Properties().apply {
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
  compileOptions {
    sourceCompatibility = JavaVersion.VERSION_17
    targetCompatibility = JavaVersion.VERSION_17
  }
  kotlinOptions { jvmTarget = "17" }
  lint { abortOnError = true }
}
dependencies { testImplementation("junit:junit:4.13.2") }
