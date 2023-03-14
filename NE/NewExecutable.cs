using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Disassembler.NE
{
	[Flags]
	public enum ContentFlagsEnum : int
	{
		SingleDataSegment = 1,
		MultipleDataSegments = 1 << 1,
		Reserved2 = 1 << 2,
		Reserved3 = 1 << 3,
		Undefined4 = 1 << 4,
		Undefined5 = 1 << 5,
		Undefined6 = 1 << 6,
		Undefined7 = 1 << 7,
		Reserved8 = 1 << 8,
		Reserved9 = 1 << 9,
		Undefined10 = 1 << 10,
		FirstSegmentIsLoader = 1 << 11,
		Undefined12 = 1 << 12,
		LinkErrors = 1 << 13,
		Reserved14 = 1 << 14,
		LibraryModule = 1 << 15
	}

	public class NewExecutable
	{
		private long lCSIP = 0;
		private long lSSSP = 0;

		private List<Segment> aSegments = new List<Segment>();
		private List<KeyValuePair<int, string>> aResidentNames = new List<KeyValuePair<int, string>>();
		private List<string> aModuleReferences = new List<string>();
		private List<Entry> aEntries = new List<Entry>();
		private List<KeyValuePair<int, string>> aNonResidentNames = new List<KeyValuePair<int, string>>();
		private List<NEResourceTypeContainer> aResources = new List<NEResourceTypeContainer>();
		private List<string> aResourceStrings = new List<string>();

		public NewExecutable(string path)
			: this(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
		{ }

		public NewExecutable(Stream stream)
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
			int iNEOffset = ReadUInt16(stream);
			stream.Seek(iNEOffset, SeekOrigin.Begin);
			iSignature = ReadUInt16(stream);
			if (iSignature != 0x454e)
			{
				throw new Exception("Not an 16bit Windows executable file");
			}
			byte bLinkerVersion = ReadByte(stream);
			byte bLinkerRevision = ReadByte(stream);

			int iEntryTableOffset = ReadUInt16(stream);
			int iEntryTableLength = ReadUInt16(stream);
			long lCRC32 = ReadUInt32(stream);
			ContentFlagsEnum eContentFlags = (ContentFlagsEnum)ReadUInt16(stream);
			int iAutoDataSegmentIndex = ReadUInt16(stream);
			int iLocalHeapSize = ReadUInt16(stream);
			int iStackSize = ReadUInt16(stream);
			this.lCSIP = ReadUInt32(stream);
			this.lSSSP = ReadUInt32(stream);

			int iSegmentTableCount = ReadUInt16(stream);
			int iModuleRefTableCount = ReadUInt16(stream);
			int iNonResidentNameTableSize = ReadUInt16(stream);

			int iSegmentTableRelOffset = ReadUInt16(stream);
			int iResourceTableRelOffset = ReadUInt16(stream);
			int iResidentNameTableRelOffset = ReadUInt16(stream);
			int iModuleRefTableRelOffset = ReadUInt16(stream);
			int iImportedNameTableRelOffset = ReadUInt16(stream);
			long lNonResidentNameTableOffset = ReadUInt32(stream);
			int iMovableEntryCount = ReadUInt16(stream);
			int iLogicalSectorSize = 1 << ReadUInt16(stream);
			int iResourceSegmentCount = ReadUInt16(stream);

			// sometimes unused
			byte bTargetOperationgSystem = ReadByte(stream);
			byte bEXEFlags = ReadByte(stream);
			int iFastLoadAreaOffset = ReadUInt16(stream);
			int iFastLoadAreaSize = ReadUInt16(stream);
			int iReserved01 = ReadUInt16(stream);
			int iMinWindowsVersion = ReadUInt16(stream);

			// read segments
			stream.Seek(iNEOffset + iSegmentTableRelOffset, SeekOrigin.Begin);
			for (int i = 0; i < iSegmentTableCount; i++)
			{
				this.aSegments.Add(new Segment(stream, iLogicalSectorSize));
			}

			// read resources
			if (iResourceSegmentCount > 0)
			{
				throw new Exception("Resource segments not yet implemented");
			}
			if (iResourceTableRelOffset > 0)
			{
				stream.Seek(iNEOffset + iResourceTableRelOffset, SeekOrigin.Begin);
				int rscAlignShift = ReadUInt16(stream);

				while (true)
				{
					int rtTypeID = ReadUInt16(stream);
					if (rtTypeID == 0)
						break;

					string sName = "";
					if ((rtTypeID & 0x8000) == 0)
					{
						long lTemp = stream.Position;
						stream.Seek(iNEOffset + iResourceTableRelOffset + rtTypeID, SeekOrigin.Begin);
						int iLength = ReadByte(stream);
						byte[] aName = new byte[iLength];
						stream.Read(aName, 0, iLength);
						sName = Encoding.ASCII.GetString(aName);
						stream.Seek(lTemp, SeekOrigin.Begin);
					}
					else
					{
						rtTypeID &= 0x7fff;
					}

					NEResourceTypeContainer resourceType = new NEResourceTypeContainer(rtTypeID, sName);

					int rtResourceCount = ReadUInt16(stream);
					long rtReserved = ReadUInt32(stream);

					for (int i = 0; i < rtResourceCount; i++)
					{
						long rnOffset = ReadUInt16(stream);
						int rnLength = ReadUInt16(stream);
						int rnFlags = ReadUInt16(stream);
						int rnID = ReadUInt16(stream);
						int rnHandle = ReadUInt16(stream);
						int rnUsage = ReadUInt16(stream);

						rnOffset <<= rscAlignShift;
						rnLength <<= rscAlignShift;
						long lTemp = stream.Position;
						stream.Seek(rnOffset, SeekOrigin.Begin);
						byte[] buffer = new byte[rnLength];
						stream.Read(buffer, 0, rnLength);
						sName = "";
						if ((rnID & 0x8000) == 0)
						{
							stream.Seek(iNEOffset + iResourceTableRelOffset + rnID, SeekOrigin.Begin);
							int iLength = ReadByte(stream);
							byte[] aName = new byte[iLength];
							stream.Read(aName, 0, iLength);
							sName = Encoding.ASCII.GetString(aName);
						}
						else
						{
							rnID &= 0x7fff;
						}
						stream.Seek(lTemp, SeekOrigin.Begin);

						resourceType.Resources.Add(new NEResource(rnOffset, buffer, rnFlags, rnID, sName));
					}
					this.aResources.Add(resourceType);
				}

				while (true)
				{
					int rscResourceNames = ReadByte(stream);
					if (rscResourceNames == 0)
						break;
					byte[] aName = new byte[rscResourceNames];
					stream.Read(aName, 0, rscResourceNames);
					this.aResourceStrings.Add(Encoding.ASCII.GetString(aName));
				}
			}

			// read Resident name strings
			stream.Seek(iNEOffset + iResidentNameTableRelOffset, SeekOrigin.Begin);
			while (true)
			{
				string sTemp = ReadString(stream);
				if (sTemp == null)
					break;
				int iOrdinal = ReadUInt16(stream);
				this.aResidentNames.Add(new KeyValuePair<int, string>(iOrdinal, sTemp));
			}

			// read Module reference table
			stream.Seek(iNEOffset + iModuleRefTableRelOffset, SeekOrigin.Begin);
			for (int i = 0; i < iModuleRefTableCount; i++)
			{
				int iOffset = iNEOffset + iImportedNameTableRelOffset + (int)ReadUInt16(stream);
				long lPosition = stream.Position;
				stream.Seek(iOffset, SeekOrigin.Begin);
				string sTemp = ReadString(stream);
				this.aModuleReferences.Add(sTemp);
				stream.Seek(lPosition, SeekOrigin.Begin);
			}

			// read Entry table
			// don't know how to handle 0xfe values (yet)
			stream.Seek(iNEOffset + iEntryTableOffset, SeekOrigin.Begin);
			byte[] abEntryTable = ReadBlock(stream, iEntryTableLength);
			MemoryStream stream1 = new MemoryStream(abEntryTable);
			int iIndex = 1;

			while (stream1.Position < stream1.Length)
			{
				int iCount = ReadByte(stream1);
				int iType = ReadByte(stream1);

				if (iType == 0)
				{
					// skip ordinal
					iIndex++;
				}
				else
				{
					for (int i = 0; i < iCount; i++)
					{
						string sName = null;
						for (int j = 0; j < this.aResidentNames.Count; j++)
						{
							if (this.aResidentNames[j].Key == iIndex)
							{
								sName = this.aResidentNames[j].Value;
								break;
							}
						}

						if (iType < 0xff)
						{
							int iFlag = ReadByte(stream1);
							int iOffset = ReadUInt16(stream1);
							this.aEntries.Add(new Entry(iType, sName, iFlag, iOffset));
						}
						else
						{
							int iFlag = ReadByte(stream1);
							int iInt3F = ReadUInt16(stream1);
							int iSegment = ReadByte(stream1);
							int iOffset = ReadUInt16(stream1);
							this.aEntries.Add(new Entry(iType, sName, iFlag, iInt3F, iSegment, iOffset));
						}
						iIndex++;
					}
				}
			}

			// read Nonresident table
			stream.Seek(lNonResidentNameTableOffset, SeekOrigin.Begin);
			while (stream.Position < stream.Position + iNonResidentNameTableSize)
			{
				string sTemp = ReadString(stream);
				if (sTemp == null)
					break;
				int iOrdinal = ReadUInt16(stream);
				this.aNonResidentNames.Add(new KeyValuePair<int, string>(iOrdinal, sTemp));
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

			if (iLength == 0)
				return null;

			if (stream.Read(abTemp, 0, iLength) != iLength)
			{
				throw new Exception("Unexpected end of stream");
			}

			return Encoding.ASCII.GetString(abTemp);
		}

		public long CSIP
		{
			get
			{
				return this.lCSIP;
			}
		}

		public long SSSP
		{
			get
			{
				return this.lSSSP;
			}
		}

		public List<Segment> Segments
		{
			get
			{
				return this.aSegments;
			}
		}

		public List<KeyValuePair<int, string>> ResidentNames
		{
			get
			{
				return this.aResidentNames;
			}
		}

		public List<string> ModuleReferences
		{
			get
			{
				return this.aModuleReferences;
			}
		}

		public List<Entry> Entries
		{
			get
			{
				return this.aEntries;
			}
		}

		public List<KeyValuePair<int, string>> NonResidentNames
		{
			get
			{
				return this.aNonResidentNames;
			}
		}

		public List<NEResourceTypeContainer> Resources
		{
			get { return this.aResources; }
		}

		public List<string> ResourceStrings
		{
			get { return this.aResourceStrings; }
		}

		public void ApplyRelocations()
		{
			for (int i = 0; i < this.aSegments.Count; i++)
			{
				Segment segment = this.aSegments[i];
				bool bDataSegment = (segment.Flags & SegmentFlagsEnum.DataSegment) == SegmentFlagsEnum.DataSegment;

				if (segment.Relocations.Count > 0)
				{
					MemoryStream stream = new MemoryStream(segment.Data);

					for (int j = 0; j < segment.Relocations.Count; j++)
					{
						Relocation relocation = segment.Relocations[j];

						switch (relocation.RelocationType)
						{
							case RelocationTypeEnum.InternalReference:
								// relocation.Parameter1, which is segment, is one (1) based
								switch (relocation.LocationType)
								{
									case LocationTypeEnum.Offset16:
										WriteWord(stream, relocation.Offset, relocation.Parameter2);
										break;
									case LocationTypeEnum.Segment16:
										WriteWord(stream, relocation.Offset, relocation.Parameter1 - 1);
										break;
									case LocationTypeEnum.SegmentOffset32:
										WriteWord(stream, relocation.Offset, relocation.Parameter2);
										WriteWord(stream, relocation.Offset + 2, relocation.Parameter1 - 1);
										break;
								}
								break;
							case RelocationTypeEnum.ImportedOrdinal:
								// relocation.Parameter1, which is module index and segment, is one (1) based
								switch (relocation.LocationType)
								{
									case LocationTypeEnum.Offset16:
										if (relocation.Parameter1 == 1 && relocation.Parameter2 == 113)
										{
											// special case for __AHSHIFT
											WriteWord(stream, relocation.Offset, 3);
										}
										else
										{
											WriteWord(stream, relocation.Offset, relocation.Parameter2);
										}
										break;
									case LocationTypeEnum.Segment16:
										WriteWord(stream, relocation.Offset, this.aSegments.Count + relocation.Parameter1 - 1);
										break;
									case LocationTypeEnum.SegmentOffset32:
										WriteWord(stream, relocation.Offset, relocation.Parameter2);
										WriteWord(stream, relocation.Offset + 2, this.aSegments.Count + relocation.Parameter1 - 1);
										break;
								}
								break;
							case RelocationTypeEnum.Additive:
								// relocation.Parameter1, which is segment, is one (1) based
								switch (relocation.LocationType)
								{
									case LocationTypeEnum.Segment16:
										WriteWord(stream, relocation.Offset, relocation.Parameter1 - 1);
										break;
									case LocationTypeEnum.Offset16:
									case LocationTypeEnum.SegmentOffset32:
										throw new Exception(string.Format("Relocation type {0} for location type {1} not implemented", 
											relocation.RelocationType.ToString(), relocation.LocationType.ToString()));
								}
								break;
							case RelocationTypeEnum.FPFixup:
								// ignore those
								break;
							case RelocationTypeEnum.ImportedName:
							case RelocationTypeEnum.OSFixup:
								throw new Exception(string.Format("Relocation type {0} not implemented", relocation.RelocationType.ToString()));
						}
					}

					stream.Close();
				}
			}
		}

		private void WriteWord(MemoryStream stream, int position, int value)
		{
			stream.Seek(position, SeekOrigin.Begin);
			stream.WriteByte((byte)(value & 0xff));
			stream.WriteByte((byte)((value & 0xff00) >> 8));
		}
	}
}
