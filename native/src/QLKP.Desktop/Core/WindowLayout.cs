using System;
using System.Windows;
namespace QLKP.Desktop.Core {
 public static class WindowLayout {
  // WPF work-area values are device-independent units, including the current desktop scale.
  public static void FitInitialSize(Window window) {
   var work=SystemParameters.WorkArea;
   double width=Math.Max(320,work.Width-24),height=Math.Max(320,work.Height-24);
   window.MinWidth=Math.Min(window.MinWidth,width);window.MinHeight=Math.Min(window.MinHeight,height);
   if(!Double.IsNaN(window.Width))window.Width=Math.Min(window.Width,width);
   if(!Double.IsNaN(window.Height))window.Height=Math.Min(window.Height,height);
  }
 }
}
