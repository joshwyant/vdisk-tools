using System;
using System.Collections.Generic;
using System.Text;
using OSDevelopment.DiskImages;

namespace vfinfo
{
    class Program
    {
        static int Main(string[] args)
        {
            switch (args.Length)
            {
                case 0:
                    System.Console.WriteLine("usage: vfinfo image-name path");
                    return 1;
                case 1:
                    if (args[0] == "--help")
                    {
                        System.Console.WriteLine("usage: vattr image-name path rhsa");
                        return 0;
                    }
                    System.Console.Error.WriteLine("missing parameter - path");
                    return 1;
                case 2:
                    try
                    {
                        FATVolume fatvol = new FATVolume(args[0]);
                        if (!fatvol.Exists(args[1]))
                            throw new Exception(string.Format("File or directory {0} does not exist on disk image.", args[1]));
                        FileInfo fi = fatvol.GetFileInfo(args[1]);
                        FileAttributes a = fi.Attributes;
                        string attr = "";
                        if (a == FileAttributes.None)
                            attr += ("(no attributes) ");
                        if ((a & FileAttributes.Directory) != FileAttributes.None)
                            attr += ("directory ");
                        if ((a & FileAttributes.ReadOnly) != FileAttributes.None)
                            attr += ("read only ");
                        if ((a & FileAttributes.Hidden) != FileAttributes.None)
                            attr += ("hidden ");
                        if ((a & FileAttributes.System) != FileAttributes.None)
                            attr += ("system ");
                        if ((a & FileAttributes.Archive) != FileAttributes.None)
                            attr += ("archive ");
                        Console.WriteLine("         Name:\t{0}", fi.Name);
                        Console.WriteLine("   Attributes:\t{0}", attr);
                        Console.WriteLine("         Size:\t{0} bytes", fi.Size);
                        Console.WriteLine("First cluster:\t{0}", fi.FirstCluster);
                        Console.WriteLine("Creation date:\t{0} {1}", fi.CreateTime.ToLongDateString(), fi.CreateTime.ToLongTimeString());
                        Console.WriteLine("Modified date:\t{0} {1}", fi.LastWriteTime.ToLongDateString(), fi.LastWriteTime.ToLongTimeString());
                        Console.WriteLine($"  Access date:\t{fi.LastAccessDate.ToLongDateString()}");
                        fatvol.Close();
                    }
                    catch (Exception e)
                    {
                        System.Console.Error.Write("error: ");
                        System.Console.Error.WriteLine(e.Message);
                        return 1;
                    }
                    return 0;
                default:
                    System.Console.Error.WriteLine("too many paramaters");
                    System.Console.Error.WriteLine("usage: vfinfo image-name path");
                    return 1;
            }
        }
    }
}