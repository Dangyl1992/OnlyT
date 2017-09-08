﻿using System.Windows;
using Microsoft.Practices.ServiceLocation;
using OnlyT.Services.Options;
using OnlyT.Utils;
using OnlyT.ViewModel;

namespace OnlyT.Windows
{
   /// <summary>
   /// Interaction logic for MainWindow.xaml
   /// </summary>
   public partial class MainWindow : Window
   {
      public MainWindow()
      {
         InitializeComponent();
      }

      protected override void OnSourceInitialized(System.EventArgs e)
      {
         var optionsService = ServiceLocator.Current.GetInstance<IOptionsService>();
         if (!string.IsNullOrEmpty(optionsService.Options.AppWindowPlacement))
         {
            this.SetPlacement(optionsService.Options.AppWindowPlacement);
         }
      }

      private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
      {
         SaveWindowPos();
         MainViewModel m = (MainViewModel)DataContext;
         m.Closing(sender, e);
      }

      private void SaveWindowPos()
      {
         var optionsService = ServiceLocator.Current.GetInstance<IOptionsService>();
         optionsService.Options.AppWindowPlacement = this.GetPlacement();
      }

   }
}
