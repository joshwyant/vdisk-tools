#define LONGNAMES

using System;
using System.Collections.Generic;
using System.Text;

namespace OSDevelopment
{
    namespace DiskImages
    {
        // Class for a stream in a volume
        class VolStream : System.IO.Stream
        {
            System.IO.Stream Str;
            long Start;
            long mLength;
            internal VolStream(System.IO.Stream str, long start, long length)
            {
                Str = str;
                Start = start;
                mLength = length;
            }
            public override bool CanRead
            {
                get { return Str.CanRead; }
            }

            public override bool CanSeek
            {
                get { return Str.CanSeek; }
            }

            public override bool CanWrite
            {
                get { return Str.CanWrite; }
            }

            public override void Flush()
            {
                Str.Flush();
            }

            public override long Length
            {
                get { return mLength; }
            }

            public override long Position
            {
                get
                {
                    return Str.Position - Start;
                }
                set
                {
                    Seek(value, System.IO.SeekOrigin.Begin);
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (!CanRead) throw new System.IO.IOException();
                if ((Position + count) > mLength)
                    count = (int)(mLength - Position);
                if (count < 0) count = 0;
                return Str.Read(buffer, offset, count);
            }

            public override long Seek(long offset, System.IO.SeekOrigin origin)
            {
                if (!CanSeek) throw new System.IO.IOException();
                long pos = Str.Position - Start;
                switch (origin)
                {
                    case System.IO.SeekOrigin.Begin:
                        pos = offset;
                        break;
                    case System.IO.SeekOrigin.Current:
                        pos += offset;
                        break;
                    case System.IO.SeekOrigin.End:
                        pos = mLength - offset;
                        break;
                    default:
                        throw new ArgumentException();
                }
                if (pos < 0)
                    throw new ArgumentOutOfRangeException("offset");
                if (pos > mLength)
                    throw new System.IO.EndOfStreamException();
                Str.Position = Start + pos;
                return Start + pos;
            }

            public override void SetLength(long value)
            {
                throw new InvalidOperationException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                if (!CanWrite) throw new System.IO.IOException();
                if ((Position + count) > mLength)
                    count = (int)(mLength - Position);
                Str.Write(buffer, offset, count);    
            }

            public override void Close()
            {
                base.Close();
                Str.Close();
            }
        }
        public enum FATType
        {
            None,
            FAT12,
            FAT16,
            FAT32,
        }
        [Flags]
        public enum FileAttributes
        {
            None = 0x00,
            ReadOnly = 0x01,
            Hidden = 0x02,
            System = 0x04,
            VolumeLabel = 0x08,
            Directory = 0x10,
            Archive = 0x20,
#if LONGNAMES
            LongName = 0x0F,
#endif
        }
#if LONGNAMES
        [Flags]
        enum CaseInfo
        {
            None = 0,
            LowerBase = 0x08,
            LowerExt = 0x10,
        }
#endif
        enum EntryType
        {
            None,
            Erased,
            NormalEntry,
            Unknown,
#if LONGNAMES
            LongnameEntry,
#endif
        }
        struct EntryInfo
        {
            public EntryType type;
            public byte Ord;
        }
        public class FileInfo
        {
            private FATVolume.DirEntry de;
            internal FileInfo(FATVolume.DirEntry de)
            {
                this.de = de;
            }
            public string Name
            {
                get
                {
                    return de.LongName;
                }
            }
            public FileAttributes Attributes
            {
                get
                {
                    return de.DirAttr;
                }
                set
                {
                    de.SetAttributes((value & ~(FileAttributes.Directory | FileAttributes.VolumeLabel)) | (de.DirAttr & FileAttributes.Directory));
                }
            }
            public System.IO.Stream OpenFile(System.IO.FileAccess access)
            {
                if (de.IsDirectory) throw new InvalidOperationException();
                return new FATVolume.fstr(de.Volume, de, access);
            }
            public int Size
            {
                get
                {
                    return (int)de.Size;
                }
            }
            public DateTime LastAccessDate
            {
                get
                {
                    return de.Access;
                }
                set
                {
                    de.Access = value;
                    de.UpdateTimes();
                }
            }
            public DateTime LastWriteTime
            {
                get
                {
                    return de.Write;
                }
                set
                {
                    de.Write = value;
                    de.UpdateTimes();
                }
            }
            public DateTime CreateTime
            {
                get
                {
                    return de.Create;
                }
                set
                {
                    de.Create = value;
                    de.UpdateTimes();
                }
            }
            public uint FirstCluster
            {
                get
                {
                    return de.Cluster;
                }
            }
            public bool IsDirectory
            {
                get
                {
                    return (de.DirAttr & FileAttributes.Directory) == FileAttributes.Directory;
                }
            }
        }
        public class FATException : Exception
        {
            public FATException(string message)
                : base(message)
            {
            }
            public FATException(string message, Exception innerException)
                : base(message, innerException)
            {
            }
        }
        public class FATVolume
        {
            #region Nested classes
            internal class DirEntry
            {
                public long Offset = 0;
                public long OffsetInDir = 0;
                public DirEntry Parent = null;
                public string FullPath = String.Empty;
                public byte[] Name = new byte[11];
                public string LongName = String.Empty;
                public string ShortName = String.Empty;
                public FileAttributes DirAttr = FileAttributes.None;
#if LONGNAMES
                public byte CheckSum = 0;
                public CaseInfo NameCase = CaseInfo.None;
                public int LongNameEntries = 0;
#endif
                public DateTime Create = DateTime.Now;
                public DateTime Access = DateTime.Now;
                public DateTime Write = DateTime.Now;
                public uint Cluster = 0;
                public uint LastCluster = 0;
                public uint Size = 0;
                public FATVolume Volume = null;
                public bool IsDirectory = false;
                public DirStream ParentStream = null;
                public DirStream Stream = null;
                void SeekPos(int pos)
                {
#if LONGNAMES
                    ParentStream.Seek(OffsetInDir + 32 * LongNameEntries + pos, System.IO.SeekOrigin.Begin);
#else
                    ParentStream.Seek(OffsetInDir + pos, System.IO.SeekOrigin.Begin);
#endif
                }
                public void SetCluster(uint c)
                {
                    Cluster = c;
                    SeekPos(20);
                    ParentStream.bw.Write((short)(Cluster >> 16));
                    SeekPos(26);
                    ParentStream.bw.Write((short)Cluster);
                }
                public void SetSize(uint s)
                {
                    Size = s;
                    SeekPos(28);
                    ParentStream.bw.Write(Size);
                }
                public void UpdateTimes()
                {
                    SeekPos(13);
                    byte tenth;
                    ushort time, date;
                    DateTime d = Create;
                    tenth = (byte)(d.Millisecond / 10 + (d.Second % 2) * 100);
                    time = (ushort)((d.Second / 2) | (d.Minute << 5) | (d.Hour << 11));
                    date = (ushort)((d.Day) | (d.Month << 5) | ((d.Year - 1980) << 9));
                    ParentStream.bw.Write(tenth);
                    ParentStream.bw.Write(time);
                    ParentStream.bw.Write(date);
                    d = Access;
                    date = (ushort)((d.Day) | (d.Month << 5) | ((d.Year - 1980) << 9));
                    ParentStream.bw.Write(date);
                    ParentStream.Seek(2, System.IO.SeekOrigin.Current);
                    d = Write;
                    time = (ushort)((d.Second / 2) | (d.Minute << 5) | (d.Hour << 11));
                    date = (ushort)((d.Day) | (d.Month << 5) | ((d.Year - 1980) << 9));
                    ParentStream.bw.Write(time);
                    ParentStream.bw.Write(date);
                }
                public void AccessNow()
                {
                    SeekPos(18);
                    DateTime d = DateTime.Now.Date;
                    Access = d;
                    ParentStream.bw.Write((ushort)((d.Day) | (d.Month << 5) | ((d.Year - 1980) << 9)));
                }
                public void WriteNow()
                {
                    SeekPos(22);
                    ushort time, date;
                    DateTime d = DateTime.Now;
                    Write = d;
                    time = (ushort)((d.Second / 2) | (d.Minute << 5) | (d.Hour << 11));
                    date = (ushort)((d.Day) | (d.Month << 5) | ((d.Year - 1980) << 9));
                    ParentStream.bw.Write(time);
                    ParentStream.bw.Write(date);
                }
                public void SetAttributes(FileAttributes attr)
                {
                    DirAttr = attr;
                    SeekPos(11);
                    ParentStream.bw.Write((byte)attr);
                }
                public override string ToString()
                {
                    return LongName;
                }
                public DirEntry(FATVolume volume)
                {
                    Volume = volume;
                }
            }
            // FAT file stream class
            internal class fstr : System.IO.Stream
            {
                FATVolume fv;
                DirEntry de;
                long pos;
                System.IO.FileAccess access;
                ClusterStream cs;
                public fstr(FATVolume parent, DirEntry de, System.IO.FileAccess access)
                {
                    fv = parent;
                    this.de = de;
                    cs = new ClusterStream(parent, de.Cluster);
                    Position = 0;
                    this.access = access;
                }
                public override bool CanRead
                {
                    get { return fv.fs.CanRead && access != System.IO.FileAccess.Write; }
                }

                public override bool CanSeek
                {
                    get { return true; }
                }

                public override bool CanWrite
                {
                    get { return fv.fs.CanWrite && access != System.IO.FileAccess.Read; }
                }

                public override void Flush()
                {
                    fv.fs.Flush();
                }

                public override long Length
                {
                    get { return de.Size; }
                }

                public override long Position
                {
                    get
                    {
                        return pos;
                    }
                    set
                    {
                        if (value > Length)
                            throw new System.IO.EndOfStreamException();
                        pos = value;
                    }
                }

                public override int Read(byte[] buffer, int offset, int count)
                {
                    if (!CanRead) throw new System.IO.IOException();
                    if ((pos + count) > de.Size)
                        count = (int)(de.Size - pos);
                    cs.Position = pos;
                    int c = cs.Read(buffer, offset, count);
                    pos += c;
                    return c;
                }

                public override long Seek(long offset, System.IO.SeekOrigin origin)
                {
                    switch (origin)
                    {
                        case System.IO.SeekOrigin.Begin:
                            Position = offset;
                            break;
                        case System.IO.SeekOrigin.Current:
                            Position = pos + offset;
                            break;
                        case System.IO.SeekOrigin.End:
                            Position = Length - offset;
                            break;
                        default:
                            throw new ArgumentException();
                    }
                    return pos;
                }

                public override void SetLength(long value)
                {
                    throw new NotImplementedException();
                }

                public override void Write(byte[] buffer, int offset, int count)
                {
                    if (!CanWrite) throw new System.IO.IOException();
                    de.WriteNow();
                    // Size of the file after write
                    uint newsize = (uint)Math.Max(de.Size, pos + count);
                    cs.Position = pos;
                    cs.Write(buffer, offset, count);
                    // Update size field
                    if (de.Size != newsize)
                    {
                        /*
#if LONGNAMES
                        fv.fs.Seek(de.Offset + 28 + 32 * de.LongNameEntries, System.IO.SeekOrigin.Begin);
#else
                        fv.fs.Seek(de.Offset + 28, System.IO.SeekOrigin.Begin);
#endif
                        fv.bw.Write(newsize);*/
                        de.SetSize(newsize);
                    }
                    // Update cluster field
                    if (de.Cluster == 0 && cs.StartCluster != 0)
                    {
                        /*de.Cluster = cs.StartCluster;
#if LONGNAMES
                        fv.fs.Seek(de.Offset + 20 + 32 * de.LongNameEntries, System.IO.SeekOrigin.Begin);
#else
                        fv.fs.Seek(de.Offset + 20, System.IO.SeekOrigin.Begin);
#endif
                        fv.bw.Write((ushort)(de.Cluster >> 16) & 0x0FFF);
#if LONGNAMES
                        fv.fs.Seek(de.Offset + 26 + 32 * de.LongNameEntries, System.IO.SeekOrigin.Begin);
#else
                        fv.fs.Seek(de.Offset + 26, System.IO.SeekOrigin.Begin);
#endif
                        fv.bw.Write((ushort)de.Cluster);*/
                        de.SetCluster(cs.StartCluster);
                    }
                    pos += count;
                }

                public override void Close()
                {
                    // Calls the base class method, NOT the Volume stream or Cluster stream method.
                    base.Close();
                }
            }
            class ClusterStream : System.IO.Stream
            {
                FATVolume fv;
                long pos;
                public uint StartCluster;
                uint clus;
                long vpos;
                uint startpos = 0;
                Stack<uint> st = new Stack<uint>();
                public ClusterStream(FATVolume parent, uint cluster)
                {
                    fv = parent;
                    clus = cluster;
                    Position = 0;
                    StartCluster = clus;
                }
                public ClusterStream(FATVolume parent, long volumeOffset)
                {
                    fv = parent;
                    uint datapos = (uint)(volumeOffset - fv.dataStart * fv.BytesPerSector);
                    clus = datapos / fv.BytesPerCluster + 2;
                    startpos = datapos % fv.BytesPerCluster;
                    Position = 0; // Will automatically account for startpos
                    StartCluster = clus;
                }
                public override bool CanRead
                {
                    get { return true; }
                }

                public override bool CanSeek
                {
                    get { return true; }
                }

                public override bool CanWrite
                {
                    get { return true; }
                }

                public override void Flush()
                {
                    fv.fs.Flush();
                }

                public override long Length
                {
                    get { throw new InvalidOperationException(); }
                }

                public override long Position
                {
                    get
                    {
                        return pos;
                    }
                    set
                    {
                        if (clus == 0)
                        {
                            if (value != 0)
                                throw new System.IO.EndOfStreamException();
                            return;
                        }
                        long iclus = (value + startpos) / fv.BytesPerCluster;
                        long off = (value + startpos) % fv.BytesPerCluster;
                        while (iclus > st.Count)
                        {
                            if (fv.eof(clus)) throw new System.IO.EndOfStreamException();
                            st.Push(clus);
                            clus = fv.NextCluster(clus);
                        }
                        while (iclus < st.Count)
                            clus = st.Pop();
                        if (!fv.eof(clus))
                        {
                            vpos = (fv.dataStart + fv.SectorsPerCluster * (clus - 2)) * fv.BytesPerSector + off;
                            fv.fs.Seek(vpos, System.IO.SeekOrigin.Begin);
                        }
                        pos = value;
                    }
                }

                public bool IsEof
                {
                    get
                    {
                        return clus == 0 || fv.eof(clus);
                    }
                }

                public override int Read(byte[] buffer, int offset, int count)
                {
                    if (clus == 0) return 0;
                    uint bytes = 0;
                    while (bytes < count)
                    {
                        if ((clus == 0) || fv.eof(clus)) return (int)bytes;
                        uint t = (uint)Math.Min(fv.BytesPerCluster - (pos % fv.BytesPerCluster), count - bytes);
                        fv.fs.Seek(vpos, System.IO.SeekOrigin.Begin);
                        uint r = (uint)fv.fs.Read(buffer, (int)(offset + bytes), (int)t);
                        bytes += r;
                        Position = pos + r;
                        if (r < t)
                            return (int)bytes;
                    }
                    return (int)bytes;
                }

                public override long Seek(long offset, System.IO.SeekOrigin origin)
                {
                    switch (origin)
                    {
                        case System.IO.SeekOrigin.Begin:
                            Position = offset;
                            break;
                        case System.IO.SeekOrigin.Current:
                            Position = pos + offset;
                            break;
                        case System.IO.SeekOrigin.End:
                            Position = Length - offset;
                            break;
                        default:
                            throw new ArgumentException();
                    }
                    return pos;
                }

                public override void SetLength(long value)
                {
                    throw new NotImplementedException();
                }

                public override void Write(byte[] buffer, int offset, int count)
                {
                    int bytes = 0;
                    while (bytes < count)
                    {
                        if ((clus == 0) || (fv.eof(clus)))
                        {
                            uint a = fv.AllocCluster();
                            if (a == 0) throw new FATException("Disk image is full.");
                            if (clus == 0)
                                StartCluster = a;
                            else
                                fv.SetNextCluster(st.Peek(), a);
                            fv.SetNextCluster(a, 0xFFFFFFFF);
                            clus = a;
                            Position = Position;
                        }
                        int t = (int)Math.Min(fv.BytesPerCluster - (pos % fv.BytesPerCluster), count - bytes);
                        fv.fs.Seek(vpos, System.IO.SeekOrigin.Begin);
                        fv.fs.Write(buffer, offset + bytes, t);
                        bytes += t;
                        Position = pos + t;
                    }
                }

                public override void Close()
                {
                    base.Close();
                }
            }
            internal class DirStream : System.IO.Stream
            {
                FATVolume fv;
                System.IO.Stream s;
                bool isCluster = true;
                internal System.IO.BinaryReader br;
                internal System.IO.BinaryWriter bw;
                public DirStream(FATVolume parent, uint cluster)
                {
                    fv = parent;
                    s = new ClusterStream(parent, cluster);
                    br = new System.IO.BinaryReader(s, Encoding.Unicode);
                    bw = new System.IO.BinaryWriter(s, Encoding.Unicode);
                }
                public DirStream(FATVolume parent, long volumeOffset)
                {
                    fv = parent;
                    isCluster = volumeOffset >= (parent.dataStart * parent.BytesPerSector);
                    s = isCluster ? (System.IO.Stream)new ClusterStream(parent, volumeOffset) : (System.IO.Stream)new VolStream(parent.fs, volumeOffset, (long)parent.dataStart * parent.BytesPerSector - volumeOffset);
                    br = new System.IO.BinaryReader(s, Encoding.Unicode);
                    bw = new System.IO.BinaryWriter(s, Encoding.Unicode);
                }
                public override bool CanRead
                {
                    get { return true; }
                }

                public override bool CanSeek
                {
                    get { return true; }
                }

                public override bool CanWrite
                {
                    get { return true; }
                }

                public override void Flush()
                {
                    fv.fs.Flush();
                }

                public override long Length
                {
                    get { throw new InvalidOperationException(); }
                }

                public override long Position
                {
                    get
                    {
                        return s.Position;
                    }
                    set
                    {
                        s.Position = value;
                    }
                }

                public bool IsEof
                {
                    get
                    {
                        return isCluster ? ((ClusterStream)s).IsEof : s.Position == s.Length;
                    }
                }

                public override int Read(byte[] buffer, int offset, int count)
                {
                    s.Position = s.Position;
                    return s.Read(buffer, offset, count);
                }

                public override long Seek(long offset, System.IO.SeekOrigin origin)
                {
                    return s.Seek(offset, origin);
                }

                public override void SetLength(long value)
                {
                    s.SetLength(value);
                }

                public override void Write(byte[] buffer, int offset, int count)
                {
                    s.Position = s.Position;
                    s.Write(buffer, offset, count);
                }

                public override void Close()
                {
                    base.Close();
                }

                public DirEntry ReadDirEntry(string Pathname)
                {
                    StringBuilder sb;
                    DirEntry de = new DirEntry(fv);
                    // to make sure fv.fs.Position is in the right place:
                    Position = Position;
                    de.Offset = fv.fs.Position;
                    de.OffsetInDir = Position;
                    de.ParentStream = this;
                    EntryInfo ei = EntryInfo();
                    if ((ei.type == EntryType.None) || (ei.type == EntryType.Erased) || ei.type == EntryType.Unknown) return null;
#if LONGNAMES
                    if (ei.type == EntryType.LongnameEntry)
                    {
                        if ((ei.Ord & 0x40) == 0) throw new FATException("Invalid long filename entry.");
                        int count = ei.Ord & 0xBF, checksum = 0;
                        de.LongNameEntries = count;
                        char[] arr = new char[count * 13];
                        for (int i = 0; i < count; i++)
                        {
                            ei = EntryInfo();
                            if (((i != 0) && (ei.Ord != (count - i))) || (ei.type != EntryType.LongnameEntry))
                                throw new FATException("Invalid long filename entry.");
                            Seek(1, System.IO.SeekOrigin.Current);
                            int j, off;
                            off = count * 13 - (i + 1) * 13;
                            for (j = 0; j < 5; j++)
                                arr[off + j] = br.ReadChar();
                            Seek(2, System.IO.SeekOrigin.Current);
                            if (i == 0)
                                checksum = br.ReadByte();
                            else
                                if (checksum != br.ReadByte()) throw new FATException("Inconsistent checksums in long name entries.");
                            off += 5;
                            for (j = 0; j < 6; j++)
                                arr[off + j] = br.ReadChar();
                            Seek(2, System.IO.SeekOrigin.Current);
                            off += 6;
                            for (j = 0; j < 2; j++)
                                arr[off + j] = br.ReadChar();
                        }
                        sb = new StringBuilder(count * 13);
                        sb.Append(arr);
                        de.LongName = sb.ToString();
                        if (de.LongName.IndexOf('\0') != -1) de.LongName = de.LongName.Remove(de.LongName.IndexOf('\0'));
                        br.Read(de.Name, 0, 11);
                        de.CheckSum = CheckSum(de.Name);
                        if (de.CheckSum != checksum) // orphaned entry
                        {
                            de.LongNameEntries = 0;
                            de.LongName = String.Empty;
                            Seek(-(count * 32 + 11), System.IO.SeekOrigin.Current);
                            for (int i = 0; i < count; i++)
                            {
                                bw.Write((byte)0xE5);
                                Seek(31, System.IO.SeekOrigin.Current);
                            }
                            Seek(11, System.IO.SeekOrigin.Current);
                        }
                    }
                    else
                    {
#endif
                        br.Read(de.Name, 0, 11);
#if LONGNAMES
                        de.CheckSum = CheckSum(de.Name);
                    }
#endif
                    de.DirAttr = (FileAttributes)br.ReadByte();
#if LONGNAMES
                    de.NameCase = (CaseInfo)
#endif
br.ReadByte();
                    int tenth, time, date;
                    tenth = (int)br.ReadByte();
                    time = (int)br.ReadUInt16();
                    date = (int)br.ReadUInt16();
                    de.Create = new DateTime(1980 + (date >> 9), Math.Max((date >> 5) & 0xF, 1), Math.Max(date & 0x1F, 1), time >> 11, (time >> 5) & 0x3F, (time & 0x1F) * 2 + tenth / 100, (tenth % 100) * 10);
                    date = (int)br.ReadUInt16();
                    de.Access = new DateTime(1980 + (date >> 9), Math.Max((date >> 5) & 0xF, 1), Math.Max(date & 0x1F, 1));
                    de.Cluster = (uint)br.ReadUInt16() << 16;
                    time = (int)br.ReadUInt16();
                    date = (int)br.ReadUInt16();
                    de.Write = new DateTime(1980 + (date >> 9), Math.Max((date >> 5) & 0xF, 1), Math.Max(date & 0x1F, 1), time >> 11, (time >> 5) & 0x3F, (time & 0x1F) * 2);
                    de.Cluster |= br.ReadUInt16();
                    de.Size = br.ReadUInt32();
                    sb = new StringBuilder(12);
                    StringBuilder sbb = new StringBuilder(8);
                    StringBuilder sbe = new StringBuilder(3);
                    for (int i = 0; i < 8; i++)
                    {
                        char c = (char)de.Name[i];
                        if (c == '\x20') continue;
                        sbb.Append(c);
                    }
#if LONGNAMES
                    sb.Append((de.NameCase & CaseInfo.LowerBase) == CaseInfo.LowerBase ? sbb.ToString().ToLower() : sbb.ToString());
#else
                sb.Append(sbb.ToString());
#endif
                    for (int i = 8; i < 11; i++)
                    {
                        char c = (char)de.Name[i];
                        if (c == '\x20') continue;
                        sbe.Append(c);
                    }
                    if (sbe.Length != 0)
                    {
                        sb.Append('.');
#if LONGNAMES
                        sb.Append((de.NameCase & CaseInfo.LowerExt) == CaseInfo.LowerExt ? sbe.ToString().ToLower() : sbe.ToString());
#else
                    sb.Append(sbe.ToString());
#endif
                    }
                    de.ShortName = sb.ToString().ToUpper();
                    if (String.IsNullOrEmpty(de.LongName))
                        de.LongName = sb.ToString();
                    de.FullPath = Pathname + de.LongName;
                    if ((de.DirAttr & FileAttributes.Directory) == FileAttributes.Directory)
                    {
                        de.IsDirectory = true;
                        de.Stream = new DirStream(fv, de.Cluster);
                    }
                    return de;
                }

                public EntryInfo EntryInfo()
                {
                    EntryInfo y = new EntryInfo();
                    y.Ord = br.ReadByte();
                    Seek(10, System.IO.SeekOrigin.Current);
                    bool x = (br.ReadByte() & 0xF) == 0xF;
                    Seek(-12, System.IO.SeekOrigin.Current);
                    if (y.Ord == 0)
                        y.type = EntryType.None;
                    else if (y.Ord == 0xE5)
                        y.type = EntryType.Erased;
                    else if (x)
#if LONGNAMES
                        y.type = EntryType.LongnameEntry;
#else
                    y.type = EntryType.Unknown;
#endif
                    else
                        y.type = EntryType.NormalEntry;
                    return y;
                }

#if LONGNAMES
                int EntriesForEntry(DirEntry de)
                {
                    CaseInfo ci;
                    bool lossy, fits;
                    FATVolume.name83(de.LongName, out lossy, out fits, out ci);
                    if (lossy || !fits)
                    {
                        return 1 + (de.LongName.Length + 12) / 13;
                    }
                    else return 1;
                }
#endif

                // Returns the entry number for an entry or -1.
                int MakeRoomForEntry(DirEntry de)
                {
                    Position = 0;
#if LONGNAMES
                    // Windows tries to add the entry to the end of the directory,
                    // only overwriting deleted entries when there is no more space.
                    // We will just find the first available space and use it.
                    int total = 0;
                    for (
                        int start = -1, i = 0, cnt = EntriesForEntry(de);
                        !IsEof;
                        total++, Seek(32, System.IO.SeekOrigin.Current)
                        )
                    {
                        EntryInfo ei = EntryInfo();
                        if (ei.Ord == 0)
                        {
                            if (!isCluster && start != -1 && (start + cnt) * 32 <= s.Length)
                                return start;
                            else if (isCluster || ((Position + cnt * 32) <= s.Length))
                                return total;
                            else
                                return -1;
                        }
                        i++;
                        if (ei.type != EntryType.Erased) { i = 0; start = -1; }
                        if (i == 1 && start == -1) start = total;
                        if (i == cnt) return start;
                    }
                    return isCluster ? total : -1;
#else
                    for (int i = 0; !IsEof; i++, Seek(32, System.IO.SeekOrigin.Current))
                    {
                        EntryInfo ei = EntryInfo();
                        if (ei.type == EntryType.Erased || ei.type == EntryType.None)
                            return i;
                    }
                    return -1;
#endif
                }

#if LONGNAMES
                // Creates a short name for the entry, and returns whether
                // a long name needs to be created
                bool MakeName(DirEntry de)
#else
                // Creates a short name for the entry.
                void MakeName(DirEntry de)
#endif
                {
                    bool lossy, fits;
#if LONGNAMES
                    de.Name = name83(de.LongName, out lossy, out fits, out de.NameCase);
#else
                    de.Name = name83(de.LongName, out lossy, out fits);
#endif
                    if (!fits || fv.FindShort(this, de.Name, "") != null)
                    {
                        de.Name = fv.FindTail(this, de.Name, "");
                    }
#if LONGNAMES
                    return (lossy || !fits);
#endif
                }

                public void CreateEntry(DirEntry de)
                {
                    int pos = MakeRoomForEntry(de);
                    if (pos == -1) throw new FATException("Directory is full.");
                    // Get entry ready
#if LONGNAMES
                    bool longname = MakeName(de);
                    de.CheckSum = CheckSum(de.Name);
                    de.ShortName = ShortName(de.Name, de.NameCase);
#else
                    MakeName(de);
                    de.ShortName = ShortName(de.Name);
#endif
                    Position = (long)pos * 32L;
                    de.ParentStream  = this;
                    de.OffsetInDir = Position;
                    de.Offset = fv.fs.Position;
                    de.Size = 0;
                    if ((de.DirAttr & FileAttributes.VolumeLabel) == FileAttributes.VolumeLabel || de.Name[0] == '\x2E')
                    {
                        de.Create = de.Access = de.Write = new DateTime(1980, 1, 1);
                    }
                    else
                    {
                        de.Create = DateTime.Now;
                        de.Access = de.Create.Date;
                        de.Write = de.Create;
                    }
                    de.IsDirectory = (de.DirAttr & (FileAttributes.Directory | FileAttributes.VolumeLabel)) == FileAttributes.Directory;
                    if (de.IsDirectory && de.Name[0] != '\x2E')
                    {
                        de.Cluster = de.LastCluster = fv.AllocCluster();
                        fv.SetNextCluster(de.Cluster, 0xFFFFFFFF);
                    }
                    // Start writing
                    Position = (long)pos * 32L;
#if LONGNAMES
                    if (longname)
                    {
                        int count = (de.LongName.Length + 12) / 13;
                        de.LongNameEntries = count;
                        for (int i = 0; i < count; i++)
                        {
                            bw.Write((byte)(i == 0 ? 0x40 | count : count - i));
                            int offset = count * 13 - (i + 1) * 13;
                            for (int j = offset; j < offset + 5; j++)
                                bw.Write(j > de.LongName.Length ? '\xFFFF' : j == de.LongName.Length ? '\0' : de.LongName[j]);
                            bw.Write((ushort)0x000F);
                            bw.Write(de.CheckSum);
                            offset += 5;
                            for (int j = offset; j < offset + 6; j++)
                                bw.Write(j > de.LongName.Length ? '\xFFFF' : j == de.LongName.Length ? '\0' : de.LongName[j]);
                            bw.Write((ushort)0);
                            offset += 6;
                            for (int j = offset; j < offset + 2; j++)
                                bw.Write(j > de.LongName.Length ? '\xFFFF' : j == de.LongName.Length ? '\0' : de.LongName[j]);
                        }
                    }
#endif
                    bw.Write(de.Name, 0, 11);
                    bw.Write((byte)de.DirAttr);
#if LONGNAMES
                    bw.Write((byte)de.NameCase);
#else
                    bw.Write((byte)0);
#endif
                    byte tenth;
                    ushort time, date;
                    DateTime d = de.Create;
                    tenth = (byte)(d.Millisecond / 10 + (d.Second % 2) * 100);
                    time = (ushort)((d.Second / 2) | (d.Minute << 5) | (d.Hour << 11));
                    date = (ushort)((d.Day) | (d.Month << 5) | ((d.Year - 1980) << 9));
                    bw.Write(tenth);
                    bw.Write(time);
                    bw.Write(date);
                    d = de.Access;
                    date = (ushort)((d.Day) | (d.Month << 5) | ((d.Year - 1980) << 9));
                    bw.Write(date);
                    bw.Write((ushort)(de.Cluster >> 16));
                    d = de.Write;
                    time = (ushort)((d.Second / 2) | (d.Minute << 5) | (d.Hour << 11));
                    date = (ushort)((d.Day) | (d.Month << 5) | ((d.Year - 1980) << 9));
                    bw.Write(time);
                    bw.Write(date);
                    bw.Write((ushort)(de.Cluster));
                    bw.Write(de.Size);
                    if (de.IsDirectory && de.Name[0] != '\x2E')
                    {
                        de.Stream = new DirStream(fv, de.Cluster);
                        de.Stream.Position = 0;
                        de.Stream.Write(new byte[(int)fv.BytesPerCluster], 0, (int)fv.BytesPerCluster);
                        DirEntry de1 = new DirEntry(fv);
                        de1.Cluster = de.Cluster;
                        de1.LongName = ".";
                        de1.DirAttr = FileAttributes.Directory;
                        de.Stream.CreateEntry(de1);
                        de1 = new DirEntry(fv);
                        de1.Cluster = isCluster ? ((ClusterStream)s).StartCluster == fv.RootClus ? 0 : ((ClusterStream)s).StartCluster : 0;
                        de1.LongName = "..";
                        de1.DirAttr = FileAttributes.Directory;
                        de.Stream.CreateEntry(de1);
                    }
                    bw.Flush();
                }
            }
            #endregion
            #region Members
            uint BytesPerSector;
            uint SectorsPerCluster;
            uint ReservedSectors;
            uint NumFATs;
            uint RootEntries;
            uint TotalSectors;
            uint MediaDescriptor;
            uint FATSectors;
            uint SectorsPerTrack;
            uint Heads;
            uint HiddenSectors;
            uint FATNumber;
            bool MirrorFAT;
            uint FSVer;
            uint RootClus;
            uint FSInfo;
            FATType fatType;
            uint RootSectors;
            uint BytesPerCluster;
            uint dataStart;
            uint rootStart;
            System.IO.Stream fs;
            System.IO.BinaryReader br;
            System.IO.BinaryWriter bw;
            uint ClusterCount;
            DirStream rootStream;
            #endregion
            #region Methods
            void SeekCluster(uint Cluster)
            {
                Cluster = Cluster & 0x0FFFFFFF;
                if (Cluster < 2)
                    throw new FATException("Bad cluster number.");
                fs.Seek(dataStart * BytesPerSector + (Cluster - 2) * BytesPerCluster, System.IO.SeekOrigin.Begin);
            }
            bool eof(uint cluster)
            {
                switch (fatType)
                {
                    case FATType.FAT12:
                        return cluster >= 0xFF8;
                    case FATType.FAT16:
                        return cluster >= 0xFFF8;
                    case FATType.FAT32:
                        return (cluster & 0x0FFFFFFF) >= 0x0FFFFFF8;
                }
                return true;
            }
            uint NextCluster(uint Cluster)
            {
                Cluster = Cluster & 0x0FFFFFFF;
                if (Cluster < 2)
                    throw new FATException("Bad cluster number.");
                uint start, end;
                if (MirrorFAT)
                {
                    start = 0;
                    end = NumFATs;
                }
                else
                    end = (start = FATNumber) + 1; // intentional; start = FATNumber; end = start + 1;
                uint lastval = 0;
                for (uint i = start; i < end; i++)
                {
                    uint ccluster;
                    fs.Seek((ReservedSectors + FATSectors * i) * BytesPerSector, System.IO.SeekOrigin.Begin);
                    if (fatType == FATType.FAT12)
                    {
                        fs.Seek(Cluster + Cluster / 2, System.IO.SeekOrigin.Current);
                        ccluster = br.ReadUInt16();
                        if (((Cluster + Cluster / 2) & 1) == 0)
                            ccluster &= 0xFFF;
                        else
                            ccluster >>= 4;
                    }
                    else if (fatType == FATType.FAT16)
                    {
                        fs.Seek(Cluster * 2, System.IO.SeekOrigin.Current);
                        ccluster = br.ReadUInt16();
                    }
                    else // FAT32
                    {
                        fs.Seek(Cluster * 4, System.IO.SeekOrigin.Current);
                        ccluster = br.ReadUInt32() & 0x0FFFFFFF;
                    }
                    if ((i != 0) && (ccluster != lastval))
                        throw new FATException("Error in clusterchain.");
                    lastval = ccluster;
                }
                return lastval;
            }
            void SetNextCluster(uint Cluster, uint next)
            {
                Cluster = Cluster & 0x0FFFFFFF;
                if (Cluster < 2)
                    throw new FATException("Bad cluster number.");
                uint start, end;
                if (MirrorFAT)
                {
                    start = 0;
                    end = NumFATs;
                }
                else
                    start = end = FATNumber;
                for (uint i = start; i < end; i++)
                {
                    fs.Seek((ReservedSectors + FATSectors * i) * BytesPerSector, System.IO.SeekOrigin.Begin);
                    if (fatType == FATType.FAT12)
                    {
                        fs.Seek(Cluster + Cluster / 2, System.IO.SeekOrigin.Current);
                        ushort t = br.ReadUInt16();
                        fs.Seek(-2, System.IO.SeekOrigin.Current);
                        if (((Cluster + Cluster / 2) & 1) == 0)
                            bw.Write((ushort)((t & 0xF000) | ((ushort)next & 0x0FFF)));
                        else
                            bw.Write((ushort)((t & 0x000F) | (ushort)(next << 4)));
                    }
                    else if (fatType == FATType.FAT16)
                    {
                        fs.Seek(Cluster * 2, System.IO.SeekOrigin.Current);
                        bw.Write((ushort)next);
                    }
                    else // FAT32
                    {
                        fs.Seek(Cluster * 4, System.IO.SeekOrigin.Current);
                        bw.Write((uint)next);
                    }
                }
            }
            uint AllocCluster()
            {
                for (uint i = 2; i < ClusterCount + 2; i++)
                {
                    try
                    {
                        uint n = NextCluster(i);
                        if (n == 0) return i;
                    }
                    catch (FATException e)
                    {
                        throw new FATException(string.Format("Error in the FAT: {0}", e.Message), e);
                    }
                }
                throw new FATException("No free space on disk image.");
            }
#if LONGNAMES
            static byte CheckSum(byte[] name)
            {
                byte sum = 0;
                for (int i = 0; i < 11; i++)
                    // Rotate sum right 1 bit, then add the character
                    sum = (byte)(((sum << 7) | (sum >> 1)) + name[i]);
                return sum;
            }
#endif
            static byte CharToByte(char c, ref bool lossy)
            {
                // Characters allowed in long names, that are converted to an '_' in short names
                if ((c > 255) ||
                    (c < 32) ||
                    (c == 127) ||
                    // Allowable in short names: >127,QWERTYUIOPASDFGHJKLZXCVBNM1234567890[space]`~!@#$%^&()-_{}'
                    // Not allowable in short names: +,;[=].*/|\":<>?
                    // Allowable in long names: +,;[=].
                    "+,;[=].".Contains(c.ToString()))
                {
                    lossy = true;
                    return (byte)'_';
                }
                return (byte)Char.ToUpper(c);
            }
#if LONGNAMES
            static byte[] name83(string name, out bool lossy, out bool fits, out CaseInfo caseInfo)
#else
            static byte[] name83(string name, out bool lossy, out bool fits)
#endif
            {
                lossy = false;
                fits = true;
#if LONGNAMES
                caseInfo = (CaseInfo)0;
#endif
                if (String.Compare(name, ".", StringComparison.InvariantCulture) == 0)
                    return new byte[] { 0x2E, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, };
                if (String.Compare(name, "..", StringComparison.InvariantCulture) == 0)
                    return new byte[] { 0x2E, 0x2E, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, };
                byte[] nameBytes = new byte[] { 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, };
                int i;
                int baseBytes = 0, extBytes = 0;
                for (i = 0; baseBytes < 8 && i < name.Length; i++)
                {
                    if (name[i] == '\x20') continue;
                    if (name[i] == '\x2E') break;
                    if ((baseBytes == 0) && (name[i] == '\xE5'))
                        nameBytes[0] = 0x05;
                    else
                        nameBytes[baseBytes] = CharToByte(name[i], ref lossy);
                    #if LONGNAMES
                    if ((baseBytes == 0) && char.IsLower(name[i])) caseInfo |= CaseInfo.LowerBase;
                    else if (char.IsLower(name[i]) && ((caseInfo & CaseInfo.LowerBase) != CaseInfo.LowerBase)) lossy = true;
                    #endif
                    baseBytes++;
                }
                if (i == name.Length) goto Return;
                if (name[i] != '\x2E')
                {
                    fits = false;
                    lossy = true;
                }
                if (!name.Contains("\x2E")) goto Return;
                for (i = name.LastIndexOf('\x2E') + 1; extBytes < 3 && i < name.Length; i++)
                {
                    if (name[i] == '\x20') continue;
                    nameBytes[extBytes + 8] = CharToByte(name[i], ref lossy);
#if LONGNAMES
                    if ((extBytes == 0) && char.IsLower(name[i])) caseInfo |= CaseInfo.LowerExt;
                    else if (char.IsLower(name[i]) && ((caseInfo & CaseInfo.LowerExt) != CaseInfo.LowerExt)) lossy = true;
#endif
                    extBytes++;
                }
                if (i != name.Length)
                {
                    fits = false;
                    lossy = true;
                }
            Return:
#if LONGNAMES
                if (lossy) caseInfo = (CaseInfo)0;
#endif
                return nameBytes;
            }
#if LONGNAMES
            static string ShortName(byte[] name, CaseInfo nameCase)
#else
            static string ShortName(byte[] name)
#endif
            {
                StringBuilder sb = new StringBuilder(12);
                StringBuilder sbb = new StringBuilder(8);
                StringBuilder sbe = new StringBuilder(3);
                for (int i = 0; i < 8; i++)
                {
                    char c = (char)name[i];
                    if (c == '\x20') continue;
                    sbb.Append(c);
                }
#if LONGNAMES
                sb.Append((nameCase & CaseInfo.LowerBase) == CaseInfo.LowerBase ? sbb.ToString().ToLower() : sbb.ToString());
#else
                sb.Append(sbb.ToString());
#endif
                for (int i = 8; i < 11; i++)
                {
                    char c = (char)name[i];
                    if (c == '\x20') continue;
                    sbe.Append(c);
                }
                if (sbe.Length != 0)
                {
                    sb.Append('.');
#if LONGNAMES
                    sb.Append((nameCase & CaseInfo.LowerExt) == CaseInfo.LowerExt ? sbe.ToString().ToLower() : sbe.ToString());
#else
                    sb.Append(sbe.ToString());
#endif
                }
                return sb.ToString();
            }
            static bool validPathName(string name)
            {
                for (int i = 0; i < name.Length; i++)
                {
                    char c = name[i];
                    if ((c < 32) || (c == 127) || "*|\":<>?".Contains(c.ToString())) return false;
                }
                return true;
            }
            static bool validName(string name)
            {
                for (int i = 0; i < name.Length; i++)
                {
                    char c = name[i];
                    if ((c < 32) || (c == 127) || "*/|\\\":<>?".Contains(c.ToString())) return false;
                }
                return true;
            }
            List<uint> getClusters(uint cluster)
            {
                List<uint> clusters = new List<uint>();
                while (!eof(cluster))
                {
                    clusters.Add(cluster);
                    cluster = NextCluster(cluster);
                }
                //for (uint cluster = de.Cluster; !eof(cluster); cluster = NextCluster(cluster))
                return clusters;
            }
            bool NamesEqual(byte[] name1, byte[] name2)
            {
                for (int i = 0; i < 11; i++)
                    if (name1[i] != name2[i]) return false;
                return true;
            }
            static byte[] MakeTail(byte[] name, int number)
            {
                byte[] newname = (byte[])name.Clone();
                string n = "~" + number.ToString();
                for (int i = 8 - n.Length, j = 0; i < 8; i++, j++)
                    newname[i] = (byte)n[j];
                return newname;
            }
            DirEntry FindFile(DirStream ds, string name, string path)
            {
                ds.Position = 0;
                while (!ds.IsEof)
                {
                    DirEntry de = ds.ReadDirEntry(path);
                    if (de == null)
                    {
                        ds.Seek(32, System.IO.SeekOrigin.Current);
                        continue;
                    }
                    if (String.Equals(name, de.LongName, StringComparison.InvariantCultureIgnoreCase) ||
                        String.Equals(name, de.ShortName, StringComparison.InvariantCultureIgnoreCase))
                        return de;
                }
                return null;
            }
            List<DirEntry> Entries(DirStream ds, string path)
            {
                List<DirEntry> del = new List<DirEntry>();
                ds.Position = 0;
                while (!ds.IsEof)
                {
                    EntryInfo ei = ds.EntryInfo();
                    if ((ei.type == EntryType.None)) goto ret;
                    if ((ei.type == EntryType.Unknown) || ei.type == EntryType.Erased)
                    {
                        ds.Seek(32, System.IO.SeekOrigin.Current);
                        continue;
                    }
                    del.Add(ds.ReadDirEntry(path));
                }
            ret:
                return del;
            }
            DirEntry FindShort(DirStream ds, byte[] name, string path)
            {
                ds.Position = 0;
                while (!ds.IsEof)
                {
                    EntryInfo ei = ds.EntryInfo();
                    if ((ei.type == EntryType.None)) goto ret;
                    if ((ei.type == EntryType.Unknown) || ei.type == EntryType.Erased)
                    {
                        ds.Seek(32, System.IO.SeekOrigin.Current);
                        continue;
                    }
                    DirEntry de = ds.ReadDirEntry(path);
                    if (NamesEqual(name, de.Name))
                        return de;
                }
                ret: return null;
            }
            byte[] FindTail(DirStream ds, byte[] name, string path)
            {
                byte[] x = (byte[])name.Clone();
                int i = 1;
                do
                {
                    x = MakeTail(x, i++);
                } while (FindShort(ds, x, path) != null);
                return x;
            }
            DirEntry GetFile(string name)
            {
                DirEntry de = GetEntry(name);
                if (de == null) return null;
                if ((de.DirAttr & (FileAttributes.Directory | FileAttributes.VolumeLabel)) != FileAttributes.None)
                    return null;
                return de;
            }
            DirEntry GetDir(string name)
            {
                DirEntry de = GetEntry(name);
                if (de == null) return null;
                if ((de.DirAttr & (FileAttributes.Directory | FileAttributes.VolumeLabel)) != FileAttributes.Directory)
                    return null;
                return de;
            }
            DirEntry GetEntry(string name)
            {
                if (!validPathName(name)) return null;
                DirEntry de = null, parent = null;
                string FullPath = "\\";
                int i = 0, j = 0;
                while (true)
                {
                    if ((j != name.Length) && ((name[j] == '\\') || (name[j] == '/')))
                        j++;
                    if (j == name.Length)
                    {
                        if (de == null) return null;
                        if ((de.DirAttr & FileAttributes.VolumeLabel) == FileAttributes.VolumeLabel)
                            return null;
                        return de;
                    }
                    StringBuilder sb = new StringBuilder(255);
                    while ((j < name.Length) && (name[j] != '\\') && (name[j] != '/'))
                    {
                        sb.Append(name[j++]);
                    }
                    if (i == 0)
                        de = FindFile(rootStream, sb.ToString(), "\\");
                    else
                        de = FindFile(de.Stream, sb.ToString(), FullPath);
                    if (de != null)
                        de.Parent = parent;
                    else
                        return null;
                    if (j != name.Length)
                    {
                        if ((de.DirAttr & (FileAttributes.Directory | FileAttributes.VolumeLabel)) == FileAttributes.Directory)
                            parent = de;
                        else
                            return null;
                        FullPath += de.LongName + "\\";
                    }
                    i++;
                }
            }
            public bool FileExists(string name)
            {
                return GetFile(name) != null;
            }
            public bool DirectoryExists(string name)
            {
                if (string.IsNullOrEmpty(name) || (name == "\\") || (name == "/"))
                    return true;
                return GetDir(name) != null;
            }
            public bool Exists(string name)
            {
                return GetEntry(name) != null;
            }
            // delete the file
            void delete(DirEntry de)
            {
                erase(de);
                if (de.IsDirectory)
                {
                    foreach (DirEntry de1 in Entries(de.Stream, de.FullPath))
                    {
                        if ((de1.DirAttr & FileAttributes.VolumeLabel) == FileAttributes.VolumeLabel || de1.Name[0] == '\x2E')
                            continue;
                        delete(de1);
                    }
                }
                unlink(de);
            }
            // just erase the entry
            void erase(DirEntry de)
            {
                DirStream fs = de.ParentStream;
                fs.Seek(de.OffsetInDir, System.IO.SeekOrigin.Begin);
#if LONGNAMES
                while (true)
                {
                    EntryInfo ei = fs.EntryInfo();
                    fs.bw.Write((byte)0xE5);
                    if (ei.type != EntryType.LongnameEntry) break;
                    fs.Seek(31, System.IO.SeekOrigin.Current);
                }
#else
                bw.Write((byte)0xE5);
#endif
            }
            // unlink the clusterchain
            void unlink(DirEntry de)
            {
                uint cluster = de.Cluster, nextcluster = cluster;
                if (cluster == 0) return;
                do
                {
                    cluster = nextcluster;
                    nextcluster = NextCluster(cluster);
                    SetNextCluster(cluster, 0);
                } while (!eof(nextcluster) && nextcluster != 0); // it shouldn't be 0, but just in case...

                de.SetSize(0);
                de.SetCluster(0);
            }
            DirEntry createDirectory(string name)
            {
                DirEntry de = GetDir(name);
                if (de != null) return de;
                name = name.Replace('\\', '/');
                while (name.EndsWith("/")) name = name.Remove(name.Length - 1);
                string dir = name.Contains("/") ? name.Substring(name.LastIndexOf("/") + 1) : name;
                DirStream ds;
                de = new DirEntry(this);
                if (!name.Contains("/") || name.LastIndexOf("/") == 0)
                    ds = rootStream;
                else
                {
                    string parent = name.Remove(name.LastIndexOf('/'));
                    DirEntry pd = GetDir(parent);
                    if (pd == null) pd = createDirectory(parent);
                    ds = pd.Stream;
                    de.Parent = pd;
                }
                de.DirAttr = FileAttributes.Archive | FileAttributes.Directory;
                de.FullPath = name;
                de.IsDirectory = true;
                de.LongName = dir;
                de.ParentStream = ds;
                ds.CreateEntry(de);
                return de;
            }
            public FileInfo GetFileInfo(string name)
            {
                DirEntry de = GetEntry(name);
                if (de == null)
                    throw new FATException("File does not exist.");
                return new FileInfo(de);
            }
            public string[] GetFiles(string directory)
            {
                List<string> strs = new List<string>();
                List<DirEntry> del;
                if (string.IsNullOrEmpty(directory) || (directory == "\\") || (directory == "/"))
                    del = Entries(rootStream, "\\");
                else
                {
                    DirEntry de1 = GetDir(directory);
                    if (de1 != null)
                        del = Entries(de1.Stream, de1.FullPath);
                    else
                        return new string[0];
                }
                foreach (DirEntry de in del)
                {
                    if ((de.DirAttr & (FileAttributes.Directory | FileAttributes.VolumeLabel)) == FileAttributes.None)
                        strs.Add(de.LongName);
                }
                return (strs.ToArray());
            }
            public string[] GetDirectories(string directory)
            {
                List<string> strs = new List<string>();
                List<DirEntry> del;
                if (string.IsNullOrEmpty(directory) || (directory == "\\") || (directory == "/"))
                    del = Entries(rootStream, "\\");
                else
                {
                    DirEntry de1 = GetDir(directory);
                    if (de1 != null)
                        del = Entries(de1.Stream, de1.FullPath);
                    else
                        return new string[0];
                }
                foreach (DirEntry de in del)
                {
                    if (((de.DirAttr & (FileAttributes.Directory | FileAttributes.VolumeLabel)) == FileAttributes.Directory)
                        && (de.Name[0] != '\x2E'))
                        strs.Add(de.LongName);
                }
                return (strs.ToArray());
            }
            public void CreateDirectory(string name)
            {
                createDirectory(name);
            }
            public void DeleteFile(string name)
            {
                DirEntry de = GetFile(name);
                if (de == null) throw new FATException("File does not exist.");
                delete(de);
            }
            public void DeleteFolder(string name)
            {
                DirEntry de = GetDir(name);
                if (de == null) throw new FATException("Directory does not exist.");
                delete(de);
            }
            public void Delete(string name)
            {
                DirEntry de = GetEntry(name);
                if (de == null) throw new FATException("File or directory does not exist.");
                delete(de);
            }
            public void Close()
            {
                bw.Flush();
                bw.Close();
            }
            public FileAttributes GetAttributes(string name)
            {
                DirEntry de = GetEntry(name);
                if (de == null) throw new System.IO.FileNotFoundException();
                return de.DirAttr;
            }
            public void SetAttributes(string name, FileAttributes attr)
            {
                DirEntry de = GetEntry(name);
                if (de == null) throw new System.IO.FileNotFoundException();
                de.SetAttributes(attr);
            }
            DirEntry createFile(string name)
            {
                name = name.Replace('\\', '/');
                string fname = name.Contains("/") ? name.Substring(name.LastIndexOf('/') + 1) : name;
                string parent = name.Contains("/") ? name.Remove(name.LastIndexOf('/')) : String.Empty;
                if (Exists(name)) Delete(name);
                DirStream ds;
                if (parent == String.Empty || parent == "/")
                    ds = rootStream;
                else
                {
                    DirEntry p = GetDir(parent);
                    if (p == null) throw new FATException(string.Format("The directory '{0}' does not exist.", parent));
                    ds = p.Stream;
                }
                DirEntry de = new DirEntry(this);
                de.LongName = fname;
                de.FullPath = name;
                de.DirAttr = FileAttributes.Archive;
                ds.CreateEntry(de);
                return de;
            }
            public System.IO.Stream OpenFile(string name, System.IO.FileAccess access, System.IO.FileMode mode)
            {
                DirEntry de = GetFile(name);
                // Check if the file exists
                switch (mode)
                {
                    case System.IO.FileMode.Open:
                    case System.IO.FileMode.Truncate:
                    case System.IO.FileMode.Append:
                        if (mode == System.IO.FileMode.Append && access != System.IO.FileAccess.Write) throw new System.IO.IOException("System.IO.FileMode.Append cannot be used in conjunction with System.IO.FileAccess." + access.ToString());
                        if (de == null) throw new System.IO.FileNotFoundException("The file does not exist.", name);
                        break;
                    case System.IO.FileMode.CreateNew:
                        if (de != null) throw new System.IO.IOException("File already exists.");
                        break;

                }
                switch (mode)
                {
                    case System.IO.FileMode.OpenOrCreate:
                        if (de != null) break;
                        de = createFile(name);
                        break;
                    case System.IO.FileMode.Create: // unlink or create
                    case System.IO.FileMode.CreateNew: // create
                    case System.IO.FileMode.Truncate: // unlink
                        if (de != null)
                            unlink(de);
                        else
                            de = createFile(name);
                        break;
                }
                de.AccessNow();
                fstr fs = new fstr(this, de, access);
                if (mode == System.IO.FileMode.Append)
                    fs.Seek(0, System.IO.SeekOrigin.End);
                return fs;
            }
            public long ComputeFreeSpace()
            {
                /* 
                 * Don't use NextCluster() here. Our goal is to compute the free space as quickly
                 * as possible. NextCluster() can be very slow. This function optimizes the task. 
                 * We don't look at all of the FATs. This is too redundant and slow to calculate
                 * free space, and it would be better to check for errors at a different time.
                 */
                uint clusters = 0;
                if (fatType == FATType.FAT12)
                {
                    for (uint i = 2; i < ClusterCount + 2; i++)
                    {
                        fs.Seek(ReservedSectors * BytesPerSector + (i + i / 2), System.IO.SeekOrigin.Begin);
                        if (((i + i / 2) & 1) == 0)
                        { if ((br.ReadUInt16() & 0xFFF) == 0) clusters++; }
                        else
                            if ((br.ReadUInt16() >> 4) == 0) clusters++;
                    }
                }
                else if (fatType == FATType.FAT16)
                {
                    fs.Seek(ReservedSectors * BytesPerSector, System.IO.SeekOrigin.Begin);
                    for (uint i = 2; i < ClusterCount + 2; i++)
                        if (br.ReadUInt16() == 0) clusters++;
                }
                else
                {
                    // use the default FAT, so this is correct whether we use FAT mirroring or not.
                    fs.Seek((ReservedSectors + FATNumber * FATSectors) * BytesPerSector, System.IO.SeekOrigin.Begin);
                    for (uint i = 2; i < ClusterCount + 2; i++)
                        if ((br.ReadUInt32() & 0x0FFFFFFF) == 0) clusters++;
                }
                return (long)(clusters * BytesPerCluster);
            }
            // performs a number of tests
            bool IsValidFat(System.IO.Stream s)
            {
                long pos = s.Position;
                // test jump code
                byte[] jmp = new byte[3];
                s.Read(jmp, 0, 3);
                if (jmp[0] != 0xe9 && !(jmp[0] == 0xeb && jmp[2] == 0x90)) return false;
                s.Position = pos + 12;
                // high byte of BytesPerSector must be 2(512) - 16(4096)
                int bps = s.ReadByte();
                if (bps != 2 && bps != 4 && bps != 8 && bps != 16) return false;
                // reserved sectors must not be 0
                s.Position = pos + 14;
                int resvd = s.ReadByte() | (s.ReadByte() << 8);
                if (resvd == 0) return false;
                // match media type to fat
                s.Position = pos + 21;
                int b = s.ReadByte();
                s.Position = (bps << 8) * resvd;
                if (s.ReadByte() != b) return false;
                return true;
            }
            public FATVolume(string filename)
            {
                if (!System.IO.File.Exists(filename))
                    throw new FATException(string.Format("Disk image '{0}' does not exist.", filename));
                System.IO.FileStream fs1 = System.IO.File.Open(filename, System.IO.FileMode.Open);
                if (!IsValidFat(fs1)) throw new FATException("Not a valid FAT image.");
                fs = new VolStream(fs1, 0, fs1.Length);
                if (!fs.CanWrite)
                    throw new FATException(string.Format("Disk image '{0}' is read-only.", filename));
                br = new System.IO.BinaryReader(fs, Encoding.Unicode); // Unicode so we can read Chars from long filenames
                bw = new System.IO.BinaryWriter(fs, Encoding.Unicode);
                // Read the paramaters from the disk
                fs.Seek(11, System.IO.SeekOrigin.Begin);
                BytesPerSector = br.ReadUInt16();
                SectorsPerCluster = br.ReadByte();
                ReservedSectors = br.ReadUInt16();
                NumFATs = br.ReadByte();
                RootEntries = br.ReadUInt16();
                TotalSectors = br.ReadUInt16();
                MediaDescriptor = br.ReadByte();
                FATSectors = br.ReadUInt16();
                SectorsPerTrack = br.ReadUInt16();
                Heads = br.ReadUInt16();
                HiddenSectors = br.ReadUInt32();
                {
                    uint TotalSectors32 = br.ReadUInt32();
                    if (TotalSectors == 0) TotalSectors = TotalSectors32;
                }
                RootSectors = (RootEntries * 32 + BytesPerSector - 1) / BytesPerSector;
                // Determine the FAT type
                if (FATSectors == 0)
                    FATSectors = br.ReadUInt32();
                ClusterCount = (TotalSectors - (FATSectors * NumFATs + ReservedSectors)) / SectorsPerCluster;
                if (ClusterCount < 4085)
                    fatType = FATType.FAT12;
                else if (ClusterCount < 65525)
                    fatType = FATType.FAT16;
                else
                    fatType = FATType.FAT32;
                BytesPerCluster = BytesPerSector * SectorsPerCluster;
                dataStart = ReservedSectors + RootSectors + FATSectors * NumFATs;
                rootStart = ReservedSectors + FATSectors * NumFATs;
                // initial values, default on FAT16 and FAT12
                MirrorFAT = true;
                FATNumber = 0;
                if (fatType == FATType.FAT32)
                {
                    // FATSectors32 should have already been read, because usually FAT32 volumes
                    // don't store this value in FATSectors16. If not, make sure we seek to the right position.
                    if (fs.Position == 36)
                        fs.Position = 40;
                    uint ExtFlags = br.ReadUInt16();
                    MirrorFAT = (ExtFlags & 0x80) == 0;
                    FATNumber = MirrorFAT ? 0 : ExtFlags & 15;
                    FSVer = br.ReadUInt16();
                    if (FSVer != 0)
                        throw new FATException("Unsupported FAT32 version.");
                    RootClus = br.ReadUInt32() & 0x0FFFFFFF;
                    FSInfo = br.ReadUInt16();
                }
                rootStream = 
                    (fatType == FATType.FAT32) ?
                    new DirStream(this, RootClus) : 
                    new DirStream(this, (long)(rootStart * BytesPerSector));
            }
            #endregion
        }
    }
}
