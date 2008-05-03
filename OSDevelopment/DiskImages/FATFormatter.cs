using System;
using System.Collections.Generic;
using System.Text;

namespace OSDevelopment.DiskImages
{
    static class FileCompression
    {
        const uint FSCTL_SET_COMPRESSION = 639040;
        const uint FSCTL_SET_SPARSE = 590020;
        const uint FSCTL_SET_ZERO_DATA = 622792;
        static byte[] sector = new byte[512];
        [System.Runtime.InteropServices.DllImport("kernel32.dll", EntryPoint = "DeviceIoControl")]
        private static extern bool SetCompression(IntPtr hDevice, uint dwControlCode, ref ushort val1, int int2, IntPtr zero1, int zero, out int bytesretd, IntPtr zero2);
        [System.Runtime.InteropServices.DllImport("kernel32.dll", EntryPoint = "DeviceIoControl")]
        private static extern bool SetSparse(IntPtr hDevice, uint dwControlCode, IntPtr ipzero, int izero, IntPtr zero1, int zero, out int bytesretd, IntPtr zero2);
        [System.Runtime.InteropServices.DllImport("kernel32.dll", EntryPoint = "DeviceIoControl")]
        private static extern bool SetZeroData(IntPtr hDevice, uint dwControlCode, ref zeroinfo zi, int i16, IntPtr zero1, int zero, out int bytesretd, IntPtr zero2);
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct zeroinfo
        {
            public long begin;
            public long end;
            public zeroinfo(long s, long e) { begin = s; end = e; }
        }
        // Compresses the file amd sets its compression attribute.
        public static void CompressStream(System.IO.FileStream fs)
        {
            ushort t = 1;
            int retd;
            SetCompression(fs.SafeFileHandle.DangerousGetHandle(), FSCTL_SET_COMPRESSION, ref t, 2, IntPtr.Zero, 0, out retd, IntPtr.Zero);
        }
        // Sets the file's sparse file attribute.
        public static void SetSparse(System.IO.FileStream fs)
        {
            int retd;
            SetSparse(fs.SafeFileHandle.DangerousGetHandle(), FSCTL_SET_SPARSE, IntPtr.Zero, 0, IntPtr.Zero, 0, out retd, IntPtr.Zero);
        }
        // Writes zeros to a file stream, with space advantage on compressed and sparse files.
        public static void WriteZeros(System.IO.FileStream fs, long begin, long end)
        {
            // Hopefully this works as expected, it's hard to get this working right over the .NET framework

            fs.Flush(); // So that ALL of the buffers are cleared and written to the file.
            zeroinfo zi = new zeroinfo();
            int retd;
            // Get the file handle
            IntPtr hndl = fs.SafeFileHandle.DangerousGetHandle();
            if (end > fs.Length)
                while (end > fs.Length)
                {
                    long pos = fs.Length;
                    long write = Math.Min(0x100000, end - pos);
                    fs.SetLength(pos + write);
                    zi.begin = pos;
                    zi.end = pos + write;
                    SetZeroData(hndl, FSCTL_SET_ZERO_DATA, ref zi, 16, IntPtr.Zero, 0, out retd, IntPtr.Zero);
                }
            zi.begin = begin;
            zi.end = end;
            SetZeroData(hndl, FSCTL_SET_ZERO_DATA, ref zi, 16, IntPtr.Zero, 0, out retd, IntPtr.Zero);
            fs.Position = end;
        }
        public static void WriteZeros(System.IO.Stream s, long begin, long end)
        {
            if (s.GetType() == typeof(System.IO.FileStream))
                WriteZeros((System.IO.FileStream)s, begin, end);
            else
            {
                for (long t = begin; t < end; t += 512)
                    s.Write(sector, 0, (int)Math.Min(512, end - t));
                s.Position = end;
            }
        }
    }
    public class FATFormatInfo
    {
        public FATFormatInfo() { }
        public FATFormatInfo(FATType type, int size_mbytes, string volumeLabel, int spt, int heads)
        {
            Type = type;
            VolumeLabel = volumeLabel;
            SectorsPerTrack = (ushort)spt;
            Heads = (ushort)heads;
            Cylinders = (uint)((ulong)size_mbytes * 0x100000 / (ulong)(spt * heads * 512));
            TotalSectors = (uint)(Cylinders * heads * spt);
        }
        public FATType Type = FATType.None;
        public uint TotalSectors = 0x1FFE00;
        public ushort SectorsPerTrack = 63;
        public ushort Heads = 16;
        public uint Cylinders = 2080;
        public string VolumeLabel = null;
        public byte DriveNumber = 0x80;
        public byte MediaType = 0xF8;
        public byte[] BootSector = null;
    }
    public static class FATFormatter
    {
        #region Tables
        static uint[][] FAT16Table = new uint[][] {
            new uint[] {8400, 0},
            new uint[] {32680, 2},
            new uint[] {262144, 4},
            new uint[] {524288, 8},
            new uint[] {1048576, 16},
            new uint[] {2097152, 32},
            new uint[] {4194304, 64},
            new uint[] {0xFFFFFFFF, 0}
        };

        static uint[][] FAT32Table = new uint[][] {
            new uint[] {66600, 0},
            new uint[] {532480, 1},
            new uint[] {16777216, 8},
            new uint[] {33554432, 16},
            new uint[] {67108864, 32},
            new uint[] {0xFFFFFFFF, 64}
        }; 
        #endregion
        public static uint GetSectorCount(int size_mbytes, int spt, int heads)
        {
            uint Cylinders = (uint)((ulong)size_mbytes * 0x100000 / (ulong)(spt * heads * 512));
            return (uint)(Cylinders * heads * spt);
        }
        public static uint GetCylinderCount(int size_mbytes, int spt, int heads)
        {
            return (uint)((ulong)size_mbytes * 0x100000 / (ulong)(spt * heads * 512));
        }
        public static FATType DefaultFatType(uint totalsectors)
        {
            if (totalsectors <= 8400) return FATType.FAT12;
            if (totalsectors < 1048576) return FATType.FAT16;
            return FATType.FAT32;
        }
        public static bool ValidFatType(FATType type, uint totalsectors)
        {
            if (totalsectors <= 8400) return type == FATType.FAT12; // Only FAT12 can go this low
            if (totalsectors <= 66600) return type == FATType.FAT16; // between 8400 and 66600 is exclusive to FAT16
            if (totalsectors > 4194304) return type == FATType.FAT32; // above this is exclusive to fat32
            return type != FATType.FAT12; // between 66600 and 4194304 is common to FAT16 or FAT32.
        }
        static void FormatDiskImage(System.IO.Stream s, FATFormatInfo fi, bool MustInitialize)
        {
            if (!s.CanWrite || !s.CanSeek) throw new System.IO.IOException();
            if (fi.Type == FATType.None) fi.Type = DefaultFatType(fi.TotalSectors);
            if (fi.Type != FATType.FAT16 && fi.Type != FATType.FAT32) throw new NotSupportedException();
            if (!ValidFatType(fi.Type, fi.TotalSectors)) throw new FATException("Invalid type of FAT, given sector count.");
            if (fi.BootSector == null) fi.BootSector = fi.Type == FATType.FAT32 ? Properties.Resources.bootsect32 : Properties.Resources.bootsect16;
            s.Seek(0, System.IO.SeekOrigin.Begin);
            System.IO.BinaryWriter bw = new System.IO.BinaryWriter(s);
            // Get sectors per cluster
            uint spc = 0;
            {
                uint[][] t = fi.Type == FATType.FAT16 ? FAT16Table : FAT32Table;
                foreach (uint[] i in t)
                {
                    if (i[0] >= fi.TotalSectors)
                    {
                        spc = i[1];
                        break;
                    }
                }
            }
#if DEBUG
            System.Diagnostics.Debug.Assert(spc != 0); // Should have been prevented by ValidFatType().
#endif
            // Get FAT sectors
            uint fatsz;
            {
                uint rootsec = fi.Type == FATType.FAT32 ? 0u : 32u;
                uint resvd = fi.Type == FATType.FAT32 ? 32u : 1u;
                uint t1 = fi.TotalSectors - (resvd + rootsec);
                uint t2 = (256 * spc) + 2;
                if (fi.Type == FATType.FAT32)
                    t2 /= 2;
                fatsz = (t1 + (t2 - 1)) / t2;
            }
            if (MustInitialize)
            {
                // FAT32: 2 fats + 32 reserved + root cluster; FAT16: 2 fats + 1 reserved + 32 root directory
                FileCompression.WriteZeros(s, 0, (long)(fatsz * 2 + (fi.Type == FATType.FAT32 ? 32 + spc : 33)) * 512);
                s.Seek(0, System.IO.SeekOrigin.Begin);
            }
            switch (fi.Type)
            {
                case FATType.FAT16:
                {
                    s.Write(fi.BootSector, 0, 512);
                    s.Seek(11, System.IO.SeekOrigin.Begin);
                    bw.Write((ushort)512); // Bytes per sector
                    bw.Write((byte)spc); // sectors per cluster
                    bw.Write((ushort)1); // Reserved sector count
                    bw.Write((byte)2); // Number of FATs
                    bw.Write((ushort)512); // Number of root entries
                    if (fi.TotalSectors < 65536)
                        bw.Write((ushort)fi.TotalSectors); // Total sectors 16
                    else
                        bw.Write((ushort)0);
                    bw.Write(fi.MediaType); // Media type
                    bw.Write((ushort)fatsz); // FAT sectors
                    bw.Write(fi.SectorsPerTrack); // Sectors per track
                    bw.Write(fi.Heads); // Number of heads
                    bw.Write((uint)0); // Hidden sectors
                    if (fi.TotalSectors >= 65536)
                        bw.Write(fi.TotalSectors); // Total sectors 32
                    else
                        bw.Write((uint)0);
                    bw.Write(fi.DriveNumber); // drv num
                    bw.Write((byte)0); // resvd
                    bw.Write((byte)0x29); // ext boot sig
                    bw.Write((uint)System.DateTime.Now.ToFileTime());
                    if (!string.IsNullOrEmpty(fi.VolumeLabel))
                    {
                        string lab = fi.VolumeLabel.ToUpper();
                        if (lab.Length > 11) lab = lab.Remove(11);
                        while (lab.Length < 11) lab += "\x20";
                        bw.Write(lab.ToCharArray());
                    }
                    else
                        bw.Write("NO NAME    ".ToCharArray());
                    bw.Write("FAT16   ".ToCharArray());
                    for (uint pos = 1; pos <= fatsz + 1; pos += fatsz)
                    {
                        s.Seek((long)pos * 512, System.IO.SeekOrigin.Begin);
                        bw.Write((uint)(0xFFFFFF00 | fi.MediaType));
                    }
                    break;
                }
                case FATType.FAT32:
                {
                    for (long pos = 0; pos <= 3072; pos += 3072)
                    {
                        s.Seek(pos, System.IO.SeekOrigin.Begin);
                        s.Write(fi.BootSector, 0, 512);
                        s.Seek(pos + 11, System.IO.SeekOrigin.Begin);
                        bw.Write((ushort)512); // Bytes per sector
                        bw.Write((byte)spc); // sectors per cluster
                        bw.Write((ushort)32); // Reserved sector count
                        bw.Write((byte)2); // Number of FATs
                        bw.Write((ushort)0); // Number of root entries
                        bw.Write((ushort)0); // Total sectors 16
                        bw.Write(fi.MediaType); // Media type
                        bw.Write((ushort)0); // FAT sectors 16
                        bw.Write(fi.SectorsPerTrack); // Sectors per track
                        bw.Write(fi.Heads); // Number of heads
                        bw.Write((uint)0); // Hidden sectors
                        bw.Write(fi.TotalSectors); // Total sectors 32
                        bw.Write(fatsz); // FAT sectors 32
                        bw.Write((ushort)0); // Flags
                        bw.Write((ushort)0); // Version
                        bw.Write((uint)2); // Root cluster
                        bw.Write((ushort)1); // FSInfo sector
                        bw.Write((ushort)6); // Backup boot sector
                        s.Seek(12, System.IO.SeekOrigin.Current); // Reserved bytes
                        bw.Write((byte)fi.DriveNumber); // Drive number
                        bw.Write((byte)0); // Reserved
                        bw.Write((byte)0x29); // ext boot sig
                        bw.Write((uint)System.DateTime.Now.ToFileTime());
                        if (!string.IsNullOrEmpty(fi.VolumeLabel))
                        {
                            string lab = fi.VolumeLabel.ToUpper();
                            if (lab.Length > 11) lab = lab.Remove(11);
                            while (lab.Length < 11) lab += "\x20";
                            bw.Write(lab.ToCharArray());
                        }
                        else
                            bw.Write("NO NAME    ".ToCharArray());
                        bw.Write("FAT32   ".ToCharArray());
                        s.Seek(pos + 512, System.IO.SeekOrigin.Begin);
                        s.Write(Properties.Resources.fsinfo, 0, 512);
                        s.Seek(510, System.IO.SeekOrigin.Current);
                        bw.Write((ushort)0xAA55); // so that all three sectors have 0xAA55
                    }
                    for (long pos = 32; pos <= fatsz + 32; pos += fatsz)
                    {
                        s.Seek(pos * 512, System.IO.SeekOrigin.Begin);
                        bw.Write((uint)(0x0FFFFF00 | fi.MediaType)); // First entry with media type
                        bw.Write((uint)(0x0FFFFFFF)); // Cluster 1 with eoc mark
                        bw.Write((uint)(0x0FFFFFFF)); // Cluster 2 used for root dorectory, mark next as eoc
                    }
                    break;
                }
            }
        }
        static void CreateDiskImage(System.IO.FileStream fs, FATFormatInfo fi)
        {
            if (fs.Length != 0) fs.SetLength(0);
            FileCompression.CompressStream(fs);
            FileCompression.SetSparse(fs);
            FileCompression.WriteZeros(fs, 0, fi.TotalSectors * 512);
            FormatDiskImage(fs, fi, false);
        }
        static void FormatDiskImage(System.IO.Stream s, bool must_init)
        {
            FATFormatInfo fi = new FATFormatInfo(FATType.None, (int)(s.Length/0x100000), null, 63, 16);
            FormatDiskImage(s, fi, must_init);
        }
        public static void FormatDiskImage(System.IO.Stream s)
        {
            FormatDiskImage(s, true);
        }
        public static void FormatDiskImage(string filename)
        {
            System.IO.FileStream s = System.IO.File.Open(filename, System.IO.FileMode.Open, System.IO.FileAccess.ReadWrite);
            FormatDiskImage(s, true);
            s.Close();
        }
        public static void CreateDiskImage(System.IO.Stream s, FATFormatInfo fi)
        {
            if (s.GetType() == typeof(System.IO.FileStream))
            {
                CreateDiskImage((System.IO.FileStream)s, fi);
                return;
            }
            s.SetLength(0);
            FileCompression.WriteZeros(s, 0, (long)fi.TotalSectors * 512);
            FormatDiskImage(s, fi, false);
        }
        public static void CreateDiskImage(string filename, FATFormatInfo fi)
        {
            System.IO.FileStream fs = System.IO.File.Create(filename);
            CreateDiskImage(fs, fi);
            fs.Close();
        }
        public static void CreateDiskImage(string filename, FATType type, int size_mbytes, string volumeLabel, int spt, int heads)
        {
            FATFormatInfo fi = new FATFormatInfo(type, size_mbytes, volumeLabel, spt, heads);
            CreateDiskImage(filename, fi);
        }
    }
}
