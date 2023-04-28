using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Disassembler.OMF
{
	public class OMFLibrary
	{
		private int iPageSize = 16;
		private bool bCaseSensitive = false;

		public OMFLibrary(string path)
			: this(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
		{
		}

		public OMFLibrary(Stream input)
		{
			bool bLibraryHeader = false;
			bool bLibraryEnd = false;
			long lDictOffset = input.Length;
			int iDictBlockCount = 0;
			byte bTemp = 0;
			int iTemp = 0;
			long lTemp = 0;
			string sTemp = null;
			byte[] abTemp = null;
			StreamWriter log = new StreamWriter("log.txt");

			// read records
			while (!bLibraryEnd && input.Position < input.Length && input.Position < lDictOffset)
			{
				byte bType = ReadByte(input);

				switch (bType)
				{
					case 0x80:
						// THEADR Translator Header Record
						if (!bLibraryHeader)
						{
							throw new Exception("Not an OMF library file");
						}
						iTemp = ReadUInt16(input);
						iTemp = ReadByte(input);
						abTemp = ReadBlock(input, iTemp);
						sTemp = Encoding.ASCII.GetString(abTemp);
						bTemp = ReadByte(input);
						log.WriteLine("Translator Header Record (0x{0:x2}): Name {1}", bType, sTemp);
						break;
					case 0x82:
						// LHEADR Library Module Header Record
						if (!bLibraryHeader)
						{
							throw new Exception("Not an OMF library file");
						}
						iTemp = ReadUInt16(input);
						input.Seek(iTemp - 1, SeekOrigin.Current);
						bTemp = ReadByte(input);
						log.WriteLine("Library Module Header Record (0x{0:x2})", bType);
						break;
					case 0x88:
						// COMENT Comment Record (Including all comment class extensions)
						if (!bLibraryHeader)
						{
							throw new Exception("Not an OMF library file");
						}
						iTemp = ReadUInt16(input);
						input.Seek(iTemp - 1, SeekOrigin.Current);
						bTemp = ReadByte(input);
						log.WriteLine("Comment Record (0x{0:x2})", bType);
						break;
					case 0x8A:
					case 0x8B:
						// MODEND Module End Record
						if (!bLibraryHeader)
						{
							throw new Exception("Not an OMF library file");
						}
						iTemp = ReadUInt16(input);
						input.Seek(iTemp - 1, SeekOrigin.Current);
						bTemp = ReadByte(input);
						AlignToPage(input);
						log.WriteLine("Module End Record (0x{0:x2})", bType);
						log.WriteLine();
						break;
					case 0x8C:
						// EXTDEF External Names Definition Record
						if (!bLibraryHeader)
						{
							throw new Exception("Not an OMF library file");
						}
						iTemp = ReadUInt16(input);
						input.Seek(iTemp - 1, SeekOrigin.Current);
						bTemp = ReadByte(input);
						log.WriteLine("External Names Definition Record (0x{0:x2})", bType);
						break;
					case 0x90:
					case 0x91:
						// PUBDEF Public Names Definition Record
						if (!bLibraryHeader)
						{
							throw new Exception("Not an OMF library file");
						}
						iTemp = ReadUInt16(input);
						input.Seek(iTemp - 1, SeekOrigin.Current);
						bTemp = ReadByte(input);
						log.WriteLine("Public Names Definition Record (0x{0:x2})", bType);
						break;
					case 0x94:
					case 0x95:
						// LINNUM Line Numbers Record
						if (!bLibraryHeader)
						{
							throw new Exception("Not an OMF library file");
						}
						iTemp = ReadUInt16(input);
						input.Seek(iTemp - 1, SeekOrigin.Current);
						bTemp = ReadByte(input);
						log.WriteLine("Line Numbers Record (0x{0:x2})", bType);
						break;
					case 0x96:
						// LNAMES List of Names Record
						if (!bLibraryHeader)
						{
							throw new Exception("Not an OMF library file");
						}
						iTemp = ReadUInt16(input);
						input.Seek(iTemp - 1, SeekOrigin.Current);
						bTemp = ReadByte(input);
						log.WriteLine("List of Names Record (0x{0:x2})", bType);
						break;
					case 0x98:
					case 0x99:
						// SEGDEF Segment Definition Record
						if (!bLibraryHeader)
						{
							throw new Exception("Not an OMF library file");
						}
						iTemp = ReadUInt16(input);
						input.Seek(iTemp - 1, SeekOrigin.Current);
						bTemp = ReadByte(input);
						log.WriteLine("Segment Definition Record (0x{0:x2})", bType);
						break;
					case 0x9A:
						// GRPDEF Group Definition Record
						if (!bLibraryHeader)
						{
							throw new Exception("Not an OMF library file");
						}
						iTemp = ReadUInt16(input);
						input.Seek(iTemp - 1, SeekOrigin.Current);
						bTemp = ReadByte(input);
						log.WriteLine("Group Definition Record (0x{0:x2})", bType);
						break;
					case 0x9C:
					case 0x9D:
						// FIXUPP Fixup Record
						if (!bLibraryHeader)
						{
							throw new Exception("Not an OMF library file");
						}
						iTemp = ReadUInt16(input);
						input.Seek(iTemp - 1, SeekOrigin.Current);
						bTemp = ReadByte(input);
						log.WriteLine("Fixup Record (0x{0:x2})", bType);
						break;
					case 0xA0:
					case 0xA1:
						// LEDATA Logical Enumerated Data Record
						if (!bLibraryHeader)
						{
							throw new Exception("Not an OMF library file");
						}
						iTemp = ReadUInt16(input);
						input.Seek(iTemp - 1, SeekOrigin.Current);
						bTemp = ReadByte(input);
						log.WriteLine("Logical Enumerated Data Record (0x{0:x2})", bType);
						break;
					case 0xA2:
					case 0xA3:
						// LIDATA Logical Iterated Data Record
						if (!bLibraryHeader)
						{
							throw new Exception("Not an OMF library file");
						}
						iTemp = ReadUInt16(input);
						input.Seek(iTemp - 1, SeekOrigin.Current);
						bTemp = ReadByte(input);
						log.WriteLine("Logical Iterated Data Record (0x{0:x2})", bType);
						break;
					case 0xB0:
						// COMDEF Communal Names Definition Record
						if (!bLibraryHeader)
						{
							throw new Exception("Not an OMF library file");
						}
						iTemp = ReadUInt16(input);
						input.Seek(iTemp - 1, SeekOrigin.Current);
						bTemp = ReadByte(input);
						log.WriteLine("Communal Names Definition Record (0x{0:x2})", bType);
						break;
					case 0xB2:
					case 0xB3:
						// BAKPAT Backpatc Record
						if (!bLibraryHeader)
						{
							throw new Exception("Not an OMF library file");
						}
						iTemp = ReadUInt16(input);
						input.Seek(iTemp - 1, SeekOrigin.Current);
						bTemp = ReadByte(input);
						log.WriteLine("Backpatc Record (0x{0:x2})", bType);
						break;
					case 0xB4:
						// LEXTDEF Local External Names Definition Record
						if (!bLibraryHeader)
						{
							throw new Exception("Not an OMF library file");
						}
						iTemp = ReadUInt16(input);
						input.Seek(iTemp - 1, SeekOrigin.Current);
						bTemp = ReadByte(input);
						log.WriteLine("Local External Names Definition Record (0x{0:x2})", bType);
						break;
					case 0xB6:
					case 0xB7:
						// LPUBDEF Local Public Names Definition Record
						if (!bLibraryHeader)
						{
							throw new Exception("Not an OMF library file");
						}
						iTemp = ReadUInt16(input);
						input.Seek(iTemp - 1, SeekOrigin.Current);
						bTemp = ReadByte(input);
						log.WriteLine("Local Public Names Definition Record (0x{0:x2})", bType);
						break;
					case 0xB8:
						// LCOMDEF Local Communal Names Definition Record
						if (!bLibraryHeader)
						{
							throw new Exception("Not an OMF library file");
						}
						iTemp = ReadUInt16(input);
						input.Seek(iTemp - 1, SeekOrigin.Current);
						bTemp = ReadByte(input);
						log.WriteLine("Local Communal Names Definition Record (0x{0:x2})", bType);
						break;
					case 0xBC:
						// CEXTDEF COMDAT External Names Definition Record
						if (!bLibraryHeader)
						{
							throw new Exception("Not an OMF library file");
						}
						iTemp = ReadUInt16(input);
						input.Seek(iTemp - 1, SeekOrigin.Current);
						bTemp = ReadByte(input);
						log.WriteLine("External Names Definition Record (0x{0:x2})", bType);
						break;
					case 0xC2:
					case 0xC3:
						// COMDAT Initialized Communal Data Record
						if (!bLibraryHeader)
						{
							throw new Exception("Not an OMF library file");
						}
						iTemp = ReadUInt16(input);
						input.Seek(iTemp - 1, SeekOrigin.Current);
						bTemp = ReadByte(input);
						log.WriteLine("Initialized Communal Data Record (0x{0:x2})", bType);
						break;
					case 0xC4:
					case 0xC5:
						// LINSYM Symbol Line Numbers Record
						if (!bLibraryHeader)
						{
							throw new Exception("Not an OMF library file");
						}
						iTemp = ReadUInt16(input);
						input.Seek(iTemp - 1, SeekOrigin.Current);
						bTemp = ReadByte(input);
						log.WriteLine("Symbol Line Numbers Record (0x{0:x2})", bType);
						break;
					case 0xC6:
						// ALIAS Alias Definition Record
						if (!bLibraryHeader)
						{
							throw new Exception("Not an OMF library file");
						}
						iTemp = ReadUInt16(input);
						input.Seek(iTemp - 1, SeekOrigin.Current);
						bTemp = ReadByte(input);
						log.WriteLine("Alias Definition Record (0x{0:x2})", bType);
						break;
					case 0xC8:
					case 0xC9:
						// NBKPAT Named Backpatc Record
						if (!bLibraryHeader)
						{
							throw new Exception("Not an OMF library file");
						}
						iTemp = ReadUInt16(input);
						input.Seek(iTemp - 1, SeekOrigin.Current);
						bTemp = ReadByte(input);
						log.WriteLine("Named Backpatc Record (0x{0:x2})", bType);
						break;
					case 0xCA:
						// LLNAMES Local Logical Names Definition Record
						if (!bLibraryHeader)
						{
							throw new Exception("Not an OMF library file");
						}
						iTemp = ReadUInt16(input);
						input.Seek(iTemp - 1, SeekOrigin.Current);
						bTemp = ReadByte(input);
						log.WriteLine("Local Logical Names Definition Record (0x{0:x2})", bType);
						break;
					case 0xCC:
						// VERNUM OMF Version Number Record
						if (!bLibraryHeader)
						{
							throw new Exception("Not an OMF library file");
						}
						iTemp = ReadUInt16(input);
						input.Seek(iTemp - 1, SeekOrigin.Current);
						bTemp = ReadByte(input);
						log.WriteLine("OMF Version Number Record (0x{0:x2})", bType);
						break;
					case 0xCE:
						// VENDEXT Vendor-specific OMF Extension Record
						if (!bLibraryHeader)
						{
							throw new Exception("Not an OMF library file");
						}
						iTemp = ReadUInt16(input);
						input.Seek(iTemp - 1, SeekOrigin.Current);
						bTemp = ReadByte(input);
						log.WriteLine("Vendor-specific OMF Extension Record (0x{0:x2})", bType);
						break;
					case 0xF0:
						// Library Header Record
						if (bLibraryHeader)
						{
							// ignore repetitive Library Header Record

							//throw new Exception("Not an OMF library file");
						}
						this.iPageSize = ReadUInt16(input) + 3;
						if (this.iPageSize < 16 || this.iPageSize > 32768)
						{
							throw new Exception("Invalid page size");
						}
						lDictOffset = ReadUInt32(input);
						iDictBlockCount = ReadUInt16(input);
						this.bCaseSensitive = (ReadByte(input) & 0x1) != 0;
						AlignToPage(input);
						bLibraryHeader = true;
						log.WriteLine("Library Header Record (0x{0:x2}): Page size: 0x{1:x4}, Dictionary offset: 0x{2:x8}, Dictionary block count: {3}, Case sensitive: {4}",
							bType, this.iPageSize, lDictOffset, iDictBlockCount, this.bCaseSensitive);
						log.WriteLine();
						break;
					case 0xF1:
						// Library End Record
						if (!bLibraryHeader)
						{
							throw new Exception("Not an OMF library file");
						}
						iTemp = ReadUInt16(input);
						input.Seek(iTemp, SeekOrigin.Current);
						AlignToPage(input);
						bLibraryHeader = false;
						bLibraryEnd = true;
						log.WriteLine("Library End Record (0x{0:x2})", bType);
						log.WriteLine();
						break;
					default:
						throw new Exception("Unknown Record type");
				}
				log.Flush();
			}
			log.Close();
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

		private void AlignToPage(Stream input)
		{
			long lOffset = input.Position & (this.iPageSize - 1);

			if (lOffset > 0)
			{
				input.Seek(this.iPageSize - lOffset, SeekOrigin.Current);
			}
		}
	}
}
