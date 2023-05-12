using Disassembler.NE;
using IRB.Collections.Generic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Disassembler.Decompiler
{
	[Flags]
	public enum CallTypeEnum
	{
		Undefined = 0,
		Cdecl = 1,
		Pascal = 2,
		Near = 0x10,
		Far = 0x20
	}

	public class CFunction
	{
		private CDecompiler oParent = null;
		private CallTypeEnum eCallType = CallTypeEnum.Cdecl;
		private int iParameterSize = -1;
		private string sName = null;
		private List<CParameter> aParameters = new List<CParameter>();
		private CType oReturnValue = CType.Void;
		private CFunctionNamespace oLocalNamespace = null;
		private int iStackSize = 0;

		// assembly stuff
		private ushort usSegment;
		private ushort usOffset;
		private List<Instruction> aInstructions = new List<Instruction>();

		// parsed statements
		private List<IStatement> aStatements = new List<IStatement>();

		public static BDictionary<InstructionEnum, int> Statistics = new BDictionary<InstructionEnum, int>();

		public CFunction(CDecompiler parent, CallTypeEnum callType, string name, List<CParameter> parameters, CType returnValue, ushort segment, ushort offset)
		{
			this.oParent = parent;
			this.eCallType = callType;
			this.sName = name;
			this.aParameters = parameters;
			this.oReturnValue = returnValue;
			this.usSegment = segment;
			this.usOffset = offset;

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

			this.oLocalNamespace = new CFunctionNamespace(this, parameters);
		}

		public void Disassemble(CDecompiler decompiler)
		{
			Segment segment = decompiler.Executable.Segments[(int)this.usSegment];
			if ((segment.Flags & SegmentFlagsEnum.DataSegment) == SegmentFlagsEnum.DataSegment)
				throw new Exception("You are trying to disassemble a data segment");

			if (this.usOffset < 0 || this.usOffset >= segment.Data.Length)
			{
				throw new Exception(string.Format("Offset {0} outside of segment {1} code data", this.usOffset, this.usSegment));
			}

			MemoryStream stream = new MemoryStream(segment.Data);
			ushort ip = this.usOffset;
			stream.Seek(ip, SeekOrigin.Begin);
			List<ushort> aJumps = new List<ushort>();
			List<ushort> aSwitches = new List<ushort>();
			bool bNextNOP = false;
			Instruction instruction1;

			while (true)
			{
				if (ip >= segment.Data.Length)
				{
					throw new Exception(string.Format("Trying to disassemble beyond segment boundary at function {0}:0x{1:x}, ip 0x{2:x}",
						this.usSegment, this.usOffset, ip));
				}

				bool bEnd = false;
				for (int i = 0; i < this.aInstructions.Count; i++)
				{
					if (this.aInstructions[i].Offset == ip)
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
						case InstructionEnum.JMP:
							parameter = instruction.Parameters[0];

							if (parameter.Type == InstructionParameterTypeEnum.Relative)
							{
								ip = (ushort)((ip + parameter.Value) & 0xffff);
								stream.Seek(ip, SeekOrigin.Begin);
							}
							else if (parameter.Type == InstructionParameterTypeEnum.MemoryAddress && parameter.Value == 6)
							{
								throw new Exception(string.Format("Relative jmp to {0} at function {1}:0x{2:x} - jcc at 0x{3:x}",
									parameter.ToString(), this.usSegment, this.usOffset, instruction.Offset));
							}
							else
							{
								// probably switch statement
								aSwitches.Add(instruction.Offset);
								bEnd = true;
							}
							break;

						case InstructionEnum.JMPF:
							throw new Exception(string.Format("Absolute jump inside function at {0}:0x{1:x} - jmp at 0x{2:x}",
								this.usSegment, this.usOffset, instruction.Offset));

						case InstructionEnum.Jcc:
							parameter = instruction.Parameters[1];
							if (parameter.Type != InstructionParameterTypeEnum.Relative)
								throw new Exception(string.Format("Relative adress offset expected, gut got {0} at function {1}:0x{2:x} - instruction at 0x{3:x}",
									parameter.ToString(), this.usSegment, this.usOffset, instruction.Offset));

							aJumps.Add((ushort)((ip + parameter.Value) & 0xffff));
							break;

						case InstructionEnum.LOOP:
						case InstructionEnum.LOOPNZ:
						case InstructionEnum.LOOPZ:
						case InstructionEnum.JCXZ:
							parameter = instruction.Parameters[0];
							if (parameter.Type != InstructionParameterTypeEnum.Relative)
								throw new Exception(string.Format("Relative adress offset expected, gut got {0} at function {1}:0x{2:x} - instruction at 0x{3:x}",
									parameter.ToString(), this.usSegment, this.usOffset, instruction.Offset));

							aJumps.Add((ushort)((ip + parameter.Value) & 0xffff));
							break;

						case InstructionEnum.IRET:
							throw new Exception(string.Format("Unexpected return from interrupt at function {0}:0x{1:x}",
								this.usSegment, this.usOffset));

						case InstructionEnum.RET:
							throw new Exception(string.Format("Unexpected near return at function {0}:0x{1:x}",
								this.usSegment, this.usOffset));

						case InstructionEnum.RETF:
							if (this.eCallType != CallTypeEnum.Undefined)
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
					}
				}

				if (bEnd)
				{
					// jumps
					if (aJumps.Count > 0)
					{
						ip = aJumps[aJumps.Count - 1];
						aJumps.RemoveAt(aJumps.Count - 1);
						stream.Seek(ip, SeekOrigin.Begin);

						continue;
					}

					// switches
					if (aSwitches.Count > 0)
					{
						// sort instructions by address before doing switches
						this.aInstructions.Sort(Instruction.CompareInstructionByAddress);

						ip = aSwitches[aSwitches.Count - 1];
						aSwitches.RemoveAt(aSwitches.Count - 1);

						int iPos = -1;
						for (int i = 0; i < this.aInstructions.Count; i++)
						{
							if (this.aInstructions[i].Offset == ip)
							{
								iPos = i;
								break;
							}
						}
						if (iPos >= 0)
						{
							if (iPos > 8)
							{
								int iPos1;
								InstructionParameter parameter;
								InstructionParameter jumpParameter;
								uint uiCount = 0;
								ushort usOffset = 0;
								ushort usDefault = 0;

								// first pattern
								if ((instruction1 = this.aInstructions[iPos1 = iPos - 8]).InstructionType == InstructionEnum.MOV &&
									instruction1.Parameters.Count == 2 &&
									instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Register &&
									instruction1.Parameters[0].Value == ((uint)RegisterEnum.CX & 0x7) &&
									instruction1.Parameters[1].Type == InstructionParameterTypeEnum.Immediate &&
									(uiCount = instruction1.Parameters[1].Value) > 0 &&

									(instruction1 = this.aInstructions[++iPos1]).InstructionType == InstructionEnum.MOV &&
									instruction1.Parameters.Count == 2 &&
									instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Register &&
									instruction1.Parameters[0].Value == ((uint)RegisterEnum.BX & 0x7) &&
									instruction1.Parameters[1].Type == InstructionParameterTypeEnum.Immediate &&
									(usOffset = (ushort)instruction1.Parameters[1].Value) > 0 &&

									(instruction1 = this.aInstructions[++iPos1]).InstructionType == InstructionEnum.MOV &&
									instruction1.Parameters.Count == 2 &&
									instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Register &&
									instruction1.Parameters[0].Value == ((uint)RegisterEnum.AX & 0x7) &&
									instruction1.Parameters[1].Type == InstructionParameterTypeEnum.MemoryAddress &&

									(instruction1 = this.aInstructions[++iPos1]).InstructionType == InstructionEnum.CMP &&
									instruction1.Parameters.Count == 2 &&
									instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Register &&
									instruction1.Parameters[0].Value == ((uint)RegisterEnum.AX & 0x7) &&
									instruction1.Parameters[1].Type == InstructionParameterTypeEnum.MemoryAddress &&
									(parameter = instruction1.Parameters[1]) != null &&

									(instruction1 = this.aInstructions[++iPos1]).InstructionType == InstructionEnum.Jcc &&
									instruction1.Parameters.Count == 2 &&
									instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Condition &&
									instruction1.Parameters[0].Value == ((uint)ConditionEnum.E) &&
									instruction1.Parameters[1].Type == InstructionParameterTypeEnum.Relative &&
									(instruction1.Parameters[1].Value == 7 || instruction1.Parameters[1].Value == 8) &&

									(instruction1 = this.aInstructions[++iPos1]).InstructionType == InstructionEnum.ADD &&
									instruction1.Parameters.Count == 2 &&
									instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Register &&
									instruction1.Parameters[0].Value == ((uint)RegisterEnum.BX & 0x7) &&
									instruction1.Parameters[1].Type == InstructionParameterTypeEnum.Immediate &&
									instruction1.Parameters[1].Value == 2 &&

									(instruction1 = this.aInstructions[++iPos1]).InstructionType == InstructionEnum.LOOP &&
									instruction1.Parameters.Count == 1 &&
									instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Relative &&
									instruction1.Parameters[0].Value == 0xfff3 &&

									(instruction1 = this.aInstructions[++iPos1]).InstructionType == InstructionEnum.JMP &&
									instruction1.Parameters.Count == 1 &&
									instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Relative &&
									(usDefault = (ushort)((instruction1.Offset + (uint)instruction1.Bytes.Count + instruction1.Parameters[0].Value) & 0xffff)) > 0 &&

									(instruction1 = this.aInstructions[++iPos1]).InstructionType == InstructionEnum.JMP &&
									instruction1.Parameters.Count == 1 &&
									instruction1.Parameters[0].Type == InstructionParameterTypeEnum.MemoryAddress &&
									(jumpParameter = instruction1.Parameters[0]) != null &&

									iPos == iPos1
									)
								{
									//Console.WriteLine("Switch type 1 at {0}:0x{1:x4}", this.uiSegment, this.aInstructions[iPos].Location.Offset);

									this.aInstructions[iPos - 8].InstructionType = InstructionEnum.NOP;
									this.aInstructions[iPos - 7].InstructionType = InstructionEnum.NOP;
									this.aInstructions[iPos - 6].InstructionType = InstructionEnum.NOP;
									this.aInstructions[iPos - 5].InstructionType = InstructionEnum.NOP;
									this.aInstructions[iPos - 4].InstructionType = InstructionEnum.NOP;
									this.aInstructions[iPos - 3].InstructionType = InstructionEnum.NOP;
									this.aInstructions[iPos - 2].InstructionType = InstructionEnum.NOP;
									this.aInstructions[iPos - 1].InstructionType = InstructionEnum.NOP;
									instruction1 = this.aInstructions[iPos];
									instruction1.InstructionType = InstructionEnum.SWITCH;
									instruction1.Parameters.Clear();

									// switching parameter
									instruction1.Parameters.Add(parameter);

									stream.Seek(usOffset, SeekOrigin.Begin);
									// read values
									for (int i = 0; i < uiCount; i++)
									{
										instruction1.Parameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Immediate, ReadWord(stream)));
									}
									// last value is the default value
									instruction1.Parameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Immediate, 0));

									for (int i = 0; i < uiCount; i++)
									{
										ushort usWord = ReadWord(stream);
										aJumps.Add(usWord);
										instruction1.Parameters[i + 1].Displacement = usWord;
									}
									aJumps.Add(usDefault);
									instruction1.Parameters[(int)uiCount + 1].Displacement = usDefault;

									ip = aJumps[aJumps.Count - 1];
									aJumps.RemoveAt(aJumps.Count - 1);
									stream.Seek(ip, SeekOrigin.Begin);
								}
								else if ((instruction1 = this.aInstructions[iPos1 = iPos - 3]).InstructionType == InstructionEnum.CMP &&
									instruction1.Parameters.Count == 2 &&
									instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Register &&
									instruction1.Parameters[0].Value == ((uint)RegisterEnum.BX & 0x7) &&
									(parameter = instruction1.Parameters[0]) != null &&
									instruction1.Parameters[1].Type == InstructionParameterTypeEnum.Immediate &&
									(uiCount = instruction1.Parameters[1].Value) > 0 &&

									(instruction1 = this.aInstructions[++iPos1]).InstructionType == InstructionEnum.Jcc &&
									instruction1.Parameters.Count == 2 &&
									instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Condition &&
									instruction1.Parameters[0].Value == ((uint)ConditionEnum.A) &&
									instruction1.Parameters[1].Type == InstructionParameterTypeEnum.Relative &&
									(usDefault = (ushort)((instruction1.Offset + (uint)instruction1.Bytes.Count + instruction1.Parameters[1].Value) & 0xffff)) > 0 &&

									(instruction1 = this.aInstructions[++iPos1]).InstructionType == InstructionEnum.SHL &&
									instruction1.Parameters.Count == 2 &&
									instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Register &&
									instruction1.Parameters[0].Value == ((uint)RegisterEnum.BX & 0x7) &&
									instruction1.Parameters[1].Type == InstructionParameterTypeEnum.Immediate &&
									instruction1.Parameters[1].Value == 1 &&

									(instruction1 = this.aInstructions[++iPos1]).InstructionType == InstructionEnum.JMP &&
									instruction1.Parameters[0].Type == InstructionParameterTypeEnum.MemoryAddress &&
									(usOffset = (ushort)instruction1.Parameters[0].Displacement) > 0 &&

									iPos == iPos1
									)
								{
									// Console.WriteLine("Switch type 2 at {0}:0x{1:x4}", this.uiSegment, this.aInstructions[iPos].Location.Offset);

									this.aInstructions[iPos - 3].InstructionType = InstructionEnum.NOP;
									this.aInstructions[iPos - 2].InstructionType = InstructionEnum.NOP;
									this.aInstructions[iPos - 1].InstructionType = InstructionEnum.NOP;
									instruction1 = this.aInstructions[iPos];
									instruction1.InstructionType = InstructionEnum.SWITCH;
									instruction1.Parameters.Clear();

									// switching parameter
									instruction1.Parameters.Add(parameter);

									stream.Seek(usOffset, SeekOrigin.Begin);

									for (int i = 0; i <= uiCount; i++)
									{
										ushort usWord = ReadWord(stream);
										aJumps.Add(usWord);
										parameter = new InstructionParameter(InstructionParameterTypeEnum.Immediate, (uint)i);
										parameter.Displacement = usWord;
										instruction1.Parameters.Add(parameter);
									}

									aJumps.Add(usDefault);
									// last value is the default value
									parameter = new InstructionParameter(InstructionParameterTypeEnum.Immediate, 0);
									parameter.Displacement = usDefault;
									instruction1.Parameters.Add(parameter);

									ip = aJumps[aJumps.Count - 1];
									aJumps.RemoveAt(aJumps.Count - 1);
									stream.Seek(ip, SeekOrigin.Begin);
								}
								else if ((instruction1 = this.aInstructions[iPos1 = iPos - 4]).InstructionType == InstructionEnum.CMP &&
									instruction1.Parameters.Count == 2 &&
									instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Register &&
									instruction1.Parameters[0].Value == ((uint)RegisterEnum.BX & 0x7) &&
									(parameter = instruction1.Parameters[0]) != null &&
									instruction1.Parameters[1].Type == InstructionParameterTypeEnum.Immediate &&
									(uiCount = instruction1.Parameters[1].Value) > 0 &&

									(instruction1 = this.aInstructions[++iPos1]).InstructionType == InstructionEnum.Jcc &&
									instruction1.Parameters.Count == 2 &&
									instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Condition &&
									instruction1.Parameters[0].Value == ((uint)ConditionEnum.BE) &&
									instruction1.Parameters[1].Type == InstructionParameterTypeEnum.Relative &&

									(instruction1 = this.aInstructions[++iPos1]).InstructionType == InstructionEnum.JMP &&
									instruction1.Parameters.Count == 1 &&
									instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Relative &&
									(usDefault = (ushort)((instruction1.Offset + (uint)instruction1.Bytes.Count + instruction1.Parameters[0].Value) & 0xffff)) > 0 &&

									(instruction1 = this.aInstructions[++iPos1]).InstructionType == InstructionEnum.SHL &&
									instruction1.Parameters.Count == 2 &&
									instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Register &&
									instruction1.Parameters[0].Value == ((uint)RegisterEnum.BX & 0x7) &&
									instruction1.Parameters[1].Type == InstructionParameterTypeEnum.Immediate &&
									instruction1.Parameters[1].Value == 1 &&

									(instruction1 = this.aInstructions[++iPos1]).InstructionType == InstructionEnum.JMP &&
									instruction1.Parameters[0].Type == InstructionParameterTypeEnum.MemoryAddress &&
									(usOffset = (ushort)instruction1.Parameters[0].Displacement) > 0 &&

									iPos == iPos1
									)
								{
									//Console.WriteLine("Switch type 3 at {0}:0x{1:x4}", this.uiSegment, this.aInstructions[iPos].Location.Offset);

									this.aInstructions[iPos - 4].InstructionType = InstructionEnum.NOP;
									this.aInstructions[iPos - 3].InstructionType = InstructionEnum.NOP;
									this.aInstructions[iPos - 2].InstructionType = InstructionEnum.NOP;
									this.aInstructions[iPos - 1].InstructionType = InstructionEnum.NOP;
									instruction1 = this.aInstructions[iPos];
									instruction1.InstructionType = InstructionEnum.SWITCH;
									instruction1.Parameters.Clear();

									// switching parameter
									instruction1.Parameters.Add(parameter);

									stream.Seek(usOffset, SeekOrigin.Begin);

									for (int i = 0; i <= uiCount; i++)
									{
										ushort usWord = ReadWord(stream);
										aJumps.Add(usWord);
										parameter = new InstructionParameter(InstructionParameterTypeEnum.Immediate, (uint)i);
										parameter.Displacement = usWord;
										instruction1.Parameters.Add(parameter);
									}

									aJumps.Add(usDefault);
									// last value is the default value
									parameter = new InstructionParameter(InstructionParameterTypeEnum.Immediate, 0);
									parameter.Displacement = usDefault;
									instruction1.Parameters.Add(parameter);

									ip = aJumps[aJumps.Count - 1];
									aJumps.RemoveAt(aJumps.Count - 1);
									stream.Seek(ip, SeekOrigin.Begin);
								}
								else
								{
									Instruction instruction = this.aInstructions[iPos];
									Console.WriteLine("Undefined switch pattern to {0} at function {1}:0x{2:x} - jmp at 0x{3:x}",
										instruction.Parameters[0].ToString(), this.usSegment, this.usOffset, instruction.Offset);
									break;
								}
							}
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

			#region check if function format is OK
			if ((instruction1 = this.aInstructions[iStart]).InstructionType == InstructionEnum.MOV &&
				instruction1.Parameters.Count == 2 &&
				instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Register &&
				instruction1.Parameters[0].Value == ((uint)RegisterEnum.AX & 0x7) &&
				instruction1.Parameters[1].Type == InstructionParameterTypeEnum.SegmentRegister &&
				instruction1.Parameters[1].Value == (uint)SegmentRegisterEnum.DS &&

				(instruction1 = this.aInstructions[iStart + 1]).InstructionType == InstructionEnum.NOP &&

				(instruction1 = this.aInstructions[iStart + 2]).InstructionType == InstructionEnum.INC &&
				instruction1.Parameters.Count == 1 &&
				instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Register &&
				instruction1.Parameters[0].Value == ((uint)RegisterEnum.BP & 0x7) &&

				(instruction1 = this.aInstructions[iStart + 3]).InstructionType == InstructionEnum.PUSH &&
				instruction1.Parameters.Count == 1 &&
				instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Register &&
				instruction1.Parameters[0].Value == ((uint)RegisterEnum.BP & 0x7) &&

				(instruction1 = this.aInstructions[iStart + 4]).InstructionType == InstructionEnum.MOV &&
				instruction1.Parameters.Count == 2 &&
				instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Register &&
				instruction1.Parameters[0].Value == ((uint)RegisterEnum.BP & 0x7) &&
				instruction1.Parameters[1].Type == InstructionParameterTypeEnum.Register &&
				instruction1.Parameters[1].Value == ((uint)RegisterEnum.SP & 0x7) &&

				(instruction1 = this.aInstructions[iStart + 5]).InstructionType == InstructionEnum.PUSH &&
				instruction1.Parameters.Count == 1 &&
				instruction1.Parameters[0].Type == InstructionParameterTypeEnum.SegmentRegister &&
				instruction1.Parameters[0].Value == ((uint)SegmentRegisterEnum.DS) &&

				(instruction1 = this.aInstructions[iStart + 6]).InstructionType == InstructionEnum.MOV &&
				instruction1.Parameters.Count == 2 &&
				instruction1.Parameters[0].Type == InstructionParameterTypeEnum.SegmentRegister &&
				instruction1.Parameters[0].Value == ((uint)SegmentRegisterEnum.DS) &&
				instruction1.Parameters[1].Type == InstructionParameterTypeEnum.Register &&
				instruction1.Parameters[1].Value == ((uint)RegisterEnum.AX & 0x7) &&

				(instruction1 = this.aInstructions[iEnd]).InstructionType == InstructionEnum.RETF &&

				(instruction1 = this.aInstructions[iEnd - 1]).InstructionType == InstructionEnum.DEC &&
				instruction1.Parameters.Count == 1 &&
				instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Register &&
				instruction1.Parameters[0].Value == ((uint)RegisterEnum.BP & 0x7) &&

				(instruction1 = this.aInstructions[iEnd - 2]).InstructionType == InstructionEnum.POP &&
				instruction1.Parameters.Count == 1 &&
				instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Register &&
				instruction1.Parameters[0].Value == ((uint)RegisterEnum.BP & 0x7) &&

				(instruction1 = this.aInstructions[iEnd - 3]).InstructionType == InstructionEnum.POP &&
				instruction1.Parameters.Count == 1 &&
				instruction1.Parameters[0].Type == InstructionParameterTypeEnum.SegmentRegister &&
				instruction1.Parameters[0].Value == ((uint)SegmentRegisterEnum.DS)
				)
			{
				this.aInstructions[iStart].InstructionType = InstructionEnum.NOP;
				this.aInstructions[iStart + 1].InstructionType = InstructionEnum.NOP;
				this.aInstructions[iStart + 2].InstructionType = InstructionEnum.NOP;
				this.aInstructions[iStart + 3].InstructionType = InstructionEnum.NOP;
				this.aInstructions[iStart + 4].InstructionType = InstructionEnum.NOP;
				this.aInstructions[iStart + 5].InstructionType = InstructionEnum.NOP;
				this.aInstructions[iStart + 6].InstructionType = InstructionEnum.NOP;

				iStart += 7;

				if ((instruction1 = this.aInstructions[iStart]).InstructionType == InstructionEnum.SUB &&
					instruction1.Parameters.Count == 2 &&
					instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Register &&
					instruction1.Parameters[0].Value == ((uint)RegisterEnum.SP & 0x7) &&
					instruction1.Parameters[1].Type == InstructionParameterTypeEnum.Immediate)
				{
					this.iStackSize = (int)instruction1.Parameters[1].Value;
					instruction1.InstructionType = InstructionEnum.NOP;
					iStart++;

					// check alignement just in case
					if ((this.iStackSize & 1) != 0)
						throw new Exception("Stack alignment error");
				}

				// minimisation, remove push si or push di at the beginning of function
				if ((instruction1 = this.aInstructions[iStart]).InstructionType == InstructionEnum.PUSH &&
					instruction1.Parameters.Count == 1 &&
					instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Register &&
					(instruction1.Parameters[0].Value == ((uint)RegisterEnum.SI & 0x7) ||
					instruction1.Parameters[0].Value == ((uint)RegisterEnum.DI & 0x7)))
				{
					instruction1.InstructionType = InstructionEnum.NOP;
					iStart++;
				}

				if ((instruction1 = this.aInstructions[iStart]).InstructionType == InstructionEnum.PUSH &&
					instruction1.Parameters.Count == 1 &&
					instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Register &&
					(instruction1.Parameters[0].Value == ((uint)RegisterEnum.SI & 0x7) ||
					instruction1.Parameters[0].Value == ((uint)RegisterEnum.DI & 0x7)))
				{
					instruction1.InstructionType = InstructionEnum.NOP;
					iStart++;
				}

				this.aInstructions[iEnd - 1].InstructionType = InstructionEnum.NOP;
				this.aInstructions[iEnd - 2].InstructionType = InstructionEnum.NOP;
				this.aInstructions[iEnd - 3].InstructionType = InstructionEnum.NOP;

				iEnd -= 4;

				// remove reserved local stack space instructions
				if (this.iStackSize > 0)
				{
					bool bPushes = false;

					if (this.iStackSize <= 4)
					{
						// simple push statements
						bPushes = true;

						if ((instruction1 = this.aInstructions[iEnd]).InstructionType == InstructionEnum.POP &&
							instruction1.Parameters.Count == 1 &&
							instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Register)
						{
							instruction1.InstructionType = InstructionEnum.NOP;
							iEnd--;
						}
						else
						{
							bPushes = false;
						}

						if (bPushes && this.iStackSize > 2)
						{
							if ((instruction1 = this.aInstructions[iEnd]).InstructionType == InstructionEnum.POP &&
							instruction1.Parameters.Count == 1 &&
							instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Register)
							{
								instruction1.InstructionType = InstructionEnum.NOP;
								iEnd--;
							}
							else
							{
								throw new Exception(string.Format("Improperly restored stack address at function {0}:0x{1:x}",
									this.usSegment, this.usOffset));
							}
						}
					}

					if (!bPushes)
					{
						if ((instruction1 = this.aInstructions[iEnd]).InstructionType == InstructionEnum.LEA &&
							instruction1.Parameters.Count == 2 &&
							instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Register &&
							instruction1.Parameters[0].Value == ((uint)RegisterEnum.SP & 0x7))
						{
							instruction1.InstructionType = InstructionEnum.NOP;
							iEnd--;
						}
						else
						{
							throw new Exception(string.Format("Improperly restored stack address at function {0}:0x{1:x}", 
								this.usSegment, this.usOffset));
						}
					}
				}

				// minimisation, remove pop si or pop di at the end of the function
				if ((instruction1 = this.aInstructions[iEnd]).InstructionType == InstructionEnum.POP &&
					instruction1.Parameters.Count == 1 &&
					instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Register &&
					(instruction1.Parameters[0].Value == ((uint)RegisterEnum.SI & 0x7) ||
					instruction1.Parameters[0].Value == ((uint)RegisterEnum.DI & 0x7)))
				{
					instruction1.InstructionType = InstructionEnum.NOP;
					iEnd--;
				}

				if ((instruction1 = this.aInstructions[iEnd]).InstructionType == InstructionEnum.POP &&
					instruction1.Parameters.Count == 1 &&
					instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Register &&
					(instruction1.Parameters[0].Value == ((uint)RegisterEnum.SI & 0x7) ||
					instruction1.Parameters[0].Value == ((uint)RegisterEnum.DI & 0x7)))
				{
					instruction1.InstructionType = InstructionEnum.NOP;
					iEnd--;
				}
			}
			else
			{
				throw new Exception(string.Format("Unknown function format at function {0}:0x{1:x}", this.usSegment, this.usOffset));
			}
			#endregion

			#region assign segment and offset references from relocations table
			for (int i = 0; i < this.aInstructions.Count; i++)
			{
				Instruction instruction = this.aInstructions[i];

				for (int j = 0; j < segment.Relocations.Count; j++)
				{
					Relocation relocation = segment.Relocations[j];
					if (relocation.Offset >= instruction.Offset && relocation.Offset - instruction.Offset < instruction.Bytes.Count)
					{
						switch (relocation.RelocationType)
						{
							case RelocationTypeEnum.InternalReference:
								// relocation.Parameter1, which is segment, is one (1) based
								switch (relocation.LocationType)
								{
									case LocationTypeEnum.Offset16:
										for (int k = 0; k < instruction.Parameters.Count; k++)
										{
											if (instruction.Parameters[k].Type == InstructionParameterTypeEnum.Immediate &&
												instruction.Parameters[k].Size == InstructionSizeEnum.Word &&
												instruction.Parameters[k].Value == (uint)relocation.Parameter2)
											{
												instruction.Parameters[k].ReferenceType = InstructionParameterReferenceEnum.Offset;
												break;
											}
										}
										break;
									case LocationTypeEnum.Segment16:
										for (int k = 0; k < instruction.Parameters.Count; k++)
										{
											if (instruction.Parameters[k].Type == InstructionParameterTypeEnum.Immediate &&
												instruction.Parameters[k].Size == InstructionSizeEnum.Word &&
												instruction.Parameters[k].Value == (uint)(relocation.Parameter1 - 1))
											{
												instruction.Parameters[k].ReferenceType = InstructionParameterReferenceEnum.Segment;
												break;
											}
										}
										break;
									case LocationTypeEnum.SegmentOffset32:
										break;
								}
								break;
							case RelocationTypeEnum.ImportedOrdinal:
								// relocation.Parameter1, which is module index and segment, is one (1) based
								switch (relocation.LocationType)
								{
									case LocationTypeEnum.Offset16:
										for (int k = 0; k < instruction.Parameters.Count; k++)
										{
											// special case for __AHSHIFT
											if (instruction.Parameters[k].Type == InstructionParameterTypeEnum.Immediate &&
												instruction.Parameters[k].Size == InstructionSizeEnum.Word &&
												(instruction.Parameters[k].Value == (uint)relocation.Parameter2 ||
												(relocation.Parameter1 == 1 && relocation.Parameter2 == 113)))
											{
												instruction.Parameters[k].ReferenceType = InstructionParameterReferenceEnum.Offset;
												break;
											}
										}
										break;
									case LocationTypeEnum.Segment16:
										for (int k = 0; k < instruction.Parameters.Count; k++)
										{
											if (instruction.Parameters[k].Type == InstructionParameterTypeEnum.Immediate &&
												instruction.Parameters[k].Size == InstructionSizeEnum.Word &&
												instruction.Parameters[k].Value == (uint)(decompiler.Executable.Segments.Count + relocation.Parameter1 - 1))
											{
												instruction.Parameters[k].ReferenceType = InstructionParameterReferenceEnum.Segment;
												break;
											}
										}
										break;
									case LocationTypeEnum.SegmentOffset32:
										// ignore those
										break;
								}
								break;
							case RelocationTypeEnum.Additive:
								// relocation.Parameter1, which is segment, is one (1) based
								switch (relocation.LocationType)
								{
									case LocationTypeEnum.Segment16:
										for (int k = 0; k < instruction.Parameters.Count; k++)
										{
											if (instruction.Parameters[k].Type == InstructionParameterTypeEnum.Immediate &&
												instruction.Parameters[k].Size == InstructionSizeEnum.Word &&
												instruction.Parameters[k].Value == (uint)(relocation.Parameter1 - 1))
											{
												instruction.Parameters[k].ReferenceType = InstructionParameterReferenceEnum.Segment;
												break;
											}
										}
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
				}
			}
			#endregion

			#region Optimize GoTo's
			for (int i = 0; i < this.aInstructions.Count; i++)
			{
				Instruction instruction = this.aInstructions[i];
				InstructionParameter parameter;
				uint uiNewOffset;
				uint uiNewOffset2;

				switch (instruction.InstructionType)
				{
					case InstructionEnum.LOOP:
					case InstructionEnum.LOOPZ:
					case InstructionEnum.LOOPNZ:
					case InstructionEnum.JCXZ:
					case InstructionEnum.JMP:
						parameter = instruction.Parameters[0];
						// force resulting ip to 16 bit result
						uiNewOffset = (instruction.Offset + (uint)instruction.Bytes.Count + parameter.Value) & 0xffff;

						// optimize immediate jumps
						uiNewOffset2 = uiNewOffset;
						while ((instruction1 = this.aInstructions[GetPositionFromOffset(uiNewOffset2)]).InstructionType == InstructionEnum.JMP)
						{
							uiNewOffset2 = (instruction1.Offset + (uint)instruction1.Bytes.Count + instruction1.Parameters[0].Value) & 0xffff;
						}
						if (uiNewOffset != uiNewOffset2)
						{
							parameter.Value = uiNewOffset2 - (instruction.Offset + (uint)instruction.Bytes.Count);
						}
						break;
					case InstructionEnum.Jcc:
						parameter = instruction.Parameters[1];
						// force resulting ip to 16 bit result
						uiNewOffset = (instruction.Offset + (uint)instruction.Bytes.Count + parameter.Value) & 0xffff;

						// optimize immediate jumps
						uiNewOffset2 = uiNewOffset;
						while ((instruction1 = this.aInstructions[GetPositionFromOffset(uiNewOffset2)]).InstructionType == InstructionEnum.JMP)
						{
							uiNewOffset2 = (instruction1.Offset + (uint)instruction1.Bytes.Count + instruction1.Parameters[0].Value) & 0xffff;
						}
						if (uiNewOffset != uiNewOffset2)
						{
							parameter.Value = uiNewOffset2 - (instruction.Offset + (uint)instruction.Bytes.Count);
						}
						break;
				}
			}
			#endregion

			#region assign labels to instructions, detect hidden functions, convert relative call to far call
			for (int i = 0; i < this.aInstructions.Count; i++)
			{
				Instruction instruction = this.aInstructions[i];
				InstructionParameter parameter;
				uint uiNewOffset;

				switch (instruction.InstructionType)
				{
					case InstructionEnum.CALL:
						parameter = instruction.Parameters[0];
						if (parameter.Type != InstructionParameterTypeEnum.Relative)
							throw new Exception(string.Format("Relative adress offset expected, but got {0} at function {1}:0x{2:x} - instruction at 0x{3:x}",
								parameter.ToString(), this.usSegment, this.usOffset, instruction.Offset));

						// force resulting ip to 16 bit result
						uiNewOffset = (instruction.Offset + (uint)instruction.Bytes.Count + parameter.Value) & 0xffff;

						instruction.InstructionType = InstructionEnum.CALLF;
						instruction.Parameters.RemoveAt(0);
						instruction.Parameters.Add(new InstructionParameter(this.usSegment, uiNewOffset));

						// is instruction prefixed by PUSH CS?
						instruction1 = this.aInstructions[i - 1];

						if (instruction1.InstructionType == InstructionEnum.PUSH && instruction1.Parameters.Count == 1 &&
							instruction1.Parameters[0].Type == InstructionParameterTypeEnum.SegmentRegister &&
							instruction1.Parameters[0].Value == (uint)SegmentRegisterEnum.CS)
						{
							// turn push cs instruction to nop, as it is not needed anymore
							instruction1.InstructionType = InstructionEnum.NOP;
						}
						break;

					case InstructionEnum.JMP:
						parameter = instruction.Parameters[0];
						// force resulting ip to 16 bit result
						uiNewOffset = (instruction.Offset + (uint)instruction.Bytes.Count + parameter.Value) & 0xffff;

						// optimize immediate jumps
						if (i + 1 < this.aInstructions.Count && uiNewOffset > 0 &&
							this.aInstructions[i + 1].Offset == uiNewOffset)
						{
							// this is just a jump to next instruction, ignore it
							this.aInstructions[i].InstructionType = InstructionEnum.NOP;
						}
						else if (uiNewOffset > 0)
						{
							this.aInstructions[GetPositionFromOffset(uiNewOffset)].Label = true;
						}
						break;
					case InstructionEnum.Jcc:
						parameter = instruction.Parameters[1];
						// force resulting ip to 16 bit result
						uiNewOffset = (instruction.Offset + (uint)instruction.Bytes.Count + parameter.Value) & 0xffff;

						if (uiNewOffset > 0)
						{
							this.aInstructions[GetPositionFromOffset(uiNewOffset)].Label = true;
						}
						break;
					case InstructionEnum.LOOP:
					case InstructionEnum.LOOPZ:
					case InstructionEnum.LOOPNZ:
					case InstructionEnum.JCXZ:
						parameter = instruction.Parameters[0];
						// force resulting ip to 16 bit result
						uiNewOffset = (instruction.Offset + (uint)instruction.Bytes.Count + parameter.Value) & 0xffff;

						if (uiNewOffset > 0)
						{
							this.aInstructions[GetPositionFromOffset(uiNewOffset)].Label = true;
						}
						break;
					case InstructionEnum.PUSH:
						parameter = instruction.Parameters[0];

						if (parameter.ReferenceType == InstructionParameterReferenceEnum.Segment)
						{
							ushort usSegment = (ushort)parameter.Value;

							if ((this.oParent.Executable.Segments[usSegment].Flags & SegmentFlagsEnum.DataSegment) != SegmentFlagsEnum.DataSegment)
							{
								// this should be code segment
								if (i + 1 < this.aInstructions.Count &&
									(instruction = this.aInstructions[i + 1]).InstructionType == InstructionEnum.PUSH &&
									instruction.Parameters.Count == 1 &&
									(parameter = instruction.Parameters[0]).Type == InstructionParameterTypeEnum.Immediate &&
									parameter.Size == InstructionSizeEnum.Word)
								{
									CFunction function = decompiler.GetFunction(usSegment, (ushort)parameter.Value);
									if (function == null)
									{
										// function is not yet defined, define it
										decompiler.Decompile(string.Format("F{0}_{1:x}", usSegment, parameter.Value),
											CallTypeEnum.Undefined, new List<CParameter>(), CType.Void, usSegment, (ushort)parameter.Value);
									}
								}
							}
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
			}
			#endregion

			#region Optimize Jcc's
			for (int i = 0; i < this.aInstructions.Count; i++)
			{
				uint uiNewOffset;
				uint uiNewOffset2;

				if (i + 2 < this.aInstructions.Count &&
					(instruction1 = this.aInstructions[i]).InstructionType == InstructionEnum.Jcc &&
					instruction1.Parameters.Count == 2 &&
					(uiNewOffset = (instruction1.Offset + (uint)instruction1.Bytes.Count + instruction1.Parameters[1].Value) & 0xffff) >= 0 &&

					(instruction1 = this.aInstructions[i + 1]).InstructionType == InstructionEnum.JMP &&
					instruction1.Parameters.Count == 1 &&
					(uiNewOffset2 = (instruction1.Offset + (uint)instruction1.Bytes.Count + instruction1.Parameters[0].Value) & 0xffff) >= 0 &&

					(instruction1 = this.aInstructions[i + 2]).Offset == uiNewOffset)
				{
					Instruction instruction = this.aInstructions[i];
					
					// goto can't be referenced to in this combination
					if (this.aInstructions[i + 1].Label)
						throw new Exception(string.Format("Unexpected label at function {0}:0x{1:x}, position 0x{2:x}",
							this.usSegment, this.usOffset, instruction.Offset));

					this.aInstructions[i + 1].InstructionType = InstructionEnum.NOP;

					InstructionParameter parameter = instruction.Parameters[0];
					parameter.Value = (uint)NegateCondition((ConditionEnum)parameter.Value);
					instruction.Parameters[1].Value = uiNewOffset2 - (instruction.Offset + (uint)instruction.Bytes.Count);
				}
			}
			#endregion

			#region Reassign labels
			for (int i = 0; i < this.aInstructions.Count; i++)
			{
				this.aInstructions[i].Label = false;
			}

			for (int i = 0; i < this.aInstructions.Count; i++)
			{
				Instruction instruction = this.aInstructions[i];
				InstructionParameter parameter;
				uint uiNewOffset;

				switch (instruction.InstructionType)
				{
					case InstructionEnum.JMP:
						parameter = instruction.Parameters[0];
						// force resulting ip to 16 bit result
						uiNewOffset = (instruction.Offset + (uint)instruction.Bytes.Count + parameter.Value) & 0xffff;

						if (uiNewOffset > 0)
						{
							this.aInstructions[GetPositionFromOffset(uiNewOffset)].Label = true;
						}
						break;
					case InstructionEnum.Jcc:
						parameter = instruction.Parameters[1];
						// force resulting ip to 16 bit result
						uiNewOffset = (instruction.Offset + (uint)instruction.Bytes.Count + parameter.Value) & 0xffff;

						if (uiNewOffset > 0)
						{
							this.aInstructions[GetPositionFromOffset(uiNewOffset)].Label = true;
						}
						break;
					case InstructionEnum.LOOP:
					case InstructionEnum.LOOPZ:
					case InstructionEnum.LOOPNZ:
					case InstructionEnum.JCXZ:
						parameter = instruction.Parameters[0];
						// force resulting ip to 16 bit result
						uiNewOffset = (instruction.Offset + (uint)instruction.Bytes.Count + parameter.Value) & 0xffff;

						if (uiNewOffset > 0)
						{
							this.aInstructions[GetPositionFromOffset(uiNewOffset)].Label = true;
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
			}
			#endregion

			#region Optimize two GoTo's
			for (int i = 0; i < this.aInstructions.Count; i++)
			{
				if ((instruction1 = this.aInstructions[i]).InstructionType == InstructionEnum.JMP &&

					(instruction1 = this.aInstructions[i + 1]).InstructionType == InstructionEnum.JMP &&
					!instruction1.Label)
				{
					instruction1.InstructionType = InstructionEnum.NOP;
				}
			}
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

			#region check for explicit segment references, optimize them
			for (int i = 0; i < this.aInstructions.Count; i++)
			{
				// match ES data reference
				ushort usSegmentValue = 0;
				uint sourceReg;

				// form 1
				if (i + 2 < this.aInstructions.Count &&
					(instruction1 = this.aInstructions[i]).InstructionType == InstructionEnum.MOV &&
					instruction1.OperandSize == InstructionSizeEnum.Word &&
					instruction1.Parameters.Count == 2 &&
					instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Register &&
					(sourceReg = instruction1.Parameters[0].Value) <= 7 &&
					instruction1.Parameters[1].Type == InstructionParameterTypeEnum.Immediate &&
					(usSegmentValue = (ushort)instruction1.Parameters[1].Value) >= 0 &&

					(instruction1 = this.aInstructions[i + 1]).InstructionType == InstructionEnum.MOV &&
					instruction1.OperandSize == InstructionSizeEnum.Word &&
					instruction1.Parameters.Count == 2 &&
					instruction1.Parameters[0].Type == InstructionParameterTypeEnum.SegmentRegister &&
					instruction1.Parameters[0].Value == (uint)SegmentRegisterEnum.ES &&
					instruction1.Parameters[1].Type == InstructionParameterTypeEnum.Register &&
					instruction1.Parameters[1].Value == sourceReg)
				{
					int iPos = i + 2;
					int iCount = 0;
					bool bMatch = true;

					while (bMatch && iPos < this.aInstructions.Count)
					{
						Instruction instruction = this.aInstructions[iPos];
						bMatch = false;
						if (instruction.Parameters.Count > 0 && instruction.Parameters[0].DataSegment == SegmentRegisterEnum.ES)
						{
							this.aInstructions[iPos].Parameters[0].DataSegment = SegmentRegisterEnum.Immediate;
							this.aInstructions[iPos].Parameters[0].Segment = usSegmentValue;
							bMatch = true;
						}
						if (instruction.Parameters.Count > 1 && instruction.Parameters[1].DataSegment == SegmentRegisterEnum.ES)
						{
							this.aInstructions[iPos].Parameters[1].DataSegment = SegmentRegisterEnum.Immediate;
							this.aInstructions[iPos].Parameters[1].Segment = usSegmentValue;
							bMatch = true;
						}
						if (bMatch)
						{
							iPos++;
							iCount++;
						}
						if (instruction.InstructionType == InstructionEnum.LES)
						{
							// LES is the last instruction that uses ES
							break;
						}
					}

					if (iCount > 0)
					{
						if (CheckIfUsed(i + 2, iEnd, InstructionSizeEnum.Word, InstructionParameterTypeEnum.Register, sourceReg))
						{
							Console.WriteLine("Found used register {0} at 0x{1:x}", ((RegisterEnum)sourceReg + 8).ToString(), this.aInstructions[i].LinearAddress);
						}
						else
						{
							this.aInstructions[i].InstructionType = InstructionEnum.NOP;
						}
						if (this.aInstructions[i + 2].InstructionType == InstructionEnum.LES)
						{
							this.aInstructions[i + 1].InstructionType = InstructionEnum.NOP;
						}
						else if (CheckIfUsed(iPos, iEnd, InstructionSizeEnum.Word, InstructionParameterTypeEnum.SegmentRegister, (uint)SegmentRegisterEnum.ES))
						{
							Console.WriteLine("Found used segment register {0} at 0x{1:x}", SegmentRegisterEnum.ES.ToString(), this.aInstructions[i].LinearAddress);
							this.aInstructions[i + 1].Parameters[1] = new InstructionParameter(InstructionParameterTypeEnum.Immediate, usSegmentValue);
						}
						else
						{
							this.aInstructions[i + 1].InstructionType = InstructionEnum.NOP;
						}
					}
				}

				// form 2
				if (i + 3 < this.aInstructions.Count &&
					(instruction1 = this.aInstructions[i]).InstructionType == InstructionEnum.MOV &&
					instruction1.OperandSize == InstructionSizeEnum.Word &&
					instruction1.Parameters.Count == 2 &&
					instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Register &&
					(sourceReg = instruction1.Parameters[0].Value) <= 7 &&
					instruction1.Parameters[1].Type == InstructionParameterTypeEnum.Immediate &&
					(usSegmentValue = (ushort)instruction1.Parameters[1].Value) >= 0 &&

					(instruction1 = this.aInstructions[i + 2]).InstructionType == InstructionEnum.MOV &&
					instruction1.OperandSize == InstructionSizeEnum.Word &&
					instruction1.Parameters.Count == 2 &&
					instruction1.Parameters[0].Type == InstructionParameterTypeEnum.SegmentRegister &&
					instruction1.Parameters[0].Value == (uint)SegmentRegisterEnum.ES &&
					instruction1.Parameters[1].Type == InstructionParameterTypeEnum.Register &&
					instruction1.Parameters[1].Value == sourceReg)
				{
					int iPos = i + 3;
					int iCount = 0;
					bool bMatch = true;

					while (bMatch && iPos < this.aInstructions.Count)
					{
						Instruction instruction = this.aInstructions[iPos];
						bMatch = false;
						if (instruction.Parameters.Count > 0 && instruction.Parameters[0].DataSegment == SegmentRegisterEnum.ES)
						{
							this.aInstructions[iPos].Parameters[0].DataSegment = SegmentRegisterEnum.Immediate;
							this.aInstructions[iPos].Parameters[0].Segment = usSegmentValue;
							bMatch = true;
						}
						if (instruction.Parameters.Count > 1 && instruction.Parameters[1].DataSegment == SegmentRegisterEnum.ES)
						{
							this.aInstructions[iPos].Parameters[1].DataSegment = SegmentRegisterEnum.Immediate;
							this.aInstructions[iPos].Parameters[1].Segment = usSegmentValue;
							bMatch = true;
						}
						if (bMatch)
						{
							iPos++;
							iCount++;
						}
						if (instruction.InstructionType == InstructionEnum.LES)
						{
							// LES is the last instruction that uses ES
							break;
						}
					}

					if (iCount > 0)
					{
						if (CheckIfUsed(i + 3, iEnd, InstructionSizeEnum.Word, InstructionParameterTypeEnum.Register, sourceReg))
						{
							Console.WriteLine("Found used register {0} at 0x{1:x}", ((RegisterEnum)sourceReg + 8).ToString(), this.aInstructions[i].LinearAddress);
						}
						else
						{
							this.aInstructions[i].InstructionType = InstructionEnum.NOP;
						}
						if (this.aInstructions[i + 3].InstructionType == InstructionEnum.LES)
						{
							this.aInstructions[i + 2].InstructionType = InstructionEnum.NOP;
						}
						else if (CheckIfUsed(iPos, iEnd, InstructionSizeEnum.Word, InstructionParameterTypeEnum.SegmentRegister, (uint)SegmentRegisterEnum.ES))
						{
							Console.WriteLine("Found used segment register {0} at 0x{1:x}", SegmentRegisterEnum.ES.ToString(), this.aInstructions[i].LinearAddress);
							this.aInstructions[i + 2].Parameters[1] = new InstructionParameter(InstructionParameterTypeEnum.Immediate, usSegmentValue);
						}
						else
						{
							this.aInstructions[i + 2].InstructionType = InstructionEnum.NOP;
						}
					}
				}
			}
			#endregion

			#region process calls and assign parameters
			for (int i = 0; i < this.aInstructions.Count; i++)
			{
				Instruction instruction = this.aInstructions[i];
				InstructionParameter parameter;

				switch (instruction.InstructionType)
				{
					case InstructionEnum.CALLF:
						parameter = instruction.Parameters[0];
						if (parameter.Type == InstructionParameterTypeEnum.SegmentOffset)
						{
							if (parameter.Segment == 50 && parameter.Value == 0x501)
							{
								// ignore old OpenResFile function
								instruction.InstructionType = InstructionEnum.NOP;
								this.aInstructions[i + 1].InstructionType = InstructionEnum.NOP;
								break;
							}

							if (parameter.Segment == 41 && parameter.Value == 0x3c)
							{
								// this is a leftover function code which does nothing, but reveals original function name
								uint lpFunctionName;

								if ((instruction1 = this.aInstructions[i - 1]).InstructionType == InstructionEnum.PUSH &&
									instruction1.Parameters.Count == 1 &&
									instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Immediate &&
									(lpFunctionName = instruction1.Parameters[0].Value) > 0)
								{
									// we found function name
									uint lpFunctionName1 = lpFunctionName;
									Segment segment1 = this.oParent.Executable.Segments[(int)this.oParent.DataSegment];
									StringBuilder sbName = new StringBuilder();
									while (segment1.Data[lpFunctionName1] != 0)
									{
										sbName.Append((char)segment1.Data[lpFunctionName1]);
										lpFunctionName1++;
									}

									string sFunctionName = sbName.ToString();

									// register this name as string in global table
									lpFunctionName |= (this.oParent.DataSegment << 16);
									this.oParent.GlobalNamespace.Variables.Add(new CVariable(this.oParent, CType.CharFarPtr,
										string.Format("Global{0}_{1:x}", (lpFunctionName & 0xffff0000) >> 16, lpFunctionName & 0xffff),
										CScopeEnum.Global, lpFunctionName, sFunctionName.Length + 1));

									if (!string.IsNullOrEmpty(sFunctionName) && sFunctionName.IndexOf('(') > 0)
									{
										string sTemp = sFunctionName.Substring(0, sFunctionName.IndexOf('('));

										while (true)
										{
											if (this.oParent.GlobalNamespace.Functions.ContainsKey(sTemp))
											{
												sTemp += "A";
											}
											else
											{
												break;
											}
										}

										this.sName = sTemp;
										if (sFunctionName.IndexOf(':') > 0)
										{
											string sNamespace = sFunctionName.Substring(sFunctionName.IndexOf(':') + 1).Trim();
											if (sNamespace.EndsWith(".c", StringComparison.InvariantCultureIgnoreCase))
												sNamespace = sNamespace.Substring(0, sNamespace.Length - 2);

											if (!string.IsNullOrEmpty(segment.Namespace) && !segment.Namespace.Equals(sNamespace, StringComparison.InvariantCultureIgnoreCase))
												throw new Exception("Segment namespaces dont match");
											segment.Namespace = sNamespace;
										}
									}

									// minimisation, remove function reference
									this.aInstructions[i - 2].InstructionType = InstructionEnum.NOP;
									this.aInstructions[i - 1].InstructionType = InstructionEnum.NOP;
									this.aInstructions[i].InstructionType = InstructionEnum.NOP;
									this.aInstructions[i + 1].InstructionType = InstructionEnum.NOP;
								}
								else
								{
									throw new Exception(string.Format("Unknown GetFunctionName format at function {0}:0x{1:x}", this.usSegment, this.usOffset));
								}
								break;
							}

							CFunction function = decompiler.GetFunction(parameter.Segment, (ushort)parameter.Value);
							if (parameter.Segment != 0 && function == null)
							{
								// function is not yet defined, define it
								decompiler.Decompile(string.Format("F{0}_{1:x}", parameter.Segment, parameter.Value),
									CallTypeEnum.Undefined, new List<CParameter>(), CType.Void, parameter.Segment, (ushort)parameter.Value);
								function = decompiler.GetFunction(parameter.Segment, (ushort)parameter.Value);
							}

							// there are some problems with parameters, disable for now
							/*if (parameter.SegmentAddress != 0 && function == null)
							{
								throw new Exception("Can't find function for parameter checking");
							}

							if (function.CallType == CallTypeEnum.Cdecl)
							{
								int iPosition = i;
								instruction1 = GetNextInstruction(ref iPosition);

								if (instruction1.InstructionType == InstructionEnum.WordsToDword &&
									!instruction1.Label &&

									(instruction1 = GetNextInstruction(ref iPosition)).InstructionType == InstructionEnum.ADD &&
									!instruction1.Label &&
									instruction1.Parameters.Count == 2 &&
									instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Register &&
									instruction1.Parameters[0].Value == ((uint)RegisterEnum.SP & 0x7) &&
									instruction1.Parameters[1].Type == InstructionParameterTypeEnum.Immediate &&
									instruction1.Parameters[1].Value >= 0)
								{
									// handle compiler optimization
									// reorder add sp to be directly after the call
									// so we don't have to handle this case later
									this.aInstructions.RemoveAt(iPosition);
									this.aInstructions.Insert(i + 1, instruction1);
								}

								// read parameter count
								iPosition = i;
								instruction1 = GetNextInstruction(ref iPosition);
								uint uiParameterSize;

								if ((instruction1.InstructionType == InstructionEnum.ADD &&
									instruction1.Parameters.Count == 2 &&
									instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Register &&
									instruction1.Parameters[0].Value == ((uint)RegisterEnum.SP & 0x7) &&
									instruction1.Parameters[1].Type == InstructionParameterTypeEnum.Immediate &&
									(uiParameterSize = instruction1.Parameters[1].Value) >= 0) ||

									(instruction1.InstructionType == InstructionEnum.POP &&
									instruction1.Parameters.Count == 1 &&
									instruction1.Parameters[0].Type == InstructionParameterTypeEnum.Register &&
									(uiParameterSize = 2) >= 0))
								{
									// pop here is unreliable. It often assigns parameterSize of 2 to a function which doesn't have parameters
									if (instruction1.InstructionType != InstructionEnum.POP && function.ParameterSize < 0)
									{
										function.ParameterSize = (int)uiParameterSize;
									}
									if (instruction1.InstructionType == InstructionEnum.POP && function.ParameterSize == 0)
									{
										uiParameterSize = 0;
									}
									if ((uiParameterSize & 1) != 0)
									{
										throw new Exception(string.Format("Stack alignment error on {0}:0x{1:x} at function {2}:0x{3:x}",
											instruction1.Location.Segment, instruction1.Location.Offset, this.uiSegment, this.uiOffset));
									}

									if (function.Parameters.Count > 0)
									{
										if ((function.HasVariableParameters && uiParameterSize >= function.ParameterSize) ||
											function.ParameterSize < 0 || uiParameterSize == function.ParameterSize)
										{
											// all is OK, parameter count will be updated later as needed
										}
										else
										{
											throw new Exception(string.Format("Function {0}:0x{1:x} has invalid parameter count at function {2}:0x{3:x}",
												parameter.SegmentAddress, parameter.Value, this.uiSegment, this.uiOffset));
										}
									}
									else if (uiParameterSize > 0)
									{
										uint uiParameterCount = 0;
										iPosition = i;

										while (uiParameterCount < uiParameterSize)
										{
											instruction1 = GetPreviousInstruction(ref iPosition);
											if (instruction1 == null)
											{
												throw new Exception(string.Format("Parameters outside function scope in function {0}:0x{1:x}",
													this.uiSegment, this.uiOffset));
											}

											if (instruction1.InstructionType == InstructionEnum.PUSH)
											{
												if (instruction1.OperandSize == InstructionSizeEnum.Word)
												{
													uiParameterCount += 2;
												}
												else if (instruction1.OperandSize == InstructionSizeEnum.DWord)
												{
													uiParameterCount += 4;
												}
											}
											else
											{
												// do nothing for now
											}
										}
									}
								}
								else
								{
									if (function.ParameterSize > 0 || function.Parameters.Count != 0)
									{
										throw new Exception(string.Format("Function {0}:0x{1:x} has invalid parameter count at function {2}:0x{3:x}",
											parameter.SegmentAddress, parameter.Value, this.uiSegment, this.uiOffset));
									}
								}
							}
							else if (function.CallType == CallTypeEnum.Pascal)
							{ }
							else
							{
								throw new Exception(string.Format("Unknown function type call to {0}:0x{1:x} at function {2}:0x{3:x}",
									parameter.SegmentAddress, parameter.Value, this.uiSegment, this.uiOffset));
							}*/
						}
						break;
				}
			}
			#endregion

			#region Translate Jcc's to If's
			for (int i = 0; i < this.aInstructions.Count; i++)
			{
				uint uiNewOffset;

				if (i + 1 < this.aInstructions.Count &&
					(instruction1 = this.aInstructions[i]).InstructionType == InstructionEnum.CMP &&
					instruction1.Parameters.Count == 2 &&

					(instruction1 = this.aInstructions[i + 1]).InstructionType == InstructionEnum.Jcc &&
					instruction1.Parameters.Count == 2 &&
					(uiNewOffset = (instruction1.Offset + (uint)instruction1.Bytes.Count + instruction1.Parameters[1].Value) & 0xffff) >= 0)
				{
					Instruction instruction = this.aInstructions[i];

					// jcc can't be referenced to in this combination
					if (instruction1.Label)
						throw new Exception(string.Format("Unexpected label at function {0}:0x{1:x}, position 0x{2:x}",
							this.usSegment, this.usOffset, instruction1.Offset));

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
								throw new Exception(string.Format("Unexpected label at function {0}:0x{1:x}, position 0x{2:x}",
									this.usSegment, this.usOffset, instruction1.Offset));

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
					(uiNewOffset = (instruction1.Offset + (uint)instruction1.Bytes.Count + instruction1.Parameters[1].Value) & 0xffff) >= 0)
				{
					Instruction instruction = this.aInstructions[i];

					// jcc can't be referenced to in this combination
					if (instruction1.Label)
						throw new Exception(string.Format("Unexpected label at function {0}:0x{1:x}, position 0x{2:x}",
							this.usSegment, this.usOffset, instruction1.Offset));

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
							throw new Exception(string.Format("Unexpected label at function {0}:0x{1:x}, position 0x{2:x}",
								this.usSegment, this.usOffset, instruction1.Offset));

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
					(uiNewOffset = (instruction1.Offset + (uint)instruction1.Bytes.Count + instruction1.Parameters[1].Value) & 0xffff) >= 0)
				{
					Instruction instruction = this.aInstructions[i];

					// jcc can't be referenced to in this combination
					if (instruction1.Label)
						throw new Exception(string.Format("Unexpected label at function {0}:0x{1:x}, position 0x{2:x}",
							this.usSegment, this.usOffset, instruction1.Offset));

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
							throw new Exception(string.Format("Unexpected label at function {0}:0x{1:x}, position 0x{2:x}",
								this.usSegment, this.usOffset, instruction1.Offset));

						instruction1.Parameters.Insert(0, instruction.Parameters[0]);
						instruction1.Parameters.Insert(1, instruction.Parameters[1]);
						instruction1.InstructionType = InstructionEnum.IfOr;
						iPos++;
					}
				}
			}
			#endregion
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

		private int GetPositionFromOffset(uint offset)
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

			if (iPosition == -1)
				throw new Exception(string.Format("Can't find the offset 0x{0} in function {1}:0x{2}", 
					offset, this.usSegment, this.usOffset));

			return iPosition;
		}

		private Instruction GetNextInstruction(ref int position)
		{
			while (position < this.aInstructions.Count)
			{
				position++;
				Instruction instruction = this.aInstructions[position];

				// ignore nop
				if (instruction.InstructionType == InstructionEnum.NOP)
					continue;

				// direct jump
				if (instruction.InstructionType == InstructionEnum.JMP)
				{
					if (instruction.Parameters.Count == 1 && instruction.Parameters[0].Type == InstructionParameterTypeEnum.Relative)
					{
						uint uiNewOffset = (instruction.Offset + (uint)instruction.Bytes.Count + instruction.Parameters[0].Value) & 0xffff;

						position = GetPositionFromOffset(uiNewOffset) - 1;
					}
					else
					{
						throw new Exception("Jump has invalid parameters");
					}
					continue;
				}

				return this.aInstructions[position];
			}

			return null;
		}

		private Instruction GetPreviousInstruction(ref int position)
		{
			while (position > 0)
			{
				position--;
				Instruction instruction = this.aInstructions[position];

				// ignore nop
				if (instruction.InstructionType == InstructionEnum.NOP)
					continue;

				// direct jump
				if (instruction.InstructionType == InstructionEnum.JMP)
				{
					uint uiNewOffset = (instruction.Offset + (uint)instruction.Bytes.Count + instruction.Parameters[0].Value) & 0xffff;

					position = GetPositionFromOffset(uiNewOffset) + 1;
					continue;
				}

				return this.aInstructions[position];
			}

			return null;
		}

		// local registers
		List<IStatement> aRegisters;

		// local segments
		List<IStatement> aSegments;

		public void Decompile(CDecompiler decompiler)
		{
			// update statistics
			for (int i = 0; i < this.aInstructions.Count; i++)
			{
				Instruction instruction = this.aInstructions[i];

				if (instruction.InstructionType == InstructionEnum.Undefined)
				{
					Console.WriteLine("Undefined instruction at function {0}:0x{1:x}, position 0x{2:x}",
						this.usSegment, this.usOffset, instruction.Offset);
				}

				if (Statistics.ContainsKey(instruction.InstructionType))
				{
					Statistics.SetValueByKey(instruction.InstructionType, Statistics.GetValueByKey(instruction.InstructionType) + 1);
				}
				else
				{
					Statistics.Add(instruction.InstructionType, 1);
				}
			}

			// BP frame:
			// BP+n = first param
			// BP+6 = last param
			// BP+4 = CS
			// BP+2 = IP
			// BP+0 = BP
			// BP-2 = DS
			// BP-4 = free..., local stack

			// create context
			this.aStatements.Clear();

			// Local variables
			this.oLocalNamespace.Variables.Clear();

			// local parameters
			int iTemp;
			int iDir;

			if (this.eCallType == CallTypeEnum.Cdecl)
			{
				// first to last
				iTemp = 6;
				iDir = 1;
			}
			else
			{
				// last to first
				iTemp = 6;
				iDir = -1;
				for (int i = 0; i < this.aParameters.Count - 1; i++)
				{
					iTemp += this.aParameters[i].Type.Size;
				}
			}

			for (int i = 0; i < this.aParameters.Count; i++)
			{
				CParameter parameter = this.aParameters[i];
				this.oLocalNamespace.Parameters.Add(new CVariableReference(this, parameter, (uint)(iTemp)));
				iTemp += (int)parameter.Type.Size * iDir;
			}

			// local registers
			this.aRegisters = new List<IStatement>(
				new IStatement[] { null, null, null, null, null, null, null, null });

			// local segments
			this.aSegments = new List<IStatement>(
				new IStatement[] { null, null, null, new CImmediateValue(this, CType.Word, ReferenceTypeEnum.Segment, 132), null, null, null, null });

			// local stack
			List<IStatement> aStack = new List<IStatement>();

			List<int> aPositionStack = new List<int>();

			/*for (int j = iStart; j < this.aInstructions.Count; j++)
			{
				if (j == iEnd)
				{
					break;
				}

				Instruction instruction = this.aInstructions[j];

				if (instruction.Label)
				{
					this.aStatements.Add(new CLabel(this, string.Format("L{0:x4}:", instruction.Location.Offset)));
				}

				uint uiNewOffset = 0;
				InstructionParameter parameter;

				switch (instruction.InstructionType)
				{
					// these operations should be integrated into Additions and Subtractions
					case InstructionEnum.ADC:
					case InstructionEnum.SBB:
						break;

					case InstructionEnum.ADD:
						SetReference(instruction.Parameters[0],
							new CMathOperation(this, GetReference(instruction.Parameters[0]), GetReference(instruction.Parameters[1]),
							MathOperationEnum.Add));
						break;
					case InstructionEnum.SUB:
						SetReference(instruction.Parameters[0],
							new CMathOperation(this, GetReference(instruction.Parameters[0]), GetReference(instruction.Parameters[1]),
							MathOperationEnum.Subtract));
						break;

					case InstructionEnum.AND:
						SetReference(instruction.Parameters[0],
							new CBinaryOperation(this, GetReference(instruction.Parameters[0]), GetReference(instruction.Parameters[1]),
							BinaryOperationEnum.And));
						break;
					case InstructionEnum.OR:
						SetReference(instruction.Parameters[0],
							new CBinaryOperation(this, GetReference(instruction.Parameters[0]), GetReference(instruction.Parameters[1]),
							BinaryOperationEnum.Or));
						break;
					case InstructionEnum.XOR:
						SetReference(instruction.Parameters[0],
							new CBinaryOperation(this, GetReference(instruction.Parameters[0]), GetReference(instruction.Parameters[1]),
							BinaryOperationEnum.Xor));
						break;

					case InstructionEnum.DEC:
					case InstructionEnum.INC:
					case InstructionEnum.NEG:
					case InstructionEnum.NOT:
						break;

					case InstructionEnum.SAR:
					case InstructionEnum.SHL:
					case InstructionEnum.SHR:
						break;
					case InstructionEnum.SHLD:
						break;
					case InstructionEnum.CBW:
						break;
					case InstructionEnum.CWD:
						break;
					case InstructionEnum.CMP:
					case InstructionEnum.TEST:
						break;
					case InstructionEnum.DIV:
					case InstructionEnum.IDIV:
						break;
					case InstructionEnum.FADDP:
						break;
					case InstructionEnum.FILD:
						break;
					case InstructionEnum.FMUL:
						break;
					case InstructionEnum.IMUL:
						break;
					case InstructionEnum.LDS:
						break;
					case InstructionEnum.LES:
						break;
					case InstructionEnum.LEA:
						break;
					case InstructionEnum.MOV:
						SetReference(instruction.Parameters[0], GetReference(instruction.Parameters[1]));
						break;

					case InstructionEnum.MOVS:
						break;

					case InstructionEnum.MOVSX:
					case InstructionEnum.MOVZX:
						break;

					case InstructionEnum.STD:
						break;
					case InstructionEnum.XCHG:
						break;

					// stack operations
					case InstructionEnum.POP:
						break;
					case InstructionEnum.POPA:
						break;
					case InstructionEnum.PUSH:
						break;
					case InstructionEnum.PUSHA:
						break;

					case InstructionEnum.NOP:
						// ignore this instruction
						break;
					case InstructionEnum.WAIT:
						// ignore this instruction
						break;

					// special syntetic functions
					case InstructionEnum.WordsToDword:
						break;

					// flow control instructions
					case InstructionEnum.SWITCH:
						break;
					case InstructionEnum.CALLF:
						break;
					case InstructionEnum.Jcc:
						uiNewOffset = (instruction.Location.Offset + (uint)instruction.Bytes.Count + instruction.Parameters[1].Value) & 0xffff;
						break;
					case InstructionEnum.JMP:
						uiNewOffset = (instruction.Location.Offset + (uint)instruction.Bytes.Count + instruction.Parameters[0].Value) & 0xffff;
						break;
					case InstructionEnum.LOOP:
						uiNewOffset = (instruction.Location.Offset + (uint)instruction.Bytes.Count + instruction.Parameters[0].Value) & 0xffff;
						break;
					case InstructionEnum.RETF:
						break;

					default:
						throw new Exception("Unexpected instruction");
				}
			}*/
		}

		private IStatement GetReference(InstructionParameter parameter)
		{
			int iDisplacement;
			ReferenceTypeEnum eReferenceType;

			switch (parameter.Type)
			{
				case InstructionParameterTypeEnum.Register:
					switch (parameter.Size)
					{
						case InstructionSizeEnum.Byte:
							switch ((RegisterEnum)parameter.Value)
							{
								case RegisterEnum.AL:
									return new CGetPart(this, this.aRegisters[(int)RegisterEnum.AX & 0x7], PartTypeEnum.LowByte);
								case RegisterEnum.CL:
									return new CGetPart(this, this.aRegisters[(int)RegisterEnum.CX & 0x7], PartTypeEnum.LowByte);
								case RegisterEnum.DL:
									return new CGetPart(this, this.aRegisters[(int)RegisterEnum.DX & 0x7], PartTypeEnum.LowByte);
								case RegisterEnum.BL:
									return new CGetPart(this, this.aRegisters[(int)RegisterEnum.BX & 0x7], PartTypeEnum.LowByte);
								case RegisterEnum.AH:
									return new CGetPart(this, this.aRegisters[(int)RegisterEnum.AX & 0x7], PartTypeEnum.HighByte);
								case RegisterEnum.CH:
									return new CGetPart(this, this.aRegisters[(int)RegisterEnum.CX & 0x7], PartTypeEnum.HighByte);
								case RegisterEnum.DH:
									return new CGetPart(this, this.aRegisters[(int)RegisterEnum.DX & 0x7], PartTypeEnum.HighByte);
								case RegisterEnum.BH:
									return new CGetPart(this, this.aRegisters[(int)RegisterEnum.BX & 0x7], PartTypeEnum.HighByte);
							}
							break;
						case InstructionSizeEnum.Word:
							return new CGetPart(this, this.aRegisters[(int)parameter.Value & 0x7], PartTypeEnum.LowWord);
						case InstructionSizeEnum.DWord:
							return this.aRegisters[(int)parameter.Value & 0x7];
					}
					break;
				case InstructionParameterTypeEnum.Immediate:
					eReferenceType = ReferenceTypeEnum.Undefined;
					switch (parameter.ReferenceType)
					{
						case InstructionParameterReferenceEnum.Offset:
							eReferenceType = ReferenceTypeEnum.Offset;
							break;
						case InstructionParameterReferenceEnum.Segment:
							eReferenceType = ReferenceTypeEnum.Segment;
							break;
					}

					switch (parameter.Size)
					{
						case InstructionSizeEnum.Byte:
							return new CImmediateValue(this, CType.Byte, eReferenceType, parameter.Value);
						case InstructionSizeEnum.Word:
							return new CImmediateValue(this, CType.Word, eReferenceType, parameter.Value);
						case InstructionSizeEnum.DWord:
							return new CImmediateValue(this, CType.DWord, eReferenceType, parameter.Value);
					}
					break;
				case InstructionParameterTypeEnum.MemoryAddress:
				case InstructionParameterTypeEnum.LEAMemoryAddress:
					switch (parameter.Value)
					{
						case 0:
							// sb.Append("[BX + SI]");
							break;
						case 1:
							// sb.Append("[BX + DI]");
							break;
						case 2:
							// sb.Append("[BP + SI]");
							break;
						case 3:
							// sb.Append("[BP + DI]");
							break;
						case 4:
							// sb.Append("[SI]");
							break;
						case 5:
							// sb.Append("[DI]");
							break;
						case 6:
							// sb.AppendFormat("[0x{0:x}]", this.uiDisplacement);
							break;
						case 7:
							// sb.Append("[BX]");
							break;

						case 8:
							// sb.AppendFormat("[BX + SI {0}]", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Byte));
							break;
						case 9:
							// sb.AppendFormat("[BX + DI {0}]", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Byte));
							break;
						case 10:
							// sb.AppendFormat("[BP + SI {0}]", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Byte));
							break;
						case 11:
							// sb.AppendFormat("[BP + DI {0}]", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Byte));
							break;
						case 12:
							// sb.AppendFormat("[SI {0}]", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Byte));
							break;
						case 13:
							// sb.AppendFormat("[DI {0}]", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Byte));
							break;
						case 14:
							// sb.AppendFormat("[BP {0}]", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Byte));
							if (parameter.DataSegment == SegmentRegisterEnum.SS)
							{
								iDisplacement = (sbyte)(parameter.Displacement & 0xff);
								if (iDisplacement >= 6)
								{
									// it's a parameter
									for (int j = 0; j < this.oLocalNamespace.Parameters.Count; j++)
									{
										if (this.oLocalNamespace.Parameters[j].Address == iDisplacement)
										{
											return this.oLocalNamespace.Parameters[j];
										}
									}
									return null;
									throw new Exception("Can't find appropriate parameter");
								}
								else if (iDisplacement < -2)
								{
									// it's a local variable
									iDisplacement += 2;
									iDisplacement = this.iStackSize + iDisplacement;
									if (iDisplacement < 0 || iDisplacement > this.iStackSize)
									{
										throw new Exception("Local variable space misalignment");
									}
									for (int j = 0; j < this.oLocalNamespace.Variables.Count; j++)
									{
										if (this.oLocalNamespace.Variables[j].Address == iDisplacement)
										{
											return this.oLocalNamespace.Variables[j];
										}
									}
									return null;
									throw new Exception("Can't find appropriate variable");
								}
								else
								{
									// between is reserved
									throw new Exception("Reserved stack region");
								}
							}
							else
							{
								throw new Exception("Invalid BP segment");
							}
							break;
						case 15:
							// sb.AppendFormat("[BX {0}]", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Byte));
							break;

						case 16:
							// sb.AppendFormat("(ushort)(this.oParent.CPU.BX.Word + this.oParent.CPU.SI.Word {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Word));
							break;
						case 17:
							// sb.AppendFormat("(ushort)(this.oParent.CPU.BX.Word + this.oParent.CPU.DI.Word {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Word));
							break;
						case 18:
							// sb.AppendFormat("(ushort)(this.oParent.CPU.BP.Word + this.oParent.CPU.SI.Word {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Word));
							break;
						case 19:
							// sb.AppendFormat("(ushort)(this.oParent.CPU.BP.Word + this.oParent.CPU.DI.Word {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Word));
							break;
						case 20:
							// sb.AppendFormat("(ushort)(this.oParent.CPU.SI.Word {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Word));
							break;
						case 21:
							// sb.AppendFormat("(ushort)(this.oParent.CPU.DI.Word {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Word));
							break;
						case 22:
							// sb.AppendFormat("(ushort)(this.oParent.CPU.BP.Word {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Word));
							break;
						case 23:
							// sb.AppendFormat("(ushort)(this.oParent.CPU.BX.Word {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Word));
							break;
					}
					break;
			}

			return null;
		}

		private void SetReference(InstructionParameter parameter, IStatement value)
		{
		}

		/// <summary>
		/// Checks if register or segment register is used after the instruction
		/// </summary>
		private bool CheckIfUsed(int position, int endPosition, InstructionSizeEnum size, InstructionParameterTypeEnum parameterType, uint value)
		{
			List<uint> aJumpStack = new List<uint>();
			List<uint> aCheckedJumps = new List<uint>();

			uint byteRegisterLow = (uint)RegisterEnum.Invalid;
			uint byteRegisterHigh = (uint)RegisterEnum.Invalid;
			uint wordRegister = (uint)RegisterEnum.Invalid;

			if (parameterType == InstructionParameterTypeEnum.Register)
			{
				if (size == InstructionSizeEnum.Byte)
				{
					switch ((RegisterEnum)value)
					{
						case RegisterEnum.AL:
							byteRegisterLow = value;
							wordRegister = (uint)RegisterEnum.AX;
							break;
						case RegisterEnum.AH:
							byteRegisterHigh = value;
							wordRegister = (uint)RegisterEnum.AX;
							break;
						case RegisterEnum.BL:
							byteRegisterLow = value;
							wordRegister = (uint)RegisterEnum.BX;
							break;
						case RegisterEnum.BH:
							byteRegisterHigh = value;
							wordRegister = (uint)RegisterEnum.BX;
							break;
						case RegisterEnum.CL:
							byteRegisterLow = value;
							wordRegister = (uint)RegisterEnum.CX;
							break;
						case RegisterEnum.CH:
							byteRegisterHigh = value;
							wordRegister = (uint)RegisterEnum.CX;
							break;
						case RegisterEnum.DL:
							byteRegisterLow = value;
							wordRegister = (uint)RegisterEnum.DX;
							break;
						case RegisterEnum.DH:
							byteRegisterHigh = value;
							wordRegister = (uint)RegisterEnum.DX;
							break;
					}
				}
				else
				{
					wordRegister = (value & 7) + 8;
					switch ((RegisterEnum)wordRegister)
					{
						case RegisterEnum.AX:
							byteRegisterLow = (uint)RegisterEnum.AL;
							byteRegisterHigh = (uint)RegisterEnum.AH;
							break;
						case RegisterEnum.BX:
							byteRegisterLow = (uint)RegisterEnum.BL;
							byteRegisterHigh = (uint)RegisterEnum.BH;
							break;
						case RegisterEnum.CX:
							byteRegisterLow = (uint)RegisterEnum.CL;
							byteRegisterHigh = (uint)RegisterEnum.CH;
							break;
						case RegisterEnum.DX:
							byteRegisterLow = (uint)RegisterEnum.DL;
							byteRegisterHigh = (uint)RegisterEnum.DH;
							break;
					}
				}
			}
			else if (parameterType == InstructionParameterTypeEnum.SegmentRegister)
			{
				wordRegister = value;
			}

			return CheckIfUsedRecursive(position, endPosition, size, parameterType, byteRegisterLow, byteRegisterHigh, wordRegister, aJumpStack, aCheckedJumps);
		}

		private bool CheckIfUsedRecursive(int pos, int endPos, InstructionSizeEnum size, InstructionParameterTypeEnum parameterType, 
			uint byteRegisterLow, uint byteRegisterHigh, uint wordRegister,
			List<uint> callJumps, List<uint> checkedJumps)
		{
			if (pos >= endPos)
				return false;

			bool bUsed = false;

			for (int i = pos; i <= endPos; i++)
			{
				bool bEndLoop = false;
				Instruction instruction = this.aInstructions[i];
				uint uiNewOffset;

				if (instruction.Label && checkedJumps.IndexOf(instruction.Offset) < 0)
				{
					checkedJumps.Add(instruction.Offset);
				}

				switch (instruction.InstructionType)
				{
					case InstructionEnum.ADC:
					case InstructionEnum.ADD:
					case InstructionEnum.AND:
					case InstructionEnum.OR:
					case InstructionEnum.SBB:
					case InstructionEnum.SUB:
					case InstructionEnum.SAR:
					case InstructionEnum.SHL:
					case InstructionEnum.SHR:
					case InstructionEnum.CMP:
					case InstructionEnum.TEST:
						// parameter 1 and 2 are sources
						if (MatchParameter(instruction.Parameters[0], parameterType, byteRegisterLow, byteRegisterHigh, wordRegister) ||
							MatchParameter(instruction.Parameters[1], parameterType, byteRegisterLow, byteRegisterHigh, wordRegister))
						{
							bUsed = true;
							bEndLoop = true;
						}
						break;
					case InstructionEnum.XOR:
						// parameter 1 and 2 are sources
						// special case for XOR to 0
						if (MatchParameter(instruction.Parameters[0], parameterType, byteRegisterLow, byteRegisterHigh, wordRegister) &&
							instruction.Parameters.Count == 2 &&
							instruction.Parameters[0].Type == InstructionParameterTypeEnum.Register &&
							instruction.Parameters[1].Type == InstructionParameterTypeEnum.Register &&
							instruction.Parameters[0].Size == instruction.Parameters[1].Size &&
							instruction.Parameters[0].Value == instruction.Parameters[1].Value)
						{
							bUsed = false;
							bEndLoop = true;
						}
						else if (MatchParameter(instruction.Parameters[0], parameterType, byteRegisterLow, byteRegisterHigh, wordRegister) ||
							MatchParameter(instruction.Parameters[1], parameterType, byteRegisterLow, byteRegisterHigh, wordRegister))
						{
							bUsed = true;
							bEndLoop = true;
						}
						break;
					case InstructionEnum.SHLD:
						if (MatchParameter(instruction.Parameters[0], parameterType, byteRegisterLow, byteRegisterHigh, wordRegister) ||
							MatchParameter(instruction.Parameters[1], parameterType, byteRegisterLow, byteRegisterHigh, wordRegister) ||
							MatchParameter(instruction.Parameters[2], parameterType, byteRegisterLow, byteRegisterHigh, wordRegister))
						{
							bUsed = true;
							bEndLoop = true;
						}
						break;
					case InstructionEnum.CALLF:
						// all temporary registers are invalid except SI, DI and DS
						if ((parameterType == InstructionParameterTypeEnum.Register &&
							(wordRegister == (uint)RegisterEnum.SI || wordRegister == (uint)RegisterEnum.DI)) ||
							(parameterType == InstructionParameterTypeEnum.SegmentRegister && wordRegister == (uint)SegmentRegisterEnum.DS))
						{
						}
						else
						{
							bUsed = false;
							bEndLoop = true;
						}
						break;
					case InstructionEnum.CBW:
						// operand size matters, AL is only used
						if (parameterType == InstructionParameterTypeEnum.Register && wordRegister == (uint)(RegisterEnum.AX))
						{
							bUsed = true;
							bEndLoop = true;
						}
						break;
					case InstructionEnum.CWD:
						if (parameterType == InstructionParameterTypeEnum.Register && wordRegister == (uint)RegisterEnum.AX)
						{
							bUsed = true;
							bEndLoop = true;
						}
						if (parameterType == InstructionParameterTypeEnum.Register && wordRegister == (uint)RegisterEnum.DX)
						{
							bUsed = false;
							bEndLoop = true;
						}
						break;

					// floating point
					case InstructionEnum.FADDP:
						break;
					case InstructionEnum.FILD:
						// parameter should be memory addressing
						if (MatchParameter(instruction.Parameters[0], parameterType, byteRegisterLow, byteRegisterHigh, wordRegister))
						{
							bUsed = true;
							bEndLoop = true;
						}
						break;
					case InstructionEnum.FMUL:
						// parameter could be memory addressing
						if (MatchParameter(instruction.Parameters[0], parameterType, byteRegisterLow, byteRegisterHigh, wordRegister))
						{
							bUsed = true;
							bEndLoop = true;
						}
						break;

					case InstructionEnum.WAIT:
						// ignore this instruction
						break;

					case InstructionEnum.DIV:
					case InstructionEnum.IDIV:
						if (instruction.OperandSize == InstructionSizeEnum.Byte)
						{
							if (MatchParameter(instruction.Parameters[0], parameterType, byteRegisterLow, byteRegisterHigh, wordRegister) ||
								MatchWordRegister(parameterType, wordRegister, RegisterEnum.AX))
							{
								bUsed = true;
								bEndLoop = true;
							}
						}
						else
						{
							if (MatchParameter(instruction.Parameters[0], parameterType, byteRegisterLow, byteRegisterHigh, wordRegister) ||
								MatchWordRegister(parameterType, wordRegister, RegisterEnum.AX) ||
								MatchWordRegister(parameterType, wordRegister, RegisterEnum.DX))
							{
								bUsed = true;
								bEndLoop = true;
							}
						}
						break;
					case InstructionEnum.IMUL:
						switch(instruction.Parameters.Count)
						{
							case 1:
								if (instruction.OperandSize == InstructionSizeEnum.Byte)
								{
									if (MatchParameter(instruction.Parameters[0], parameterType, byteRegisterLow, byteRegisterHigh, wordRegister) ||
										MatchWordRegister(parameterType, wordRegister, RegisterEnum.AX))
									{
										bUsed = true;
										bEndLoop = true;
									}
								}
								else
								{
									if (MatchWordRegister(parameterType, wordRegister, RegisterEnum.DX))
									{
										bUsed = false;
										bEndLoop = true;
									}
									if (MatchParameter(instruction.Parameters[0], parameterType, byteRegisterLow, byteRegisterHigh, wordRegister) ||
										MatchWordRegister(parameterType, wordRegister, RegisterEnum.AX))
									{
										bUsed = true;
										bEndLoop = true;
									}
								}
								break;
							case 2:
								if (MatchParameter(instruction.Parameters[0], parameterType, byteRegisterLow, byteRegisterHigh, wordRegister) ||
									MatchParameter(instruction.Parameters[1], parameterType, byteRegisterLow, byteRegisterHigh, wordRegister))
								{
									bUsed = true;
									bEndLoop = true;
								}
								break;
							case 3:
								if (MatchParameter(instruction.Parameters[0], parameterType, byteRegisterLow, byteRegisterHigh, wordRegister))
								{
									bUsed = false;
									bEndLoop = true;
								}
								if (MatchParameterAddressing(instruction.Parameters[0], parameterType, wordRegister) ||
									MatchParameter(instruction.Parameters[1], parameterType, byteRegisterLow, byteRegisterHigh, wordRegister))
								{
									bUsed = true;
									bEndLoop = true;
								}
								break;
							default:
								throw new Exception("IMUL has unknown number of parameters");
						}
						break;
					case InstructionEnum.LDS:
						if (MatchParameter(instruction.Parameters[0], parameterType, byteRegisterLow, byteRegisterHigh, wordRegister) ||
							MatchWordRegister(parameterType, wordRegister, SegmentRegisterEnum.DS))
						{
							bUsed = false;
							bEndLoop = true;
						}
						if (MatchParameter(instruction.Parameters[1], parameterType, byteRegisterLow, byteRegisterHigh, wordRegister) &&
							!MatchWordRegister(parameterType, wordRegister, SegmentRegisterEnum.DS))
						{
							bUsed = true;
							bEndLoop = true;
						}
						break;
					case InstructionEnum.LES:
						if (MatchParameter(instruction.Parameters[0], parameterType, byteRegisterLow, byteRegisterHigh, wordRegister) ||
							MatchWordRegister(parameterType, wordRegister, SegmentRegisterEnum.ES))
						{
							bUsed = false;
							bEndLoop = true;
						}
						if (MatchParameter(instruction.Parameters[1], parameterType, byteRegisterLow, byteRegisterHigh, wordRegister) &&
							!MatchWordRegister(parameterType, wordRegister, SegmentRegisterEnum.ES))
						{
							bUsed = true;
							bEndLoop = true;
						}
						break;
					case InstructionEnum.LEA:
						if (MatchParameter(instruction.Parameters[0], parameterType, byteRegisterLow, byteRegisterHigh, wordRegister))
						{
							bUsed = false;
							bEndLoop = true;
						}
						if (MatchParameter(instruction.Parameters[1], parameterType, byteRegisterLow, byteRegisterHigh, wordRegister))
						{
							bUsed = true;
							bEndLoop = true;
						}
						break;
					case InstructionEnum.MOV:
						if (MatchParameter(instruction.Parameters[0], parameterType, byteRegisterLow, byteRegisterHigh, wordRegister))
						{
							bUsed = false;
							bEndLoop = true;
						}
						if (MatchParameterAddressing(instruction.Parameters[0], parameterType, wordRegister) ||
							MatchParameter(instruction.Parameters[1], parameterType, byteRegisterLow, byteRegisterHigh, wordRegister))
						{
							bUsed = true;
							bEndLoop = true;
						}
						break;
					case InstructionEnum.MOVS:
						if ((instruction.DefaultDataSegment == SegmentRegisterEnum.CS &&
							MatchWordRegister(InstructionParameterTypeEnum.SegmentRegister, wordRegister, SegmentRegisterEnum.CS)) ||
							(instruction.DefaultDataSegment == SegmentRegisterEnum.SS &&
							MatchWordRegister(InstructionParameterTypeEnum.SegmentRegister, wordRegister, SegmentRegisterEnum.SS)) ||
							MatchWordRegister(InstructionParameterTypeEnum.SegmentRegister, wordRegister, SegmentRegisterEnum.ES) ||

							((instruction.DefaultDataSegment != SegmentRegisterEnum.CS && instruction.DefaultDataSegment != SegmentRegisterEnum.ES &&
							instruction.DefaultDataSegment != SegmentRegisterEnum.SS) && MatchWordRegister(InstructionParameterTypeEnum.SegmentRegister, wordRegister, SegmentRegisterEnum.DS)) ||

							MatchWordRegister(parameterType, wordRegister, RegisterEnum.SI) ||
							MatchWordRegister(parameterType, wordRegister, RegisterEnum.DI))
						{
							bUsed = true;
							bEndLoop = true;
						}
						break;
					case InstructionEnum.MOVSX:
					case InstructionEnum.MOVZX:
						if (MatchParameter(instruction.Parameters[0], parameterType, byteRegisterLow, byteRegisterHigh, wordRegister))
						{
							bUsed = false;
							bEndLoop = true;
						}
						if (MatchParameterAddressing(instruction.Parameters[0], parameterType, wordRegister) ||
							MatchParameter(instruction.Parameters[1], parameterType, byteRegisterLow, byteRegisterHigh, wordRegister))
						{
							bUsed = true;
							bEndLoop = true;
						}
						break;
					case InstructionEnum.DEC:
					case InstructionEnum.INC:
					case InstructionEnum.NEG:
					case InstructionEnum.NOT:
						if (MatchParameter(instruction.Parameters[0], parameterType, byteRegisterLow, byteRegisterHigh, wordRegister))
						{
							bUsed = true;
							bEndLoop = true;
						}
						break;

					case InstructionEnum.NOP:
						// ignore this instruction
						break;

					case InstructionEnum.POP:
						if (MatchParameter(instruction.Parameters[0], parameterType, byteRegisterLow, byteRegisterHigh, wordRegister))
						{
							bUsed = false;
							bEndLoop = true;
						}
						break;
					case InstructionEnum.POPA:
						bUsed = false;
						bEndLoop = true;
						break;
					case InstructionEnum.PUSH:
						if (MatchParameter(instruction.Parameters[0], parameterType, byteRegisterLow, byteRegisterHigh, wordRegister))
						{
							bUsed = true;
							bEndLoop = true;
						}
						break;
					case InstructionEnum.PUSHA:
						bUsed = true;
						bEndLoop = true;
						break;

					case InstructionEnum.STD:
						// does not use any register
						break;

					case InstructionEnum.XCHG:
						if (MatchParameter(instruction.Parameters[0], parameterType, byteRegisterLow, byteRegisterHigh, wordRegister) ||
							MatchParameter(instruction.Parameters[1], parameterType, byteRegisterLow, byteRegisterHigh, wordRegister))
						{
							bUsed = true;
							bEndLoop = true;
						}
						break;

					// special syntetic functions
					case InstructionEnum.WordsToDword:
						if (MatchParameter(instruction.Parameters[0], parameterType, byteRegisterLow, byteRegisterHigh, wordRegister))
						{
							bUsed = false;
							bEndLoop = true;
						}
						if (MatchParameterAddressing(instruction.Parameters[0], parameterType, wordRegister) ||
							MatchParameter(instruction.Parameters[1], parameterType, byteRegisterLow, byteRegisterHigh, wordRegister) ||
							MatchParameter(instruction.Parameters[2], parameterType, byteRegisterLow, byteRegisterHigh, wordRegister))
						{
							bUsed = true;
							bEndLoop = true;
						}
						break;

					// flow control instructions
					case InstructionEnum.SWITCH:
						if (MatchParameter(instruction.Parameters[0], parameterType, byteRegisterLow, byteRegisterHigh, wordRegister))
						{
							bUsed = true;
							bEndLoop = true;
						}
						else
						{
							// add jumps to check
							for (int j = 1; j < instruction.Parameters.Count; j++)
							{
								callJumps.Add(instruction.Parameters[j].Displacement);
							}
							bEndLoop = true;
						}
						break;
					case InstructionEnum.Jcc:
						if (instruction.Parameters[1].Type == InstructionParameterTypeEnum.Relative)
						{
							uiNewOffset = (instruction.Offset + (uint)instruction.Bytes.Count + instruction.Parameters[1].Value) & 0xffff;
							callJumps.Add(uiNewOffset);
						}
						else
						{
							throw new Exception("Not a relative jump");
						}
						break;
					case InstructionEnum.LOOP:
						if (instruction.Parameters[0].Type == InstructionParameterTypeEnum.Relative)
						{
							uiNewOffset = (instruction.Offset + (uint)instruction.Bytes.Count + instruction.Parameters[0].Value) & 0xffff;
							callJumps.Add(uiNewOffset);
						}
						else
						{
							throw new Exception("Not a relative jump");
						}
						break;
					case InstructionEnum.JMP:
						if (instruction.Parameters[0].Type == InstructionParameterTypeEnum.Relative)
						{
							uiNewOffset = (instruction.Offset + (uint)instruction.Bytes.Count + instruction.Parameters[0].Value) & 0xffff;
							callJumps.Add(uiNewOffset);
							bEndLoop = true;
						}
						else
						{
							throw new Exception("Not a relative jump");
						}
						break;
					case InstructionEnum.RETF:
						bUsed = false;
						bEndLoop = true;
						break;
					default:
						throw new Exception("Unexpected instruction");
				}

				if (bEndLoop)
					break;
			}

			while (callJumps.Count > 0)
			{
				uint uiOffset = callJumps[callJumps.Count - 1];
				bool bJumpFound = false;

				callJumps.RemoveAt(callJumps.Count - 1);

				for (int i = 0; i < checkedJumps.Count; i++)
				{
					if (checkedJumps[i] == uiOffset)
					{
						bJumpFound = true;
						break;
					}
				}

				if (!bJumpFound)
				{
					checkedJumps.Add(uiOffset);

					for (int i = 0; i < this.aInstructions.Count; i++)
					{
						if (this.aInstructions[i].Offset == uiOffset)
						{
							return CheckIfUsedRecursive(i, endPos, size, parameterType, byteRegisterLow, byteRegisterHigh, wordRegister, callJumps, checkedJumps);
						}
					}
				}
			}

			return bUsed;
		}

		private bool MatchWordRegister(InstructionParameterTypeEnum parameterType, uint wordRegister, RegisterEnum matchingRegister)
		{
			if (parameterType == InstructionParameterTypeEnum.Register && wordRegister == ((uint)matchingRegister & 0x7))
			{
				return true;
			}

			return false;
		}

		private bool MatchWordRegister(InstructionParameterTypeEnum parameterType, uint wordRegister, SegmentRegisterEnum matchingRegister)
		{
			if (parameterType == InstructionParameterTypeEnum.SegmentRegister && wordRegister == (uint)matchingRegister)
			{
				return true;
			}

			return false;
		}

		private bool MatchParameter(InstructionParameter parameter, InstructionParameterTypeEnum parameterType,
			uint byteRegisterLow, uint byteRegisterHigh, uint wordRegister)
		{
			if (parameterType == InstructionParameterTypeEnum.Register)
			{
				if (parameter.Type == parameterType && parameter.Size == InstructionSizeEnum.Byte && 
					(parameter.Value == byteRegisterLow || parameter.Value == byteRegisterHigh))
				{
					return true;
				}
				if (parameter.Type == parameterType &&
					(parameter.Size == InstructionSizeEnum.Word || parameter.Size == InstructionSizeEnum.DWord) &&
					parameter.Value == (wordRegister & 0x7))
				{
					return true;
				}
				if (parameterType == InstructionParameterTypeEnum.Register &&
					(parameter.Type == InstructionParameterTypeEnum.MemoryAddress || parameter.Type == InstructionParameterTypeEnum.LEAMemoryAddress) &&
					(wordRegister == (uint)RegisterEnum.DI || wordRegister == (uint)RegisterEnum.SI || 
					wordRegister == (uint)RegisterEnum.BX || wordRegister == (uint)RegisterEnum.BP))
				{
					switch (parameter.Value)
					{
						case 0:
							// [BX + SI]
							if (wordRegister == (uint)RegisterEnum.BX || wordRegister == (uint)RegisterEnum.SI)
								return true;
							break;
						case 1:
							// [BX + DI]
							if (wordRegister == (uint)RegisterEnum.BX || wordRegister == (uint)RegisterEnum.DI)
								return true;
							break;
						case 2:
							// [BP + SI]
							if (wordRegister == (uint)RegisterEnum.BP || wordRegister == (uint)RegisterEnum.SI)
								return true;
							break;
						case 3:
							// [BP + DI]
							if (wordRegister == (uint)RegisterEnum.BP || wordRegister == (uint)RegisterEnum.DI)
								return true;
							break;
						case 4:
							// [SI]
							if (wordRegister == (uint)RegisterEnum.SI)
								return true;
							break;
						case 5:
							// [DI]
							if (wordRegister == (uint)RegisterEnum.DI)
								return true;
							break;
						case 6:
							// direct displacement
							break;
						case 7:
							// [BX]
							if (wordRegister == (uint)RegisterEnum.BX)
								return true;
							break;
						case 8:
							// [BX + SI {0}]
							if (wordRegister == (uint)RegisterEnum.BX || wordRegister == (uint)RegisterEnum.SI)
								return true;
							break;
						case 9:
							// [BX + DI {0}]
							if (wordRegister == (uint)RegisterEnum.BX || wordRegister == (uint)RegisterEnum.DI)
								return true;
							break;
						case 10:
							// [BP + SI {0}]
							if (wordRegister == (uint)RegisterEnum.BP || wordRegister == (uint)RegisterEnum.SI)
								return true;
							break;
						case 11:
							// [BP + DI {0}]
							if (wordRegister == (uint)RegisterEnum.BP || wordRegister == (uint)RegisterEnum.DI)
								return true;
							break;
						case 12:
							// [SI {0}]
							if (wordRegister == (uint)RegisterEnum.SI)
								return true;
							break;
						case 13:
							// [DI {0}]
							if (wordRegister == (uint)RegisterEnum.DI)
								return true;
							break;
						case 14:
							// [BP {0}]
							if (wordRegister == (uint)RegisterEnum.BP)
								return true;
							break;
						case 15:
							// [BX {0}]
							if (wordRegister == (uint)RegisterEnum.BX)
								return true;
							break;
						case 16:
							// [BX + SI {0}]
							if (wordRegister == (uint)RegisterEnum.BX || wordRegister == (uint)RegisterEnum.SI)
								return true;
							break;
						case 17:
							// [BX + DI {0}]
							if (wordRegister == (uint)RegisterEnum.BX || wordRegister == (uint)RegisterEnum.DI)
								return true;
							break;
						case 18:
							// [BP + SI {0}]
							if (wordRegister == (uint)RegisterEnum.BP || wordRegister == (uint)RegisterEnum.SI)
								return true;
							break;
						case 19:
							// [BP + DI {0}]
							if (wordRegister == (uint)RegisterEnum.BP || wordRegister == (uint)RegisterEnum.DI)
								return true;
							break;
						case 20:
							// [SI {0}]
							if (wordRegister == (uint)RegisterEnum.SI)
								return true;
							break;
						case 21:
							// [DI {0}]
							if (wordRegister == (uint)RegisterEnum.DI)
								return true;
							break;
						case 22:
							// [BP {0}]
							if (wordRegister == (uint)RegisterEnum.BP)
								return true;
							break;
						case 23:
							// [BX {0}]
							if (wordRegister == (uint)RegisterEnum.BX)
								return true;
							break;
					}
				}
				else if (parameterType == InstructionParameterTypeEnum.SegmentRegister &&
					(parameter.Type == InstructionParameterTypeEnum.MemoryAddress || parameter.Type == InstructionParameterTypeEnum.LEAMemoryAddress) &&
					parameter.DataSegment == (SegmentRegisterEnum)wordRegister)
				{
					return true;
				}
			}
			else if (parameterType == InstructionParameterTypeEnum.SegmentRegister)
			{
				if (parameter.Type == parameterType && parameter.Value == wordRegister ||
					((parameter.Type == InstructionParameterTypeEnum.MemoryAddress || parameter.Type == InstructionParameterTypeEnum.LEAMemoryAddress) && 
					parameter.DataSegment == (SegmentRegisterEnum)wordRegister))
				{
					return true;
				}
			}

			return false;
		}

		private bool MatchParameterAddressing(InstructionParameter parameter, InstructionParameterTypeEnum parameterType, uint wordRegister)
		{
			if (parameterType == InstructionParameterTypeEnum.Register &&
				(parameter.Type == InstructionParameterTypeEnum.MemoryAddress || parameter.Type == InstructionParameterTypeEnum.LEAMemoryAddress) &&
				(wordRegister == (uint)RegisterEnum.DI || wordRegister == (uint)RegisterEnum.SI ||
				wordRegister == (uint)RegisterEnum.BX || wordRegister == (uint)RegisterEnum.BP))
			{
				switch (parameter.Value)
				{
					case 0:
						// [BX + SI]
						if (wordRegister == (uint)RegisterEnum.BX || wordRegister == (uint)RegisterEnum.SI)
							return true;
						break;
					case 1:
						// [BX + DI]
						if (wordRegister == (uint)RegisterEnum.BX || wordRegister == (uint)RegisterEnum.DI)
							return true;
						break;
					case 2:
						// [BP + SI]
						if (wordRegister == (uint)RegisterEnum.BP || wordRegister == (uint)RegisterEnum.SI)
							return true;
						break;
					case 3:
						// [BP + DI]
						if (wordRegister == (uint)RegisterEnum.BP || wordRegister == (uint)RegisterEnum.DI)
							return true;
						break;
					case 4:
						// [SI]
						if (wordRegister == (uint)RegisterEnum.SI)
							return true;
						break;
					case 5:
						// [DI]
						if (wordRegister == (uint)RegisterEnum.DI)
							return true;
						break;
					case 6:
						// direct displacement
						break;
					case 7:
						// [BX]
						if (wordRegister == (uint)RegisterEnum.BX)
							return true;
						break;
					case 8:
						// [BX + SI {0}]
						if (wordRegister == (uint)RegisterEnum.BX || wordRegister == (uint)RegisterEnum.SI)
							return true;
						break;
					case 9:
						// [BX + DI {0}]
						if (wordRegister == (uint)RegisterEnum.BX || wordRegister == (uint)RegisterEnum.DI)
							return true;
						break;
					case 10:
						// [BP + SI {0}]
						if (wordRegister == (uint)RegisterEnum.BP || wordRegister == (uint)RegisterEnum.SI)
							return true;
						break;
					case 11:
						// [BP + DI {0}]
						if (wordRegister == (uint)RegisterEnum.BP || wordRegister == (uint)RegisterEnum.DI)
							return true;
						break;
					case 12:
						// [SI {0}]
						if (wordRegister == (uint)RegisterEnum.SI)
							return true;
						break;
					case 13:
						// [DI {0}]
						if (wordRegister == (uint)RegisterEnum.DI)
							return true;
						break;
					case 14:
						// [BP {0}]
						if (wordRegister == (uint)RegisterEnum.BP)
							return true;
						break;
					case 15:
						// [BX {0}]
						if (wordRegister == (uint)RegisterEnum.BX)
							return true;
						break;
					case 16:
						// [BX + SI {0}]
						if (wordRegister == (uint)RegisterEnum.BX || wordRegister == (uint)RegisterEnum.SI)
							return true;
						break;
					case 17:
						// [BX + DI {0}]
						if (wordRegister == (uint)RegisterEnum.BX || wordRegister == (uint)RegisterEnum.DI)
							return true;
						break;
					case 18:
						// [BP + SI {0}]
						if (wordRegister == (uint)RegisterEnum.BP || wordRegister == (uint)RegisterEnum.SI)
							return true;
						break;
					case 19:
						// [BP + DI {0}]
						if (wordRegister == (uint)RegisterEnum.BP || wordRegister == (uint)RegisterEnum.DI)
							return true;
						break;
					case 20:
						// [SI {0}]
						if (wordRegister == (uint)RegisterEnum.SI)
							return true;
						break;
					case 21:
						// [DI {0}]
						if (wordRegister == (uint)RegisterEnum.DI)
							return true;
						break;
					case 22:
						// [BP {0}]
						if (wordRegister == (uint)RegisterEnum.BP)
							return true;
						break;
					case 23:
						// [BX {0}]
						if (wordRegister == (uint)RegisterEnum.BX)
							return true;
						break;
				}
			}
			else if (parameterType == InstructionParameterTypeEnum.SegmentRegister &&
				(parameter.Type == InstructionParameterTypeEnum.MemoryAddress || parameter.Type == InstructionParameterTypeEnum.LEAMemoryAddress) &&
				parameter.DataSegment == (SegmentRegisterEnum)wordRegister)
			{
				return true;
			}

			return false;
		}

		private ushort ReadWord(MemoryStream stream)
		{
			int byte0 = stream.ReadByte();
			int byte1 = stream.ReadByte();

			byte0 &= 0xff;
			byte1 &= 0xff;

			return (ushort)((byte1 << 8) | byte0);
		}

		public CDecompiler Parent
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

		public uint Segment
		{
			get { return this.usSegment; }
		}

		public uint Offset
		{
			get { return this.usOffset; }
		}

		public List<Instruction> Instructions
		{
			get { return this.aInstructions; }
		}

		public CFunctionNamespace LocalNamespace
		{
			get { return this.oLocalNamespace; }
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
