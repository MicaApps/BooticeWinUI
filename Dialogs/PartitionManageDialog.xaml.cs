using System;
using System.Collections.Generic;
using System.Linq;
using BooticeWinUI.Models;
using BooticeWinUI.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BooticeWinUI.Dialogs
{
    public sealed partial class PartitionManageDialog : ContentDialog
    {
        private readonly DiskService _diskService;
        private readonly int _diskIndex;
        private List<PartitionEntry> _partitions;
        private PartitionEntry _selectedPartition;

        public PartitionManageDialog(DiskService diskService, int diskIndex)
        {
            this.InitializeComponent();
            _diskService = diskService;
            _diskIndex = diskIndex;
            LoadPartitions();
            
            PartitionsList.SelectionChanged += PartitionsList_SelectionChanged;
        }

        private void LoadPartitions()
        {
            try
            {
                // Auto-detects MBR/GPT internally now
                _partitions = _diskService.GetPartitions(_diskIndex);
                PartitionsList.ItemsSource = null; // Force refresh
                PartitionsList.ItemsSource = _partitions;
                
                if (_partitions.Count > 0 && _partitions[0].IsGpt)
                {
                    Title = "Partition Management (GPT)";
                }
                else
                {
                    Title = "Partition Management (MBR)";
                }
                
                UpdateButtonsState();
            }
            catch (Exception ex)
            {
                ShowError($"Failed to load partitions: {ex.Message}");
            }
        }

        private void PartitionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedPartition = PartitionsList.SelectedItem as PartitionEntry;
            UpdateButtonsState();
        }

        private void UpdateButtonsState()
        {
            bool hasSelection = _selectedPartition != null;
            bool isGpt = _partitions != null && _partitions.Count > 0 && _partitions[0].IsGpt;

            ActivateBtn.IsEnabled = hasSelection && !isGpt; // GPT doesn't use legacy Active flag
            HideBtn.IsEnabled = hasSelection;
            UnhideBtn.IsEnabled = hasSelection;
            DeleteBtn.IsEnabled = hasSelection;
            // SaveBtn.IsEnabled = !isGpt; // Now enabled for both (implicit)

            if (hasSelection)
            {
                // Update button text contextually if needed
                if (_selectedPartition.IsActive) ActivateBtn.Content = "Deactivate";
                else ActivateBtn.Content = "Activate";
                
                // MBR Hidden types usually add 0x10 or change specific IDs
                // Simple check for now
                bool isHidden = IsHiddenType(_selectedPartition.FileSystemType);
                HideBtn.IsEnabled = !isHidden;
                UnhideBtn.IsEnabled = isHidden;
            }
        }
        
        private bool IsHiddenType(byte type)
        {
            // Common hidden types
            return type == 0x11 || type == 0x12 || type == 0x14 || type == 0x16 || type == 0x17 || type == 0x1B || type == 0x1C || type == 0x1E;
        }

        private void ActivateBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPartition == null) return;
            
            // Toggle Active state
            // Ensure only one active partition for MBR
            if (!_selectedPartition.IsActive)
            {
                foreach (var part in _partitions) part.IsActive = false;
                _selectedPartition.IsActive = true;
            }
            else
            {
                _selectedPartition.IsActive = false;
            }
            
            RefreshList();
        }

        private void HideBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPartition == null) return;
            
            // Simple logic: If type < 0x10, add 0x10? Or specific mapping?
            // Windows usually uses specific hidden IDs.
            // For FAT16 (0x06) -> 0x16. For NTFS (0x07) -> 0x17.
            // Let's implement a simple mapping for common types.
            
            byte type = _selectedPartition.FileSystemType;
            byte newType = type;
            
            if (type == 0x01) newType = 0x11;
            else if (type == 0x04) newType = 0x14;
            else if (type == 0x06) newType = 0x16;
            else if (type == 0x07) newType = 0x17; // NTFS/exFAT
            else if (type == 0x0B) newType = 0x1B; // FAT32
            else if (type == 0x0C) newType = 0x1C; // FAT32 LBA
            else if (type == 0x0E) newType = 0x1E; // FAT16 LBA
            else if (type == 0x0F) newType = 0x1F; // Extended LBA
            
            if (newType != type)
            {
                _selectedPartition.FileSystemType = newType;
                _selectedPartition.FileSystemDesc = PartitionEntry.GetFileSystemDescription(newType) + " (Hidden)";
                RefreshList();
            }
            else
            {
                ShowError("Cannot hide this partition type or logic not implemented.");
            }
        }

        private void UnhideBtn_Click(object sender, RoutedEventArgs e)
        {
             if (_selectedPartition == null) return;
            
            byte type = _selectedPartition.FileSystemType;
            byte newType = type;
            
            if (type == 0x11) newType = 0x01;
            else if (type == 0x14) newType = 0x04;
            else if (type == 0x16) newType = 0x06;
            else if (type == 0x17) newType = 0x07;
            else if (type == 0x1B) newType = 0x0B;
            else if (type == 0x1C) newType = 0x0C;
            else if (type == 0x1E) newType = 0x0E;
            else if (type == 0x1F) newType = 0x0F;
            
            if (newType != type)
            {
                _selectedPartition.FileSystemType = newType;
                _selectedPartition.FileSystemDesc = PartitionEntry.GetFileSystemDescription(newType);
                RefreshList();
            }
        }

        private void DeleteBtn_Click(object sender, RoutedEventArgs e)
        {
             if (_selectedPartition == null) return;
             
             // Clear the entry
             _selectedPartition.IsActive = false;
             _selectedPartition.FileSystemType = 0;
             _selectedPartition.FileSystemDesc = "Empty";
             _selectedPartition.StartLba = 0;
             _selectedPartition.TotalSectors = 0;
             _selectedPartition.PartitionTypeGuid = Guid.Empty;
             _selectedPartition.Name = "";
             
             RefreshList();
        }

        private void RefreshList()
        {
            PartitionsList.ItemsSource = null;
            PartitionsList.ItemsSource = _partitions;
            UpdateButtonsState();
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _diskService.SavePartitionTable(_diskIndex, _partitions);
                ShowSuccess("Partition table saved successfully. Please reboot for changes to take effect.");
            }
            catch (Exception ex)
            {
                ShowError($"Failed to save: {ex.Message}");
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
