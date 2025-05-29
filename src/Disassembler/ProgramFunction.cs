using Disassembler.CPU;
using IRB.Collections.Generic;

namespace Disassembler
{
	public class ProgramFunction
	{
		private ProgramSegment parent;

		private int ordinal = -1;
		private ushort fnOffset;
		private uint fnEntryPoint;
		private string? name;
		private ProgramFunctionTypeEnum callType = ProgramFunctionTypeEnum.Cdecl;
		private CPUParameterSizeEnum returnType = CPUParameterSizeEnum.Undefined;
		private int stackSize = 0;
		private BDictionary<int, ILVariable> localParameters = new BDictionary<int, ILVariable>();
		private BDictionary<int, ILVariable> localVariables = new BDictionary<int, ILVariable>();

		private FlowGraph? flowGraph = null;

		// Assembly instructions
		private List<CPUInstruction> asmInstructions = new List<CPUInstruction>();

		// IL Instructions
		private List<ILExpression> ilInstructions = new List<ILExpression>();

		public ProgramFunction(ProgramSegment parent, ushort offset, string? name)
		{
			this.parent = parent;
			this.fnOffset = offset;
			this.fnEntryPoint = MainProgram.ToLinearAddress(parent.CPUSegment, offset);
			this.name = name;
		}

		public void Disassemble()
		{
			this.asmInstructions.Clear();
			uint fnLinearAddress = this.fnEntryPoint;

			#region Process function assembly instructions, including possible switch instructions
			uint fnSegment = MainProgram.ToLinearAddress(this.parent.CPUSegment, 0);
			List<uint> aJumps = new();
			List<uint> aSwitches = new();
			byte[] exeData;
			uint fnAddress = this.fnEntryPoint;

			if (this.parent.CPUOverlay > 0)
			{
				exeData = this.parent.Parent.Executable.Overlays[this.parent.CPUOverlay - 1].Data;
			}
			else
			{
				exeData = this.parent.Parent.Executable.Data;
			}

			if (fnAddress >= exeData.Length)
			{
				throw new Exception($"Trying to disassemble outside of executable range in function {this.parent.ToString()}.{this.Name}, 0x{fnAddress:x}");
			}

			MemoryStream stream = new(exeData);

			stream.Seek(fnAddress, SeekOrigin.Begin);

			while (true)
			{
				CPUInstruction instruction1;
				bool bEnd = false;

				if (fnAddress >= stream.Length)
				{
					throw new Exception($"Trying to disassemble outside of executable range in function {this.parent.ToString()}.{this.Name}, 0x{fnAddress:x}");
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
					CPUInstruction instruction = new CPUInstruction(this.parent.CPUSegment, (ushort)(fnAddress - fnSegment), stream);

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
								throw new Exception($"Relative jump to {parameter.ToString()} in function {this.parent.ToString()}.{this.Name} " +
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
								Console.WriteLine($"Jump to computed address {parameter.ToString()} in function {this.parent.ToString()}.{this.Name} " +
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
									this.parent.GlobalVariables.Add((int)(instruction.Offset + 1), new ILVariable(this, ILVariableScopeEnum.Global,
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
								Console.WriteLine($"Jump to {parameter.ToString()} in function {this.parent.ToString()}.{this.Name} " +
									$"(Instruction at 0x{instruction.LinearAddress:x})");
								// treat this as end of a instruction stream
								bEnd = true;
							}
							break;

						case CPUInstructionEnum.Jcc:
							parameter = instruction.Parameters[1];
							if (parameter.Type != CPUParameterTypeEnum.Immediate)
								throw new Exception(
									$"Relative offset expected, but got indirect parameter {parameter.ToString()} in function {this.parent.ToString()}.{this.Name} " +
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
									$"Relative offset expected, but got indirect parameter {parameter.ToString()} in function {this.parent.ToString()}.{this.Name} " +
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
								Console.WriteLine($"Inconsistent function return type in {this.parent.ToString()}.{this.Name}");
							}
							this.callType |= ProgramFunctionTypeEnum.Near;

							if (instruction.Parameters.Count == 1 && (this.callType & ProgramFunctionTypeEnum.Cdecl) == ProgramFunctionTypeEnum.Cdecl)
							{
								Console.WriteLine($"Inconsistent function call type in {this.parent.ToString()}.{this.Name}");
							}
							else if (instruction.Parameters.Count == 0 && (this.callType & ProgramFunctionTypeEnum.Pascal) == ProgramFunctionTypeEnum.Pascal)
							{
								Console.WriteLine($"Inconsistent function call type in {this.parent.ToString()}.{this.Name}");
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
								Console.WriteLine($"Inconsistent function return type in {this.parent.ToString()}.{this.Name}");
							}
							this.callType |= ProgramFunctionTypeEnum.Far;

							if (instruction.Parameters.Count == 1 && (this.callType & ProgramFunctionTypeEnum.Cdecl) == ProgramFunctionTypeEnum.Cdecl)
							{
								Console.WriteLine($"Inconsistent function call type in {this.parent.ToString()}.{this.Name}");
							}
							else if (instruction.Parameters.Count == 0 && (this.callType & ProgramFunctionTypeEnum.Pascal) == ProgramFunctionTypeEnum.Pascal)
							{
								Console.WriteLine($"Inconsistent function call type in {this.parent.ToString()}.{this.Name}");
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
								Console.WriteLine($"Inconsistent function return type in {this.parent.ToString()}.{this.Name}");
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
									Console.WriteLine($"Undefined switch pattern {instruction1.Parameters[0].ToString()} in function {this.parent.ToString()}.{this.Name} " +
										$"(Instruction at 0x{instruction1.LinearAddress:x})");
									//break;
								}
							}
						}
						else
						{
							Console.WriteLine($"Can't find location of switch statement in function {this.parent.ToString()}.{this.Name} " +
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
					if (this.parent.CPUOverlay == 0)
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
						asmInstruction.Parameters.Add(new CPUParameter(CPUParameterTypeEnum.Immediate, CPUParameterSizeEnum.UInt8, this.parent.CPUOverlay));
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
										Console.WriteLine($"Stack parameter with register offset {parameter.ToString()} in function {this.parent.ToString()}.{this.Name} " +
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
							function = this.parent.Parent.FindFunction(0, this.parent.CPUSegment, (ushort)parameter.Value);
							if (function == null)
							{
								// function is not yet defined, define it
								this.parent.Parent.Disassemble(0, this.parent.CPUSegment, (ushort)parameter.Value, null);
							}
						}
						break;

					case CPUInstructionEnum.CALLF:
						parameter = instruction.Parameters[0];
						if (parameter.Type == CPUParameterTypeEnum.SegmentOffset)
						{
							function = this.parent.Parent.FindFunction(0, parameter.Segment, (ushort)parameter.Value);
							if (function == null)
							{
								// function is not yet defined, define it
								this.parent.Parent.Disassemble(0, parameter.Segment, (ushort)parameter.Value, null);
							}
						}
						break;

					case CPUInstructionEnum.CallOverlay:
						if (instruction.Parameters[0].Value == 0)
						{
							throw new Exception("Overlay manager references overlay 0");
						}

						function = this.parent.Parent.FindFunction((ushort)instruction.Parameters[0].Value, 0, (ushort)instruction.Parameters[1].Value);
						if (function == null)
						{
							// function is not yet defined, define it
							this.parent.Parent.Disassemble((ushort)instruction.Parameters[0].Value, 0, (ushort)instruction.Parameters[1].Value, null);
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

		/*public void Disassemble(MainProgram decompiler)
		{
			byte[] exeData;
			uint fnAddress;

			if (this.parent.CPUOverlay > 0)
			{
				exeData = this.parent.Parent.Executable.Overlays[this.parent.CPUOverlay - 1].Data;
				fnAddress = this.fnEntryPoint;
			}
			else
			{
				exeData = this.parent.Parent.Executable.Data;
				fnAddress = MainProgram.ToLinearAddress(this.parent.CPUSegment, this.fnEntryPoint);
			}

			if (fnAddress >= exeData.Length)
			{
				Console.WriteLine($"Trying to disassemble outside of executable range in function {this.parent.ToString()}.{this.Name}, 0x{this.fnEntryPoint:x4}");
				return;
			}

			MemoryStream stream = new MemoryStream(exeData);
			ushort fnIP = this.fnEntryPoint;
			List<ushort> aJumps = new List<ushort>();
			List<ushort> aSwitches = new List<ushort>();
			CPUInstruction instruction1;

			stream.Seek(fnAddress, SeekOrigin.Begin);

			#region Process function assembly instructions, including possible switch instructions
			while (true)
			{
				if (fnIP >= stream.Length)
				{
					throw new Exception($"Trying to disassemble outside of executable range in function {this.parent.ToString()}.{this.Name}, {this.parent.ToString()}, 0x{fnIP:x4}");
				}

				bool bEnd = false;
				for (int i = 0; i < this.asmInstructions.Count; i++)
				{
					if (this.asmInstructions[i].LinearAddress == MainProgram.ToLinearAddress(this.parent.CPUSegment, fnIP))
					{
						bEnd = true;
						break;
					}
				}

				if (!bEnd)
				{
					CPUInstructionParameter parameter;
					CPUInstruction instruction = new CPUInstruction(this.parent.CPUSegment, fnIP, stream);
					this.asmInstructions.Add(instruction);
					fnIP += (ushort)instruction.Bytes.Count;

					switch (instruction.InstructionType)
					{
						case CPUInstructionEnum.JMP:
							parameter = instruction.Parameters[0];
							if (parameter.Type == CPUInstructionParameterTypeEnum.Immediate)
							{
								fnIP = (ushort)parameter.Value;
								if (this.parent.CPUOverlay > 0)
								{
									stream.Seek(fnIP, SeekOrigin.Begin);
								}
								else
								{
									stream.Seek(MainProgram.ToLinearAddress(this.parent.CPUSegment, fnIP), SeekOrigin.Begin);
								}
							}
							else if (parameter.Type == CPUInstructionParameterTypeEnum.MemoryAddress && parameter.Value == 6)
							{
								throw new Exception($"Relative jmp to {parameter.ToString()} at function {this.parent.ToString()}.{this.Name} " +
									$"(Instruction at offset 0x{instruction.Offset:x}");
							}
							else if (parameter.Type == CPUInstructionParameterTypeEnum.MemoryAddress)
							{
								// probably switch statement
								aSwitches.Add(instruction.Offset);
								//Console.WriteLine($"Switch statement {parameter.ToString()} in function {this.sName} - instruction at 0x{this.usSegment:x4}:0x{instruction.Offset:x4}");
								bEnd = true;
							}
							else
							{
								Console.WriteLine($"Jump to relative address {parameter.ToString()} in function {this.parent.ToString()}.{this.Name} " +
									$"(Instruction at offset 0x{instruction.Offset:x4})");
								// treat this as end of a instruction stream
								bEnd = true;
							}
							break;

						case CPUInstructionEnum.JMPF:
							bEnd = true;
							break;

						case CPUInstructionEnum.Jcc:
							parameter = instruction.Parameters[1];
							if (parameter.Type != CPUInstructionParameterTypeEnum.Immediate)
								throw new Exception(
									$"Relative address offset expected, but got indirect {parameter.ToString()} in function {this.parent.ToString()}.{this.Name} " +
									$"(Instruction at offset 0x{instruction.Offset:x4})");

							aJumps.Add((ushort)parameter.Value);
							break;

						case CPUInstructionEnum.LOOP:
						case CPUInstructionEnum.LOOPNZ:
						case CPUInstructionEnum.LOOPZ:
						case CPUInstructionEnum.JCXZ:
							parameter = instruction.Parameters[0];
							if (parameter.Type != CPUInstructionParameterTypeEnum.Immediate)
								throw new Exception(
									$"Relative adress offset expected, but got indirect {parameter.ToString()} in function {this.parent.ToString()}.{this.Name} " +
									$"(Instruction at offset 0x{instruction.Offset:x4})");

							aJumps.Add((ushort)parameter.Value);
							break;

						case CPUInstructionEnum.INT:
							if (instruction.Parameters[0].Type == CPUInstructionParameterTypeEnum.Immediate &&
								instruction.Parameters[0].Value == 0x20)
							{
								// exit application instruction
								bEnd = true;
							}
							else if (instruction.Parameters[0].Type == CPUInstructionParameterTypeEnum.Immediate &&
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
								instruction.Parameters.Add(new CPUInstructionParameter(CPUInstructionParameterTypeEnum.Immediate, CPUParameterSizeEnum.UInt8, (byte)(byte0 & 0xff)));
								instruction.Parameters.Add(new CPUInstructionParameter(CPUInstructionParameterTypeEnum.Immediate, CPUParameterSizeEnum.UInt16, (ushort)((byte1 & 0xff) | ((byte2 & 0xff) << 8))));
								fnIP += 3;
							}
							else if (asmInstructions.Count > 1)
							{
								instruction1 = asmInstructions[asmInstructions.Count - 2];
								if (instruction1.Parameters.Count > 1 &&
									instruction1.Parameters[0].Type == CPUInstructionParameterTypeEnum.Register &&
									instruction1.Parameters[0].Size == CPUParameterSizeEnum.UInt8 &&
									instruction1.Parameters[0].Value == (uint)CPURegisterEnum.AH &&
									instruction1.Parameters[1].Type == CPUInstructionParameterTypeEnum.Immediate &&
									instruction1.Parameters[1].Value == 0x4c)
								{
									// exit application instruction
									bEnd = true;
								}
								else if (instruction1.Parameters.Count > 1 &&
									instruction1.Parameters[0].Type == CPUInstructionParameterTypeEnum.Register &&
									instruction1.Parameters[0].Size == CPUParameterSizeEnum.UInt16 &&
									instruction1.Parameters[0].Value == (uint)(((uint)CPURegisterEnum.AX) & 0x7) &&
									instruction1.Parameters[1].Type == CPUInstructionParameterTypeEnum.Immediate &&
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
								Console.WriteLine($"Inconsistent function return type in {this.parent.ToString()}.{this.Name}");
							}
							this.callType |= ProgramFunctionTypeEnum.Near;

							if (instruction.Parameters.Count == 1 && (this.callType & ProgramFunctionTypeEnum.Cdecl) == ProgramFunctionTypeEnum.Cdecl)
							{
								Console.WriteLine($"Inconsistent function call type in {this.parent.ToString()}.{this.Name}");
							}
							else if (instruction.Parameters.Count == 0 && (this.callType & ProgramFunctionTypeEnum.Pascal) == ProgramFunctionTypeEnum.Pascal)
							{
								Console.WriteLine($"Inconsistent function call type in {this.parent.ToString()}.{this.Name}");
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
								Console.WriteLine($"Inconsistent function return type in {this.parent.ToString()}.{this.Name}");
							}
							this.callType |= ProgramFunctionTypeEnum.Far;

							if (instruction.Parameters.Count == 1 && (this.callType & ProgramFunctionTypeEnum.Cdecl) == ProgramFunctionTypeEnum.Cdecl)
							{
								Console.WriteLine($"Inconsistent function call type in {this.parent.ToString()}.{this.Name}");
							}
							else if (instruction.Parameters.Count == 0 && (this.callType & ProgramFunctionTypeEnum.Pascal) == ProgramFunctionTypeEnum.Pascal)
							{
								Console.WriteLine($"Inconsistent function call type in {this.parent.ToString()}.{this.Name}");
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
								Console.WriteLine($"Inconsistent function return type in {this.parent.ToString()}.{this.Name}");
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
						fnIP = aJumps[aJumps.Count - 1];
						aJumps.RemoveAt(aJumps.Count - 1);
						if (this.parent.CPUOverlay > 0)
						{
							stream.Seek(fnIP, SeekOrigin.Begin);
						}
						else
						{
							stream.Seek(MainProgram.ToLinearAddress(this.parent.CPUSegment, fnIP), SeekOrigin.Begin);
						}

						continue;
					}

					// switches
					if (aSwitches.Count > 0)
					{
						// sort instructions by address before doing switches
						this.asmInstructions.Sort(CPUInstruction.CompareInstructionByAddress);

						fnIP = aSwitches[aSwitches.Count - 1];
						aSwitches.RemoveAt(aSwitches.Count - 1);

						int iPos = GetInstructionPositionByOffset(fnIP);
						if (iPos >= 0)
						{
							if (iPos > 5)
							{
								int iPos1;
								CPUInstructionParameter parameter;
								uint uiCount = 0;
								ushort usSwitchOffset = 0;

								// first pattern
								if ((instruction1 = this.asmInstructions[iPos1 = iPos - 5]).InstructionType == CPUInstructionEnum.CMP &&
									instruction1.Parameters.Count == 2 &&
									instruction1.Parameters[0].Type == CPUInstructionParameterTypeEnum.Register &&
									(parameter = instruction1.Parameters[0]).Value == ((uint)CPURegisterEnum.AX & 0x7) &&
									instruction1.Parameters[1].Type == CPUInstructionParameterTypeEnum.Immediate &&
									(uiCount = instruction1.Parameters[1].Value) >= 0 &&

									(instruction1 = this.asmInstructions[++iPos1]).InstructionType == CPUInstructionEnum.Jcc &&
									instruction1.Parameters.Count == 2 &&
									instruction1.Parameters[0].Type == CPUInstructionParameterTypeEnum.Condition &&
									instruction1.Parameters[0].Value == (uint)CPUJumpConditionEnum.BE &&
									instruction1.Parameters[1].Type == CPUInstructionParameterTypeEnum.Immediate &&

									(instruction1 = this.asmInstructions[++iPos1]).InstructionType == CPUInstructionEnum.JMP &&
									instruction1.Parameters.Count == 1 &&
									instruction1.Parameters[0].Type == CPUInstructionParameterTypeEnum.Immediate &&

									(instruction1 = this.asmInstructions[++iPos1]).InstructionType == CPUInstructionEnum.ADD &&
									instruction1.Parameters.Count == 2 &&
									instruction1.Parameters[0].Type == CPUInstructionParameterTypeEnum.Register &&
									(parameter = instruction1.Parameters[0]).Value == ((uint)CPURegisterEnum.AX & 0x7) &&
									instruction1.Parameters[1].Type == CPUInstructionParameterTypeEnum.Register &&
									instruction1.Parameters[1].Value == instruction1.Parameters[0].Value &&

									(instruction1 = this.asmInstructions[++iPos1]).InstructionType == CPUInstructionEnum.XCHG &&
									instruction1.Parameters.Count == 2 &&
									instruction1.Parameters[0].Type == CPUInstructionParameterTypeEnum.Register &&
									instruction1.Parameters[0].Value == ((uint)CPURegisterEnum.AX & 0x7) &&
									instruction1.Parameters[1].Type == CPUInstructionParameterTypeEnum.Register &&
									instruction1.Parameters[1].Value == ((uint)CPURegisterEnum.BX & 0x7) &&

									(instruction1 = this.asmInstructions[++iPos1]).InstructionType == CPUInstructionEnum.JMP &&
									instruction1.Parameters.Count == 1 &&
									instruction1.Parameters[0].Type == CPUInstructionParameterTypeEnum.MemoryAddress &&
									(usSwitchOffset = (ushort)instruction1.Parameters[0].Displacement) >= 0 &&

									iPos == iPos1
									)
								{
									//Console.WriteLine("Switch type 1 at {0}:0x{1:x4}", this.uiSegment, this.aInstructions[iPos].Location.Offset);

									this.asmInstructions[iPos - 2].InstructionType = CPUInstructionEnum.NOP;
									this.asmInstructions[iPos - 1].InstructionType = CPUInstructionEnum.NOP;
									instruction1 = this.asmInstructions[iPos];
									instruction1.InstructionType = CPUInstructionEnum.SWITCH;
									instruction1.Parameters.Clear();

									// switching parameter
									instruction1.Parameters.Add(parameter);

									if (this.parent.CPUOverlay > 0)
									{
										stream.Seek(usSwitchOffset, SeekOrigin.Begin);
									}
									else
									{
										stream.Seek(MainProgram.ToLinearAddress(this.parent.CPUSegment, usSwitchOffset), SeekOrigin.Begin);
									}

									// values and offsets
									for (int i = 0; i <= uiCount; i++)
									{
										ushort usWord = ReadWord(stream);
										aJumps.Add(usWord);
										instruction1.Parameters.Add(new CPUInstructionParameter(CPUInstructionParameterTypeEnum.Immediate, 
											CPUParameterSizeEnum.UInt16, (uint)i, (int)usWord));
									}

									fnIP = aJumps[aJumps.Count - 1];
									aJumps.RemoveAt(aJumps.Count - 1);

									if (this.parent.CPUOverlay > 0)
									{
										stream.Seek(fnIP, SeekOrigin.Begin);
									}
									else
									{
										stream.Seek(MainProgram.ToLinearAddress(this.parent.CPUSegment, fnIP), SeekOrigin.Begin);
									}
								}
								else if ((instruction1 = this.asmInstructions[iPos1 = iPos - 5]).InstructionType == CPUInstructionEnum.SUB &&
									instruction1.Parameters.Count == 2 &&
									instruction1.Parameters[0].Type == CPUInstructionParameterTypeEnum.Register &&
									instruction1.Parameters[0].Value == ((uint)CPURegisterEnum.AX & 0x7) &&
									instruction1.Parameters[1].Type == CPUInstructionParameterTypeEnum.Immediate &&

									(instruction1 = this.asmInstructions[++iPos1]).InstructionType == CPUInstructionEnum.CMP &&
									instruction1.Parameters.Count == 2 &&
									instruction1.Parameters[0].Type == CPUInstructionParameterTypeEnum.Register &&
									(parameter = instruction1.Parameters[0]).Value == ((uint)CPURegisterEnum.AX & 0x7) &&
									instruction1.Parameters[1].Type == CPUInstructionParameterTypeEnum.Immediate &&
									(uiCount = instruction1.Parameters[1].Value) >= 0 &&

									(instruction1 = this.asmInstructions[++iPos1]).InstructionType == CPUInstructionEnum.Jcc &&
									instruction1.Parameters.Count == 2 &&
									instruction1.Parameters[0].Type == CPUInstructionParameterTypeEnum.Condition &&
									instruction1.Parameters[0].Value == (uint)CPUJumpConditionEnum.A &&
									instruction1.Parameters[1].Type == CPUInstructionParameterTypeEnum.Immediate &&

									(instruction1 = this.asmInstructions[++iPos1]).InstructionType == CPUInstructionEnum.ADD &&
									instruction1.Parameters.Count == 2 &&
									instruction1.Parameters[0].Type == CPUInstructionParameterTypeEnum.Register &&
									(parameter = instruction1.Parameters[0]).Value == ((uint)CPURegisterEnum.AX & 0x7) &&
									instruction1.Parameters[1].Type == CPUInstructionParameterTypeEnum.Register &&
									instruction1.Parameters[1].Value == instruction1.Parameters[0].Value &&

									(instruction1 = this.asmInstructions[++iPos1]).InstructionType == CPUInstructionEnum.XCHG &&
									instruction1.Parameters.Count == 2 &&
									instruction1.Parameters[0].Type == CPUInstructionParameterTypeEnum.Register &&
									instruction1.Parameters[0].Value == ((uint)CPURegisterEnum.AX & 0x7) &&
									instruction1.Parameters[1].Type == CPUInstructionParameterTypeEnum.Register &&
									instruction1.Parameters[1].Value == ((uint)CPURegisterEnum.BX & 0x7) &&

									(instruction1 = this.asmInstructions[++iPos1]).InstructionType == CPUInstructionEnum.JMP &&
									instruction1.Parameters.Count == 1 &&
									instruction1.Parameters[0].Type == CPUInstructionParameterTypeEnum.MemoryAddress &&
									(usSwitchOffset = (ushort)instruction1.Parameters[0].Displacement) >= 0 &&

									iPos == iPos1
									)
								{
									//Console.WriteLine("Switch type 1 at {0}:0x{1:x4}", this.uiSegment, this.aInstructions[iPos].Location.Offset);

									this.asmInstructions[iPos - 2].InstructionType = CPUInstructionEnum.NOP;
									this.asmInstructions[iPos - 1].InstructionType = CPUInstructionEnum.NOP;
									instruction1 = this.asmInstructions[iPos];
									instruction1.InstructionType = CPUInstructionEnum.SWITCH;
									instruction1.Parameters.Clear();

									// switching parameter
									instruction1.Parameters.Add(parameter);

									if (this.parent.CPUOverlay > 0)
									{
										stream.Seek(usSwitchOffset, SeekOrigin.Begin);
									}
									else
									{
										stream.Seek(MainProgram.ToLinearAddress(this.parent.CPUSegment, usSwitchOffset), SeekOrigin.Begin);
									}

									// values and offsets
									for (int i = 0; i <= uiCount; i++)
									{
										ushort usWord = ReadWord(stream);
										aJumps.Add(usWord);
										instruction1.Parameters.Add(new CPUInstructionParameter(CPUInstructionParameterTypeEnum.Immediate, 
											CPUParameterSizeEnum.UInt16, (uint)i, usWord));
									}

									fnIP = aJumps[aJumps.Count - 1];
									aJumps.RemoveAt(aJumps.Count - 1);

									if (this.parent.CPUOverlay > 0)
									{
										stream.Seek(fnIP, SeekOrigin.Begin);
									}
									else
									{
										stream.Seek(MainProgram.ToLinearAddress(this.parent.CPUSegment, fnIP), SeekOrigin.Begin);
									}
								}
								else
								{
									CPUInstruction instruction = this.asmInstructions[iPos];
									Console.WriteLine($"Undefined switch pattern to {instruction.Parameters[0].ToString()} in function {this.parent.ToString()}.{this.Name} - jmp at 0x{instruction.Segment:x4}:0x{instruction.Offset:x4}");
									//break;
								}
							}
						}
						else
						{
							Console.WriteLine($"Can't find location of switch statement in function {this.parent.ToString()}.{this.Name} - jmp at 0x{this.parent.CPUSegment:x4}:0x{fnIP:x4}");
						}
						continue;
					}

					// no more jumps or switches, we are done
					break;
				}
			}
			#endregion

			this.asmInstructions.Sort(CPUInstruction.CompareInstructionByAddress);

			int iEnd = this.asmInstructions.Count - 1;

			// if function entry differs from zero position
			int iPosition = GetInstructionPositionByOffset(this.fnEntryPoint);

			if (iPosition > 0)
			{
				CPUInstruction instruction; // = this.aInstructions[0];

				//ip = (ushort)((uint)this.aInstructions[iPosition].Offset - (uint)(instruction.Offset));
				instruction = new CPUInstruction(this.parent.CPUSegment, 0xffff, CPUInstructionEnum.JMP, CPUParameterSizeEnum.UInt16);
				instruction.Parameters.Add(new CPUInstructionParameter(CPUInstructionParameterTypeEnum.Immediate, CPUParameterSizeEnum.UInt16, 
					0, this.fnEntryPoint));
				this.asmInstructions.Insert(0, instruction);
			}

			#region Optimize GoTo's
			for (int i = 0; i < this.asmInstructions.Count; i++)
			{
				CPUInstruction instruction = this.asmInstructions[i];
				CPUInstructionParameter parameter;
				ushort usNewAddress;
				ushort usNewAddress2;

				switch (instruction.InstructionType)
				{
					case CPUInstructionEnum.LOOP:
					case CPUInstructionEnum.LOOPZ:
					case CPUInstructionEnum.LOOPNZ:
					case CPUInstructionEnum.JCXZ:
					case CPUInstructionEnum.JMP:
						parameter = instruction.Parameters[0];

						if (parameter.Type == CPUInstructionParameterTypeEnum.Immediate)
						{
							usNewAddress = (ushort)parameter.Value;

							// optimize immediate jumps
							usNewAddress2 = usNewAddress;
							while ((instruction1 = this.asmInstructions[GetInstructionPositionByOffset(usNewAddress2)]).InstructionType == CPUInstructionEnum.JMP)
							{
								usNewAddress2 = (ushort)instruction1.Parameters[0].Value;
							}
							if (usNewAddress != usNewAddress2)
							{
								parameter.Value = usNewAddress2;
							}
						}
						break;

					case CPUInstructionEnum.Jcc:
						parameter = instruction.Parameters[1];

						if (parameter.Type == CPUInstructionParameterTypeEnum.Immediate)
						{
							usNewAddress = (ushort)parameter.Value;

							// optimize immediate jumps
							usNewAddress2 = usNewAddress;
							while ((instruction1 = this.asmInstructions[GetInstructionPositionByOffset(usNewAddress2)]).InstructionType == CPUInstructionEnum.JMP)
							{
								usNewAddress2 = (ushort)instruction1.Parameters[0].Value;
							}
							if (usNewAddress != usNewAddress2)
							{
								parameter.Value = usNewAddress2;
							}
						}
						break;
				}
			}
			#endregion

			#region Assign labels to instructions, convert relative call to absolute call
			for (int i = 0; i < this.asmInstructions.Count; i++)
			{
				CPUInstruction instruction = this.asmInstructions[i];
				CPUInstructionParameter parameter;
				ushort usNewOffset;

				switch (instruction.InstructionType)
				{
					case CPUInstructionEnum.CALL:
						if (this.parent.CPUOverlay != 0)
						{
							// if we are calling inside of overlay translate this to call overlay function
							instruction.InstructionType = CPUInstructionEnum.CallOverlay;
							parameter = instruction.Parameters[0];
							usNewOffset = (ushort)parameter.Value;
							instruction.Parameters.Clear();
							instruction.Parameters.Add(new CPUInstructionParameter(CPUInstructionParameterTypeEnum.Immediate, CPUParameterSizeEnum.UInt8, (uint)this.parent.CPUOverlay));
							instruction.Parameters.Add(new CPUInstructionParameter(CPUInstructionParameterTypeEnum.Immediate, CPUParameterSizeEnum.UInt16, usNewOffset));
						}
						break;

					case CPUInstructionEnum.JMP:
						parameter = instruction.Parameters[0];
						if (parameter.Type == CPUInstructionParameterTypeEnum.Immediate)
						{
							usNewOffset = (ushort)parameter.Value;

							// optimize immediate jumps
							if (i + 1 < this.asmInstructions.Count && 
								this.asmInstructions[i + 1].Offset == usNewOffset)
							{
								// this is just a jump to next instruction, ignore it
								this.asmInstructions[i].InstructionType = CPUInstructionEnum.NOP;
							}
							else
							{
								this.asmInstructions[GetInstructionPositionByOffset(usNewOffset)].Label = true;
							}
						}
						break;

					case CPUInstructionEnum.Jcc:
						usNewOffset = (ushort)instruction.Parameters[1].Value;
						if (usNewOffset > 0)
						{
							this.asmInstructions[GetInstructionPositionByOffset(usNewOffset)].Label = true;
						}
						break;

					case CPUInstructionEnum.LOOP:
					case CPUInstructionEnum.LOOPZ:
					case CPUInstructionEnum.LOOPNZ:
					case CPUInstructionEnum.JCXZ:
						usNewOffset = (ushort)instruction.Parameters[0].Value;
						if (usNewOffset > 0)
						{
							this.asmInstructions[GetInstructionPositionByOffset(usNewOffset)].Label = true;
						}
						break;

					case CPUInstructionEnum.SWITCH:
						for (int j = 1; j < instruction.Parameters.Count; j++)
						{
							parameter = instruction.Parameters[j];

							this.asmInstructions[GetInstructionPositionByOffset((ushort)parameter.Displacement)].Label = true;
						}
						break;
				}
			}
			#endregion

			#region Optimize (XOR, SUB), (PUSH word, PUSH word, POP dword) and (LEA, PUSH)
			for (int i = 0; i < this.asmInstructions.Count; i++)
			{
				CPUInstruction instruction = this.asmInstructions[i];
				uint sourceReg1 = 0;
				uint sourceReg2 = 0;
				uint destinationReg = 0;

				// all xors and subs with same source and destination are 0
				if (i + 1 < this.asmInstructions.Count &&
					((instruction1 = this.asmInstructions[i]).InstructionType == CPUInstructionEnum.XOR ||
					instruction1.InstructionType == CPUInstructionEnum.SUB) &&
					instruction1.Parameters.Count == 2 &&
					instruction1.Parameters[0].Type == CPUInstructionParameterTypeEnum.Register &&
					instruction1.Parameters[1].Type == CPUInstructionParameterTypeEnum.Register &&
					instruction1.Parameters[0].Size == instruction1.Parameters[1].Size &&
					instruction1.Parameters[0].Value == instruction1.Parameters[1].Value &&

					(instruction1 = this.asmInstructions[i + 1]).InstructionType != CPUInstructionEnum.Jcc)
				{
					instruction1 = this.asmInstructions[i];
					instruction1.InstructionType = CPUInstructionEnum.MOV;
					instruction1.Parameters[1] = new CPUInstructionParameter(CPUInstructionParameterTypeEnum.Immediate, 0);
				}

				// optimize convert two words to dword
				if (i + 2 < this.asmInstructions.Count &&
					(instruction1 = this.asmInstructions[i]).InstructionType == CPUInstructionEnum.PUSH &&
					instruction1.OperandSize == CPUParameterSizeEnum.UInt16 &&
					instruction1.Parameters.Count == 1 &&
					instruction1.Parameters[0].Type == CPUInstructionParameterTypeEnum.Register &&
					(sourceReg1 = instruction1.Parameters[0].Value) <= 7 &&

					(instruction1 = this.asmInstructions[i + 1]).InstructionType == CPUInstructionEnum.PUSH &&
					instruction1.OperandSize == CPUParameterSizeEnum.UInt16 &&
					instruction1.Parameters.Count == 1 &&
					instruction1.Parameters[0].Type == CPUInstructionParameterTypeEnum.Register &&
					(sourceReg2 = instruction1.Parameters[0].Value) <= 7 &&

					(instruction1 = this.asmInstructions[i + 2]).InstructionType == CPUInstructionEnum.POP &&
					instruction1.OperandSize == CPUParameterSizeEnum.UInt32 &&
					instruction1.Parameters.Count == 1 &&
					instruction1.Parameters[0].Type == CPUInstructionParameterTypeEnum.Register &&
					(destinationReg = instruction1.Parameters[0].Value) <= 7)
				{
					instruction1 = this.asmInstructions[i];
					instruction1.InstructionType = CPUInstructionEnum.WordsToDword;
					instruction1.OperandSize = CPUParameterSizeEnum.UInt32;
					instruction1.Parameters.Clear();
					instruction1.Parameters.Add(new CPUInstructionParameter(CPUInstructionParameterTypeEnum.Register,
						CPUParameterSizeEnum.UInt32, destinationReg));
					instruction1.Parameters.Add(new CPUInstructionParameter(CPUInstructionParameterTypeEnum.Register,
						CPUParameterSizeEnum.UInt16, sourceReg1));
					instruction1.Parameters.Add(new CPUInstructionParameter(CPUInstructionParameterTypeEnum.Register,
						CPUParameterSizeEnum.UInt16, sourceReg2));

					this.asmInstructions[i + 1].InstructionType = CPUInstructionEnum.NOP;
					this.asmInstructions[i + 2].InstructionType = CPUInstructionEnum.NOP;
				}
				if (i + 1 < this.asmInstructions.Count &&
					(instruction1 = this.asmInstructions[i]).InstructionType == CPUInstructionEnum.LEA &&
					instruction1.OperandSize == CPUParameterSizeEnum.UInt16 &&
					instruction1.Parameters.Count == 2 &&
					instruction1.Parameters[0].Type == CPUInstructionParameterTypeEnum.Register &&
					(sourceReg1 = instruction1.Parameters[0].Value) <= 7 &&

					(instruction1 = this.asmInstructions[i + 1]).InstructionType == CPUInstructionEnum.PUSH &&
					instruction1.OperandSize == CPUParameterSizeEnum.UInt16 &&
					instruction1.Parameters.Count == 1 &&
					instruction1.Parameters[0].Type == CPUInstructionParameterTypeEnum.Register &&
					instruction1.Parameters[0].Value == sourceReg1)
				{
					this.asmInstructions[i].InstructionType = CPUInstructionEnum.NOP;
					CPUInstructionParameter parameter = this.asmInstructions[i].Parameters[1];
					instruction1.Parameters[0] = new CPUInstructionParameter(CPUInstructionParameterTypeEnum.LEAMemoryAddress,
						parameter.Size, parameter.DataSegment, parameter.Value, parameter.Displacement);
				}
			}
			#endregion

			#region Remove Nop, Wait and unused GoTo instructions
			for (int i = 0; i < this.asmInstructions.Count; i++)
			{
				CPUInstruction instruction = this.asmInstructions[i];

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
						if (i + 1 < this.asmInstructions.Count &&
							(instruction1 = this.asmInstructions[i]).InstructionType == CPUInstructionEnum.JMP &&
							(instruction1 = this.asmInstructions[i + 1]).InstructionType == CPUInstructionEnum.JMP &&
							instruction1.Label == false)
						{
							this.asmInstructions.RemoveAt(i + 1);
							i--;
						}
						break;
				}
			}
			#endregion

			#region Convert Relative stack Memory addresses to local variables
			for (int i = 0; i < this.asmInstructions.Count; i++)
			{
				CPUInstruction instruction = this.asmInstructions[i];
				CPUInstructionParameter parameter;

				for (int j = 0; j < instruction.Parameters.Count; j++)
				{
					parameter = instruction.Parameters[j];
					int iVarOffset;

					switch (parameter.Type)
					{
						case CPUInstructionParameterTypeEnum.MemoryAddress:
						case CPUInstructionParameterTypeEnum.LEAMemoryAddress:
							if (parameter.Size == CPUParameterSizeEnum.UInt32)
								throw new Exception("x32 addressing mode not yet implemented");

							if (parameter.DataSegment == CPUSegmentRegisterEnum.SS)
							{
								switch (parameter.Value)
								{
									case 2:
										// [BP + SI]
										Console.WriteLine($"Stack parameter with register offset at function '{this.Name}' - instruction at 0x{instruction.Segment:x4}:0x{instruction.Offset:x4}");
										break;

									case 3:
										// [BP + DI]
										Console.WriteLine($"Stack parameter with register offset at function '{this.Name}' - instruction at 0x{instruction.Segment:x4}:0x{instruction.Offset:x4}");
										break;

									case 10:
										// [BP + SI {0}]
										break;

									case 11:
										// [BP + DI {0}]
										break;

									case 14:
										// [BP {0}]
										iVarOffset = parameter.Displacement;
										if (iVarOffset < 0)
										{
											// local variable
											iVarOffset = -iVarOffset;

											instruction.Parameters[j] = new CPUInstructionParameter(CPUInstructionParameterTypeEnum.LocalVariable, CPUParameterSizeEnum.UInt16,
												CPUSegmentRegisterEnum.SS, 0, iVarOffset);

											if (!this.localVariables.ContainsKey(iVarOffset))
											{
												this.localVariables.Add(iVarOffset, new ILVariable(this, instruction.OperandSize, iVarOffset));
											}
										}
										else if (iVarOffset > 0)
										{
											// local parameters
											instruction.Parameters[j] = new CPUInstructionParameter(CPUInstructionParameterTypeEnum.LocalParameter, CPUParameterSizeEnum.UInt16,
												CPUSegmentRegisterEnum.SS, 0, iVarOffset);

											if (!this.localParameters.ContainsKey(iVarOffset))
											{
												this.localParameters.Add(iVarOffset, new ILVariable(this, ILVariableScopeEnum.LocalParameter, instruction.OperandSize, iVarOffset));
											}
										}
										break;

									case 18:
										// [BP + SI {0}]
										break;

									case 19:
										// [BP + DI {0}]
										break;

									case 22:
										// [BP {0}]"
										iVarOffset = parameter.Displacement;
										if (iVarOffset < 0)
										{
											// local variable
											iVarOffset = -iVarOffset;

											instruction.Parameters[j] = new CPUInstructionParameter(CPUInstructionParameterTypeEnum.LocalVariable, CPUParameterSizeEnum.UInt16,
												CPUSegmentRegisterEnum.SS, 0, iVarOffset);

											if (!this.localVariables.ContainsKey(iVarOffset))
											{
												this.localVariables.Add(iVarOffset, new ILVariable(this, instruction.OperandSize, iVarOffset));
											}
										}
										else if (iVarOffset > 0)
										{
											// local parameters
											instruction.Parameters[j] = new CPUInstructionParameter(CPUInstructionParameterTypeEnum.LocalParameter, CPUParameterSizeEnum.UInt16,
												CPUSegmentRegisterEnum.SS, 0, iVarOffset);

											if (!this.localParameters.ContainsKey(iVarOffset))
											{
												this.localParameters.Add(iVarOffset, new ILVariable(this, ILVariableScopeEnum.LocalParameter, instruction.OperandSize, iVarOffset));
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

			#region Assign ordinals to labels
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

			#region Process calls to other functions
			for (int i = 0; i < this.asmInstructions.Count; i++)
			{
				CPUInstruction instruction = this.asmInstructions[i];
				CPUInstructionParameter parameter;
				ProgramFunction? function;

				switch (instruction.InstructionType)
				{
					case CPUInstructionEnum.CALL:
						parameter = instruction.Parameters[0];

						if (i > 0 && instruction.Parameters[0].Type == CPUInstructionParameterTypeEnum.Immediate)
						{
							// is instruction prefixed by PUSH CS?
							instruction1 = this.asmInstructions[i - 1];

							if (instruction1.InstructionType == CPUInstructionEnum.PUSH &&
								instruction1.Parameters[0].Type == CPUInstructionParameterTypeEnum.SegmentRegister &&
								instruction1.Parameters[0].Value == (uint)CPUSegmentRegisterEnum.CS)
							{
								function = this.parent.Parent.FindFunction(0, instruction.Segment, (ushort)instruction.Parameters[0].Value);
								if (function == null)
								{
									// function is not yet defined, define it
									function = decompiler.Disassemble(0, instruction.Segment, (ushort)instruction.Parameters[0].Value, null);
								}

								if ((function.CallType & ProgramFunctionTypeEnum.Far) == ProgramFunctionTypeEnum.Far)
								{
									instruction.InstructionType = CPUInstructionEnum.CALLF;
									instruction.Parameters[0] = new CPUInstructionParameter(instruction.Segment, instruction.Parameters[0].Value);

									// turn push cs instruction to nop, as it is not needed anymore
									if (instruction1.Label)
									{
										instruction1.InstructionType = CPUInstructionEnum.NOP;
									}
									else
									{
										this.asmInstructions.RemoveAt(i - 1);
										i--;
									}
								}
							}
							else
							{
								function = decompiler.FindFunction(0, this.parent.CPUSegment, (ushort)parameter.Value);
								if (function == null)
								{
									// function is not yet defined, define it
									decompiler.Disassemble(0, this.parent.CPUSegment, (ushort)parameter.Value, null);
									//function = decompiler.GetFunction(parameter.Segment, (ushort)parameter.Value, this.uiStreamOffset);
								}
							}
						}
						else if (parameter.Type == CPUInstructionParameterTypeEnum.Immediate)
						{
							function = decompiler.FindFunction(0, this.parent.CPUSegment, (ushort)parameter.Value);
							if (function == null)
							{
								// function is not yet defined, define it
								decompiler.Disassemble(0, this.parent.CPUSegment, (ushort)parameter.Value, null);
								//function = decompiler.GetFunction(parameter.Segment, (ushort)parameter.Value, this.uiStreamOffset);
							}
						}
						break;

					case CPUInstructionEnum.CALLF:
						parameter = instruction.Parameters[0];
						if (parameter.Type == CPUInstructionParameterTypeEnum.SegmentOffset)
						{
							function = decompiler.FindFunction(0, parameter.Segment, (ushort)parameter.Value);
							if (function == null)
							{
								// function is not yet defined, define it
								decompiler.Disassemble(0, parameter.Segment, (ushort)parameter.Value, null);
								//function = decompiler.GetFunction(parameter.Segment, (ushort)parameter.Value, this.uiStreamOffset);
							}
						}
						break;

					case CPUInstructionEnum.CallOverlay:
						if (instruction.Parameters[0].Value == 0)
						{
							throw new Exception("Overlay manager references overlay 0");
						}
						function = decompiler.FindFunction((ushort)instruction.Parameters[0].Value, 0, (ushort)instruction.Parameters[1].Value);
						if (function == null)
						{
							// function is not yet defined, define it
							decompiler.Disassemble((ushort)instruction.Parameters[0].Value, 0, (ushort)instruction.Parameters[1].Value, null);
							//function = decompiler.GetFunction(parameter.Segment, (ushort)parameter.Value, this.uiStreamOffset);
						}
						break;

					case CPUInstructionEnum.JMPF:
						parameter = instruction.Parameters[0];
						if (parameter.Type == CPUInstructionParameterTypeEnum.SegmentOffset)
						{
							function = decompiler.FindFunction(0, parameter.Segment, (ushort)parameter.Value);
							if (function == null)
							{
								// function is not yet defined, define it
								decompiler.Disassemble(0, parameter.Segment, (ushort)parameter.Value, null);
								//function = decompiler.GetFunction(parameter.Segment, (ushort)parameter.Value, this.uiStreamOffset);
							}
						}
						break;
				}
			}
			#endregion

			this.Disassemble();
		}*/

		public bool TranslateToIL()
		{
			List<CPUInstruction> instructions = new(this.asmInstructions);
			CPUInstruction instruction;
			int instructionCount = instructions.Count;
			BDictionary<CPURegisterEnum, ILExpression> localRegisters = new();
			BDictionary<CPUSegmentRegisterEnum, uint> localSegments = new();

			this.ilInstructions.Clear();
			localSegments.Add(CPUSegmentRegisterEnum.DS, this.parent.Parent.DefaultDS);
			localSegments.Add(CPUSegmentRegisterEnum.ES, this.parent.Parent.DefaultDS);

			Console.WriteLine($"Translating '{this.parent.ToString()}.{this.Name}'");

			// Check for function signature
			if (instructionCount > 4 &&
				(instruction = instructions[0]).InstructionType == CPUInstructionEnum.PUSH &&
				instruction.OperandSize == CPUParameterSizeEnum.UInt16 &&
				instruction.Parameters.Count == 1 &&
				instruction.Parameters[0].Type == CPUParameterTypeEnum.Register &&
				instruction.Parameters[0].Value == (uint)CPURegisterEnum.BP &&

				(instruction = instructions[1]).InstructionType == CPUInstructionEnum.MOV &&
				instruction.OperandSize == CPUParameterSizeEnum.UInt16 &&
				instruction.Parameters.Count == 2 &&
				instruction.Parameters[0].Type == CPUParameterTypeEnum.Register &&
				instruction.Parameters[0].Value == (uint)CPURegisterEnum.BP &&
				instruction.Parameters[1].Type == CPUParameterTypeEnum.Register &&
				instruction.Parameters[1].Value == (uint)CPURegisterEnum.SP &&

				(instruction = instructions[instructionCount - 2]).InstructionType == CPUInstructionEnum.POP &&
				instruction.OperandSize == CPUParameterSizeEnum.UInt16 &&
				instruction.Parameters.Count == 1 &&
				instruction.Parameters[0].Type == CPUParameterTypeEnum.Register &&
				instruction.Parameters[0].Value == (uint)CPURegisterEnum.BP &&

				((instruction = instructions[instructionCount - 1]).InstructionType == CPUInstructionEnum.RETF ||
				instruction.InstructionType == CPUInstructionEnum.IRET ||
				instruction.InstructionType == CPUInstructionEnum.RET))
			{
				instructions.RemoveAt(0);
				instructionCount--;
				instructions.RemoveAt(0);
				instructionCount--;
				instructions.RemoveAt(--instructionCount);
				instructions.RemoveAt(--instructionCount);

				if ((instruction = instructions[instructionCount - 1]).InstructionType == CPUInstructionEnum.MOV &&
					instruction.OperandSize == CPUParameterSizeEnum.UInt16 &&
					instruction.Parameters.Count == 2 &&
					instruction.Parameters[0].Type == CPUParameterTypeEnum.Register &&
					instruction.Parameters[0].Value == (uint)CPURegisterEnum.SP &&
					instruction.Parameters[1].Type == CPUParameterTypeEnum.Register &&
					instruction.Parameters[1].Value == (uint)CPURegisterEnum.BP)
				{
					instructions.RemoveAt(--instructionCount);
				}
				
				bool bDI = false;
				bool bSI = false;

				if ((instruction = instructions[0]).InstructionType == CPUInstructionEnum.PUSH &&
					instruction.OperandSize == CPUParameterSizeEnum.UInt16 &&
					instruction.Parameters.Count == 1 &&
					instruction.Parameters[0].Type == CPUParameterTypeEnum.Register &&
					instruction.Parameters[0].Value == (uint)CPURegisterEnum.DI &&
					(instruction = instructions[instructionCount - 1]).InstructionType == CPUInstructionEnum.POP &&
					instruction.OperandSize == CPUParameterSizeEnum.UInt16 &&
					instruction.Parameters.Count == 1 &&
					instruction.Parameters[0].Type == CPUParameterTypeEnum.Register &&
					instruction.Parameters[0].Value == (uint)CPURegisterEnum.DI)
				{
					instructions.RemoveAt(0);
					instructionCount--;
					instructions.RemoveAt(--instructionCount);
					bDI = true;
				}

				if ((instruction = instructions[0]).InstructionType == CPUInstructionEnum.PUSH &&
					instruction.OperandSize == CPUParameterSizeEnum.UInt16 &&
					instruction.Parameters.Count == 1 &&
					instruction.Parameters[0].Type == CPUParameterTypeEnum.Register &&
					instruction.Parameters[0].Value == (uint)CPURegisterEnum.SI &&
					(instruction = instructions[instructionCount - 1]).InstructionType == CPUInstructionEnum.POP &&
					instruction.OperandSize == CPUParameterSizeEnum.UInt16 &&
					instruction.Parameters.Count == 1 &&
					instruction.Parameters[0].Type == CPUParameterTypeEnum.Register &&
					instruction.Parameters[0].Value == (uint)CPURegisterEnum.SI)
				{
					instructions.RemoveAt(0);
					instructionCount--;
					instructions.RemoveAt(--instructionCount);
					bSI = true;
				}

				// track the stack state
				// sometimes the function call doesn't adjust SP (for the length of the parameters) at the end of the function
				int localVariableSize = 0;
				int localVariablePosition = 6;
				Stack<ILExpression> localStack = new Stack<ILExpression>();

				if ((instruction = instructions[0]).InstructionType == CPUInstructionEnum.SUB &&
					instruction.OperandSize == CPUParameterSizeEnum.UInt16 &&
					instruction.Parameters.Count == 2 &&
					instruction.Parameters[0].Type == CPUParameterTypeEnum.Register &&
					instruction.Parameters[0].Value == (uint)CPURegisterEnum.SP &&
					instruction.Parameters[1].Type == CPUParameterTypeEnum.Immediate)
				{
					instructions.RemoveAt(0);
					instructionCount--;

					localVariableSize = (int)instruction.Parameters[1].Value;
					localVariablePosition += localVariableSize;
				}

				for (int i = 0; i < instructions.Count; i++)
				{
					instruction = instructions[i];
					CPUParameter parameter0;
					CPURegisterEnum register;
					ILExpression variable;
					ProgramFunction? function;

					switch (instruction.InstructionType)
					{
						case CPUInstructionEnum.SUB:
							parameter0 = instruction.Parameters[0];

							if (parameter0.Type == CPUParameterTypeEnum.Register &&
								parameter0.Type == instruction.Parameters[1].Type &&
								parameter0.Value == instruction.Parameters[1].Value)
							{
								register = parameter0.RegisterValue;
								localVariables.Add(localVariablePosition, new ILVariable(this, ILValueTypeEnum.Int16, localVariablePosition));
								variable = new ILLocalVariableReference(this, localVariablePosition);

								if (localRegisters.ContainsKey(register))
								{
									localRegisters.SetValueByKey(register, variable);
								}
								else
								{
									localRegisters.Add(register, variable);
								}

								this.ilInstructions.Add(new ILAssignment(variable, new ILImmediateValue(parameter0.Size, 0)));

								localVariablePosition += 2;
							}
							else
							{
								register = parameter0.RegisterValue;

								if (localRegisters.ContainsKey(register))
								{
									variable = localRegisters.GetValueByKey(register);
									ILOperator op = new ILOperator(variable, ILOperatorEnum.Substract, ParameterToIL(localSegments, localRegisters, instruction.Parameters[1]));
									this.ilInstructions.Add(new ILAssignment(variable, op));
								}
								else
								{
									throw new Exception($"Use of a undefined variable '{register}'");
								}
							}
							break;

						case CPUInstructionEnum.INC:
							parameter0 = instruction.Parameters[0];
							switch (parameter0.Type)
							{
								case CPUParameterTypeEnum.Register:
									register = parameter0.RegisterValue;

									if (localRegisters.ContainsKey(register))
									{
										this.ilInstructions.Add(new ILUnaryAssignmentOperator(localRegisters.GetValueByKey(register), ILUnaryOperatorEnum.IncrementAfter));
									}
									else
									{
										throw new Exception($"Use of a undefined variable '{register}'");
									}
									break;

								default:
									throw new Exception($"INC parameter type '{parameter0.Type}' not implemented");
							}
							break;

						case CPUInstructionEnum.MOV:
							parameter0 = instruction.Parameters[0];

							switch (parameter0.Type)
							{
								case CPUParameterTypeEnum.Register:
									register = parameter0.RegisterValue;
									localVariables.Add(localVariablePosition, new ILVariable(this, ILValueTypeEnum.Int16, localVariablePosition));
									variable = new ILLocalVariableReference(this, localVariablePosition);

									if (localRegisters.ContainsKey(register))
									{
										localRegisters.SetValueByKey(register, variable);
									}
									else
									{
										localRegisters.Add(register, variable);
									}

									this.ilInstructions.Add(new ILAssignment(variable, ParameterToIL(localSegments, localRegisters, instruction.Parameters[1])));

									localVariablePosition += 2;
									break;

								default:
									throw new Exception($"MOV left parameter type '{parameter0.Type}' not implemented");
							}
							break;

						case CPUInstructionEnum.PUSH:
							localStack.Push(ParameterToIL(localSegments, localRegisters, instruction.Parameters[0]));
							break;

						case CPUInstructionEnum.CALLF:
							parameter0 = instruction.Parameters[0];

							if (parameter0.Type == CPUParameterTypeEnum.SegmentOffset)
							{
								function = this.parent.Parent.FindFunction(0, parameter0.Segment, (ushort)parameter0.Value);

								if (function != null)
								{
									if ((function.CallType & ProgramFunctionTypeEnum.Cdecl) == ProgramFunctionTypeEnum.Cdecl)
									{
										if (i + 1 >= this.asmInstructions.Count)
										{
											// this function call is at the end of the function body, no stack adjustment available
											List<ILExpression> parameterList = new List<ILExpression>();

											while (localStack.Count > 0)
											{
												parameterList.Add(localStack.Pop());
											}

											this.ilInstructions.Add(new ILFunctionCall(function, parameterList));
										}
										else if ((instruction = instructions[i + 1]).InstructionType == CPUInstructionEnum.ADD &&
											instruction.OperandSize == CPUParameterSizeEnum.UInt16 &&
											instruction.Parameters.Count == 2 &&
											instruction.Parameters[0].Type == CPUParameterTypeEnum.Register &&
											instruction.Parameters[0].Value == (uint)CPURegisterEnum.SP &&
											instruction.Parameters[1].Type == CPUParameterTypeEnum.Immediate)
										{
											if (instruction.Parameters[1].Value == function.ParameterSize)
											{
												// normal Cdecl function call
												List<ILExpression> parameterList = new List<ILExpression>();

												for (int j = 0; j < function.Parameters.Count; j++)
												{
													parameterList.Add(localStack.Pop());
												}

												this.ilInstructions.Add(new ILFunctionCall(function, parameterList));
												i++;
											}
											else
											{
												throw new Exception($"The function '{function.Parent.ToString()}.{function.Name}' "+
													$"accepts {function.Parameters.Count} parameters, but {(instruction.Parameters[1].Value / 2)} parameters passed");
											}
										}
									}
									else if ((function.CallType & ProgramFunctionTypeEnum.Pascal) == ProgramFunctionTypeEnum.Pascal)
									{
										throw new Exception("Pascal function call not implemented");
									}
									else
									{
										throw new Exception($"Unsupported function call type '{function.CallType}'");
									}
								}
								else
								{
									throw new Exception($"Can't find the function at 0x{parameter0.Segment:x}:0x{parameter0.Value:x}");
								}
							}
							else
							{
								throw new Exception("Indirect function call not implemented");
							}
							break;

						default:
							throw new Exception($"Don't know how to translate '{instruction.ToString()}'");
					}
				}
			}

			return false;
		}

		private ILExpression ParameterToIL(BDictionary<CPUSegmentRegisterEnum, uint> localSegments, 
			BDictionary<CPURegisterEnum, ILExpression> localRegisters, CPUParameter parameter)
		{
			switch (parameter.Type)
			{
				case CPUParameterTypeEnum.Immediate:
					switch (parameter.Size)
					{
						case CPUParameterSizeEnum.UInt8:
							return new ILImmediateValue(ILValueTypeEnum.UInt8, parameter.Value);

						case CPUParameterSizeEnum.UInt16:
							return new ILImmediateValue(ILValueTypeEnum.UInt16, parameter.Value);

						case CPUParameterSizeEnum.UInt32:
							return new ILImmediateValue(ILValueTypeEnum.UInt32, parameter.Value);

						default:
							throw new Exception($"Parameter size {parameter.Size} not implemented");
					}

				case CPUParameterTypeEnum.Register:
					CPURegisterEnum register = parameter.RegisterValue;
					if (localRegisters.ContainsKey(register))
					{
						return localRegisters.GetValueByKey(register);
					}
					else
					{
						throw new Exception($"Use of a undefined variable '{register}'");
					}

				case CPUParameterTypeEnum.LocalParameter:
					return new ILLocalParameterReference(this, (int)parameter.Displacement);

				case CPUParameterTypeEnum.LocalVariable:
					return new ILLocalVariableReference(this, (int)parameter.Displacement);

				case CPUParameterTypeEnum.MemoryAddress:
					if (parameter.DataSegment == CPUSegmentRegisterEnum.SS)
					{
						switch (parameter.Value)
						{
							case 0: // [BX + SI]
							case 1: // [BX + DI]
							case 4: // [SI]
							case 5: // [DI]
							case 6: // [{0}]
							case 7: // [BX]

							case 8: // [{0} + BX + SI]
							case 9: // [{0} + BX + DI]
							case 12: // [{0} + SI]
							case 13: // [{0} + DI]
							case 15: // [{0} + BX]

							case 16: // [{0} + BX + SI]
							case 17: // [{0} + BX + DI]
							case 20: // [{0} + SI]
							case 21: // [{0} + DI]
							case 23: // [{0} + BX]
								throw new Exception($"The addressing type {parameter.ToString()} is not supported on segment {parameter.DataSegment}");

							case 2: // [BP + SI]
								break;
							case 3: // [BP + DI]
								break;

							case 10: // [BP {0} + SI]
								break;
							case 11: // [BP {0} + DI]
								break;
							case 14: // [BP {0}]
								break;

							case 18: // [BP {0} + SI]
								break;
							case 19: // [BP {0} + DI]
								break;
							case 22: // [BP {0}]
								break;
						}

						throw new Exception("Not implemented");
					}
					else
					{
						if (!localSegments.ContainsKey(parameter.DataSegment))
						{
							throw new Exception($"The segment '{parameter.DataSegment}' is not defined");
						}

						ProgramSegment segment = this.parent.Parent.FindOrCreateSegment(this.parent.CPUOverlay, (ushort)localSegments.GetValueByKey(parameter.DataSegment));

						switch (parameter.Value)
						{
							case 2: // [BP + SI]
							case 3: // [BP + DI]

							case 10: // [BP {0} + SI]
							case 11: // [BP {0} + DI]
							case 14: // [BP {0}]

							case 18: // [BP {0} + SI]
							case 19: // [BP {0} + DI]
							case 22: // [BP {0}]
								throw new Exception($"The addressing type {parameter.ToString()} is not supported on segment {parameter.DataSegment}");

							case 0: // [BX + SI]
								break;
							case 1: // [BX + DI]
								break;
							case 4: // [SI]
								break;
							case 5: // [DI]
								break;
							case 6: // [{0}]
								segment.GetOrDefineGlobalVariable(parameter.Size, parameter.Displacement);
								return new ILGlobalVariableReference(segment, parameter.Displacement);
							case 7: // [BX]
								break;

							case 8: // [{0} + BX + SI]
								break;
							case 9: // [{0} + BX + DI]
								break;
							case 12: // [{0} + SI]
								break;
							case 13: // [{0} + DI]
								break;
							case 15: // [{0} + BX]
								break;

							case 16: // [{0} + BX + SI]
								break;
							case 17: // [{0} + BX + DI]
								break;
							case 20: // [{0} + SI]
								break;
							case 21: // [{0} + DI]
								break;
							case 23: // [{0} + BX]
								break;
						}

						throw new Exception("Not implemented");
					}

				default:
					throw new Exception($"Parameter type '{parameter.Type}' not implemented");
			}
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

			if (this.flowGraph != null)
			{
				writer.WriteLine($"{GetTabs(tabLevel + 1)}// {(this.flowGraph.BPFrame ? "Standard C frame" : "Assembly")}");
			}

			if (verbosity > 0)
			{
				writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.Log.EnterBlock(\"'{this.Name}'({this.callType.ToString()}) at {this.parent.ToString()}:0x{this.fnOffset:x}\");");
				writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.CS.UInt16 = 0x{this.parent.Segment:x4}; // set this function segment");
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
				CPUInstruction asmInstruction = this.asmInstructions[k];

				if (asmInstruction.Label)
				{
					writer.WriteLine();
					if (verbosity > 0)
					{
						writer.WriteLine($"{GetTabs(tabLevel)}{asmInstruction.LabelName}: // 0x{asmInstruction.Offset:x}");
					}
					else
					{
						writer.WriteLine($"{GetTabs(tabLevel)}{asmInstruction.LabelName}:");
					}
				}

				uint uiOffset = 0;
				CPUParameter parameter;
				ProgramFunction? function1;
				int instructionIndex;

				switch (asmInstruction.InstructionType)
				{
					case CPUInstructionEnum.ADC:
					case CPUInstructionEnum.ADD:
					case CPUInstructionEnum.AND:
					case CPUInstructionEnum.OR:
					case CPUInstructionEnum.SBB:
					case CPUInstructionEnum.SUB:
					case CPUInstructionEnum.XOR:
						parameter = asmInstruction.Parameters[0];
						writer.Write($"{GetTabs(tabLevel + 1)}");
						writer.WriteLine(parameter.ToDestinationCSTextMZ(asmInstruction.OperandSize, string.Format("this.oCPU.{0}{1}({2}, {3})",
							asmInstruction.InstructionType.ToString(), asmInstruction.OperandSize.ToString(),
							parameter.ToSourceCSTextMZ(asmInstruction.OperandSize), asmInstruction.Parameters[1].ToSourceCSTextMZ(asmInstruction.OperandSize))));
						break;

					case CPUInstructionEnum.DEC:
					case CPUInstructionEnum.INC:
					case CPUInstructionEnum.NEG:
					case CPUInstructionEnum.NOT:
						parameter = asmInstruction.Parameters[0];
						writer.Write($"{GetTabs(tabLevel + 1)}");
						writer.WriteLine(parameter.ToDestinationCSTextMZ(asmInstruction.OperandSize, string.Format("this.oCPU.{0}{1}({2})",
							asmInstruction.InstructionType.ToString(), asmInstruction.OperandSize.ToString(), parameter.ToSourceCSTextMZ(asmInstruction.OperandSize))));
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
						parameter = asmInstruction.Parameters[0];
						writer.Write($"{GetTabs(tabLevel + 1)}");
						writer.WriteLine(parameter.ToDestinationCSTextMZ(asmInstruction.OperandSize, string.Format("this.oCPU.{0}{1}({2}, {3})",
							asmInstruction.InstructionType.ToString(), asmInstruction.OperandSize.ToString(),
							parameter.ToSourceCSTextMZ(asmInstruction.OperandSize), asmInstruction.Parameters[1].ToSourceCSTextMZ(asmInstruction.Parameters[1].Size))));
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
						if (asmInstruction.OperandSize == CPUParameterSizeEnum.UInt16)
						{
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.CBW();");
						}
						else
						{
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.CWDE();");
						}
						break;

					case CPUInstructionEnum.CWD:
						if (asmInstruction.OperandSize == CPUParameterSizeEnum.UInt16)
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
						parameter = asmInstruction.Parameters[0];
						writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.{asmInstruction.InstructionType.ToString()}{asmInstruction.OperandSize.ToString()}" +
							$"({parameter.ToSourceCSTextMZ(asmInstruction.OperandSize)}, {asmInstruction.Parameters[1].ToSourceCSTextMZ(asmInstruction.OperandSize)});");
						break;

					case CPUInstructionEnum.DIV:
					case CPUInstructionEnum.IDIV:
						if (asmInstruction.OperandSize == CPUParameterSizeEnum.UInt8)
						{
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.{asmInstruction.InstructionType.ToString()}{asmInstruction.OperandSize.ToString()}" +
								$"(this.oCPU.AX, {asmInstruction.Parameters[0].ToSourceCSTextMZ(asmInstruction.OperandSize)});");
						}
						else
						{
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.{asmInstruction.InstructionType.ToString()}{asmInstruction.OperandSize.ToString()}" +
								$"(this.oCPU.AX, this.oCPU.DX, {asmInstruction.Parameters[0].ToSourceCSTextMZ(asmInstruction.OperandSize)});");
						}
						break;

					case CPUInstructionEnum.MUL:
						if (asmInstruction.OperandSize == CPUParameterSizeEnum.UInt8)
						{
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.{asmInstruction.InstructionType.ToString()}{asmInstruction.OperandSize.ToString()}" +
								$"(this.oCPU.AX, {asmInstruction.Parameters[1].ToSourceCSTextMZ(asmInstruction.OperandSize)}," +
								$"{asmInstruction.Parameters[2].ToSourceCSTextMZ(asmInstruction.OperandSize)});");
						}
						else
						{
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.{asmInstruction.InstructionType.ToString()}{asmInstruction.OperandSize.ToString()}" +
								$"(this.oCPU.AX, this.oCPU.DX, {asmInstruction.Parameters[1].ToSourceCSTextMZ(asmInstruction.OperandSize)}, " +
								$"{asmInstruction.Parameters[2].ToSourceCSTextMZ(asmInstruction.OperandSize)});");
						}
						break;

					case CPUInstructionEnum.IMUL:
						parameter = asmInstruction.Parameters[0];
						writer.Write($"{GetTabs(tabLevel + 1)}");

						if (parameter.Type == CPUParameterTypeEnum.Register)
						{
							if (parameter.RegisterValue == CPURegisterEnum.AX_DX)
							{
								writer.WriteLine($"this.oCPU.{asmInstruction.InstructionType.ToString()}{asmInstruction.OperandSize.ToString()}(" +
									"this.oCPU.AX, this.oCPU.DX, " +
									$"{asmInstruction.Parameters[1].ToSourceCSTextMZ(asmInstruction.OperandSize)}, " +
									$"{asmInstruction.Parameters[2].ToSourceCSTextMZ(asmInstruction.OperandSize)});");
							}
							else if (parameter.RegisterValue == CPURegisterEnum.AX)
							{
								writer.WriteLine($"this.oCPU.{asmInstruction.InstructionType.ToString()}{asmInstruction.OperandSize.ToString()}(" +
									"this.oCPU.AX, " +
									$"{asmInstruction.Parameters[1].ToSourceCSTextMZ(asmInstruction.OperandSize)}, " +
									$"{asmInstruction.Parameters[2].ToSourceCSTextMZ(asmInstruction.OperandSize)});");
							}
							else
							{
								writer.WriteLine($"this.oCPU.{asmInstruction.InstructionType.ToString()}{asmInstruction.OperandSize.ToString()}(" +
									$"this.oCPU.{parameter.RegisterValue.ToString()}, " +
									$"{asmInstruction.Parameters[1].ToSourceCSTextMZ(asmInstruction.OperandSize)}, " +
									$"{asmInstruction.Parameters[2].ToSourceCSTextMZ(asmInstruction.OperandSize)});");
							}
						}
						else
						{
							throw new Exception("Invalid IMUL instruction");
						}
						break;

					case CPUInstructionEnum.LDS:
						parameter = asmInstruction.Parameters[1];
						writer.WriteLine($"{GetTabs(tabLevel + 1)}// LDS");
						writer.WriteLine($"{GetTabs(tabLevel + 1)}{asmInstruction.Parameters[0].ToDestinationCSTextMZ(asmInstruction.OperandSize, parameter.ToSourceCSTextMZ(asmInstruction.OperandSize))}");
						writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.DS.{asmInstruction.OperandSize.ToString()} = this.oCPU.Read{asmInstruction.OperandSize.ToString()}" +
							$"({parameter.GetSegmentTextMZ()}, (ushort)({parameter.ToCSTextMZ(asmInstruction.OperandSize)} + 2));");
						break;

					case CPUInstructionEnum.LES:
						parameter = asmInstruction.Parameters[1];
						writer.WriteLine($"{GetTabs(tabLevel + 1)}// LES");
						writer.WriteLine($"{GetTabs(tabLevel + 1)}{asmInstruction.Parameters[0].ToDestinationCSTextMZ(asmInstruction.OperandSize, parameter.ToSourceCSTextMZ(asmInstruction.OperandSize))}");
						writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.ES.{asmInstruction.OperandSize.ToString()} = this.oCPU.Read{asmInstruction.OperandSize.ToString()}" +
							$"({parameter.GetSegmentTextMZ()}, (ushort)({parameter.ToCSTextMZ(asmInstruction.OperandSize)} + 2));");
						break;

					case CPUInstructionEnum.LEA:
						parameter = asmInstruction.Parameters[1];
						writer.WriteLine($"{GetTabs(tabLevel + 1)}// LEA");
						writer.Write($"{GetTabs(tabLevel + 1)}{asmInstruction.Parameters[0].ToDestinationCSTextMZ(asmInstruction.OperandSize, parameter.ToCSTextMZ(asmInstruction.OperandSize))}");
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
						if (asmInstruction.RepPrefix == CPUInstructionPrefixEnum.REPE || asmInstruction.RepPrefix == CPUInstructionPrefixEnum.REPNE)
						{
							writer.WriteLine("{0}_LODS{1}();", asmInstruction.RepPrefix.ToString(), asmInstruction.OperandSize.ToString());
						}
						else
						{
							writer.WriteLine("LODS{0}();", asmInstruction.OperandSize.ToString());
						}
						break;

					case CPUInstructionEnum.MOV:
						parameter = asmInstruction.Parameters[1];
						writer.Write($"{GetTabs(tabLevel + 1)}");
						writer.Write(asmInstruction.Parameters[0].ToDestinationCSTextMZ(asmInstruction.OperandSize, parameter.ToSourceCSTextMZ(asmInstruction.OperandSize)));
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
						if (asmInstruction.RepPrefix == CPUInstructionPrefixEnum.REPE || asmInstruction.RepPrefix == CPUInstructionPrefixEnum.REPNE)
						{
							writer.WriteLine("{0}_MOVS{1}({2}, this.oCPU.SI, this.oCPU.ES, this.oCPU.DI, this.oCPU.CX);",
								asmInstruction.RepPrefix.ToString(), asmInstruction.OperandSize.ToString(), asmInstruction.GetDefaultDataSegmentTextMZ());
						}
						else
						{
							writer.WriteLine("MOVS{0}({1}, this.oCPU.SI, this.oCPU.ES, this.oCPU.DI);",
								asmInstruction.OperandSize.ToString(), asmInstruction.GetDefaultDataSegmentTextMZ());
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
						parameter = asmInstruction.Parameters[0];
						/*Console.WriteLine("POP {0} in {1}.{2} ", instruction.Parameters[0].ToString(),
							segment.Namespace, function.Name);*/
						writer.Write($"{GetTabs(tabLevel + 1)}");
						writer.WriteLine(parameter.ToDestinationCSTextMZ(asmInstruction.OperandSize, string.Format("this.oCPU.POP{0}()",
							asmInstruction.OperandSize.ToString())));
						break;

					case CPUInstructionEnum.POPA:
						writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.POPA{asmInstruction.OperandSize.ToString()}();");
						break;

					case CPUInstructionEnum.POPF:
						writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.POPFUInt16();");
						break;

					case CPUInstructionEnum.PUSH:
						parameter = asmInstruction.Parameters[0];
						writer.Write($"{GetTabs(tabLevel + 1)}this.oCPU.PUSH{asmInstruction.OperandSize.ToString()}({parameter.ToSourceCSTextMZ(asmInstruction.OperandSize)});");
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
						writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.PUSHA{asmInstruction.OperandSize.ToString()}();");
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
						if (asmInstruction.RepPrefix == CPUInstructionPrefixEnum.REPE || asmInstruction.RepPrefix == CPUInstructionPrefixEnum.REPNE)
						{
							writer.WriteLine("{0}_STOS{1}();",
								asmInstruction.RepPrefix.ToString(), asmInstruction.OperandSize.ToString());
						}
						else
						{
							writer.WriteLine("STOS{0}();", asmInstruction.OperandSize.ToString());
						}
						break;

					case CPUInstructionEnum.XCHG:
						parameter = asmInstruction.Parameters[0];
						//writer.WriteLine("{GetTabs(tabLevel)}// XCHG");
						writer.Write($"{GetTabs(tabLevel + 1)}");
						writer.WriteLine("this.oCPU.Temp.{0} = {1};", asmInstruction.OperandSize.ToString(), parameter.ToSourceCSTextMZ(asmInstruction.OperandSize));
						writer.Write($"{GetTabs(tabLevel + 1)}");
						writer.WriteLine(parameter.ToDestinationCSTextMZ(asmInstruction.OperandSize, asmInstruction.Parameters[1].ToSourceCSTextMZ(asmInstruction.OperandSize)));
						parameter = asmInstruction.Parameters[1];
						writer.Write($"{GetTabs(tabLevel + 1)}");
						writer.WriteLine(parameter.ToDestinationCSTextMZ(asmInstruction.OperandSize, string.Format("this.oCPU.Temp.{0}", asmInstruction.OperandSize.ToString())));
						break;

					/*case CPUInstructionEnum.XLAT:
						writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.XLATUInt8({asmInstruction.GetDefaultDataSegmentTextMZ()});");
						break;*/

					// input and output port instructions
					case CPUInstructionEnum.IN:
						writer.Write($"{GetTabs(tabLevel + 1)}");
						writer.WriteLine("{0}",
							asmInstruction.Parameters[0].ToDestinationCSTextMZ(asmInstruction.Parameters[0].Size,
							string.Format("this.oCPU.IN{0}({1})",
							asmInstruction.OperandSize.ToString(),
							asmInstruction.Parameters[1].ToSourceCSTextMZ(asmInstruction.Parameters[1].Size))));
						break;
					case CPUInstructionEnum.OUT:
						writer.Write($"{GetTabs(tabLevel + 1)}");
						writer.WriteLine("this.oCPU.OUT{0}({1}, {2});",
							asmInstruction.OperandSize.ToString(),
							asmInstruction.Parameters[0].ToSourceCSTextMZ(asmInstruction.Parameters[0].Size),
							asmInstruction.Parameters[1].ToSourceCSTextMZ(asmInstruction.Parameters[1].Size));
						break;

					case CPUInstructionEnum.OUTS:
						writer.Write($"{GetTabs(tabLevel + 1)}this.oCPU.");
						if (asmInstruction.RepPrefix == CPUInstructionPrefixEnum.REPE || asmInstruction.RepPrefix == CPUInstructionPrefixEnum.REPNE)
						{
							writer.WriteLine("{0}_{1}{2}({3}, this.oCPU.SI, this.oCPU.CX);",
								asmInstruction.RepPrefix.ToString(),
								asmInstruction.InstructionType.ToString(), asmInstruction.OperandSize.ToString(),
								asmInstruction.GetDefaultDataSegmentTextMZ());
						}
						else
						{
							writer.WriteLine("{0}{1}({2}, this.oCPU.SI);",
								asmInstruction.InstructionType.ToString(), asmInstruction.OperandSize.ToString(),
								asmInstruction.GetDefaultDataSegmentTextMZ());
						}
						break;

					// special syntetic functions
					case CPUInstructionEnum.WordsToDword:
						writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.{asmInstruction.Parameters[0].RegisterValue.ToString()}.UInt32 = " +
							$"(uint)(((uint)this.oCPU.{asmInstruction.Parameters[1].RegisterValue.ToString()}.UInt16 << 16) | " +
							$"(uint)this.oCPU.{asmInstruction.Parameters[2].RegisterValue.ToString()}.UInt16);");
						break;

					// flow control instructions
					case CPUInstructionEnum.SWITCH:
						writer.WriteLine($"{GetTabs(tabLevel + 1)}switch({asmInstruction.Parameters[0].ToSourceCSTextMZ(asmInstruction.OperandSize)})");
						writer.WriteLine($"{GetTabs(tabLevel + 1)}{{");
						for (int l = 1; l < asmInstruction.Parameters.Count; l++)
						{
							parameter = asmInstruction.Parameters[l];
							instructionIndex = this.GetInstructionPositionByOffset((ushort)parameter.Displacement);

							if (instructionIndex < 0)
							{
								throw new Exception($"Can't find instruction in function {this.parent.ToString()}.{this.Name} at offset 0x{parameter.Displacement:x4}");
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
						uiOffset = asmInstruction.Parameters[1].Value;
						instructionIndex = this.GetInstructionPositionByOffset((ushort)uiOffset);

						if (instructionIndex < 0)
						{
							throw new Exception($"Can't find instruction in function {this.parent.ToString()}.{this.Name} at offset 0x{uiOffset:x4}");
						}
						else
						{
							writer.WriteLine($"{GetTabs(tabLevel + 1)}if (this.oCPU.Flags.{((CPUJumpConditionEnum)asmInstruction.Parameters[0].Value).ToString()}) goto " +
								$"{this.asmInstructions[instructionIndex].LabelName};");
						}
						break;

					case CPUInstructionEnum.JCXZ:
						uiOffset = (uint)asmInstruction.Parameters[0].Value;
						instructionIndex = this.GetInstructionPositionByOffset((ushort)uiOffset);

						if (instructionIndex < 0)
						{
							throw new Exception($"Can't find instruction in function {this.parent.ToString()}.{this.Name} at offset 0x{uiOffset:x4}");
						}
						else
						{
							writer.WriteLine($"{GetTabs(tabLevel + 1)}if (this.oCPU.CX.UInt16 == 0) goto {this.asmInstructions[instructionIndex].LabelName};");
						}
						break;

					case CPUInstructionEnum.LOOP:
						uiOffset = (uint)asmInstruction.Parameters[0].Value;
						instructionIndex = this.GetInstructionPositionByOffset((ushort)uiOffset);

						if (instructionIndex < 0)
						{
							throw new Exception($"Can't find instruction in function {this.parent.ToString()}.{this.Name} at offset 0x{uiOffset:x4}");
						}
						else
						{
							writer.WriteLine($"{GetTabs(tabLevel + 1)}if (this.oCPU.LoopUInt16(this.oCPU.CX)) goto {this.asmInstructions[instructionIndex].LabelName};");
						}
						break;

					case CPUInstructionEnum.JMP:
						parameter = asmInstruction.Parameters[0];
						if (parameter.Type == CPUParameterTypeEnum.Immediate)
						{
							uiOffset = (uint)asmInstruction.Parameters[0].Value;
							instructionIndex = this.GetInstructionPositionByOffset((ushort)uiOffset);

							if (instructionIndex < 0)
							{
								throw new Exception($"Can't find instruction in function {this.parent.ToString()}.{this.Name} at offset 0x{uiOffset:x4}");
							}
							else
							{
								writer.WriteLine($"{GetTabs(tabLevel + 1)}goto {this.asmInstructions[instructionIndex].LabelName};");
							}
						}
						else if (parameter.Type == CPUParameterTypeEnum.Register)
						{
							writer.WriteLine($"{GetTabs(tabLevel + 1)}// Probably a switch statement - near jump to register value");
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.JmpUInt16({parameter.ToCSTextMZ(asmInstruction.OperandSize)});");
						}
						else
						{
							writer.WriteLine($"{GetTabs(tabLevel + 1)}// Probably a switch statement - near jump to indirect address");
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.JmpUInt16(this.oCPU.ReadUInt16({parameter.GetSegmentTextMZ()}, {parameter.ToCSTextMZ(asmInstruction.OperandSize)}));");
						}
						break;

					case CPUInstructionEnum.JMPF:
						parameter = asmInstruction.Parameters[0];
						if (parameter.Type == CPUParameterTypeEnum.SegmentOffset)
						{
							uiOffset = MainProgram.ToLinearAddress(parameter.Segment, parameter.Value);
							if (uiOffset == 0)
							{
								writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.JmpUInt32(this.oCPU.ReadUInt32(this.oCPU.CS.UInt16, 0x{(asmInstruction.Offset + 1):x}));");
							}
							else
							{
								instructionIndex = this.GetInstructionPositionByLinearAddress(uiOffset);

								if (instructionIndex < 0)
								{
									throw new Exception($"Can't find instruction in function {this.parent.ToString()}.{this.Name} at 0x{parameter.Segment:x}:0x{parameter.Value:x}");
								}
								else
								{
									writer.WriteLine($"{GetTabs(tabLevel + 1)}goto {this.asmInstructions[instructionIndex].LabelName};");
								}
							}
						}
						else
						{
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.JmpUInt32(this.oCPU.ReadUInt32({parameter.GetSegmentTextMZ()}, {parameter.ToCSTextMZ(asmInstruction.OperandSize)}));");
						}
						break;

					case CPUInstructionEnum.CALL:
						parameter = asmInstruction.Parameters[0];
						if (verbosity > 0)
						{
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.PushUInt16(0x{asmInstruction.Offset + asmInstruction.Bytes.Count:x4}); // stack management - push return offset");
							writer.WriteLine($"{GetTabs(tabLevel + 1)}// Instruction address 0x{asmInstruction.Segment:x4}:0x{asmInstruction.Offset:x4}, size: {asmInstruction.Bytes.Count}");
						}
						else
						{
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.PushUInt16(0); // stack management - push return offset");
						}

						if (parameter.Type != CPUParameterTypeEnum.Immediate)
						{
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.CallUInt16(this.oCPU.ReadUInt16({parameter.GetSegmentTextMZ()}, {parameter.ToCSTextMZ(asmInstruction.OperandSize)}));");
						}
						else
						{
							function1 = this.parent.Parent.FindFunction(0, asmInstruction.Segment, (ushort)parameter.Value);

							if (function1 != null)
							{
								if ((function1.CallType & ProgramFunctionTypeEnum.Near) != ProgramFunctionTypeEnum.Near &&
									(function1.CallType & ProgramFunctionTypeEnum.Far) == ProgramFunctionTypeEnum.Far)
								{
									Console.WriteLine($"Function '{function1.Name}' doesn't support near return");
								}

								if (this.parent.Segment != function1.Parent.Segment)
								{
									if ((this.callType & ProgramFunctionTypeEnum.CAPI) == ProgramFunctionTypeEnum.CAPI)
									{
										writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oParent.MSCAPI.{function1.Name}();");
									}
									else
									{
										writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oParent.{function1.Parent.ToString()}.{function1.Name}();");
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
											writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oParent.{function1.Parent.ToString()}.{function1.Name}();");
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
						parameter = asmInstruction.Parameters[0];
						if (verbosity > 0)
						{
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.PushUInt16(this.oCPU.CS.UInt16); // stack management - push return segment");
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.PushUInt16(0x{asmInstruction.Offset + asmInstruction.Bytes.Count:x4}); // stack management - push return offset");
							writer.WriteLine($"{GetTabs(tabLevel + 1)}// Instruction address 0x{asmInstruction.Segment:x4}:0x{asmInstruction.Offset:x4}, size: {asmInstruction.Bytes.Count}");
						}
						else
						{
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.PushUInt32(0); // stack management - push return segment, offset");
						}

						if (parameter.Type == CPUParameterTypeEnum.SegmentOffset)
						{
							function1 = this.parent.Parent.FindFunction(0, parameter.Segment, (ushort)parameter.Value);
							if (function1 != null)
							{
								if ((function1.CallType & ProgramFunctionTypeEnum.Far) != ProgramFunctionTypeEnum.Far &&
									(function1.CallType & ProgramFunctionTypeEnum.Near) == ProgramFunctionTypeEnum.Near)
								{
									Console.WriteLine($"Function '{function1.Name}' doesn't support far return");
								}

								if (this.parent.Segment != function1.Parent.Segment)
								{
									if ((function1.CallType & ProgramFunctionTypeEnum.CAPI) == ProgramFunctionTypeEnum.CAPI)
									{
										writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oParent.MSCAPI.{function1.Name}();");
									}
									else
									{
										writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oParent.{function1.Parent.ToString()}.{function1.Name}();");
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
											writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oParent.{function1.Parent.ToString()}.{function1.Name}();");
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
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.CallUInt32(this.oCPU.ReadUInt32({parameter.GetSegmentTextMZ()}, {parameter.ToCSTextMZ(asmInstruction.OperandSize)}));");
						}
						writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.PopUInt32(); // stack management - pop return offset and segment");
						if (verbosity > 0)
						{
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.CS.UInt16 = 0x{this.parent.CPUSegment:x4}; // restore this function segment");
						}
						break;

					case CPUInstructionEnum.CallOverlay:
						writer.WriteLine($"{GetTabs(tabLevel + 1)}// Call to overlay");
						if (verbosity > 0)
						{
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.PushUInt16(this.oCPU.CS.UInt16); // stack management - push return segment");
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.PushUInt16(0x{asmInstruction.Offset + asmInstruction.Bytes.Count:x4}); // stack management - push return offset");
						}
						else
						{
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.PushUInt32(0); // stack management - push return segment, offset");
						}

						function1 = this.parent.Parent.FindFunction((ushort)asmInstruction.Parameters[0].Value, 0, (ushort)asmInstruction.Parameters[1].Value);
						if (function1 != null)
						{
							if ((function1.CallType & ProgramFunctionTypeEnum.Far) != ProgramFunctionTypeEnum.Far && (function1.CallType & ProgramFunctionTypeEnum.Near) == ProgramFunctionTypeEnum.Near)
							{
								Console.WriteLine($"Function '{function1.Name}' doesn't support far return");
							}

							if (this.parent != function1.Parent)
							{
								if ((function1.CallType & ProgramFunctionTypeEnum.CAPI) == ProgramFunctionTypeEnum.CAPI)
								{
									writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oParent.MSCAPI.{function1.Name}();");
								}
								else
								{
									writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oParent.{function1.Parent.ToString()}.{function1.Name}();");
								}
							}
							else
							{
								writer.WriteLine($"{GetTabs(tabLevel + 1)}{function1.Name}();");
							}
						}
						else
						{
							throw new Exception($"Can't find function 'F{asmInstruction.Parameters[0].Value}_0000_{asmInstruction.Parameters[1].Value:x4}'");
						}

						writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.PopUInt32(); // stack management - pop return offset and segment");
						if (verbosity > 0)
						{
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.CS.UInt16 = 0x{this.parent.CPUSegment:x4}; // restore this function segment");
						}
						break;

					case CPUInstructionEnum.RET:
						writer.WriteLine();
						writer.WriteLine($"{GetTabs(tabLevel + 1)}// Near return");
						if (asmInstruction.Parameters.Count > 0)
						{
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.SP.UInt16 = this.oCPU.ADDUInt16(this.oCPU.SP.UInt16, 0x{asmInstruction.Parameters[0].Value:x});");
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
						if (asmInstruction.Parameters.Count > 0)
						{
							writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.SP.UInt16 = this.oCPU.ADDUInt16(this.oCPU.SP.UInt16, 0x{asmInstruction.Parameters[0].Value:x});");
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
						writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.INT(0x{asmInstruction.Parameters[0].Value:x2});");
						break;

					default:
						//throw new Exception($"Unexpected instruction type: {instruction.InstructionType}");
						Console.WriteLine($"Unexpected instruction type '{asmInstruction.InstructionType}' " +
							$"in function {this.parent.ToString()}.{this.Name} at offset 0x{asmInstruction.Offset:x4}");
						break;
				}
			}
			if (!retInstruction)
			{
				writer.WriteLine();
				writer.WriteLine($"{GetTabs(tabLevel + 1)}this.oCPU.Log.ExitBlock(\"'{this.Name}'\");");
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

		public ProgramSegment Parent { get => this.parent; }

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

		public CPUParameterSizeEnum ReturnType { get => this.returnType; set => this.returnType = value; }

		public int LocalStackSize { get => this.stackSize; }

		public FlowGraph? Graph { get => this.flowGraph; }

		public List<CPUInstruction> AsmInstructions { get => this.asmInstructions; }

		public List<ILExpression> ILInstructions { get => this.ilInstructions; }
	}
}
