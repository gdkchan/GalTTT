using System;
using System.IO;

namespace GalTTT
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("GalTTT - Galerians TIM Texture Tool");
            Console.WriteLine("Made by gdkchan");
            Console.WriteLine("Version 1.0\n");
            Console.ResetColor();

            if (args.Length != 3 && args.Length != 2)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("Usage:\n");
                Console.ResetColor();
                Console.WriteLine("galttt [command] gamefile.ext folder/file\n");
                Console.WriteLine("Accepted commands:\n");
                Console.WriteLine("-xcdb  Extract CDB archive to a folder");
                Console.WriteLine("-ccdb  Creates a CDB archive from a folder");
                Console.WriteLine("-xtim  Decompress texture TIM pack to a folder");
                Console.WriteLine("-ctim  Compress a textures TIM pack from a folder");
                Console.WriteLine("-dec   Decompress raw file");
                Console.WriteLine("-cmp   Compress raw file");
                Console.WriteLine("\nExamples:\n");
                Console.WriteLine("galttt -xcdb DISPLAY.CDB display");
                Console.WriteLine("galttt -xtim file_00042.bin tstex");
                Console.WriteLine("galttt -dec compressed.bin decompressed.tim");
                Console.WriteLine("galttt -cmp compressed.bin decompressed.tim");
            }
            else
            {
                bool InvalidCmd = false;

                switch (args[0].ToLower())
                {
                    case "-xcdb": GalFilePackage.Unpack(args[1], args[2]); break;
                    case "-ccdb": GalFilePackage.Pack(args[1], args[2]); break;
                    case "-xtim": GalTexPack.Unpack(args[1], args[2]); break;
                    case "-ctim": GalTexPack.Pack(args[1], args[2]); break;
                    case "-dec":  GalCompression.Decompress(args[1], args[2]); break;
                    case "-cmp":  GalCompression.Compress(args[2], args[1]); break;

                    default: InvalidCmd = true; break;
                }

                if (InvalidCmd)
                    Console.WriteLine("Invalid command!");
                else
                    Console.WriteLine("\nDone.");
            }
        }
    }
}
