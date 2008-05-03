using System;
using System.Collections.Generic;
using System.Text;
using OSDevelopment.DiskImages;

namespace vexits
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args[0] == "--help")
            {
                Console.WriteLine("usage:\r\nvexists image-file filename [-f|-d]\r\nvexists --help");
                return 0;
            }
            int b = 0;
            FATVolume fv;
            switch (args.Length)
            {
                case 2:
                    if (!System.IO.File.Exists(args[0]))
                    {
                        Console.Error.WriteLine("vexists: Disk image file does not exist.");
                        return 0;
                    }
                    try
                    {
                        fv = new FATVolume(args[0]);
                        b = fv.Exists(args[1]) == false ? 0 : 1;
                        fv.Close();
                    }
                    catch (Exception e)
                    {
                        System.Console.Error.WriteLine("vexists: error: {0}", e.Message);
                        return 0;
                    }
                    return b;
                case 3:
                    if (!System.IO.File.Exists(args[0]))
                    {
                        Console.Error.WriteLine("vexists: Disk image file does not exist.");
                        return 0;
                    }
                    if (args[2] != "-d" && args[2] != "-f")
                    {
                        Console.Error.WriteLine("vexists: Third argument is not -d or -f");
                        return 0;
                    }
                    try
                    {
                        fv = new FATVolume(args[0]);
                        b = (args[2] == "-f" ? fv.FileExists(args[1]) : fv.DirectoryExists(args[1])) == false ? 0 : 1;
                        fv.Close();
                    }
                    catch (Exception e)
                    {
                        System.Console.Error.WriteLine("vexists: error: {0}", e.Message);
                        return 0;
                    }
                    return b;
                default:
                    Console.Error.WriteLine("vexists: wrong number of arguments given");
                    Console.WriteLine("usage:\r\nvexists image-file filename [-f|-d]\r\nvexists --help");
                    return 0;
            }
        }
    }
}
