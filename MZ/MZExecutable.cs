using Disassembler.NE;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace Disassembler.MZ
{
	public struct MZRelocationItem
	{
		// 0x00 - Offset, word (Offset of the relocation within provided segment.)
		private int iOffset;
		// 0x02 - Segment, word (Segment of the relocation, relative to the load segment address.)
		private int iSegment;

		public MZRelocationItem(int offset, int segment)
		{
			this.iOffset = offset;
			this.iSegment = segment;
		}

		public int Offset
		{
			get { return iOffset; }
		}

		public int Segment
		{
			get { return iSegment; }
		}
	}

	public class MZExecutable
	{
		// 0x00 - Signature, word (0x5A4D (ASCII for 'M' and 'Z'))
		protected int iSignature = -1;
		// 0x0A - Minimum allocation, word (The number of paragraphs required by the program, excluding the PSP and program image. If no free block is big enough, the loading stops.)
		protected int iMinimumAllocation = -1;
		// 0x0C - Maximum allocation, word (The number of paragraphs requested by the program. If no free block is big enough, the biggest one possible is allocated.)
		protected int iMaximumAllocation = -1;
		// 0x0E - Initial SS, word (Relocatable segment address for SS.)
		protected int iInitialSS = -1;
		// 0x10 - Initial SP, word (Initial value for SP.)
		protected int iInitialSP = -1;
		// 0x14 - Initial IP, word (Initial value for IP.)
		protected int iInitialIP = -1;
		// 0x16 - Initial CS, word (Relocatable segment address for CS.)
		protected int iInitialCS = -1;
		// 0x1A - Overlay, word (Value used for overlay management. If zero, this is the main executable.)
		protected int iOverlayIndex = -1;
		// actual code or data
		protected byte[] aData = new byte[0];
		// relocation data
		protected List<MZRelocationItem> aRelocations = new List<MZRelocationItem>();
		// overlays
		protected List<MZExecutable> aOverlays = new List<MZExecutable>();

		protected MZExecutable()
		{ }

		public MZExecutable(string path)
			: this(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
		{ }

		public MZExecutable(Stream stream)
		{
			int iFilePosition = 0;
			iFilePosition += ReadHeader(stream, iFilePosition, this);

			// load overlays
			while (iFilePosition < stream.Length)
			{
				MZExecutable overlay = new MZExecutable();
				stream.Seek(iFilePosition, SeekOrigin.Begin);
				iFilePosition += ReadHeader(stream, iFilePosition, overlay);

				this.aOverlays.Add(overlay);
			}
		}

		private static int ReadHeader(Stream stream, int position, MZExecutable exe)
		{
			// load header
			exe.iSignature = ReadUInt16(stream);
			if (exe.iSignature != 0x5a4d)
			{
				throw new Exception("Not an MS-DOS executable file");
			}

			// 0x02 - Extra bytes, word (Number of bytes in the last page.)
			int iExtraBytes = ReadUInt16(stream);
			// 0x04 - Pages, word (Number of whole/partial pages.)
			int iPages = ReadUInt16(stream);
			// 0x06 - Relocation items, word (Number of entries in the relocation table.)
			int iRelocationItems = ReadUInt16(stream);
			// 0x08 - Header size, word (The number of paragraphs taken up by the header. It can be any value, as the loader just uses it to find where the actual executable data starts. It may be larger than what the "standard" fields take up, and you may use it if you want to include your own header metadata, or put the relocation table there, or use it for any other purpose.)
			int iHeaderSize = ReadUInt16(stream);
			exe.iMinimumAllocation = ReadUInt16(stream);
			exe.iMaximumAllocation = ReadUInt16(stream);
			exe.iInitialSS = ReadUInt16(stream);
			exe.iInitialSP = ReadUInt16(stream);
			// 0x12 - Checksum, word (When added to the sum of all other words in the file, the result should be zero.)
			int iChecksum = ReadUInt16(stream);
			exe.iInitialIP = ReadUInt16(stream);
			exe.iInitialCS = ReadUInt16(stream);
			// 0x18 - Relocation table, word (The (absolute) offset to the relocation table.)
			int iRelocationTableOffset = ReadUInt16(stream);
			exe.iOverlayIndex = ReadUInt16(stream);

			// read relocations
			if (iRelocationItems > 0)
			{
				stream.Seek(position + iRelocationTableOffset, SeekOrigin.Begin);
				for (int i = 0; i < iRelocationItems; i++)
				{
					exe.aRelocations.Add(new MZRelocationItem(ReadUInt16(stream), ReadUInt16(stream)));
				}

				stream.Seek(position + iHeaderSize << 4, SeekOrigin.Begin);
			}

			// read data
			int iDataSize = (iExtraBytes > 0) ? (((iPages - 1) << 9) + iExtraBytes) - (iHeaderSize << 4) : (iPages << 9) - (iHeaderSize << 4);
			stream.Seek(position + (iHeaderSize << 4), SeekOrigin.Begin);
			byte[] buffer = new byte[iDataSize];
			stream.Read(buffer, 0, iDataSize);
			exe.aData = buffer;

			return (iPages << 9);
		}

		#region Helper functions
		public static byte ReadByte(Stream stream)
		{
			int b0 = stream.ReadByte();

			if (b0 < 0)
			{
				throw new Exception("Unexpected end of stream");
			}

			return (byte)(b0 & 0xff);
		}

		public static int ReadUInt16(Stream stream)
		{
			int b0 = stream.ReadByte();
			int b1 = stream.ReadByte();

			if (b0 < 0 || b1 < 0)
			{
				throw new Exception("Unexpected end of stream");
			}

			return (b0 & 0xff) | ((b1 & 0xff) << 8);
		}

		public static long ReadUInt32(Stream stream)
		{
			int b0 = stream.ReadByte();
			int b1 = stream.ReadByte();
			int b2 = stream.ReadByte();
			int b3 = stream.ReadByte();

			if (b0 < 0 || b1 < 0 || b2 < 0 || b3 < 0)
			{
				throw new Exception("Unexpected end of stream");
			}

			return (long)((uint)((uint)b0 & 0xff) | (uint)(((uint)b1 & 0xff) << 8) |
				(uint)(((uint)b2 & 0xff) << 16) | (uint)(((uint)b3 & 0xff) << 24));
		}

		public static byte[] ReadBlock(Stream stream, int size)
		{
			byte[] abTemp = new byte[size];

			if (stream.Read(abTemp, 0, size) != size)
			{
				throw new Exception("Unexpected end of stream");
			}

			return abTemp;
		}

		public static string? ReadString(Stream stream)
		{
			int iLength = ReadByte(stream);
			byte[] abTemp = new byte[iLength];

			if (iLength == 0)
				return null;

			if (stream.Read(abTemp, 0, iLength) != iLength)
			{
				throw new Exception("Unexpected end of stream");
			}

			return Encoding.ASCII.GetString(abTemp);
		}
		#endregion


		public int Signature
		{
			get { return this.iSignature; }
		}

		public int MinimumAllocation
		{
			get { return this.iMinimumAllocation; }
			set { this.iMinimumAllocation = value; }
		}

		public int MaximumAllocation
		{
			get { return this.iMaximumAllocation; }
			set { this.iMaximumAllocation = value; }
		}

		public int InitialSS
		{
			get { return this.iInitialSS; }
			set { this.iInitialSS = value; }
		}

		public int InitialSP
		{
			get { return this.iInitialSP; }
			set { this.iInitialSP = value; }
		}

		public int InitialIP
		{
			get { return this.iInitialIP; }
			set { this.iInitialIP = value; }
		}

		public int InitialCS
		{
			get { return this.iInitialCS; }
			set { this.InitialCS = value; }
		}

		public int OverlayIndex
		{
			get { return this.iOverlayIndex; }
			set { this.iOverlayIndex = value; }
		}

		public byte[] Data
		{
			get { return this.aData; }
			set { this.aData = value; }
		}

		public List<MZRelocationItem> Relocations
		{
			get { return this.aRelocations; }
		}

		public List<MZExecutable> Overlays
		{
			get { return this.aOverlays; }
		}
	}
}
