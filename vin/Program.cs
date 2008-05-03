using System;
using System.Collections.Generic;
using System.Text;
using OSDevelopment.DiskImages;

namespace vin
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args[0] == "--help")
            {
                Console.WriteLine("usage:\r\nvin image-file newfile\r\nvin --help");
                return 0;
            }
            switch (args.Length)
            {
                case 2:
                    if (!System.IO.File.Exists(args[0]))
                    {
                        Console.Error.WriteLine("vin: Disk image file does not exist.");
                        return 1;
                    }
                    try
                    {
                        FATVolume fv = new FATVolume(args[0]);
                        System.IO.StreamWriter sw = new System.IO.StreamWriter(fv.OpenFile(args[1], System.IO.FileAccess.Write, System.IO.FileMode.Create));
                        sw.Write(Console.In.ReadToEnd());
                        sw.Close();
                        fv.Close();
                    }
                    catch (Exception e)
                    {
                        System.Console.Error.WriteLine("vin: error: {0}", e.Message);
                        return 1;
                    }
                    return 0;
                default:
                    Console.Error.WriteLine("vin: wrong number of arguments given");
                    Console.WriteLine("usage:\r\nvin image-file newfile\r\nvin --help");
                    return 1;
            }
        }
    }
}
