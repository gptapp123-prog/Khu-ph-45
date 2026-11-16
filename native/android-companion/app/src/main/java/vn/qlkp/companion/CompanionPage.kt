package vn.qlkp.companion

import android.app.Activity
import android.graphics.Color
import android.view.Gravity
import android.view.View
import android.view.ViewGroup.LayoutParams.MATCH_PARENT
import android.view.ViewGroup.LayoutParams.WRAP_CONTENT
import android.widget.LinearLayout
import android.widget.ScrollView
import androidx.core.view.ViewCompat
import androidx.core.view.WindowCompat
import androidx.core.view.WindowInsetsCompat
import kotlin.math.roundToInt

internal fun Activity.dp(value: Int): Int = (value * resources.displayMetrics.density).roundToInt()

internal fun Activity.showCompanionPage(page: LinearLayout, fixedHeader:View?=null) {
 val gutter=dp(16)
 val scroll=ScrollView(this).apply {
  isFillViewport=true
  addView(page,android.widget.FrameLayout.LayoutParams(MATCH_PARENT,WRAP_CONTENT,Gravity.TOP))
  isFocusableInTouchMode=true
 }
 val root=LinearLayout(this).apply {
  orientation=LinearLayout.VERTICAL
  setBackgroundColor(Color.rgb(240,242,243))
  fixedHeader?.let { addView(it,LinearLayout.LayoutParams(MATCH_PARENT,WRAP_CONTENT)) }
  addView(scroll,LinearLayout.LayoutParams(MATCH_PARENT,0,1f))
 }
 WindowCompat.setDecorFitsSystemWindows(window,false)
 ViewCompat.setOnApplyWindowInsetsListener(root) { view,insets ->
  val safe=insets.getInsets(WindowInsetsCompat.Type.systemBars() or WindowInsetsCompat.Type.displayCutout() or WindowInsetsCompat.Type.ime())
  view.setPadding(safe.left+gutter,safe.top+gutter,safe.right+gutter,safe.bottom+gutter)
  insets
 }
 setContentView(root)
 scroll.requestFocus()
 ViewCompat.requestApplyInsets(root)
}
