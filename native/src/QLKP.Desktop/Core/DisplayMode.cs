using System;
using System.IO;
using System.Security;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Microsoft.Win32;

namespace QLKP.Desktop.Core {
 internal static class DisplayMode {
  const string Preferences=@"Software\QLKP\Appearance";
  internal static bool Depth {get;private set;}
  internal static bool Persist {get;set;}=true;

  internal static void LoadPreference() {
   bool depth=false;
   try {
    using(var key=Registry.CurrentUser.OpenSubKey(Preferences))
     depth=Object.Equals(key?.GetValue("Depth"),1);
   } catch(Exception ex) when(ex is IOException||ex is UnauthorizedAccessException||ex is SecurityException) {}
   Apply(depth);
  }

  internal static void Apply(bool depth) {
   Depth=depth;
   var resources=Application.Current.Resources;
   resources["ThemeHeader"]=depth?Gradient("#264E80","#0C2347"):Brush("#102D59");
   resources["ThemeWorkspace"]=depth?Gradient("#F3F8FF","#DFEAF8"):Brush("#F3F7FC");
   resources["ThemeAction"]=depth?Gradient("#2B78E4","#1252AC"):Brush("#1263D6");
   resources["ThemeOutline"]=Brush(depth?"#BCCFE5":"#D5E0ED");
   if(depth) {
    var effect=new DropShadowEffect {BlurRadius=18,ShadowDepth=3,Opacity=0.24,Color=Color.FromRgb(26,58,95)};
    effect.Freeze();resources["ThemeCardEffect"]=effect;
   } else {
    resources.Remove("ThemeCardEffect");
   }
  }

  internal static void SavePreference() {
   if(!Persist)return;
   try {
    using(var key=Registry.CurrentUser.CreateSubKey(Preferences))
     key?.SetValue("Depth",Depth?1:0,RegistryValueKind.DWord);
   } catch(Exception ex) when(ex is IOException||ex is UnauthorizedAccessException||ex is SecurityException) {}
  }

  static Brush Brush(string hex) {
   var brush=new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
   brush.Freeze();return brush;
  }

  static Brush Gradient(string light,string dark) {
   var brush=new LinearGradientBrush((Color)ColorConverter.ConvertFromString(light),(Color)ColorConverter.ConvertFromString(dark),90);
   brush.Freeze();return brush;
  }
 }
}
