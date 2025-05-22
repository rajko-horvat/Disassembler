using Disassembler.Formats.MZ;
using System.Text;

namespace Disassembler.Formats.OMF
{
	public class OMFLibrary
	{
		private int iPageSize = 16;
		private bool bCaseSensitive = false;
		private List<OMFOBJModule> aModules = new List<OMFOBJModule>();

		public OMFLibrary(string path)
		{
			FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
			bool bLibraryHeader = false;
			bool bLibraryEnd = false;
			long lDictOffset = stream.Length;
			int iDictBlockCount = 0;
			int iTemp = 0;
			StreamWriter oLog = new StreamWriter($"{Path.GetFileNameWithoutExtension(path)}.txt");

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
						OMFOBJModule oModule = new OMFOBJModule(stream, oLog);
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

		public static void MatchLibrary(OMFLibrary library, MZExecutable exe, List<ModuleMatch> matches)
		{
			for (int i = 0; i < library.Modules.Count; i++)
			{
				MatchModule(library.Modules[i], exe, matches);
			}
		}

		private static void MatchModule(OMFOBJModule module, MZExecutable exe, List<ModuleMatch> matches)
		{
			if (module.Name.EndsWith("crt0fp.asm"))
				return;

			// iterate through data records that contain code
			for (int i = 0; i < module.DataRecords.Count; i++)
			{
				OMFDataRecord moduleData = module.DataRecords[i];

				// skip module data that has less than two bytes of code
				int iLength = moduleData.Data.Length;
				for (int j = 0; j < moduleData.Fixups.Count; j++)
				{
					iLength -= moduleData.Fixups[j].Length;
				}
				if (iLength < 4)
					continue;

				iLength = moduleData.Data.Length;
				bool bSkip = true;
				for (int j = 0; j < iLength; j++)
				{
					if (moduleData.Data[j] != 0)
					{
						bSkip = false;
						break;
					}
				}
				if (bSkip)
					continue;

				if (moduleData.Segment != null &&
					(moduleData.Segment.ClassName.Equals("CODE", StringComparison.CurrentCultureIgnoreCase) ||
					moduleData.Segment.ClassName.Equals("_CODE", StringComparison.CurrentCultureIgnoreCase) ||
					moduleData.Segment.ClassName.Equals("TEXT", StringComparison.CurrentCultureIgnoreCase) ||
					moduleData.Segment.ClassName.Equals("_TEXT", StringComparison.CurrentCultureIgnoreCase)))
				{
					// skip segments that contain data or are too short
					if (exe.Data.Length >= moduleData.Data.Length)
					{
						int iPos = 0;
						int iPos1 = 0;
						int iRelIndex = 0;
						int iFixIndex = 0;

						for (; iPos < exe.Data.Length; iPos++)
						{
							// we have a match
							// also, continue searching for additional instances of the same module
							if (iPos1 >= moduleData.Data.Length && iFixIndex >= moduleData.Fixups.Count)
							{
								int iTemp = moduleData.Data.Length;
								matches.Add(new ModuleMatch(module, (uint)(iPos - iTemp), iTemp));
								Console.WriteLine("Matched library module {0} in segment {1} [0x{2:x} - 0x{3:x}]", module.Name, 0, iPos - iTemp, iPos - 1);
								iPos--;
								iPos1 = 0;
								iFixIndex = 0;

								// no data left to compare
								if (moduleData.Data.Length > exe.Data.Length - iPos)
									break;

								continue;
							}

							// three distinct categories
							// first, there are no relocations in a segment and no fixups in code
							if (exe.Relocations.Count == 0 && moduleData.Fixups.Count == 0)
							{
								if (exe.Data[iPos] == moduleData.Data[iPos1])
								{
									iPos1++;
									continue;
								}
								iPos1 = 0;
								// no data left to compare
								if (moduleData.Data.Length > exe.Data.Length - iPos)
									break;
							}
							// second, there are no relocations in a segment and some fixups in code
							else if (exe.Relocations.Count == 0 && moduleData.Fixups.Count > 0)
							{
								if (iFixIndex < moduleData.Fixups.Count &&
									moduleData.Fixups[iFixIndex].FixupLocationType == OMFFixupLocationTypeEnum.Base16bit)
								{
									// definitely not a match
									break;
								}
								// compensate when compiler replaces far call or jump with short one
								// call/jmp far ... with nop, push cs, call/jmp near ...
								else if (iFixIndex < moduleData.Fixups.Count &&
									(moduleData.Data[iPos1] == 0x9a || moduleData.Data[iPos1] == 0xea) &&
									moduleData.Fixups[iFixIndex].FixupLocationType == OMFFixupLocationTypeEnum.LongPointer32bit &&
									moduleData.Fixups[iFixIndex].DataOffset == iPos1 + 1 &&
									exe.Data[iPos + 1] == 0x90 && exe.Data[iPos + 2] == 0xe)
								{
									iPos += moduleData.Fixups[iFixIndex].Length;
									iPos1 += moduleData.Fixups[iFixIndex].Length + 1;
									iFixIndex++;
									continue;
								}
								// skip long pointer references
								else if (iFixIndex < moduleData.Fixups.Count &&
									moduleData.Fixups[iFixIndex].FixupLocationType == OMFFixupLocationTypeEnum.LongPointer32bit &&
									moduleData.Fixups[iFixIndex].DataOffset == iPos1)
								{
									iPos += moduleData.Fixups[iFixIndex].Length;
									iPos1 += moduleData.Fixups[iFixIndex].Length;
									iFixIndex++;
									continue;
								}
								// skip intra segment offset adjustments
								else if (iFixIndex < moduleData.Fixups.Count &&
									moduleData.Fixups[iFixIndex].DataOffset == iPos1 &&
									(moduleData.Fixups[iFixIndex].FixupLocationType == OMFFixupLocationTypeEnum.Offset16bit ||
									moduleData.Fixups[iFixIndex].FixupLocationType == OMFFixupLocationTypeEnum.Offset16bit_1))
								{
									iPos += moduleData.Fixups[iFixIndex].Length - 1;
									iPos1 += moduleData.Fixups[iFixIndex].Length;
									iFixIndex++;
									continue;
								}
								// and finaly compare only the data in the segment with module
								else if (exe.Data[iPos] == moduleData.Data[iPos1])
								{
									iPos1++;
									continue;
								}

								iPos1 = 0;
								iFixIndex = 0;

								// no data left to compare
								if (moduleData.Data.Length > exe.Data.Length - iPos)
									break;
							}
							// third, there are relocations in segment as well as fixups in code
							else
							{
								// compensate when compiler replaces far call or jump with short one
								// call/jmp far ... with nop, push cs, call/jmp near ...
								if (iFixIndex < moduleData.Fixups.Count &&
								iRelIndex < exe.Relocations.Count &&
								exe.Relocations[iRelIndex].Offset != iPos &&
								(moduleData.Data[iPos1] == 0x9a || moduleData.Data[iPos1] == 0xea) &&
								moduleData.Fixups[iFixIndex].FixupLocationType == OMFFixupLocationTypeEnum.LongPointer32bit &&
								moduleData.Fixups[iFixIndex].DataOffset == iPos1 + 1 &&
								exe.Data[iPos] == 0x90 && exe.Data[iPos + 1] == 0xe)
								{
									iPos += moduleData.Fixups[iFixIndex].Length;
									iPos1 += moduleData.Fixups[iFixIndex].Length + 1;
									iFixIndex++;
									continue;
								}
								// skip long pointer references
								if (iFixIndex < moduleData.Fixups.Count &&
								iRelIndex < exe.Relocations.Count &&
								exe.Relocations[iRelIndex].Offset != iPos &&
								moduleData.Fixups[iFixIndex].FixupLocationType == OMFFixupLocationTypeEnum.LongPointer32bit &&
								moduleData.Fixups[iFixIndex].DataOffset == iPos1)
								{
									iPos += moduleData.Fixups[iFixIndex].Length - 1;
									iPos1 += moduleData.Fixups[iFixIndex].Length;
									iFixIndex++;
									continue;
								}

								// no relocations for this position and data content matches
								else if (exe.Data[iPos] == moduleData.Data[iPos1] &&
									(iRelIndex >= exe.Relocations.Count || exe.Relocations[iRelIndex].Offset != iPos) &&
									(iFixIndex >= moduleData.Fixups.Count || moduleData.Fixups[iFixIndex].DataOffset != iPos1))
								{
									iPos1++;
									continue;
								}
								// segment and segment-offset fixup
								else if (iFixIndex < moduleData.Fixups.Count &&
									iRelIndex < exe.Relocations.Count &&
									exe.Relocations[iRelIndex].Offset == iPos &&
									moduleData.Fixups[iFixIndex].DataOffset == iPos1)
								{
									iPos += moduleData.Fixups[iFixIndex].Length - 1;
									iPos1 += moduleData.Fixups[iFixIndex].Length;
									iRelIndex++;
									iFixIndex++;
									continue;
								}
								// offset fixup
								else if (iFixIndex < moduleData.Fixups.Count &&
									moduleData.Fixups[iFixIndex].DataOffset == iPos1 &&
									(moduleData.Fixups[iFixIndex].FixupLocationType == OMFFixupLocationTypeEnum.Offset16bit ||
									moduleData.Fixups[iFixIndex].FixupLocationType == OMFFixupLocationTypeEnum.Offset16bit_1))
								{
									iPos += moduleData.Fixups[iFixIndex].Length - 1;
									iPos1 += moduleData.Fixups[iFixIndex].Length;
									iFixIndex++;
									// fixup for faulty library fixup data
									while (iFixIndex < moduleData.Fixups.Count && moduleData.Fixups[iFixIndex].DataOffset < iPos1)
									{
										iFixIndex++;
									}
									continue;
								}

								// not a match, start again
								if (iRelIndex < exe.Relocations.Count &&
									exe.Relocations[iRelIndex].Offset == iPos)
								{
									iPos += 1;
									iRelIndex++;
								}
								if (iPos1 > 0)
								{
									// compensate for partial match
									iPos -= iPos1;
								}
								iPos1 = 0;
								iFixIndex = 0;

								// no data left to compare
								if (moduleData.Data.Length > exe.Data.Length - iPos)
									break;
							}
						}

						// we have a match
						if (iPos1 >= moduleData.Data.Length && iFixIndex >= moduleData.Fixups.Count)
						{
							int iTemp = moduleData.Data.Length;
							matches.Add(new ModuleMatch(module, (uint)(iPos - iTemp), iTemp));
							Console.WriteLine("Matched library module {0} in segment {1} [0x{2:x} - 0x{3:x}]", module.Name, 0, iPos - iTemp, iPos - 1);
						}
					}
				}
			}
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

		public List<OMFOBJModule> Modules
		{
			get
			{
				return this.aModules;
			}
		}
	}
}
