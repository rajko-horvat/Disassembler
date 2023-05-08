using Disassembler.CPU;
using Disassembler.MZ;
using Disassembler.NE;
using IRB.Collections.Generic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Disassembler.Decompiler
{
	public class MZFunction
	{
		private MZDecompiler oParent;
		private CallTypeEnum eCallType = CallTypeEnum.Cdecl;
		private int iParameterSize = -1;
		private string sName;
		private List<CParameter> aParameters = new List<CParameter>();
		private CType oReturnValue = CType.Void;
		private int iStackSize = 0;
		private int iOverlay = 0;

		// assembly stuff
		private ushort usSegment;
		private ushort usOffset;
		private uint uiLinearAddress;
		private uint uiStreamOffset;
		private List<Instruction> aInstructions = new List<Instruction>();

		// parsed statements
		private List<IStatement> aStatements = new List<IStatement>();

		public static BDictionary<InstructionEnum, int> Statistics = new BDictionary<InstructionEnum, int>();

		public MZFunction(MZDecompiler parent, CallTypeEnum callType, string name, List<CParameter> parameters, CType returnValue,
			int overlay, ushort segment, ushort offset, uint streamOffset)
		{
			this.oParent = parent;
			this.eCallType = callType;
			this.sName = name;
			this.aParameters = parameters;
			this.oReturnValue = returnValue;
			this.iOverlay = overlay;
			this.usSegment = segment;
			this.usOffset = offset;
			this.uiLinearAddress = MemoryRegion.ToLinearAddress(segment, offset);
			this.uiStreamOffset = streamOffset;

			int iSize = 0;
			if (parameters.Count > 0)
			{
				for (int i = 0; i < parameters.Count; i++)
				{
					CParameter param = parameters[i];
					if (param.Type != CType.Variable)
					{
						iSize += param.Type.Size;
					}
				}
				this.iParameterSize = iSize;
			}
		}

		public void Disassemble(MZDecompiler decompiler)
		{
			byte[] exeData;
			uint uiStart;

			if (this.iOverlay > 0)
			{
				exeData = this.oParent.Executable.Overlays[this.iOverlay - 1].Data;
				uiStart = this.usOffset;
			}
			else
			{
				exeData = this.oParent.Executable.Data;
				uiStart = MemoryRegion.ToLinearAddress(this.usSegment, this.usOffset) - this.uiStreamOffset;
			}

			if (uiStart >= exeData.Length)
			{
				Console.WriteLine($"Trying to disassemble outside of executable range in function {this.sName}, address 0x{this.usSegment:x4}:0x{this.usOffset:x4}");
				return;
			}

			MemoryStream stream = new MemoryStream(exeData);
			ushort ip = this.usOffset;
			stream.Seek(uiStart, SeekOrigin.Begin);
			List<ushort> aJumps = new List<ushort>();
			List<ushort> aSwitches = new List<ushort>();
			bool bNextNOP = false;
			Instruction instruction1;

			while (true)
			{
				if (ip >= stream.Length)
				{
					throw new Exception($"Trying to disassemble outside of executable range in function {this.sName}, address 0x{this.usSegment:x4}:0x{ip:x4}");
				}

				bool bEnd = false;
				for (int i = 0; i < this.aInstructions.Count; i++)
				{
					if (this.aInstructions[i].LinearAddress == MemoryRegion.ToLinearAddress(this.usSegment, ip))
					{
						bEnd = true;
						break;
					}
				}

				if (!bEnd)
				{
					InstructionParameter parameter;
					Instruction instruction = new Instruction(this.usSegment, ip, stream);
					this.aInstructions.Add(instruction);
					ip += (ushort)instruction.Bytes.Count;

					if (bNextNOP)
					{
						bNextNOP = false;
						instruction.InstructionType = InstructionEnum.NOP;
					}

					switch (instruction.InstructionType)
					{
						case InstructionEnum.CALL:
							parameter = instruction.Parameters[0];
							if (parameter.Type == InstructionParameterTypeEnum.MemoryAddress && parameter.Value == 6 &&
								this.usSegment == 0x3045 && parameter.Displacement == 0x588e)
							{
								// special case for indirect exit() function
								bEnd = true;
							}
							break;

						case InstructionEnum.JMP:
							parameter = instruction.Parameters[0];
							if (parameter.Type == InstructionParameterTypeEnum.Relative)
							{
								ip = AddRelativeToOffset(instruction, parameter);
								if (this.iOverlay > 0)
								{
									stream.Seek(ip, SeekOrigin.Begin);
								}
								else
								{
									stream.Seek(MemoryRegion.ToLinearAddress(this.usSegment, ip) - this.uiStreamOffset, SeekOrigin.Begin);
								}
							}
							else if (parameter.Type == InstructionParameterTypeEnum.MemoryAddress && parameter.Value == 6)
							{
								throw new Exception(string.Format("Relative jmp to {0} at function {1}:0x{2:x} - jcc at 0x{3:x}",
									parameter.ToString(), this.usSegment, this.usOffset, instruction.Offset));
							}
							else if (parameter.Type == InstructionParameterTypeEnum.MemoryAddress)
							{
								// probably switch statement
								aSwitches.Add(instruction.Offset);
								//Console.WriteLine($"Switch statement {parameter.ToString()} in function {this.sName} - instruction at 0x{this.usSegment:x4}:0x{instruction.Offset:x4}");
								bEnd = true;
							}
							else
							{
								Console.WriteLine($"Jump to relative address {parameter.ToString()} in function {this.sName} - instruction at 0x{this.usSegment:x4}:0x{instruction.Offset:x4}");
								// treat this as end of a instruction stream
								bEnd = true;
							}
							break;

						case InstructionEnum.JMPF:
							/*parameter = instruction.Parameters[0];
							if (parameter.Type != InstructionParameterTypeEnum.SegmentOffset)
							{
								throw new Exception($"Indirect jmp to {parameter.ToString()} in function {this.sName} - jmp at 0x{this.usSegment:x4}:0x{ip:x4}");
							}*/
							bEnd = true;
							break;

						case InstructionEnum.Jcc:
							parameter = instruction.Parameters[1];
							if (parameter.Type != InstructionParameterTypeEnum.Relative)
								throw new Exception(
									$"Relative address offset expected, but got indirect {parameter.ToString()} in function {this.sName} - instruction at 0x{this.usSegment:x4}:0x{instruction.Offset:x4}");

							aJumps.Add(AddRelativeToOffset(instruction, parameter));
							break;

						case InstructionEnum.LOOP:
						case InstructionEnum.LOOPNZ:
						case InstructionEnum.LOOPZ:
						case InstructionEnum.JCXZ:
							parameter = instruction.Parameters[0];
							if (parameter.Type != InstructionParameterTypeEnum.Relative)
								throw new Exception(
									$"Relative adress offset expected, but got indirect {parameter.ToString()} in function {this.sName} - instruction at 0x{this.usSegment:x4}:0x{instruction.Offset:x4}");

							aJumps.Add(AddRelativeToOffset(instruction, parameter));
							break;

						case InstructionEnum.INT:
							if (instruction.Parameters[0].Type == InstructionParameterTypeEnum.Immediate &&
								instruction.Parameters[0].Value == 0x20)
							{
								// exit application instruction
								bEnd = true;
							}
							else if (instruction.Parameters[0].Type == InstructionParameterTypeEnum.Immediate &&
								instruction.Parameters[0].Value == 0x3f)
							{
								// overlay manager
								instruction.InstructionType = InstructionEnum.CallOverlay;
								int byte0 = stream.ReadByte();
								int byte1 = stream.ReadByte();
								int byte2 = stream.ReadByte();
								if (byte0 < 0 || byte1 < 0 || byte2 < 0)
									throw new Exception("Int 0x3F missing parameters");

								instruction.Bytes.Add((byte)(byte0 & 0xff));
								instruction.Bytes.Add((byte)(byte1 & 0xff));
								instruction.Bytes.Add((byte)(byte2 & 0xff));

								instruction.Parameters.Clear();
								instruction.Parameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Immediate, InstructionSizeEnum.Byte, (byte)(byte0 & 0xff)));
								instruction.Parameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Immediate, InstructionSizeEnum.Word, (ushort)((byte1 & 0xff) | ((byte2 & 0xff) << 8))));
								ip += 3;

							}
							else if (aInstructions.Count > 1)
							{
								instruction1 = aInstructions[aInstructions.Count - 2];
								if (instruction1.Parameters.Count > 1 &&
									instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Register &&
									instruction1.Parameters[0].Size == InstructionSizeEnum.Byte &&
									instruction1.Parameters[0].Value == (uint)RegisterEnum.AH &&
									instruction1.Parameters[1].Type == InstructionParameterTypeEnum.Immediate &&
									instruction1.Parameters[1].Value == 0x4c)
								{
									// exit application instruction
									bEnd = true;
								}
								else if (instruction1.Parameters.Count > 1 &&
									instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Register &&
									instruction1.Parameters[0].Size == InstructionSizeEnum.Word &&
									instruction1.Parameters[0].Value == (uint)(((uint)RegisterEnum.AX) & 0x7) &&
									instruction1.Parameters[1].Type == InstructionParameterTypeEnum.Immediate &&
									(instruction1.Parameters[1].Value & 0xff00) == 0x4c00)
								{
									// exit application instruction
									bEnd = true;
								}
							}
							break;

						case InstructionEnum.IRET:
							//Console.WriteLine($"Unexpected return from interrupt at function {this.sName} at 0x{this.usSegment:x4}:0x{instruction.Offset:x4}");
							bEnd = true;
							break;

						case InstructionEnum.RET:
							// convert near return to far return
							instruction.InstructionType = InstructionEnum.RETF;
							if (instruction.Parameters.Count == 1 && this.eCallType != CallTypeEnum.Undefined && this.eCallType != CallTypeEnum.Pascal)
								throw new Exception("Inconsistent function call type");

							if (instruction.Parameters.Count == 0 && this.eCallType != CallTypeEnum.Undefined && this.eCallType != CallTypeEnum.Cdecl)
								throw new Exception("Inconsistent function call type");

							if (instruction.Parameters.Count == 1)
							{
								this.eCallType = CallTypeEnum.Pascal;
								this.iParameterSize = (int)instruction.Parameters[0].Value;
							}
							else
							{
								this.eCallType = CallTypeEnum.Cdecl;
							}
							bEnd = true;
							break;

						case InstructionEnum.RETF:
							if (instruction.Parameters.Count == 1 && this.eCallType != CallTypeEnum.Undefined && this.eCallType != CallTypeEnum.Pascal)
								Console.WriteLine($"Inconsistent function call type in {this.sName}");

							if (instruction.Parameters.Count == 0 && this.eCallType != CallTypeEnum.Undefined && this.eCallType != CallTypeEnum.Cdecl)
								Console.WriteLine($"Inconsistent function call type in {this.sName}");

							if (instruction.Parameters.Count == 1)
							{
								this.eCallType = CallTypeEnum.Pascal;
								this.iParameterSize = (int)instruction.Parameters[0].Value;
							}
							else
							{
								this.eCallType = CallTypeEnum.Cdecl;
							}
							bEnd = true;
							break;
					}
				}

				if (bEnd)
				{
					// jumps
					if (aJumps.Count > 0)
					{
						ip = aJumps[aJumps.Count - 1];
						aJumps.RemoveAt(aJumps.Count - 1);
						if (this.iOverlay > 0)
						{
							stream.Seek(ip, SeekOrigin.Begin);
						}
						else
						{
							stream.Seek(MemoryRegion.ToLinearAddress(this.usSegment, ip) - this.uiStreamOffset, SeekOrigin.Begin);
						}

						continue;
					}

					// switches
					if (aSwitches.Count > 0)
					{
						// sort instructions by address before doing switches
						this.aInstructions.Sort(Instruction.CompareInstructionByAddress);

						ip = aSwitches[aSwitches.Count - 1];
						aSwitches.RemoveAt(aSwitches.Count - 1);

						int iPos = GetPositionFromAddress(ip);
						if (iPos >= 0)
						{
							if (iPos > 5)
							{
								int iPos1;
								InstructionParameter parameter;
								uint uiCount = 0;
								ushort usSwitchOffset = 0;

								// first pattern
								if ((instruction1 = this.aInstructions[iPos1 = iPos - 5]).InstructionType == InstructionEnum.CMP &&
									instruction1.Parameters.Count == 2 &&
									instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Register &&
									(parameter=instruction1.Parameters[0]).Value == ((uint)RegisterEnum.AX & 0x7) &&
									instruction1.Parameters[1].Type == InstructionParameterTypeEnum.Immediate &&
									(uiCount= instruction1.Parameters[1].Value)>=0 &&

									(instruction1 = this.aInstructions[++iPos1]).InstructionType == InstructionEnum.Jcc &&
									instruction1.Parameters.Count == 2 &&
									instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Condition &&
									instruction1.Parameters[0].Value == (uint)ConditionEnum.BE &&
									instruction1.Parameters[1].Type == InstructionParameterTypeEnum.Relative &&

									(instruction1 = this.aInstructions[++iPos1]).InstructionType == InstructionEnum.JMP &&
									instruction1.Parameters.Count == 1 &&
									instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Relative &&

									(instruction1 = this.aInstructions[++iPos1]).InstructionType == InstructionEnum.ADD &&
									instruction1.Parameters.Count == 2 &&
									instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Register &&
									(parameter = instruction1.Parameters[0]).Value == ((uint)RegisterEnum.AX & 0x7) &&
									instruction1.Parameters[1].Type == InstructionParameterTypeEnum.Register &&
									instruction1.Parameters[1].Value == instruction1.Parameters[0].Value &&

									(instruction1 = this.aInstructions[++iPos1]).InstructionType == InstructionEnum.XCHG &&
									instruction1.Parameters.Count == 2 &&
									instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Register &&
									instruction1.Parameters[0].Value == ((uint)RegisterEnum.AX & 0x7) &&
									instruction1.Parameters[1].Type == InstructionParameterTypeEnum.Register &&
									instruction1.Parameters[1].Value == ((uint)RegisterEnum.BX & 0x7) &&

									(instruction1 = this.aInstructions[++iPos1]).InstructionType == InstructionEnum.JMP &&
									instruction1.Parameters.Count == 1 &&
									instruction1.Parameters[0].Type == InstructionParameterTypeEnum.MemoryAddress &&
									(usSwitchOffset = (ushort)instruction1.Parameters[0].Displacement) >=0 &&

									iPos == iPos1
									)
								{
									//Console.WriteLine("Switch type 1 at {0}:0x{1:x4}", this.uiSegment, this.aInstructions[iPos].Location.Offset);

									this.aInstructions[iPos - 2].InstructionType = InstructionEnum.NOP;
									this.aInstructions[iPos - 1].InstructionType = InstructionEnum.NOP;
									instruction1 = this.aInstructions[iPos];
									instruction1.InstructionType = InstructionEnum.SWITCH;
									instruction1.Parameters.Clear();

									// switching parameter
									instruction1.Parameters.Add(parameter);

									if (this.iOverlay > 0)
									{
										stream.Seek(usSwitchOffset, SeekOrigin.Begin);
									}
									else
									{
										stream.Seek(MemoryRegion.ToLinearAddress(this.usSegment, usSwitchOffset) - this.uiStreamOffset, SeekOrigin.Begin);
									}

									// values and offsets
									for (int i = 0; i <= uiCount; i++)
									{
										ushort usWord = ReadWord(stream);
										aJumps.Add(usWord);
										parameter = new InstructionParameter(InstructionParameterTypeEnum.Immediate, (uint)i);
										parameter.Displacement = usWord;
										instruction1.Parameters.Add(parameter);
									}

									ip = aJumps[aJumps.Count - 1];
									aJumps.RemoveAt(aJumps.Count - 1);

									if (this.iOverlay > 0)
									{
										stream.Seek(ip, SeekOrigin.Begin);
									}
									else
									{
										stream.Seek(MemoryRegion.ToLinearAddress(this.usSegment, ip) - this.uiStreamOffset, SeekOrigin.Begin);
									}
								}
								else if ((instruction1 = this.aInstructions[iPos1 = iPos - 5]).InstructionType == InstructionEnum.SUB &&
									instruction1.Parameters.Count == 2 &&
									instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Register &&
									instruction1.Parameters[0].Value == ((uint)RegisterEnum.AX & 0x7) &&
									instruction1.Parameters[1].Type == InstructionParameterTypeEnum.Immediate &&

									(instruction1 = this.aInstructions[++iPos1]).InstructionType == InstructionEnum.CMP &&
									instruction1.Parameters.Count == 2 &&
									instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Register &&
									(parameter = instruction1.Parameters[0]).Value == ((uint)RegisterEnum.AX & 0x7) &&
									instruction1.Parameters[1].Type == InstructionParameterTypeEnum.Immediate &&
									(uiCount = instruction1.Parameters[1].Value) >= 0 &&

									(instruction1 = this.aInstructions[++iPos1]).InstructionType == InstructionEnum.Jcc &&
									instruction1.Parameters.Count == 2 &&
									instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Condition &&
									instruction1.Parameters[0].Value == (uint)ConditionEnum.A &&
									instruction1.Parameters[1].Type == InstructionParameterTypeEnum.Relative &&

									(instruction1 = this.aInstructions[++iPos1]).InstructionType == InstructionEnum.ADD &&
									instruction1.Parameters.Count == 2 &&
									instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Register &&
									(parameter = instruction1.Parameters[0]).Value == ((uint)RegisterEnum.AX & 0x7) &&
									instruction1.Parameters[1].Type == InstructionParameterTypeEnum.Register &&
									instruction1.Parameters[1].Value == instruction1.Parameters[0].Value &&

									(instruction1 = this.aInstructions[++iPos1]).InstructionType == InstructionEnum.XCHG &&
									instruction1.Parameters.Count == 2 &&
									instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Register &&
									instruction1.Parameters[0].Value == ((uint)RegisterEnum.AX & 0x7) &&
									instruction1.Parameters[1].Type == InstructionParameterTypeEnum.Register &&
									instruction1.Parameters[1].Value == ((uint)RegisterEnum.BX & 0x7) &&

									(instruction1 = this.aInstructions[++iPos1]).InstructionType == InstructionEnum.JMP &&
									instruction1.Parameters.Count == 1 &&
									instruction1.Parameters[0].Type == InstructionParameterTypeEnum.MemoryAddress &&
									(usSwitchOffset = (ushort)instruction1.Parameters[0].Displacement) >= 0 &&

									iPos == iPos1
									)
								{
									//Console.WriteLine("Switch type 1 at {0}:0x{1:x4}", this.uiSegment, this.aInstructions[iPos].Location.Offset);

									this.aInstructions[iPos - 2].InstructionType = InstructionEnum.NOP;
									this.aInstructions[iPos - 1].InstructionType = InstructionEnum.NOP;
									instruction1 = this.aInstructions[iPos];
									instruction1.InstructionType = InstructionEnum.SWITCH;
									instruction1.Parameters.Clear();

									// switching parameter
									instruction1.Parameters.Add(parameter);

									if (this.iOverlay > 0)
									{
										stream.Seek(usSwitchOffset, SeekOrigin.Begin);
									}
									else
									{
										stream.Seek(MemoryRegion.ToLinearAddress(this.usSegment, usSwitchOffset) - this.uiStreamOffset, SeekOrigin.Begin);
									}

									// values and offsets
									for (int i = 0; i <= uiCount; i++)
									{
										ushort usWord = ReadWord(stream);
										aJumps.Add(usWord);
										parameter = new InstructionParameter(InstructionParameterTypeEnum.Immediate, (uint)i);
										parameter.Displacement = usWord;
										instruction1.Parameters.Add(parameter);
									}

									ip = aJumps[aJumps.Count - 1];
									aJumps.RemoveAt(aJumps.Count - 1);

									if (this.iOverlay > 0)
									{
										stream.Seek(ip, SeekOrigin.Begin);
									}
									else
									{
										stream.Seek(MemoryRegion.ToLinearAddress(this.usSegment, ip) - this.uiStreamOffset, SeekOrigin.Begin);
									}
								}
								else
								{
									Instruction instruction = this.aInstructions[iPos];
									Console.WriteLine($"Undefined switch pattern to {instruction.Parameters[0].ToString()} at function '{this.sName}' - jmp at 0x{instruction.Segment:x4}:0x{instruction.Offset:x4}");
									//break;
								}
							}
						}
						else
						{
							Console.WriteLine($"Can't find location of switch statement at function '{this.sName}' - jmp at 0x{this.usSegment:x4}:0x{ip:x4}");
						}
						continue;
					}

					// no more jumps or switches, we are done
					break;
				}
			}

			this.aInstructions.Sort(Instruction.CompareInstructionByAddress);

			int iStart = 0;
			int iEnd = this.aInstructions.Count - 1;

			// if function entry differs from zero position
			int iPosition = GetPositionFromAddress(this.usOffset);
			if (iPosition > 0)
			{
				Instruction instruction; // = this.aInstructions[0];
				//ip = (ushort)((uint)this.aInstructions[iPosition].Offset - (uint)(instruction.Offset));
				instruction = new Instruction(this.usSegment, 0xffff, InstructionEnum.JMP, InstructionSizeEnum.Word);
				instruction.Parameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Relative, InstructionSizeEnum.Word, (ushort)(this.usOffset + 1)));
				this.aInstructions.Insert(0, instruction);
			}

			#region assign segment references from relocations table
			for (int i = 0; i < this.aInstructions.Count; i++)
			{
				Instruction instruction = this.aInstructions[i];

				for (int j = 0; j < this.oParent.Executable.Relocations.Count; j++)
				{
					MZRelocationItem relocation = this.oParent.Executable.Relocations[j];
					uint uiRelocationAddress = MemoryRegion.ToLinearAddress(relocation.Segment, relocation.Offset);

					if (uiRelocationAddress >= instruction.LinearAddress && uiRelocationAddress < instruction.LinearAddress + instruction.Bytes.Count)
					{
						for (int k = 0; k < instruction.Parameters.Count; k++)
						{
							if (instruction.Parameters[k].Type == InstructionParameterTypeEnum.Immediate &&
								instruction.Parameters[k].Size == InstructionSizeEnum.Word)
							{
								instruction.Parameters[k].ReferenceType = InstructionParameterReferenceEnum.Segment;
								break;
							}
						}
						break;
					}
				}
			}
			#endregion

			#region Optimize GoTo's
			/*for (int i = 0; i < this.aInstructions.Count; i++)
			{
				Instruction instruction = this.aInstructions[i];
				InstructionParameter parameter;
				ushort usNewAddress;
				ushort usNewAddress2;

				switch (instruction.InstructionType)
				{
					case InstructionEnum.LOOP:
					case InstructionEnum.LOOPZ:
					case InstructionEnum.LOOPNZ:
					case InstructionEnum.JCXZ:
					case InstructionEnum.JMP:
						parameter = instruction.Parameters[0];
						usNewAddress = AddRelativeToOffset(instruction, parameter);

						// optimize immediate jumps
						usNewAddress2 = usNewAddress;
						while ((instruction1 = this.aInstructions[GetPositionFromAddress(usNewAddress2)]).InstructionType == InstructionEnum.JMP)
						{
							usNewAddress2 = AddRelativeToOffset(instruction1, instruction1.Parameters[0]);
						}
						if (usNewAddress != usNewAddress2)
						{
							parameter.Value = (ushort)((uint)((int)usNewAddress2 - (int)((int)instruction.Offset + instruction.Bytes.Count)) & 0xffff);
						}
						break;
					case InstructionEnum.Jcc:
						parameter = instruction.Parameters[1];
						usNewAddress = AddRelativeToOffset(instruction, parameter);

						// optimize immediate jumps
						usNewAddress2 = usNewAddress;
						while ((instruction1 = this.aInstructions[GetPositionFromAddress(usNewAddress2)]).InstructionType == InstructionEnum.JMP)
						{
							usNewAddress2 = AddRelativeToOffset(instruction1, instruction1.Parameters[0]);
						}
						if (usNewAddress != usNewAddress2)
						{
							parameter.Value = (ushort)((uint)((int)usNewAddress2 - (int)((int)instruction.Offset + instruction.Bytes.Count)) & 0xffff);
						}
						break;
				}
			}*/
			#endregion

			#region assign labels to instructions, convert relative call to far call
			for (int i = 0; i < this.aInstructions.Count; i++)
			{
				Instruction instruction = this.aInstructions[i];
				InstructionParameter parameter;
				ushort usNewOffset;

				switch (instruction.InstructionType)
				{
					case InstructionEnum.CALL:
						if (this.iOverlay == 0)
						{
							parameter = instruction.Parameters[0];
							if (parameter.Type == InstructionParameterTypeEnum.Relative)
							{
								usNewOffset = AddRelativeToOffset(instruction, parameter);

								instruction.Parameters[0] = new InstructionParameter(InstructionParameterTypeEnum.Immediate, InstructionSizeEnum.Word, usNewOffset);
							}

							/*parameter = instruction.Parameters[0];
							if (parameter.Type == InstructionParameterTypeEnum.Relative)
							{
								usNewOffset = AddRelativeToOffset(instruction, parameter);

								instruction.InstructionType = InstructionEnum.CALLF;
								instruction.Parameters[0] = new InstructionParameter(this.usSegment, usNewOffset);

								if (i > 0)
								{
									// is instruction prefixed by PUSH CS?
									instruction1 = this.aInstructions[i - 1];

									if (instruction1.InstructionType == InstructionEnum.PUSH &&
										instruction1.Parameters.Count == 1 &&
										instruction1.Parameters[0].Type == InstructionParameterTypeEnum.SegmentRegister &&
										instruction1.Parameters[0].Value == (uint)SegmentRegisterEnum.CS)
									{
										// turn push cs instruction to nop, as it is not needed anymore
										instruction1.InstructionType = InstructionEnum.NOP;
									}
								}
							}*/
						}
						else
						{
							// if we are calling inside of overlay translate this to call overlay function
							instruction.InstructionType = InstructionEnum.CallOverlay;
							parameter = instruction.Parameters[0];
							usNewOffset = AddRelativeToOffset(instruction, parameter);
							instruction.Parameters.Clear();
							instruction.Parameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Immediate, InstructionSizeEnum.Byte, (uint)this.iOverlay));
							instruction.Parameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Immediate, InstructionSizeEnum.Word, usNewOffset));
						}
						break;

					case InstructionEnum.JMP:
						parameter = instruction.Parameters[0];
						if (parameter.Type == InstructionParameterTypeEnum.Relative)
						{
							usNewOffset = AddRelativeToOffset(instruction, parameter);

							// optimize immediate jumps
							if (i + 1 < this.aInstructions.Count && usNewOffset > 0 &&
								this.aInstructions[i + 1].Offset == usNewOffset)
							{
								// this is just a jump to next instruction, ignore it
								this.aInstructions[i].InstructionType = InstructionEnum.NOP;
							}
							else if (usNewOffset > 0)
							{
								this.aInstructions[GetPositionFromAddress(usNewOffset)].Label = true;
							}
						}
						break;

					case InstructionEnum.Jcc:
						usNewOffset = AddRelativeToOffset(instruction, instruction.Parameters[1]);
						if (usNewOffset > 0)
						{
							this.aInstructions[GetPositionFromAddress(usNewOffset)].Label = true;
						}
						break;

					case InstructionEnum.LOOP:
					case InstructionEnum.LOOPZ:
					case InstructionEnum.LOOPNZ:
					case InstructionEnum.JCXZ:
						usNewOffset = AddRelativeToOffset(instruction, instruction.Parameters[0]);
						if (usNewOffset > 0)
						{
							this.aInstructions[GetPositionFromAddress(usNewOffset)].Label = true;
						}
						break;
					case InstructionEnum.SWITCH:
						for (int j = 1; j < instruction.Parameters.Count; j++)
						{
							parameter = instruction.Parameters[j];

							this.aInstructions[GetPositionFromAddress((ushort)parameter.Displacement)].Label = true;
						}
						break;
				}
			}
			#endregion

			#region Optimize Jcc's
			/*for (int i = 0; i < this.aInstructions.Count; i++)
			{
				ushort usNewAddress;
				ushort usNewAddress2;

				if (i + 2 < this.aInstructions.Count &&
					(instruction1 = this.aInstructions[i]).InstructionType == InstructionEnum.Jcc &&
					instruction1.Parameters.Count == 2 &&
					(usNewAddress = AddRelativeToOffset(instruction1, instruction1.Parameters[1])) >= 0 &&

					(instruction1 = this.aInstructions[i + 1]).InstructionType == InstructionEnum.JMP &&
					instruction1.Parameters.Count == 1 &&
					(usNewAddress2 = AddRelativeToOffset(instruction1, instruction1.Parameters[0])) >= 0 &&

					(instruction1 = this.aInstructions[i + 2]).Offset == usNewAddress)
				{
					Instruction instruction = this.aInstructions[i];

					// goto can't be referenced to in this combination
					if (this.aInstructions[i + 1].Label)
						throw new Exception($"Unexpected label at function {this.sName}, position 0x{this.usSegment:x4}:0x{this.aInstructions[i + 1].Offset:x4}");

					this.aInstructions[i + 1].InstructionType = InstructionEnum.NOP;

					InstructionParameter parameter = instruction.Parameters[0];
					parameter.Value = (uint)NegateCondition((ConditionEnum)parameter.Value);
					instruction.Parameters[1].Value = (uint)((int)usNewAddress2 - ((int)instruction.Offset + instruction.Bytes.Count)) & 0xffff;
				}
			}*/
			#endregion

			#region Reassign labels
			/*for (int i = 0; i < this.aInstructions.Count; i++)
			{
				this.aInstructions[i].Label = false;
			}

			for (int i = 0; i < this.aInstructions.Count; i++)
			{
				Instruction instruction = this.aInstructions[i];
				InstructionParameter parameter;
				ushort usNewOffset;

				switch (instruction.InstructionType)
				{
					case InstructionEnum.JMP:
						parameter = instruction.Parameters[0];
						usNewOffset = AddRelativeToOffset(instruction, parameter);

						if (usNewOffset > 0)
						{
							this.aInstructions[GetPositionFromAddress(usNewOffset)].Label = true;
						}
						break;
					case InstructionEnum.Jcc:
						parameter = instruction.Parameters[1];
						usNewOffset = AddRelativeToOffset(instruction, parameter);

						if (usNewOffset > 0)
						{
							int iPosition1 = GetPositionFromAddress(usNewOffset);
							if (iPosition1 >= 0)
								this.aInstructions[iPosition1].Label = true;
						}
						break;
					case InstructionEnum.LOOP:
					case InstructionEnum.LOOPZ:
					case InstructionEnum.LOOPNZ:
					case InstructionEnum.JCXZ:
						parameter = instruction.Parameters[0];
						usNewOffset = AddRelativeToOffset(instruction, parameter);

						if (usNewOffset > 0)
						{
							this.aInstructions[GetPositionFromAddress(usNewOffset)].Label = true;
						}
						break;
					case InstructionEnum.SWITCH:
						for (int j = 1; j < instruction.Parameters.Count; j++)
						{
							parameter = instruction.Parameters[j];

							this.aInstructions[GetPositionFromOffset(parameter.Displacement)].Label = true;
						}
						break;
				}
			}*/
			#endregion

			#region Optimize two GoTo's
			/*for (int i = 0; i < this.aInstructions.Count; i++)
			{
				if (i + 1 < this.aInstructions.Count &&
					(instruction1 = this.aInstructions[i]).InstructionType == InstructionEnum.JMP &&

					(instruction1 = this.aInstructions[i + 1]).InstructionType == InstructionEnum.JMP &&
					!instruction1.Label)
				{
					instruction1.InstructionType = InstructionEnum.NOP;
				}
			}*/
			#endregion

			#region Optimize XOR, (PUSH word, PUSH word, POP dword) and (LEA, PUSH)
			for (int i = 0; i < this.aInstructions.Count; i++)
			{
				Instruction instruction = this.aInstructions[i];
				uint sourceReg1 = 0;
				uint sourceReg2 = 0;
				uint destinationReg = 0;

				// all xors with same source and destination are 0
				if (i + 1 < this.aInstructions.Count &&
					(instruction1 = this.aInstructions[i]).InstructionType == InstructionEnum.XOR &&
					instruction1.Parameters.Count == 2 &&
					instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Register &&
					instruction1.Parameters[1].Type == InstructionParameterTypeEnum.Register &&
					instruction1.Parameters[0].Size == instruction1.Parameters[1].Size &&
					instruction1.Parameters[0].Value == instruction1.Parameters[1].Value &&

					(instruction1 = this.aInstructions[i + 1]).InstructionType != InstructionEnum.Jcc)
				{
					instruction1 = this.aInstructions[i];
					instruction1.InstructionType = InstructionEnum.MOV;
					instruction1.Parameters[1] = new InstructionParameter(InstructionParameterTypeEnum.Immediate, 0);
				}

				// optimize convert two words to dword
				if (i + 2 < this.aInstructions.Count &&
					(instruction1 = this.aInstructions[i]).InstructionType == InstructionEnum.PUSH &&
					instruction1.OperandSize == InstructionSizeEnum.Word &&
					instruction1.Parameters.Count == 1 &&
					instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Register &&
					(sourceReg1 = instruction1.Parameters[0].Value) <= 7 &&

					(instruction1 = this.aInstructions[i + 1]).InstructionType == InstructionEnum.PUSH &&
					instruction1.OperandSize == InstructionSizeEnum.Word &&
					instruction1.Parameters.Count == 1 &&
					instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Register &&
					(sourceReg2 = instruction1.Parameters[0].Value) <= 7 &&

					(instruction1 = this.aInstructions[i + 2]).InstructionType == InstructionEnum.POP &&
					instruction1.OperandSize == InstructionSizeEnum.DWord &&
					instruction1.Parameters.Count == 1 &&
					instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Register &&
					(destinationReg = instruction1.Parameters[0].Value) <= 7)
				{
					instruction1 = this.aInstructions[i];
					instruction1.InstructionType = InstructionEnum.WordsToDword;
					instruction1.OperandSize = InstructionSizeEnum.DWord;
					instruction1.Parameters.Clear();
					instruction1.Parameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register,
						InstructionSizeEnum.DWord, destinationReg));
					instruction1.Parameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register,
						InstructionSizeEnum.Word, sourceReg1));
					instruction1.Parameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register,
						InstructionSizeEnum.Word, sourceReg2));

					this.aInstructions[i + 1].InstructionType = InstructionEnum.NOP;
					this.aInstructions[i + 2].InstructionType = InstructionEnum.NOP;
				}
				if (i + 1 < this.aInstructions.Count &&
					(instruction1 = this.aInstructions[i]).InstructionType == InstructionEnum.LEA &&
					instruction1.OperandSize == InstructionSizeEnum.Word &&
					instruction1.Parameters.Count == 2 &&
					instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Register &&
					(sourceReg1 = instruction1.Parameters[0].Value) <= 7 &&

					(instruction1 = this.aInstructions[i + 1]).InstructionType == InstructionEnum.PUSH &&
					instruction1.OperandSize == InstructionSizeEnum.Word &&
					instruction1.Parameters.Count == 1 &&
					instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Register &&
					instruction1.Parameters[0].Value == sourceReg1)
				{
					this.aInstructions[i].InstructionType = InstructionEnum.NOP;
					InstructionParameter parameter = this.aInstructions[i].Parameters[1];
					instruction1.Parameters[0] = new InstructionParameter(InstructionParameterTypeEnum.LEAMemoryAddress,
						parameter.Size, parameter.DataSegment, parameter.Value, parameter.Displacement);
				}
			}
			#endregion

			#region Translate Jcc's to If's
			/*for (int i = 0; i < this.aInstructions.Count; i++)
			{
				ushort usNewOffset;

				if (i + 1 < this.aInstructions.Count &&
					(instruction1 = this.aInstructions[i]).InstructionType == InstructionEnum.CMP &&
					instruction1.Parameters.Count == 2 &&

					(instruction1 = this.aInstructions[i + 1]).InstructionType == InstructionEnum.Jcc &&
					instruction1.Parameters.Count == 2 &&
					(usNewOffset = AddRelativeToOffset(instruction1, instruction1.Parameters[1])) >= 0)
				{
					Instruction instruction = this.aInstructions[i];

					// jcc can't be referenced to in this combination
					if (instruction1.Label)
						throw new Exception($"Unexpected label at function {this.sName}, position 0x{this.usSegment:x4}:0x{instruction1.Offset:x4}");

					instruction.InstructionType = InstructionEnum.If;
					instruction.Parameters.AddRange(instruction1.Parameters);

					// correct jump offset
					instruction.Parameters[3].Value += (uint)instruction1.Bytes.Count;
					instruction1.InstructionType = InstructionEnum.NOP;

					int iPos = i + 2;
					while (iPos < this.aInstructions.Count &&
						((instruction1 = this.aInstructions[iPos]).InstructionType == InstructionEnum.Jcc ||
						instruction1.InstructionType == InstructionEnum.NOP))
					{
						if (instruction1.InstructionType != InstructionEnum.NOP)
						{
							// jcc can't be referenced to in this combination
							if (instruction1.Label)
								throw new Exception(string.Format("Unexpected label at function {0, position 0x{1:x8}",
									this.sName, instruction1.LinearAddress));

							instruction1.Parameters.Insert(0, instruction.Parameters[0]);
							instruction1.Parameters.Insert(1, instruction.Parameters[1]);
							instruction1.InstructionType = InstructionEnum.If;
						}
						iPos++;
					}
				}
				else if (i + 1 < this.aInstructions.Count &&
					(instruction1 = this.aInstructions[i]).InstructionType == InstructionEnum.TEST &&
					instruction1.Parameters.Count == 2 &&

					(instruction1 = this.aInstructions[i + 1]).InstructionType == InstructionEnum.Jcc &&
					instruction1.Parameters.Count == 2 &&
					(usNewOffset = AddRelativeToOffset(instruction1, instruction1.Parameters[1])) >= 0)
				{
					Instruction instruction = this.aInstructions[i];

					// jcc can't be referenced to in this combination
					if (instruction1.Label)
						throw new Exception(string.Format("Unexpected label at function {0}, position 0x{1:x8}",
							this.sName, instruction1.LinearAddress));

					instruction.InstructionType = InstructionEnum.IfAnd;
					instruction.Parameters.AddRange(instruction1.Parameters);

					// correct jump offset
					instruction.Parameters[3].Value += (uint)instruction1.Bytes.Count;
					instruction1.InstructionType = InstructionEnum.NOP;

					int iPos = i + 2;
					while (iPos < this.aInstructions.Count &&
						(instruction1 = this.aInstructions[iPos]).InstructionType == InstructionEnum.Jcc)
					{
						// jcc can't be referenced to in this combination
						if (instruction1.Label)
							throw new Exception(string.Format("Unexpected label at function {0}, position 0x{1:x8}",
								this.sName, instruction1.LinearAddress));

						instruction1.Parameters.Insert(0, instruction.Parameters[0]);
						instruction1.Parameters.Insert(1, instruction.Parameters[1]);
						instruction1.InstructionType = InstructionEnum.IfAnd;
						iPos++;
					}
				}
				else if (i + 1 < this.aInstructions.Count &&
					(instruction1 = this.aInstructions[i]).InstructionType == InstructionEnum.OR &&
					instruction1.Parameters.Count == 2 &&
					instruction1.Parameters[0].Type == instruction1.Parameters[1].Type &&
					instruction1.Parameters[0].Value == instruction1.Parameters[1].Value &&
					instruction1.Parameters[0].DataSegment == instruction1.Parameters[1].DataSegment &&

					(instruction1 = this.aInstructions[i + 1]).InstructionType == InstructionEnum.Jcc &&
					instruction1.Parameters.Count == 2 &&
					(usNewOffset = AddRelativeToOffset(instruction1, instruction1.Parameters[1])) >= 0)
				{
					Instruction instruction = this.aInstructions[i];

					// jcc can't be referenced to in this combination
					if (instruction1.Label)
						throw new Exception(string.Format("Unexpected label at function {0}, position 0x{1:x8}",
							this.sName, instruction1.LinearAddress));

					instruction.InstructionType = InstructionEnum.IfOr;
					instruction.Parameters.AddRange(instruction1.Parameters);

					// correct jump offset
					instruction.Parameters[3].Value += (uint)instruction1.Bytes.Count;
					instruction1.InstructionType = InstructionEnum.NOP;

					int iPos = i + 2;
					while (iPos < this.aInstructions.Count &&
						(instruction1 = this.aInstructions[iPos]).InstructionType == InstructionEnum.Jcc)
					{
						// jcc can't be referenced to in this combination
						if (instruction1.Label)
							throw new Exception(string.Format("Unexpected label in function {0}, position 0x{1:x8}",
								this.sName, instruction1.LinearAddress));

						instruction1.Parameters.Insert(0, instruction.Parameters[0]);
						instruction1.Parameters.Insert(1, instruction.Parameters[1]);
						instruction1.InstructionType = InstructionEnum.IfOr;
						iPos++;
					}
				}
			}*/
			#endregion

			#region process calls and assign parameters
			for (int i = 0; i < this.aInstructions.Count; i++)
			{
				Instruction instruction = this.aInstructions[i];
				InstructionParameter parameter;
				MZFunction? function;

				switch (instruction.InstructionType)
				{
					case InstructionEnum.CALL:
						parameter = instruction.Parameters[0];
						if (parameter.Type == InstructionParameterTypeEnum.Immediate)
						{
							function = decompiler.GetFunction(0, this.usSegment, (ushort)parameter.Value, this.uiStreamOffset);
							if (function == null)
							{
								// function is not yet defined, define it
								decompiler.Decompile($"F0_{this.usSegment:x4}_{parameter.Value:x4}",
									CallTypeEnum.Undefined, new List<CParameter>(), CType.Void,
									0, this.usSegment, (ushort)parameter.Value, this.uiStreamOffset);
								//function = decompiler.GetFunction(parameter.Segment, (ushort)parameter.Value, this.uiStreamOffset);
							}
						}
						break;

					case InstructionEnum.CALLF:
						parameter = instruction.Parameters[0];
						if (parameter.Type == InstructionParameterTypeEnum.SegmentOffset)
						{
							function = decompiler.GetFunction(0, parameter.Segment, (ushort)parameter.Value, this.uiStreamOffset);
							if (function == null)
							{
								// function is not yet defined, define it
								decompiler.Decompile($"F0_{parameter.Segment:x4}_{parameter.Value:x4}",
									CallTypeEnum.Undefined, new List<CParameter>(), CType.Void, 
									0, parameter.Segment, (ushort)parameter.Value, this.uiStreamOffset);
								//function = decompiler.GetFunction(parameter.Segment, (ushort)parameter.Value, this.uiStreamOffset);
							}
						}
						break;

					case InstructionEnum.CallOverlay:
						if (instruction.Parameters[0].Value == 0)
						{
							throw new Exception("Overlay manager references overlay 0");
						}
						function = decompiler.GetFunction((int)instruction.Parameters[0].Value, 0, (ushort)instruction.Parameters[1].Value, this.uiStreamOffset);
						if (function == null)
						{
							// function is not yet defined, define it
							decompiler.Decompile($"F{instruction.Parameters[0].Value}_0000_{instruction.Parameters[1].Value:x4}",
								CallTypeEnum.Undefined, new List<CParameter>(), CType.Void,
								(int)instruction.Parameters[0].Value, 0, (ushort)instruction.Parameters[1].Value, this.uiStreamOffset);
							//function = decompiler.GetFunction(parameter.Segment, (ushort)parameter.Value, this.uiStreamOffset);
						}
						break;

					case InstructionEnum.JMPF:
						parameter = instruction.Parameters[0];
						if (parameter.Type == InstructionParameterTypeEnum.SegmentOffset)
						{
							function = decompiler.GetFunction(0, parameter.Segment, (ushort)parameter.Value, this.uiStreamOffset);
							if (function == null)
							{
								// function is not yet defined, define it
								decompiler.Decompile(string.Format("F0_{0:x4}_{1:x4}", parameter.Segment, parameter.Value),
									CallTypeEnum.Undefined, new List<CParameter>(), CType.Void, 
									0, parameter.Segment, (ushort)parameter.Value, this.uiStreamOffset);
								//function = decompiler.GetFunction(parameter.Segment, (ushort)parameter.Value, this.uiStreamOffset);
							}
						}
						break;
				}
			}
			#endregion
		}

		public static ushort AddRelativeToOffset(Instruction instruction, InstructionParameter parameter)
		{
			ushort result = (ushort)(instruction.Offset + instruction.Bytes.Count);

			switch (parameter.Size)
			{
				case InstructionSizeEnum.Byte:
					result = (ushort)((uint)((int)(instruction.Offset + instruction.Bytes.Count) + (sbyte)(parameter.Value & 0xff)) & 0xffff);
					break;
				case InstructionSizeEnum.Word:
					result = (ushort)((uint)((int)(instruction.Offset + instruction.Bytes.Count) + (short)(parameter.Value & 0xffff)) & 0xffff);
					break;
				case InstructionSizeEnum.DWord:
					throw new Exception("Can't add DWord to Word Offset");
			}

			return result;
		}

		private ConditionEnum NegateCondition(ConditionEnum condition)
		{
			switch (condition)
			{
				case ConditionEnum.O:
					return ConditionEnum.NO;
				case ConditionEnum.NO:
					return ConditionEnum.O;
				case ConditionEnum.B:
					return ConditionEnum.AE;
				case ConditionEnum.AE:
					return ConditionEnum.B;
				case ConditionEnum.E:
					return ConditionEnum.NE;
				case ConditionEnum.NE:
					return ConditionEnum.E;
				case ConditionEnum.BE:
					return ConditionEnum.A;
				case ConditionEnum.A:
					return ConditionEnum.BE;
				case ConditionEnum.S:
					return ConditionEnum.NS;
				case ConditionEnum.NS:
					return ConditionEnum.S;
				case ConditionEnum.P:
					return ConditionEnum.NP;
				case ConditionEnum.NP:
					return ConditionEnum.P;
				case ConditionEnum.L:
					return ConditionEnum.GE;
				case ConditionEnum.GE:
					return ConditionEnum.L;
				case ConditionEnum.LE:
					return ConditionEnum.G;
				case ConditionEnum.G:
					return ConditionEnum.LE;
			}

			return ConditionEnum.Undefined;
		}

		private int GetPositionFromAddress(ushort offset)
		{
			int iPosition = -1;

			for (int i = 0; i < this.aInstructions.Count; i++)
			{
				if (this.aInstructions[i].Offset == offset)
				{
					iPosition = i;
					break;
				}
			}

			//if (iPosition == -1)
			//	throw new Exception(string.Format("Can't find the offset 0x{0:x4} in function {1}", offset, this.sName));

			return iPosition;
		}

		private ushort ReadWord(MemoryStream stream)
		{
			int byte0 = stream.ReadByte();
			int byte1 = stream.ReadByte();

			byte0 &= 0xff;
			byte1 &= 0xff;

			return (ushort)((byte1 << 8) | byte0);
		}

		public MZDecompiler Parent
		{
			get { return this.oParent; }
		}

		public string Name
		{
			get { return this.sName; }
		}

		public CallTypeEnum CallType
		{
			get { return this.eCallType; }
		}

		public int Overlay
		{
			get { return this.iOverlay; }
			set { this.iOverlay = value; }
		}

		public ushort Segment
		{
			get { return this.usSegment; }
		}

		public ushort Offset
		{
			get { return this.usOffset; }
		}

		public uint LinearAddress
		{
			get { return this.uiLinearAddress; }
		}

		public uint StreamOffset
		{
			get { return this.uiStreamOffset; }
		}

		public List<CParameter> Parameters
		{
			get { return this.aParameters; }
		}

		public int ParameterSize
		{
			get { return this.iParameterSize; }
			set { this.iParameterSize = value; }
		}

		public bool HasVariableParameters
		{
			get
			{
				if (this.aParameters.Count > 0 &&
					this.aParameters[this.aParameters.Count - 1].Type.Type == CTypeEnum.VariableParameters)
				{
					return true;
				}

				return false;
			}
		}

		public CType ReturnValue
		{
			get { return this.oReturnValue; }
		}

		public List<Instruction> Instructions
		{
			get { return this.aInstructions; }
		}

		public List<IStatement> Statements
		{
			get { return this.aStatements; }
		}

		public int StackSize
		{
			get { return this.iStackSize; }
		}
	}
}
