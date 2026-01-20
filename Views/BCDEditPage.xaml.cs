using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BooticeWinUI.Models;
using BooticeWinUI.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace BooticeWinUI.Views
{
    public sealed partial class BcdEditPage : Page
    {
        private readonly BcdService _bcdService;
        private string _currentBcdPath;
        private BcdEntry SelectedEntry;

        public BcdEditPage()
        {
            this.InitializeComponent();
            _bcdService = new BcdService();
            
            // Initial load of System BCD
            Loaded += BcdEditPage_Loaded;
        }

        private async void BcdEditPage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadBcdEntries();
        }

        private async Task LoadBcdEntries()
        {
            try
            {
                var entries = await _bcdService.EnumEntriesAsync(_currentBcdPath);
                BcdEntriesList.ItemsSource = entries;
                StatusInfoBar.Severity = InfoBarSeverity.Success;
                StatusInfoBar.Message = "BCD Loaded Successfully";
                StatusInfoBar.IsOpen = true;
            }
            catch (Exception ex)
            {
                ShowError($"Failed to load BCD: {ex.Message}");
            }
        }

        private void BcdSourceRadio_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (BcdSourceRadio == null || FileSelectionGrid == null) return;

            var selected = BcdSourceRadio.SelectedItem as RadioButton;
            if (selected?.Tag?.ToString() == "System")
            {
                _currentBcdPath = null; // System BCD
                FileSelectionGrid.Visibility = Visibility.Collapsed;
                _ = LoadBcdEntries();
            }
            else
            {
                FileSelectionGrid.Visibility = Visibility.Visible;
                // Don't auto load until file selected
                if (!string.IsNullOrEmpty(BcdFilePathBox.Text))
                {
                    _currentBcdPath = BcdFilePathBox.Text;
                    _ = LoadBcdEntries();
                }
            }
        }

        private async void BrowseBcd_Click(object sender, RoutedEventArgs e)
        {
            var window = (Application.Current as App)?.Window;
            var hWnd = WindowNative.GetWindowHandle(window);

            var openPicker = new FileOpenPicker();
            InitializeWithWindow.Initialize(openPicker, hWnd);
            openPicker.ViewMode = PickerViewMode.List;
            openPicker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
            openPicker.FileTypeFilter.Add("*"); // BCD files have no extension usually, or .dat

            var file = await openPicker.PickSingleFileAsync();
            if (file != null)
            {
                BcdFilePathBox.Text = file.Path;
                _currentBcdPath = file.Path;
                await LoadBcdEntries();
            }
        }

        private void BcdEntriesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedEntry = BcdEntriesList.SelectedItem as BcdEntry;
            
            if (SelectedEntry != null)
            {
                EntryDetailsPanel.Visibility = Visibility.Visible;
                NoSelectionText.Visibility = Visibility.Collapsed;
                // Re-binding happens automatically via x:Bind OneWay/TwoWay
                // But we need to ensure the details panel updates
                Bindings.Update(); 
            }
            else
            {
                EntryDetailsPanel.Visibility = Visibility.Collapsed;
                NoSelectionText.Visibility = Visibility.Visible;
            }
        }

        private async void SaveEntry_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedEntry == null) return;

            try
            {
                // Save modified properties
                // BCD edit requires separate calls for each property
                await _bcdService.SetElementAsync(SelectedEntry.Identifier, "description", SelectedEntry.Description, _currentBcdPath);
                await _bcdService.SetElementAsync(SelectedEntry.Identifier, "device", SelectedEntry.Device, _currentBcdPath);
                await _bcdService.SetElementAsync(SelectedEntry.Identifier, "path", SelectedEntry.Path, _currentBcdPath);
                await _bcdService.SetElementAsync(SelectedEntry.Identifier, "locale", SelectedEntry.Locale, _currentBcdPath);
                
                ShowSuccess("Entry saved successfully.");
                // Refresh list to confirm? Or trust local update.
            }
            catch (Exception ex)
            {
                ShowError($"Failed to save entry: {ex.Message}");
            }
        }

        private void AddEntry_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement Add Entry Dialog
            ShowError("Add Entry not implemented yet.");
        }

        private async void DeleteEntry_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedEntry == null) return;
            
            // TODO: Confirm dialog
            try
            {
                // await _bcdService.DeleteObjectAsync(SelectedEntry.Identifier, _currentBcdPath);
                ShowError("Delete logic pending implementation in Service.");
            }
            catch (Exception ex)
            {
                ShowError($"Failed to delete: {ex.Message}");
            }
        }

        private void MoveUp_Click(object sender, RoutedEventArgs e) { /* TODO */ }
        private void MoveDown_Click(object sender, RoutedEventArgs e) { /* TODO */ }

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
