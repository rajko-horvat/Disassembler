using System.Text;

namespace Disassembler.Formats.OMF
{
    public class OMFOBJModule
    {
        private string sName = "";
		private List<OMFSegmentDefinition> aSegments = new List<OMFSegmentDefinition>();
		private List<OMFSegmentGroupDefinition> aSegmentGroups = new List<OMFSegmentGroupDefinition>();
		private List<OMFExternalNameDefinition> aExternalNames = new List<OMFExternalNameDefinition>();
		private List<OMFPublicNameDefinition> aPublicNames = new List<OMFPublicNameDefinition>();
		private List<OMFPublicNameDefinition> aLocalPublicNames = new List<OMFPublicNameDefinition>();
		private List<OMFDataRecord> dataRecords = new List<OMFDataRecord>();

		public OMFOBJModule(string path)
			: this(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read), new StreamWriter("moduleLog.txt"))
		{ }

		public OMFOBJModule(Stream stream, StreamWriter log)
		{
			bool bModuleEnd = false;
			List<string> aNameList = new List<string>();
			OMFSegmentDefinition segDef;
			OMFSegmentGroupDefinition segGrp;
			OMFPublicNameDefinition pubDef;
			OMFDataRecord dataRec;
			List<OMFFixup> aFixups;
			int iCount;

			// read records
			while (!bModuleEnd && stream.Position < stream.Length)
			{
				MemoryStream oRecord = ReadRecord(stream);
				byte bType = ReadByte(oRecord);
				oRecord.Seek(2, SeekOrigin.Current); // skip record length field

				switch (bType)
				{
					case 0x80:
						// THEADR Translator Header Record
						this.sName = ReadString(oRecord);
						log.WriteLine("Translator Header Record (0x{0:x2}): Name: {1}", bType, this.sName);
						break;

					case 0x8A:
						// MODEND Module End Record
						log.WriteLine("MODEND - Module End Record (0x{0:x2})", bType);
						bModuleEnd = true;
						break;

					case 0x88:
						// COMENT Comment Record (Including all comment class extensions)
						log.WriteLine("COMENT - Comment Record (0x{0:x2}), ignored", bType);
						// we can safely ignore those as they are completely uninformative

						/*oRecord.Position = 0;
						for (int i = 0; i < oRecord.Length; i++)
						{
							if (i > 0)
								log.Write(", ");
							log.Write("0x{0:x2}", ReadByte(oRecord));
						}
						log.WriteLine(")");*/
						break;

					case 0x96:
						// LNAMES List of Names Record
						log.Write("LNAMES - List of Names Record (0x{0:x2})", bType);
						while (oRecord.Position < oRecord.Length - 1)
						{
							string sTemp1 = ReadString(oRecord);
							aNameList.Add(sTemp1);
							log.Write(", '{0}'", sTemp1);
						}
						log.WriteLine();
						break;

					case 0x98:
						// SEGDEF Segment Definition Record
						log.Write("SEGDEF - Segment Definition Record (0x{0:x2})", bType);
						segDef = new OMFSegmentDefinition(oRecord, aNameList);
						aSegments.Add(segDef);
						log.WriteLine(": {0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}",
							segDef.Alignment, segDef.FrameNumber, segDef.Offset,
							segDef.Combine, segDef.Big, segDef.PBit,
							segDef.Name, segDef.ClassName, segDef.OverlayName);
						break;

					case 0x9A:
						// GRPDEF Segment Group Definition Record
						log.Write("GRPDEF - Segment Group Definition Record (0x{0:x2})", bType);
						segGrp = new OMFSegmentGroupDefinition(oRecord, aNameList);
						this.aSegmentGroups.Add(segGrp);
						log.Write(" : '{0}' {{", segGrp.Name);
						for (int i = 0; i < segGrp.SegmentIndexes.Count; i++)
						{
							if (i > 0)
								log.Write(", ");
							log.Write("'{0}'", this.aSegments[segGrp.SegmentIndexes[i]].Name);
						}
						log.WriteLine("}");
						break;

					case 0x8C:
						// EXTDEF External Names Definition Record
						log.Write("EXTDEF - External Names Definition Record (0x{0:x2}) {{", bType);
						iCount = 0;
						while (oRecord.Position < oRecord.Length - 1)
						{
							OMFExternalNameDefinition extDef = new OMFExternalNameDefinition(oRecord);
							this.aExternalNames.Add(extDef);
							if (iCount > 0)
								log.Write(", ");
							log.Write("'{0}'", extDef.Name);
							iCount++;
						}
						log.WriteLine("}");
						break;

					case 0xB4:
						// LEXTDEF Local External Names Definition Record
						log.Write("LEXTDEF - Local External Names Definition Record (0x{0:x2}) {{", bType);
						iCount = 0;
						while (oRecord.Position < oRecord.Length - 1)
						{
							OMFExternalNameDefinition extDef = new OMFExternalNameDefinition(oRecord);
							this.aExternalNames.Add(extDef);
							if (iCount > 0)
								log.Write(", ");
							log.Write("'{0}'(0x{1:x2})", extDef.Name, extDef.TypeIndex);
							iCount++;
						}
						log.WriteLine("}");
						break;

					case 0x90:
						// PUBDEF Public Names Definition Record
						log.Write("PUBDEF - Public Names Definition Record (0x{0:x2})", bType);
						pubDef = new OMFPublicNameDefinition(oRecord, this.aSegments, this.aSegmentGroups);
						this.aPublicNames.Add(pubDef);
						if (pubDef.SegmentGroup != null)
							log.Write(", Group: '{0}'", pubDef.SegmentGroup.Name);
						if (pubDef.Segment != null)
							log.Write(", Segment: '{0}'", pubDef.Segment.Name);
						if (pubDef.BaseFrame > 0)
							log.Write(", Base frame: {0}", pubDef.BaseFrame);
						log.Write(" (");
						for (int i = 0; i < pubDef.PublicNames.Count; i++)
						{
							if (i > 0)
								log.Write(", ");
							log.Write("'{0}':0x{1:x4}", pubDef.PublicNames[i].Key, pubDef.PublicNames[i].Value);
						}
						log.WriteLine(")");
						break;

					case 0xB6:
						// LPUBDEF Local Public Names Definition Record
						log.Write("LPUBDEF - Local Public Names Definition Record (0x{0:x2})", bType);
						pubDef = new OMFPublicNameDefinition(oRecord, this.aSegments, this.aSegmentGroups);
						this.aLocalPublicNames.Add(pubDef);
						if (pubDef.SegmentGroup != null)
							log.Write(", Group: '{0}'", pubDef.SegmentGroup.Name);
						if (pubDef.Segment != null)
							log.Write(", Segment: '{0}'", pubDef.Segment.Name);
						log.Write(" (");
						for (int i = 0; i < pubDef.PublicNames.Count; i++)
						{
							if (i > 0)
								log.Write(", ");
							log.Write("'{0}':0x{1:x4}", pubDef.PublicNames[i].Key, pubDef.PublicNames[i].Value);
						}
						log.WriteLine(")");
						break;

					case 0xB0:
						// COMDEF Communal Names Definition Record
						log.WriteLine("COMDEF - Communal Names Definition Record (0x{0:x2}), Ignored", bType);
						// deprecated, ignore those
						/*oRecord.Position = 0;
						for (int i = 0; i < oRecord.Length; i++)
						{
							if (i > 0)
								log.Write(", ");
							byte byt = ReadByte(oRecord);
							log.Write("0x{0:x2}", byt);
							if (byt >= 0x20)
								log.Write($"'{(char)byt}'");
						}
						log.WriteLine(")");*/
						break;

					case 0xA0:
						// LEDATA Logical Enumerated Data Record
						log.Write("LEDATA - Logical Enumerated Data Record (0x{0:x2})", bType);
						dataRec = new OMFDataRecord(oRecord, this.aSegments, false);
						dataRecords.Add(dataRec);
						log.Write(" '{0}':0x{1:x4}, Length: 0x{2:x4} (", (dataRec.Segment != null) ? dataRec.Segment.Name : "", dataRec.Offset, dataRec.Data.Length);
						for (int i = 0; i < dataRec.Data.Length; i++)
						{
							if (i > 0)
								log.Write(", ");
							log.Write("0x{0:x2}", dataRec.Data[i]);
						}
						log.WriteLine(")");
						break;

					case 0xA2:
						// LIDATA Logical Iterated Data Record
						log.Write("LIDATA - Logical Iterated Data Record (0x{0:x2})", bType);
						dataRec = new OMFDataRecord(oRecord, this.aSegments, true);
						dataRecords.Add(dataRec);
						log.Write(" '{0}':0x{1:x4}, Length: 0x{2:x4} (", (dataRec.Segment != null) ? dataRec.Segment.Name : "", dataRec.Offset, dataRec.Data.Length);
						for (int i = 0; i < dataRec.Data.Length; i++)
						{
							if (i > 0)
								log.Write(", ");
							log.Write("0x{0:x2}", dataRec.Data[i]);
						}
						log.WriteLine(")");
						break;

					case 0x9C:
						// FIXUPP Fixup Record
						log.Write("FIXUPP - Fixup Record (0x{0:x2})", bType);
						log.Write(" {");
						/*oRecord.Position = 0;
						int iFixCount = 0;
						while (oRecord.Position < oRecord.Length - 1)
						{
							if (iFixCount > 0)
								log.Write(", ");
							log.Write($"0x{oRecord.ReadByte():x2}");
							iFixCount++;
						}
						log.Write("}");
						oRecord.Seek(3, SeekOrigin.Begin);*/

						aFixups = new List<OMFFixup>();
						//iFixCount = 0;
						//log.WriteLine();
						while (oRecord.Position < oRecord.Length - 1)
						{
							//log.WriteLine($"Index: {iFixCount}, Position: {oRecord.Position}");
							aFixups.Add(new OMFFixup(oRecord));
							//iFixCount++;
						}

						log.WriteLine("[");
						for (int i = 0; i < aFixups.Count; i++)
						{
							if (i > 0)
								log.WriteLine(", ");
							OMFFixup fixup = aFixups[i];
							if (fixup.Type == OMFFixupTypeEnum.TargetThread)
							{
								log.Write("\t(Target thread: {0}", fixup.TargetMethod);
								log.Write(", Thread index: {0}", fixup.ThreadIndex);
								log.Write(", Index: {0}", fixup.Index);
								log.Write(")");
							}
							else if (fixup.Type == OMFFixupTypeEnum.FrameThread)
							{
								log.Write("\t(Frame thread: {0}", fixup.FrameMethod);
								log.Write(", Thread index: {0}", fixup.ThreadIndex);
								if (fixup.Index >= 0)
									log.Write(", Index: {0}", fixup.Index);
								log.Write(")");
							}
							else
							{
								log.Write("\t(Fixup: {0}, Location: {1}, DataOffset: 0x{2:x4}",
									fixup.FixupMode, fixup.FixupLocationType, fixup.DataOffset);
								if (fixup.FrameMethod != OMFFrameMethodEnum.Undefined)
									log.Write(", Frame method: {0}", fixup.FrameMethod);
								log.Write(", Frame thread index: {0}", fixup.FrameThreadIndex);
								if (fixup.TargetMethod != OMFTargetMethodEnum.Undefined)
									log.Write(", Target method: {0}", fixup.TargetMethod);
								log.Write(", Target thread index: {0}", fixup.TargetThreadIndex);
								log.Write(", Target displacement: {0}", fixup.TargetDisplacement);
								log.Write(")");
							}

							switch (fixup.Type)
							{
								case OMFFixupTypeEnum.FrameThread:
								case OMFFixupTypeEnum.TargetThread:
									break;
								case OMFFixupTypeEnum.Fixup:
									if (this.dataRecords.Count > 0)
									{
										this.dataRecords[this.dataRecords.Count - 1].Fixups.Add(fixup);
									}
									else
									{
										throw new Exception("No data record before FIXUPP record");
									}
									break;
							}
						}
						log.WriteLine("]");
						break;

					default:
						throw new Exception("Unknown Record type");
				}
				log.Flush();
			}

			// sort fixups by data offset
			for (int i = 0; i < this.dataRecords.Count; i++)
			{
				this.dataRecords[i].Fixups.Sort(OMFFixup.CompareByOffset);
			}

			// Allow patching
			// Append same segment LEDATA records if they overlay each other
			for (int i = 0; i < this.dataRecords.Count; i++)
			{
				OMFDataRecord dataRecord = this.dataRecords[i];

				if (dataRecord.Segment != null)
				{
					int recordLastPosition = dataRecord.Offset + dataRecord.Data.Length - 1;

					for (int j = i + 1; j < this.dataRecords.Count; j++)
					{
						OMFDataRecord dataRecord1 = this.dataRecords[j];

						if (dataRecord.Offset != dataRecord1.Offset && dataRecord1.Segment != null &&
							dataRecord.Segment.Name.Equals(dataRecord1.Segment.Name, StringComparison.CurrentCultureIgnoreCase))
						{
							if (dataRecord1.Offset <= recordLastPosition)
							{
								int newDataSize = dataRecord1.Offset + dataRecord1.Data.Length;

								if (dataRecord.Data.Length < newDataSize)
								{
									dataRecord.IncreaseDataSize(newDataSize);
									Array.Copy(dataRecord1.Data, 0, dataRecord.Data, dataRecord1.Offset, dataRecord1.Data.Length);
									dataRecord.Fixups.AddRange(dataRecord1.Fixups);
									this.dataRecords.RemoveAt(j);
									i = -1;
									break;
								}
							}
						}
					}
				}
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

		public static MemoryStream ReadRecord(Stream stream)
		{
			ReadByte(stream);
			int iRecordLength = ReadUInt16(stream) + 3;
			stream.Seek(-3, SeekOrigin.Current);

			byte[] abTemp = new byte[iRecordLength];
			if (stream.Read(abTemp, 0, iRecordLength) != iRecordLength)
			{
				throw new Exception("Unexpected end of stream");
			}

			if (abTemp[iRecordLength - 1] > 0)
			{
				int iChecksum = 0;
				for (int i = 0; i < iRecordLength; i++)
				{
					iChecksum += abTemp[i];
					iChecksum &= 0xff;
				}

				if (iChecksum != 0 && iChecksum != 0xff)
				{
					throw new Exception("Record checksum is invalid");
				}
			}

			return new MemoryStream(abTemp);
		}

		public string Name
		{
			get
			{
				return this.sName;
			}
		}

		public List<OMFSegmentDefinition> Segments
		{
			get
			{
				return this.aSegments;
			}
		}

		public List<OMFSegmentGroupDefinition> SegmentGroups
		{
			get
			{
				return this.aSegmentGroups;
			}
		}

		public List<OMFPublicNameDefinition> PublicNames
		{
			get
			{
				return this.aPublicNames;
			}
		}

		public List<OMFPublicNameDefinition> LocalPublicNames
		{
			get
			{
				return this.aLocalPublicNames;
			}
		}

		public List<OMFDataRecord> DataRecords
		{
			get
			{
				return this.dataRecords;
			}
		}

		public List<OMFExternalNameDefinition> ExternalNames
		{
			get
			{
				return this.aExternalNames;
			}
		}
	}
}
