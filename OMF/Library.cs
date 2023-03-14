using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Disassembler.OMF
{
	public class Library
	{
		private int iPageSize = 16;
		private bool bCaseSensitive = false;
		private List<CModule> aModules = new List<CModule>();

		public Library(string path)
			: this(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
		{ }

		public Library(Stream stream)
		{
			bool bLibraryHeader = false;
			bool bLibraryEnd = false;
			long lDictOffset = stream.Length;
			int iDictBlockCount = 0;
			int iTemp = 0;
			StreamWriter oLog = new StreamWriter("log.txt");

			// read records
			while (!bLibraryEnd && stream.Position < stream.Length && stream.Position < lDictOffset)
			{
				byte bType = ReadByte(stream);

				switch (bType)
				{
					case 0x80:
						// THEADR Translator Header Record
						if (!bLibraryHeader)
						{
							throw new Exception("Not an OMF library file");
						}
						stream.Seek(-1, SeekOrigin.Current);
						CModule oModule = new CModule(stream, oLog);
						this.aModules.Add(oModule);
						AlignToPage(stream);
						oLog.WriteLine();
						break;
					case 0xF0:
						// Library Header Record
						if (bLibraryHeader)
						{
							// ignore repetitive Library Header Record

							//throw new Exception("Not an OMF library file");
						}
						this.iPageSize = ReadUInt16(stream) + 3;
						if (this.iPageSize < 16 || this.iPageSize > 32768)
						{
							throw new Exception("Invalid page size");
						}
						lDictOffset = ReadUInt32(stream);
						iDictBlockCount = ReadUInt16(stream);
						this.bCaseSensitive = (ReadByte(stream) & 0x1) != 0;
						AlignToPage(stream);
						bLibraryHeader = true;
						oLog.WriteLine("Library Header Record (0x{0:x2}): Page size: 0x{1:x4}, Dictionary offset: 0x{2:x8}, Dictionary block count: {3}, Case sensitive: {4}",
							bType, this.iPageSize, lDictOffset, iDictBlockCount, this.bCaseSensitive);
						oLog.WriteLine();
						break;
					case 0xF1:
						// Library End Record
						if (!bLibraryHeader)
						{
							throw new Exception("Not an OMF library file");
						}
						iTemp = ReadUInt16(stream);
						stream.Seek(iTemp, SeekOrigin.Current);
						AlignToPage(stream);
						bLibraryHeader = false;
						bLibraryEnd = true;
						oLog.WriteLine("Library End Record (0x{0:x2})", bType);
						oLog.WriteLine();
						break;
					default:
						throw new Exception("Unknown Record type");
				}
				oLog.Flush();
			}

			oLog.Close();
		}

		private byte ReadByte(Stream input)
		{
			int b0 = input.ReadByte();

			if (b0 < 0)
			{
				throw new Exception("Unexpected end of stream");
			}

			return (byte)(b0 & 0xff);
		}

		private int ReadUInt16(Stream input)
		{
			int b0 = input.ReadByte();
			int b1 = input.ReadByte();

			if (b0 < 0 || b1 < 0)
			{
				throw new Exception("Unexpected end of stream");
			}

			return (b0 & 0xff) | ((b1 & 0xff) << 8);
		}

		private long ReadUInt32(Stream input)
		{
			int b0 = input.ReadByte();
			int b1 = input.ReadByte();
			int b2 = input.ReadByte();
			int b3 = input.ReadByte();

			if (b0 < 0 || b1 < 0 || b2 < 0 || b3 < 0)
			{
				throw new Exception("Unexpected end of stream");
			}

			return (long)((uint)((uint)b0 & 0xff) | (uint)(((uint)b1 & 0xff) << 8) |
				(uint)(((uint)b2 & 0xff) << 16) | (uint)(((uint)b3 & 0xff) << 24));
		}

		private byte[] ReadBlock(Stream input, int size)
		{
			byte[] abTemp = new byte[size];

			if (input.Read(abTemp, 0, size) != size)
			{
				throw new Exception("Unexpected end of stream");
			}

			return abTemp;
		}

		private string ReadString(Stream stream)
		{
			int iLength = ReadByte(stream);
			byte[] abTemp = new byte[iLength];

			if (stream.Read(abTemp, 0, iLength) != iLength)
			{
				throw new Exception("Unexpected end of stream");
			}

			return Encoding.ASCII.GetString(abTemp);
		}

		private void AlignToPage(Stream input)
		{
			long lOffset = input.Position & (this.iPageSize - 1);

			if (lOffset > 0)
			{
				input.Seek(this.iPageSize - lOffset, SeekOrigin.Current);
			}
		}

		public bool CaseSensitive
		{
			get
			{
				return this.bCaseSensitive;
			}
		}

		public List<CModule> Modules
		{
			get
			{
				return this.aModules;
			}
		}
	}
}
