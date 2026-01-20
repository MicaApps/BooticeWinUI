using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace BooticeWinUI.Views
{
    public sealed partial class UtilitiesPage : Page
    {
        private string _currentFilePath;

        public UtilitiesPage()
        {
            this.InitializeComponent();
            
            // Default GRUB4DOS Template
            GrubEditor.Text = "# GRUB4DOS Menu Template\n\ntimeout 30\ndefault 0\n\ntitle Windows NT 6.x\nfind --set-root /bootmgr\nchainloader /bootmgr\n\ntitle Windows NT 5.x\nfind --set-root /ntldr\nchainloader /ntldr\n\ntitle Reboot\nreboot\n\ntitle Halt\nhalt";
        }

        private async void GrubOpen_Click(object sender, RoutedEventArgs e)
        {
            var window = (Application.Current as App)?.Window;
            var hWnd = WindowNative.GetWindowHandle(window);

            var openPicker = new FileOpenPicker();
            InitializeWithWindow.Initialize(openPicker, hWnd);
            openPicker.ViewMode = PickerViewMode.List;
            openPicker.SuggestedStartLocation = PickerLocationId.Desktop;
            openPicker.FileTypeFilter.Add(".lst");
            openPicker.FileTypeFilter.Add(".txt");
            openPicker.FileTypeFilter.Add("*");

            var file = await openPicker.PickSingleFileAsync();
            if (file != null)
            {
                try
                {
                    _currentFilePath = file.Path;
                    // Detect encoding or use selected?
                    // Simple logic: Try UTF8, fallback to Default (ANSI)
                    // But user might select encoding in Combo.
                    
                    Encoding encoding = GetSelectedEncoding();
                    string content = await File.ReadAllTextAsync(_currentFilePath, encoding);
                    GrubEditor.Text = content;
                    GrubStatusText.Text = $"Loaded: {_currentFilePath}";
                }
                catch (Exception ex)
                {
                    ShowError($"Failed to open file: {ex.Message}");
                }
            }
        }

        private async void GrubSave_Click(object sender, RoutedEventArgs e)
        {
            var window = (Application.Current as App)?.Window;
            var hWnd = WindowNative.GetWindowHandle(window);

            if (string.IsNullOrEmpty(_currentFilePath))
            {
                var savePicker = new FileSavePicker();
                InitializeWithWindow.Initialize(savePicker, hWnd);
                savePicker.SuggestedStartLocation = PickerLocationId.Desktop;
                savePicker.FileTypeChoices.Add("GRUB4DOS Menu", new[] { ".lst" });
                savePicker.SuggestedFileName = "menu.lst";

                var file = await savePicker.PickSaveFileAsync();
                if (file != null)
                {
                    _currentFilePath = file.Path;
                }
                else
                {
                    return; // User cancelled
                }
            }

            try
            {
                Encoding encoding = GetSelectedEncoding();
                await File.WriteAllTextAsync(_currentFilePath, GrubEditor.Text, encoding);
                ShowSuccess($"Saved to {_currentFilePath}");
                GrubStatusText.Text = $"Saved: {_currentFilePath}";
            }
            catch (Exception ex)
            {
                ShowError($"Failed to save file: {ex.Message}");
            }
        }

        private Encoding GetSelectedEncoding()
        {
            var selected = EncodingCombo.SelectedItem as ComboBoxItem;
            if (selected?.Tag?.ToString() == "gbk")
            {
                // GBK / ANSI
                // CodePage 936 for GBK
                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
                return Encoding.GetEncoding(936);
            }
            else
            {
                return Encoding.UTF8;
            }
        }

        private void OpenFillDialog_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Open Sector Fill Dialog
            ShowError("Sector Fill utility is under construction.");
        }

        private void ShowError(string message)
        {
            StatusInfoBar.Severity = InfoBarSeverity.Error;
            StatusInfoBar.Title = "Error";
            StatusInfoBar.Message = message;
            StatusInfoBar.IsOpen = true;
        }

        private void ShowSuccess(string message)
        {
            StatusInfoBar.Severity = InfoBarSeverity.Success;
            StatusInfoBar.Title = "Success";
            StatusInfoBar.Message = message;
            StatusInfoBar.IsOpen = true;
        }
    }
}
