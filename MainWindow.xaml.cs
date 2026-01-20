using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using BooticeWinUI.Views;

namespace BooticeWinUI
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar); 

            try
            {
                var resourceLoader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
                var title = resourceLoader.GetString("AppTitle/Title");
                if (!string.IsNullOrEmpty(title))
                {
                    this.Title = title;
                }
            }
            catch { }
        }

        private void NavView_Loaded(object sender, RoutedEventArgs e)
        {
            // Select the first item by default
            NavView.SelectedItem = NavView.MenuItems[0];
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                ContentFrame.Navigate(typeof(SettingsPage));
            }
            else
            {
                var selectedItem = (NavigationViewItem)args.SelectedItem;
                string pageTag = selectedItem.Tag.ToString();

                switch (pageTag)
                {
                    case "PhysicalDisk":
                        ContentFrame.Navigate(typeof(PhysicalDiskPage));
                        break;
                    case "DiskImage":
                        ContentFrame.Navigate(typeof(DiskImagePage));
                        break;
                    case "BCDEdit":
                         ContentFrame.Navigate(typeof(BcdEditPage));
                        break;
                    case "UEFI":
                         ContentFrame.Navigate(typeof(UefiPage));
                        break;
                    case "Utilities":
                         ContentFrame.Navigate(typeof(UtilitiesPage));
                        break;
                }
            }
        }
    }
}
