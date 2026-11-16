using System;
using System.Windows;
using System.Windows.Controls;
namespace QLKP.Desktop.Core {
 public static class WindowLayout {
  public static void ArrangeMasterDetail(Grid layout,FrameworkElement detail,bool stacked,double listWeight,double detailWeight) {
   layout.ColumnDefinitions[0].Width=new GridLength(stacked?1:listWeight,GridUnitType.Star);
   layout.ColumnDefinitions[1].Width=stacked?new GridLength(0):new GridLength(detailWeight,GridUnitType.Star);
   layout.RowDefinitions[0].Height=new GridLength(stacked?4:1,GridUnitType.Star);
   layout.RowDefinitions[1].Height=stacked?new GridLength(5,GridUnitType.Star):new GridLength(0);
   Grid.SetRow(detail,stacked?1:0);
   Grid.SetColumn(detail,stacked?0:1);
  }
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
