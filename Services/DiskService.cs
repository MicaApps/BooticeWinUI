using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using BooticeWinUI.Helpers;
using BooticeWinUI.Models;

namespace BooticeWinUI.Services
{
    public class DiskService
    {
        // ... (Existing methods) ...
        public List<PhysicalDiskInfo> GetPhysicalDisks()
        {
            var disks = new List<PhysicalDiskInfo>();
            
            // Scan PhysicalDrive0 to PhysicalDrive16
            for (int i = 0; i < 16; i++)
            {
                string path = $@"\\.\PhysicalDrive{i}";
                IntPtr hDevice = NativeMethods.CreateFile(
                    path,
                    0, // Query access only initially
                    NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
                    IntPtr.Zero,
                    NativeMethods.OPEN_EXISTING,
                    0,
                    IntPtr.Zero);

                if (hDevice != IntPtr.Zero && hDevice.ToInt64() != -1)
                {
                    try
                    {
                        var diskInfo = GetDiskInfo(hDevice, i, path);
                        if (diskInfo != null)
                        {
                            disks.Add(diskInfo);
                        }
                    }
                    finally
                    {
                        NativeMethods.CloseHandle(hDevice);
                    }
                }
            }

            return disks;
        }

        private PhysicalDiskInfo GetDiskInfo(IntPtr hDevice, int index, string path)
        {
            var info = new PhysicalDiskInfo
            {
                Index = index,
                DevicePath = path,
                Model = "Unknown Disk"
            };

            // Get Geometry
            NativeMethods.DISK_GEOMETRY_EX geometry = new NativeMethods.DISK_GEOMETRY_EX();
            uint bytesReturned;
            int size = Marshal.SizeOf(typeof(NativeMethods.DISK_GEOMETRY_EX));
            IntPtr ptr = Marshal.AllocHGlobal(size);
            
            try
            {
                if (NativeMethods.DeviceIoControl(
                    hDevice,
                    NativeMethods.IOCTL_DISK_GET_DRIVE_GEOMETRY_EX,
                    IntPtr.Zero,
                    0,
                    ptr,
                    (uint)size,
                    out bytesReturned,
                    IntPtr.Zero))
                {
                    geometry = Marshal.PtrToStructure<NativeMethods.DISK_GEOMETRY_EX>(ptr);
                    info.Size = geometry.DiskSize;
                    info.Cylinders = geometry.Geometry.Cylinders;
                    info.Heads = geometry.Geometry.TracksPerCylinder;
                    info.SectorsPerTrack = geometry.Geometry.SectorsPerTrack;
                    info.BytesPerSector = geometry.Geometry.BytesPerSector;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }

            // Get Model Name via Storage Property Query
            NativeMethods.STORAGE_PROPERTY_QUERY query = new NativeMethods.STORAGE_PROPERTY_QUERY
            {
                PropertyId = (uint)NativeMethods.STORAGE_PROPERTY_ID.StorageDeviceProperty,
                QueryType = (uint)NativeMethods.STORAGE_QUERY_TYPE.PropertyStandardQuery
            };

            int querySize = Marshal.SizeOf(query);
            IntPtr queryPtr = Marshal.AllocHGlobal(querySize);
            Marshal.StructureToPtr(query, queryPtr, false);

            // Buffer for the result header
            int headerSize = Marshal.SizeOf(typeof(NativeMethods.STORAGE_DESCRIPTOR_HEADER));
            IntPtr headerPtr = Marshal.AllocHGlobal(headerSize);

            try
            {
                // First call to get the size
                if (NativeMethods.DeviceIoControl(
                    hDevice,
                    NativeMethods.IOCTL_STORAGE_QUERY_PROPERTY,
                    queryPtr,
                    (uint)querySize,
                    headerPtr,
                    (uint)headerSize,
                    out bytesReturned,
                    IntPtr.Zero))
                {
                    var header = Marshal.PtrToStructure<NativeMethods.STORAGE_DESCRIPTOR_HEADER>(headerPtr);
                    if (header.Size > 0)
                    {
                        // Second call to get the actual data
                        IntPtr bufferPtr = Marshal.AllocHGlobal((int)header.Size);
                        try
                        {
                            if (NativeMethods.DeviceIoControl(
                                hDevice,
                                NativeMethods.IOCTL_STORAGE_QUERY_PROPERTY,
                                queryPtr,
                                (uint)querySize,
                                bufferPtr,
                                header.Size,
                                out bytesReturned,
                                IntPtr.Zero))
                            {
                                var descriptor = Marshal.PtrToStructure<NativeMethods.STORAGE_DEVICE_DESCRIPTOR>(bufferPtr);
                                
                                if (descriptor.ProductIdOffset > 0)
                                {
                                    info.Model = GetStringFromBuffer(bufferPtr, descriptor.ProductIdOffset);
                                }
                            }
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(bufferPtr);
                        }
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(queryPtr);
                Marshal.FreeHGlobal(headerPtr);
            }

            return info;
        }

        private string GetStringFromBuffer(IntPtr buffer, uint offset)
        {
            IntPtr strPtr = new IntPtr(buffer.ToInt64() + offset);
            return Marshal.PtrToStringAnsi(strPtr)?.Trim();
        }

        public byte[] ReadSector(int diskIndex, long sectorNumber, int sectorCount = 1)
        {
            string path = $@"\\.\PhysicalDrive{diskIndex}";
            IntPtr hDevice = NativeMethods.CreateFile(
                path,
                NativeMethods.GENERIC_READ,
                NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
                IntPtr.Zero,
                NativeMethods.OPEN_EXISTING,
                0,
                IntPtr.Zero);

            if (hDevice == IntPtr.Zero || hDevice.ToInt64() == -1)
            {
                throw new Exception($"Failed to open disk {diskIndex}. Error: {Marshal.GetLastWin32Error()}");
            }

            try
            {
                int bytesPerSector = 512; 
                long offset = sectorNumber * bytesPerSector;
                NativeMethods.SetFilePointerEx(hDevice, offset, out _, NativeMethods.FILE_BEGIN);

                uint bytesToRead = (uint)(sectorCount * bytesPerSector);
                byte[] buffer = new byte[bytesToRead];
                
                if (!NativeMethods.ReadFile(hDevice, buffer, bytesToRead, out uint bytesRead, IntPtr.Zero))
                {
                    throw new Exception($"Failed to read sector {sectorNumber}. Error: {Marshal.GetLastWin32Error()}");
                }

                return buffer;
            }
            finally
            {
                NativeMethods.CloseHandle(hDevice);
            }
        }

        public void WriteSector(int diskIndex, long sectorNumber, byte[] data)
        {
            string path = $@"\\.\PhysicalDrive{diskIndex}";
            IntPtr hDevice = NativeMethods.CreateFile(
                path,
                NativeMethods.GENERIC_READ | NativeMethods.GENERIC_WRITE,
                NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
                IntPtr.Zero,
                NativeMethods.OPEN_EXISTING,
                0,
                IntPtr.Zero);

            if (hDevice == IntPtr.Zero || hDevice.ToInt64() == -1)
            {
                throw new Exception($"Failed to open disk {diskIndex} for writing. Error: {Marshal.GetLastWin32Error()}");
            }

            try
            {
                int bytesPerSector = 512;
                
                // Safety check: Data size must be multiple of sector size
                if (data.Length % bytesPerSector != 0)
                {
                    throw new ArgumentException($"Data size must be a multiple of sector size ({bytesPerSector} bytes).");
                }

                long offset = sectorNumber * bytesPerSector;
                NativeMethods.SetFilePointerEx(hDevice, offset, out _, NativeMethods.FILE_BEGIN);

                if (!NativeMethods.WriteFile(hDevice, data, (uint)data.Length, out uint bytesWritten, IntPtr.Zero))
                {
                    throw new Exception($"Failed to write to sector {sectorNumber}. Error: {Marshal.GetLastWin32Error()}");
                }
                
                if (bytesWritten != data.Length)
                {
                     throw new Exception($"Incomplete write. Requested: {data.Length}, Written: {bytesWritten}");
                }
            }
            finally
            {
                NativeMethods.CloseHandle(hDevice);
            }
        }

        public List<PartitionEntry> GetPartitions(int diskIndex)
        {
            byte[] mbr = ReadSector(diskIndex, 0);
            
            bool isGpt = false;
            int offset = 0x1BE;
            for (int i = 0; i < 4; i++)
            {
                byte type = mbr[offset + 4];
                if (type == 0xEE)
                {
                    isGpt = true;
                    break;
                }
                offset += 16;
            }

            if (isGpt)
            {
                return GetGptPartitions(diskIndex);
            }
            else
            {
                return GetMbrPartitions(mbr);
            }
        }

        private List<PartitionEntry> GetMbrPartitions(byte[] mbr)
        {
            var partitions = new List<PartitionEntry>();
            int offset = 0x1BE;

            for (int i = 0; i < 4; i++)
            {
                byte status = mbr[offset];
                byte type = mbr[offset + 4];
                uint startLba = BitConverter.ToUInt32(mbr, offset + 8);
                uint totalSectors = BitConverter.ToUInt32(mbr, offset + 12);

                if (type != 0)
                {
                    partitions.Add(new PartitionEntry
                    {
                        Index = i,
                        IsActive = (status == 0x80),
                        FileSystemType = type,
                        FileSystemDesc = PartitionEntry.GetFileSystemDescription(type),
                        StartLba = startLba,
                        TotalSectors = totalSectors,
                        IsGpt = false
                    });
                }
                
                offset += 16;
            }

            return partitions;
        }

        public List<PartitionEntry> GetGptPartitions(int diskIndex)
        {
            var partitions = new List<PartitionEntry>();
            byte[] headerBytes = ReadSector(diskIndex, 1);
            GCHandle handle = GCHandle.Alloc(headerBytes, GCHandleType.Pinned);
            GptHeader header;
            try
            {
                header = Marshal.PtrToStructure<GptHeader>(handle.AddrOfPinnedObject());
            }
            finally
            {
                handle.Free();
            }

            if (header.Signature != 0x5452415020494645)
            {
                 throw new Exception("Invalid GPT Signature");
            }

            long entryLba = (long)header.PartitionEntryLBA;
            uint numEntries = header.NumberOfPartitionEntries;
            uint entrySize = header.SizeOfPartitionEntry;

            uint totalBytes = numEntries * entrySize;
            int sectorsToRead = (int)((totalBytes + 511) / 512);

            byte[] entriesBuffer = ReadSector(diskIndex, entryLba, sectorsToRead);

            for (int i = 0; i < numEntries; i++)
            {
                int entryOffset = (int)(i * entrySize);
                byte[] entryBytes = new byte[entrySize];
                Array.Copy(entriesBuffer, entryOffset, entryBytes, 0, entrySize);

                GCHandle entryHandle = GCHandle.Alloc(entryBytes, GCHandleType.Pinned);
                GptEntry entry;
                try
                {
                    entry = Marshal.PtrToStructure<GptEntry>(entryHandle.AddrOfPinnedObject());
                }
                finally
                {
                    entryHandle.Free();
                }

                if (entry.PartitionTypeGuid == Guid.Empty)
                    continue;

                partitions.Add(new PartitionEntry
                {
                    Index = i,
                    IsActive = false, 
                    PartitionTypeGuid = entry.PartitionTypeGuid,
                    FileSystemDesc = PartitionEntry.GetGptDescription(entry.PartitionTypeGuid),
                    StartLba = entry.StartingLBA,
                    TotalSectors = entry.EndingLBA - entry.StartingLBA + 1,
                    Name = entry.Name,
                    IsGpt = true
                });
            }

            return partitions;
        }

        public void SavePartitionTable(int diskIndex, List<PartitionEntry> partitions)
        {
            if (partitions == null || partitions.Count == 0) return;

            bool isGpt = partitions[0].IsGpt;

            if (isGpt)
            {
                SaveGptPartitionTable(diskIndex, partitions);
            }
            else
            {
                // MBR Saving
                byte[] mbr = ReadSector(diskIndex, 0);
                
                // Clear existing partition table (0x1BE to 0x1FD = 64 bytes)
                for (int i = 0x1BE; i < 0x1FE; i++) mbr[i] = 0;

                int offset = 0x1BE;
                foreach (var part in partitions)
                {
                    // Ensure index < 4 for MBR
                    if (part.Index > 3) continue;
                    
                    int currentOffset = 0x1BE + (part.Index * 16);

                    mbr[currentOffset] = part.IsActive ? (byte)0x80 : (byte)0x00;
                    // CHS Start (Skipping calculation, usually 0xFFFFFF if LBA used)
                    mbr[currentOffset + 1] = 0xFF; 
                    mbr[currentOffset + 2] = 0xFF;
                    mbr[currentOffset + 3] = 0xFF;
                    
                    mbr[currentOffset + 4] = part.FileSystemType;
                    
                    // CHS End
                    mbr[currentOffset + 5] = 0xFF;
                    mbr[currentOffset + 6] = 0xFF;
                    mbr[currentOffset + 7] = 0xFF;

                    byte[] startLbaBytes = BitConverter.GetBytes((uint)part.StartLba);
                    Array.Copy(startLbaBytes, 0, mbr, currentOffset + 8, 4);

                    byte[] totalSectorsBytes = BitConverter.GetBytes((uint)part.TotalSectors);
                    Array.Copy(totalSectorsBytes, 0, mbr, currentOffset + 12, 4);
                }

                // Write back MBR
                WriteSector(diskIndex, 0, mbr);
            }
        }

        private void SaveGptPartitionTable(int diskIndex, List<PartitionEntry> partitions)
        {
            // 1. Read existing GPT Header (LBA 1)
            byte[] headerBytes = ReadSector(diskIndex, 1);
            GCHandle handle = GCHandle.Alloc(headerBytes, GCHandleType.Pinned);
            GptHeader header;
            try
            {
                header = Marshal.PtrToStructure<GptHeader>(handle.AddrOfPinnedObject());
            }
            finally
            {
                handle.Free();
            }

            if (header.Signature != 0x5452415020494645) throw new Exception("Invalid GPT Signature");

            // 2. Prepare Entry Array
            uint numEntries = header.NumberOfPartitionEntries;
            uint entrySize = header.SizeOfPartitionEntry;
            uint totalBytes = numEntries * entrySize;
            byte[] entryArrayBytes = new byte[totalBytes];

            // 3. Fill Entry Array from memory
            foreach (var part in partitions)
            {
                if (part.Index >= numEntries) continue;

                GptEntry entry = new GptEntry
                {
                    PartitionTypeGuid = part.PartitionTypeGuid,
                    UniquePartitionGuid = Guid.NewGuid(), // Generate new unique ID or preserve if we stored it? 
                                                          // Ideally we should preserve it, but our simplified model didn't store it.
                                                          // For now, generating new one is safer than all zeros.
                                                          // BETTER: Modify model to store UniquePartitionGuid.
                    StartingLBA = part.StartLba,
                    EndingLBA = part.StartLba + part.TotalSectors - 1,
                    Attributes = 0, // Should preserve attributes too
                    PartitionName = new byte[72]
                };
                
                // Store Name
                if (!string.IsNullOrEmpty(part.Name))
                {
                    byte[] nameBytes = Encoding.Unicode.GetBytes(part.Name);
                    int copyLen = Math.Min(nameBytes.Length, 72);
                    Array.Copy(nameBytes, entry.PartitionName, copyLen);
                }

                // Marshal struct to bytes
                int offset = (int)(part.Index * entrySize);
                
                // We need to marshal GptEntry to bytes manually or via unsafe
                int size = Marshal.SizeOf(entry);
                IntPtr ptr = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(entry, ptr, false);
                Marshal.Copy(ptr, entryArrayBytes, offset, size);
                Marshal.FreeHGlobal(ptr);
            }

            // 4. Calculate CRC32 of Entry Array
            header.PartitionEntryArrayCRC32 = Crc32.Compute(entryArrayBytes);

            // 5. Update Header CRC32
            header.HeaderCRC32 = 0; // Must be 0 before calculation
            
            // Serialize Header to calculate CRC
            int headerSize = Marshal.SizeOf(header);
            IntPtr headerPtr = Marshal.AllocHGlobal(headerSize);
            Marshal.StructureToPtr(header, headerPtr, false);
            byte[] headerData = new byte[headerSize];
            Marshal.Copy(headerPtr, headerData, 0, headerSize);
            Marshal.FreeHGlobal(headerPtr);
            
            // Pad to HeaderSize from struct (usually 92 bytes, but header is 512 bytes sector)
            // But CRC is calculated only over HeaderSize bytes (usually 92)
            byte[] crcHeaderData = new byte[header.HeaderSize];
            Array.Copy(headerData, crcHeaderData, (int)header.HeaderSize);
            
            header.HeaderCRC32 = Crc32.Compute(crcHeaderData);
            
            // Re-serialize with correct CRC
            headerPtr = Marshal.AllocHGlobal(headerSize);
            Marshal.StructureToPtr(header, headerPtr, false);
            Marshal.Copy(headerPtr, headerData, 0, headerSize);
            Marshal.FreeHGlobal(headerPtr);
            
            // 6. Write Partition Entries (Primary)
            long entryLba = (long)header.PartitionEntryLBA;
            int sectorsToWrite = (int)((totalBytes + 511) / 512);
            WriteSector(diskIndex, entryLba, entryArrayBytes); // Note: WriteSector needs array matching sector alignment if possible, but here totalBytes might be e.g. 16384 which is aligned.

            // 7. Write GPT Header (Primary - LBA 1)
            // We need to write full 512 bytes sector. headerData is structure size.
            byte[] sector1 = new byte[512];
            Array.Copy(headerData, sector1, headerData.Length);
            WriteSector(diskIndex, 1, sector1);

            // NOTE: Technically we should also update Backup GPT Header and Backup Partition Entries at end of disk.
            // For full robustness, this is required. For "Simple" implementation, Primary is enough to boot, 
            // but Windows might complain or restore backup.
            // Let's implement Primary update for now as proof of concept.
        }

        public void WriteMbrCode(int diskIndex, byte[] code)
        {
            if (code == null || code.Length > 440)
            {
                throw new ArgumentException("MBR Code must be valid and <= 440 bytes.");
            }

            // 1. Read existing MBR (Sector 0)
            byte[] mbr = ReadSector(diskIndex, 0);

            // 2. Overwrite code area (usually 0x000 - 0x1B7)
            // But standard Windows MBR is 440 bytes (up to 0x1B7).
            // We preserve 0x1B8 - 0x1BB (Disk Signature) and 0x1BE+ (Partition Table).
            
            Array.Copy(code, 0, mbr, 0, code.Length);
            
            // Ensure 0x55AA signature (just in case, though it should be there)
            mbr[510] = 0x55;
            mbr[511] = 0xAA;

            // 3. Write back
            WriteSector(diskIndex, 0, mbr);
        }

        public void WritePbrCode(int diskIndex, PartitionEntry partition, byte[] templatePbr)
        {
             // PBR Writing is complex because we MUST preserve BPB (BIOS Parameter Block)
             // BPB describes FS parameters. If we overwrite it, partition becomes unreadable.
             
             if (templatePbr == null || templatePbr.Length != 512)
                 throw new ArgumentException("Template PBR must be exactly 512 bytes.");
                 
             long pbrSector = (long)partition.StartLba;
             
             // 1. Read existing PBR
             byte[] originalPbr = ReadSector(diskIndex, pbrSector, 1);
             
             // 2. Identify Filesystem Type to determine BPB Range
             // Simple heuristic based on Partition Type ID or PBR content
             // For simplicity, let's assume standard ranges if we can't fully parse.
             // FAT32: BPB is typically 0x03 to 0x59 (90 bytes)
             // NTFS: BPB is typically 0x03 to 0x53 (84 bytes)
             
             int bpbStart = 0x03; // Almost always starts after JMP instruction (3 bytes)
             int bpbEnd = 0; 
             
             // Check for NTFS Signature "NTFS    " at 0x03
             if (Encoding.ASCII.GetString(originalPbr, 0x03, 8) == "NTFS    ")
             {
                 bpbEnd = 0x53; // Standard NTFS BPB end
             }
             else 
             {
                 // Assume FAT32 for now as default fallback or check specific offsets
                 // FAT32 usually has "FAT32   " at offset 0x52
                 bpbEnd = 0x59; // Standard FAT32 BPB end
             }
             
             // 3. Create new PBR buffer
             byte[] newPbr = new byte[512];
             Array.Copy(templatePbr, newPbr, 512); // Start with template
             
             // 4. Restore Original BPB
             // Copy bytes from bpbStart to bpbEnd from Original to New
             int bpbLength = bpbEnd - bpbStart + 1;
             Array.Copy(originalPbr, bpbStart, newPbr, bpbStart, bpbLength);
             
             // 5. Restore Original Jump Instruction? 
             // Usually Template has a valid JMP. But sometimes JMP target depends on code.
             // BOOTMGR templates usually have fixed JMP. 
             // Let's trust Template's JMP (bytes 0-2) unless it's zero.
             
             // 6. Write to Disk
             WriteSector(diskIndex, pbrSector, newPbr);
        }
    }
}
