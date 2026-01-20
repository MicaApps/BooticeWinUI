using System;
using System.Runtime.InteropServices;

namespace BooticeWinUI.Models
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct GptHeader
    {
        public ulong Signature;
        public uint Revision;
        public uint HeaderSize;
        public uint HeaderCRC32;
        public uint Reserved;
        public ulong CurrentLBA;
        public ulong BackupLBA;
        public ulong FirstUsableLBA;
        public ulong LastUsableLBA;
        public Guid DiskGuid;
        public ulong PartitionEntryLBA;
        public uint NumberOfPartitionEntries;
        public uint SizeOfPartitionEntry;
        public uint PartitionEntryArrayCRC32;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct GptEntry
    {
        public Guid PartitionTypeGuid;
        public Guid UniquePartitionGuid;
        public ulong StartingLBA;
        public ulong EndingLBA;
        public ulong Attributes;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 72)]
        public byte[] PartitionName; // 36 chars * 2 bytes (UTF-16LE)
        
        public string Name
        {
            get
            {
                if (PartitionName == null) return string.Empty;
                return System.Text.Encoding.Unicode.GetString(PartitionName).TrimEnd('\0');
            }
        }
    }
}
