using System;
using System.Collections.Generic;
using System.Text;
using OSDevelopment.DiskImages;

namespace vput
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args[0] == "--help")
            {
                Console.WriteLine("usage:\r\nvput image-file file newname\r\nvput --help");
                return 0;
            }
            switch (args.Length)
            {
                case 3:
                    if (!System.IO.File.Exists(args[0]) || !System.IO.File.Exists(args[1]))
                    {
                        Console.Error.WriteLine("vput: File does not exist.");
                        return 1;
                    }
                    try
                    {
                        FATVolume fv = new FATVolume(args[0]);
                        System.IO.Stream sr = new System.IO.FileStream(args[1], System.IO.FileMode.Open);
                        System.IO.Stream sw = fv.OpenFile(args[2], System.IO.FileAccess.Write, System.IO.FileMode.Create);
                        byte[] x = new byte[1024];
                        while (sr.Position < sr.Length)
                        {
                            sw.Write(x, 0, sr.Read(x, 0, (int)Math.Min(1024, sr.Length - sr.Position)));
                        }
                        sw.Close();
                        sr.Close();
                        fv.Close();
                    }
                    catch (Exception e)
                    {
                        System.Console.Error.WriteLine("vput: error: {0}", e.Message);
                        return 1;
                    }
                    return 0;
                default:
                    Console.Error.WriteLine("vput: wrong number of arguments given");
                    Console.WriteLine("usage:\r\nvput image-file file newname\r\nvput --help");
                    return 1;
            }
        }
    }
}
