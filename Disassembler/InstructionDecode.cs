using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Disassembler
{
	public partial class Instruction
	{
		private void Decode(MemoryStream stream)
		{
			bool bPrefix;
			bool bSignExtendImmediate = false;
			bool bReverseDirection = false;
			this.eOperandSize = this.eDefaultSize;
			this.eAddressSize = this.eDefaultSize;

			do
			{
				bool bExitCase = false;
				this.iByte0 = stream.ReadByte();
				if (this.iByte0 < 0)
				{
					bInvalid = true;
					return;
				}
				this.aBytes.Add((byte)this.iByte0);

				bPrefix = false;
				switch (this.iByte0)
				{
					case 0x00:
					case 0x01:
					case 0x02:
					case 0x03:
						// ADD (7)
						bReverseDirection = (this.iByte0 & 0x02) == 0;
						eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;

						// OpCode byte: 1
						this.iByte1 = stream.ReadByte();
						if (this.iByte1 < 0)
						{
							bInvalid = true;
							return;
						}
						this.aBytes.Add((byte)this.iByte1);

						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte1 & 0x38) >> 3)));
						this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
						this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.ZF | FlagsEnum.SF | FlagsEnum.OF;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.ADD;
							this.sDescription = "Add register to register/memory; Add register/memory to register";
						}
						break;
					case 0x04:
					case 0x05:
						// ADD (8)
						eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, 0));
						this.aParameters.Add(ReadImmediate(stream, 0, eOperandSize, bSignExtendImmediate));
						this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.ZF | FlagsEnum.SF | FlagsEnum.OF;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.ADD;
							this.sDescription = "Add immediate to accumulator";
						}
						break;
					case 0x06:
					case 0x0e:
					case 0x16:
					case 0x1e:
						// PUSH (121)
						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.SegmentRegister, eOperandSize, (uint)((this.iByte0 & 0x18) >> 3)));
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.PUSH;
							this.sDescription = "Push Operand onto the Stack Segment register (ES, CS, SS, or DS)";
						}
						break;
					case 0x07:
					case 0x17:
					case 0x1f:
						// POP (107)
						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.SegmentRegister, eOperandSize, (uint)((this.iByte0 & 0x18) >> 3)));
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.POP;
							this.sDescription = "Pop a Word from the Stack Segment register (ES, SS, or DS)";
						}
						break;
					case 0x08:
					case 0x09:
					case 0x0a:
					case 0x0b:
						// OR (101)
						bReverseDirection = (this.iByte0 & 0x02) == 0;
						eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;

						// OpCode byte: 1
						this.iByte1 = stream.ReadByte();
						if (this.iByte1 < 0)
						{
							bInvalid = true;
							return;
						}
						this.aBytes.Add((byte)this.iByte1);

						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte1 & 0x38) >> 3)));
						this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
						this.eClearedFlags = FlagsEnum.CF | FlagsEnum.OF;
						this.eModifiedFlags = FlagsEnum.PF | FlagsEnum.ZF | FlagsEnum.SF;
						this.eUndefinedFlags = FlagsEnum.AF;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.OR;
							this.sDescription = "Logical Inclusive OR register to register/memory; Logical Inclusive OR register/memory to register";
						}
						break;
					case 0x0c:
					case 0x0d:
						// OR (102)
						eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, 0));
						this.aParameters.Add(ReadImmediate(stream, 0, eOperandSize, bSignExtendImmediate));
						this.eClearedFlags = FlagsEnum.CF | FlagsEnum.OF;
						this.eModifiedFlags = FlagsEnum.PF | FlagsEnum.ZF | FlagsEnum.SF;
						this.eUndefinedFlags = FlagsEnum.AF;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.OR;
							this.sDescription = "Logical Inclusive OR immediate to accumulator";
						}
						break;
					case 0x0f:
						// Multiple instructions, alternate method
						this.iByte1 = stream.ReadByte();
						if (this.iByte1 < 0)
						{
							bInvalid = true;
							return;
						}
						this.aBytes.Add((byte)this.iByte1);

						bExitCase = false;
						switch (this.iByte1)
						{
							case 0x00:
								this.iByte2 = stream.ReadByte();
								if (this.iByte2 < 0)
								{
									bInvalid = true;
									return;
								}
								this.aBytes.Add((byte)this.iByte2);

								switch (this.iByte2 & 0x38)
								{
									case 0x00:
										// SLDT (167)
										this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte2 & 0xc7));
										if (!bInvalid)
										{
											this.eCPUType = CPUEnum.i80286;
											this.eInstruction = InstructionEnum.SLDT;
											this.sDescription = "Store Local Descriptor Table register to register/memory";
										}
										break;
									case 0x08:
										// STR (173)
										this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte2 & 0xc7));
										if (!bInvalid)
										{
											this.eCPUType = CPUEnum.i80286;
											this.eInstruction = InstructionEnum.STR;
											this.sDescription = "Store Task register to register/memory";
										}
										break;
									case 0x10:
										// LLDT (79)
										this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte2 & 0xc7));
										if (!bInvalid)
										{
											this.eCPUType = CPUEnum.i80286;
											this.eInstruction = InstructionEnum.LLDT;
											this.sDescription = "Load Local Descriptor Table register from register/memory";
										}
										break;
									case 0x18:
										// LTR (88)
										this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte2 & 0xc7));
										if (!bInvalid)
										{
											this.eCPUType = CPUEnum.i80286;
											this.eInstruction = InstructionEnum.LTR;
											this.sDescription = "Load Task register from register/memory";
										}
										break;
									case 0x20:
										// VERR (180)
										this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte2 & 0xc7));
										this.eModifiedFlags = FlagsEnum.ZF;
										if (!bInvalid)
										{
											this.eCPUType = CPUEnum.i80286;
											this.eInstruction = InstructionEnum.VERR;
											this.sDescription = "Verify a Segment for Reading register/Memory";
										}
										break;
									case 0x28:
										// VERW (181)
										this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte2 & 0xc7));
										this.eModifiedFlags = FlagsEnum.ZF;
										if (!bInvalid)
										{
											this.eCPUType = CPUEnum.i80286;
											this.eInstruction = InstructionEnum.VERW;
											this.sDescription = "Verify a Segment for Writing To register/memory";
										}
										break;
									default:
										bInvalid = true;
										break;
								}
								bExitCase = true;
								break;
							case 0x01:
								this.iByte2 = stream.ReadByte();
								if (this.iByte2 < 0)
								{
									bInvalid = true;
									return;
								}
								this.aBytes.Add((byte)this.iByte2);

								switch (this.iByte2 & 0x38)
								{
									case 0x00:
										// SGDT (155)
										this.aParameters.Add(MemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte2 & 0xc7));
										if (!bInvalid)
										{
											this.eCPUType = CPUEnum.i80286;
											this.eInstruction = InstructionEnum.SGDT;
											this.sDescription = "Store Global Descriptor Table register";
										}
										break;
									case 0x08:
										// SIDT (166)
										this.aParameters.Add(MemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte2 & 0xc7));
										if (!bInvalid)
										{
											this.eCPUType = CPUEnum.i80286;
											this.eInstruction = InstructionEnum.SIDT;
											this.sDescription = "Store Interrupt Descriptor Table register";
										}
										break;
									case 0x10:
										// LGDT (76)
										this.aParameters.Add(MemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte2 & 0xc7));
										if (!bInvalid)
										{
											this.eCPUType = CPUEnum.i80286;
											this.eInstruction = InstructionEnum.LGDT;
											this.sDescription = "Load Global Descriptor Table register";
										}
										break;
									case 0x18:
										// LIDT (78)
										this.aParameters.Add(MemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte2 & 0xc7));
										if (!bInvalid)
										{
											this.eCPUType = CPUEnum.i80286;
											this.eInstruction = InstructionEnum.LIDT;
											this.sDescription = "Load Interrupt Descriptor Table register";
										}
										break;
									case 0x20:
										// SMSW (168)
										this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte2 & 0xc7));
										if (!bInvalid)
										{
											this.eCPUType = CPUEnum.i80286;
											this.eInstruction = InstructionEnum.SMSW;
											this.sDescription = "Store Machine Status Word to register/memory";
										}
										break;
									case 0x30:
										// LMSW (80)
										this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte2 & 0xc7));
										if (!bInvalid)
										{
											this.eCPUType = CPUEnum.i80286;
											this.eInstruction = InstructionEnum.LMSW;
											this.sDescription = "Load Machine Status Word from register/memory";
										}
										break;
									default:
										bInvalid = true;
										break;
								}
								bExitCase = true;
								break;
							case 0x02:
								// LAR (70)

								// OpCode byte: 2
								this.iByte2 = stream.ReadByte();
								if (this.iByte2 < 0)
								{
									bInvalid = true;
									return;
								}
								this.aBytes.Add((byte)this.iByte2);

								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte2 & 0x38) >> 3)));
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte2 & 0xc7));
								this.eModifiedFlags = FlagsEnum.ZF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i80286;
									this.eInstruction = InstructionEnum.LAR;
									this.sDescription = "Load Access Rights Byte From register/memory";
								}
								bExitCase = true;
								break;
							case 0x03:
								// LSL (86)

								// OpCode byte: 2
								this.iByte2 = stream.ReadByte();
								if (this.iByte2 < 0)
								{
									bInvalid = true;
									return;
								}
								this.aBytes.Add((byte)this.iByte2);

								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte2 & 0x38) >> 3)));
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte2 & 0xc7));
								this.eModifiedFlags = FlagsEnum.ZF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i80286;
									this.eInstruction = InstructionEnum.LSL;
									this.sDescription = "Load Segment Limit from register/memory";
								}
								bExitCase = true;
								break;
							case 0x06:
								// CLTS (34)
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i80286;
									this.eInstruction = InstructionEnum.CLTS;
									this.sDescription = "Clear Task-Switched Flag in CR0";
								}
								bExitCase = true;
								break;
							case 0xa3:
								// BT (18)

								// OpCode byte: 2
								this.iByte2 = stream.ReadByte();
								if (this.iByte2 < 0)
								{
									bInvalid = true;
									return;
								}
								this.aBytes.Add((byte)this.iByte2);

								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte2 & 0xc7));
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte2 & 0x38) >> 3)));
								this.eModifiedFlags = FlagsEnum.CF;
								this.eUndefinedFlags = FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.ZF | FlagsEnum.SF | FlagsEnum.OF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.BT;
									this.sDescription = "Bit Test register/memory, register";
								}
								bExitCase = true;
								break;
							case 0xa4:
								// SHLD (159)

								// OpCode byte: 2
								this.iByte2 = stream.ReadByte();
								if (this.iByte2 < 0)
								{
									bInvalid = true;
									return;
								}
								this.aBytes.Add((byte)this.iByte2);

								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte2 & 0xc7));
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte2 & 0x38) >> 3)));
								this.aParameters.Add(ReadImmediate(stream, 1, eOperandSize, bSignExtendImmediate));
								this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.PF | FlagsEnum.ZF | FlagsEnum.SF;
								this.eUndefinedFlags = FlagsEnum.AF | FlagsEnum.OF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.SHLD;
									this.sDescription = "Double Precision Shift Left register, register/memory by immediate";
								}
								bExitCase = true;
								break;
							case 0xa5:
								// SHLD (160)

								// OpCode byte: 2
								this.iByte2 = stream.ReadByte();
								if (this.iByte2 < 0)
								{
									bInvalid = true;
									return;
								}
								this.aBytes.Add((byte)this.iByte2);

								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte2 & 0xc7));
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte2 & 0x38) >> 3)));
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, InstructionSizeEnum.Byte, 1));
								this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.PF | FlagsEnum.ZF | FlagsEnum.SF;
								this.eUndefinedFlags = FlagsEnum.AF | FlagsEnum.OF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.SHLD;
									this.sDescription = "Double Precision Shift Left register, register/memory by CL";
								}
								bExitCase = true;
								break;
							case 0xab:
								// BTS (24)

								// OpCode byte: 2
								this.iByte2 = stream.ReadByte();
								if (this.iByte2 < 0)
								{
									bInvalid = true;
									return;
								}
								this.aBytes.Add((byte)this.iByte2);

								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte2 & 0xc7));
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte2 & 0x38) >> 3)));
								this.eModifiedFlags = FlagsEnum.CF;
								this.eUndefinedFlags = FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.ZF | FlagsEnum.SF | FlagsEnum.OF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.BTS;
									this.sDescription = "Bit Test and Set register/memory, register";
								}
								bExitCase = true;
								break;
							case 0xac:
								// SHRD (164)

								// OpCode byte: 2
								this.iByte2 = stream.ReadByte();
								if (this.iByte2 < 0)
								{
									bInvalid = true;
									return;
								}
								this.aBytes.Add((byte)this.iByte2);

								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte2 & 0xc7));
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte2 & 0x38) >> 3)));
								this.aParameters.Add(ReadImmediate(stream, 1, eOperandSize, bSignExtendImmediate));
								this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.PF | FlagsEnum.ZF | FlagsEnum.SF;
								this.eUndefinedFlags = FlagsEnum.AF | FlagsEnum.OF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.SHRD;
									this.sDescription = "Double Precision Shift Right register, register/memory by immediate";
								}
								bExitCase = true;
								break;
							case 0xad:
								// SHRD (165)

								// OpCode byte: 2
								this.iByte2 = stream.ReadByte();
								if (this.iByte2 < 0)
								{
									bInvalid = true;
									return;
								}
								this.aBytes.Add((byte)this.iByte2);

								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte2 & 0xc7));
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte2 & 0x38) >> 3)));
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, InstructionSizeEnum.Byte, 1));
								this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.PF | FlagsEnum.ZF | FlagsEnum.SF;
								this.eUndefinedFlags = FlagsEnum.AF | FlagsEnum.OF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.SHRD;
									this.sDescription = "Double Precision Shift Right register, register/memory by CL";
								}
								bExitCase = true;
								break;
							case 0xaf:
								// IMUL (49)

								// OpCode byte: 2
								this.iByte2 = stream.ReadByte();
								if (this.iByte2 < 0)
								{
									bInvalid = true;
									return;
								}
								this.aBytes.Add((byte)this.iByte2);

								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte2 & 0x38) >> 3)));
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte2 & 0xc7));
								this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.OF;
								this.eUndefinedFlags = FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.ZF | FlagsEnum.SF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i80186;
									this.eInstruction = InstructionEnum.IMUL;
									this.sDescription = "Signed Multiply register with register/memory to register";
								}
								bExitCase = true;
								break;
							case 0xb2:
								// LSS (87)

								// OpCode byte: 2
								this.iByte2 = stream.ReadByte();
								if (this.iByte2 < 0)
								{
									bInvalid = true;
									return;
								}
								this.aBytes.Add((byte)this.iByte2);

								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte2 & 0x38) >> 3)));
								this.aParameters.Add(MemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte2 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i80386;
									this.eInstruction = InstructionEnum.LSS;
									this.sDescription = "Load Pointer to SS";
								}
								bExitCase = true;
								break;
							case 0xb3:
								// BTR (22)

								// OpCode byte: 2
								this.iByte2 = stream.ReadByte();
								if (this.iByte2 < 0)
								{
									bInvalid = true;
									return;
								}
								this.aBytes.Add((byte)this.iByte2);

								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte2 & 0xc7));
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte2 & 0x38) >> 3)));
								this.eModifiedFlags = FlagsEnum.CF;
								this.eUndefinedFlags = FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.ZF | FlagsEnum.SF | FlagsEnum.OF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.BTR;
									this.sDescription = "Bit Test and Reset register/memory, register";
								}
								bExitCase = true;
								break;
							case 0xb4:
								// LFS (75)

								// OpCode byte: 2
								this.iByte2 = stream.ReadByte();
								if (this.iByte2 < 0)
								{
									bInvalid = true;
									return;
								}
								this.aBytes.Add((byte)this.iByte2);

								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte2 & 0x38) >> 3)));
								this.aParameters.Add(MemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte2 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i80386;
									this.eInstruction = InstructionEnum.LFS;
									this.sDescription = "Load Pointer to FS";
								}
								bExitCase = true;
								break;
							case 0xb5:
								// LGS (77)

								// OpCode byte: 2
								this.iByte2 = stream.ReadByte();
								if (this.iByte2 < 0)
								{
									bInvalid = true;
									return;
								}
								this.aBytes.Add((byte)this.iByte2);

								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte2 & 0x38) >> 3)));
								this.aParameters.Add(MemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte2 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i80386;
									this.eInstruction = InstructionEnum.LGS;
									this.sDescription = "Load Pointer to GS";
								}
								bExitCase = true;
								break;
							case 0xba:
								this.iByte2 = stream.ReadByte();
								if (this.iByte2 < 0)
								{
									bInvalid = true;
									return;
								}
								this.aBytes.Add((byte)this.iByte2);

								switch (this.iByte2 & 0x38)
								{
									case 0x20:
										// BT (19)
										this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte2 & 0xc7));
										this.aParameters.Add(ReadImmediate(stream, 1, eOperandSize, bSignExtendImmediate));
										this.eModifiedFlags = FlagsEnum.CF;
										this.eUndefinedFlags = FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.ZF | FlagsEnum.SF | FlagsEnum.OF;
										if (!bInvalid)
										{
											this.eCPUType = CPUEnum.i8086;
											this.eInstruction = InstructionEnum.BT;
											this.sDescription = "Bit Test register/memory, immediate";
										}
										break;
									case 0x28:
										// BTS (25)
										this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte2 & 0xc7));
										this.aParameters.Add(ReadImmediate(stream, 1, eOperandSize, bSignExtendImmediate));
										this.eModifiedFlags = FlagsEnum.CF;
										this.eUndefinedFlags = FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.ZF | FlagsEnum.SF | FlagsEnum.OF;
										if (!bInvalid)
										{
											this.eCPUType = CPUEnum.i8086;
											this.eInstruction = InstructionEnum.BTS;
											this.sDescription = "Bit Test and Set register/memory, immediate";
										}
										break;
									case 0x30:
										// BTR (23)
										this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte2 & 0xc7));
										this.aParameters.Add(ReadImmediate(stream, 1, eOperandSize, bSignExtendImmediate));
										this.eModifiedFlags = FlagsEnum.CF;
										this.eUndefinedFlags = FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.ZF | FlagsEnum.SF | FlagsEnum.OF;
										if (!bInvalid)
										{
											this.eCPUType = CPUEnum.i8086;
											this.eInstruction = InstructionEnum.BTR;
											this.sDescription = "Bit Test and Reset register/memory, immediate";
										}
										break;
									case 0x38:
										// BTC (20)
										this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte2 & 0xc7));
										this.aParameters.Add(ReadImmediate(stream, 1, eOperandSize, bSignExtendImmediate));
										this.eModifiedFlags = FlagsEnum.CF;
										this.eUndefinedFlags = FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.ZF | FlagsEnum.SF | FlagsEnum.OF;
										if (!bInvalid)
										{
											this.eCPUType = CPUEnum.i8086;
											this.eInstruction = InstructionEnum.BTC;
											this.sDescription = "Bit Test and Complement register/memory, immediate";
										}
										break;
									default:
										bInvalid = true;
										break;
								}
								bExitCase = true;
								break;
							case 0xbb:
								// BTC (21)

								// OpCode byte: 2
								this.iByte2 = stream.ReadByte();
								if (this.iByte2 < 0)
								{
									bInvalid = true;
									return;
								}
								this.aBytes.Add((byte)this.iByte2);

								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte2 & 0xc7));
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte2 & 0x38) >> 3)));
								this.eModifiedFlags = FlagsEnum.CF;
								this.eUndefinedFlags = FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.ZF | FlagsEnum.SF | FlagsEnum.OF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.BTC;
									this.sDescription = "Bit Test and Complement register/memory, register";
								}
								bExitCase = true;
								break;
							case 0xbc:
								// BSF (15)

								// OpCode byte: 2
								this.iByte2 = stream.ReadByte();
								if (this.iByte2 < 0)
								{
									bInvalid = true;
									return;
								}
								this.aBytes.Add((byte)this.iByte2);

								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte2 & 0x38) >> 3)));
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte2 & 0xc7));
								this.eModifiedFlags = FlagsEnum.ZF;
								this.eUndefinedFlags = FlagsEnum.CF | FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.SF | FlagsEnum.OF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.BSF;
									this.sDescription = "Bit Scan Forward register/memory, register";
								}
								bExitCase = true;
								break;
							case 0xbd:
								// BSR (16)

								// OpCode byte: 2
								this.iByte2 = stream.ReadByte();
								if (this.iByte2 < 0)
								{
									bInvalid = true;
									return;
								}
								this.aBytes.Add((byte)this.iByte2);

								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte2 & 0x38) >> 3)));
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte2 & 0xc7));
								this.eModifiedFlags = FlagsEnum.ZF;
								this.eUndefinedFlags = FlagsEnum.CF | FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.SF | FlagsEnum.OF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.BSR;
									this.sDescription = "Bit Scan Reverse register/memory, register";
								}
								bExitCase = true;
								break;
							default:
								break;
						}
						if (bExitCase)
							break;
						bExitCase = false;
						switch (this.iByte1 & 0xfe)
						{
							case 0xb6:
								// MOVZX (96)
								// OpCode byte: 2
								this.iByte2 = stream.ReadByte();
								if (this.iByte2 < 0)
								{
									bInvalid = true;
									return;
								}
								this.aBytes.Add((byte)this.iByte2);

								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte2 & 0x38) >> 3)));
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, 
									(eOperandSize == InstructionSizeEnum.Word) ? InstructionSizeEnum.Byte : ((this.iByte1 & 0x01) != 0) ? InstructionSizeEnum.Word : InstructionSizeEnum.Byte, 
									eAddressSize, this.iByte2 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i80386;
									this.eInstruction = InstructionEnum.MOVZX;
									this.sDescription = "Move with Zero-Extend register/memory to register";
								}
								bExitCase = true;
								break;
							case 0xbe:
								// MOVSX (95)
								// OpCode byte: 2
								this.iByte2 = stream.ReadByte();
								if (this.iByte2 < 0)
								{
									bInvalid = true;
									return;
								}
								this.aBytes.Add((byte)this.iByte2);

								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte2 & 0x38) >> 3)));
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment,
									(eOperandSize == InstructionSizeEnum.Word) ? InstructionSizeEnum.Byte : ((this.iByte1 & 0x01) != 0) ? InstructionSizeEnum.Word : InstructionSizeEnum.Byte,
									eAddressSize, this.iByte2 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i80386;
									this.eInstruction = InstructionEnum.MOVSX;
									this.sDescription = "Move with Sign-Extend register/memory to register";
								}
								bExitCase = true;
								break;
							default:
								break;
						}
						if (bExitCase)
							break;
						if ((this.iByte1 & 0xf8) == 0xc8)
						{
							// BSWAP (17)
							this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte1 & 0x07) >> 0)));
							if (!bInvalid)
							{
								this.eCPUType = CPUEnum.i80486;
								this.eInstruction = InstructionEnum.BSWAP;
								this.sDescription = "Byte swap";
							}
							break;
						}
						bExitCase = false;
						switch (this.iByte1 & 0xc7)
						{
							case 0x80:
								// PUSH (122)
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.SegmentRegister, eOperandSize, (uint)((this.iByte1 & 0x38) >> 3)));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i80386;
									this.eInstruction = InstructionEnum.PUSH;
									this.sDescription = "Push Operand onto the Stack Segment register (FS or GS)";
								}
								bExitCase = true;
								break;
							case 0x81:
								// POP (108)
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.SegmentRegister, eOperandSize, (uint)((this.iByte1 & 0x38) >> 3)));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i80386;
									this.eInstruction = InstructionEnum.POP;
									this.sDescription = "Pop a Word from the Stack Segment register (FS or GS)";
								}
								bExitCase = true;
								break;
							default:
								break;
						}
						if (bExitCase)
							break;
						bExitCase = false;
						switch (this.iByte1 & 0xf0)
						{
							case 0x80:
								// Jcc (61)
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Condition, (uint)((this.iByte1 & 0x0f) >> 0)));
								this.aParameters.Add(ReadRelative(stream, 2, eOperandSize));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.Jcc;
									this.sDescription = "Jump if Condition is Met, Full displacement";
								}
								bExitCase = true;
								break;
							case 0x90:
								// SETcc (154)
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Condition, (uint)((this.iByte1 & 0x0f) >> 0)));

								// OpCode byte: 2
								this.iByte2 = stream.ReadByte();
								if (this.iByte2 < 0)
								{
									bInvalid = true;
									return;
								}
								this.aBytes.Add((byte)this.iByte2);

								if ((this.iByte2 & 0x38) != 0x00)
								{
									bInvalid = true;
								}
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte2 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i80386;
									this.eInstruction = InstructionEnum.SETcc;
									this.sDescription = "Byte Set on Condition register/memory";
								}
								bExitCase = true;
								break;
							default:
								break;
						}
						if (bExitCase)
							break;
						break;
					case 0x10:
					case 0x11:
					case 0x12:
					case 0x13:
						// ADC (4)
						bReverseDirection = (this.iByte0 & 0x02) == 0;
						eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;

						// OpCode byte: 1
						this.iByte1 = stream.ReadByte();
						if (this.iByte1 < 0)
						{
							bInvalid = true;
							return;
						}
						this.aBytes.Add((byte)this.iByte1);

						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte1 & 0x38) >> 3)));
						this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
						this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.ZF | FlagsEnum.SF | FlagsEnum.OF;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.ADC;
							this.sDescription = "Add with Carry register to register/memory; Add with Carry register/memory to register";
						}
						break;
					case 0x14:
					case 0x15:
						// ADC (5)
						eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, 0));
						this.aParameters.Add(ReadImmediate(stream, 0, eOperandSize, bSignExtendImmediate));
						this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.ZF | FlagsEnum.SF | FlagsEnum.OF;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.ADC;
							this.sDescription = "Add with Carry immediate to accumulator";
						}
						break;
					case 0x18:
					case 0x19:
					case 0x1a:
					case 0x1b:
						// SBB (150)
						bReverseDirection = (this.iByte0 & 0x02) == 0;
						eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;

						// OpCode byte: 1
						this.iByte1 = stream.ReadByte();
						if (this.iByte1 < 0)
						{
							bInvalid = true;
							return;
						}
						this.aBytes.Add((byte)this.iByte1);

						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte1 & 0x38) >> 3)));
						this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
						this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.ZF | FlagsEnum.SF | FlagsEnum.OF;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.SBB;
							this.sDescription = "Integer Subtraction with Borrow register to register/memory; Integer Subtraction with Borrow register/memory to register";
						}
						break;
					case 0x1c:
					case 0x1d:
						// SBB (151)
						eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, 0));
						this.aParameters.Add(ReadImmediate(stream, 0, eOperandSize, bSignExtendImmediate));
						this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.ZF | FlagsEnum.SF | FlagsEnum.OF;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.SBB;
							this.sDescription = "Integer Subtraction with Borrow immediate to accumulator";
						}
						break;
					case 0x20:
					case 0x21:
					case 0x22:
					case 0x23:
						// AND (10)
						bReverseDirection = (this.iByte0 & 0x02) == 0;
						eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;

						// OpCode byte: 1
						this.iByte1 = stream.ReadByte();
						if (this.iByte1 < 0)
						{
							bInvalid = true;
							return;
						}
						this.aBytes.Add((byte)this.iByte1);

						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte1 & 0x38) >> 3)));
						this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
						this.eClearedFlags = FlagsEnum.CF | FlagsEnum.OF;
						this.eModifiedFlags = FlagsEnum.PF | FlagsEnum.ZF | FlagsEnum.SF;
						this.eUndefinedFlags = FlagsEnum.AF;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.AND;
							this.sDescription = "Logical AND register to register/memory; Logical AND register/memory to register";
						}
						break;
					case 0x24:
					case 0x25:
						// AND (11)
						eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, 0));
						this.aParameters.Add(ReadImmediate(stream, 0, eOperandSize, bSignExtendImmediate));
						this.eClearedFlags = FlagsEnum.CF | FlagsEnum.OF;
						this.eModifiedFlags = FlagsEnum.PF | FlagsEnum.ZF | FlagsEnum.SF;
						this.eUndefinedFlags = FlagsEnum.AF;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.AND;
							this.sDescription = "Logical AND immediate to accumulator";
						}
						break;
					case 0x26:
						// Prefix: ES
						this.eDefaultDataSegment = SegmentRegisterEnum.ES;
						bPrefix = true;
						break;
					case 0x27:
						// DAA (41)
						this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.ZF | FlagsEnum.SF;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.DAA;
							this.sDescription = "Decimal Adjust AL after Addition";
						}
						break;
					case 0x28:
					case 0x29:
					case 0x2a:
					case 0x2b:
						// SUB (174)
						bReverseDirection = (this.iByte0 & 0x02) == 0;
						eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;

						// OpCode byte: 1
						this.iByte1 = stream.ReadByte();
						if (this.iByte1 < 0)
						{
							bInvalid = true;
							return;
						}
						this.aBytes.Add((byte)this.iByte1);

						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte1 & 0x38) >> 3)));
						this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
						this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.ZF | FlagsEnum.SF | FlagsEnum.OF;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.SUB;
							this.sDescription = "Integer Subtraction register to register/memory; Integer Subtraction register/memory to register";
						}
						break;
					case 0x2c:
					case 0x2d:
						// SUB (175)
						eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, 0));
						this.aParameters.Add(ReadImmediate(stream, 0, eOperandSize, bSignExtendImmediate));
						this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.ZF | FlagsEnum.SF | FlagsEnum.OF;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.SUB;
							this.sDescription = "Integer Subtraction immediate to accumulator";
						}
						break;
					case 0x2e:
						// Prefix: CS
						this.eDefaultDataSegment = SegmentRegisterEnum.CS;
						bPrefix = true;
						break;
					case 0x2f:
						// DAS (42)
						this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.ZF | FlagsEnum.SF;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.DAS;
							this.sDescription = "Decimal Adjust AL after Subtraction";
						}
						break;
					case 0x30:
					case 0x31:
					case 0x32:
					case 0x33:
						// XOR (186)
						bReverseDirection = (this.iByte0 & 0x02) == 0;
						eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;

						// OpCode byte: 1
						this.iByte1 = stream.ReadByte();
						if (this.iByte1 < 0)
						{
							bInvalid = true;
							return;
						}
						this.aBytes.Add((byte)this.iByte1);

						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte1 & 0x38) >> 3)));
						this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
						this.eClearedFlags = FlagsEnum.CF | FlagsEnum.OF;
						this.eModifiedFlags = FlagsEnum.PF | FlagsEnum.ZF | FlagsEnum.SF;
						this.eUndefinedFlags = FlagsEnum.AF;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.XOR;
							this.sDescription = "Logical Exclusive OR register to register/memory; Logical Exclusive OR register/memory to register";
						}
						break;
					case 0x34:
					case 0x35:
						// XOR (187)
						eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, 0));
						this.aParameters.Add(ReadImmediate(stream, 0, eOperandSize, bSignExtendImmediate));
						this.eClearedFlags = FlagsEnum.CF | FlagsEnum.OF;
						this.eModifiedFlags = FlagsEnum.PF | FlagsEnum.ZF | FlagsEnum.SF;
						this.eUndefinedFlags = FlagsEnum.AF;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.XOR;
							this.sDescription = "Logical Exclusive OR immediate to accumulator";
						}
						break;
					case 0x36:
						// Prefix: SS
						this.eDefaultDataSegment = SegmentRegisterEnum.SS;
						bPrefix = true;
						break;
					case 0x37:
						// AAA (0)
						this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.AF;
						this.eUndefinedFlags = FlagsEnum.PF | FlagsEnum.ZF | FlagsEnum.SF | FlagsEnum.OF;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.AAA;
							this.sDescription = "ASCII Adjust AL after addition";
						}
						break;
					case 0x38:
					case 0x39:
					case 0x3a:
					case 0x3b:
						// CMP (36)
						bReverseDirection = (this.iByte0 & 0x02) == 0;
						eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;

						// OpCode byte: 1
						this.iByte1 = stream.ReadByte();
						if (this.iByte1 < 0)
						{
							bInvalid = true;
							return;
						}
						this.aBytes.Add((byte)this.iByte1);

						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte1 & 0x38) >> 3)));
						this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
						this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.ZF | FlagsEnum.SF | FlagsEnum.OF;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.CMP;
							this.sDescription = "Compare register to register/memory; Compare register/memory to register";
						}
						break;
					case 0x3c:
					case 0x3d:
						// CMP (37)
						eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, 0));
						this.aParameters.Add(ReadImmediate(stream, 0, eOperandSize, bSignExtendImmediate));
						this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.ZF | FlagsEnum.SF | FlagsEnum.OF;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.CMP;
							this.sDescription = "Compare immediate to accumulator";
						}
						break;
					case 0x3e:
						// Prefix: DS
						this.eDefaultDataSegment = SegmentRegisterEnum.DS;
						bPrefix = true;
						break;
					case 0x3f:
						// AAS (3)
						this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.AF;
						this.eUndefinedFlags = FlagsEnum.PF | FlagsEnum.ZF | FlagsEnum.SF | FlagsEnum.OF;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.AAS;
							this.sDescription = "ASCII Adjust AL after subtraction";
						}
						break;
					case 0x40:
					case 0x41:
					case 0x42:
					case 0x43:
					case 0x44:
					case 0x45:
					case 0x46:
					case 0x47:
						// INC (54)
						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte0 & 0x07) >> 0)));
						this.eModifiedFlags = FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.ZF | FlagsEnum.SF | FlagsEnum.OF;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.INC;
							this.sDescription = "Increment by 1 register";
						}
						break;
					case 0x48:
					case 0x49:
					case 0x4a:
					case 0x4b:
					case 0x4c:
					case 0x4d:
					case 0x4e:
					case 0x4f:
						// DEC (43)
						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte0 & 0x07) >> 0)));
						this.eModifiedFlags = FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.ZF | FlagsEnum.SF | FlagsEnum.OF;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.DEC;
							this.sDescription = "Decrement by 1 register";
						}
						break;
					case 0x50:
					case 0x51:
					case 0x52:
					case 0x53:
					case 0x54:
					case 0x55:
					case 0x56:
					case 0x57:
						// PUSH (123)
						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte0 & 0x07) >> 0)));
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.PUSH;
							this.sDescription = "Push Operand onto the Stack register";
						}
						break;
					case 0x58:
					case 0x59:
					case 0x5a:
					case 0x5b:
					case 0x5c:
					case 0x5d:
					case 0x5e:
					case 0x5f:
						// POP (109)
						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte0 & 0x07) >> 0)));
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.POP;
							this.sDescription = "Pop a Word from the Stack register";
						}
						break;
					case 0x60:
						// PUSHA (126)
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i80186;
							this.eInstruction = InstructionEnum.PUSHA;
							this.sDescription = "Push all General registers";
						}
						break;
					case 0x61:
						// POPA (111)
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i80186;
							this.eInstruction = InstructionEnum.POPA;
							this.sDescription = "Pop all General registers";
						}
						break;
					case 0x62:
						// BOUND (14)

						// OpCode byte: 1
						this.iByte1 = stream.ReadByte();
						if (this.iByte1 < 0)
						{
							bInvalid = true;
							return;
						}
						this.aBytes.Add((byte)this.iByte1);

						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte1 & 0x38) >> 3)));
						this.aParameters.Add(MemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i80186;
							this.eInstruction = InstructionEnum.BOUND;
							this.sDescription = "Check Array Index Against Bounds, Interrupt 5 if Detect Value Out Range";
						}
						break;
					case 0x63:
						// ARPL (13)

						// OpCode byte: 1
						this.iByte1 = stream.ReadByte();
						if (this.iByte1 < 0)
						{
							bInvalid = true;
							return;
						}
						this.aBytes.Add((byte)this.iByte1);

						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte1 & 0x38) >> 3)));
						this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
						this.eModifiedFlags = FlagsEnum.ZF;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i80286;
							this.eInstruction = InstructionEnum.ARPL;
							this.sDescription = "Adjust RPL Field of Selector From register/memory";
						}
						break;
					case 0x64:
						// Prefix: FS
						this.eDefaultDataSegment = SegmentRegisterEnum.FS;
						bPrefix = true;
						break;
					case 0x65:
						// Prefix: GS
						this.eDefaultDataSegment = SegmentRegisterEnum.GS;
						bPrefix = true;
						break;
					case 0x66:
						// Prefix: OperandSize
						this.eOperandSize = (this.eDefaultSize == InstructionSizeEnum.Word) ? InstructionSizeEnum.DWord : InstructionSizeEnum.Word;
						bPrefix = true;
						break;
					case 0x67:
						// Prefix: AddressSize
						this.eAddressSize = (this.eDefaultSize == InstructionSizeEnum.Word) ? InstructionSizeEnum.DWord : InstructionSizeEnum.Word;
						bPrefix = true;
						break;
					case 0x68:
					case 0x6a:
						// PUSH (124)
						bSignExtendImmediate = (this.iByte0 & 0x02) != 0;
						this.aParameters.Add(ReadImmediate(stream, 0, eOperandSize, bSignExtendImmediate));
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i80186;
							this.eInstruction = InstructionEnum.PUSH;
							this.sDescription = "Push Operand onto the Stack immediate";
						}
						break;
					case 0x69:
					case 0x6b:
						// IMUL (50)
						bSignExtendImmediate = (this.iByte0 & 0x02) != 0;

						// OpCode byte: 1
						this.iByte1 = stream.ReadByte();
						if (this.iByte1 < 0)
						{
							bInvalid = true;
							return;
						}
						this.aBytes.Add((byte)this.iByte1);

						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte1 & 0x38) >> 3)));
						this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
						this.aParameters.Add(ReadImmediate(stream, 0, eOperandSize, bSignExtendImmediate));
						this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.OF;
						this.eUndefinedFlags = FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.ZF | FlagsEnum.SF;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i80186;
							this.eInstruction = InstructionEnum.IMUL;
							this.sDescription = "Signed Multiply register/memory with immediate to register";
						}
						break;
					case 0x6c:
					case 0x6d:
						// INS (56)
						eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i80186;
							this.eInstruction = InstructionEnum.INS;
							this.sDescription = "Input Byte/Word from DX Port";
						}
						break;
					case 0x6e:
					case 0x6f:
						// OUTS (106)
						eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i80186;
							this.eInstruction = InstructionEnum.OUTS;
							this.sDescription = "Output String to Port Byte/Word to DX Port";
						}
						break;
					case 0x70:
					case 0x71:
					case 0x72:
					case 0x73:
					case 0x74:
					case 0x75:
					case 0x76:
					case 0x77:
					case 0x78:
					case 0x79:
					case 0x7a:
					case 0x7b:
					case 0x7c:
					case 0x7d:
					case 0x7e:
					case 0x7f:
						// Jcc (62)
						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Condition, (uint)((this.iByte0 & 0x0f) >> 0)));
						this.aParameters.Add(ReadRelative(stream, 1, eOperandSize));
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.Jcc;
							this.sDescription = "Jump if Condition is Met, 8-bit displacement";
						}
						break;
					case 0x80:
					case 0x81:
					case 0x82:
					case 0x83:
						this.iByte1 = stream.ReadByte();
						if (this.iByte1 < 0)
						{
							bInvalid = true;
							return;
						}
						this.aBytes.Add((byte)this.iByte1);

						switch (this.iByte1 & 0x38)
						{
							case 0x00:
								// ADD (9)
								bSignExtendImmediate = (this.iByte0 & 0x02) != 0;
								eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								this.aParameters.Add(ReadImmediate(stream, 0, eOperandSize, bSignExtendImmediate));
								this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.ZF | FlagsEnum.SF | FlagsEnum.OF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.ADD;
									this.sDescription = "Add immediate to register/memory";
								}
								break;
							case 0x08:
								// OR (103)
								bSignExtendImmediate = (this.iByte0 & 0x02) != 0;
								eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								this.aParameters.Add(ReadImmediate(stream, 0, eOperandSize, bSignExtendImmediate));
								this.eClearedFlags = FlagsEnum.CF | FlagsEnum.OF;
								this.eModifiedFlags = FlagsEnum.PF | FlagsEnum.ZF | FlagsEnum.SF;
								this.eUndefinedFlags = FlagsEnum.AF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.OR;
									this.sDescription = "Logical Inclusive OR immediate to register/memory";
								}
								break;
							case 0x10:
								// ADC (6)
								bSignExtendImmediate = (this.iByte0 & 0x02) != 0;
								eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								this.aParameters.Add(ReadImmediate(stream, 0, eOperandSize, bSignExtendImmediate));
								this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.ZF | FlagsEnum.SF | FlagsEnum.OF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.ADC;
									this.sDescription = "Add with Carry immediate to register/memory";
								}
								break;
							case 0x18:
								// SBB (152)
								bSignExtendImmediate = (this.iByte0 & 0x02) != 0;
								eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								this.aParameters.Add(ReadImmediate(stream, 0, eOperandSize, bSignExtendImmediate));
								this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.ZF | FlagsEnum.SF | FlagsEnum.OF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.SBB;
									this.sDescription = "Integer Subtraction with Borrow immediate to register/memory";
								}
								break;
							case 0x20:
								// AND (12)
								bSignExtendImmediate = (this.iByte0 & 0x02) != 0;
								eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								this.aParameters.Add(ReadImmediate(stream, 0, eOperandSize, bSignExtendImmediate));
								this.eClearedFlags = FlagsEnum.CF | FlagsEnum.OF;
								this.eModifiedFlags = FlagsEnum.PF | FlagsEnum.ZF | FlagsEnum.SF;
								this.eUndefinedFlags = FlagsEnum.AF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.AND;
									this.sDescription = "Logical AND immediate to register/memory";
								}
								break;
							case 0x28:
								// SUB (176)
								bSignExtendImmediate = (this.iByte0 & 0x02) != 0;
								eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								this.aParameters.Add(ReadImmediate(stream, 0, eOperandSize, bSignExtendImmediate));
								this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.ZF | FlagsEnum.SF | FlagsEnum.OF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.SUB;
									this.sDescription = "Integer Subtraction immediate to register/memory";
								}
								break;
							case 0x30:
								// XOR (188)
								bSignExtendImmediate = (this.iByte0 & 0x02) != 0;
								eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								this.aParameters.Add(ReadImmediate(stream, 0, eOperandSize, bSignExtendImmediate));
								this.eClearedFlags = FlagsEnum.CF | FlagsEnum.OF;
								this.eModifiedFlags = FlagsEnum.PF | FlagsEnum.ZF | FlagsEnum.SF;
								this.eUndefinedFlags = FlagsEnum.AF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.XOR;
									this.sDescription = "Logical Exclusive OR immediate to register/memory";
								}
								break;
							case 0x38:
								// CMP (38)
								bSignExtendImmediate = (this.iByte0 & 0x02) != 0;
								eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								this.aParameters.Add(ReadImmediate(stream, 0, eOperandSize, bSignExtendImmediate));
								this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.ZF | FlagsEnum.SF | FlagsEnum.OF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.CMP;
									this.sDescription = "Compare immediate to register/memory";
								}
								break;
							default:
								bInvalid = true;
								break;
						}
						break;
					case 0x84:
					case 0x85:
						// TEST (177)
						eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;

						// OpCode byte: 1
						this.iByte1 = stream.ReadByte();
						if (this.iByte1 < 0)
						{
							bInvalid = true;
							return;
						}
						this.aBytes.Add((byte)this.iByte1);

						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte1 & 0x38) >> 3)));
						this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
						this.eClearedFlags = FlagsEnum.CF | FlagsEnum.OF;
						this.eModifiedFlags = FlagsEnum.PF | FlagsEnum.ZF | FlagsEnum.SF;
						this.eUndefinedFlags = FlagsEnum.AF;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.TEST;
							this.sDescription = "Logical Compare register/memory and register";
						}
						break;
					case 0x86:
					case 0x87:
						// XCHG (183)
						eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;

						// OpCode byte: 1
						this.iByte1 = stream.ReadByte();
						if (this.iByte1 < 0)
						{
							bInvalid = true;
							return;
						}
						this.aBytes.Add((byte)this.iByte1);

						this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte1 & 0x38) >> 3)));
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.XCHG;
							this.sDescription = "Exchange register/Memory with register";
						}
						break;
					case 0x88:
					case 0x89:
					case 0x8a:
					case 0x8b:
						// MOV (89)
						bReverseDirection = (this.iByte0 & 0x02) == 0;
						eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;

						// OpCode byte: 1
						this.iByte1 = stream.ReadByte();
						if (this.iByte1 < 0)
						{
							bInvalid = true;
							return;
						}
						this.aBytes.Add((byte)this.iByte1);

						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte1 & 0x38) >> 3)));
						this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.MOV;
							this.sDescription = "Move Data register to register/memory; Move Data register/memory to register";
						}
						break;
					case 0x8c:
					case 0x8e:
						// MOV (90)
						bReverseDirection = (this.iByte0 & 0x02) == 0;

						// OpCode byte: 1
						this.iByte1 = stream.ReadByte();
						if (this.iByte1 < 0)
						{
							bInvalid = true;
							return;
						}
						this.aBytes.Add((byte)this.iByte1);

						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.SegmentRegister, eOperandSize, (uint)((this.iByte1 & 0x38) >> 3)));
						this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.MOV;
							this.sDescription = "Move Data Segment register to register/memory; Move Data register/memory to segment register";
						}
						break;
					case 0x8d:
						// LEA (72)

						// OpCode byte: 1
						this.iByte1 = stream.ReadByte();
						if (this.iByte1 < 0)
						{
							bInvalid = true;
							return;
						}
						this.aBytes.Add((byte)this.iByte1);

						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte1 & 0x38) >> 3)));
						this.aParameters.Add(MemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.LEA;
							this.sDescription = "Load Effective Address to register";
						}
						break;
					case 0x8f:
						// POP (110)

						// OpCode byte: 1
						this.iByte1 = stream.ReadByte();
						if (this.iByte1 < 0)
						{
							bInvalid = true;
							return;
						}
						this.aBytes.Add((byte)this.iByte1);

						if ((this.iByte1 & 0x38) != 0x00)
						{
							bInvalid = true;
						}
						this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.POP;
							this.sDescription = "Pop a Word from the Stack register/memory";
						}
						break;
					case 0x90:
						// NOP (99)
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.NOP;
							this.sDescription = "No Operation";
						}
						break;
					case 0x91:
					case 0x92:
					case 0x93:
					case 0x94:
					case 0x95:
					case 0x96:
					case 0x97:
						// XCHG (184)
						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, 0));
						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte0 & 0x07) >> 0)));
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.XCHG;
							this.sDescription = "Exchange Accumulator with register";
						}
						break;
					case 0x98:
						// CBW (30)
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.CBW;
							this.sDescription = "Convert Byte to Word";
						}
						break;
					case 0x99:
						// CWD (40)
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.CWD;
							this.sDescription = "Convert Word to Dword";
						}
						break;
					case 0x9a:
						// CALLF (28)
						this.aParameters.Add(ReadSegmentOffset(stream, eOperandSize));
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.CALLF;
							this.sDescription = "Call Procedure direct intersegment";
						}
						break;
					case 0x9b:
						// WAIT (182)
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.WAIT;
							this.sDescription = "Wait until FPU ready";
						}
						break;
					case 0x9c:
						// PUSHF (127)
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.PUSHF;
							this.sDescription = "Push Flags register onto the Stack";
						}
						break;
					case 0x9d:
						// POPF (112)
						this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.ZF | FlagsEnum.SF | FlagsEnum.OF | FlagsEnum.IF | FlagsEnum.DF | FlagsEnum.All;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.POPF;
							this.sDescription = "Pop Stack into FLAGS register";
						}
						break;
					case 0x9e:
						// SAHF (146)
						this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.ZF | FlagsEnum.SF;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.SAHF;
							this.sDescription = "Store AH into Flags";
						}
						break;
					case 0x9f:
						// LAHF (69)
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.LAHF;
							this.sDescription = "Load Flags into AH register";
						}
						break;
					case 0xa0:
					case 0xa1:
					case 0xa2:
					case 0xa3:
						// MOV (91)
						bReverseDirection = (this.iByte0 & 0x02) == 0;
						eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
						this.aParameters.Add(MemoryImmediate(stream, this.eDefaultDataSegment, eAddressSize));
						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, 0));
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.MOV;
							this.sDescription = "Move Data Memory to Accumulator; Move Data Accumulator to Memory";
						}
						break;
					case 0xa4:
					case 0xa5:
						// MOVS (94)
						eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.MOVS;
							this.sDescription = "Move Data from String to String Byte/Word";
						}
						break;
					case 0xa6:
					case 0xa7:
						// CMPS (39)
						eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
						this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.ZF | FlagsEnum.SF | FlagsEnum.OF;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.CMPS;
							this.sDescription = "Compare String Operands Compare Byte Word";
						}
						break;
					case 0xa8:
					case 0xa9:
						// TEST (178)
						eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, 0));
						this.aParameters.Add(ReadImmediate(stream, 0, eOperandSize, bSignExtendImmediate));
						this.eClearedFlags = FlagsEnum.CF | FlagsEnum.OF;
						this.eModifiedFlags = FlagsEnum.PF | FlagsEnum.ZF | FlagsEnum.SF;
						this.eUndefinedFlags = FlagsEnum.AF;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.TEST;
							this.sDescription = "Logical Compare immediate and accumulator";
						}
						break;
					case 0xaa:
					case 0xab:
						// STOS (172)
						eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.STOS;
							this.sDescription = "Store String Data Byte/Word from AL/AX";
						}
						break;
					case 0xac:
					case 0xad:
						// LODS (82)
						eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.LODS;
							this.sDescription = "Load String Operand Byte/Word to AL/AX";
						}
						break;
					case 0xae:
					case 0xaf:
						// SCAS (153)
						eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
						this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.ZF | FlagsEnum.SF | FlagsEnum.OF;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.SCAS;
							this.sDescription = "Compare String Data Byte/Word";
						}
						break;
					case 0xb0:
					case 0xb1:
					case 0xb2:
					case 0xb3:
					case 0xb4:
					case 0xb5:
					case 0xb6:
					case 0xb7:
					case 0xb8:
					case 0xb9:
					case 0xba:
					case 0xbb:
					case 0xbc:
					case 0xbd:
					case 0xbe:
					case 0xbf:
						// MOV (92)
						eOperandSize = ((this.iByte0 & 0x08) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte0 & 0x07) >> 0)));
						this.aParameters.Add(ReadImmediate(stream, 0, eOperandSize, bSignExtendImmediate));
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.MOV;
							this.sDescription = "Move Data immediate to register";
						}
						break;
					case 0xc0:
					case 0xc1:
						this.iByte1 = stream.ReadByte();
						if (this.iByte1 < 0)
						{
							bInvalid = true;
							return;
						}
						this.aBytes.Add((byte)this.iByte1);

						switch (this.iByte1 & 0x38)
						{
							case 0x00:
								// ROL (140)
								eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								this.aParameters.Add(ReadImmediate(stream, 1, eOperandSize, bSignExtendImmediate));
								this.eModifiedFlags = FlagsEnum.CF;
								this.eUndefinedFlags = FlagsEnum.OF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i80186;
									this.eInstruction = InstructionEnum.ROL;
									this.sDescription = "Rotate register/memory by immediate count";
								}
								break;
							case 0x08:
								// ROR (143)
								eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								this.aParameters.Add(ReadImmediate(stream, 1, eOperandSize, bSignExtendImmediate));
								this.eModifiedFlags = FlagsEnum.CF;
								this.eUndefinedFlags = FlagsEnum.OF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i80186;
									this.eInstruction = InstructionEnum.ROR;
									this.sDescription = "Rotate register/memory by immediate count";
								}
								break;
							case 0x10:
								// RCL (128)
								eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								this.aParameters.Add(ReadImmediate(stream, 1, eOperandSize, bSignExtendImmediate));
								this.eModifiedFlags = FlagsEnum.CF;
								this.eUndefinedFlags = FlagsEnum.OF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i80186;
									this.eInstruction = InstructionEnum.RCL;
									this.sDescription = "Rotate register/memory by immediate count";
								}
								break;
							case 0x18:
								// RCR (131)
								eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								this.aParameters.Add(ReadImmediate(stream, 1, eOperandSize, bSignExtendImmediate));
								this.eModifiedFlags = FlagsEnum.CF;
								this.eUndefinedFlags = FlagsEnum.OF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i80186;
									this.eInstruction = InstructionEnum.RCR;
									this.sDescription = "Rotate register/memory by immediate count";
								}
								break;
							case 0x20:
								// SHL (156)
								eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								this.aParameters.Add(ReadImmediate(stream, 1, eOperandSize, bSignExtendImmediate));
								this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.PF | FlagsEnum.ZF | FlagsEnum.SF;
								this.eUndefinedFlags = FlagsEnum.AF | FlagsEnum.OF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i80186;
									this.eInstruction = InstructionEnum.SHL;
									this.sDescription = "Shift register/memory by immediate count";
								}
								break;
							case 0x28:
								// SHR (161)
								eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								this.aParameters.Add(ReadImmediate(stream, 1, eOperandSize, bSignExtendImmediate));
								this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.PF | FlagsEnum.ZF | FlagsEnum.SF;
								this.eUndefinedFlags = FlagsEnum.AF | FlagsEnum.OF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i80186;
									this.eInstruction = InstructionEnum.SHR;
									this.sDescription = "Shift register/memory by immediate count";
								}
								break;
							case 0x38:
								// SAR (147)
								eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								this.aParameters.Add(ReadImmediate(stream, 1, eOperandSize, bSignExtendImmediate));
								this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.PF | FlagsEnum.ZF | FlagsEnum.SF;
								this.eUndefinedFlags = FlagsEnum.AF | FlagsEnum.OF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i80186;
									this.eInstruction = InstructionEnum.SAR;
									this.sDescription = "Shift register/memory by immediate count";
								}
								break;
							default:
								bInvalid = true;
								break;
						}
						break;
					case 0xc2:
						// RET (136)
						this.aParameters.Add(ReadImmediate(stream, 2, eOperandSize, bSignExtendImmediate));
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.RET;
							this.sDescription = "Return from Procedure Adding immediate to SP";
						}
						break;
					case 0xc3:
						// RET (137)
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.RET;
							this.sDescription = "Return from Procedure";
						}
						break;
					case 0xc4:
						// LES (74)

						// OpCode byte: 1
						this.iByte1 = stream.ReadByte();
						if (this.iByte1 < 0)
						{
							bInvalid = true;
							return;
						}
						this.aBytes.Add((byte)this.iByte1);

						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte1 & 0x38) >> 3)));
						this.aParameters.Add(MemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.LES;
							this.sDescription = "Load Pointer to ES";
						}
						break;
					case 0xc5:
						// LDS (71)

						// OpCode byte: 1
						this.iByte1 = stream.ReadByte();
						if (this.iByte1 < 0)
						{
							bInvalid = true;
							return;
						}
						this.aBytes.Add((byte)this.iByte1);

						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte1 & 0x38) >> 3)));
						this.aParameters.Add(MemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.LDS;
							this.sDescription = "Load Pointer to DS";
						}
						break;
					case 0xc6:
					case 0xc7:
						// MOV (93)
						eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;

						// OpCode byte: 1
						this.iByte1 = stream.ReadByte();
						if (this.iByte1 < 0)
						{
							bInvalid = true;
							return;
						}
						this.aBytes.Add((byte)this.iByte1);

						if ((this.iByte1 & 0x38) != 0x00)
						{
							bInvalid = true;
						}
						this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
						this.aParameters.Add(ReadImmediate(stream, 0, eOperandSize, bSignExtendImmediate));
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.MOV;
							this.sDescription = "Move Data immediate to register/memory";
						}
						break;
					case 0xc8:
						// ENTER (46)
						this.aParameters.Add(ReadImmediate(stream, 2, eOperandSize, bSignExtendImmediate));
						this.aParameters.Add(ReadImmediate(stream, 1, eOperandSize, bSignExtendImmediate));
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i80186;
							this.eInstruction = InstructionEnum.ENTER;
							this.sDescription = "Enter Procedure";
						}
						break;
					case 0xc9:
						// LEAVE (73)
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i80186;
							this.eInstruction = InstructionEnum.LEAVE;
							this.sDescription = "Leave Procedure";
						}
						break;
					case 0xca:
						// RETF (138)
						this.aParameters.Add(ReadImmediate(stream, 2, eOperandSize, bSignExtendImmediate));
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.RETF;
							this.sDescription = "Return from Procedure Intersegment adding immediate to SP";
						}
						break;
					case 0xcb:
						// RETF (139)
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.RETF;
							this.sDescription = "Return from Procedure Intersegment";
						}
						break;
					case 0xcc:
						// INT (57)
						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Immediate, InstructionSizeEnum.Byte, 3));
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.INT;
							this.sDescription = "Call to Interrupt Procedure 3";
						}
						break;
					case 0xcd:
						// INT (58)
						this.aParameters.Add(ReadImmediate(stream, 1, eOperandSize, bSignExtendImmediate));
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.INT;
							this.sDescription = "Call to Interrupt Procedure n";
						}
						break;
					case 0xce:
						// INTO (59)
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.INTO;
							this.sDescription = "Call to Interrupt Procedure, Interrupt 4 if Overflow Flag Set";
						}
						break;
					case 0xcf:
						// IRET (60)
						this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.ZF | FlagsEnum.SF | FlagsEnum.OF | FlagsEnum.IF | FlagsEnum.DF | FlagsEnum.All;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.IRET;
							this.sDescription = "Interrupt Return";
						}
						break;
					case 0xd0:
					case 0xd1:
						this.iByte1 = stream.ReadByte();
						if (this.iByte1 < 0)
						{
							bInvalid = true;
							return;
						}
						this.aBytes.Add((byte)this.iByte1);

						switch (this.iByte1 & 0x38)
						{
							case 0x00:
								// ROL (141)
								eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Immediate, InstructionSizeEnum.Byte, 1));
								this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.OF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.ROL;
									this.sDescription = "Rotate register/memory by 1";
								}
								break;
							case 0x08:
								// ROR (144)
								eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Immediate, InstructionSizeEnum.Byte, 1));
								this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.OF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.ROR;
									this.sDescription = "Rotate register/memory by 1";
								}
								break;
							case 0x10:
								// RCL (129)
								eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Immediate, InstructionSizeEnum.Byte, 1));
								this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.OF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.RCL;
									this.sDescription = "Rotate register/memory by 1";
								}
								break;
							case 0x18:
								// RCR (132)
								eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Immediate, InstructionSizeEnum.Byte, 1));
								this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.OF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.RCR;
									this.sDescription = "Rotate register/memory by 1";
								}
								break;
							case 0x20:
								// SHL (157)
								eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Immediate, InstructionSizeEnum.Byte, 1));
								this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.PF | FlagsEnum.ZF | FlagsEnum.SF | FlagsEnum.OF;
								this.eUndefinedFlags = FlagsEnum.AF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.SHL;
									this.sDescription = "Shift register/memory by 1";
								}
								break;
							case 0x28:
								// SHR (162)
								eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Immediate, InstructionSizeEnum.Byte, 1));
								this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.PF | FlagsEnum.ZF | FlagsEnum.SF | FlagsEnum.OF;
								this.eUndefinedFlags = FlagsEnum.AF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.SHR;
									this.sDescription = "Shift register/memory by 1";
								}
								break;
							case 0x38:
								// SAR (148)
								eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Immediate, InstructionSizeEnum.Byte, 1));
								this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.PF | FlagsEnum.ZF | FlagsEnum.SF | FlagsEnum.OF;
								this.eUndefinedFlags = FlagsEnum.AF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.SAR;
									this.sDescription = "Shift register/memory by 1";
								}
								break;
							default:
								bInvalid = true;
								break;
						}
						break;
					case 0xd2:
					case 0xd3:
						this.iByte1 = stream.ReadByte();
						if (this.iByte1 < 0)
						{
							bInvalid = true;
							return;
						}
						this.aBytes.Add((byte)this.iByte1);

						switch (this.iByte1 & 0x38)
						{
							case 0x00:
								// ROL (142)
								eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, InstructionSizeEnum.Byte, 1));
								this.eModifiedFlags = FlagsEnum.CF;
								this.eUndefinedFlags = FlagsEnum.OF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.ROL;
									this.sDescription = "Rotate register/memory by CL";
								}
								break;
							case 0x08:
								// ROR (145)
								eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, InstructionSizeEnum.Byte, 1));
								this.eModifiedFlags = FlagsEnum.CF;
								this.eUndefinedFlags = FlagsEnum.OF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.ROR;
									this.sDescription = "Rotate register/memory by CL";
								}
								break;
							case 0x10:
								// RCL (130)
								eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, InstructionSizeEnum.Byte, 1));
								this.eModifiedFlags = FlagsEnum.CF;
								this.eUndefinedFlags = FlagsEnum.OF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.RCL;
									this.sDescription = "Rotate register/memory by CL";
								}
								break;
							case 0x18:
								// RCR (133)
								eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, InstructionSizeEnum.Byte, 1));
								this.eModifiedFlags = FlagsEnum.CF;
								this.eUndefinedFlags = FlagsEnum.OF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.RCR;
									this.sDescription = "Rotate register/memory by CL";
								}
								break;
							case 0x20:
								// SHL (158)
								eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, InstructionSizeEnum.Byte, 1));
								this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.PF | FlagsEnum.ZF | FlagsEnum.SF;
								this.eUndefinedFlags = FlagsEnum.AF | FlagsEnum.OF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.SHL;
									this.sDescription = "Shift register/memory by CL";
								}
								break;
							case 0x28:
								// SHR (163)
								eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, InstructionSizeEnum.Byte, 1));
								this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.PF | FlagsEnum.ZF | FlagsEnum.SF;
								this.eUndefinedFlags = FlagsEnum.AF | FlagsEnum.OF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.SHR;
									this.sDescription = "Shift register/memory by CL";
								}
								break;
							case 0x38:
								// SAR (149)
								eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, InstructionSizeEnum.Byte, 1));
								this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.PF | FlagsEnum.ZF | FlagsEnum.SF;
								this.eUndefinedFlags = FlagsEnum.AF | FlagsEnum.OF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.SAR;
									this.sDescription = "Shift register/memory by CL";
								}
								break;
							default:
								bInvalid = true;
								break;
						}
						break;
					case 0xd4:
						// AAM (2)

						// OpCode byte: 1
						this.iByte1 = stream.ReadByte();
						if (this.iByte1 < 0)
						{
							bInvalid = true;
							return;
						}
						this.aBytes.Add((byte)this.iByte1);

						if ((this.iByte1 & 0xff) != 0x0a)
						{
							bInvalid = true;
						}
						this.eModifiedFlags = FlagsEnum.PF | FlagsEnum.ZF | FlagsEnum.SF;
						this.eUndefinedFlags = FlagsEnum.CF | FlagsEnum.AF | FlagsEnum.OF;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.AAM;
							this.sDescription = "ASCII Adjust AX after multiply";
						}
						break;
					case 0xd5:
						// AAD (1)

						// OpCode byte: 1
						this.iByte1 = stream.ReadByte();
						if (this.iByte1 < 0)
						{
							bInvalid = true;
							return;
						}
						this.aBytes.Add((byte)this.iByte1);

						if ((this.iByte1 & 0xff) != 0x0a)
						{
							bInvalid = true;
						}
						this.eModifiedFlags = FlagsEnum.PF | FlagsEnum.ZF | FlagsEnum.SF;
						this.eUndefinedFlags = FlagsEnum.CF | FlagsEnum.AF | FlagsEnum.OF;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.AAD;
							this.sDescription = "ASCII Adjust AX before division";
						}
						break;
					case 0xd7:
						// XLAT (185)
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.XLAT;
							this.sDescription = "Table Look-up Translation";
						}
						break;
					case 0xd8:
						// Multiple instructions, alternate method
						this.iByte1 = stream.ReadByte();
						if (this.iByte1 < 0)
						{
							bInvalid = true;
							return;
						}
						this.aBytes.Add((byte)this.iByte1);

						bExitCase = false;
						switch (this.iByte1 & 0xf8)
						{
							case 0xc0:
								// FADD (193)
								this.bFPUDestination0 = (this.iByte0 & 0x04) == 0;
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.FPUStackAddress, (uint)((this.iByte1 & 0x07) >> 0)));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FADD;
									this.sDescription = "Add Real with ST(0) (ST(d) = ST(0) + ST(i))";
								}
								bExitCase = true;
								break;
							case 0xc8:
								// FMUL (258)
								this.bFPUDestination0 = (this.iByte0 & 0x04) == 0;
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.FPUStackAddress, (uint)((this.iByte1 & 0x07) >> 0)));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FMUL;
									this.sDescription = "Multiply real with ST(0) (ST(d) = ST(0) * ST(i))";
								}
								bExitCase = true;
								break;
							case 0xd0:
								// FCOM (199)
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.FPUStackAddress, (uint)((this.iByte1 & 0x07) >> 0)));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FCOM;
									this.sDescription = "Compare ST(0) with Real (ST(i))";
								}
								bExitCase = true;
								break;
							case 0xd8:
								// FCOMP (202)
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.FPUStackAddress, (uint)((this.iByte1 & 0x07) >> 0)));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FCOMP;
									this.sDescription = "Compare ST(0) with Real and Pop (ST(i))";
								}
								bExitCase = true;
								break;
							case 0xe0:
								// FSUB (285)
								this.bFPUDestination0 = (this.iByte0 & 0x04) == 0;
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.FPUStackAddress, (uint)((this.iByte1 & 0x07) >> 0)));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FSUB;
									this.sDescription = "Subtract real from ST(0) (ST(d) = ST(0) - ST(i))";
								}
								bExitCase = true;
								break;
							case 0xe8:
								// FSUBR (289)
								this.bFPUDestination0 = (this.iByte0 & 0x04) == 0;
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.FPUStackAddress, (uint)((this.iByte1 & 0x07) >> 0)));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FSUBR;
									this.sDescription = "Subtract real reversed (Subtract ST(0) from real) (ST(d) = ST(i) - ST(0))";
								}
								bExitCase = true;
								break;
							case 0xf0:
								// FDIV (210)
								this.bFPUDestination0 = (this.iByte0 & 0x04) == 0;
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.FPUStackAddress, (uint)((this.iByte1 & 0x07) >> 0)));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FDIV;
									this.sDescription = "Divide ST(0) by Real (ST(d) = ST(0)/ST(i))";
								}
								bExitCase = true;
								break;
							case 0xf8:
								// FDIVR (214)
								this.bFPUDestination0 = (this.iByte0 & 0x04) == 0;
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.FPUStackAddress, (uint)((this.iByte1 & 0x07) >> 0)));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FDIVR;
									this.sDescription = "Divide real reversed (Real/ST(0)) (ST(d) = ST(i)/ST(0))";
								}
								bExitCase = true;
								break;
							default:
								break;
						}
						if (bExitCase)
							break;
						bExitCase = false;
						switch (this.iByte1 & 0x38)
						{
							case 0x00:
								// FADD (191)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FADD;
									this.sDescription = "Add Real with ST(0) (ST(0) = ST(0) + 32-bit memory)";
								}
								bExitCase = true;
								break;
							case 0x08:
								// FMUL (256)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FMUL;
									this.sDescription = "Multiply real with ST(0) (ST(0) = ST(0) * 32-bit memory)";
								}
								bExitCase = true;
								break;
							case 0x10:
								// FCOM (200)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FCOM;
									this.sDescription = "Compare ST(0) with Real (32-bit memory)";
								}
								bExitCase = true;
								break;
							case 0x18:
								// FCOMP (203)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FCOMP;
									this.sDescription = "Compare ST(0) with Real and Pop (32-bit memory)";
								}
								bExitCase = true;
								break;
							case 0x20:
								// FSUB (283)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FSUB;
									this.sDescription = "Subtract real from ST(0) (ST(0) = ST(0) - 32-bit memory)";
								}
								bExitCase = true;
								break;
							case 0x28:
								// FSUBR (287)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FSUBR;
									this.sDescription = "Subtract real reversed (Subtract ST(0) from real) (ST(0) = 32-bit memory - ST(0))";
								}
								bExitCase = true;
								break;
							case 0x30:
								// FDIV (208)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FDIV;
									this.sDescription = "Divide ST(0) by Real (ST(0) = ST(0)/32-bit memory)";
								}
								bExitCase = true;
								break;
							case 0x38:
								// FDIVR (212)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FDIVR;
									this.sDescription = "Divide real reversed (Real/ST(0)) (ST(0) = 32-bit memory/ST(0))";
								}
								bExitCase = true;
								break;
							default:
								break;
						}
						if (bExitCase)
							break;
						break;
					case 0xd9:
						// Multiple instructions, alternate method
						this.iByte1 = stream.ReadByte();
						if (this.iByte1 < 0)
						{
							bInvalid = true;
							return;
						}
						this.aBytes.Add((byte)this.iByte1);

						bExitCase = false;
						switch (this.iByte1)
						{
							case 0xd0:
								// FNOP (260)
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FNOP;
									this.sDescription = "No operations";
								}
								bExitCase = true;
								break;
							case 0xe0:
								// FCHS (197)
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FCHS;
									this.sDescription = "Change sign of ST(0)";
								}
								bExitCase = true;
								break;
							case 0xe1:
								// FABS (190)
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FABS;
									this.sDescription = "Absolute value of ST(0)";
								}
								bExitCase = true;
								break;
							case 0xe4:
								// FTST (291)
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FTST;
									this.sDescription = "Compare ST(0) with 0.0";
								}
								bExitCase = true;
								break;
							case 0xe5:
								// FXAM (295)
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FXAM;
									this.sDescription = "Examine ST(0)";
								}
								bExitCase = true;
								break;
							case 0xe8:
								// FLD1 (247)
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FLD1;
									this.sDescription = "Load +1.0 into ST(0)";
								}
								bExitCase = true;
								break;
							case 0xe9:
								// FLDL2T (251)
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FLDL2T;
									this.sDescription = "Load log2(10) into ST(0)";
								}
								bExitCase = true;
								break;
							case 0xea:
								// FLDL2E (250)
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FLDL2E;
									this.sDescription = "Load log2(e) into ST(0)";
								}
								bExitCase = true;
								break;
							case 0xeb:
								// FLDPI (254)
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FLDPI;
									this.sDescription = "Load PI into ST(0)";
								}
								bExitCase = true;
								break;
							case 0xec:
								// FLDLG2 (252)
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FLDLG2;
									this.sDescription = "Load log10(2) into ST(0)";
								}
								bExitCase = true;
								break;
							case 0xed:
								// FLDLN2 (253)
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FLDLN2;
									this.sDescription = "Load loge(2) into ST(0)";
								}
								bExitCase = true;
								break;
							case 0xee:
								// FLDZ (255)
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FLDZ;
									this.sDescription = "Load +0.0 into ST(0)";
								}
								bExitCase = true;
								break;
							case 0xf0:
								// F2XM1 (189)
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.F2XM1;
									this.sDescription = "2^ST(0) - 1";
								}
								bExitCase = true;
								break;
							case 0xf1:
								// FYL2X (298)
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FYL2X;
									this.sDescription = "ST(1) * log2(ST(0))";
								}
								bExitCase = true;
								break;
							case 0xf2:
								// FPTAN (264)
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FPTAN;
									this.sDescription = "Partial tangent of ST(0)";
								}
								bExitCase = true;
								break;
							case 0xf3:
								// FPATAN (261)
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FPATAN;
									this.sDescription = "Partial arctangent";
								}
								bExitCase = true;
								break;
							case 0xf4:
								// FXTRACT (297)
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FXTRACT;
									this.sDescription = "Extract components of ST(0)";
								}
								bExitCase = true;
								break;
							case 0xf5:
								// FPREM1 (263)
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i80387;
									this.eInstruction = InstructionEnum.FPREM1;
									this.sDescription = "Partial Reminder (IEEE)";
								}
								bExitCase = true;
								break;
							case 0xf6:
								// FDECSTP (207)
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FDECSTP;
									this.sDescription = "Decrement Stack Pointer";
								}
								bExitCase = true;
								break;
							case 0xf7:
								// FINCSTP (232)
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FINCSTP;
									this.sDescription = "Increment Stack Pointer";
								}
								bExitCase = true;
								break;
							case 0xf8:
								// FPREM (262)
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FPREM;
									this.sDescription = "Partial Reminder";
								}
								bExitCase = true;
								break;
							case 0xf9:
								// FYL2XP1 (299)
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FYL2XP1;
									this.sDescription = "ST(1) x log2(ST(0) + 1.0)";
								}
								bExitCase = true;
								break;
							case 0xfa:
								// FSQRT (271)
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FSQRT;
									this.sDescription = "Square Root";
								}
								bExitCase = true;
								break;
							case 0xfb:
								// FSINCOS (270)
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i80387;
									this.eInstruction = InstructionEnum.FSINCOS;
									this.sDescription = "Sine and cosine of ST(0)";
								}
								bExitCase = true;
								break;
							case 0xfc:
								// FRNDINT (265)
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FRNDINT;
									this.sDescription = "Round ST(0) to integer";
								}
								bExitCase = true;
								break;
							case 0xfd:
								// FSCALE (268)
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FSCALE;
									this.sDescription = "Scale ST(0) by ST(1)";
								}
								bExitCase = true;
								break;
							case 0xfe:
								// FSIN (269)
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i80387;
									this.eInstruction = InstructionEnum.FSIN;
									this.sDescription = "Sine of ST(0)";
								}
								bExitCase = true;
								break;
							case 0xff:
								// FCOS (206)
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i80387;
									this.eInstruction = InstructionEnum.FCOS;
									this.sDescription = "Cosine of ST(0)";
								}
								bExitCase = true;
								break;
							default:
								break;
						}
						if (bExitCase)
							break;
						bExitCase = false;
						switch (this.iByte1 & 0xf8)
						{
							case 0xc0:
								// FLD (243)
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.FPUStackAddress, (uint)((this.iByte1 & 0x07) >> 0)));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FLD;
									this.sDescription = "Real Load to ST(0) (ST(i))";
								}
								bExitCase = true;
								break;
							case 0xc8:
								// FXCH (296)
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.FPUStackAddress, (uint)((this.iByte1 & 0x07) >> 0)));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FXCH;
									this.sDescription = "Exchange ST(0) and ST(i)";
								}
								bExitCase = true;
								break;
							default:
								break;
						}
						if (bExitCase)
							break;
						bExitCase = false;
						switch (this.iByte1 & 0x38)
						{
							case 0x00:
								// FLD (244)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FLD;
									this.sDescription = "Real Load to ST(0) (32-bit memory)";
								}
								bExitCase = true;
								break;
							case 0x10:
								// FST (272)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FST;
									this.sDescription = "Store Real from ST(0) (32-bit memory)";
								}
								bExitCase = true;
								break;
							case 0x18:
								// FSTP (277)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FSTP;
									this.sDescription = "Store Real from ST(0) and Pop (32-bit memory)";
								}
								bExitCase = true;
								break;
							case 0x20:
								// FLDENV (249)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FLDENV;
									this.sDescription = "Load environment";
								}
								bExitCase = true;
								break;
							case 0x28:
								// FLDCW (248)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FLDCW;
									this.sDescription = "Load control word";
								}
								bExitCase = true;
								break;
							case 0x30:
								// FSTENV (276)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FSTENV;
									this.sDescription = "Store environment";
								}
								bExitCase = true;
								break;
							case 0x38:
								// FSTCW (275)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FSTCW;
									this.sDescription = "Store control word";
								}
								bExitCase = true;
								break;
							default:
								break;
						}
						if (bExitCase)
							break;
						break;
					case 0xda:
						// Multiple instructions, alternate method
						this.iByte1 = stream.ReadByte();
						if (this.iByte1 < 0)
						{
							bInvalid = true;
							return;
						}
						this.aBytes.Add((byte)this.iByte1);

						if ((this.iByte1 & 0xff) == 0xe9)
						{
							// FUCOMPP (294)
							if (!bInvalid)
							{
								this.eCPUType = CPUEnum.i80387;
								this.eInstruction = InstructionEnum.FUCOMPP;
								this.sDescription = "Unordered compare ST(0) with ST(i) and Pop Twice";
							}
							break;
						}
						bExitCase = false;
						switch (this.iByte1 & 0x38)
						{
							case 0x00:
								// FIADD (217)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FIADD;
									this.sDescription = "Add Integer to ST(0) (ST(0) = ST(0) + 32-bit memory)";
								}
								bExitCase = true;
								break;
							case 0x08:
								// FIMUL (230)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FIMUL;
									this.sDescription = "Multiply Integer with ST(0) (ST(0) = ST(0) * 32-bit memory)";
								}
								bExitCase = true;
								break;
							case 0x10:
								// FICOM (219)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FICOM;
									this.sDescription = "Compare ST(0) with Integer (32-bit memory)";
								}
								bExitCase = true;
								break;
							case 0x18:
								// FICOMP (221)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FICOMP;
									this.sDescription = "Compare ST(0) with Integer and Pop (32-bit memory)";
								}
								bExitCase = true;
								break;
							case 0x20:
								// FISUB (239)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FISUB;
									this.sDescription = "Subtract Integer from ST(0) (ST(0) = ST(0) - 32-bit memory)";
								}
								bExitCase = true;
								break;
							case 0x28:
								// FISUBR (241)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FISUBR;
									this.sDescription = "Integer Subtract Reversed (ST(0) = 32-bit memory - ST(0))";
								}
								bExitCase = true;
								break;
							case 0x30:
								// FIDIV (223)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FIDIV;
									this.sDescription = "Integer Divide (ST(0) = ST(0)/32-bit memory)";
								}
								bExitCase = true;
								break;
							case 0x38:
								// FIDIVR (225)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FIDIVR;
									this.sDescription = "Integer Divide Reversed (ST(0) = 32-bit memory/ST(0))";
								}
								bExitCase = true;
								break;
							default:
								break;
						}
						if (bExitCase)
							break;
						break;
					case 0xdb:
						// Multiple instructions, alternate method
						this.iByte1 = stream.ReadByte();
						if (this.iByte1 < 0)
						{
							bInvalid = true;
							return;
						}
						this.aBytes.Add((byte)this.iByte1);

						bExitCase = false;
						switch (this.iByte1)
						{
							case 0xe2:
								// FCLEX (198)
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FCLEX;
									this.sDescription = "Clear exceptions";
								}
								bExitCase = true;
								break;
							case 0xe3:
								// FINIT (233)
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FINIT;
									this.sDescription = "Initialize FPU";
								}
								bExitCase = true;
								break;
							default:
								break;
						}
						if (bExitCase)
							break;
						bExitCase = false;
						switch (this.iByte1 & 0x38)
						{
							case 0x00:
								// FILD (227)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FILD;
									this.sDescription = "Integer Load to ST(0) (32-bit memory)";
								}
								bExitCase = true;
								break;
							case 0x10:
								// FIST (234)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FIST;
									this.sDescription = "Store Integer from ST(0) (32-bit memory)";
								}
								bExitCase = true;
								break;
							case 0x18:
								// FISTP (236)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FISTP;
									this.sDescription = "Store Integer from ST(0) and Pop (32-bit memory)";
								}
								bExitCase = true;
								break;
							case 0x28:
								// FLD (245)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FLD;
									this.sDescription = "Real Load to ST(0) (80-bit memory)";
								}
								bExitCase = true;
								break;
							case 0x38:
								// FSTP (278)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FSTP;
									this.sDescription = "Store Real from ST(0) and Pop (80-bit memory)";
								}
								bExitCase = true;
								break;
							default:
								break;
						}
						if (bExitCase)
							break;
						break;
					case 0xdc:
						// Multiple instructions, alternate method
						this.iByte1 = stream.ReadByte();
						if (this.iByte1 < 0)
						{
							bInvalid = true;
							return;
						}
						this.aBytes.Add((byte)this.iByte1);

						bExitCase = false;
						switch (this.iByte1 & 0xf8)
						{
							case 0xc0:
								// FADD (193)
								this.bFPUDestination0 = (this.iByte0 & 0x04) == 0;
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.FPUStackAddress, (uint)((this.iByte1 & 0x07) >> 0)));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FADD;
									this.sDescription = "Add Real with ST(0) (ST(d) = ST(0) + ST(i))";
								}
								bExitCase = true;
								break;
							case 0xc8:
								// FMUL (258)
								this.bFPUDestination0 = (this.iByte0 & 0x04) == 0;
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.FPUStackAddress, (uint)((this.iByte1 & 0x07) >> 0)));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FMUL;
									this.sDescription = "Multiply real with ST(0) (ST(d) = ST(0) * ST(i))";
								}
								bExitCase = true;
								break;
							case 0xe0:
								// FSUB (285)
								this.bFPUDestination0 = (this.iByte0 & 0x04) == 0;
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.FPUStackAddress, (uint)((this.iByte1 & 0x07) >> 0)));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FSUB;
									this.sDescription = "Subtract real from ST(0) (ST(d) = ST(0) - ST(i))";
								}
								bExitCase = true;
								break;
							case 0xe8:
								// FSUBR (289)
								this.bFPUDestination0 = (this.iByte0 & 0x04) == 0;
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.FPUStackAddress, (uint)((this.iByte1 & 0x07) >> 0)));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FSUBR;
									this.sDescription = "Subtract real reversed (Subtract ST(0) from real) (ST(d) = ST(i) - ST(0))";
								}
								bExitCase = true;
								break;
							case 0xf0:
								// FDIV (210)
								this.bFPUDestination0 = (this.iByte0 & 0x04) == 0;
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.FPUStackAddress, (uint)((this.iByte1 & 0x07) >> 0)));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FDIV;
									this.sDescription = "Divide ST(0) by Real (ST(d) = ST(0)/ST(i))";
								}
								bExitCase = true;
								break;
							case 0xf8:
								// FDIVR (214)
								this.bFPUDestination0 = (this.iByte0 & 0x04) == 0;
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.FPUStackAddress, (uint)((this.iByte1 & 0x07) >> 0)));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FDIVR;
									this.sDescription = "Divide real reversed (Real/ST(0)) (ST(d) = ST(i)/ST(0))";
								}
								bExitCase = true;
								break;
							default:
								break;
						}
						if (bExitCase)
							break;
						bExitCase = false;
						switch (this.iByte1 & 0x38)
						{
							case 0x00:
								// FADD (192)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FADD;
									this.sDescription = "Add Real with ST(0) (ST(0) = ST(0) + 64-bit memory)";
								}
								bExitCase = true;
								break;
							case 0x08:
								// FMUL (257)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FMUL;
									this.sDescription = "Multiply real with ST(0) (ST(0) = ST(0) * 64-bit memory)";
								}
								bExitCase = true;
								break;
							case 0x10:
								// FCOM (201)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FCOM;
									this.sDescription = "Compare ST(0) with Real (64-bit memory)";
								}
								bExitCase = true;
								break;
							case 0x18:
								// FCOMP (204)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FCOMP;
									this.sDescription = "Compare ST(0) with Real and Pop (64-bit memory)";
								}
								bExitCase = true;
								break;
							case 0x20:
								// FSUB (284)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FSUB;
									this.sDescription = "Subtract real from ST(0) (ST(0) = ST(0) - 64-bit memory)";
								}
								bExitCase = true;
								break;
							case 0x28:
								// FSUBR (288)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FSUBR;
									this.sDescription = "Subtract real reversed (Subtract ST(0) from real) (ST(0) = 64-bit memory - ST(0))";
								}
								bExitCase = true;
								break;
							case 0x30:
								// FDIV (209)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FDIV;
									this.sDescription = "Divide ST(0) by Real (ST(0) = ST(0)/64-bit memory)";
								}
								bExitCase = true;
								break;
							case 0x38:
								// FDIVR (213)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FDIVR;
									this.sDescription = "Divide real reversed (Real/ST(0)) (ST(0) = 64-bit memory/ST(0))";
								}
								bExitCase = true;
								break;
							default:
								break;
						}
						if (bExitCase)
							break;
						break;
					case 0xdd:
						// Multiple instructions, alternate method
						this.iByte1 = stream.ReadByte();
						if (this.iByte1 < 0)
						{
							bInvalid = true;
							return;
						}
						this.aBytes.Add((byte)this.iByte1);

						bExitCase = false;
						switch (this.iByte1 & 0xf8)
						{
							case 0xc0:
								// FFREE (216)
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.FPUStackAddress, (uint)((this.iByte1 & 0x07) >> 0)));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FFREE;
									this.sDescription = "Free ST(i)";
								}
								bExitCase = true;
								break;
							case 0xc8:
								// FSTP (279)
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.FPUStackAddress, (uint)((this.iByte1 & 0x07) >> 0)));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FSTP;
									this.sDescription = "Store Real from ST(0) and Pop (ST(i))";
								}
								bExitCase = true;
								break;
							case 0xd0:
								// FST (273)
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.FPUStackAddress, (uint)((this.iByte1 & 0x07) >> 0)));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FST;
									this.sDescription = "Store Real from ST(0) (ST(i))";
								}
								bExitCase = true;
								break;
							case 0xe0:
								// FUCOM (292)
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.FPUStackAddress, (uint)((this.iByte1 & 0x07) >> 0)));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i80387;
									this.eInstruction = InstructionEnum.FUCOM;
									this.sDescription = "Unordered compare ST(0) with ST(i)";
								}
								bExitCase = true;
								break;
							case 0xe8:
								// FUCOMP (293)
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.FPUStackAddress, (uint)((this.iByte1 & 0x07) >> 0)));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i80387;
									this.eInstruction = InstructionEnum.FUCOMP;
									this.sDescription = "Unordered compare ST(0) with ST(i) and Pop";
								}
								bExitCase = true;
								break;
							default:
								break;
						}
						if (bExitCase)
							break;
						bExitCase = false;
						switch (this.iByte1 & 0x38)
						{
							case 0x00:
								// FLD (246)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FLD;
									this.sDescription = "Real Load to ST(0) (64-bit memory)";
								}
								bExitCase = true;
								break;
							case 0x10:
								// FST (274)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FST;
									this.sDescription = "Store Real from ST(0) (64-bit memory)";
								}
								bExitCase = true;
								break;
							case 0x18:
								// FSTP (280)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FSTP;
									this.sDescription = "Store Real from ST(0) and Pop (64-bit memory)";
								}
								bExitCase = true;
								break;
							case 0x20:
								// FRSTOR (266)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FRSTOR;
									this.sDescription = "Restore state";
								}
								bExitCase = true;
								break;
							case 0x30:
								// FSAVE (267)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FSAVE;
									this.sDescription = "Save state";
								}
								bExitCase = true;
								break;
							case 0x38:
								// FSTSW (281)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FSTSW;
									this.sDescription = "Store status word into memory";
								}
								bExitCase = true;
								break;
							default:
								break;
						}
						if (bExitCase)
							break;
						break;
					case 0xde:
						// Multiple instructions, alternate method
						this.iByte1 = stream.ReadByte();
						if (this.iByte1 < 0)
						{
							bInvalid = true;
							return;
						}
						this.aBytes.Add((byte)this.iByte1);

						if ((this.iByte1 & 0xff) == 0xd9)
						{
							// FCOMPP (205)
							if (!bInvalid)
							{
								this.eCPUType = CPUEnum.i8087;
								this.eInstruction = InstructionEnum.FCOMPP;
								this.sDescription = "Compare ST(0) with ST(1) and Pop Twice";
							}
							break;
						}
						bExitCase = false;
						switch (this.iByte1 & 0xf8)
						{
							case 0xc0:
								// FADDP (194)
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.FPUStackAddress, (uint)((this.iByte1 & 0x07) >> 0)));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FADDP;
									this.sDescription = "Add real with ST(0) and Pop (ST(i) = ST(0) + ST(i))";
								}
								bExitCase = true;
								break;
							case 0xc8:
								// FMULP (259)
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.FPUStackAddress, (uint)((this.iByte1 & 0x07) >> 0)));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FMULP;
									this.sDescription = "Multiply ST(0) with ST(i) and Pop (ST(i) = ST(0) * ST(i))";
								}
								bExitCase = true;
								break;
							case 0xe0:
								// FSUBRP (290)
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.FPUStackAddress, (uint)((this.iByte1 & 0x07) >> 0)));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FSUBRP;
									this.sDescription = "Subtract real reversed and Pop (ST(i) = ST(i) - ST(0))";
								}
								bExitCase = true;
								break;
							case 0xe8:
								// FSUBP (286)
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.FPUStackAddress, (uint)((this.iByte1 & 0x07) >> 0)));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FSUBP;
									this.sDescription = "Subtract real from ST(0) and Pop (ST(i) = ST(0) - ST(i))";
								}
								bExitCase = true;
								break;
							case 0xf0:
								// FDIVRP (215)
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.FPUStackAddress, (uint)((this.iByte1 & 0x07) >> 0)));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FDIVRP;
									this.sDescription = "Divide real reversed and Pop(ST(i) = ST(i)/ST(0))";
								}
								bExitCase = true;
								break;
							case 0xf8:
								// FDIVP (211)
								this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.FPUStackAddress, (uint)((this.iByte1 & 0x07) >> 0)));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FDIVP;
									this.sDescription = "Divide ST(0) by ST(i) and Pop (ST(i) = ST(0)/ST(i))";
								}
								bExitCase = true;
								break;
							default:
								break;
						}
						if (bExitCase)
							break;
						bExitCase = false;
						switch (this.iByte1 & 0x38)
						{
							case 0x00:
								// FIADD (218)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FIADD;
									this.sDescription = "Add Integer to ST(0) (ST(0) = ST(0) + 16-bit memory)";
								}
								bExitCase = true;
								break;
							case 0x08:
								// FIMUL (231)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FIMUL;
									this.sDescription = "Multiply Integer with ST(0) (ST(0) = ST(0) * 16-bit memory)";
								}
								bExitCase = true;
								break;
							case 0x10:
								// FICOM (220)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FICOM;
									this.sDescription = "Compare ST(0) with Integer (16-bit memory)";
								}
								bExitCase = true;
								break;
							case 0x18:
								// FICOMP (222)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FICOMP;
									this.sDescription = "Compare ST(0) with Integer and Pop (16-bit memory)";
								}
								bExitCase = true;
								break;
							case 0x20:
								// FISUB (240)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FISUB;
									this.sDescription = "Subtract Integer from ST(0) (ST(0) = ST(0) - 16-bit memory)";
								}
								bExitCase = true;
								break;
							case 0x28:
								// FISUBR (242)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FISUBR;
									this.sDescription = "Integer Subtract Reversed (ST(0) = 16-bit memory - ST(0))";
								}
								bExitCase = true;
								break;
							case 0x30:
								// FIDIV (224)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FIDIV;
									this.sDescription = "Integer Divide (ST(0) = ST(0)/16-bit memory)";
								}
								bExitCase = true;
								break;
							case 0x38:
								// FIDIVR (226)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FIDIVR;
									this.sDescription = "Integer Divide Reversed (ST(0) = 16-bit memory/ST(0))";
								}
								bExitCase = true;
								break;
							default:
								break;
						}
						if (bExitCase)
							break;
						break;
					case 0xdf:
						// Multiple instructions, alternate method
						this.iByte1 = stream.ReadByte();
						if (this.iByte1 < 0)
						{
							bInvalid = true;
							return;
						}
						this.aBytes.Add((byte)this.iByte1);

						if ((this.iByte1 & 0xff) == 0xe0)
						{
							// FSTSW (282)
							if (!bInvalid)
							{
								this.eCPUType = CPUEnum.i8087;
								this.eInstruction = InstructionEnum.FSTSW;
								this.sDescription = "Store status word into AX";
							}
							break;
						}
						bExitCase = false;
						switch (this.iByte1 & 0x38)
						{
							case 0x00:
								// FILD (228)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FILD;
									this.sDescription = "Integer Load to ST(0) (16-bit memory)";
								}
								bExitCase = true;
								break;
							case 0x10:
								// FIST (235)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FIST;
									this.sDescription = "Store Integer from ST(0) (16-bit memory)";
								}
								bExitCase = true;
								break;
							case 0x18:
								// FISTP (237)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FISTP;
									this.sDescription = "Store Integer from ST(0) and Pop (16-bit memory)";
								}
								bExitCase = true;
								break;
							case 0x20:
								// FBLD (195)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FBLD;
									this.sDescription = "BCD Load to ST(0)";
								}
								bExitCase = true;
								break;
							case 0x28:
								// FILD (229)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FILD;
									this.sDescription = "Integer Load to ST(0) (64-bit memory)";
								}
								bExitCase = true;
								break;
							case 0x30:
								// FBSTP (196)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FBSTP;
									this.sDescription = "Store BCD from ST(0) and Pop";
								}
								bExitCase = true;
								break;
							case 0x38:
								// FISTP (238)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8087;
									this.eInstruction = InstructionEnum.FISTP;
									this.sDescription = "Store Integer from ST(0) and Pop (64-bit memory)";
								}
								bExitCase = true;
								break;
							default:
								break;
						}
						if (bExitCase)
							break;
						break;
					case 0xe0:
						// LOOPNZ (84)
						this.aParameters.Add(ReadRelative(stream, 1, eOperandSize));
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.LOOPNZ;
							this.sDescription = "Loop Control with CX Counter while Not Zero";
						}
						break;
					case 0xe1:
						// LOOPZ (85)
						this.aParameters.Add(ReadRelative(stream, 1, eOperandSize));
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.LOOPZ;
							this.sDescription = "Loop Control with CX Counter with Zero/Equal";
						}
						break;
					case 0xe2:
						// LOOP (83)
						this.aParameters.Add(ReadRelative(stream, 1, eOperandSize));
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.LOOP;
							this.sDescription = "Loop Control with CX Counter";
						}
						break;
					case 0xe3:
						// JCXZ (63)
						this.aParameters.Add(ReadRelative(stream, 1, eOperandSize));
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.JCXZ;
							this.sDescription = "Jump on CX/ECX Zero";
						}
						break;
					case 0xe4:
					case 0xe5:
						// IN (52)
						eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, 0));
						this.aParameters.Add(ReadImmediate(stream, 1, eOperandSize, bSignExtendImmediate));
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.IN;
							this.sDescription = "Input from Port Fixed Port";
						}
						break;
					case 0xe6:
					case 0xe7:
						// OUT (104)
						eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
						this.aParameters.Add(ReadImmediate(stream, 1, eOperandSize, bSignExtendImmediate));
						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, 0));
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.OUT;
							this.sDescription = "Output to Port";
						}
						break;
					case 0xe8:
						// CALL (26)
						this.aParameters.Add(ReadRelative(stream, 2, eOperandSize));
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.CALL;
							this.sDescription = "Call Procedure direct";
						}
						break;
					case 0xe9:
						// JMP (64)
						this.aParameters.Add(ReadRelative(stream, 2, eOperandSize));
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.JMP;
							this.sDescription = "Jump full displacement";
						}
						break;
					case 0xea:
						// JMPF (67)
						this.aParameters.Add(ReadSegmentOffset(stream, eOperandSize));
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.JMPF;
							this.sDescription = "Jump Direct intersegment";
						}
						break;
					case 0xeb:
						// JMP (65)
						this.aParameters.Add(ReadRelative(stream, 1, eOperandSize));
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.JMP;
							this.sDescription = "Jump short displacement";
						}
						break;
					case 0xec:
					case 0xed:
						// IN (53)
						eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, 0));
						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, InstructionSizeEnum.Word, 2));
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.IN;
							this.sDescription = "Input from Port Variable Port";
						}
						break;
					case 0xee:
					case 0xef:
						// OUT (105)
						eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, InstructionSizeEnum.Word, 2));
						this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, 0));
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.OUT;
							this.sDescription = "Output to Port";
						}
						break;
					case 0xf0:
						// Prefix: Lock
						this.bLockPrefix = true;
						bPrefix = true;
						break;
					case 0xf2:
						// Prefix: REPNE
						this.eRepPrefix = InstructionPrefixEnum.REPNE;
						bPrefix = true;
						break;
					case 0xf3:
						// Prefix: REPE
						this.eRepPrefix = InstructionPrefixEnum.REPE;
						bPrefix = true;
						break;
					case 0xf4:
						// HLT (47)
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.HLT;
							this.sDescription = "Halt";
						}
						break;
					case 0xf5:
						// CMC (35)
						this.eModifiedFlags = FlagsEnum.CF;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.CMC;
							this.sDescription = "Complement Carry Flag";
						}
						break;
					case 0xf6:
					case 0xf7:
						this.iByte1 = stream.ReadByte();
						if (this.iByte1 < 0)
						{
							bInvalid = true;
							return;
						}
						this.aBytes.Add((byte)this.iByte1);

						switch (this.iByte1 & 0x38)
						{
							case 0x00:
								// TEST (179)
								eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								this.aParameters.Add(ReadImmediate(stream, 0, eOperandSize, bSignExtendImmediate));
								this.eClearedFlags = FlagsEnum.CF | FlagsEnum.OF;
								this.eModifiedFlags = FlagsEnum.PF | FlagsEnum.ZF | FlagsEnum.SF;
								this.eUndefinedFlags = FlagsEnum.AF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.TEST;
									this.sDescription = "Logical Compare immediate and register/memory";
								}
								break;
							case 0x10:
								// NOT (100)
								eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.NOT;
									this.sDescription = "One's Complement Negation register/memory";
								}
								break;
							case 0x18:
								// NEG (98)
								eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.ZF | FlagsEnum.SF | FlagsEnum.OF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.NEG;
									this.sDescription = "Two's Complement Negation register/memory";
								}
								break;
							case 0x20:
								// MUL (97)
								eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.OF;
								this.eUndefinedFlags = FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.ZF | FlagsEnum.SF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.MUL;
									this.sDescription = "Unsigned Multiplication of AL or AX with register/memory";
								}
								break;
							case 0x28:
								// IMUL (51)
								eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								this.eModifiedFlags = FlagsEnum.CF | FlagsEnum.OF;
								this.eUndefinedFlags = FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.ZF | FlagsEnum.SF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i80186;
									this.eInstruction = InstructionEnum.IMUL;
									this.sDescription = "Signed Multiply accumulator with register/memory to AX or DX:AX";
								}
								break;
							case 0x30:
								// DIV (45)
								eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								this.eUndefinedFlags = FlagsEnum.CF | FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.ZF | FlagsEnum.SF | FlagsEnum.OF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.DIV;
									this.sDescription = "Unsigned Divide Accumulator by register/memory";
								}
								break;
							case 0x38:
								// IDIV (48)
								eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								this.eUndefinedFlags = FlagsEnum.CF | FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.ZF | FlagsEnum.SF | FlagsEnum.OF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.IDIV;
									this.sDescription = "Signed Divide Accumulator by register/memory";
								}
								break;
							default:
								bInvalid = true;
								break;
						}
						break;
					case 0xf8:
						// CLC (31)
						this.eClearedFlags = FlagsEnum.CF;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.CLC;
							this.sDescription = "Clear Carry Flag";
						}
						break;
					case 0xf9:
						// STC (169)
						this.eSetFlags = FlagsEnum.CF;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.STC;
							this.sDescription = "Set Carry Flag";
						}
						break;
					case 0xfa:
						// CLI (33)
						this.eClearedFlags = FlagsEnum.IF;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.CLI;
							this.sDescription = "Clear Interrupt Enable Flag";
						}
						break;
					case 0xfb:
						// STI (171)
						this.eSetFlags = FlagsEnum.IF;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.STI;
							this.sDescription = "Set Interrupt Enable Flag";
						}
						break;
					case 0xfc:
						// CLD (32)
						this.eClearedFlags = FlagsEnum.DF;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.CLD;
							this.sDescription = "Clear Direction Flag";
						}
						break;
					case 0xfd:
						// STD (170)
						this.eSetFlags = FlagsEnum.DF;
						if (!bInvalid)
						{
							this.eCPUType = CPUEnum.i8086;
							this.eInstruction = InstructionEnum.STD;
							this.sDescription = "Set Direction Flag";
						}
						break;
					case 0xfe:
						this.iByte1 = stream.ReadByte();
						if (this.iByte1 < 0)
						{
							bInvalid = true;
							return;
						}
						this.aBytes.Add((byte)this.iByte1);

						switch (this.iByte1 & 0x38)
						{
							case 0x00:
								// INC (55)
								eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								this.eModifiedFlags = FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.ZF | FlagsEnum.SF | FlagsEnum.OF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.INC;
									this.sDescription = "Increment by 1 register/memory";
								}
								break;
							case 0x08:
								// DEC (44)
								eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								this.eModifiedFlags = FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.ZF | FlagsEnum.SF | FlagsEnum.OF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.DEC;
									this.sDescription = "Decrement by 1 register/memory";
								}
								break;
							default:
								bInvalid = true;
								break;
						}
						break;
					case 0xff:
						this.iByte1 = stream.ReadByte();
						if (this.iByte1 < 0)
						{
							bInvalid = true;
							return;
						}
						this.aBytes.Add((byte)this.iByte1);

						switch (this.iByte1 & 0x38)
						{
							case 0x00:
								// INC (55)
								eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								this.eModifiedFlags = FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.ZF | FlagsEnum.SF | FlagsEnum.OF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.INC;
									this.sDescription = "Increment by 1 register/memory";
								}
								break;
							case 0x08:
								// DEC (44)
								eOperandSize = ((this.iByte0 & 0x01) != 0) ? eOperandSize : InstructionSizeEnum.Byte;
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								this.eModifiedFlags = FlagsEnum.PF | FlagsEnum.AF | FlagsEnum.ZF | FlagsEnum.SF | FlagsEnum.OF;
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.DEC;
									this.sDescription = "Decrement by 1 register/memory";
								}
								break;
							case 0x10:
								// CALL (27)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.CALL;
									this.sDescription = "Call Procedure register/memory Indirect";
								}
								break;
							case 0x18:
								// CALLF (29)
								this.aParameters.Add(MemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.CALLF;
									this.sDescription = "Call Procedure indirect intersegment";
								}
								break;
							case 0x20:
								// JMP (66)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.JMP;
									this.sDescription = "Jump register/memory Indirect";
								}
								break;
							case 0x28:
								// JMPF (68)
								this.aParameters.Add(MemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.JMPF;
									this.sDescription = "Jump Indirect intersegment";
								}
								break;
							case 0x30:
								// PUSH (125)
								this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte1 & 0xc7));
								if (!bInvalid)
								{
									this.eCPUType = CPUEnum.i8086;
									this.eInstruction = InstructionEnum.PUSH;
									this.sDescription = "Push Operand onto the Stack register/memory";
								}
								break;
							default:
								bInvalid = true;
								break;
						}
						break;
					default:
						bInvalid = true;
						break;
				}
			} while (bPrefix);

			if (bReverseDirection)
			{
				InstructionParameter oTemp = this.aParameters[0];
				this.aParameters[0] = this.aParameters[1];
				this.aParameters[1] = oTemp;
			}
		}
	}
}
