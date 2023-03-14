using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Disassembler.OMF
{
    public class CModule
    {
        private string sName = null;
		private List<SegmentDefinition> aSegments = new List<SegmentDefinition>();
		private List<SegmentGroupDefinition> aSegmentGroups = new List<SegmentGroupDefinition>();
		private List<PublicNameDefinition> aPublicNames = new List<PublicNameDefinition>();
		private List<PublicNameDefinition> aLocalPublicNames = new List<PublicNameDefinition>();
		private List<LogicalData> aDataRecords = new List<LogicalData>();
		private List<string> aExternalNames = new List<string>();

		public CModule(string path)
			: this(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read), new StreamWriter("moduleLog.txt"))
		{ }

		public CModule(Stream stream, StreamWriter log)
        {
            bool bModuleEnd = false;
            string sTemp = null;
			List<string> aNameList = new List<string>();
			SegmentDefinition segDef = null;
			SegmentGroupDefinition segGrp = null;
			PublicNameDefinition pubDef = null;
			LogicalData dataRec = null;
			List<Fixup> aFixups = null;

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
                    case 0x88:
                        // COMENT Comment Record (Including all comment class extensions)
                        log.WriteLine("Comment Record (0x{0:x2})", bType);
						// we can safely ignore those as they are completely uninformative
						/*for (int i = 3; i < oRecord.Length - 1; i++)
						{
							if (i > 3)
								log.Write(", ");
							log.Write("0x{0:x2}", ReadByte(oRecord));
						}
						log.WriteLine();*/
                        break;
                    case 0x8A:
                        // MODEND Module End Record
						log.WriteLine("Module End Record (0x{0:x2})", bType);
                        bModuleEnd = true;
                        break;
                    case 0x8C:
                        // EXTDEF External Names Definition Record
                        log.Write("External Names Definition Record (0x{0:x2})", bType);
						sTemp = ReadString(oRecord);
						this.aExternalNames.Add(sTemp);
						// type index is ignored
						ReadByte(oRecord);
						log.WriteLine("'{0}'", sTemp);
                        break;
                    case 0x90:
                        // PUBDEF Public Names Definition Record
                        log.Write("Public Names Definition Record (0x{0:x2})", bType);
						pubDef = new PublicNameDefinition(oRecord, this.aSegments, this.aSegmentGroups);
						this.aPublicNames.Add(pubDef);
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
                    case 0x94:
                        // LINNUM Line Numbers Record
                        log.WriteLine("Line Numbers Record (0x{0:x2})", bType);
                        break;
                    case 0x96:
                        // LNAMES List of Names Record
                        log.Write("List of Names Record (0x{0:x2})", bType);
						//aNames.Clear();
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
                        log.Write("Segment Definition Record (0x{0:x2})", bType);
						segDef = new SegmentDefinition(oRecord, aNameList);
						aSegments.Add(segDef);
						log.WriteLine(": {0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}", 
							segDef.Alignment, segDef.FrameNumber, segDef.Offset,
							segDef.Combine, segDef.Big, segDef.PBit, 
							segDef.Name, segDef.ClassName, segDef.OverlayName);
                        break;
                    case 0x9A:
                        // GRPDEF Segment Group Definition Record
                        log.Write("Segment Group Definition Record (0x{0:x2})", bType);
						segGrp = new SegmentGroupDefinition(oRecord, aNameList);
						this.aSegmentGroups.Add(segGrp);
						log.Write(" : '{0}' {{", segGrp.Name);
						for (int i = 0; i < segGrp.Segments.Count; i++)
						{
							if (i > 0)
								log.Write(", ");
							log.Write("'{0}'", this.aSegments[segGrp.Segments[i]].Name);
						}
						log.WriteLine("}");
                        break;
                    case 0x9C:
                        // FIXUPP Fixup Record
						// unclear in the specification, had to do analysis
                        log.Write("Fixup Record (0x{0:x2})", bType);
						log.Write("(");
						/*int iCount = 0;
						oRecord.Seek(0, SeekOrigin.Begin);
						while (oRecord.Position < oRecord.Length)
						{
							if (iCount > 0)
								log.Write(", ");
							int iByte = ReadByte(oRecord);
							sTemp = Convert.ToString(iByte, 2);
							while (sTemp.Length < 8)
							{
								sTemp = "0" + sTemp;
							}
							log.Write("0x{0:x2} [{1}]", iByte, sTemp);
							iCount++;
						}
						log.WriteLine(")");
						oRecord.Seek(3, SeekOrigin.Begin);*/

						aFixups = new List<Fixup>();

						while (oRecord.Position < oRecord.Length - 1)
						{
							aFixups.Add(new Fixup(oRecord));
						}

						// sort ascending by offset
						aFixups.Sort(Fixup.CompareByOffset);

						this.aDataRecords[this.aDataRecords.Count - 1].Fixups.AddRange(aFixups);
						log.WriteLine("[");
						for (int i = 0; i < aFixups.Count; i++)
						{
							if (i > 0)
								log.WriteLine(", ");
							Fixup item = aFixups[i];
							if (item.CompareType(FixupItemTypeEnum.Thread))
							{
								if (item.CompareType(FixupItemTypeEnum.Frame))
								{
									log.Write("(Frame thread: {0}, Method: {1}",
										item.FrameThread, item.FrameMethod);
									if (item.HasFrameIndex)
										log.Write(", Index: {0}", item.FrameIndex);
									log.Write(")");
								}
								else
								{
									log.Write("(Target thread: {0}, Method: {1}",
										item.TargetThread, item.TargetMethod);
									if (item.HasTargetIndex)
										log.Write(", Index: {0}", item.TargetIndex);
									log.Write(")");
								}
							}
							else
							{
								log.Write("(Fix type: {0}, Location: {1}, DataOffset: 0x{2:x4}",
									(item.CompareType(FixupItemTypeEnum.SegmentRelativeFixup) ? "SegmentRelativeFixup" : "SelfRelativeFixup"),
									item.LocationType, item.Offset);

								if (item.CompareType(FixupItemTypeEnum.Frame))
								{
									log.Write(", Frame method: {0}", item.FrameMethod);
									if (item.HasFrameIndex)
										log.Write(", Index: {0}", item.FrameIndex);
								}
								else
								{
									log.Write(", Frame thread: {0}", item.FrameThread);
								}

								if (item.CompareType(FixupItemTypeEnum.Target))
								{
									log.Write(", Target method: {0}", item.TargetMethod);
									if (item.HasTargetIndex)
										log.Write(", Index: {0}", item.TargetIndex);
								}
								else
								{
									log.Write(", Target thread: {0}", item.TargetThread);
									if (item.HasTargetDisplacement)
										log.Write(", Displacement: {0}", item.TargetDisplacement);
								}

								log.Write(")");
							}
						}
						log.WriteLine("]");
                        break;
                    case 0xA0:
                        // LEDATA Logical Enumerated Data Record
                        log.Write("Logical Enumerated Data Record (0x{0:x2})", bType);
						dataRec = new LogicalData(oRecord, this.aSegments);
						aDataRecords.Add(dataRec);
						log.Write(" '{0}':0x{1:x4} (", dataRec.Segment.Name, dataRec.Offset);
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
                        log.WriteLine("Logical Iterated Data Record (0x{0:x2})", bType);
                        break;
                    case 0xB0:
                        // COMDEF Communal Names Definition Record
                        log.WriteLine("Communal Names Definition Record (0x{0:x2})", bType);
                        break;
                    case 0xB2:
                        // BAKPAT Backpatc Record
                        log.WriteLine("Backpatc Record (0x{0:x2})", bType);
                        break;
                    case 0xB4:
					case 0xB5:
                        // LEXTDEF Local External Names Definition Record
                        log.WriteLine("Local External Names Definition Record (0x{0:x2})", bType);
                        break;
                    case 0xB6:
                        // LPUBDEF Local Public Names Definition Record
                        log.Write("Local Public Names Definition Record (0x{0:x2})", bType);
						pubDef = new PublicNameDefinition(oRecord, this.aSegments, this.aSegmentGroups);
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
                    case 0xB8:
                        // LCOMDEF Local Communal Names Definition Record
                        log.WriteLine("Local Communal Names Definition Record (0x{0:x2})", bType);
                        break;
                    case 0xBC:
                        // CEXTDEF COMDAT External Names Definition Record
                        log.WriteLine("External Names Definition Record (0x{0:x2})", bType);
                        break;
                    case 0xC2:
                    case 0xC3:
                        // COMDAT Initialized Communal Data Record
                        log.WriteLine("Initialized Communal Data Record (0x{0:x2})", bType);
                        break;
                    case 0xC4:
                    case 0xC5:
                        // LINSYM Symbol Line Numbers Record
                        log.WriteLine("Symbol Line Numbers Record (0x{0:x2})", bType);
                        break;
                    case 0xC6:
                        // ALIAS Alias Definition Record
                        log.WriteLine("Alias Definition Record (0x{0:x2})", bType);
                        break;
                    case 0xC8:
                    case 0xC9:
                        // NBKPAT Named Backpatc Record
                        log.WriteLine("Named Backpatc Record (0x{0:x2})", bType);
                        break;
                    case 0xCA:
                        // LLNAMES Local Logical Names Definition Record
                        log.WriteLine("Local Logical Names Definition Record (0x{0:x2})", bType);
                        break;
                    case 0xCC:
                        // VERNUM OMF Version Number Record
                        log.WriteLine("OMF Version Number Record (0x{0:x2})", bType);
                        break;
                    case 0xCE:
                        // VENDEXT Vendor-specific OMF Extension Record
                        log.WriteLine("Vendor-specific OMF Extension Record (0x{0:x2})", bType);
                        break;
                    default:
                        throw new Exception("Unknown Record type");
                }
                log.Flush();
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

				if (iChecksum != 0)
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

		public List<SegmentDefinition> Segments
		{
			get
			{
				return this.aSegments;
			}
		}

		public List<SegmentGroupDefinition> SegmentGroups
		{
			get
			{
				return this.aSegmentGroups;
			}
		}

		public List<PublicNameDefinition> PublicNames
		{
			get
			{
				return this.aPublicNames;
			}
		}

		public List<PublicNameDefinition> LocalPublicNames
		{
			get
			{
				return this.aLocalPublicNames;
			}
		}

		public List<LogicalData> DataRecords
		{
			get
			{
				return this.aDataRecords;
			}
		}

		public List<string> ExternalNames
		{
			get
			{
				return this.aExternalNames;
			}
		}
	}
}
