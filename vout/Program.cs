using System;
using System.Collections.Generic;
using System.Text;
using OSDevelopment.DiskImages;

namespace vout
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args[0] == "--help")
            {
                Console.WriteLine("usage:\r\nvout image-file filename\r\nvout --help");
                return 0;
            }
            switch (args.Length)
            {
                case 2:
                    if (!System.IO.File.Exists(args[0]))
                    {
                        Console.Error.WriteLine("vout: Disk image file does not exist.");
                        return 1;
                    }
                    try
                    {
                        FATVolume fv = new FATVolume(args[0]);
                        if (!fv.Exists(args[1]))
                        {
                            Console.Error.WriteLine("vout: File does not exist on image.");
                            return 1;
                        }
                        System.IO.StreamReader tr = new System.IO.StreamReader(fv.OpenFile(args[1], System.IO.FileAccess.Read, System.IO.FileMode.Open));
                        System.Console.Out.Write(tr.ReadToEnd());
                        tr.Close();
                        fv.Close();
                    }
                    catch (Exception e)
                    {
                        System.Console.Error.WriteLine("vout: error: {0}", e.Message);
                        return 1;
                    }
                    return 0;
                default:
                    Console.Error.WriteLine("vout: wrong number of arguments given");
                    Console.WriteLine("usage:\r\nvout image-file filename\r\nvout --help");
                    return 1;
            }
        }
    }
}
