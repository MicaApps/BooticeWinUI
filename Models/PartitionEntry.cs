using System;

namespace BooticeWinUI.Models
{
    public class PartitionEntry
    {
        public int Index { get; set; }
        public bool IsActive { get; set; }
        public byte FileSystemType { get; set; } // For MBR
        public Guid PartitionTypeGuid { get; set; } // For GPT
        public string FileSystemDesc { get; set; }
        public ulong StartLba { get; set; } // Changed to ulong for GPT
        public ulong TotalSectors { get; set; } // Changed to ulong for GPT
        public double SizeGB => (double)TotalSectors * 512 / 1024 / 1024 / 1024; // Assuming 512 bytes sector
        public string Name { get; set; } // For GPT
        public bool IsGpt { get; set; }

        // Helper to get description for common types
        public static string GetFileSystemDescription(byte type)
        {
            return type switch
            {
                0x00 => "Empty",
                0x01 => "FAT12",
                0x04 => "FAT16 <32M",
                0x05 => "Extended",
                0x06 => "FAT16",
                0x07 => "NTFS / exFAT",
                0x0B => "FAT32",
                0x0C => "FAT32 LBA",
                0x0E => "FAT16 LBA",
                0x0F => "Extended LBA",
                0x82 => "Linux Swap",
                0x83 => "Linux",
                0xEE => "GPT Protective",
                0xEF => "EFI System",
                _ => $"Unknown (0x{type:X2})"
            };
        }

        public static string GetGptDescription(Guid typeGuid)
        {
            // Common GPT Type GUIDs
            if (typeGuid == new Guid("EBD0A0A2-B9E5-4433-87C0-68B6B72699C7")) return "Microsoft Basic Data";
            if (typeGuid == new Guid("C12A7328-F81F-11D2-BA4B-00A0C93EC93B")) return "EFI System Partition";
            if (typeGuid == new Guid("E3C9E316-0B5C-4DB8-817D-F92DF00215AE")) return "Microsoft Reserved";
            if (typeGuid == new Guid("DE94BBA4-06D1-4D40-A16A-BFD50179D6AC")) return "Windows Recovery";
            if (typeGuid == new Guid("0FC63DAF-8483-4772-8E79-3D69D8477DE4")) return "Linux Filesystem";
            if (typeGuid == new Guid("0657FD6D-A4AB-43C4-84E5-0933C84B4F4F")) return "Linux Swap";
            
            return typeGuid.ToString();
        }
    }
}
