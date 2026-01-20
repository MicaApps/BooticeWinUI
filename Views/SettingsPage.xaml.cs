using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace BooticeWinUI.Views
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            this.InitializeComponent();
            LoadCurrentTheme();
            LoadVersion();
            
            // Listen for size changes to update layout
            this.SizeChanged += SettingsPage_SizeChanged;
        }

        private void SettingsPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width >= 850)
            {
                // Wide State
                MainLayoutGrid.ColumnDefinitions[1].Width = new GridLength(300);
                MainLayoutGrid.ColumnDefinitions[1].MinWidth = 300;
                
                Grid.SetRow(AboutPanel, 0);
                Grid.SetColumn(AboutPanel, 1);
                AboutPanel.Margin = new Thickness(40, 0, 0, 0);
                
                SeparatorLine.Visibility = Visibility.Collapsed;
            }
            else
            {
                // Narrow State
                MainLayoutGrid.ColumnDefinitions[1].Width = new GridLength(0);
                MainLayoutGrid.ColumnDefinitions[1].MinWidth = 0;
                
                Grid.SetRow(AboutPanel, 1);
                Grid.SetColumn(AboutPanel, 0);
                AboutPanel.Margin = new Thickness(0, 40, 0, 0);
                
                SeparatorLine.Visibility = Visibility.Visible;
            }
        }

        private void LoadCurrentTheme()
        {
            var currentTheme = ((MainWindow)((App)Application.Current).Window).Content is FrameworkElement root ? root.RequestedTheme : ElementTheme.Default;
            
            string themeTag = currentTheme.ToString(); // "Light", "Dark", "Default"
            
            foreach (ComboBoxItem item in ThemeComboBox.Items)
            {
                if (item.Tag.ToString() == themeTag)
                {
                    ThemeComboBox.SelectedItem = item;
                    break;
                }
            }

            if (ThemeComboBox.SelectedItem == null)
            {
                ThemeComboBox.SelectedIndex = 2; 
            }
        }

        private void LoadVersion()
        {
            try
            {
                var version = Windows.ApplicationModel.Package.Current.Id.Version;
                VersionText.Text = $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
            }
            catch
            {
                // Fallback for unpackaged app
                var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                VersionText.Text = $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
            }
        }

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                if (Enum.TryParse(typeof(ElementTheme), selectedItem.Tag.ToString(), out object theme))
                {
                    // Update the theme for the main window content
                    if (((App)Application.Current).Window is MainWindow mainWindow && 
                        mainWindow.Content is FrameworkElement rootElement)
                    {
                        rootElement.RequestedTheme = (ElementTheme)theme;
                    }
                }
            }
        }
    }
}