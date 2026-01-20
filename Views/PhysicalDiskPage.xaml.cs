using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using BooticeWinUI.Services;
using BooticeWinUI.Models;
using BooticeWinUI.Dialogs;
using System;
using System.Text;
using System.Linq;

namespace BooticeWinUI.Views
{
    public sealed partial class PhysicalDiskPage : Page
    {
        private readonly DiskService _diskService;
        private PhysicalDiskInfo _selectedDisk;

        public PhysicalDiskPage()
        {
            this.InitializeComponent();
            _diskService = new DiskService();
            LoadDisks();
        }

        private void LoadDisks()
        {
            try
            {
                var disks = _diskService.GetPhysicalDisks();
                DiskComboBox.ItemsSource = disks;
                if (disks.Count > 0)
                {
                    DiskComboBox.SelectedIndex = 0;
                }
                else
                {
                    ShowError("No physical disks found or access denied. Please run as Administrator.");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Failed to enumerate disks: {ex.Message}");
            }
        }

        private void DiskComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DiskComboBox.SelectedItem is PhysicalDiskInfo disk)
            {
                _selectedDisk = disk;
                DiskInfoTextBlock.Text = $"Cylinders: {disk.Cylinders}, Heads: {disk.Heads}, Sectors per Track: {disk.SectorsPerTrack}, Bytes per Sector: {disk.BytesPerSector}\nTotal Sectors: {disk.TotalSectors}, Size: {disk.Size} bytes";
                StatusInfoBar.IsOpen = false;
                HexDumpBox.Text = string.Empty;
            }
        }

        private void ReadSectorBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedDisk == null) return;

            if (!long.TryParse(SectorInput.Text, out long sector))
            {
                ShowError("Invalid sector number.");
                return;
            }

            try
            {
                // Read Sector (512 bytes)
                byte[] data = _diskService.ReadSector(_selectedDisk.Index, sector);
                HexDumpBox.Text = FormatHexDump(data, sector);
                ShowInfo($"Read Sector {sector} successfully.");
            }
            catch (Exception ex)
            {
                ShowError($"Failed to read sector: {ex.Message}");
            }
        }

        private async void ProcessMBR_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedDisk == null) return;

            var dialog = new ProcessMBRDialog(_diskService, _selectedDisk.Index);
            dialog.XamlRoot = this.XamlRoot;
            await dialog.ShowAsync();
        }

        private async void ProcessPBR_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedDisk == null) return;

            var dialog = new ProcessPBRDialog(_diskService, _selectedDisk.Index);
            dialog.XamlRoot = this.XamlRoot;
            await dialog.ShowAsync();
        }

        private async void PartsManage_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedDisk == null) return;

            var dialog = new PartitionManageDialog(_diskService, _selectedDisk.Index);
            dialog.XamlRoot = this.XamlRoot;
            await dialog.ShowAsync();
        }

        private string FormatHexDump(byte[] data, long startSector = 0)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Sector {startSector} (Offset 0x{startSector * 512:X}):");
            sb.AppendLine();
            
            int length = data.Length;
            for (int i = 0; i < length; i += 16)
            {
                sb.AppendFormat("{0:X8}   ", i);

                // Hex
                for (int j = 0; j < 16; j++)
                {
                    if (i + j < length)
                        sb.AppendFormat("{0:X2} ", data[i + j]);
                    else
                        sb.Append("   ");
                }

                sb.Append("  ");

                // ASCII
                for (int j = 0; j < 16; j++)
                {
                    if (i + j < length)
                    {
                        char c = (char)data[i + j];
                        if (c < 32 || c > 126) c = '.';
                        sb.Append(c);
                    }
                }

                sb.AppendLine();
            }
            return sb.ToString();
        }

        private void ShowError(string message)
        {
            StatusInfoBar.Severity = InfoBarSeverity.Error;
            StatusInfoBar.Title = "Error";
            StatusInfoBar.Message = message;
            StatusInfoBar.IsOpen = true;
        }

        private void ShowInfo(string message)
        {
            StatusInfoBar.Severity = InfoBarSeverity.Informational;
            StatusInfoBar.Title = "Information";
            StatusInfoBar.Message = message;
            StatusInfoBar.IsOpen = true;
        }
    }
}
