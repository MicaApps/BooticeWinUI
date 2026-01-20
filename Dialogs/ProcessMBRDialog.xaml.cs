using System;
using System.IO;
using BooticeWinUI.Helpers;
using BooticeWinUI.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace BooticeWinUI.Dialogs
{
    public sealed partial class ProcessMBRDialog : ContentDialog
    {
        private readonly DiskService _diskService;
        private readonly int _diskIndex;
        
        public ProcessMBRDialog(DiskService diskService, int diskIndex)
        {
            this.InitializeComponent();
            _diskService = diskService;
            _diskIndex = diskIndex;
            
            // Handle radio button changes to toggle UI if needed
            BackupRadio.Checked += (s, e) => UpdateUI();
            RestoreRadio.Checked += (s, e) => UpdateUI();
            WindowsNTMBRRadio.Checked += (s, e) => UpdateUI();
            
            DetectMbrType();
        }

        private void DetectMbrType()
        {
            try
            {
                byte[] mbr = _diskService.ReadSector(_diskIndex, 0);
                
                // Simple detection logic: check signature or specific bytes
                // Windows NT 6.x MBR usually starts with 33 C0 8E D0 ...
                // Let's check the first 4 bytes
                if (mbr[0] == 0x33 && mbr[1] == 0xC0 && mbr[2] == 0x8E && mbr[3] == 0xD0)
                {
                    CurrentMBRText.Text = "Current MBR Type: Windows NT 6.x MBR";
                }
                else if (mbr[0] == 0xFA && mbr[1] == 0x33 && mbr[2] == 0xC0) // Some old MBRs start with CLI
                {
                     CurrentMBRText.Text = "Current MBR Type: Unknown / Old Standard";
                }
                else
                {
                    CurrentMBRText.Text = "Current MBR Type: Unknown";
                }
            }
            catch
            {
                CurrentMBRText.Text = "Current MBR Type: Read Error";
            }
        }

        private void UpdateUI()
        {
            ResultInfoBar.IsOpen = false;
            
            bool isFileOp = BackupRadio.IsChecked == true || RestoreRadio.IsChecked == true;
            FileSection.Visibility = isFileOp ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void BrowseFile_Click(object sender, RoutedEventArgs e)
        {
            var window = (Application.Current as App)?.Window;
            var hWnd = WindowNative.GetWindowHandle(window);

            if (BackupRadio.IsChecked == true)
            {
                var savePicker = new FileSavePicker();
                InitializeWithWindow.Initialize(savePicker, hWnd);
                savePicker.SuggestedStartLocation = PickerLocationId.Desktop;
                savePicker.FileTypeChoices.Add("Binary File", new[] { ".bin" });
                savePicker.SuggestedFileName = $"MBR_Disk{_diskIndex}.bin";

                var file = await savePicker.PickSaveFileAsync();
                if (file != null)
                {
                    FilePathBox.Text = file.Path;
                }
            }
            else if (RestoreRadio.IsChecked == true)
            {
                var openPicker = new FileOpenPicker();
                InitializeWithWindow.Initialize(openPicker, hWnd);
                openPicker.ViewMode = PickerViewMode.List;
                openPicker.SuggestedStartLocation = PickerLocationId.Desktop;
                openPicker.FileTypeFilter.Add(".bin");
                openPicker.FileTypeFilter.Add("*");

                var file = await openPicker.PickSingleFileAsync();
                if (file != null)
                {
                    FilePathBox.Text = file.Path;
                }
            }
        }

        private void ExecuteBtn_Click(object sender, RoutedEventArgs e)
        {
            ResultInfoBar.IsOpen = false;

            try
            {
                if (BackupRadio.IsChecked == true)
                {
                    string filePath = FilePathBox.Text;
                    if (string.IsNullOrWhiteSpace(filePath))
                    {
                        ShowError("Please select a file path.");
                        return;
                    }
                    
                    // Backup MBR (Sector 0, 512 bytes)
                    byte[] mbrData = _diskService.ReadSector(_diskIndex, 0, 1);
                    File.WriteAllBytes(filePath, mbrData);
                    ShowSuccess($"Successfully backed up MBR to {filePath}");
                }
                else if (RestoreRadio.IsChecked == true)
                {
                    string filePath = FilePathBox.Text;
                    if (string.IsNullOrWhiteSpace(filePath))
                    {
                        ShowError("Please select a file path.");
                        return;
                    }

                    if (!File.Exists(filePath))
                    {
                        ShowError("File does not exist.");
                        return;
                    }

                    byte[] data = File.ReadAllBytes(filePath);
                    if (data.Length != 512)
                    {
                        ShowError($"Invalid file size ({data.Length} bytes). MBR must be exactly 512 bytes.");
                        return;
                    }

                    // Write to Sector 0
                    _diskService.WriteSector(_diskIndex, 0, data);
                    ShowSuccess("Successfully restored MBR from file.");
                }
                else if (WindowsNTMBRRadio.IsChecked == true)
                {
                    // Install Windows NT 6.x MBR
                    _diskService.WriteMbrCode(_diskIndex, MbrTemplates.WindowsNT6_MBR);
                    ShowSuccess("Successfully installed Windows NT 6.x MBR.");
                    DetectMbrType(); // Refresh detection
                }
            }
            catch (Exception ex)
            {
                ShowError($"Operation failed: {ex.Message}");
            }
        }

        private void ShowError(string message)
        {
            ResultInfoBar.Severity = InfoBarSeverity.Error;
            ResultInfoBar.Title = "Error";
            ResultInfoBar.Message = message;
            ResultInfoBar.IsOpen = true;
        }

        private void ShowSuccess(string message)
        {
            ResultInfoBar.Severity = InfoBarSeverity.Success;
            ResultInfoBar.Title = "Success";
            ResultInfoBar.Message = message;
            ResultInfoBar.IsOpen = true;
        }
    }
}
