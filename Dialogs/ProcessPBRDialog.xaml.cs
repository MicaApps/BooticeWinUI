using System;
using System.IO;
using System.Linq;
using BooticeWinUI.Helpers;
using BooticeWinUI.Models;
using BooticeWinUI.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace BooticeWinUI.Dialogs
{
    public sealed partial class ProcessPBRDialog : ContentDialog
    {
        private readonly DiskService _diskService;
        private readonly int _diskIndex;
        
        public ProcessPBRDialog(DiskService diskService, int diskIndex)
        {
            this.InitializeComponent();
            _diskService = diskService;
            _diskIndex = diskIndex;
            
            LoadPartitions();
            
            BackupRadio.Checked += (s, e) => UpdateUI();
            RestoreRadio.Checked += (s, e) => UpdateUI();
            InstallBootmgrRadio.Checked += (s, e) => UpdateUI();
            
            // Detect PBR type for initial selection
            PartitionCombo.SelectionChanged += (s, e) => DetectPbrType();
        }

        private void DetectPbrType()
        {
            var selectedPart = PartitionCombo.SelectedItem as PartitionEntry;
            if (selectedPart == null) return;

            try
            {
                byte[] pbr = _diskService.ReadSector(_diskIndex, (long)selectedPart.StartLba);
                
                // Simple detection
                string pbrText = "Unknown";
                
                // BOOTMGR usually has "BOOTMGR" string at 0x1B0 or similar
                // NTLDR usually has "NTLDR" string
                // FAT32 usually has "MSDOS5.0" at 0x03
                
                string oem = System.Text.Encoding.ASCII.GetString(pbr, 0x03, 8);
                
                // Search for BOOTMGR string in code area
                bool hasBootmgr = false;
                for(int i=0; i<512-7; i++)
                {
                    if(pbr[i] == 'B' && pbr[i+1] == 'O' && pbr[i+2] == 'O' && pbr[i+3] == 'T' && pbr[i+4] == 'M' && pbr[i+5] == 'G' && pbr[i+6] == 'R')
                    {
                        hasBootmgr = true;
                        break;
                    }
                }
                
                if (hasBootmgr) pbrText = "BOOTMGR (Windows Vista/7/8/10/11)";
                else if (oem.StartsWith("MSDOS")) pbrText = "MS-DOS / Windows 9x (NTLDR compatible)";
                else if (oem.StartsWith("NTFS")) pbrText = "NTFS (Version determined by OS)";
                else pbrText = $"Unknown (OEM: {oem.Trim()})";
                
                CurrentPBRText.Text = $"Current PBR Type: {pbrText}";
            }
            catch
            {
                CurrentPBRText.Text = "Current PBR Type: Read Error";
            }
        }

        private void LoadPartitions()
        {
            try
            {
                var partitions = _diskService.GetPartitions(_diskIndex);
                PartitionCombo.ItemsSource = partitions;
                if (partitions.Count > 0)
                {
                    PartitionCombo.SelectedIndex = 0;
                    DetectPbrType();
                }
            }
            catch (Exception ex)
            {
                ShowError($"Failed to load partitions: {ex.Message}");
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
            var selectedPart = PartitionCombo.SelectedItem as PartitionEntry;
            string partSuffix = selectedPart != null ? $"_Part{selectedPart.Index}" : "";

            if (BackupRadio.IsChecked == true)
            {
                var savePicker = new FileSavePicker();
                InitializeWithWindow.Initialize(savePicker, hWnd);
                savePicker.SuggestedStartLocation = PickerLocationId.Desktop;
                savePicker.FileTypeChoices.Add("Binary File", new[] { ".bin" });
                savePicker.SuggestedFileName = $"PBR_Disk{_diskIndex}{partSuffix}.bin";

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
            var selectedPart = PartitionCombo.SelectedItem as PartitionEntry;

            if (selectedPart == null)
            {
                ShowError("Please select a target partition.");
                return;
            }

            try
            {
                long pbrSector = (long)selectedPart.StartLba;
                
                if (BackupRadio.IsChecked == true)
                {
                    string filePath = FilePathBox.Text;
                    if (string.IsNullOrWhiteSpace(filePath))
                    {
                        ShowError("Please select a file path.");
                        return;
                    }
                    
                    byte[] pbrData = _diskService.ReadSector(_diskIndex, pbrSector, 1);
                    File.WriteAllBytes(filePath, pbrData);
                    ShowSuccess($"Successfully backed up PBR to {filePath}");
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
                         ShowError($"Invalid file size ({data.Length} bytes). PBR must be exactly 512 bytes.");
                         return;
                    }

                    _diskService.WriteSector(_diskIndex, pbrSector, data);
                    ShowSuccess("Successfully restored PBR from file.");
                }
                else if (InstallBootmgrRadio.IsChecked == true)
                {
                    // Install BOOTMGR (FAT32) - For NTFS we need different template, but let's assume FAT32 for simple USB scenario
                    // or better, check FS.
                    // The template we added is for FAT32.
                    
                    if (selectedPart.FileSystemDesc.Contains("FAT32") || selectedPart.FileSystemType == 0x0B || selectedPart.FileSystemType == 0x0C)
                    {
                        _diskService.WritePbrCode(_diskIndex, selectedPart, PbrTemplates.FAT32_BOOTMGR);
                        ShowSuccess("Successfully installed BOOTMGR PBR (FAT32).");
                        DetectPbrType();
                    }
                    else
                    {
                        ShowError("Currently only FAT32 partitions are supported for BOOTMGR installation in this version.");
                    }
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
