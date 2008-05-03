using System;
using System.Collections.Generic;
using System.Text;
using OSDevelopment.DiskImages;

namespace vmkimg
{
    class Program
    {
        static void ShowHelp()
        {
            Console.WriteLine("usage: vmkimg -i image -M size options\r\nOptions:");
            Console.WriteLine("-i\tImage filename");
            Console.WriteLine("-b\tOverride boot sector file name (must give FAT type)");
            Console.WriteLine("-d\tDevice number (e.g. -d 80)");
            Console.WriteLine("-s\tSectors per track");
            Console.WriteLine("-h\tHeads");
            Console.WriteLine("-m\tMedia type (e.g. -m F8)");
            Console.WriteLine("-t\tFAT16 or FAT32");
            Console.WriteLine("-v\tVolume label");
            Console.WriteLine("-M\tSize of image in Megabytes");
        }
        static bool isopt(string arg)
        {
            return (arg == "-i" ||
                    arg == "-b" ||
                    arg == "-d" ||
                    arg == "-s" ||
                    arg == "-h" ||
                    arg == "-m" ||
                    arg == "-t" ||
                    arg == "-v" ||
                    arg == "-M");
        }
        static int Main(string[] args)
        {
#if DEBUG
            switch (0)
            {
                case 0:
                    args = new string[] { "-i", "C:\\abc.img", "-M", "512", "-h", "16", "-s", "63", "-t", "FAT16", "-v", "myos" };
                    break;
            }
#endif
            if (args[0] == "--help")
            {
                ShowHelp();
#if DEBUG
                Console.Write("Press any key to continue...");
                Console.ReadKey(true);
                Console.WriteLine();
#endif
                return 0;
            }
            string last, image, boot, device, spt, heads, media, type, lab, megs;
            last = image = boot = device = spt = heads = media = type = lab = megs = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (!isopt(args[i]))
                {
                    if (last == "-i")
                        image = args[i];
                    else if (last == "-b")
                        boot = args[i];
                    else if (last == "-d")
                        device = args[i];
                    else if (last == "-s")
                        spt = args[i];
                    else if (last == "-h")
                        heads = args[i];
                    else if (last == "-m")
                        media = args[i];
                    else if (last == "-t")
                        type = args[i];
                    else if (last == "-v")
                        lab = args[i];
                    else if (last == "-M")
                        megs = args[i];
                    else
                    {
                        System.Console.Error.WriteLine("Invalid option");
                        return 1;
                    }
                }
                last = args[i];
            }
            if (isopt(last))
            {
                Console.Error.WriteLine("Missing parameter after {0}", last);
                return 1;
            }
            if (string.IsNullOrEmpty(image) ||
                string.IsNullOrEmpty(megs) ||
                (!string.IsNullOrEmpty(boot) && string.IsNullOrEmpty(type)) ||
                (!string.IsNullOrEmpty(spt) ^ !string.IsNullOrEmpty(heads))
                )
            {
                Console.Error.WriteLine("Missing parameter");
                return 1;
            }
            try
            {
                FATType t = FATType.None;
                if (type != null)
                {
                    if (type.ToUpper() == "FAT32")
                        t = FATType.FAT32;
                    else if (type.ToUpper() == "FAT16")
                        t = FATType.FAT16;
                    else
                        throw new ArgumentOutOfRangeException();
                }
                int m = int.Parse(megs);
                if (m == 0)
                    throw new Exception("Invalid disk size");
                FATFormatInfo fi = new FATFormatInfo(
                    t,
                    m,
                    lab,
                    string.IsNullOrEmpty(spt) ? 63 : int.Parse(spt),
                    string.IsNullOrEmpty(heads) ? 16 : int.Parse(heads)
                    );
                if (!string.IsNullOrEmpty(boot))
                    fi.BootSector = System.IO.File.ReadAllBytes(boot);
                if (!string.IsNullOrEmpty(device))
                    fi.DriveNumber = byte.Parse(device, System.Globalization.NumberStyles.AllowHexSpecifier);
                if (!string.IsNullOrEmpty(media))
                    fi.MediaType = byte.Parse(media, System.Globalization.NumberStyles.AllowHexSpecifier);
                if (FATFormatter.DefaultFatType(fi.TotalSectors) == FATType.FAT12)
                    throw new Exception("Too few sectors");
                if (t == FATType.None)
                    t = FATFormatter.DefaultFatType(fi.TotalSectors);
                if (!FATFormatter.ValidFatType(t, fi.TotalSectors))
                    throw new Exception(string.Format("Cannot create a {0} image that is {1} megabytes", t, m));
                Console.WriteLine("{0}\tHeads", fi.Heads);
                Console.WriteLine("{0}\tSectors per track", fi.SectorsPerTrack);
                Console.WriteLine("{0}\tCylinders", fi.Cylinders);
                Console.WriteLine("{0} Total sectors", fi.TotalSectors);
                Console.WriteLine("Formatting as {0}...", t);
                FATFormatter.CreateDiskImage(image, fi);
                Console.WriteLine("Format complete.");
#if DEBUG
                Console.Write("Press any key to continue...");
                Console.ReadKey(true);
                Console.WriteLine();
#endif
            }
            catch (Exception e)
            {
                System.Console.Error.WriteLine("vmkimg: error: {0}", e.Message);
                return 1;
            }
            return 0;
        }
    }
}
