using System;using System.Collections.Generic;using System.Windows;using System.Windows.Media;
namespace PCMonitor {
 public static class Appearance {
  public static void Apply(FrameworkElement root,bool light){
   root.Resources["PageBg"]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(light?"#F2F5F8":"#0B1017"));
   root.Resources["CardBg"]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(light?"#FFFFFF":"#141D29"));
   root.Resources["CardStroke"]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(light?"#D9E1EB":"#243348"));
   root.Resources["Muted"]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(light?"#526579":"#9CAFC5"));
   root.Resources["Accent"]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(light?"#087A60":"#58E0BB"));
   root.Resources["PrimaryText"]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(light?"#18283B":"#F0F6FD"));
   root.Resources["SecondaryText"]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(light?"#40566D":"#BCCBDC"));
   root.Resources["AltRowBg"]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(light?"#F4F7FB":"#101822"));
   root.Resources["TableHeaderBg"]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(light?"#EBF0F6":"#182435"));
   root.Resources["Separator"]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(light?"#D9E1EB":"#213044"));
   root.Resources["IconBg"]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(light?"#D9F3E9":"#173C38"));
   root.Resources["StatusBg"]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(light?"#DEF3E9":"#15352F"));
   root.Resources["OnAccent"]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(light?"#FFFFFF":"#08251E"));
   root.Resources["RamColor"]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(light?"#2864B4":"#74ABFF"));
   root.Resources["GpuColor"]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(light?"#96600E":"#FBC777"));
   root.Resources["DiskColor"]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(light?"#7B4BBC":"#BD9BFF"));
   root.Resources["FooterBg"]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(light?"#E8EEF5":"#0E1621"));
   root.Resources["GridLine"]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(light?"#DFE6EF":"#263548"));
   root.Resources["ButtonBg"]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(light?"#FFFFFF":"#1B293A"));
   root.Resources["ButtonHover"]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(light?"#E8F0F6":"#293D53"));
   root.Resources["ButtonPressed"]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(light?"#D8E6F0":"#324B65"));
   root.Resources["SelectionBg"]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(light?"#D6EFE6":"#254C4B"));
   root.Resources["AccentHover"]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(light?"#096A55":"#78EACB"));
   root.Resources["AccentPressed"]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(light?"#06523F":"#3AC9A3"));
   root.Resources["WarningText"]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(light?"#8A4F08":"#FBC777"));
  }
 }
}
