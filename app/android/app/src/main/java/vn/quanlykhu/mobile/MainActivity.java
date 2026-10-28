package vn.quanlykhu.mobile;

import android.annotation.SuppressLint;
import android.app.Activity;
import android.os.Bundle;
import android.os.Build;
import android.provider.Settings;
import android.view.View;
import android.view.WindowManager;
import android.webkit.CookieManager;
import android.webkit.JavascriptInterface;
import android.webkit.WebChromeClient;
import android.webkit.WebResourceRequest;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.webkit.SslErrorHandler;
import android.net.http.SslError;
import android.content.Intent;
import android.net.Uri;
import android.graphics.Color;
import android.graphics.Typeface;
import android.widget.Button;
import android.widget.EditText;
import android.widget.LinearLayout;
import android.widget.TextView;
import android.text.InputType;
import android.webkit.ValueCallback;
import java.net.URI;
import java.security.MessageDigest;
import java.nio.charset.StandardCharsets;
import java.util.Locale;

/** Stateless Android client. No residents, credentials, tokens or PC URL stored on the phone. */
public final class MainActivity extends Activity {
    private WebView web;
    private String trustedOrigin;
    private ValueCallback<Uri[]> filePicker;
    private static final int PICK_FILE = 7441;

    @Override public void onCreate(Bundle state) {
        super.onCreate(state);
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_SECURE);
        CookieManager.getInstance().setAcceptCookie(false);
        showConnect();
    }
    private boolean safeLanUrl(String value) {
        try {
            URI uri=new URI(value);
            if (!"https".equalsIgnoreCase(uri.getScheme()) || uri.getHost()==null || uri.getUserInfo()!=null || uri.getFragment()!=null) return false;
            String h=uri.getHost();
            if (h.matches("\\d+\\.\\d+\\.\\d+\\.\\d+")) {
                String[] s=h.split("\\."); int a=Integer.parseInt(s[0]),b=Integer.parseInt(s[1]);
                return a==10 || a==192&&b==168 || a==172&&b>=16&&b<=31;
            }
            return h.endsWith(".internal") || h.endsWith(".lan") || h.endsWith(".home.arpa");
        } catch(Exception e){return false;}
    }
    private void showConnect() {
        trustedOrigin=null;
        LinearLayout layout=new LinearLayout(this);layout.setOrientation(LinearLayout.VERTICAL);layout.setPadding(36,80,36,20);layout.setBackgroundColor(Color.rgb(246,249,253));
        TextView title=new TextView(this);title.setText("QUẢN LÝ KHU PHỐ");title.setTextSize(22);title.setTypeface(null,Typeface.BOLD);title.setTextColor(Color.rgb(7,35,65));layout.addView(title);
        TextView note=new TextView(this);note.setText("Ứng dụng con – Kết nối an toàn đến PC chính. Điện thoại không lưu hồ sơ, tệp hay thông tin đăng nhập.\n\nPC phải bật chế độ LAN HTTPS và điện thoại phải tin cậy chứng chỉ PC.");note.setPadding(0,16,0,26);note.setTextColor(Color.rgb(52,79,103));layout.addView(note);
        EditText address=new EditText(this);address.setHint("https://192.168.1.10:8765");address.setSingleLine(true);address.setInputType(InputType.TYPE_CLASS_TEXT|InputType.TYPE_TEXT_VARIATION_URI);layout.addView(address);
        Button connect=new Button(this);connect.setText("Kết nối PC chính");layout.addView(connect);
        TextView error=new TextView(this);error.setTextColor(Color.RED);layout.addView(error);
        connect.setOnClickListener(v->{String url=address.getText().toString().trim();if(!safeLanUrl(url)){
            error.setText("Chỉ chấp nhận HTTPS với địa chỉ LAN nội bộ; không kết nối máy chủ Internet.");return;}
            try{URI u=new URI(url);trustedOrigin=u.getScheme()+"://"+u.getAuthority();showWeb(url);}catch(Exception ex){error.setText("Địa chỉ PC không hợp lệ.");}
        });
        setContentView(layout);
    }
    private boolean safeNavigation(String url) {
        try{URI u=new URI(url);return trustedOrigin!=null && (u.getScheme()+"://"+u.getAuthority()).equalsIgnoreCase(trustedOrigin);}catch(Exception e){return false;}
    }
    @SuppressLint("SetJavaScriptEnabled") private void showWeb(String url) {
        web=new WebView(this);web.setBackgroundColor(Color.WHITE);
        web.getSettings().setJavaScriptEnabled(true);
        web.getSettings().setDomStorageEnabled(false);
        web.getSettings().setDatabaseEnabled(false);
        web.getSettings().setAllowFileAccess(false);
        web.getSettings().setAllowContentAccess(true); // SAF picker only, user initiated
        web.getSettings().setCacheMode(android.webkit.WebSettings.LOAD_NO_CACHE);
        web.getSettings().setSaveFormData(false);
        web.getSettings().setSupportMultipleWindows(false);
        if(Build.VERSION.SDK_INT>=26)web.getSettings().setSafeBrowsingEnabled(true);
        web.addJavascriptInterface(new DeviceBridge(),"KPDevice");
        CookieManager.getInstance().setAcceptThirdPartyCookies(web,false);
        web.setWebViewClient(new WebViewClient(){
          @Override public boolean shouldOverrideUrlLoading(WebView view,WebResourceRequest req){return !safeNavigation(req.getUrl().toString());}
          @Override public void onReceivedSslError(WebView view,SslErrorHandler handler,SslError error){handler.cancel();}
        });
        web.setWebChromeClient(new WebChromeClient(){
          @Override public boolean onShowFileChooser(WebView view,ValueCallback<Uri[]> cb,FileChooserParams params){
            if(filePicker!=null)filePicker.onReceiveValue(null);filePicker=cb;
            try { Intent i=params.createIntent(); startActivityForResult(i,PICK_FILE);return true; }
            catch(Exception ex){filePicker=null;return false;}
          }
        });
        setContentView(web);web.loadUrl(url);
    }
    @Override protected void onActivityResult(int requestCode,int resultCode,Intent data){
        super.onActivityResult(requestCode,resultCode,data);
        if(requestCode==PICK_FILE && filePicker!=null){
            filePicker.onReceiveValue(WebChromeClient.FileChooserParams.parseResult(resultCode,data));filePicker=null;
        }
    }
    public final class DeviceBridge {
        @JavascriptInterface public String deviceId(){
            try{
                String raw=Settings.Secure.getString(getContentResolver(),Settings.Secure.ANDROID_ID);
                MessageDigest hash=MessageDigest.getInstance("SHA-256");
                byte[] out=hash.digest(("QuanLyKhuPho/"+raw).getBytes(StandardCharsets.UTF_8));
                StringBuilder s=new StringBuilder("android-");for(byte b:out)s.append(String.format(Locale.ROOT,"%02x",b));return s.toString();
            }catch(Exception e){return "device-unavailable";}
        }
        @JavascriptInterface public String label(){return "Android - "+Build.MANUFACTURER+" "+Build.MODEL;}
    }
    @Override public void onBackPressed(){if(web!=null){web.stopLoading();web.clearHistory();web.clearCache(true);web.destroy();web=null;showConnect();}else super.onBackPressed();}
    @Override protected void onDestroy(){
        if(filePicker!=null){filePicker.onReceiveValue(null);filePicker=null;}
        if(web!=null){web.loadUrl("about:blank");web.clearHistory();web.clearCache(true);web.destroy();web=null;}
        super.onDestroy();
    }
}
