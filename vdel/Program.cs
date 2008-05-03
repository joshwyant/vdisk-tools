using System;
using System.Collections.Generic;
using System.Text;
using OSDevelopment.DiskImages;

namespace vdel
{
    class Program
    {
        static int Main(string[] args)
        {
            switch (args.Length)
            {
                case 0:
                    System.Console.WriteLine("usage: vdel image-name file");
                    return 1;
                case 1:
                    if (args[0] == "--help")
                    {
                        System.Console.WriteLine("usage: vdel image-name file");
                        return 0;
                    }
                    System.Console.Error.WriteLine("missing parameter - file");
                    return 1;
                case 2:
                    try
                    {
                        FATVolume fatvol = new FATVolume(args[0]);
                        fatvol.Delete(args[1]);
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
                    System.Console.Error.WriteLine("usage: vdel image-name file");
                    return 1;
            }
        }
    }
}