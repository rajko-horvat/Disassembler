using Disassembler.CPU;
using IRB.Collections.Generic;
using System.Reflection.Metadata;

namespace Disassembler
{
	public class ProgramFunction
	{
		private ProgramSegment parentSegment;

		private int ordinal = -1;
		private ushort fnOffset;
		private uint fnEntryPoint;
		private string? name;
		private bool isLibraryFunction = false;
		private ProgramFunctionTypeEnum callType = ProgramFunctionTypeEnum.Cdecl;
		private CPUParameterSizeEnum returnType = CPUParameterSizeEnum.Undefined;
		private int stackSize = 0;
		private BDictionary<int, ILVariable> localParameters = new BDictionary<int, ILVariable>();
		private BDictionary<int, ILVariable> localVariables = new BDictionary<int, ILVariable>();
		private int localVariableSize = 0;
		private int localVariablePosition = 6;

		private FlowGraph? flowGraph = null;

		// Assembly instructions
		private List<CPUInstruction> asmInstructions = new List<CPUInstruction>();

		public ProgramFunction(ProgramSegment segment, ushort offset, string? name)
		{
			this.parentSegment = segment;
			this.fnOffset = offset;
			this.fnEntryPoint = MainProgram.ToLinearAddress(segment.CPUSegment, offset);
			this.name = name;
		}

		public void Disassemble()
		{
			this.asmInstructions.Clear();
			uint fnLinearAddress = this.fnEntryPoint;

			#region Process function assembly instructions, including possible switch instructions
			uint fnSegment = MainProgram.ToLinearAddress(this.parentSegment.CPUSegment, 0);
			List<uint> aJumps = new();
			List<uint> aSwitches = new();
			byte[] exeData;
			uint fnAddress = this.fnEntryPoint;

			if (this.parentSegment.CPUOverlay > 0)
			{
				exeData = this.parentSegment.Parent.Executable.Overlays[this.parentSegment.CPUOverlay - 1].Data;
			}
			else
			{
				exeData = this.parentSegment.Parent.Executable.Data;
			}

			if (fnAddress >= exeData.Length)
			{
				throw new Exception($"Trying to disassemble outside of executable range in function {this.parentSegment.ToString()}.{this.Name}, 0x{fnAddress:x}");
			}

			MemoryStream stream = new(exeData);

			stream.Seek(fnAddress, SeekOrigin.Begin);

			while (true)
			{
				CPUInstruction instruction1;
				bool bEnd = false;

				if (fnAddress >= stream.Length)
				{
					throw new Exception($"Trying to disassemble outside of executable range in function {this.parentSegment.ToString()}.{this.Name}, 0x{fnAddress:x}");
				}

				for (int i = 0; i < this.asmInstructions.Count; i++)
				{
					if (this.asmInstructions[i].LinearAddress == fnAddress)
					{
						bEnd = true;
						break;
					}
				}

				if (!bEnd)
				{
					CPUParameter parameter;
					CPUInstruction instruction = new CPUInstruction(this.parentSegment.CPUSegment, (ushort)(fnAddress - fnSegment), stream);

					this.asmInstructions.Add(instruction);
					fnAddress += (uint)instruction.Bytes.Count;

					switch (instruction.InstructionType)
					{
						case CPUInstructionEnum.JMP:
							parameter = instruction.Parameters[0];
							if (parameter.Type == CPUParameterTypeEnum.Immediate)
							{
								fnAddress = (uint)(fnSegment + parameter.Value);
								stream.Seek(fnAddress, SeekOrigin.Begin);
							}
							else if (parameter.Type == CPUParameterTypeEnum.MemoryAddress && parameter.Value == 6)
							{
								throw new Exception($"Relative jump to {parameter.ToString()} in function {this.parentSegment.ToString()}.{this.Name} " +
									$"(Instruction at 0x{instruction.LinearAddress:x})");
							}
							else if (parameter.Type == CPUParameterTypeEnum.MemoryAddress)
							{
								// probably switch statement
								aSwitches.Add(instruction.LinearAddress);
								bEnd = true;
							}
							else
							{
								Console.WriteLine($"Jump to computed address {parameter.ToString()} in function {this.parentSegment.ToString()}.{this.Name} " +
									$"(Instruction at 0x{instruction.LinearAddress:x})");
								// treat this as end of a instruction stream
								bEnd = true;
							}
							break;

						case CPUInstructionEnum.JMPF:
							parameter = instruction.Parameters[0];
							if (parameter.Type == CPUParameterTypeEnum.SegmentOffset)
							{
								fnAddress = MainProgram.ToLinearAddress(parameter.Segment, parameter.Value);

								if (fnAddress == 0)
								{
									this.parentSegment.GlobalVariables.Add((int)(instruction.Offset + 1), new ILVariable(this, ILVariableScopeEnum.Global,
										ILValueTypeEnum.FnPtr32, (int)(instruction.Offset + 1)));
									// treat this as end of a instruction stream
									bEnd = true;
								}
								else
								{
									stream.Seek(fnAddress, SeekOrigin.Begin);
								}
							}
							else
							{
								Console.WriteLine($"Jump to {parameter.ToString()} in function {this.parentSegment.ToString()}.{this.Name} " +
									$"(Instruction at 0x{instruction.LinearAddress:x})");
								// treat this as end of a instruction stream
								bEnd = true;
							}
							break;

						case CPUInstructionEnum.Jcc:
							parameter = instruction.Parameters[1];
							if (parameter.Type != CPUParameterTypeEnum.Immediate)
								throw new Exception(
									$"Relative offset expected, but got indirect parameter {parameter.ToString()} in function {this.parentSegment.ToString()}.{this.Name} " +
									$"(Instruction at 0x{instruction.LinearAddress:x})");
							aJumps.Add((uint)(fnSegment + parameter.Value));
							break;

						case CPUInstructionEnum.LOOP:
						case CPUInstructionEnum.LOOPNE:
						case CPUInstructionEnum.LOOPE:
						case CPUInstructionEnum.JCXZ:
							parameter = instruction.Parameters[0];
							if (parameter.Type != CPUParameterTypeEnum.Immediate)
								throw new Exception(
									$"Relative offset expected, but got indirect parameter {parameter.ToString()} in function {this.parentSegment.ToString()}.{this.Name} " +
									$"(Instruction at 0x{instruction.LinearAddress:x})");

							aJumps.Add((uint)(fnSegment + parameter.Value));
							break;

						case CPUInstructionEnum.INT:
							if (instruction.Parameters[0].Type == CPUParameterTypeEnum.Immediate &&
								instruction.Parameters[0].Value == 0x20)
							{
								// exit application instruction
								bEnd = true;
							}
							else if (instruction.Parameters[0].Type == CPUParameterTypeEnum.Immediate &&
								instruction.Parameters[0].Value == 0x3f)
							{
								// overlay manager
								instruction.InstructionType = CPUInstructionEnum.CallOverlay;
								int byte0 = stream.ReadByte();
								int byte1 = stream.ReadByte();
								int byte2 = stream.ReadByte();
								if (byte0 < 0 || byte1 < 0 || byte2 < 0)
									throw new Exception("Int 0x3F missing parameters");

								instruction.Bytes.Add((byte)(byte0 & 0xff));
								instruction.Bytes.Add((byte)(byte1 & 0xff));
								instruction.Bytes.Add((byte)(byte2 & 0xff));

								instruction.Parameters.Clear();
								instruction.Parameters.Add(new CPUParameter(CPUParameterTypeEnum.Immediate, CPUParameterSizeEnum.UInt8,
									(byte)(byte0 & 0xff)));
								instruction.Parameters.Add(new CPUParameter(CPUParameterTypeEnum.Immediate, CPUParameterSizeEnum.UInt16,
									(ushort)((byte1 & 0xff) | ((byte2 & 0xff) << 8))));
								fnAddress += 3;
							}
							else if (this.asmInstructions.Count > 1)
							{
								instruction1 = this.asmInstructions[this.asmInstructions.Count - 2];

								if (instruction1.Parameters.Count > 1 &&
									instruction1.Parameters[0].Type == CPUParameterTypeEnum.Register &&
									instruction1.Parameters[0].Size == CPUParameterSizeEnum.UInt8 &&
									instruction1.Parameters[0].Value == (uint)CPURegisterEnum.AH &&
									instruction1.Parameters[1].Type == CPUParameterTypeEnum.Immediate &&
									instruction1.Parameters[1].Value == 0x4c)
								{
									// exit application instruction
									bEnd = true;
								}
								else if (instruction1.Parameters.Count > 1 &&
									instruction1.Parameters[0].Type == CPUParameterTypeEnum.Register &&
									instruction1.Parameters[0].Size == CPUParameterSizeEnum.UInt16 &&
									instruction1.Parameters[0].Value == (uint)CPURegisterEnum.AX &&
									instruction1.Parameters[1].Type == CPUParameterTypeEnum.Immediate &&
									(instruction1.Parameters[1].Value & 0xff00) == 0x4c00)
								{
									// exit application instruction
									bEnd = true;
								}
							}
							break;

						case CPUInstructionEnum.RET:
							// convert near return to far return
							if ((this.callType & ProgramFunctionTypeEnum.Far) == ProgramFunctionTypeEnum.Far)
							{
								Console.WriteLine($"Inconsistent function return type in {this.parentSegment.ToString()}.{this.Name}");
							}
							this.callType |= ProgramFunctionTypeEnum.Near;

							if (instruction.Parameters.Count == 1 && (this.callType & ProgramFunctionTypeEnum.Cdecl) == ProgramFunctionTypeEnum.Cdecl)
							{
								Console.WriteLine($"Inconsistent function call type in {this.parentSegment.ToString()}.{this.Name}");
							}
							else if (instruction.Parameters.Count == 0 && (this.callType & ProgramFunctionTypeEnum.Pascal) == ProgramFunctionTypeEnum.Pascal)
							{
								Console.WriteLine($"Inconsistent function call type in {this.parentSegment.ToString()}.{this.Name}");
							}
							else
							{
								if (instruction.Parameters.Count == 1)
								{
									this.callType |= ProgramFunctionTypeEnum.Pascal;
								}
								else
								{
									this.callType |= ProgramFunctionTypeEnum.Cdecl;
								}
							}
							bEnd = true;
							break;

						case CPUInstructionEnum.RETF:
							if ((this.callType & ProgramFunctionTypeEnum.Near) == ProgramFunctionTypeEnum.Near)
							{
								Console.WriteLine($"Inconsistent function return type in {this.parentSegment.ToString()}.{this.Name}");
							}
							this.callType |= ProgramFunctionTypeEnum.Far;

							if (instruction.Parameters.Count == 1 && (this.callType & ProgramFunctionTypeEnum.Cdecl) == ProgramFunctionTypeEnum.Cdecl)
							{
								Console.WriteLine($"Inconsistent function call type in {this.parentSegment.ToString()}.{this.Name}");
							}
							else if (instruction.Parameters.Count == 0 && (this.callType & ProgramFunctionTypeEnum.Pascal) == ProgramFunctionTypeEnum.Pascal)
							{
								Console.WriteLine($"Inconsistent function call type in {this.parentSegment.ToString()}.{this.Name}");
							}
							else
							{
								if (instruction.Parameters.Count == 1)
								{
									this.callType |= ProgramFunctionTypeEnum.Pascal;
								}
								else
								{
									this.callType |= ProgramFunctionTypeEnum.Cdecl;
								}
							}
							bEnd = true;
							break;

						case CPUInstructionEnum.IRET:
							if ((this.callType & ProgramFunctionTypeEnum.Near) == ProgramFunctionTypeEnum.Near)
							{
								Console.WriteLine($"Inconsistent function return type in {this.parentSegment.ToString()}.{this.Name}");
							}
							this.callType |= ProgramFunctionTypeEnum.Far;

							bEnd = true;
							break;
					}
				}

				if (bEnd)
				{
					// jumps
					if (aJumps.Count > 0)
					{
						fnAddress = aJumps[aJumps.Count - 1];
						aJumps.RemoveAt(aJumps.Count - 1);
						stream.Seek(fnAddress, SeekOrigin.Begin);

						continue;
					}

					// switches
					if (aSwitches.Count > 0)
					{
						// sort instructions by address before doing switches
						this.asmInstructions.Sort((item1, item2) => item1.LinearAddress.CompareTo(item2.LinearAddress));

						fnAddress = aSwitches[aSwitches.Count - 1];
						aSwitches.RemoveAt(aSwitches.Count - 1);

						int iPos = GetInstructionPositionByLinearAddress(fnAddress);
						if (iPos >= 0)
						{
							if (iPos > 5)
							{
								int iPos1;
								CPUParameter parameter;
								uint uiCount = 0;
								ushort usSwitchOffset = 0;

								// first pattern
								if ((instruction1 = this.asmInstructions[iPos1 = iPos - 5]).InstructionType == CPUInstructionEnum.CMP &&
									instruction1.Parameters.Count == 2 &&
									instruction1.Parameters[0].Type == CPUParameterTypeEnum.Register &&
									(parameter = instruction1.Parameters[0]).RegisterValue == CPURegisterEnum.AX &&
									instruction1.Parameters[1].Type == CPUParameterTypeEnum.Immediate &&
									(uiCount = instruction1.Parameters[1].Value) >= 0 &&

									(instruction1 = this.asmInstructions[++iPos1]).InstructionType == CPUInstructionEnum.Jcc &&
									instruction1.Parameters.Count == 2 &&
									instruction1.Parameters[0].Type == CPUParameterTypeEnum.Condition &&
									instruction1.Parameters[0].Value == (uint)CPUJumpConditionEnum.BE &&
									instruction1.Parameters[1].Type == CPUParameterTypeEnum.Immediate &&

									(instruction1 = this.asmInstructions[++iPos1]).InstructionType == CPUInstructionEnum.JMP &&
									instruction1.Parameters.Count == 1 &&
									instruction1.Parameters[0].Type == CPUParameterTypeEnum.Immediate &&

									(instruction1 = this.asmInstructions[++iPos1]).InstructionType == CPUInstructionEnum.ADD &&
									instruction1.Parameters.Count == 2 &&
									instruction1.Parameters[0].Type == CPUParameterTypeEnum.Register &&
									(parameter = instruction1.Parameters[0]).RegisterValue == CPURegisterEnum.AX &&
									instruction1.Parameters[1].Type == CPUParameterTypeEnum.Register &&
									instruction1.Parameters[1].Value == instruction1.Parameters[0].Value &&

									(instruction1 = this.asmInstructions[++iPos1]).InstructionType == CPUInstructionEnum.XCHG &&
									instruction1.Parameters.Count == 2 &&
									instruction1.Parameters[0].Type == CPUParameterTypeEnum.Register &&
									instruction1.Parameters[0].RegisterValue == CPURegisterEnum.AX &&
									instruction1.Parameters[1].Type == CPUParameterTypeEnum.Register &&
									instruction1.Parameters[1].RegisterValue == CPURegisterEnum.BX &&

									(instruction1 = this.asmInstructions[++iPos1]).InstructionType == CPUInstructionEnum.JMP &&
									instruction1.Parameters.Count == 1 &&
									instruction1.Parameters[0].Type == CPUParameterTypeEnum.MemoryAddress &&
									(usSwitchOffset = (ushort)instruction1.Parameters[0].Displacement) >= 0 &&

									iPos == iPos1)
								{
									//Console.WriteLine("Switch type 1 at {0}:0x{1:x4}", this.uiSegment, this.aInstructions[iPos].Location.Offset);

									this.asmInstructions[iPos - 2].InstructionType = CPUInstructionEnum.NOP;
									this.asmInstructions[iPos - 1].InstructionType = CPUInstructionEnum.NOP;
									instruction1 = this.asmInstructions[iPos];
									instruction1.InstructionType = CPUInstructionEnum.SWITCH;
									instruction1.Parameters.Clear();

									// switching parameter
									instruction1.Parameters.Add(parameter);

									stream.Seek(fnSegment + usSwitchOffset, SeekOrigin.Begin);

									// values and offsets
									for (int i = 0; i <= uiCount; i++)
									{
										ushort usWord = ReadWord(stream);
										aJumps.Add((uint)(fnSegment + usWord));
										instruction1.Parameters.Add(new CPUParameter(CPUParameterTypeEnum.Immediate,
											CPUParameterSizeEnum.UInt16, (uint)i, usWord));
									}

									fnAddress = aJumps[aJumps.Count - 1];
									aJumps.RemoveAt(aJumps.Count - 1);

									stream.Seek(fnAddress, SeekOrigin.Begin);
								}
								else if ((instruction1 = this.asmInstructions[iPos1 = iPos - 5]).InstructionType == CPUInstructionEnum.SUB &&
									instruction1.Parameters.Count == 2 &&
									instruction1.Parameters[0].Type == CPUParameterTypeEnum.Register &&
									instruction1.Parameters[0].RegisterValue == CPURegisterEnum.AX &&
									instruction1.Parameters[1].Type == CPUParameterTypeEnum.Immediate &&

									(instruction1 = this.asmInstructions[++iPos1]).InstructionType == CPUInstructionEnum.CMP &&
									instruction1.Parameters.Count == 2 &&
									instruction1.Parameters[0].Type == CPUParameterTypeEnum.Register &&
									(parameter = instruction1.Parameters[0]).RegisterValue == CPURegisterEnum.AX &&
									instruction1.Parameters[1].Type == CPUParameterTypeEnum.Immediate &&
									(uiCount = instruction1.Parameters[1].Value) >= 0 &&

									(instruction1 = this.asmInstructions[++iPos1]).InstructionType == CPUInstructionEnum.Jcc &&
									instruction1.Parameters.Count == 2 &&
									instruction1.Parameters[0].Type == CPUParameterTypeEnum.Condition &&
									instruction1.Parameters[0].Value == (uint)CPUJumpConditionEnum.A &&
									instruction1.Parameters[1].Type == CPUParameterTypeEnum.Immediate &&

									(instruction1 = this.asmInstructions[++iPos1]).InstructionType == CPUInstructionEnum.ADD &&
									instruction1.Parameters.Count == 2 &&
									instruction1.Parameters[0].Type == CPUParameterTypeEnum.Register &&
									(parameter = instruction1.Parameters[0]).Value == (uint)CPURegisterEnum.AX &&
									instruction1.Parameters[1].Type == CPUParameterTypeEnum.Register &&
									instruction1.Parameters[1].Value == instruction1.Parameters[0].Value &&

									(instruction1 = this.asmInstructions[++iPos1]).InstructionType == CPUInstructionEnum.XCHG &&
									instruction1.Parameters.Count == 2 &&
									instruction1.Parameters[0].Type == CPUParameterTypeEnum.Register &&
									instruction1.Parameters[0].RegisterValue == CPURegisterEnum.AX &&
									instruction1.Parameters[1].Type == CPUParameterTypeEnum.Register &&
									instruction1.Parameters[1].RegisterValue == CPURegisterEnum.BX &&

									(instruction1 = this.asmInstructions[++iPos1]).InstructionType == CPUInstructionEnum.JMP &&
									instruction1.Parameters.Count == 1 &&
									instruction1.Parameters[0].Type == CPUParameterTypeEnum.MemoryAddress &&
									(usSwitchOffset = (ushort)instruction1.Parameters[0].Displacement) >= 0 &&

									iPos == iPos1)
								{
									//Console.WriteLine("Switch type 1 at {0}:0x{1:x4}", this.uiSegment, this.aInstructions[iPos].Location.Offset);

									this.asmInstructions[iPos - 2].InstructionType = CPUInstructionEnum.NOP;
									this.asmInstructions[iPos - 1].InstructionType = CPUInstructionEnum.NOP;
									instruction1 = this.asmInstructions[iPos];
									instruction1.InstructionType = CPUInstructionEnum.SWITCH;
									instruction1.Parameters.Clear();

									// switching parameter
									instruction1.Parameters.Add(parameter);

									stream.Seek(fnSegment + usSwitchOffset, SeekOrigin.Begin);

									// values and offsets
									for (int i = 0; i <= uiCount; i++)
									{
										ushort usWord = ReadWord(stream);
										aJumps.Add((uint)(fnSegment + usWord));
										instruction1.Parameters.Add(new CPUParameter(CPUParameterTypeEnum.Immediate,
											CPUParameterSizeEnum.UInt16, (uint)i, usWord));
									}

									fnAddress = aJumps[aJumps.Count - 1];
									aJumps.RemoveAt(aJumps.Count - 1);

									stream.Seek(fnAddress, SeekOrigin.Begin);
								}
								else
								{
									instruction1 = this.asmInstructions[iPos];
									Console.WriteLine($"Undefined switch pattern {instruction1.Parameters[0].ToString()} in function {this.parentSegment.ToString()}.{this.Name} " +
										$"(Instruction at 0x{instruction1.LinearAddress:x})");
									//break;
								}
							}
						}
						else
						{
							Console.WriteLine($"Can't find location of switch statement in function {this.parentSegment.ToString()}.{this.Name} " +
								$"(Instruction at 0x{fnAddress:x})");
						}
						continue;
					}

					// no more jumps or switches, we are done
					break;
				}
			}

			stream.Close();

			this.asmInstructions.Sort((item1, item2) => item1.LinearAddress.CompareTo(item2.LinearAddress));
			#endregion

			#region Find ret instruction and create one if it doesn't exist
			CPUInstruction? retInstruction = null;
			ushort lastInstructionOffset = 0;

			for (int i = 0; i < this.asmInstructions.Count; i++)
			{
				CPUInstruction instruction = this.asmInstructions[i];

				lastInstructionOffset = Math.Max(lastInstructionOffset, instruction.Offset);

				if (instruction.InstructionType == CPUInstructionEnum.RET ||
					instruction.InstructionType == CPUInstructionEnum.RETF ||
					instruction.InstructionType == CPUInstructionEnum.IRET)
				{
					retInstruction = instruction;
					break;
				}
			}

			if (retInstruction == null)
			{
				// it the function doesn't have return instruction, we will append it as it's required for further analysis
				retInstruction = new CPUInstruction(this.parentSegment.CPUSegment, (ushort)(lastInstructionOffset + 1), CPUInstructionEnum.RETF, CPUParameterSizeEnum.UInt16);
				this.asmInstructions.Add(retInstruction);

				Console.WriteLine($"Warning, the function {this.parentSegment.ToString()}.{this.Name} doesn't have return instruction, adding one.");
			}
			#endregion

			#region Optimize GoTo's
			for (int i = 0; i < this.asmInstructions.Count; i++)
			{
				CPUInstruction instruction = this.asmInstructions[i];
				CPUParameter parameter0;

				switch (instruction.InstructionType)
				{
					case CPUInstructionEnum.LOOP:
					case CPUInstructionEnum.LOOPE:
					case CPUInstructionEnum.LOOPNE:
					case CPUInstructionEnum.JCXZ:
					case CPUInstructionEnum.JMP:
					case CPUInstructionEnum.JMPF:
						parameter0 = instruction.Parameters[0];

						if (parameter0.Type == CPUParameterTypeEnum.Immediate || parameter0.Type == CPUParameterTypeEnum.SegmentOffset)
						{
							// optimize immediate jumps
							CPUInstruction instruction1;
							ushort segment;
							uint address;

							if (parameter0.Type == CPUParameterTypeEnum.Immediate)
							{
								segment = instruction.Segment;
								address = (uint)(fnSegment + parameter0.Value);
							}
							else
							{
								segment = parameter0.Segment;
								address = MainProgram.ToLinearAddress(parameter0.Segment, parameter0.Value);
							}

							if (instruction.InstructionType != CPUInstructionEnum.JMPF || address != 0)
							{
								uint newAddress = address;
								ushort newSegment = segment;

								while ((instruction1 = this.asmInstructions[GetInstructionPositionByLinearAddress(newAddress)]).InstructionType == CPUInstructionEnum.JMP ||
									instruction1.InstructionType == CPUInstructionEnum.JMPF)
								{
									CPUParameter parameter1 = instruction1.Parameters[0];

									if (parameter1.Type == CPUParameterTypeEnum.Immediate)
									{
										newSegment = instruction1.Segment;
										newAddress = MainProgram.ToLinearAddress(newSegment, instruction1.Parameters[0].Value);
									}
									else if (parameter1.Type == CPUParameterTypeEnum.SegmentOffset)
									{
										newSegment = instruction1.Parameters[0].Segment;
										newAddress = MainProgram.ToLinearAddress(newSegment, instruction1.Parameters[0].Value);
									}
									else
									{
										break;
									}

									if (instruction1.InstructionType == CPUInstructionEnum.JMPF && newAddress == 0)
										break;
								}

								if (address != newAddress)
								{
									if (segment != newSegment)
									{
										instruction.InstructionType = CPUInstructionEnum.JMPF;
										instruction.Parameters.Clear();
										instruction.Parameters.Add(new CPUParameter(newSegment, newAddress - MainProgram.ToLinearAddress(newSegment, 0)));
									}
									else
									{
										instruction.InstructionType = CPUInstructionEnum.JMP;
										instruction.Parameters.Clear();
										instruction.Parameters.Add(new CPUParameter(CPUParameterTypeEnum.Immediate, newAddress - MainProgram.ToLinearAddress(newSegment, 0)));
									}
								}
							}
						}
						break;

					case CPUInstructionEnum.Jcc:
						parameter0 = instruction.Parameters[1];

						if (parameter0.Type == CPUParameterTypeEnum.Immediate)
						{
							// optimize immediate jumps
							CPUInstruction instruction1;
							uint address = (uint)(fnSegment + parameter0.Value);
							uint newAddress = address;

							while ((instruction1 = this.asmInstructions[GetInstructionPositionByLinearAddress(newAddress)]).InstructionType == CPUInstructionEnum.JMP)
							{
								newAddress = (uint)(fnSegment + instruction1.Parameters[0].Value);
							}
							if (address != newAddress)
							{
								parameter0.Value = (ushort)(newAddress - fnSegment);
							}
						}
						break;
				}
			}
			#endregion

			#region Convert relative call(s) to absolute call(s)
			for (int i = 0; i < this.asmInstructions.Count; i++)
			{
				CPUInstruction asmInstruction = this.asmInstructions[i];
				CPUInstruction asmInstruction1;

				if (asmInstruction.InstructionType == CPUInstructionEnum.CALL)
				{
					if (this.parentSegment.CPUOverlay == 0)
					{
						if (i > 0 &&
							(asmInstruction1 = this.asmInstructions[i - 1]).InstructionType == CPUInstructionEnum.PUSH &&
							asmInstruction1.Parameters[0].Type == CPUParameterTypeEnum.SegmentRegister &&
							asmInstruction1.Parameters[0].Value == (uint)CPUSegmentRegisterEnum.CS)
						{
							asmInstruction.InstructionType = CPUInstructionEnum.CALLF;
							asmInstruction.Parameters[0] = new CPUParameter(asmInstruction.Segment, asmInstruction.Parameters[0].Value);

							asmInstruction1.InstructionType = CPUInstructionEnum.NOP;
							asmInstruction1.Parameters.Clear();
						}
					}
					else
					{
						// if we are calling inside of overlay translate this to call overlay function
						CPUParameter parameter = asmInstruction.Parameters[0];

						asmInstruction.InstructionType = CPUInstructionEnum.CallOverlay;
						asmInstruction.Parameters.Clear();
						asmInstruction.Parameters.Add(new CPUParameter(CPUParameterTypeEnum.Immediate, CPUParameterSizeEnum.UInt8, this.parentSegment.CPUOverlay));
						asmInstruction.Parameters.Add(new CPUParameter(CPUParameterTypeEnum.Immediate, CPUParameterSizeEnum.UInt16, parameter.Value));
					}
				}
			}
			#endregion

			#region Assign labels
			for (int i = 0; i < this.asmInstructions.Count; i++)
			{
				CPUInstruction instruction = this.asmInstructions[i];
				CPUParameter parameter;
				uint newAddress;

				switch (instruction.InstructionType)
				{
					case CPUInstructionEnum.JMP:
						parameter = instruction.Parameters[0];

						if (parameter.Type == CPUParameterTypeEnum.Immediate)
						{
							newAddress = (uint)(fnSegment + parameter.Value);

							// optimize immediate jumps
							if (i + 1 < this.asmInstructions.Count &&
								this.asmInstructions[i + 1].LinearAddress == newAddress)
							{
								// this is just a jump to next instruction, ignore it
								this.asmInstructions[i].InstructionType = CPUInstructionEnum.NOP;
							}
							else
							{
								this.asmInstructions[GetInstructionPositionByLinearAddress(newAddress)].Label = true;
							}
						}
						break;

					case CPUInstructionEnum.JMPF:
						parameter = instruction.Parameters[0];

						if (parameter.Type == CPUParameterTypeEnum.SegmentOffset)
						{
							newAddress = MainProgram.ToLinearAddress(parameter.Segment, parameter.Value);

							if (newAddress != 0)
							{
								this.asmInstructions[GetInstructionPositionByLinearAddress(newAddress)].Label = true;
							}
						}
						break;

					case CPUInstructionEnum.Jcc:
						newAddress = (ushort)instruction.Parameters[1].Value;
						this.asmInstructions[GetInstructionPositionByLinearAddress((uint)(fnSegment + newAddress))].Label = true;
						break;

					case CPUInstructionEnum.LOOP:
					case CPUInstructionEnum.LOOPE:
					case CPUInstructionEnum.LOOPNE:
					case CPUInstructionEnum.JCXZ:
						newAddress = (ushort)(instruction.Parameters[0].Value & 0xffff);
						this.asmInstructions[GetInstructionPositionByLinearAddress((uint)(fnSegment + newAddress))].Label = true;
						break;

					case CPUInstructionEnum.SWITCH:
						for (int j = 1; j < instruction.Parameters.Count; j++)
						{
							parameter = instruction.Parameters[j];

							this.asmInstructions[GetInstructionPositionByLinearAddress((uint)(fnSegment + parameter.Displacement))].Label = true;
						}
						break;
				}
			}
			#endregion

			#region Optimize (XOR, SUB), (PUSH word, PUSH word, POP dword) and (LEA, PUSH)
			for (int i = 0; i < this.asmInstructions.Count; i++)
			{
				CPUInstruction instruction = this.asmInstructions[i];
				CPUInstruction instruction1;
				uint sourceReg1 = 0;
				uint sourceReg2 = 0;
				uint destinationReg = 0;

				// all xors and subs with same source and destination are 0
				if (i + 1 < this.asmInstructions.Count &&
					((instruction1 = this.asmInstructions[i]).InstructionType == CPUInstructionEnum.XOR ||
					instruction1.InstructionType == CPUInstructionEnum.SUB) &&
					instruction1.Parameters.Count == 2 &&
					instruction1.Parameters[0].Type == CPUParameterTypeEnum.Register &&
					instruction1.Parameters[1].Type == CPUParameterTypeEnum.Register &&
					instruction1.Parameters[0].Size == instruction1.Parameters[1].Size &&
					instruction1.Parameters[0].Value == instruction1.Parameters[1].Value &&

					(instruction1 = this.asmInstructions[i + 1]).InstructionType != CPUInstructionEnum.Jcc)
				{
					instruction1 = this.asmInstructions[i];
					instruction1.InstructionType = CPUInstructionEnum.MOV;
					instruction1.Parameters[1] = new CPUParameter(CPUParameterTypeEnum.Immediate, 0);
				}

				// optimize convert two words to dword
				if (i + 2 < this.asmInstructions.Count &&
					(instruction1 = this.asmInstructions[i]).InstructionType == CPUInstructionEnum.PUSH &&
					instruction1.OperandSize == CPUParameterSizeEnum.UInt16 &&
					instruction1.Parameters.Count == 1 &&
					instruction1.Parameters[0].Type == CPUParameterTypeEnum.Register &&
					(sourceReg1 = instruction1.Parameters[0].Value) != (uint)CPURegisterEnum.Invalid &&

					(instruction1 = this.asmInstructions[i + 1]).InstructionType == CPUInstructionEnum.PUSH &&
					instruction1.OperandSize == CPUParameterSizeEnum.UInt16 &&
					instruction1.Parameters.Count == 1 &&
					instruction1.Parameters[0].Type == CPUParameterTypeEnum.Register &&
					(sourceReg2 = instruction1.Parameters[0].Value) != (uint)CPURegisterEnum.Invalid &&

					(instruction1 = this.asmInstructions[i + 2]).InstructionType == CPUInstructionEnum.POP &&
					instruction1.OperandSize == CPUParameterSizeEnum.UInt32 &&
					instruction1.Parameters.Count == 1 &&
					instruction1.Parameters[0].Type == CPUParameterTypeEnum.Register &&
					(destinationReg = instruction1.Parameters[0].Value) != (uint)CPURegisterEnum.Invalid)
				{
					instruction1 = this.asmInstructions[i];
					instruction1.InstructionType = CPUInstructionEnum.WordsToDword;
					instruction1.OperandSize = CPUParameterSizeEnum.UInt32;
					instruction1.Parameters.Clear();
					instruction1.Parameters.Add(new CPUParameter(CPUParameterTypeEnum.Register,
						CPUParameterSizeEnum.UInt32, destinationReg));
					instruction1.Parameters.Add(new CPUParameter(CPUParameterTypeEnum.Register,
						CPUParameterSizeEnum.UInt16, sourceReg1));
					instruction1.Parameters.Add(new CPUParameter(CPUParameterTypeEnum.Register,
						CPUParameterSizeEnum.UInt16, sourceReg2));

					this.asmInstructions[i + 1].InstructionType = CPUInstructionEnum.NOP;
					this.asmInstructions[i + 2].InstructionType = CPUInstructionEnum.NOP;
				}
				if (i + 1 < this.asmInstructions.Count &&
					(instruction1 = this.asmInstructions[i]).InstructionType == CPUInstructionEnum.LEA &&
					instruction1.OperandSize == CPUParameterSizeEnum.UInt16 &&
					instruction1.Parameters.Count == 2 &&
					instruction1.Parameters[0].Type == CPUParameterTypeEnum.Register &&
					(sourceReg1 = instruction1.Parameters[0].Value) != (uint)CPURegisterEnum.Invalid &&

					(instruction1 = this.asmInstructions[i + 1]).InstructionType == CPUInstructionEnum.PUSH &&
					instruction1.OperandSize == CPUParameterSizeEnum.UInt16 &&
					instruction1.Parameters.Count == 1 &&
					instruction1.Parameters[0].Type == CPUParameterTypeEnum.Register &&
					instruction1.Parameters[0].Value == sourceReg1)
				{
					this.asmInstructions[i].InstructionType = CPUInstructionEnum.NOP;
					CPUParameter parameter = this.asmInstructions[i].Parameters[1];
					instruction1.Parameters[0] = new CPUParameter(CPUParameterTypeEnum.LEAMemoryAddress,
						parameter.Size, parameter.DataSegment, parameter.Value, parameter.Displacement);
				}
			}
			#endregion

			#region Remove Nop, Wait and unused GoTo instructions
			for (int i = 0; i < this.asmInstructions.Count; i++)
			{
				CPUInstruction instruction = this.asmInstructions[i];
				CPUInstruction instruction1;

				switch (instruction.InstructionType)
				{
					case CPUInstructionEnum.NOP:
					case CPUInstructionEnum.WAIT:
						if (!instruction.Label)
						{
							this.asmInstructions.RemoveAt(i);
							i--;
						}
						break;

					case CPUInstructionEnum.JMP:
					case CPUInstructionEnum.JMPF:
						if (i + 1 < this.asmInstructions.Count &&
							((instruction1 = this.asmInstructions[i + 1]).InstructionType == CPUInstructionEnum.JMP ||
							instruction1.InstructionType == CPUInstructionEnum.JMPF) &&
							!instruction1.Label)
						{
							this.asmInstructions.RemoveAt(i + 1);
							i--;
						}
						break;
				}
			}
			#endregion

			#region Convert Relative stack Memory addresses to local variables and parameters
			for (int i = 0; i < this.asmInstructions.Count; i++)
			{
				CPUInstruction instruction = this.asmInstructions[i];
				CPUParameter parameter;

				for (int j = 0; j < instruction.Parameters.Count; j++)
				{
					parameter = instruction.Parameters[j];
					int varOffset;

					switch (parameter.Type)
					{
						case CPUParameterTypeEnum.MemoryAddress:
						case CPUParameterTypeEnum.LEAMemoryAddress:
							if (parameter.Size == CPUParameterSizeEnum.UInt32)
								throw new Exception("x32 addressing mode not yet implemented");

							if (parameter.DataSegment == CPUSegmentRegisterEnum.SS)
							{
								switch (parameter.Value)
								{
									case 2: // [BP + SI]
									case 3: // [BP + DI]
										Console.WriteLine($"Stack parameter with register offset {parameter.ToString()} in function {this.parentSegment.ToString()}.{this.Name} " +
											$"(Instruction at 0x{instruction.LinearAddress:x})");
										break;

									case 10:
									case 18:
										// [BP + SI {0}]
										varOffset = parameter.Displacement;
										if (varOffset < 0)
										{
											// local variable
											varOffset = -varOffset;

											/*if (iVarOffset > this.LocalStackSize)
											{
												Console.WriteLine($"Local variable 0x{iVarOffset:x} is outside of a local stack space 0x{this.LocalStackSize:x} "+
													$"at function {this.parent.ToString()}.{this.Name}' (instruction at 0x{instruction.Offset:x4})");
											}*/

											instruction.Parameters[j] = new CPUParameter(CPUParameterTypeEnum.LocalVariableWithSI,
												CPUParameterSizeEnum.UInt16, CPUSegmentRegisterEnum.SS, 0, varOffset);

											if (!this.localVariables.ContainsKey(varOffset))
											{
												ILVariable variable = new ILVariable(this, ILVariableScopeEnum.LocalVariable, instruction.OperandSize, varOffset);
												variable.ArraySize = 1;
												this.localVariables.Add(varOffset, variable);
											}
											else
											{
												// ansure that we mark this as an array
												this.localVariables.GetValueByKey(varOffset).ArraySize = 1;
											}
										}
										else if (varOffset > 0)
										{
											// local parameters
											instruction.Parameters[j] = new CPUParameter(CPUParameterTypeEnum.LocalParameterWithSI,
												CPUParameterSizeEnum.UInt16, CPUSegmentRegisterEnum.SS, 0, varOffset);

											if (!this.localParameters.ContainsKey(varOffset))
											{
												ILVariable variable = new ILVariable(this, ILVariableScopeEnum.LocalParameter, instruction.OperandSize, varOffset);
												variable.ArraySize = 1;
												this.localParameters.Add(varOffset, variable);
											}
											else
											{
												// ansure that we mark this as an array
												this.localParameters.GetValueByKey(varOffset).ArraySize = 1;
											}
										}
										break;

									case 11:
									case 19:
										// [BP + DI {0}]
										varOffset = parameter.Displacement;
										if (varOffset < 0)
										{
											// local variable
											varOffset = -varOffset;

											/*if (iVarOffset > this.LocalStackSize)
											{
												Console.WriteLine($"Local variable 0x{iVarOffset:x} is outside of a local stack space 0x{this.LocalStackSize:x} " +
													$"at function {this.parent.ToString()}.{this.Name}' (instruction at 0x{instruction.Offset:x4})");
											}*/

											instruction.Parameters[j] = new CPUParameter(CPUParameterTypeEnum.LocalVariableWithDI,
												CPUParameterSizeEnum.UInt16, CPUSegmentRegisterEnum.SS, 0, varOffset);

											if (!this.localVariables.ContainsKey(varOffset))
											{
												ILVariable variable = new ILVariable(this, ILVariableScopeEnum.LocalVariable, instruction.OperandSize, varOffset);
												variable.ArraySize = 1;
												this.localVariables.Add(varOffset, variable);
											}
											else
											{
												// ansure that we mark this as an array
												this.localVariables.GetValueByKey(varOffset).ArraySize = 1;
											}
										}
										else if (varOffset > 0)
										{
											// local parameters
											instruction.Parameters[j] = new CPUParameter(CPUParameterTypeEnum.LocalParameterWithDI,
												CPUParameterSizeEnum.UInt16, CPUSegmentRegisterEnum.SS, 0, varOffset);

											if (!this.localParameters.ContainsKey(varOffset))
											{
												ILVariable variable = new ILVariable(this, ILVariableScopeEnum.LocalParameter, instruction.OperandSize, varOffset);
												variable.ArraySize = 1;
												this.localParameters.Add(varOffset, variable);
											}
											else
											{
												// ansure that we mark this as an array
												this.localParameters.GetValueByKey(varOffset).ArraySize = 1;
											}
										}
										break;

									case 14:
									case 22:
										// [BP {0}]
										varOffset = parameter.Displacement;
										if (varOffset < 0)
										{
											// local variable
											varOffset = -varOffset;

											/*if (iVarOffset > this.LocalStackSize)
											{
												Console.WriteLine($"Local variable 0x{iVarOffset:x} is outside of a local stack space 0x{this.LocalStackSize:x} " +
													$"at function {this.parent.ToString()}.{this.Name}' (instruction at 0x{instruction.Offset:x4})");
											}*/

											instruction.Parameters[j] = new CPUParameter(CPUParameterTypeEnum.LocalVariable,
												CPUParameterSizeEnum.UInt16, CPUSegmentRegisterEnum.SS, 0, varOffset);

											if (!this.localVariables.ContainsKey(varOffset))
											{
												this.localVariables.Add(varOffset, new ILVariable(this, instruction.OperandSize, varOffset));
											}
										}
										else if (varOffset > 0)
										{
											// local parameters
											instruction.Parameters[j] = new CPUParameter(CPUParameterTypeEnum.LocalParameter,
												CPUParameterSizeEnum.UInt16, CPUSegmentRegisterEnum.SS, 0, varOffset);

											if (!this.localParameters.ContainsKey(varOffset))
											{
												this.localParameters.Add(varOffset, new ILVariable(this, ILVariableScopeEnum.LocalParameter, instruction.OperandSize, varOffset));
											}
										}
										break;
								}
							}
							break;
					}
				}
			}
			#endregion

			#region Append goto instruction after INT exit instruction(s) and after indirect JMP(F)
			for (int i = 0; i < this.asmInstructions.Count; i++)
			{
				CPUInstruction instruction = this.asmInstructions[i];
				CPUInstruction instruction1;

				if (instruction.InstructionType == CPUInstructionEnum.INT)
				{
					if (instruction.Parameters[0].Type == CPUParameterTypeEnum.Immediate &&
						instruction.Parameters[0].Value == 0x20)
					{
						// exit application instruction
						instruction1 = new CPUInstruction(instruction.Segment, 0, CPUInstructionEnum.JMP, instruction.DefaultSize);
						instruction1.Parameters.Add(new CPUParameter(CPUParameterTypeEnum.Immediate, retInstruction.Offset));

						this.asmInstructions.Insert(i + 1, instruction1);
						retInstruction.Label = true;
						i++;
					}
					else if (i > 2)
					{
						instruction1 = this.asmInstructions[this.asmInstructions.Count - 2];

						if (instruction1.Parameters.Count > 1 &&
							instruction1.Parameters[0].Type == CPUParameterTypeEnum.Register &&
							instruction1.Parameters[0].Size == CPUParameterSizeEnum.UInt8 &&
							instruction1.Parameters[0].Value == (uint)CPURegisterEnum.AH &&
							instruction1.Parameters[1].Type == CPUParameterTypeEnum.Immediate &&
							instruction1.Parameters[1].Value == 0x4c)
						{
							// exit application instruction
							instruction1 = new CPUInstruction(instruction.Segment, 0, CPUInstructionEnum.JMP, instruction.DefaultSize);
							instruction1.Parameters.Add(new CPUParameter(CPUParameterTypeEnum.Immediate, retInstruction.Offset));

							this.asmInstructions.Insert(i + 1, instruction1);
							retInstruction.Label = true;
							i++;
						}
						else if (instruction1.Parameters.Count > 1 &&
							instruction1.Parameters[0].Type == CPUParameterTypeEnum.Register &&
							instruction1.Parameters[0].Size == CPUParameterSizeEnum.UInt16 &&
							instruction1.Parameters[0].Value == (uint)CPURegisterEnum.AX &&
							instruction1.Parameters[1].Type == CPUParameterTypeEnum.Immediate &&
							(instruction1.Parameters[1].Value & 0xff00) == 0x4c00)
						{
							// exit application instruction
							instruction1 = new CPUInstruction(instruction.Segment, 0, CPUInstructionEnum.JMP, instruction.DefaultSize);
							instruction1.Parameters.Add(new CPUParameter(CPUParameterTypeEnum.Immediate, retInstruction.Offset));

							this.asmInstructions.Insert(i + 1, instruction1);
							retInstruction.Label = true;
							i++;
						}
					}
				}
				else if ((instruction.InstructionType == CPUInstructionEnum.JMP && instruction.Parameters[0].Type != CPUParameterTypeEnum.Immediate) || 
					(instruction.InstructionType == CPUInstructionEnum.JMPF && instruction.Parameters[0].Type != CPUParameterTypeEnum.SegmentOffset))
				{
					instruction1 = new CPUInstruction(instruction.Segment, 0, CPUInstructionEnum.JMP, instruction.DefaultSize);
					instruction1.Parameters.Add(new CPUParameter(CPUParameterTypeEnum.Immediate, retInstruction.Offset));

					this.asmInstructions.Insert(i + 1, instruction1);
					retInstruction.Label = true;
					i++;
				}
			}
			#endregion

			#region Assign ordinals to labels

			if (this.asmInstructions.Count > 0 && this.fnEntryPoint != this.asmInstructions[0].LinearAddress)
			{
				CPUInstruction? instruction = GetInstructionByLinearAddress(this.fnEntryPoint);

				if (instruction != null)
				{
					instruction.Label = true;
				}
			}

			int labelOrdinal = 1;

			for (int i = 0; i < this.asmInstructions.Count; i++)
			{
				CPUInstruction instruction = this.asmInstructions[i];

				if (instruction.Label)
				{
					instruction.LabelOrdinal = labelOrdinal++;
				}
			}
			#endregion

			this.flowGraph = new FlowGraph(this, this.Name);

			#region Process calls to other functions
			for (int i = 0; i < this.asmInstructions.Count; i++)
			{
				CPUInstruction instruction = this.asmInstructions[i];
				CPUParameter parameter;
				ProgramFunction? function;

				switch (instruction.InstructionType)
				{
					case CPUInstructionEnum.CALL:
						parameter = instruction.Parameters[0];

						if (parameter.Type == CPUParameterTypeEnum.Immediate)
						{
							function = this.parentSegment.Parent.FindFunction(0, this.parentSegment.CPUSegment, (ushort)parameter.Value);
							if (function == null)
							{
								// function is not yet defined, define it
								this.parentSegment.Parent.Disassemble(0, this.parentSegment.CPUSegment, (ushort)parameter.Value, null);
							}
						}
						break;

					case CPUInstructionEnum.CALLF:
						parameter = instruction.Parameters[0];
						if (parameter.Type == CPUParameterTypeEnum.SegmentOffset)
						{
							function = this.parentSegment.Parent.FindFunction(0, parameter.Segment, (ushort)parameter.Value);
							if (function == null)
							{
								// function is not yet defined, define it
								this.parentSegment.Parent.Disassemble(0, parameter.Segment, (ushort)parameter.Value, null);
							}
						}
						break;

					case CPUInstructionEnum.CallOverlay:
						if (instruction.Parameters[0].Value == 0)
						{
							throw new Exception("Overlay manager references overlay 0");
						}

						function = this.parentSegment.Parent.FindFunction((ushort)instruction.Parameters[0].Value, 0, (ushort)instruction.Parameters[1].Value);
						if (function == null)
						{
							// function is not yet defined, define it
							this.parentSegment.Parent.Disassemble((ushort)instruction.Parameters[0].Value, 0, (ushort)instruction.Parameters[1].Value, null);
						}
						break;

					case CPUInstructionEnum.JMPF:
						//throw new Exception("Jump to a far address");
						/*parameter = instruction.Parameters[0];
						if (parameter.Type == CPUInstructionParameterTypeEnum.SegmentOffset)
						{
							function = decompiler.FindFunction(0, parameter.Segment, (ushort)parameter.Value);
							if (function == null)
							{
								// function is not yet defined, define it
								decompiler.Disassemble(0, parameter.Segment, (ushort)parameter.Value, null);
								//function = decompiler.GetFunction(parameter.Segment, (ushort)parameter.Value, this.uiStreamOffset);
							}
						}*/
						break;
				}
			}
			#endregion
		}

		private CPUJumpConditionEnum NegateCondition(CPUJumpConditionEnum condition)
		{
			switch (condition)
			{
				case CPUJumpConditionEnum.O:
					return CPUJumpConditionEnum.NO;
				case CPUJumpConditionEnum.NO:
					return CPUJumpConditionEnum.O;
				case CPUJumpConditionEnum.B:
					return CPUJumpConditionEnum.AE;
				case CPUJumpConditionEnum.AE:
					return CPUJumpConditionEnum.B;
				case CPUJumpConditionEnum.E:
					return CPUJumpConditionEnum.NE;
				case CPUJumpConditionEnum.NE:
					return CPUJumpConditionEnum.E;
				case CPUJumpConditionEnum.BE:
					return CPUJumpConditionEnum.A;
				case CPUJumpConditionEnum.A:
					return CPUJumpConditionEnum.BE;
				case CPUJumpConditionEnum.S:
					return CPUJumpConditionEnum.NS;
				case CPUJumpConditionEnum.NS:
					return CPUJumpConditionEnum.S;
				case CPUJumpConditionEnum.P:
					return CPUJumpConditionEnum.NP;
				case CPUJumpConditionEnum.NP:
					return CPUJumpConditionEnum.P;
				case CPUJumpConditionEnum.L:
					return CPUJumpConditionEnum.GE;
				case CPUJumpConditionEnum.GE:
					return CPUJumpConditionEnum.L;
				case CPUJumpConditionEnum.LE:
					return CPUJumpConditionEnum.G;
				case CPUJumpConditionEnum.G:
					return CPUJumpConditionEnum.LE;
			}

			return CPUJumpConditionEnum.Undefined;
		}

		public CPUInstruction? GetInstructionByOffset(ushort offset)
		{
			for (int i = 0; i < this.asmInstructions.Count; i++)
			{
				if (this.asmInstructions[i].Offset == offset)
				{
					return this.asmInstructions[i];
				}
			}

			return null;
		}

		public int GetInstructionPositionByOffset(ushort offset)
		{
			for (int i = 0; i < this.asmInstructions.Count; i++)
			{
				if (this.asmInstructions[i].Offset == offset)
				{
					return i;
				}
			}

			return -1;
		}

		public CPUInstruction? GetInstructionByLinearAddress(uint address)
		{
			for (int i = 0; i < this.asmInstructions.Count; i++)
			{
				if (this.asmInstructions[i].LinearAddress == address)
				{
					return this.asmInstructions[i];
				}
			}

			return null;
		}

		public int GetInstructionPositionByLinearAddress(uint address)
		{
			for (int i = 0; i < this.asmInstructions.Count; i++)
			{
				if (this.asmInstructions[i].LinearAddress == address)
				{
					return i;
				}
			}

			return -1;
		}

		private ushort ReadWord(MemoryStream stream)
		{
			int byte0 = stream.ReadByte();
			int byte1 = stream.ReadByte();

			byte0 &= 0xff;
			byte1 &= 0xff;

			return (ushort)((byte1 << 8) | byte0);
		}

		public void WriteAsmCS(StreamWriter writer, int tabLevel, int verbosity)
		{
			bool retInstruction = false;
			writer.WriteLine();
			writer.Write($"{GetTabs(tabLevel)}public void {this.Name}(");
			
			ILVariable[] parameters = this.localParameters.Values.ToArray();
			Array.Sort(parameters, (item1, item2) => item1.Offset.CompareTo(item2.Offset));

			for (int k = 0; k < parameters.Length; k++)
			{
				if (k > 0)
					writer.Write(", ");
				writer.Write(parameters[k].ToCSDeclarationString());
			}

			writer.WriteLine(")");
			writer.WriteLine($"{GetTabs(tabLevel)}{{");

			if (this.isLibraryFunction)
			{
				writer.WriteLine($"{GetTabs(tabLevel + 1)}// Will not disassemble library function at 0x{this.fnOffset:x}");
			}
			else
			{
				if (this.flowGraph != null)
				{
					writer.WriteLine($"{GetTabs(tabLevel + 1)}// {(this.flowGraph.BPFrame ? "Standard C frame" : "Assembly")}");
				}

				if (verbosity > 0)
				{
					writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.Log.EnterBlock(\"'{this.Name}'({this.callType.ToString()}) at {this.parentSegment.ToString()}:0x{this.fnOffset:x}\");");
					writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.CS.UInt16 = 0x{this.parentSegment.Segment:x4}; // set this function segment");
				}
				else
				{
					writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.Log.EnterBlock(\"'{this.Name}'({this.callType.ToString()}) at 0x{this.fnOffset:x}\");");
				}

				if (this.localVariables.Count > 0)
				{
					writer.WriteLine();
					writer.WriteLine($"{GetTabs(tabLevel + 1)}// Local variables");

					ILVariable[] variables = this.localVariables.Values.ToArray();
					Array.Sort(variables, (item1, item2) => item1.Offset.CompareTo(item2.Offset));

					for (int k = 0; k < variables.Length; k++)
					{
						writer.WriteLine($"{GetTabs(tabLevel + 1)}{variables[k].ToCSDeclarationString()};");
					}
				}

				writer.WriteLine();
				writer.WriteLine($"{GetTabs(tabLevel + 1)}// function body");

				if (this.asmInstructions.Count > 0 && this.fnEntryPoint != this.asmInstructions[0].LinearAddress)
				{
					writer.WriteLine($"{GetTabs(tabLevel + 1)}goto {GetInstructionByLinearAddress(this.fnEntryPoint)?.LabelName};");
				}

				for (int k = 0; k < this.asmInstructions.Count; k++)
				{
					// writer.WriteLine("{GetTabs(tabLevel)}{0}\t{1}", function.Instructions[j].Location.ToString(), function.Instructions[j]);
					CPUInstruction instruction = this.asmInstructions[k];

					if (instruction.Label)
					{
						writer.WriteLine();
						if (verbosity > 0)
						{
							writer.WriteLine($"{GetTabs(tabLevel)}{instruction.LabelName}: // 0x{instruction.Offset:x}");
						}
						else
						{
							writer.WriteLine($"{GetTabs(tabLevel)}{instruction.LabelName}:");
						}
					}

					uint uiOffset = 0;
					CPUParameter parameter;
					ProgramFunction? function1;
					int instructionIndex;

					switch (instruction.InstructionType)
					{
						case CPUInstructionEnum.ADC:
						case CPUInstructionEnum.ADD:
						case CPUInstructionEnum.AND:
						case CPUInstructionEnum.OR:
						case CPUInstructionEnum.SBB:
						case CPUInstructionEnum.SUB:
						case CPUInstructionEnum.XOR:
							parameter = instruction.Parameters[0];
							writer.Write($"{GetTabs(tabLevel + 1)}");
							writer.WriteLine(parameter.ToDestinationCSTextMZ(instruction.OperandSize, string.Format("this.oCPU.{0}{1}({2}, {3})",
								instruction.InstructionType.ToString(), instruction.OperandSize.ToString(),
								parameter.ToSourceCSTextMZ(instruction.OperandSize), instruction.Parameters[1].ToSourceCSTextMZ(instruction.OperandSize))));
							break;

						case CPUInstructionEnum.DEC:
						case CPUInstructionEnum.INC:
						case CPUInstructionEnum.NEG:
						case CPUInstructionEnum.NOT:
							parameter = instruction.Parameters[0];
							writer.Write($"{GetTabs(tabLevel + 1)}");
							writer.WriteLine(parameter.ToDestinationCSTextMZ(instruction.OperandSize, string.Format("this.oCPU.{0}{1}({2})",
								instruction.InstructionType.ToString(), instruction.OperandSize.ToString(), parameter.ToSourceCSTextMZ(instruction.OperandSize))));
							break;

						case CPUInstructionEnum.DAS:
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.DAS();");
							break;

						/*case CPUInstructionEnum.AAA:
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.AAA();");
							break;*/

						case CPUInstructionEnum.SAR:
						case CPUInstructionEnum.SHL:
						case CPUInstructionEnum.SHR:
						case CPUInstructionEnum.RCR:
						case CPUInstructionEnum.RCL:
						case CPUInstructionEnum.ROL:
						case CPUInstructionEnum.ROR:
							parameter = instruction.Parameters[0];
							writer.Write($"{GetTabs(tabLevel + 1)}");
							writer.WriteLine(parameter.ToDestinationCSTextMZ(instruction.OperandSize, string.Format("this.oCPU.{0}{1}({2}, {3})",
								instruction.InstructionType.ToString(), instruction.OperandSize.ToString(),
								parameter.ToSourceCSTextMZ(instruction.OperandSize), instruction.Parameters[1].ToSourceCSTextMZ(instruction.Parameters[1].Size))));
							break;

						/*case CPUInstructionEnum.SHLD:
							parameter = asmInstruction.Parameters[0];
							writer.Write($"{GetTabs(tabLevel + 1)}");
							writer.WriteLine(parameter.ToDestinationCSTextMZ(asmInstruction.OperandSize, string.Format("this.oCPU.{0}1{1}({2}, {3}, {4})",
								asmInstruction.InstructionType.ToString(), asmInstruction.OperandSize.ToString(),
								parameter.ToSourceCSTextMZ(asmInstruction.OperandSize), asmInstruction.Parameters[1].ToSourceCSTextMZ(asmInstruction.OperandSize),
								asmInstruction.Parameters[2].ToSourceCSTextMZ(asmInstruction.Parameters[2].Size))));
							break;*/

						case CPUInstructionEnum.CBW:
							if (instruction.OperandSize == CPUParameterSizeEnum.UInt16)
							{
								writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.CBW();");
							}
							else
							{
								writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.CWDE();");
							}
							break;

						case CPUInstructionEnum.CWD:
							if (instruction.OperandSize == CPUParameterSizeEnum.UInt16)
							{
								writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.CWD();");
							}
							else
							{
								writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.CDQ();");
							}
							break;

						/*case CPUInstructionEnum.CMPS:
							writer.Write($"{GetTabs(tabLevel + 1)}this.oCPU.");
							if (asmInstruction.RepPrefix == CPUInstructionPrefixEnum.REPE || asmInstruction.RepPrefix == CPUInstructionPrefixEnum.REPNE)
							{
								writer.WriteLine("{0}_CMPS{1}(this.oCPU.ES, this.oCPU.DI, {2}, this.oCPU.SI);",
									asmInstruction.RepPrefix.ToString(), asmInstruction.OperandSize.ToString(), asmInstruction.GetDefaultDataSegmentTextMZ());
							}
							else
							{
								writer.WriteLine("CMPS{0}(this.oCPU.ES, this.oCPU.DI, {1}, this.oCPU.SI);",
									asmInstruction.OperandSize.ToString(), asmInstruction.GetDefaultDataSegmentTextMZ());
							}
							break;*/

						case CPUInstructionEnum.CMP:
						case CPUInstructionEnum.TEST:
							parameter = instruction.Parameters[0];
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.{instruction.InstructionType.ToString()}{instruction.OperandSize.ToString()}" +
								$"({parameter.ToSourceCSTextMZ(instruction.OperandSize)}, {instruction.Parameters[1].ToSourceCSTextMZ(instruction.OperandSize)});");
							break;

						case CPUInstructionEnum.DIV:
						case CPUInstructionEnum.IDIV:
							if (instruction.OperandSize == CPUParameterSizeEnum.UInt8)
							{
								writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.{instruction.InstructionType.ToString()}{instruction.OperandSize.ToString()}" +
									$"(this.oCPU.AX, {instruction.Parameters[0].ToSourceCSTextMZ(instruction.OperandSize)});");
							}
							else
							{
								writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.{instruction.InstructionType.ToString()}{instruction.OperandSize.ToString()}" +
									$"(this.oCPU.AX, this.oCPU.DX, {instruction.Parameters[0].ToSourceCSTextMZ(instruction.OperandSize)});");
							}
							break;

						case CPUInstructionEnum.MUL:
							if (instruction.OperandSize == CPUParameterSizeEnum.UInt8)
							{
								writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.{instruction.InstructionType.ToString()}{instruction.OperandSize.ToString()}" +
									$"(this.oCPU.AX, {instruction.Parameters[1].ToSourceCSTextMZ(instruction.OperandSize)}," +
									$"{instruction.Parameters[2].ToSourceCSTextMZ(instruction.OperandSize)});");
							}
							else
							{
								writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.{instruction.InstructionType.ToString()}{instruction.OperandSize.ToString()}" +
									$"(this.oCPU.AX, this.oCPU.DX, {instruction.Parameters[1].ToSourceCSTextMZ(instruction.OperandSize)}, " +
									$"{instruction.Parameters[2].ToSourceCSTextMZ(instruction.OperandSize)});");
							}
							break;

						case CPUInstructionEnum.IMUL:
							parameter = instruction.Parameters[0];
							writer.Write($"{GetTabs(tabLevel + 1)}");

							if (parameter.Type == CPUParameterTypeEnum.Register)
							{
								if (parameter.RegisterValue == CPURegisterEnum.AX_DX)
								{
									writer.WriteLine($"this.oCPU.{instruction.InstructionType.ToString()}{instruction.OperandSize.ToString()}(" +
										"this.oCPU.AX, this.oCPU.DX, " +
										$"{instruction.Parameters[1].ToSourceCSTextMZ(instruction.OperandSize)}, " +
										$"{instruction.Parameters[2].ToSourceCSTextMZ(instruction.OperandSize)});");
								}
								else if (parameter.RegisterValue == CPURegisterEnum.AX)
								{
									writer.WriteLine($"this.oCPU.{instruction.InstructionType.ToString()}{instruction.OperandSize.ToString()}(" +
										"this.oCPU.AX, " +
										$"{instruction.Parameters[1].ToSourceCSTextMZ(instruction.OperandSize)}, " +
										$"{instruction.Parameters[2].ToSourceCSTextMZ(instruction.OperandSize)});");
								}
								else
								{
									writer.WriteLine($"this.oCPU.{instruction.InstructionType.ToString()}{instruction.OperandSize.ToString()}(" +
										$"this.oCPU.{parameter.RegisterValue.ToString()}, " +
										$"{instruction.Parameters[1].ToSourceCSTextMZ(instruction.OperandSize)}, " +
										$"{instruction.Parameters[2].ToSourceCSTextMZ(instruction.OperandSize)});");
								}
							}
							else
							{
								throw new Exception("Invalid IMUL instruction");
							}
							break;

						case CPUInstructionEnum.LDS:
							parameter = instruction.Parameters[1];
							writer.WriteLine($"{GetTabs(tabLevel + 1)}// LDS");
							writer.WriteLine($"{GetTabs(tabLevel + 1)}{instruction.Parameters[0].ToDestinationCSTextMZ(instruction.OperandSize, parameter.ToSourceCSTextMZ(instruction.OperandSize))}");
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.DS.{instruction.OperandSize.ToString()} = this.oCPU.Read{instruction.OperandSize.ToString()}" +
								$"({parameter.GetSegmentTextMZ()}, (ushort)({parameter.ToCSTextMZ(instruction.OperandSize)} + 2));");
							break;

						case CPUInstructionEnum.LES:
							parameter = instruction.Parameters[1];
							writer.WriteLine($"{GetTabs(tabLevel + 1)}// LES");
							writer.WriteLine($"{GetTabs(tabLevel + 1)}{instruction.Parameters[0].ToDestinationCSTextMZ(instruction.OperandSize, parameter.ToSourceCSTextMZ(instruction.OperandSize))}");
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.ES.{instruction.OperandSize.ToString()} = this.oCPU.Read{instruction.OperandSize.ToString()}" +
								$"({parameter.GetSegmentTextMZ()}, (ushort)({parameter.ToCSTextMZ(instruction.OperandSize)} + 2));");
							break;

						case CPUInstructionEnum.LEA:
							parameter = instruction.Parameters[1];
							writer.WriteLine($"{GetTabs(tabLevel + 1)}// LEA");
							writer.Write($"{GetTabs(tabLevel + 1)}{instruction.Parameters[0].ToDestinationCSTextMZ(instruction.OperandSize, parameter.ToCSTextMZ(instruction.OperandSize))}");
							if (parameter.ReferenceType != CPUParameterReferenceEnum.None)
							{
								writer.WriteLine(" // {0}", parameter.ReferenceType.ToString());
							}
							else
							{
								writer.WriteLine();
							}
							break;

						case CPUInstructionEnum.LODS:
							writer.Write($"{GetTabs(tabLevel + 1)}this.oCPU.");
							if (instruction.RepPrefix == CPUInstructionPrefixEnum.REPE || instruction.RepPrefix == CPUInstructionPrefixEnum.REPNE)
							{
								writer.WriteLine("{0}_LODS{1}();", instruction.RepPrefix.ToString(), instruction.OperandSize.ToString());
							}
							else
							{
								writer.WriteLine("LODS{0}();", instruction.OperandSize.ToString());
							}
							break;

						case CPUInstructionEnum.MOV:
							parameter = instruction.Parameters[1];
							writer.Write($"{GetTabs(tabLevel + 1)}");
							writer.Write(instruction.Parameters[0].ToDestinationCSTextMZ(instruction.OperandSize, parameter.ToSourceCSTextMZ(instruction.OperandSize)));
							if (parameter.ReferenceType != CPUParameterReferenceEnum.None)
							{
								writer.WriteLine(" // {0}", parameter.ReferenceType.ToString());
							}
							else
							{
								writer.WriteLine();
							}
							break;

						case CPUInstructionEnum.MOVS:
							writer.Write($"{GetTabs(tabLevel + 1)}this.oCPU.");
							if (instruction.RepPrefix == CPUInstructionPrefixEnum.REPE || instruction.RepPrefix == CPUInstructionPrefixEnum.REPNE)
							{
								writer.WriteLine("{0}_MOVS{1}({2}, this.oCPU.SI, this.oCPU.ES, this.oCPU.DI, this.oCPU.CX);",
									instruction.RepPrefix.ToString(), instruction.OperandSize.ToString(), instruction.GetDefaultDataSegmentTextMZ());
							}
							else
							{
								writer.WriteLine("MOVS{0}({1}, this.oCPU.SI, this.oCPU.ES, this.oCPU.DI);",
									instruction.OperandSize.ToString(), instruction.GetDefaultDataSegmentTextMZ());
							}
							break;

						/*case CPUInstructionEnum.MOVSX:
						case CPUInstructionEnum.MOVZX:
							parameter = asmInstruction.Parameters[0];
							writer.Write($"{GetTabs(tabLevel + 1)}");
							writer.WriteLine(parameter.ToDestinationCSTextMZ(asmInstruction.OperandSize, string.Format("this.oCPU.{0}{1}({2})",
								asmInstruction.InstructionType.ToString(), asmInstruction.OperandSize.ToString(),
								asmInstruction.Parameters[1].ToSourceCSTextMZ(asmInstruction.Parameters[1].Size))));
							break;*/

						case CPUInstructionEnum.NOP:
							// ignore this instruction
							break;

						case CPUInstructionEnum.WAIT:
							// ignore this instruction
							break;

						case CPUInstructionEnum.ENTER:
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.Enter();");
							break;

						case CPUInstructionEnum.LEAVE:
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.Leave();");
							break;

						// stack instructions
						case CPUInstructionEnum.POP:
							parameter = instruction.Parameters[0];
							/*Console.WriteLine("POP {0} in {1}.{2} ", instruction.Parameters[0].ToString(),
								segment.Namespace, function.Name);*/
							writer.Write($"{GetTabs(tabLevel + 1)}");
							writer.WriteLine(parameter.ToDestinationCSTextMZ(instruction.OperandSize, string.Format("this.oCPU.POP{0}()",
								instruction.OperandSize.ToString())));
							break;

						case CPUInstructionEnum.POPA:
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.POPA{instruction.OperandSize.ToString()}();");
							break;

						case CPUInstructionEnum.POPF:
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.POPFUInt16();");
							break;

						case CPUInstructionEnum.PUSH:
							parameter = instruction.Parameters[0];
							writer.Write($"{GetTabs(tabLevel + 1)}this.oCPU.PUSH{instruction.OperandSize.ToString()}({parameter.ToSourceCSTextMZ(instruction.OperandSize)});");
							if (parameter.ReferenceType != CPUParameterReferenceEnum.None)
							{
								writer.WriteLine(" // {0}", parameter.ReferenceType.ToString());
							}
							else
							{
								writer.WriteLine();
							}
							break;

						case CPUInstructionEnum.PUSHA:
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.PUSHA{instruction.OperandSize.ToString()}();");
							break;

						case CPUInstructionEnum.PUSHF:
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.PUSHFUInt16();");
							break;

						case CPUInstructionEnum.CLD:
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.Flags.D = false;");
							break;

						case CPUInstructionEnum.STD:
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.Flags.D = true;");
							break;

						case CPUInstructionEnum.CLC:
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.Flags.C = false;");
							break;

						case CPUInstructionEnum.STC:
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.Flags.C = true;");
							break;

						case CPUInstructionEnum.CMC:
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.Flags.C = !this.oCPU.Flags.C;");
							break;

						case CPUInstructionEnum.CLI:
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.CLI();");
							break;

						case CPUInstructionEnum.STI:
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.STI();");
							break;

						/*case CPUInstructionEnum.SCAS:
							writer.Write($"{GetTabs(tabLevel + 1)}this.oCPU.");
							if (asmInstruction.RepPrefix == CPUInstructionPrefixEnum.REPE || asmInstruction.RepPrefix == CPUInstructionPrefixEnum.REPNE)
							{
								writer.WriteLine("{0}_SCAS{1}();",
									asmInstruction.RepPrefix.ToString(), asmInstruction.OperandSize.ToString());
							}
							else
							{
								writer.WriteLine("SCAS{0}();", asmInstruction.OperandSize.ToString());
							}
							break;*/

						case CPUInstructionEnum.STOS:
							writer.Write($"{GetTabs(tabLevel + 1)}this.oCPU.");
							if (instruction.RepPrefix == CPUInstructionPrefixEnum.REPE || instruction.RepPrefix == CPUInstructionPrefixEnum.REPNE)
							{
								writer.WriteLine("{0}_STOS{1}();",
									instruction.RepPrefix.ToString(), instruction.OperandSize.ToString());
							}
							else
							{
								writer.WriteLine("STOS{0}();", instruction.OperandSize.ToString());
							}
							break;

						case CPUInstructionEnum.XCHG:
							parameter = instruction.Parameters[0];
							//writer.WriteLine("{GetTabs(tabLevel)}// XCHG");
							writer.Write($"{GetTabs(tabLevel + 1)}");
							writer.WriteLine("this.oCPU.Temp.{0} = {1};", instruction.OperandSize.ToString(), parameter.ToSourceCSTextMZ(instruction.OperandSize));
							writer.Write($"{GetTabs(tabLevel + 1)}");
							writer.WriteLine(parameter.ToDestinationCSTextMZ(instruction.OperandSize, instruction.Parameters[1].ToSourceCSTextMZ(instruction.OperandSize)));
							parameter = instruction.Parameters[1];
							writer.Write($"{GetTabs(tabLevel + 1)}");
							writer.WriteLine(parameter.ToDestinationCSTextMZ(instruction.OperandSize, string.Format("this.oCPU.Temp.{0}", instruction.OperandSize.ToString())));
							break;

						/*case CPUInstructionEnum.XLAT:
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.XLATUInt8({asmInstruction.GetDefaultDataSegmentTextMZ()});");
							break;*/

						// input and output port instructions
						case CPUInstructionEnum.IN:
							writer.Write($"{GetTabs(tabLevel + 1)}");
							writer.WriteLine("{0}",
								instruction.Parameters[0].ToDestinationCSTextMZ(instruction.Parameters[0].Size,
								string.Format("this.oCPU.IN{0}({1})",
								instruction.OperandSize.ToString(),
								instruction.Parameters[1].ToSourceCSTextMZ(instruction.Parameters[1].Size))));
							break;
						case CPUInstructionEnum.OUT:
							writer.Write($"{GetTabs(tabLevel + 1)}");
							writer.WriteLine("this.oCPU.OUT{0}({1}, {2});",
								instruction.OperandSize.ToString(),
								instruction.Parameters[0].ToSourceCSTextMZ(instruction.Parameters[0].Size),
								instruction.Parameters[1].ToSourceCSTextMZ(instruction.Parameters[1].Size));
							break;

						case CPUInstructionEnum.OUTS:
							writer.Write($"{GetTabs(tabLevel + 1)}this.oCPU.");
							if (instruction.RepPrefix == CPUInstructionPrefixEnum.REPE || instruction.RepPrefix == CPUInstructionPrefixEnum.REPNE)
							{
								writer.WriteLine("{0}_{1}{2}({3}, this.oCPU.SI, this.oCPU.CX);",
									instruction.RepPrefix.ToString(),
									instruction.InstructionType.ToString(), instruction.OperandSize.ToString(),
									instruction.GetDefaultDataSegmentTextMZ());
							}
							else
							{
								writer.WriteLine("{0}{1}({2}, this.oCPU.SI);",
									instruction.InstructionType.ToString(), instruction.OperandSize.ToString(),
									instruction.GetDefaultDataSegmentTextMZ());
							}
							break;

						// special syntetic functions
						case CPUInstructionEnum.WordsToDword:
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.{instruction.Parameters[0].RegisterValue.ToString()}.UInt32 = " +
								$"(uint)(((uint)this.oCPU.{instruction.Parameters[1].RegisterValue.ToString()}.UInt16 << 16) | " +
								$"(uint)this.oCPU.{instruction.Parameters[2].RegisterValue.ToString()}.UInt16);");
							break;

						// flow control instructions
						case CPUInstructionEnum.SWITCH:
							writer.WriteLine($"{GetTabs(tabLevel + 1)}switch({instruction.Parameters[0].ToSourceCSTextMZ(instruction.OperandSize)})");
							writer.WriteLine($"{GetTabs(tabLevel + 1)}{{");
							for (int l = 1; l < instruction.Parameters.Count; l++)
							{
								parameter = instruction.Parameters[l];
								instructionIndex = this.GetInstructionPositionByOffset((ushort)parameter.Displacement);

								if (instructionIndex < 0)
								{
									throw new Exception($"Can't find instruction in function {this.parentSegment.ToString()}.{this.Name} at offset 0x{parameter.Displacement:x4}");
								}
								else
								{
									writer.WriteLine($"{GetTabs(tabLevel + 2)}case {0}:", parameter.Value);
									writer.WriteLine($"{GetTabs(tabLevel + 3)}goto {this.asmInstructions[instructionIndex].LabelName};");
								}
							}
							writer.WriteLine($"{GetTabs(tabLevel + 1)}}}");
							break;

						case CPUInstructionEnum.Jcc:
							uiOffset = instruction.Parameters[1].Value;
							instructionIndex = this.GetInstructionPositionByOffset((ushort)uiOffset);

							if (instructionIndex < 0)
							{
								throw new Exception($"Can't find instruction in function {this.parentSegment.ToString()}.{this.Name} at offset 0x{uiOffset:x4}");
							}
							else
							{
								writer.WriteLine($"{GetTabs(tabLevel + 1)}if (this.oCPU.Flags.{((CPUJumpConditionEnum)instruction.Parameters[0].Value).ToString()}) goto " +
									$"{this.asmInstructions[instructionIndex].LabelName};");
							}
							break;

						case CPUInstructionEnum.JCXZ:
							uiOffset = (uint)instruction.Parameters[0].Value;
							instructionIndex = this.GetInstructionPositionByOffset((ushort)uiOffset);

							if (instructionIndex < 0)
							{
								throw new Exception($"Can't find instruction in function {this.parentSegment.ToString()}.{this.Name} at offset 0x{uiOffset:x4}");
							}
							else
							{
								writer.WriteLine($"{GetTabs(tabLevel + 1)}if (this.oCPU.CX.UInt16 == 0) goto {this.asmInstructions[instructionIndex].LabelName};");
							}
							break;

						case CPUInstructionEnum.LOOP:
							uiOffset = (uint)instruction.Parameters[0].Value;
							instructionIndex = this.GetInstructionPositionByOffset((ushort)uiOffset);

							if (instructionIndex < 0)
							{
								throw new Exception($"Can't find instruction in function {this.parentSegment.ToString()}.{this.Name} at offset 0x{uiOffset:x4}");
							}
							else
							{
								writer.WriteLine($"{GetTabs(tabLevel + 1)}if (this.oCPU.LoopUInt16(this.oCPU.CX)) goto {this.asmInstructions[instructionIndex].LabelName};");
							}
							break;

						case CPUInstructionEnum.JMP:
							parameter = instruction.Parameters[0];
							if (parameter.Type == CPUParameterTypeEnum.Immediate)
							{
								uiOffset = (uint)instruction.Parameters[0].Value;
								instructionIndex = this.GetInstructionPositionByOffset((ushort)uiOffset);

								if (instructionIndex < 0)
								{
									throw new Exception($"Can't find instruction in function {this.parentSegment.ToString()}.{this.Name} at offset 0x{uiOffset:x4}");
								}
								else
								{
									writer.WriteLine($"{GetTabs(tabLevel + 1)}goto {this.asmInstructions[instructionIndex].LabelName};");
								}
							}
							else if (parameter.Type == CPUParameterTypeEnum.Register)
							{
								writer.WriteLine($"{GetTabs(tabLevel + 1)}// Probably a switch statement - near jump to register value");
								writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.JmpUInt16({parameter.ToCSTextMZ(instruction.OperandSize)});");
							}
							else
							{
								writer.WriteLine($"{GetTabs(tabLevel + 1)}// Probably a switch statement - near jump to indirect address");
								writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.JmpUInt16(this.oCPU.ReadUInt16({parameter.GetSegmentTextMZ()}, {parameter.ToCSTextMZ(instruction.OperandSize)}));");
							}
							break;

						case CPUInstructionEnum.JMPF:
							parameter = instruction.Parameters[0];
							if (parameter.Type == CPUParameterTypeEnum.SegmentOffset)
							{
								uiOffset = MainProgram.ToLinearAddress(parameter.Segment, parameter.Value);
								if (uiOffset == 0)
								{
									writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.JmpUInt32(this.oCPU.ReadUInt32(this.oCPU.CS.UInt16, 0x{(instruction.Offset + 1):x}));");
								}
								else
								{
									instructionIndex = this.GetInstructionPositionByLinearAddress(uiOffset);

									if (instructionIndex < 0)
									{
										throw new Exception($"Can't find instruction in function {this.parentSegment.ToString()}.{this.Name} at 0x{parameter.Segment:x}:0x{parameter.Value:x}");
									}
									else
									{
										writer.WriteLine($"{GetTabs(tabLevel + 1)}goto {this.asmInstructions[instructionIndex].LabelName};");
									}
								}
							}
							else
							{
								writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.JmpUInt32(this.oCPU.ReadUInt32({parameter.GetSegmentTextMZ()}, {parameter.ToCSTextMZ(instruction.OperandSize)}));");
							}
							break;

						case CPUInstructionEnum.CALL:
							parameter = instruction.Parameters[0];
							if (verbosity > 0)
							{
								writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.PushUInt16(0x{instruction.Offset + instruction.Bytes.Count:x4}); // stack management - push return offset");
								writer.WriteLine($"{GetTabs(tabLevel + 1)}// Instruction address 0x{instruction.Segment:x4}:0x{instruction.Offset:x4}, size: {instruction.Bytes.Count}");
							}
							else
							{
								writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.PushUInt16(0); // stack management - push return offset");
							}

							if (parameter.Type != CPUParameterTypeEnum.Immediate)
							{
								writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.CallUInt16(this.oCPU.ReadUInt16({parameter.GetSegmentTextMZ()}, {parameter.ToCSTextMZ(instruction.OperandSize)}));");
							}
							else
							{
								function1 = this.parentSegment.Parent.FindFunction(0, instruction.Segment, (ushort)parameter.Value);

								if (function1 != null)
								{
									if ((function1.CallType & ProgramFunctionTypeEnum.Near) != ProgramFunctionTypeEnum.Near &&
										(function1.CallType & ProgramFunctionTypeEnum.Far) == ProgramFunctionTypeEnum.Far)
									{
										Console.WriteLine($"Function '{function1.Name}' doesn't support near return");
									}

									if (this.parentSegment.Segment != function1.Segment.Segment)
									{
										if ((this.callType & ProgramFunctionTypeEnum.CAPI) == ProgramFunctionTypeEnum.CAPI)
										{
											writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oParent.MSCAPI.{function1.Name}();");
										}
										else
										{
											writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oParent.{function1.Segment.ToString()}.{function1.Name}();");
										}
									}
									else
									{
										if (((this.callType & ProgramFunctionTypeEnum.CAPI) == ProgramFunctionTypeEnum.CAPI &&
											(function1.CallType & ProgramFunctionTypeEnum.CAPI) != ProgramFunctionTypeEnum.CAPI) ||
											((this.callType & ProgramFunctionTypeEnum.CAPI) != ProgramFunctionTypeEnum.CAPI &&
											(function1.CallType & ProgramFunctionTypeEnum.CAPI) == ProgramFunctionTypeEnum.CAPI))
										{
											if ((function1.CallType & ProgramFunctionTypeEnum.CAPI) == ProgramFunctionTypeEnum.CAPI)
											{
												writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oParent.MSCAPI.{function1.Name}();");
											}
											else
											{
												writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oParent.{function1.Segment.ToString()}.{function1.Name}();");
											}
										}
										else
										{
											writer.WriteLine($"{GetTabs(tabLevel + 1)}{function1.Name}();");
										}
									}
								}
								else
								{
									throw new Exception($"Can't find function 'F0_{parameter.Segment:x4}_{parameter.Value:x4}'");
								}
							}
							// all calls in medium memory model are far
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.PopUInt16(); // stack management - pop return offset");
							//writer.WriteLine("{GetTabs(tabLevel)+1}this.oCPU.CS.UInt16 = 0x{0:x4}; // restore this function segment", function.Segment);
							break;

						case CPUInstructionEnum.CALLF:
							parameter = instruction.Parameters[0];
							if (verbosity > 0)
							{
								writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.PushUInt16(this.oCPU.CS.UInt16); // stack management - push return segment");
								writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.PushUInt16(0x{instruction.Offset + instruction.Bytes.Count:x4}); // stack management - push return offset");
								writer.WriteLine($"{GetTabs(tabLevel + 1)}// Instruction address 0x{instruction.Segment:x4}:0x{instruction.Offset:x4}, size: {instruction.Bytes.Count}");
							}
							else
							{
								writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.PushUInt32(0); // stack management - push return segment, offset");
							}

							if (parameter.Type == CPUParameterTypeEnum.SegmentOffset)
							{
								function1 = this.parentSegment.Parent.FindFunction(0, parameter.Segment, (ushort)parameter.Value);
								if (function1 != null)
								{
									if ((function1.CallType & ProgramFunctionTypeEnum.Far) != ProgramFunctionTypeEnum.Far &&
										(function1.CallType & ProgramFunctionTypeEnum.Near) == ProgramFunctionTypeEnum.Near)
									{
										Console.WriteLine($"Function '{function1.Name}' doesn't support far return");
									}

									if (this.parentSegment.Segment != function1.Segment.Segment)
									{
										if ((function1.CallType & ProgramFunctionTypeEnum.CAPI) == ProgramFunctionTypeEnum.CAPI)
										{
											writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oParent.MSCAPI.{function1.Name}();");
										}
										else
										{
											writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oParent.{function1.Segment.ToString()}.{function1.Name}();");
										}
									}
									else
									{
										if (((this.callType & ProgramFunctionTypeEnum.CAPI) == ProgramFunctionTypeEnum.CAPI &&
											(function1.CallType & ProgramFunctionTypeEnum.CAPI) != ProgramFunctionTypeEnum.CAPI) ||
											((this.callType & ProgramFunctionTypeEnum.CAPI) != ProgramFunctionTypeEnum.CAPI &&
											(function1.CallType & ProgramFunctionTypeEnum.CAPI) == ProgramFunctionTypeEnum.CAPI))
										{
											if ((function1.CallType & ProgramFunctionTypeEnum.CAPI) == ProgramFunctionTypeEnum.CAPI)
											{
												writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oParent.MSCAPI.{function1.Name}();");
											}
											else
											{
												writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oParent.{function1.Segment.ToString()}.{function1.Name}();");
											}
										}
										else
										{
											writer.WriteLine($"{GetTabs(tabLevel + 1)}{function1.Name}();");
										}
									}
								}
								else
								{
									throw new Exception($"Can't find function 'F0_{parameter.Segment:x4}_{parameter.Value:x4}'");
								}
							}
							else
							{
								writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.CallUInt32(this.oCPU.ReadUInt32({parameter.GetSegmentTextMZ()}, {parameter.ToCSTextMZ(instruction.OperandSize)}));");
							}
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.PopUInt32(); // stack management - pop return offset and segment");
							if (verbosity > 0)
							{
								writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.CS.UInt16 = 0x{this.parentSegment.CPUSegment:x4}; // restore this function segment");
							}
							break;

						case CPUInstructionEnum.CallOverlay:
							writer.WriteLine($"{GetTabs(tabLevel + 1)}// Call to overlay");
							if (verbosity > 0)
							{
								writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.PushUInt16(this.oCPU.CS.UInt16); // stack management - push return segment");
								writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.PushUInt16(0x{instruction.Offset + instruction.Bytes.Count:x4}); // stack management - push return offset");
							}
							else
							{
								writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.PushUInt32(0); // stack management - push return segment, offset");
							}

							function1 = this.parentSegment.Parent.FindFunction((ushort)instruction.Parameters[0].Value, 0, (ushort)instruction.Parameters[1].Value);
							if (function1 != null)
							{
								if ((function1.CallType & ProgramFunctionTypeEnum.Far) != ProgramFunctionTypeEnum.Far && (function1.CallType & ProgramFunctionTypeEnum.Near) == ProgramFunctionTypeEnum.Near)
								{
									Console.WriteLine($"Function '{function1.Name}' doesn't support far return");
								}

								if (this.parentSegment != function1.Segment)
								{
									if ((function1.CallType & ProgramFunctionTypeEnum.CAPI) == ProgramFunctionTypeEnum.CAPI)
									{
										writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oParent.MSCAPI.{function1.Name}();");
									}
									else
									{
										writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oParent.{function1.Segment.ToString()}.{function1.Name}();");
									}
								}
								else
								{
									writer.WriteLine($"{GetTabs(tabLevel + 1)}{function1.Name}();");
								}
							}
							else
							{
								throw new Exception($"Can't find function 'F{instruction.Parameters[0].Value}_0000_{instruction.Parameters[1].Value:x4}'");
							}

							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.PopUInt32(); // stack management - pop return offset and segment");
							if (verbosity > 0)
							{
								writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.CS.UInt16 = 0x{this.parentSegment.CPUSegment:x4}; // restore this function segment");
							}
							break;

						case CPUInstructionEnum.RET:
							writer.WriteLine();
							writer.WriteLine($"{GetTabs(tabLevel + 1)}// Near return");
							if (instruction.Parameters.Count > 0)
							{
								writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.SP.UInt16 = this.oCPU.ADDUInt16(this.oCPU.SP.UInt16, 0x{instruction.Parameters[0].Value:x});");
							}
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.Log.ExitBlock(\"'{this.Name}'\");");
							if (k != this.asmInstructions.Count - 1)
							{
								writer.WriteLine($"{GetTabs(tabLevel + 1)}return;");
							}
							retInstruction = true;
							break;

						case CPUInstructionEnum.RETF:
							writer.WriteLine();
							writer.WriteLine($"{GetTabs(tabLevel + 1)}// Far return");
							if (instruction.Parameters.Count > 0)
							{
								writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.SP.UInt16 = this.oCPU.ADDUInt16(this.oCPU.SP.UInt16, 0x{instruction.Parameters[0].Value:x});");
							}
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.Log.ExitBlock(\"'{this.Name}'\");");
							if (k != this.asmInstructions.Count - 1)
							{
								writer.WriteLine($"{GetTabs(tabLevel + 1)}return;");
							}
							retInstruction = true;
							break;

						case CPUInstructionEnum.IRET:
							writer.WriteLine();
							writer.WriteLine($"{GetTabs(tabLevel + 1)}// IRET - Pop flags and Far return");
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.Log.ExitBlock(\"'{this.Name}'\");");
							if (k != this.asmInstructions.Count - 1)
							{
								writer.WriteLine($"{GetTabs(tabLevel + 1)}return;");
							}
							retInstruction = true;
							break;

						case CPUInstructionEnum.INT:
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.INT(0x{instruction.Parameters[0].Value:x2});");
							break;

						default:
							//throw new Exception($"Unexpected instruction type: {instruction.InstructionType}");
							Console.WriteLine($"Unexpected instruction type '{instruction.InstructionType}' " +
								$"in function {this.parentSegment.ToString()}.{this.Name} at offset 0x{instruction.Offset:x4}");
							break;
					}
				}
				if (!retInstruction)
				{
					writer.WriteLine();
					writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.Log.ExitBlock(\"'{this.Name}'\");");
				}
			}

			writer.WriteLine($"{GetTabs(tabLevel)}}}");
		}

		public static string ConditionToCSText(CPUJumpConditionEnum condition)
		{
			switch (condition)
			{
				case CPUJumpConditionEnum.B:
					return "<";
				case CPUJumpConditionEnum.AE:
					return ">=";
				case CPUJumpConditionEnum.E:
					return "==";
				case CPUJumpConditionEnum.NE:
					return "!=";
				case CPUJumpConditionEnum.BE:
					return "<=";
				case CPUJumpConditionEnum.A:
					return ">";
				case CPUJumpConditionEnum.L:
					return "<";
				case CPUJumpConditionEnum.GE:
					return ">=";
				case CPUJumpConditionEnum.LE:
					return "<=";
				case CPUJumpConditionEnum.G:
					return ">";
			}

			return "!!!";
		}

		private string GetTabs(int tabLevel)
		{
			StringWriter tabs = new StringWriter();

			for (int i = 0; i < tabLevel; i++)
			{
				tabs.Write("\t");
			}

			return tabs.ToString();
		}

		public ProgramSegment Segment { get => this.parentSegment; }

		public int Ordinal { get => this.ordinal; set => this.ordinal = value; }

		public ushort FunctionOffset { get => this.fnOffset; }

		public uint FunctionEntryPoint { get => this.fnEntryPoint; }

		public string Name
		{
			get 
			{
				if (string.IsNullOrEmpty(this.name))
				{
					if (this.ordinal >= 0)
					{
						return $"Fn{this.ordinal}";
					}
					else
					{
						return $"Fn_{this.fnOffset:x4}";
					}
				}

				return this.name;
			}
		}

		public bool IsLibraryFunction { get => this.isLibraryFunction; set => this.isLibraryFunction = value; }

		public ProgramFunctionTypeEnum CallType { get => this.callType; }

		public BDictionary<int, ILVariable> Parameters { get => this.localParameters; }

		public int ParameterSize
		{
			get
			{
				int parameterSize = 0;

				for (int i = 0; i < this.localParameters.Count; i++)
				{
					ILVariable parameter = this.localParameters[i].Value;

					switch (parameter.Type)
					{
						case ILValueTypeEnum.UInt8:
						case ILValueTypeEnum.Int8:
							parameterSize += 2;
							break;

						case ILValueTypeEnum.UInt16:
						case ILValueTypeEnum.Int16:
						case ILValueTypeEnum.Ptr16:
							parameterSize += 2;
							break;

						case ILValueTypeEnum.UInt32:
						case ILValueTypeEnum.Int32:
						case ILValueTypeEnum.Ptr32:
							parameterSize += 4;
							break;
					}
				}

				return parameterSize;
			}
		}

		public BDictionary<int, ILVariable> Variables { get => this.localVariables; }

		public int LocalVariableSize { get => this.localVariableSize; set => this.localVariableSize = value; }

		public int LocalVariablePosition { get => this.localVariablePosition; set => this.localVariablePosition = value; }

		public CPUParameterSizeEnum ReturnType { get => this.returnType; set => this.returnType = value; }

		public int LocalStackSize { get => this.stackSize; }

		public FlowGraph? FlowGraph { get => this.flowGraph; }

		public List<CPUInstruction> AsmInstructions { get => this.asmInstructions; }
	}
}
