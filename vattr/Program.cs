using System;
using System.Collections.Generic;
using System.Text;
using OSDevelopment.DiskImages;

namespace vattr
{
    class Program
    {
        static int Main(string[] args)
        {
            switch (args.Length)
            {
                case 0:
                    System.Console.WriteLine("usage: vattr image-name path rhsa");
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
                    System.Console.Error.WriteLine("missing parameter - attributes");
                    return 1;
                case 3:
                    try
                    {
                        FATVolume fatvol = new FATVolume(args[0]);
                        if (!fatvol.Exists(args[1]))
                            throw new Exception(string.Format("File or directory {0} does not exist on disk image.", args[1]));
                        FileAttributes a = fatvol.GetAttributes(args[1]) & (FileAttributes.Directory);
                        for (int i = 0; i < args[2].Length; i++)
                        {
                            switch (args[2][i])
                            {
                                case 'r':
                                    a |= FileAttributes.ReadOnly;
                                    break;
                                case 'h':
                                    a |= FileAttributes.Hidden;
                                    break;
                                case 's':
                                    a |= FileAttributes.System;
                                    break;
                                case 'a':
                                    a |= FileAttributes.Archive;
                                    break;
                                default:
                                    throw new Exception(string.Format("Unrecognized attrubute - '{0}'", args[2][i]));
                            }
                        }
                        fatvol.SetAttributes(args[1], a);
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
                    System.Console.Error.WriteLine("usage: vattr image-name path rhsa");
                    return 1;
            }
        }
    }
}