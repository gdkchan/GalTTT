using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GalTTT
{
    class GalCompression
    {
        public static void Decompress(string InFile, string OutFile)
        {
            using (FileStream Input = new FileStream(InFile, FileMode.Open))
            {
                byte[] Data = Decompress(Input);

                File.WriteAllBytes(OutFile, Data);

                int Percentage = (int)(((float)Input.Length / Data.Length) * 100);

                Console.WriteLine("Decompressed {0} to {1} bytes (C.R. = {2}%)",
                    Input.Length,
                    Data.Length,
                    Percentage);
            }
        }

        public static byte[] Decompress(Stream Input)
        {
            using (MemoryStream Output = new MemoryStream())
            {
                byte[] Map = new byte[0x200];

                BinaryReader Reader = new BinaryReader(Input);

                uint CompressedLength = Reader.ReadUInt32();

                long EndPosition = Input.Position + CompressedLength;

                while (Input.Position < EndPosition)
                {
                    //Reset Dictionary to default values
                    for (int Position = 0; Position < 0x100; Position++)
                    {
                        Map[Position]         = (byte)Position;
                        Map[Position | 0x100] = 0;
                    }

                    //Fill Map
                    int MapPos = 0;

                    do
                    {
                        byte Header = Reader.ReadByte();
                        byte Length = 1;

                        if ((Header & 0x80) != 0)
                            MapPos += Header - 0x7f;
                        else
                            Length += Header;

                        if (MapPos >= 0x100) break;

                        while (Length-- > 0)
                        {
                            int Position = MapPos++;

                            Map[Position] = Reader.ReadByte();

                            if (Position != Map[Position])
                            {
                                Map[Position | 0x100] = Reader.ReadByte();
                            }
                        }
                    }
                    while (MapPos < 0x100);

                    //Decompress data
                    Stack<int> Values = new Stack<int>();

                    byte LengthHigh = Reader.ReadByte();
                    byte LengthLow  = Reader.ReadByte();

                    int BlkLength = (LengthHigh << 8) | LengthLow;

                    while (BlkLength-- > 0)
                    {
                        Values.Push(Reader.ReadByte());

                        while (Values.Count > 0)
                        {
                            MapPos = Values.Pop();

                            if (MapPos != Map[MapPos])
                            {
                                Values.Push(Map[MapPos | 0x100]);
                                Values.Push(Map[MapPos]);
                            }
                            else
                            {
                                Output.WriteByte((byte)MapPos);
                            }
                        }
                    }
                }

                return Output.ToArray();
            }
        }

        public static void Compress(string InFile, string OutFile)
        {
            using (FileStream Output = new FileStream(OutFile, FileMode.Create))
            {
                byte[] Data = File.ReadAllBytes(InFile);

                Compress(Data, Output);

                int Percentage = (int)(((float)Output.Length / Data.Length) * 100);

                Console.WriteLine("Compressed {0} to {1} bytes (C.R. = {2}%)",
                    Data.Length,
                    Output.Length,
                    Percentage);
            }
        }

        public static void Compress(byte[] Data, Stream Output)
        {
            BinaryWriter Writer = new BinaryWriter(Output);

            long StartPosition = Output.Position;

            Writer.Write(0u);

            int Pos = 0;

            while (Pos < Data.Length)
            {
                //Build ideal map for current block, encode and write to output
                byte[] Map = BuildMap(Data, Pos);

                for (int MapPos = 0; MapPos < 0x100;)
                {
                    int Length0 = 0;
                    int Length1 = 0;

                    int StartPos = MapPos;

                    while (MapPos < 0x100)
                    {
                        if      (Map[MapPos] == MapPos && Length0 < 0x80 && Length1 == 0)
                            Length0++;
                        else if (Map[MapPos] != MapPos && Length1 < 0x80)
                            Length1++;
                        else
                            break;

                        MapPos++;
                    }

                    if (Length0 > 0)
                    {
                        Writer.Write((byte)(Length0 + 0x7f));
                    }

                    if (Length1 > 0)
                    {
                        if (Length0 > 0)
                        {
                            Writer.Write(Map[StartPos + Length0]);
                            Writer.Write(Map[StartPos + Length0++ | 0x100]);

                            Length1--;
                        }

                        if (Length1 > 0)
                        {
                            Writer.Write((byte)(Length1 - 1));

                            while (Length1-- > 0)
                            {
                                Writer.Write(Map[StartPos + Length0]);
                                Writer.Write(Map[StartPos + Length0++ | 0x100]);
                            }
                        }
                    }
                    else if (Length0 > 0 && MapPos < 0x100)
                    {
                        Writer.Write(Map[MapPos++]);
                    }
                }

                //Block start, write compressed block data
                long BlkStart = Output.Position;

                Writer.Write((ushort)0);

                while (Pos < Data.Length && (Output.Position - BlkStart - 2) < 0xffff)
                {
                    byte Value = Data[Pos];

                    if (Value == Map[Value])
                    {
                        int OptVal = GetOptimalValue(Map, Data, Pos);

                        Pos += (OptVal >> 8);

                        Output.WriteByte((byte)OptVal);
                    }
                    else
                    {
                        break;
                    }
                }

                long Position = Output.Position;

                long BlkLength = Position - BlkStart - 2;

                Output.Seek(BlkStart, SeekOrigin.Begin);

                Writer.Write((byte)(BlkLength >> 8));
                Writer.Write((byte)(BlkLength >> 0));

                Output.Seek(Position, SeekOrigin.Begin);
            }

            long FullLength = Output.Position - StartPosition;

            Output.Seek(StartPosition, SeekOrigin.Begin);

            Writer.Write((uint)FullLength - 4);

            Output.Seek(StartPosition + FullLength, SeekOrigin.Begin);
        }

        private static byte[] BuildMap(byte[] Data, int Addr)
        {
            const int MaxAnalysisLength = 0x1000;
            const int MaxFullWordLength = 8;

            List<ulong> BestMatches = new List<ulong>();

            bool[] Use = new bool[0x100];
            byte[] Map = new byte[0x200];

            for (int Position = 0; Position < 0x100; Position++)
            {
                Map[Position]         = (byte)Position;
                Map[Position | 0x100] = 0;
            }

            int Length = Math.Min(Data.Length - Addr, MaxAnalysisLength);

            //This array is used to keep track of values that are already being used inside this block.
            //Mark those so we store words only on unused spaces.
            for (int Index = 0; Index < Length; Index++)
            {
                Use[Data[Addr + Index]] = true;
            }

            //Finds repeating patterns inside this block.
            //Found patterns are added to the BestMatches list, they can contain repetitions.
            for (int Index = Addr + 2; Index < Addr + Length; Index++)
            {
                List<ulong> Matches = new List<ulong>();

                int RePos = Addr;

                while ((RePos = Array.IndexOf(Data, Data[Index], RePos)) != -1)
                {
                    if (RePos >= Index) break;

                    ulong ReLen = 1;

                    int Dist = Math.Min(Index - RePos, Data.Length - Index);

                    //Compare bytes from current position to the ones at found match position.
                    //If byte matches we increment the length, otherwise we stop comparing.
                    for (int Offset = 1; Offset < Dist; Offset++)
                    {
                        if (Data[Index + Offset] == Data[RePos + Offset])
                            ReLen++;
                        else
                            break;
                    }

                    //If only 1 byte is equal to current data, then it is useless to us (no repeating pattern).
                    //Otherwise we store it with the length at the higher 32 bits (more efficient than creating a struct).
                    if (ReLen > 1)
                    {
                        Matches.Add((uint)RePos | (ReLen << 32));
                    }

                    RePos += (int)ReLen;
                }

                if (Matches.Count > 0)
                {
                    ulong PosLen = Matches.Max();

                    Index += (int)(PosLen >> 32) - 1;

                    /*
                     * Bits  0-31 = Absolute position on Data
                     * Bits 32-47 = Length of found match
                     * Bits 48-63 = Number of occurrences found (negated)
                     * When sorted in ascending order, occurrences with more matches will be at the beggining.
                     */
                    BestMatches.Add(PosLen | ((ulong)(Matches.Count ^ 0xffff) << 48));
                }
            }

            BestMatches.Sort();

            int BestIndex = -1;

            while (++BestIndex < BestMatches.Count)
            {
                int Pos = (int)BestMatches[BestIndex];
                int Len = (int)((BestMatches[BestIndex] >> 32) & 0xffff);

                int EndPos  = Pos + Len;
                int LastPos = GetOptimalValue(Map, Data, Pos);

                if (Len <= MaxFullWordLength)
                {
                    Pos += LastPos >> 8;
                }

                int MapPos;

                for (MapPos = 0; MapPos < 0x100 && Pos < EndPos; MapPos++)
                {
                    if (!Use[MapPos])
                    {
                        int Value0 = GetOptimalValue(Map, Data, Pos);
                        int Value1 = GetOptimalValue(Map, Data, Pos + (Value0 >> 8));

                        /*
                         * When the block is too big, it's better to store it in
                         * groups of two instead of creating a complete chain, this way
                         * the smaller groups can be re-used to form bigger ones, and is
                         * usually more efficient since the larget the data is, it's more unlikely
                         * that it will repeat again inside the file.
                         * TODO: Ideally, develop a smart algorithm that detects the best place to break the chain.
                         */
                        if (Len > MaxFullWordLength)
                        {
                            Map[MapPos] = (byte)Value1;

                            Pos += (Value0 >> 8) + (Value1 >> 8);
                        }
                        else
                        {
                            Map[MapPos] = (byte)LastPos;

                            Pos += Value0 >> 8;
                        }

                        Map[MapPos | 0x100] = (byte)Value0;

                        LastPos = MapPos;

                        Use[MapPos] = true;

                        MapPos = -1;
                    }
                }

                if (MapPos == 0x100) break;
            }

            return Map;
        }

        private static int GetOptimalValue(byte[] Map, byte[] Data, int Pos)
        {
            //This will find all paths on the Map that can form the current data being
            //encoded, and return the one that matches the greater amount of bytes.
            //The lower 8 bits contains the position on the Map, while the higher 24 bits
            //contains the length of the match.
            Queue<int> Visit = new Queue<int>();
            List<int> Matches = new List<int>();

            Visit.Enqueue(Data[Pos] | 0x100);

            while (Visit.Count > 0)
            {
                int ValLen = Visit.Dequeue();
                int Value  = ValLen & 0xff;
                int Length = ValLen >> 8;
                int Count  = 0;

                for (int MapPos = 0; MapPos < 0x100; MapPos++)
                {
                    if (MapPos != Value && Map[MapPos] == Value && Pos + Length + 1 < Data.Length)
                    {
                        bool IsMatch = true;

                        Stack<int> Values = new Stack<int>();

                        Values.Push(Map[MapPos | 0x100]);

                        while (Values.Count > 0 && Pos + Length < Data.Length)
                        {
                            int Position = Values.Pop();

                            if (Position != Map[Position])
                            {
                                Values.Push(Map[Position | 0x100]);
                                Values.Push(Map[Position]);
                            }
                            else
                            {
                                IsMatch = Data[Pos + Length] == Position;

                                if (!IsMatch) break;

                                Length++;
                            }
                        }

                        if (IsMatch)
                        {
                            Visit.Enqueue(MapPos | (Length << 8));

                            Count++;
                        }

                        Length = ValLen >> 8;
                    }
                }

                if (Count == 0)
                {
                    Matches.Add(ValLen);
                }
            }

            return Matches.Max();
        }
    }
}
