using IRB.Collections.Generic;

namespace Disassembler.CPU.OpCodes
{
	public static class OpCodeTable
	{
		public static void ParseTable()
		{
			StreamReader file = new StreamReader(@"C:\Users\rajko\Documents\Projects\Disassembler\CPU\Instructions.txt");
			List<OpCodeInstructionDefinition> aInstructions = new List<OpCodeInstructionDefinition>();

			while (!file.EndOfStream)
			{
				string? sLine = file.ReadLine();

				if (!string.IsNullOrEmpty(sLine))
				{
					string[] aParts = sLine.Split('\t');
					if (aParts.Length != 4 && aParts.Length != 6)
						throw new Exception("Malformed line '" + sLine + "'");

					string sName = aParts[0].Trim();
					string sDescription = aParts[1].Trim();
					string sOpCode = aParts[2].Trim();
					string sCPU = aParts[3].Trim();
					string sModifiedFlags = "";
					string sUndefinedFlags = "";
					if (aParts.Length > 4)
					{
						sModifiedFlags = aParts[4].Trim();
						sUndefinedFlags = aParts[5].Trim();
					}

					aInstructions.Add(new OpCodeInstructionDefinition(sName, sDescription, sOpCode, sCPU, sModifiedFlags, sUndefinedFlags));
				}
			}
			file.Close();

			BDictionary<int, List<int>> aTable = new BDictionary<int, List<int>>();

			for (int i = 0; i < aInstructions.Count; i++)
			{
				OpCodeInstructionDefinition instruction = aInstructions[i];

				if (instruction.OpCodes.Count == 0)
					throw new Exception("Instruction definition not properly initialized");

				byte[] aOpCodes = instruction.OpCodes[0].Expand();

				if (aOpCodes == null || aOpCodes.Length == 0)
					throw new Exception("Instruction definition not properly initialized");

				for (int j = 0; j < aOpCodes.Length; j++)
				{
					if (aTable.ContainsKey(aOpCodes[j]))
					{
						aTable.GetValueByKey(aOpCodes[j]).Add(i);
					}
					else
					{
						List<int> aTemp = new List<int>();
						aTemp.Add(i);
						aTable.Add(aOpCodes[j], aTemp);
					}
				}
			}

			StreamWriter writer = new StreamWriter("table.log");
			int tabs = 0;
			writer.WriteLine("{0}public partial class Instruction", Tabs(tabs));
			writer.WriteLine("{0}{{", Tabs(tabs));
			tabs++;
			writer.WriteLine("{0}private void Decode(MemoryStream stream)", Tabs(tabs));
			writer.WriteLine("{0}{{", Tabs(tabs));
			tabs++;
			writer.WriteLine("{0}bool bPrefix;", Tabs(tabs));
			writer.WriteLine("{0}bool bSignExtendImmediate = false;", Tabs(tabs));
			writer.WriteLine("{0}bool bReverseDirection = false;", Tabs(tabs));
			writer.WriteLine("{0}InstructionSizeEnum eOperandSize = this.eDefaultSize;", Tabs(tabs));
			writer.WriteLine("{0}InstructionSizeEnum eAddressSize = this.eDefaultSize;", Tabs(tabs));
			writer.WriteLine();
			writer.WriteLine("{0}do", Tabs(tabs));
			writer.WriteLine("{0}{{", Tabs(tabs));
			tabs++;
			writer.WriteLine("{0}bool bExitCase = false;", Tabs(tabs));
			writer.WriteLine("{0}this.iByte0 = stream.ReadByte();", Tabs(tabs));
			writer.WriteLine("{0}if (this.iByte0 < 0)", Tabs(tabs));
			writer.WriteLine("{0}{{", Tabs(tabs));
			writer.WriteLine("{0}bInvalid = true;", Tabs(tabs + 1));
			writer.WriteLine("{0}return;", Tabs(tabs + 1));
			writer.WriteLine("{0}}}", Tabs(tabs));
			writer.WriteLine("{0}this.aBytes.Add((byte)this.iByte0);", Tabs(tabs));
			writer.WriteLine();
			writer.WriteLine("{0}bPrefix = false;", Tabs(tabs));
			writer.WriteLine("{0}switch(this.iByte0)", Tabs(tabs));
			writer.WriteLine("{0}{{", Tabs(tabs));
			tabs++;

			for (int i = 0; i < 256; i++)
			{
				if (aTable.ContainsKey(i))
				{
					List<int> aTemp = aTable.GetValueByKey(i);

					if (i == 0x90)
					{
						// NOP instruction, clear xchg instruction
						for (int j = 0; j < aTemp.Count; j++)
						{
							if (aInstructions[aTemp[j]].Instruction != CPUInstructionEnum.NOP)
							{
								aTemp.RemoveAt(j);
								j--;
							}
						}
					}

					if (aTemp.Count > 0)
					{
						writer.WriteLine("{0}case 0x{1:x2}:", Tabs(tabs), i);

						for (int j = i + 1; j < 256; j++)
						{
							if (aTable.ContainsKey(j))
							{
								bool bMatch = false;
								List<int> aTemp1 = aTable.GetValueByKey(j);

								if (aTemp.Count == aTemp1.Count)
								{
									bMatch = true;

									for (int k = 0; k < aTemp.Count; k++)
									{
										if (aTemp[k] != aTemp1[k])
										{
											bMatch = false;
											break;
										}
									}
								}

								if (bMatch)
								{
									// we have a match, no need to repeat it again
									writer.WriteLine("{0}case 0x{1:x2}:", Tabs(tabs), j);
									aTable.GetValueByKey(j).Clear();
								}
							}
						}

						tabs++;
						if (aTemp.Count == 1)
						{
							// only one instruction to decode
							EncodeInstruction(writer, aInstructions, aTemp[0], tabs, 0);
						}
						else
						{
							// multiple instructions to decode
							EncodeInstructions(writer, aInstructions, aTemp, tabs, 1);
						}
						writer.WriteLine("{0}break;", Tabs(tabs));
						tabs--;
					}
				}
				else
				{
					//writer.WriteLine("\t\tcase 0x{0:x2}:", i);
					//writer.WriteLine("\t\t\t// This OpCode is not defined");
					//writer.WriteLine("\t\t\tbreak;");
				}
			}
			writer.WriteLine("{0}default:", Tabs(tabs));
			writer.WriteLine("{0}bInvalid = true;", Tabs(tabs + 1));
			writer.WriteLine("{0}break;", Tabs(tabs + 1));
			tabs--;
			writer.WriteLine("{0}}}", Tabs(tabs));
			tabs--;
			writer.WriteLine("{0}}} while(bPrefix);", Tabs(tabs));
			writer.WriteLine();
			writer.WriteLine("{0}if (bReverseDirection)", Tabs(tabs));
			writer.WriteLine("{0}{{", Tabs(tabs));
			tabs++;
			writer.WriteLine("{0}InstructionParameter oTemp = this.aParameters[0];", Tabs(tabs));
			writer.WriteLine("{0}this.aParameters[0] = this.aParameters[1];", Tabs(tabs));
			writer.WriteLine("{0}this.aParameters[1] = oTemp;", Tabs(tabs));
			tabs--;
			writer.WriteLine("{0}}}", Tabs(tabs));
			tabs--;
			writer.WriteLine("{0}}}", Tabs(tabs));
			tabs--;
			writer.WriteLine("{0}}}", Tabs(tabs));
			writer.Close();
		}

		private static void EncodeInstructions(StreamWriter writer, List<OpCodeInstructionDefinition> instructions, List<int> list, int tabs, int level)
		{
			int iMask = 0;
			bool bMaskEqual = true;

			for (int i = 0; i < list.Count; i++)
			{
				int index = list[i];
				OpCodeInstructionDefinition instruction = instructions[index];

				if (level >= instruction.OpCodes.Count)
				{
					//throw new Exception(string.Format("{0}// Can't expand, level {1} too great on: {2} ({3})", Tabs(tabs), level, instruction.Instruction, index));
					level--;
					bMaskEqual = false;
					break;
				}
				if (i == 0)
				{
					iMask = instruction.OpCodes[level].Mask;
					continue;
				}

				if (iMask != instruction.OpCodes[level].Mask)
				{
					bMaskEqual = false;
					break;
				}
			}

			if (bMaskEqual)
			{
				BDictionary<int, List<int>> aTable = new BDictionary<int, List<int>>();

				for (int i = 0; i < list.Count; i++)
				{
					int index = list[i];
					OpCodeInstructionDefinition instruction = instructions[index];
					int iOpCode = instruction.OpCodes[level].OpCode;

					if (aTable.ContainsKey(iOpCode))
					{
						aTable.GetValueByKey(iOpCode).Add(index);
					}
					else
					{
						List<int> aTemp = new List<int>();
						aTemp.Add(index);
						aTable.Add(iOpCode, aTemp);
					}
				}

				writer.WriteLine("{0}this.iByte{1} = stream.ReadByte();", Tabs(tabs), level);
				writer.WriteLine("{0}if (this.iByte{1} < 0)", Tabs(tabs), level);
				writer.WriteLine("{0}{{", Tabs(tabs));
				writer.WriteLine("{0}bInvalid = true;", Tabs(tabs + 1));
				writer.WriteLine("{0}return;", Tabs(tabs + 1));
				writer.WriteLine("{0}}}", Tabs(tabs));
				writer.WriteLine("{0}this.aBytes.Add((byte)this.iByte{1});", Tabs(tabs), level);
				writer.WriteLine();
				if (iMask != 0xff)
				{
					writer.WriteLine("{0}switch(this.iByte{1} & 0x{2:x2})", Tabs(tabs), level, iMask);
				}
				else
				{
					writer.WriteLine("{0}switch(this.iByte{1})", Tabs(tabs), level);
				}
				writer.WriteLine("{0}{{", Tabs(tabs));
				tabs++;

				for (int i = 0; i < 256; i++)
				{
					if (aTable.ContainsKey(i))
					{
						List<int> aTemp = aTable.GetValueByKey(i);
						if (aTemp.Count > 0)
						{
							writer.WriteLine("{0}case 0x{1:x2}:", Tabs(tabs), i);

							for (int j = i + 1; j < 256; j++)
							{
								if (aTable.ContainsKey(j))
								{
									bool bMatch = false;
									List<int> aTemp1 = aTable.GetValueByKey(j);

									if (aTemp.Count == aTemp1.Count)
									{
										bMatch = true;

										for (int k = 0; k < aTemp.Count; k++)
										{
											if (aTemp[k] != aTemp1[k])
											{
												bMatch = false;
												break;
											}
										}
									}

									if (bMatch)
									{
										// we have a match, no need to repeat it again
										writer.WriteLine("{0}case 0x{1:x2}:", Tabs(tabs), j);
										aTable.GetValueByKey(j).Clear();
									}
								}
							}
							tabs++;

							if (aTemp.Count == 1)
							{
								// only one instruction to decode
								EncodeInstruction(writer, instructions, aTemp[0], tabs, level + 1);
							}
							else
							{
								// multiple instructions to decode
								EncodeInstructions(writer, instructions, aTemp, tabs, level + 1);
							}
							writer.WriteLine("{0}break;", Tabs(tabs));
							tabs--;
						}
					}
				}
				writer.WriteLine("{0}default:", Tabs(tabs));
				writer.WriteLine("{0}bInvalid = true;", Tabs(tabs + 1));
				writer.WriteLine("{0}break;", Tabs(tabs + 1));
				tabs--;
				writer.WriteLine("{0}}}", Tabs(tabs));
			}
			else
			{
				writer.WriteLine("{0}// Multiple instructions, alternate method", Tabs(tabs));

				// first separate instructions with mask and without mask
				// there should be no more than one instruction without mask
				BDictionary<int, List<int>> aTable1 = new BDictionary<int, List<int>>();
				List<int> aTable2 = new List<int>();

				for (int i = 0; i < list.Count; i++)
				{
					int index = list[i];
					OpCodeInstructionDefinition instruction = instructions[index];
					iMask = instruction.OpCodes[level].Mask;

					if (iMask > 0)
					{
						if (aTable1.ContainsKey(iMask))
						{
							aTable1.GetValueByKey(iMask).Add(index);
						}
						else
						{
							List<int> aTemp = new List<int>();
							aTemp.Add(index);
							aTable1.Add(iMask, aTemp);
						}
					}
					else
					{
						aTable2.Add(index);
					}
				}

				// now we have two tables, first is mask based, and the other should be a single instruction

				writer.WriteLine("{0}this.iByte{1} = stream.ReadByte();", Tabs(tabs), level);
				writer.WriteLine("{0}if (this.iByte{1} < 0)", Tabs(tabs), level);
				writer.WriteLine("{0}{{", Tabs(tabs));
				writer.WriteLine("{0}bInvalid = true;", Tabs(tabs + 1));
				writer.WriteLine("{0}return;", Tabs(tabs + 1));
				writer.WriteLine("{0}}}", Tabs(tabs));
				writer.WriteLine("{0}this.aBytes.Add((byte)this.iByte{1});", Tabs(tabs), level);
				writer.WriteLine();

				// first list differentiates by descending mask size
				for (int i = 8; i >= 0; i--)
				{
					for (int j = 0; j < aTable1.Count; j++)
					{
						int iValue = aTable1[j].Key;
						int iCount = 0;

						for (int k = 0; k < 8; k++)
						{
							if ((iValue & (1 << k)) != 0)
								iCount++;
						}
						if (iCount == i)
						{
							if (aTable1[j].Value.Count > 1)
							{
								// make a third table, the masks are all equal, just differentiate by opcode
								iMask = aTable1[j].Key;
								BDictionary<int, List<int>> aTable3 = new BDictionary<int, List<int>>();
								List<int> list1 = aTable1[j].Value;

								// order opcodes ascending
								for (int k = 0; k < 256; k++)
								{
									for (int l = 0; l < list1.Count; l++)
									{
										int index = list1[l];
										OpCodeInstructionDefinition instruction = instructions[index];
										int iOpCode = instruction.OpCodes[level].OpCode;

										if (iOpCode == k)
										{
											if (aTable3.ContainsKey(iOpCode))
											{
												aTable3.GetValueByKey(iOpCode).Add(index);
											}
											else
											{
												List<int> aTemp = new List<int>();
												aTemp.Add(index);
												aTable3.Add(iOpCode, aTemp);
											}
										}
									}
								}

								writer.WriteLine("{0}bExitCase = false;", Tabs(tabs));
								if (iMask != 0xff)
								{
									writer.WriteLine("{0}switch(this.iByte{1} & 0x{2:x2})", Tabs(tabs), level, iMask);
								}
								else
								{
									writer.WriteLine("{0}switch(this.iByte{1})", Tabs(tabs), level);
								}
								writer.WriteLine("{0}{{", Tabs(tabs));
								tabs++;

								for (int k = 0; k < aTable3.Count; k++)
								{
									if (aTable3[k].Value.Count > 1)
									{
										writer.WriteLine("{0}case 0x{1:x2}:", Tabs(tabs), aTable3[k].Key);
										tabs++;
										EncodeInstructions(writer, instructions, aTable3[k].Value, tabs, level + 1);
										writer.WriteLine("{0}bExitCase = true;", Tabs(tabs));
										writer.WriteLine("{0}break;", Tabs(tabs));
										tabs--;
									}
									else
									{
										int index = aTable3[k].Value[0];

										writer.WriteLine("{0}case 0x{1:x2}:", Tabs(tabs), aTable3[k].Key);
										tabs++;
										EncodeInstruction(writer, instructions, index, tabs, level);
										writer.WriteLine("{0}bExitCase = true;", Tabs(tabs));
										writer.WriteLine("{0}break;", Tabs(tabs));
										tabs--;
									}
								}

								writer.WriteLine("{0}default:", Tabs(tabs));
								writer.WriteLine("{0}break;", Tabs(tabs + 1));
								tabs--;
								writer.WriteLine("{0}}}", Tabs(tabs));
								writer.WriteLine("{0}if (bExitCase)", Tabs(tabs));
								writer.WriteLine("{0}break;", Tabs(tabs + 1));
							}
							else
							{
								int index = aTable1[j].Value[0];
								OpCodeInstructionDefinition instruction = instructions[index];
								OpCodeDefinition op = instruction.OpCodes[level];

								// level 0 is just NOP and XCHG
								if (level > 0)
								{
									writer.WriteLine("{0}if ((this.iByte{1} & 0x{2:x2}) == 0x{3:x2})", Tabs(tabs), level, op.Mask, op.OpCode);
									writer.WriteLine("{0}{{", Tabs(tabs));
									EncodeInstruction(writer, instructions, index, tabs + 1, level);
									writer.WriteLine("{0}break;", Tabs(tabs + 1));
									writer.WriteLine("{0}}}", Tabs(tabs));
								}
								else if (instruction.Instruction == CPUInstructionEnum.NOP)
								{
									// special case for NOP
									EncodeInstruction(writer, instructions, index, tabs, level);
								}
							}

							aTable1.RemoveAt(j);
							j--;
						}
					}
				}

				if (aTable2.Count > 0)
				{
					if (aTable2.Count > 0)
					{
						throw new Exception("Multiple instructions encountered");
					}

					EncodeInstruction(writer, instructions, aTable2[0], tabs, level);
				}
			}
		}

		private static void EncodeInstruction(StreamWriter writer, List<OpCodeInstructionDefinition> instructions, int index, int tabs, int level)
		{
			OpCodeInstructionDefinition instruction = instructions[index];

			if (instruction.Instruction == CPUInstructionEnum.Undefined)
			{
				writer.WriteLine("{0}// Prefix: {1}", Tabs(tabs), instruction.Prefix);

				switch (instruction.Prefix)
				{
					case CPUInstructionPrefixEnum.Lock:
						writer.WriteLine("{0}this.bLockPrefix = true;", Tabs(tabs));
						break;
					case CPUInstructionPrefixEnum.OperandSize:
						writer.WriteLine("{0}eOperandSize = (this.eDefaultSize == InstructionSizeEnum.Word) ? InstructionSizeEnum.DWord : InstructionSizeEnum.Word;", Tabs(tabs));
						break;
					case CPUInstructionPrefixEnum.AddressSize:
						writer.WriteLine("{0}eAddressSize = (this.eDefaultSize == InstructionSizeEnum.Word) ? InstructionSizeEnum.DWord : InstructionSizeEnum.Word;", Tabs(tabs));
						break;
					case CPUInstructionPrefixEnum.REPE:
						writer.WriteLine("{0}this.eRepPrefix = InstructionPrefixEnum.REPE;", Tabs(tabs));
						break;
					case CPUInstructionPrefixEnum.REPNE:
						writer.WriteLine("{0}this.eRepPrefix = InstructionPrefixEnum.REPNE;", Tabs(tabs));
						break;
					case CPUInstructionPrefixEnum.ES:
						writer.WriteLine("{0}this.eDefaultDataSegment = SegmentRegisterEnum.ES;", Tabs(tabs));
						break;
					case CPUInstructionPrefixEnum.CS:
						writer.WriteLine("{0}this.eDefaultDataSegment = SegmentRegisterEnum.CS;", Tabs(tabs));
						break;
					case CPUInstructionPrefixEnum.SS:
						writer.WriteLine("{0}this.eDefaultDataSegment = SegmentRegisterEnum.SS;", Tabs(tabs));
						break;
					case CPUInstructionPrefixEnum.DS:
						writer.WriteLine("{0}this.eDefaultDataSegment = SegmentRegisterEnum.DS;", Tabs(tabs));
						break;
					case CPUInstructionPrefixEnum.FS:
						writer.WriteLine("{0}this.eDefaultDataSegment = SegmentRegisterEnum.FS;", Tabs(tabs));
						break;
					case CPUInstructionPrefixEnum.GS:
						writer.WriteLine("{0}this.eDefaultDataSegment = SegmentRegisterEnum.GS;", Tabs(tabs));
						break;
					default:
						break;
				}

				writer.WriteLine("{0}bPrefix = true;", Tabs(tabs));
			}
			else
			{
				writer.WriteLine("{0}// {1} ({2})", Tabs(tabs), instruction.Instruction, index);
				for (int i = 0; i < instruction.OpCodes.Count; i++)
				{
					OpCodeDefinition op = instruction.OpCodes[i];

					if (op.Mask != 0 || op.Parameters.Count != 1 ||
						(op.Parameters[0].Type != OpCodeParameterTypeEnum.AccumulatorWithImmediateValue &&
						op.Parameters[0].Type != OpCodeParameterTypeEnum.ImmediateValueWithAccumulator &&
						op.Parameters[0].Type != OpCodeParameterTypeEnum.ImmediateValue &&
						op.Parameters[0].Type != OpCodeParameterTypeEnum.ImmediateMemoryAddressWithAccumulator &&
						op.Parameters[0].Type != OpCodeParameterTypeEnum.RelativeValue &&
						op.Parameters[0].Type != OpCodeParameterTypeEnum.ImmediateSegmentOffset &&
						op.Parameters[0].Type != OpCodeParameterTypeEnum.RegisterCL &&
						op.Parameters[0].Type != OpCodeParameterTypeEnum.RegisterAWithDX &&
						op.Parameters[0].Type != OpCodeParameterTypeEnum.RegisterDXWithA &&
						op.Parameters[0].Type != OpCodeParameterTypeEnum.ImmediateValue1 &&
						op.Parameters[0].Type != OpCodeParameterTypeEnum.ImmediateValue3))
					{
						if (i > level)
						{
							writer.WriteLine();
							writer.WriteLine("{0}// OpCode byte: {1}", Tabs(tabs), i);
							writer.WriteLine("{0}this.iByte{1} = stream.ReadByte();", Tabs(tabs), i);
							writer.WriteLine("{0}if (this.iByte{1} < 0)", Tabs(tabs), i);
							writer.WriteLine("{0}{{", Tabs(tabs));
							writer.WriteLine("{0}bInvalid = true;", Tabs(tabs + 1));
							writer.WriteLine("{0}return;", Tabs(tabs + 1));
							writer.WriteLine("{0}}}", Tabs(tabs));
							writer.WriteLine("{0}this.aBytes.Add((byte)this.iByte{1});", Tabs(tabs), i);
							writer.WriteLine();
						}
					}
					if (i > 0 && i > level && op.Mask != 0)
					{
						writer.WriteLine("{0}if ((this.iByte{1} & 0x{2:x2}) != 0x{3:x2})", Tabs(tabs), i, op.Mask, op.OpCode);
						writer.WriteLine("{0}{{", Tabs(tabs));
						writer.WriteLine("{0}bInvalid = true;", Tabs(tabs + 1));
						writer.WriteLine("{0}}}", Tabs(tabs));
					}

					for (int k = 0; k < op.Parameters.Count; k++)
					{
						EncodeParameter(writer, op.Parameters[k], tabs, i);
					}
				}
				if (instruction.ClearedFlags != CPUFlagsEnum.Undefined)
					writer.WriteLine("{0}this.eClearedFlags = {1};", Tabs(tabs), OpCodeInstructionDefinition.FlagsToString(instruction.ClearedFlags));
				if (instruction.SetFlags != CPUFlagsEnum.Undefined)
					writer.WriteLine("{0}this.eSetFlags = {1};", Tabs(tabs), OpCodeInstructionDefinition.FlagsToString(instruction.SetFlags));
				if (instruction.ModifiedFlags != CPUFlagsEnum.Undefined)
					writer.WriteLine("{0}this.eModifiedFlags = {1};", Tabs(tabs), OpCodeInstructionDefinition.FlagsToString(instruction.ModifiedFlags));
				if (instruction.UndefinedFlags != CPUFlagsEnum.Undefined)
					writer.WriteLine("{0}this.eUndefinedFlags = {1};", Tabs(tabs), OpCodeInstructionDefinition.FlagsToString(instruction.UndefinedFlags));

				writer.WriteLine("{0}if (!bInvalid)", Tabs(tabs), instruction.Instruction);
				writer.WriteLine("{0}{{", Tabs(tabs));
				writer.WriteLine("{0}this.eCPU = CPUEnum.{1};", Tabs(tabs + 1), instruction.CPU.ToString());
				writer.WriteLine("{0}this.eInstruction = InstructionEnum.{1};", Tabs(tabs + 1), instruction.Instruction);
				writer.WriteLine("{0}this.sDescription = \"{1}\";", Tabs(tabs + 1), instruction.Description);
				writer.WriteLine("{0}}}", Tabs(tabs));
			}
		}

		private static void EncodeParameter(StreamWriter writer, OpCodeParameter param, int tabs, int level)
		{
			switch (param.Type)
			{
				case OpCodeParameterTypeEnum.SignExtend:
					writer.WriteLine("{0}bSignExtendImmediate = (this.iByte{1} & 0x{2:x2}) != 0;", Tabs(tabs), level, param.Mask);
					break;
				case OpCodeParameterTypeEnum.OperandSize:
					writer.WriteLine("{0}eOperandSize = ((this.iByte{1} & 0x{2:x2}) != 0) ? eOperandSize : InstructionSizeEnum.Byte;",
						Tabs(tabs), level, param.Mask);
					break;
				case OpCodeParameterTypeEnum.ReverseDirection:
					writer.WriteLine("{0}bReverseDirection = (this.iByte{1} & 0x{2:x2}) == 0;", Tabs(tabs), level, param.Mask);
					break;
				case OpCodeParameterTypeEnum.FPUDestination:
					writer.WriteLine("{0}this.bFPUDestination0 = (this.iByte{1} & 0x{2:x2}) == 0;", Tabs(tabs), level, param.Mask);
					break;
				case OpCodeParameterTypeEnum.FPUStackAddress:
					writer.WriteLine("{0}this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.FPUStackAddress, (uint)((this.iByte{1} & 0x{2:x2}) >> {3})));",
						Tabs(tabs), level, param.Mask, param.BitPosition);
					break;
				case OpCodeParameterTypeEnum.AccumulatorWithRegister:
					writer.WriteLine("{0}this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, 0));", Tabs(tabs));
					writer.WriteLine("{0}this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte{1} & 0x{2:x2}) >> {3})));",
						Tabs(tabs), level, param.Mask, param.BitPosition);
					break;
				case OpCodeParameterTypeEnum.Register:
					writer.WriteLine("{0}this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte{1} & 0x{2:x2}) >> {3})));",
						Tabs(tabs), level, param.Mask, param.BitPosition);
					break;
				case OpCodeParameterTypeEnum.RegisterCL:
					writer.WriteLine("{0}this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, InstructionSizeEnum.Byte, 1));",
						Tabs(tabs));
					break;
				case OpCodeParameterTypeEnum.RegisterAWithDX:
					writer.WriteLine("{0}this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, 0));", Tabs(tabs));
					writer.WriteLine("{0}this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, InstructionSizeEnum.Word, 2));", Tabs(tabs));
					break;
				case OpCodeParameterTypeEnum.RegisterDXWithA:
					writer.WriteLine("{0}this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, InstructionSizeEnum.Word, 2));", Tabs(tabs));
					writer.WriteLine("{0}this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, 0));", Tabs(tabs));
					break;
				case OpCodeParameterTypeEnum.SegmentRegister:
				case OpCodeParameterTypeEnum.SegmentRegisterNoCS:
				case OpCodeParameterTypeEnum.SegmentRegisterFSGS:
					writer.WriteLine("{0}this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.SegmentRegister, eOperandSize, (uint)((this.iByte{1} & 0x{2:x2}) >> {3})));",
						Tabs(tabs), level, param.Mask, param.BitPosition);
					break;
				case OpCodeParameterTypeEnum.MemoryAddressing:
					writer.WriteLine("{0}this.aParameters.Add(MemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte{1} & 0x{2:x2}));",
						Tabs(tabs), level, param.Mask);
					break;
				case OpCodeParameterTypeEnum.RegisterOrMemoryAddressing:
					writer.WriteLine("{0}this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte{1} & 0x{2:x2}));",
						Tabs(tabs), level, param.Mask);
					break;
				case OpCodeParameterTypeEnum.Condition:
					writer.WriteLine("{0}this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Condition, (uint)((this.iByte{1} & 0x{2:x2}) >> {3})));",
						Tabs(tabs), level, param.Mask, param.BitPosition);
					break;
				case OpCodeParameterTypeEnum.AccumulatorWithImmediateValue:
					writer.WriteLine("{0}this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, 0));", Tabs(tabs));
					writer.WriteLine("{0}this.aParameters.Add(ReadImmediate(stream, {1}, eOperandSize, bSignExtendImmediate));", Tabs(tabs), param.ByteSize);
					break;
				case OpCodeParameterTypeEnum.ImmediateValueWithAccumulator:
					writer.WriteLine("{0}this.aParameters.Add(ReadImmediate(stream, {1}, eOperandSize, bSignExtendImmediate));", Tabs(tabs), param.ByteSize);
					writer.WriteLine("{0}this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, 0));", Tabs(tabs));
					break;
				case OpCodeParameterTypeEnum.ImmediateValue:
					writer.WriteLine("{0}this.aParameters.Add(ReadImmediate(stream, {1}, eOperandSize, bSignExtendImmediate));", Tabs(tabs), param.ByteSize);
					break;
				case OpCodeParameterTypeEnum.ImmediateValue1:
					writer.WriteLine("{0}this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Immediate, InstructionSizeEnum.Byte, 1));", Tabs(tabs));
					break;
				case OpCodeParameterTypeEnum.ImmediateValue3:
					writer.WriteLine("{0}this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Immediate, InstructionSizeEnum.Byte, 3));", Tabs(tabs));
					break;
				case OpCodeParameterTypeEnum.ImmediateMemoryAddressWithAccumulator:
					writer.WriteLine("{0}this.aParameters.Add(MemoryImmediate(stream, this.eDefaultDataSegment, eAddressSize));", Tabs(tabs));
					writer.WriteLine("{0}this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, 0));", Tabs(tabs));
					break;
				case OpCodeParameterTypeEnum.RelativeValue:
					writer.WriteLine("{0}this.aParameters.Add(ReadRelative(stream, {1}, eOperandSize));", Tabs(tabs), param.ByteSize);
					break;
				case OpCodeParameterTypeEnum.ImmediateSegmentOffset:
					writer.WriteLine("{0}this.aParameters.Add(ReadSegmentOffset(stream, eOperandSize));", Tabs(tabs));
					break;
			}
		}

		private static string Tabs(int level)
		{
			return new string('\t', level);
		}
	}
}
