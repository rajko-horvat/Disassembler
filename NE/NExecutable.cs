using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Disassembler.NE
{
	public class NExecutable
	{
		public NExecutable(string path)
			: this(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
		{ }

		public NExecutable(Stream stream)
		{
			// perform basic checks, we don't need to load entire MS-DOS header
			int iSignature = ReadUInt16(stream);
			if (iSignature != 0x5a4d)
			{
				throw new Exception("Not an MS-DOS executable file");
			}
			stream.Seek(0x18, SeekOrigin.Begin);
			int iTemp = ReadUInt16(stream);
			if (iTemp < 0x40)
			{
				throw new Exception("Not an 16bit Windows executable file");
			}
			stream.Seek(0x3c, SeekOrigin.Begin);
			int iOffset = ReadUInt16(stream);
			stream.Seek(iOffset, SeekOrigin.Begin);
			iSignature = ReadUInt16(stream);
			if (iSignature != 0x454e)
			{
				throw new Exception("Not an 16bit Windows executable file");
			}

		}

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

		public static string ReadString(Stream stream)
		{
			int iLength = ReadByte(stream);
			byte[] abTemp = new byte[iLength];

			if (stream.Read(abTemp, 0, iLength) != iLength)
			{
				throw new Exception("Unexpected end of stream");
			}

			return Encoding.ASCII.GetString(abTemp);
		}
	}
}
