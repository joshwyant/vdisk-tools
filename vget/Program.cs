using System;
using System.Collections.Generic;
using System.Text;
using OSDevelopment.DiskImages;

namespace vget
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args[0] == "--help")
            {
                Console.WriteLine("usage:\r\nvget image-file filename [newname]\r\nvget --help");
                return 0;
            }
            switch (args.Length)
            {
                case 2:
                case 3:
                    if (!System.IO.File.Exists(args[0]))
                    {
                        Console.Error.WriteLine("vget: Disk image file does not exist.");
                        return 1;
                    }
                    try
                    {
                        FATVolume fv = new FATVolume(args[0]);
                        if (!fv.Exists(args[1]))
                        {
                            Console.Error.WriteLine("vget: File does not exist on image.");
                            return 1;
                        }
                        args[1] = args[1].Replace('/', '\\');
                        string name =
                            args.Length == 3 ?
                                args[2] :
                                args[1].Contains("\\") ?
                                    args[1].Substring(args[1].LastIndexOf('\\') + 1) :
                                    args[1];

                        System.IO.Stream sw = new System.IO.FileStream(name, System.IO.FileMode.Create);
                        System.IO.Stream sr = fv.OpenFile(args[1], System.IO.FileAccess.Read, System.IO.FileMode.Open);
                        byte[] x = new byte[1024];
                        while (sr.Position < sr.Length)
                        {
                            sw.Write(x, 0, sr.Read(x, 0, (int)Math.Min(1024, sr.Length - sr.Position)));
                        }
                        sr.Close();
                        sw.Close();
                        fv.Close();
                    }
                    catch (Exception e)
                    {
                        System.Console.Error.WriteLine("vget: error: {0}", e.Message);
                        return 1;
                    }
                    return 0;
                default:
                    Console.Error.WriteLine("vget: wrong number of arguments given");
                    Console.WriteLine("usage:\r\nvget image-file filename newname\r\nvget --help");
                    return 1;
            }
        }
    }
}
