using System;
using System.IO;

namespace GalTTT
{
    static class GalFilePackage
    {
        public static void Unpack(string FileName, string OutFolder)
        {
            if (!Directory.Exists(OutFolder))
            {
                Directory.CreateDirectory(OutFolder);
            }

            using (FileStream Input = new FileStream(FileName, FileMode.Open))
            {
                BinaryReader Reader = new BinaryReader(Input);

                uint FilesCount = Reader.ReadUInt32();

                for (int Index = 0; Index < FilesCount; Index++)
                {
                    Input.Seek(4 + Index * 4, SeekOrigin.Begin);

                    ushort FileAddr = Reader.ReadUInt16();
                    ushort FileLen  = Reader.ReadUInt16();

                    Input.Seek(FileAddr * 0x800, SeekOrigin.Begin);

                    byte[] Data = Reader.ReadBytes(FileLen * 0x800);

                    string OutFileName = Path.Combine(OutFolder, string.Format("file_{0:d5}.bin", Index));

                    Console.WriteLine("{0:x8} -> {1}", FileAddr * 0x800, OutFileName);

                    File.WriteAllBytes(OutFileName, Data);
                }
            }
        }

        public static void Pack(string FileName, string InFolder)
        {
            string[] Files = Directory.GetFiles(InFolder);

            using (FileStream Output = new FileStream(FileName, FileMode.Create))
            {
                BinaryWriter Writer = new BinaryWriter(Output);

                Writer.Write(Files.Length);

                long DataPosition = 4 + Files.Length * 4;

                while ((DataPosition & 0x7ff) != 0) DataPosition++;

                for (int Index = 0; Index < Files.Length; Index++)
                {
                    Console.WriteLine("{0} -> {1:x8}", Files[Index], DataPosition);

                    byte[] Data = File.ReadAllBytes(Files[Index]);

                    int DataLength = Data.Length;

                    while ((DataLength & 0x7ff) != 0) DataLength++;

                    Output.Seek(4 + Index * 4, SeekOrigin.Begin);

                    Writer.Write((ushort)(DataPosition / 0x800));
                    Writer.Write((ushort)(DataLength   / 0x800));

                    Output.Seek(DataPosition, SeekOrigin.Begin);

                    Writer.Write(Data);

                    DataPosition += DataLength;
                }

                while ((Output.Position & 0x7ff) != 0) Output.WriteByte(0);
            }
        }
    }
}
