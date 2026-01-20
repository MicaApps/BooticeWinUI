using System;
using BooticeWinUI.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace BooticeWinUI.Views
{
    public sealed partial class DiskImagePage : Page
    {
        private readonly VhdService _vhdService;

        public DiskImagePage()
        {
            this.InitializeComponent();
            _vhdService = new VhdService();
        }

        private async void BrowseVhd_Click(object sender, RoutedEventArgs e)
        {
            var window = (Application.Current as App)?.Window;
            var hWnd = WindowNative.GetWindowHandle(window);

            var openPicker = new FileOpenPicker();
            InitializeWithWindow.Initialize(openPicker, hWnd);
            openPicker.ViewMode = PickerViewMode.List;
            openPicker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
            openPicker.FileTypeFilter.Add(".vhd");
            openPicker.FileTypeFilter.Add(".vhdx");
            openPicker.FileTypeFilter.Add("*");

            var file = await openPicker.PickSingleFileAsync();
            if (file != null)
            {
                VhdPathBox.Text = file.Path;
            }
        }

        private async void Attach_Click(object sender, RoutedEventArgs e)
        {
            string path = VhdPathBox.Text;
            if (string.IsNullOrWhiteSpace(path))
            {
                ShowError("Please select a VHD file first.");
                return;
            }

            try
            {
                bool readOnly = ReadOnlyCheck.IsChecked == true;
                AttachBtn.IsEnabled = false;
                
                await _vhdService.AttachVhdAsync(path, readOnly);
                ShowSuccess($"Successfully attached: {path}");
            }
            catch (Exception ex)
            {
                ShowError($"Failed to attach: {ex.Message}");
            }
            finally
            {
                AttachBtn.IsEnabled = true;
            }
        }

        private async void Detach_Click(object sender, RoutedEventArgs e)
        {
            string path = VhdPathBox.Text;
            if (string.IsNullOrWhiteSpace(path))
            {
                ShowError("Please select a VHD file first.");
                return;
            }

            try
            {
                DetachBtn.IsEnabled = false;
                await _vhdService.DetachVhdAsync(path);
                ShowSuccess($"Successfully detached: {path}");
            }
            catch (Exception ex)
            {
                ShowError($"Failed to detach: {ex.Message}");
            }
            finally
            {
                DetachBtn.IsEnabled = true;
            }
        }

        private async void Create_Click(object sender, RoutedEventArgs e)
        {
            // Gather params
            var sizeVal = SizeBox.Value;
            bool isGb = SizeUnitCombo.SelectedIndex == 1;
            long sizeMb = (long)(sizeVal * (isGb ? 1024 : 1));
            
            bool isVhdx = FormatCombo.SelectedIndex == 1;
            bool isFixed = TypeCombo.SelectedIndex == 0;
            
            // Save Dialog
            var window = (Application.Current as App)?.Window;
            var hWnd = WindowNative.GetWindowHandle(window);
            
            var savePicker = new FileSavePicker();
            InitializeWithWindow.Initialize(savePicker, hWnd);
            savePicker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
            
            if (isVhdx)
            {
                savePicker.FileTypeChoices.Add("VHDX Disk Image", new[] { ".vhdx" });
                savePicker.SuggestedFileName = "NewDisk.vhdx";
            }
            else
            {
                savePicker.FileTypeChoices.Add("VHD Disk Image", new[] { ".vhd" });
                savePicker.SuggestedFileName = "NewDisk.vhd";
            }

            var file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                try
                {
                    StatusInfoBar.Severity = InfoBarSeverity.Informational;
                    StatusInfoBar.Title = "Creating...";
                    StatusInfoBar.Message = "Please wait, creating virtual disk...";
                    StatusInfoBar.IsOpen = true;
                    
                    await _vhdService.CreateVhdAsync(file.Path, sizeMb, isVhdx, isFixed);
                    
                    ShowSuccess($"Successfully created: {file.Path}");
                    VhdPathBox.Text = file.Path; // Auto select newly created file
                }
                catch (Exception ex)
                {
                    ShowError($"Failed to create VHD: {ex.Message}");
                }
            }
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
