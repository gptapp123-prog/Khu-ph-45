package vn.qlkp.companion

import android.app.Activity
import android.graphics.Color
import android.view.Gravity
import android.view.ViewGroup.LayoutParams.MATCH_PARENT
import android.view.ViewGroup.LayoutParams.WRAP_CONTENT
import android.widget.LinearLayout
import android.widget.ScrollView
import androidx.core.view.ViewCompat
import androidx.core.view.WindowCompat
import androidx.core.view.WindowInsetsCompat
import kotlin.math.roundToInt

internal fun Activity.dp(value: Int): Int = (value * resources.displayMetrics.density).roundToInt()

internal fun Activity.showCompanionPage(page: LinearLayout) {
 val gutter=dp(16)
 val scroll=ScrollView(this).apply {
  isFillViewport=true
  clipToPadding=false
  setBackgroundColor(Color.rgb(240,242,243))
  addView(page,android.widget.FrameLayout.LayoutParams(MATCH_PARENT,WRAP_CONTENT,Gravity.TOP))
  isFocusableInTouchMode=true
 }
 WindowCompat.setDecorFitsSystemWindows(window,false)
 ViewCompat.setOnApplyWindowInsetsListener(scroll) { view,insets ->
  val safe=insets.getInsets(WindowInsetsCompat.Type.systemBars() or WindowInsetsCompat.Type.displayCutout() or WindowInsetsCompat.Type.ime())
  view.setPadding(safe.left+gutter,safe.top+gutter,safe.right+gutter,safe.bottom+gutter)
  insets
 }
 setContentView(scroll)
 scroll.requestFocus()
 ViewCompat.requestApplyInsets(scroll)
}
