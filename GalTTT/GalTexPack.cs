using System;
using System.IO;

namespace GalTTT
{
    static class GalTexPack
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
                    Input.Seek(4 + Index * 8, SeekOrigin.Begin);

                    uint FileAddr = Reader.ReadUInt32();
                    uint FileLen  = Reader.ReadUInt32();

                    Input.Seek(FileAddr, SeekOrigin.Begin);

                    byte[] Data = GalCompression.Decompress(Input);

                    string OutFileName = Path.Combine(OutFolder, string.Format("file_{0:d5}.tim", Index));

                    Console.WriteLine("{0:x8} -> {1}", FileAddr, OutFileName);

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

                long DataPosition = 4 + Files.Length * 8;

                for (int Index = 0; Index < Files.Length; Index++)
                {
                    Console.WriteLine("{0} -> {1:x8}", Files[Index], DataPosition);

                    byte[] Data = File.ReadAllBytes(Files[Index]);

                    Output.Seek(DataPosition, SeekOrigin.Begin);

                    GalCompression.Compress(Data, Output);

                    while ((Output.Position & 3) != 0) Output.WriteByte(0);

                    long OldPosition = DataPosition;

                    DataPosition = Output.Position;

                    Output.Seek(4 + Index * 8, SeekOrigin.Begin);

                    Writer.Write((uint)OldPosition);
                    Writer.Write((uint)(DataPosition - OldPosition));
                }
            }
        }
    }
}
