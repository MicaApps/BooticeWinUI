using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BooticeWinUI.Models;
using BooticeWinUI.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BooticeWinUI.Views
{
    public sealed partial class UefiPage : Page
    {
        private readonly UefiService _uefiService;
        private UefiEntry SelectedEntry;
        private List<UefiEntry> _allEntries;

        public UefiPage()
        {
            this.InitializeComponent();
            _uefiService = new UefiService();
            Loaded += UefiPage_Loaded;
        }

        private async void UefiPage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadEntries();
        }

        private async Task LoadEntries()
        {
            try
            {
                _allEntries = await _uefiService.EnumFirmwareEntriesAsync();
                
                // Filter out non-application entries for the list if needed
                // For now, show all except {fwbootmgr} itself if possible
                var displayList = _allEntries.Where(x => x.Identifier != "{fwbootmgr}").ToList();
                
                UefiEntriesList.ItemsSource = displayList;
                
                StatusInfoBar.Severity = InfoBarSeverity.Success;
                StatusInfoBar.Message = $"Loaded {displayList.Count} UEFI entries.";
                StatusInfoBar.IsOpen = true;
            }
            catch (Exception ex)
            {
                ShowError($"Failed to load UEFI entries: {ex.Message}");
            }
        }

        private void UefiEntriesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedEntry = UefiEntriesList.SelectedItem as UefiEntry;
            
            if (SelectedEntry != null)
            {
                EntryDetailsPanel.Visibility = Visibility.Visible;
                NoSelectionText.Visibility = Visibility.Collapsed;
                Bindings.Update();
            }
            else
            {
                EntryDetailsPanel.Visibility = Visibility.Collapsed;
                NoSelectionText.Visibility = Visibility.Visible;
            }
        }

        private async void MoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedEntry == null) return;
            
            var list = UefiEntriesList.ItemsSource as List<UefiEntry>;
            if (list == null) return;
            
            int index = list.IndexOf(SelectedEntry);
            if (index > 0)
            {
                list.RemoveAt(index);
                list.Insert(index - 1, SelectedEntry);
                UefiEntriesList.ItemsSource = null;
                UefiEntriesList.ItemsSource = list;
                UefiEntriesList.SelectedItem = SelectedEntry;
                
                await SaveBootOrder(list);
            }
        }

        private async void MoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedEntry == null) return;
            
            var list = UefiEntriesList.ItemsSource as List<UefiEntry>;
            if (list == null) return;
            
            int index = list.IndexOf(SelectedEntry);
            if (index < list.Count - 1)
            {
                list.RemoveAt(index);
                list.Insert(index + 1, SelectedEntry);
                UefiEntriesList.ItemsSource = null;
                UefiEntriesList.ItemsSource = list;
                UefiEntriesList.SelectedItem = SelectedEntry;
                
                await SaveBootOrder(list);
            }
        }

        private async void SetTop_Click(object sender, RoutedEventArgs e)
        {
             if (SelectedEntry == null) return;
             
             try
             {
                 await _uefiService.SetTopAsync(SelectedEntry.Identifier);
                 await LoadEntries(); // Reload to reflect real state
                 ShowSuccess("Entry moved to top of boot order.");
             }
             catch (Exception ex)
             {
                 ShowError($"Failed to move entry: {ex.Message}");
             }
        }

        private async Task SaveBootOrder(List<UefiEntry> orderedList)
        {
            try
            {
                var ids = orderedList.Select(x => x.Identifier).ToList();
                await _uefiService.SetBootOrderAsync(ids);
                // Don't show success message for every move, it's distracting
            }
            catch (Exception ex)
            {
                ShowError($"Failed to save boot order: {ex.Message}");
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
