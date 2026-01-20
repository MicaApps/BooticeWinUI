namespace BooticeWinUI.Models
{
    public class PhysicalDiskInfo
    {
        public int Index { get; set; }
        public string DevicePath { get; set; }
        public string Model { get; set; }
        public long Size { get; set; }
        public long Cylinders { get; set; }
        public int Heads { get; set; }
        public int SectorsPerTrack { get; set; }
        public int BytesPerSector { get; set; }
        public long TotalSectors => Size / BytesPerSector;

        public string DisplayName => $"HD{Index}: {Model} ({FormatSize(Size)})";

        private static string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.0} {sizes[order]}";
        }
    }
}
