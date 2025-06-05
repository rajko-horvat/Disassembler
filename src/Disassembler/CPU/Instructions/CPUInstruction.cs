using System.Text;

namespace Disassembler.CPU
{
	public partial class CPUInstruction
	{
		// location of a instruction
		private uint uiLinearAddress = 0;
		private ushort usSegment = 0;
		private ushort usOffset = 0;

		private CPUTypeEnum eCPUType = CPUTypeEnum.Undefined;
		private CPUInstructionEnum eInstruction = CPUInstructionEnum.Undefined;
		private CPUParameterSizeEnum eDefaultSize = CPUParameterSizeEnum.UInt16;
		private CPUParameterSizeEnum eOperandSize = CPUParameterSizeEnum.UInt16;
		private CPUParameterSizeEnum eAddressSize = CPUParameterSizeEnum.UInt16;
		private CPUSegmentRegisterEnum eDefaultDataSegment = CPUSegmentRegisterEnum.Undefined;

		private string sDescription = "";
		private List<CPUParameter> aParameters = new List<CPUParameter>();
		private bool bInvalid = false;
		private List<byte> aBytes = new List<byte>();
		// instruction bytes (not including the prefixes)
		private int iByte0 = -1;
		private int iByte1 = -1;
		private int iByte2 = -1;

		private bool bFPUDestination0 = false;

		// this instruction location is referenced by jump
		private bool bLabel = false;
		private int labelOrdinal = -1;

		// prefixes
		private bool bLockPrefix = false;
		private CPUInstructionPrefixEnum eRepPrefix = CPUInstructionPrefixEnum.Undefined;

		// flags
		private CPUFlagsEnum eClearedFlags = CPUFlagsEnum.Undefined;
		private CPUFlagsEnum eSetFlags = CPUFlagsEnum.Undefined;
		private CPUFlagsEnum eModifiedFlags = CPUFlagsEnum.Undefined;
		private CPUFlagsEnum eUndefinedFlags = CPUFlagsEnum.Undefined;

		public CPUInstruction(ushort segment, ushort offset, CPUInstructionEnum instructiontype, CPUParameterSizeEnum size)
		{
			this.uiLinearAddress = MainProgram.ToLinearAddress(segment, offset);
			this.usSegment = segment;
			this.usOffset = offset;
			this.eInstruction = instructiontype;
			this.eDefaultSize = size;
			this.eOperandSize = size;
			this.eAddressSize = size;
		}

		public CPUInstruction(ushort segment, ushort offset, MemoryStream stream)
		{
			this.uiLinearAddress = MainProgram.ToLinearAddress(segment, offset);
			this.usSegment = segment;
			this.usOffset = offset;

			DecodeInstruction(stream);
		}

		public void DecodeInstruction(MemoryStream stream)
		{
			Decode(stream);

			// check for REP prefix validity
			if (this.eRepPrefix == CPUInstructionPrefixEnum.REPE)
			{
				switch (this.eInstruction)
				{
					// REP/REPE is valid only for these instructions
					case CPUInstructionEnum.CMPS:
					case CPUInstructionEnum.LODS:
					case CPUInstructionEnum.MOVS:
					case CPUInstructionEnum.SCAS:
					case CPUInstructionEnum.STOS:
					case CPUInstructionEnum.INS:
					case CPUInstructionEnum.OUTS:
						break;
					default:
						this.bInvalid = true;
						break;
				}
			}
			else if (this.eRepPrefix == CPUInstructionPrefixEnum.REPNE)
			{
				switch (this.eInstruction)
				{
					// REPNE is valid only for these instructions
					case CPUInstructionEnum.CMPS:
					case CPUInstructionEnum.SCAS:
						break;
					default:
						this.bInvalid = true;
						break;
				}
			}
		}

		public uint LinearAddress
		{
			get { return this.uiLinearAddress; }
		}

		public ushort Segment
		{
			get
			{
				return this.usSegment;
			}
		}

		public ushort Offset
		{
			get
			{
				return this.usOffset;
			}
		}

		public CPUTypeEnum CPUType
		{
			get
			{
				return this.eCPUType;
			}
		}

		public CPUInstructionEnum InstructionType
		{
			get { return this.eInstruction; }
			set { this.eInstruction = value; }
		}

		public CPUParameterSizeEnum DefaultSize
		{
			get
			{
				return this.eDefaultSize;
			}
		}

		public CPUSegmentRegisterEnum DefaultDataSegment
		{
			get { return this.eDefaultDataSegment; }
		}

		public string GetDefaultDataSegmentTextMZ()
		{
			switch (this.eDefaultDataSegment)
			{
				case CPUSegmentRegisterEnum.Undefined:
					return string.Format("this.oCPU.DS");

				default:
					return string.Format("this.oCPU.{0}", this.eDefaultDataSegment.ToString());
			}
		}

		public CPUParameterSizeEnum OperandSize
		{
			get { return this.eOperandSize; }
			set { this.eOperandSize = value; }
		}

		public CPUParameterSizeEnum AddressSize
		{
			get
			{
				return this.eAddressSize;
			}
		}

		public string Description
		{
			get
			{
				return this.sDescription;
			}
		}

		public bool Invalid
		{
			get
			{
				return this.bInvalid;
			}
		}

		public List<byte> Bytes
		{
			get
			{
				return this.aBytes;
			}
		}

		public List<CPUParameter> Parameters
		{
			get
			{
				return this.aParameters;
			}
		}

		public bool FPUDestination0
		{
			get
			{
				return this.bFPUDestination0;
			}
		}

		public bool Label
		{
			get { return this.bLabel; }
			set { this.bLabel = value; }
		}

		public int LabelOrdinal
		{
			get { return this.labelOrdinal; }
			set { this.labelOrdinal = value; }
		}

		public string LabelName
		{
			get
			{
				if (this.labelOrdinal < 0)
				{
					return $"L_{this.usOffset:x4}";
				}

				return $"Label{this.labelOrdinal}";
			}
		}

		// prefixes
		public bool LockPrefix
		{
			get
			{
				return this.bLockPrefix;
			}
		}

		public CPUInstructionPrefixEnum RepPrefix
		{
			get
			{
				return this.eRepPrefix;
			}
		}

		// flags
		public CPUFlagsEnum ClearedFlags
		{
			get
			{
				return this.eClearedFlags;
			}
		}

		public CPUFlagsEnum SetFlags
		{
			get
			{
				return this.eSetFlags;
			}
		}

		public CPUFlagsEnum ModifiedFlags
		{
			get
			{
				return this.eModifiedFlags;
			}
		}

		public CPUFlagsEnum UndefinedFlags
		{
			get
			{
				return this.eUndefinedFlags;
			}
		}

		static public bool TestFlag(CPUFlagsEnum flags, CPUFlagsEnum flag)
		{
			return (flags & flag) == flag;
		}

		private byte ReadByte(MemoryStream stream)
		{
			int byte0 = stream.ReadByte();
			if (byte0 < 0)
				this.bInvalid = true;
			this.aBytes.Add((byte)byte0);

			byte0 &= 0xff;

			return (byte)byte0;
		}

		private ushort ReadWord(MemoryStream stream)
		{
			int byte0 = stream.ReadByte();
			int byte1 = stream.ReadByte();
			if (byte0 < 0 || byte1 < 0)
				this.bInvalid = true;
			this.aBytes.Add((byte)byte0);
			this.aBytes.Add((byte)byte1);

			byte0 &= 0xff;
			byte1 &= 0xff;

			return (ushort)((byte1 << 8) | byte0);
		}

		private uint ReadDWord(MemoryStream stream)
		{
			int byte0 = stream.ReadByte();
			int byte1 = stream.ReadByte();
			int byte2 = stream.ReadByte();
			int byte3 = stream.ReadByte();
			if (byte0 < 0 || byte1 < 0 || byte2 < 0 || byte3 < 0)
				this.bInvalid = true;
			this.aBytes.Add((byte)byte0);
			this.aBytes.Add((byte)byte1);
			this.aBytes.Add((byte)byte2);
			this.aBytes.Add((byte)byte3);

			byte0 &= 0xff;
			byte1 &= 0xff;
			byte2 &= 0xff;
			byte3 &= 0xff;

			return (uint)(((uint)byte3 << 24) | ((uint)byte2 << 16) | ((uint)byte1 << 8) | (uint)byte0);
		}

		private CPUParameter ReadImmediateMemoryOffset(MemoryStream stream, CPUSegmentRegisterEnum segment, CPUParameterSizeEnum size)
		{
			CPUParameter parameter;

			// read word or dword address
			switch (size)
			{
				case CPUParameterSizeEnum.UInt16:
					parameter = new CPUParameter(CPUParameterTypeEnum.MemoryAddress, size, segment,
						6, ReadWord(stream));
					break;

				case CPUParameterSizeEnum.UInt32:
					parameter = new CPUParameter(CPUParameterTypeEnum.MemoryAddress, size, segment,
						6, (int)ReadDWord(stream));
					break;

				default:
					throw new Exception("Undefined immediate memory offset size");
			}

			return parameter;
		}

		private CPUParameter ReadSegmentOffsetBySize(MemoryStream stream, CPUParameterSizeEnum size)
		{
			uint usOffset;
			ushort usSegment;

			switch (size)
			{
				case CPUParameterSizeEnum.UInt16:
					usOffset = ReadWord(stream);
					usSegment = ReadWord(stream);

					return new CPUParameter(usSegment, usOffset);

				case CPUParameterSizeEnum.UInt32:
					usOffset = ReadDWord(stream);
					usSegment = ReadWord(stream);

					return new CPUParameter(usSegment, usOffset);

				default:
					throw new Exception("Undefined Segment, Offset value size");
			}
		}

		private int ReadRelativeBySize(MemoryStream stream, CPUParameterSizeEnum size)
		{
			// read byte, word or dword
			switch (size)
			{
				case CPUParameterSizeEnum.UInt8:
					return (sbyte)ReadByte(stream);

				case CPUParameterSizeEnum.UInt16:
					return (short)ReadWord(stream);

				case CPUParameterSizeEnum.UInt32:
					return (int)ReadDWord(stream);

				default:
					throw new Exception("Undefined relative value size");
			}
		}

		private uint SignExtend(uint value, CPUParameterSizeEnum from, CPUParameterSizeEnum to)
		{
			switch (from)
			{
				case CPUParameterSizeEnum.UInt8:
					switch (to)
					{
						case CPUParameterSizeEnum.UInt16:
							return (value & 0x80) != 0 ? value | 0xff00 : value;

						case CPUParameterSizeEnum.UInt32:
							return (value & 0x80) != 0 ? value | 0xffffff00 : value;

						default:
							return value;
					}

				case CPUParameterSizeEnum.UInt16:
					switch (to)
					{
						case CPUParameterSizeEnum.UInt16:
							return value;

						case CPUParameterSizeEnum.UInt32:
							return (value & 0x8000) != 0 ? value | 0xffff0000 : value;

						default:
							throw new Exception(string.Format("Can't convert from {0} to {1}", from.ToString(), to.ToString()));
					}

				default:
					throw new Exception(string.Format("Can't convert from {0} to {1}", from.ToString(), to.ToString()));
			}
		}

		private CPUParameter ToSegmentRegisterParameter(CPUParameterSizeEnum operandSize, uint segmentRegister)
		{
			CPUParameter parameter;

			switch (operandSize)
			{
				case CPUParameterSizeEnum.UInt8:
					parameter = new CPUParameter(CPUParameterTypeEnum.SegmentRegister, operandSize, (uint)CPUSegmentRegisterEnum.Invalid);
					break;

				case CPUParameterSizeEnum.UInt16:
				case CPUParameterSizeEnum.UInt32:
					operandSize = CPUParameterSizeEnum.UInt16; // Segment registers are fixed to 16bit value

					switch (segmentRegister)
					{
						case 0:
							parameter = new CPUParameter(CPUParameterTypeEnum.SegmentRegister, operandSize, (uint)CPUSegmentRegisterEnum.ES);
							break;

						case 1:
							parameter = new CPUParameter(CPUParameterTypeEnum.SegmentRegister, operandSize, (uint)CPUSegmentRegisterEnum.CS);
							break;

						case 2:
							parameter = new CPUParameter(CPUParameterTypeEnum.SegmentRegister, operandSize, (uint)CPUSegmentRegisterEnum.SS);
							break;

						case 3:
							parameter = new CPUParameter(CPUParameterTypeEnum.SegmentRegister, operandSize, (uint)CPUSegmentRegisterEnum.DS);
							break;

						case 4:
							parameter = new CPUParameter(CPUParameterTypeEnum.SegmentRegister, operandSize, (uint)CPUSegmentRegisterEnum.FS);
							break;

						case 5:
							parameter = new CPUParameter(CPUParameterTypeEnum.SegmentRegister, operandSize, (uint)CPUSegmentRegisterEnum.GS);
							break;

						case 6:
							parameter = new CPUParameter(CPUParameterTypeEnum.SegmentRegister, operandSize, (uint)CPUSegmentRegisterEnum.Invalid);
							break;

						case 7:
							parameter = new CPUParameter(CPUParameterTypeEnum.SegmentRegister, operandSize, (uint)CPUSegmentRegisterEnum.Invalid);
							break;

						default:
							throw new Exception("Unknown segment register"); // silence compiler error
					}
					break;

				default:
					throw new Exception("Unknown segment register"); // silence compiler error
			}

			return parameter;
		}

		private CPUParameter ToRegisterParameter(CPUParameterSizeEnum operandSize, uint register)
		{
			CPUParameter parameter;

			if (operandSize == CPUParameterSizeEnum.UInt8)
			{
				switch (register)
				{
					case 0:
						parameter = new CPUParameter(CPUParameterTypeEnum.Register, operandSize, (uint)CPURegisterEnum.AL);
						break;

					case 1:
						parameter = new CPUParameter(CPUParameterTypeEnum.Register, operandSize, (uint)CPURegisterEnum.CL);
						break;

					case 2:
						parameter = new CPUParameter(CPUParameterTypeEnum.Register, operandSize, (uint)CPURegisterEnum.DL);
						break;

					case 3:
						parameter = new CPUParameter(CPUParameterTypeEnum.Register, operandSize, (uint)CPURegisterEnum.BL);
						break;

					case 4:
						parameter = new CPUParameter(CPUParameterTypeEnum.Register, operandSize, (uint)CPURegisterEnum.AH);
						break;

					case 5:
						parameter = new CPUParameter(CPUParameterTypeEnum.Register, operandSize, (uint)CPURegisterEnum.CH);
						break;

					case 6:
						parameter = new CPUParameter(CPUParameterTypeEnum.Register, operandSize, (uint)CPURegisterEnum.DH);
						break;

					case 7:
						parameter = new CPUParameter(CPUParameterTypeEnum.Register, operandSize, (uint)CPURegisterEnum.BH);
						break;

					default:
						throw new Exception("Unknown register"); // silence compiler error
				}
			}
			else
			{
				switch (register)
				{
					case 0:
						parameter = new CPUParameter(CPUParameterTypeEnum.Register, operandSize, (uint)CPURegisterEnum.AX);
						break;

					case 1:
						parameter = new CPUParameter(CPUParameterTypeEnum.Register, operandSize, (uint)CPURegisterEnum.CX);
						break;

					case 2:
						parameter = new CPUParameter(CPUParameterTypeEnum.Register, operandSize, (uint)CPURegisterEnum.DX);
						break;

					case 3:
						parameter = new CPUParameter(CPUParameterTypeEnum.Register, operandSize, (uint)CPURegisterEnum.BX);
						break;

					case 4:
						parameter = new CPUParameter(CPUParameterTypeEnum.Register, operandSize, (uint)CPURegisterEnum.SP);
						break;

					case 5:
						parameter = new CPUParameter(CPUParameterTypeEnum.Register, operandSize, (uint)CPURegisterEnum.BP);
						break;

					case 6:
						parameter = new CPUParameter(CPUParameterTypeEnum.Register, operandSize, (uint)CPURegisterEnum.SI);
						break;

					case 7:
						parameter = new CPUParameter(CPUParameterTypeEnum.Register, operandSize, (uint)CPURegisterEnum.DI);
						break;

					default:
						throw new Exception("Unknown register"); // silence compiler error
				}
			}

			return parameter;
		}

		private CPUParameter ToRegisterOrMemoryAddressingParameter(MemoryStream stream, CPUSegmentRegisterEnum defaultSegment,
			CPUParameterSizeEnum operandSize, CPUParameterSizeEnum addressSize, int value)
		{
			CPUParameter parameter;
			uint mod = (uint)((value & 0xc0) >> 6);
			uint rm = (uint)(value & 0x7);

			if (addressSize != CPUParameterSizeEnum.UInt16)
			{
				Console.WriteLine("x32 addressing mode not yet implemented");
				//throw new Exception("x32 addressing mode not yet implemented");
				return new CPUParameter(CPUParameterTypeEnum.Undefined, 0);
			}

			switch (mod)
			{
				case 0:
					// direct displacement
					if (rm == 6)
					{
						parameter = new CPUParameter(CPUParameterTypeEnum.MemoryAddress, addressSize,
							defaultSegment, mod << 3 | rm, (short)ReadWord(stream));
					}
					else
					{
						parameter = new CPUParameter(CPUParameterTypeEnum.MemoryAddress, addressSize,
							defaultSegment, mod << 3 | rm);
					}
					break;

				case 1:
					parameter = new CPUParameter(CPUParameterTypeEnum.MemoryAddress, addressSize,
						defaultSegment, mod << 3 | rm, (sbyte)ReadByte(stream));
					break;

				case 2:
					parameter = new CPUParameter(CPUParameterTypeEnum.MemoryAddress, addressSize,
						defaultSegment, mod << 3 | rm, (short)ReadWord(stream));
					break;

				case 3:
					parameter = ToRegisterParameter(operandSize, rm);
					break;

				default:
					throw new Exception("Unknown addressing type");
			}

			return parameter;
		}

		private CPUParameter MemoryAddressing(MemoryStream stream, CPUSegmentRegisterEnum defaultSegment,
			CPUParameterSizeEnum operandSize, CPUParameterSizeEnum addressSize, int value)
		{
			CPUParameter parameter;
			uint mod = (uint)((value & 0xc0) >> 6);
			uint rm = (uint)(value & 0x7);

			if (addressSize != CPUParameterSizeEnum.UInt16)
				throw new Exception("x32 addressing mode not yet implemented");

			switch (mod)
			{
				case 0:
					// direct displacement
					if (rm == 6)
					{
						parameter = new CPUParameter(CPUParameterTypeEnum.MemoryAddress, addressSize,
							defaultSegment, mod << 3 | rm, (short)ReadWord(stream));
					}
					else
					{
						parameter = new CPUParameter(CPUParameterTypeEnum.MemoryAddress, addressSize,
							defaultSegment, mod << 3 | rm);
					}
					break;

				case 1:
					parameter = new CPUParameter(CPUParameterTypeEnum.MemoryAddress, addressSize,
						defaultSegment, mod << 3 | rm, (sbyte)ReadByte(stream));
					break;


				case 2:
					parameter = new CPUParameter(CPUParameterTypeEnum.MemoryAddress, addressSize,
						defaultSegment, mod << 3 | rm, (short)ReadWord(stream));
					break;

				case 3:
					this.bInvalid = true;
					parameter = ToRegisterParameter(operandSize, rm);
					break;

				default:
					throw new Exception("Unknown addressing mode");
			}

			return parameter;
		}

		private CPUParameter ReadImmediate(MemoryStream stream, int size, CPUParameterSizeEnum operandSize, bool signExtend)
		{
			CPUParameter oParameter;

			if (size == 0)
			{
				// determine size by operand size prefix
				switch (operandSize)
				{
					case CPUParameterSizeEnum.UInt8:
						// read byte
						oParameter = new CPUParameter(CPUParameterTypeEnum.Immediate, operandSize,
							ReadByte(stream));
						break;

					case CPUParameterSizeEnum.UInt16:
						// read word
						if (signExtend)
						{
							oParameter = new CPUParameter(CPUParameterTypeEnum.Immediate, operandSize,
								SignExtend(ReadByte(stream), CPUParameterSizeEnum.UInt8, CPUParameterSizeEnum.UInt16));
						}
						else
						{
							oParameter = new CPUParameter(CPUParameterTypeEnum.Immediate, operandSize,
								ReadWord(stream));
						}
						break;

					case CPUParameterSizeEnum.UInt32:
						// read dword
						if (signExtend)
						{
							oParameter = new CPUParameter(CPUParameterTypeEnum.Immediate, operandSize,
								SignExtend(ReadByte(stream), CPUParameterSizeEnum.UInt8, CPUParameterSizeEnum.UInt32));
						}
						else
						{
							oParameter = new CPUParameter(CPUParameterTypeEnum.Immediate, operandSize,
								ReadDWord(stream));
						}
						break;

					default:
						throw new Exception("Unknown parameter size");
				}
			}
			else
			{
				switch (size)
				{
					case 1:
						// read byte
						oParameter = new CPUParameter(CPUParameterTypeEnum.Immediate,
							CPUParameterSizeEnum.UInt8, ReadByte(stream));
						break;

					case 2:
						// read word
						oParameter = new CPUParameter(CPUParameterTypeEnum.Immediate,
							CPUParameterSizeEnum.UInt16, ReadWord(stream));
						break;

					default:
						throw new Exception("Unknown immediate size");
				}
			}

			return oParameter;
		}

		public override string ToString()
		{
			StringBuilder sbValue = new StringBuilder();

			if (this.bInvalid)
			{
				sbValue.Append("[Invalid] ");
			}

			if (this.bLockPrefix)
			{
				sbValue.Append("LOCK ");
			}

			switch (this.eRepPrefix)
			{
				case CPUInstructionPrefixEnum.REPE:
					sbValue.Append("REPE ");
					break;
				case CPUInstructionPrefixEnum.REPNE:
					sbValue.Append("REPNE ");
					break;
				default:
					break;
			}

			CPUParameter oAcc = new CPUParameter(CPUParameterTypeEnum.Register, this.eOperandSize, 0);

			switch (this.eInstruction)
			{
				case CPUInstructionEnum.AAA:
					sbValue.Append("AAA");
					break;

				case CPUInstructionEnum.AAD:
					sbValue.Append("AAD");
					break;

				case CPUInstructionEnum.AAM:
					sbValue.Append("AAM");
					break;

				case CPUInstructionEnum.AAS:
					sbValue.Append("AAS");
					break;

				case CPUInstructionEnum.ADC:
					sbValue.AppendFormat("ADC {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;

				case CPUInstructionEnum.ADD:
					sbValue.AppendFormat("ADD {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;

				case CPUInstructionEnum.AND:
					sbValue.AppendFormat("AND {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;

				case CPUInstructionEnum.ARPL:
					sbValue.AppendFormat("ARPL {0}, {1}", this.aParameters[1].ToString(), this.aParameters[0].ToString());
					break;

				case CPUInstructionEnum.BOUND:
					sbValue.AppendFormat("BOUND {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;

				case CPUInstructionEnum.BSF:
					sbValue.AppendFormat("BSF {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;

				case CPUInstructionEnum.BSR:
					sbValue.AppendFormat("BSR {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;

				case CPUInstructionEnum.BSWAP:
					sbValue.AppendFormat("BSWAP {0}", this.aParameters[0].ToString());
					break;

				case CPUInstructionEnum.BT:
					sbValue.AppendFormat("BT {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;

				case CPUInstructionEnum.BTC:
					sbValue.AppendFormat("BTC {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;

				case CPUInstructionEnum.BTR:
					sbValue.AppendFormat("BTR {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;

				case CPUInstructionEnum.BTS:
					sbValue.AppendFormat("BTS {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;

				case CPUInstructionEnum.CALL:
					sbValue.AppendFormat("CALL {0}", this.aParameters[0].ToString());
					break;

				case CPUInstructionEnum.CALLF:
					sbValue.AppendFormat("CALL far {0}", this.aParameters[0].ToString());
					break;

				case CPUInstructionEnum.CBW:
					if (this.eOperandSize == CPUParameterSizeEnum.UInt16)
						sbValue.Append("CBW");
					else
						sbValue.Append("CWDE");
					break;

				case CPUInstructionEnum.CLC:
					sbValue.Append("CLC");
					break;

				case CPUInstructionEnum.CLD:
					sbValue.Append("CLD");
					break;

				case CPUInstructionEnum.CLI:
					sbValue.Append("CLI");
					break;

				case CPUInstructionEnum.CLTS:
					sbValue.Append("CLTS");
					break;

				case CPUInstructionEnum.CMC:
					sbValue.Append("CMC");
					break;

				case CPUInstructionEnum.CMP:
					sbValue.AppendFormat("CMP {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;

				case CPUInstructionEnum.CMPS:
					sbValue.AppendFormat("CMPS{0}", GetSizeSuffix());
					break;

				case CPUInstructionEnum.CWD:
					if (this.eOperandSize == CPUParameterSizeEnum.UInt16)
						sbValue.Append("CWD");
					else
						sbValue.Append("CDQ");
					break;

				case CPUInstructionEnum.DAA:
					sbValue.Append("DAA");
					break;

				case CPUInstructionEnum.DAS:
					sbValue.Append("DAS");
					break;

				case CPUInstructionEnum.DEC:
					sbValue.AppendFormat("DEC {0}", this.aParameters[0].ToString());
					break;

				case CPUInstructionEnum.DIV:
					sbValue.AppendFormat("DIV {0}, {1}", oAcc.ToString(), this.aParameters[0].ToString());
					break;

				case CPUInstructionEnum.ENTER:
					sbValue.AppendFormat("ENTER {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;

				case CPUInstructionEnum.F2XM1:
				case CPUInstructionEnum.FABS:
				case CPUInstructionEnum.FADD:
				case CPUInstructionEnum.FADDP:
				case CPUInstructionEnum.FBLD:
				case CPUInstructionEnum.FBSTP:
				case CPUInstructionEnum.FCHS:
				case CPUInstructionEnum.FCLEX:
				case CPUInstructionEnum.FCOM:
				case CPUInstructionEnum.FCOMP:
				case CPUInstructionEnum.FCOMPP:
				case CPUInstructionEnum.FCOS:
				case CPUInstructionEnum.FDECSTP:
				case CPUInstructionEnum.FDIV:
				case CPUInstructionEnum.FDIVP:
				case CPUInstructionEnum.FDIVR:
				case CPUInstructionEnum.FDIVRP:
				case CPUInstructionEnum.FFREE:
				case CPUInstructionEnum.FIADD:
				case CPUInstructionEnum.FICOM:
				case CPUInstructionEnum.FICOMP:
				case CPUInstructionEnum.FIDIV:
				case CPUInstructionEnum.FIDIVR:
				case CPUInstructionEnum.FILD:
				case CPUInstructionEnum.FIMUL:
				case CPUInstructionEnum.FINCSTP:
				case CPUInstructionEnum.FINIT:
				case CPUInstructionEnum.FIST:
				case CPUInstructionEnum.FISTP:
				case CPUInstructionEnum.FISUB:
				case CPUInstructionEnum.FISUBR:
				case CPUInstructionEnum.FLD:
				case CPUInstructionEnum.FLD1:
				case CPUInstructionEnum.FLDCW:
				case CPUInstructionEnum.FLDENV:
				case CPUInstructionEnum.FLDL2E:
				case CPUInstructionEnum.FLDL2T:
				case CPUInstructionEnum.FLDLG2:
				case CPUInstructionEnum.FLDLN2:
				case CPUInstructionEnum.FLDPI:
				case CPUInstructionEnum.FLDZ:
				case CPUInstructionEnum.FMUL:
				case CPUInstructionEnum.FMULP:
				case CPUInstructionEnum.FNOP:
				case CPUInstructionEnum.FPATAN:
				case CPUInstructionEnum.FPREM:
				case CPUInstructionEnum.FPREM1:
				case CPUInstructionEnum.FPTAN:
				case CPUInstructionEnum.FRNDINT:
				case CPUInstructionEnum.FRSTOR:
				case CPUInstructionEnum.FSAVE:
				case CPUInstructionEnum.FSCALE:
				case CPUInstructionEnum.FSIN:
				case CPUInstructionEnum.FSINCOS:
				case CPUInstructionEnum.FSQRT:
				case CPUInstructionEnum.FST:
				case CPUInstructionEnum.FSTCW:
				case CPUInstructionEnum.FSTENV:
				case CPUInstructionEnum.FSTP:
				case CPUInstructionEnum.FSTSW:
				case CPUInstructionEnum.FSUB:
				case CPUInstructionEnum.FSUBP:
				case CPUInstructionEnum.FSUBR:
				case CPUInstructionEnum.FSUBRP:
				case CPUInstructionEnum.FTST:
				case CPUInstructionEnum.FUCOM:
				case CPUInstructionEnum.FUCOMP:
				case CPUInstructionEnum.FUCOMPP:
				case CPUInstructionEnum.FXAM:
				case CPUInstructionEnum.FXCH:
				case CPUInstructionEnum.FXTRACT:
				case CPUInstructionEnum.FYL2X:
				case CPUInstructionEnum.FYL2XP1:
					sbValue.Append(Enum.GetName(typeof(CPUInstructionEnum), this.eInstruction));
					break;

				case CPUInstructionEnum.HLT:
					sbValue.Append("HLT");
					break;

				case CPUInstructionEnum.IDIV:
					sbValue.Append("IDIV ");
					if (this.eOperandSize == CPUParameterSizeEnum.UInt8)
						sbValue.AppendFormat("{0}", this.aParameters[0].ToString());
					else
						sbValue.AppendFormat("{0}, {1}", oAcc.ToString(), this.aParameters[0].ToString());
					break;

				case CPUInstructionEnum.IMUL:
					sbValue.Append("IMUL ");
					for (int i = 0; i < this.aParameters.Count; i++)
					{
						if (i > 0)
							sbValue.Append(", ");
						sbValue.Append(this.aParameters[i].ToString());
					}
					break;

				case CPUInstructionEnum.IN:
					sbValue.AppendFormat("IN {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;

				case CPUInstructionEnum.INC:
					sbValue.AppendFormat("INC {0}", this.aParameters[0].ToString());
					break;

				case CPUInstructionEnum.INS:
					sbValue.AppendFormat("INS{0}", GetSizeSuffix());
					break;

				case CPUInstructionEnum.INT:
					sbValue.AppendFormat("INT {0}", this.aParameters[0].ToString());
					break;

				case CPUInstructionEnum.INTO:
					sbValue.Append("INTO");
					break;

				case CPUInstructionEnum.IRET:
					sbValue.Append("IRET");
					break;

				case CPUInstructionEnum.Jcc:
					sbValue.AppendFormat("J{0} {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;

				case CPUInstructionEnum.JCXZ:
					sbValue.AppendFormat("JCXZ {0}", this.aParameters[0].ToString());
					break;

				case CPUInstructionEnum.JMP:
					sbValue.AppendFormat("JMP {0}", this.aParameters[0].ToString());
					break;

				case CPUInstructionEnum.JMPF:
					sbValue.AppendFormat("JMP far {0}", this.aParameters[0].ToString());
					break;

				case CPUInstructionEnum.LAHF:
					sbValue.Append("LAHF");
					break;

				case CPUInstructionEnum.LAR:
					sbValue.AppendFormat("LAR {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;

				case CPUInstructionEnum.LDS:
					sbValue.AppendFormat("LDS {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;

				case CPUInstructionEnum.LEA:
					sbValue.AppendFormat("LEA {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;

				case CPUInstructionEnum.LEAVE:
					sbValue.Append("LEAVE");
					break;

				case CPUInstructionEnum.LES:
					sbValue.AppendFormat("LES {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;

				case CPUInstructionEnum.LFS:
					sbValue.AppendFormat("LFS {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;

				case CPUInstructionEnum.LGDT:
					sbValue.AppendFormat("LGDT {0}", this.aParameters[0].ToString());
					break;

				case CPUInstructionEnum.LGS:
					sbValue.AppendFormat("LGS {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;

				case CPUInstructionEnum.LIDT:
					sbValue.AppendFormat("LIDT {0}", this.aParameters[0].ToString());
					break;

				case CPUInstructionEnum.LLDT:
					sbValue.AppendFormat("LLDT {0}", this.aParameters[0].ToString());
					break;

				case CPUInstructionEnum.LMSW:
					sbValue.AppendFormat("LMSW {0}", this.aParameters[0].ToString());
					break;

				case CPUInstructionEnum.LODS:
					sbValue.AppendFormat("LODS{0}", GetSizeSuffix());
					break;

				case CPUInstructionEnum.LOOP:
					sbValue.AppendFormat("LOOP {0}", this.aParameters[0].ToString());
					break;

				case CPUInstructionEnum.LOOPE:
					sbValue.AppendFormat("LOOPZ {0}", this.aParameters[0].ToString());
					break;

				case CPUInstructionEnum.LOOPNE:
					sbValue.AppendFormat("LOOPNZ {0}", this.aParameters[0].ToString());
					break;

				case CPUInstructionEnum.LSL:
					sbValue.AppendFormat("LSL {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;

				case CPUInstructionEnum.LSS:
					sbValue.AppendFormat("LSS {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;

				case CPUInstructionEnum.LTR:
					sbValue.AppendFormat("LTR {0}", this.aParameters[0].ToString());
					break;

				case CPUInstructionEnum.MOV:
					sbValue.AppendFormat("MOV {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;

				case CPUInstructionEnum.MOVS:
					sbValue.AppendFormat("MOVS{0}", GetSizeSuffix());
					break;

				case CPUInstructionEnum.MOVSX:
					sbValue.AppendFormat("MOVSX {0}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;

				case CPUInstructionEnum.MOVZX:
					sbValue.AppendFormat("MOVZX {0}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;

				case CPUInstructionEnum.MUL:
					sbValue.Append("MUL ");
					for (int i = 0; i < this.aParameters.Count; i++)
					{
						if (i > 0)
							sbValue.Append(", ");
						sbValue.Append(this.aParameters[i].ToString());
					}
					break;

				case CPUInstructionEnum.NEG:
					sbValue.AppendFormat("NEG {0}", this.aParameters[0].ToString());
					break;

				case CPUInstructionEnum.NOP:
					sbValue.Append("NOP");
					break;

				case CPUInstructionEnum.NOT:
					sbValue.AppendFormat("NOT {0}", this.aParameters[0].ToString());
					break;

				case CPUInstructionEnum.OR:
					sbValue.AppendFormat("OR {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;

				case CPUInstructionEnum.OUT:
					sbValue.AppendFormat("OUT {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;

				case CPUInstructionEnum.OUTS:
					sbValue.AppendFormat("OUTS{0}", GetSizeSuffix());
					break;

				case CPUInstructionEnum.POP:
					sbValue.AppendFormat("POP {0}", this.aParameters[0].ToString());
					break;

				case CPUInstructionEnum.POPA:
					sbValue.Append("POPA");
					break;

				case CPUInstructionEnum.POPF:
					sbValue.Append("POPF");
					break;

				case CPUInstructionEnum.PUSH:
					sbValue.AppendFormat("PUSH {0}", this.aParameters[0].ToString());
					break;

				case CPUInstructionEnum.PUSHA:
					sbValue.Append("PUSHA");
					break;

				case CPUInstructionEnum.PUSHF:
					sbValue.Append("PUSHF");
					break;

				case CPUInstructionEnum.RCL:
					sbValue.AppendFormat("RCL {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;

				case CPUInstructionEnum.RCR:
					sbValue.AppendFormat("RCR {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;

				case CPUInstructionEnum.RET:
					sbValue.Append("RET");
					if (this.aParameters.Count > 0)
					{
						sbValue.AppendFormat(" {0}", this.aParameters[0].ToString());
					}
					break;

				case CPUInstructionEnum.RETF:
					sbValue.Append("RETF");
					if (this.aParameters.Count > 0)
					{
						sbValue.AppendFormat(" {0}", this.aParameters[0].ToString());
					}
					break;

				case CPUInstructionEnum.ROL:
					sbValue.AppendFormat("ROL {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;

				case CPUInstructionEnum.ROR:
					sbValue.AppendFormat("ROR {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;

				case CPUInstructionEnum.SAHF:
					sbValue.Append("SAHF");
					break;

				case CPUInstructionEnum.SAR:
					sbValue.AppendFormat("SAR {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;

				case CPUInstructionEnum.SBB:
					sbValue.AppendFormat("SBB {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;

				case CPUInstructionEnum.SCAS:
					sbValue.AppendFormat("SCAS{0}", GetSizeSuffix());
					break;

				case CPUInstructionEnum.SETcc:
					sbValue.AppendFormat("SET{0} {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;

				case CPUInstructionEnum.SGDT:
					sbValue.AppendFormat("SGDT {0}", this.aParameters[0].ToString());
					break;

				case CPUInstructionEnum.SHL:
					sbValue.AppendFormat("SHL {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;

				case CPUInstructionEnum.SHLD:
					sbValue.AppendFormat("SHLD {0}, {1}, {2}", this.aParameters[1].ToString(), this.aParameters[0].ToString(),
						this.aParameters[2].ToString());
					break;

				case CPUInstructionEnum.SHR:
					sbValue.AppendFormat("SHR {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;

				case CPUInstructionEnum.SHRD:
					sbValue.AppendFormat("SHRD {0}, {1}, {2}", this.aParameters[1].ToString(), this.aParameters[0].ToString(),
						this.aParameters[2].ToString());
					break;

				case CPUInstructionEnum.SIDT:
					sbValue.AppendFormat("SIDT {0}", this.aParameters[0].ToString());
					break;

				case CPUInstructionEnum.SLDT:
					sbValue.AppendFormat("SLDT {0}", this.aParameters[0].ToString());
					break;

				case CPUInstructionEnum.SMSW:
					sbValue.AppendFormat("SMSW {0}", this.aParameters[0].ToString());
					break;

				case CPUInstructionEnum.STC:
					sbValue.Append("STC");
					break;

				case CPUInstructionEnum.STD:
					sbValue.Append("STD");
					break;

				case CPUInstructionEnum.STI:
					sbValue.Append("STI");
					break;

				case CPUInstructionEnum.STOS:
					sbValue.AppendFormat("STOS{0}", GetSizeSuffix());
					break;

				case CPUInstructionEnum.STR:
					sbValue.AppendFormat("STR {0}", this.aParameters[0].ToString());
					break;

				case CPUInstructionEnum.SUB:
					sbValue.AppendFormat("SUB {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;

				case CPUInstructionEnum.TEST:
					sbValue.AppendFormat("TEST {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;

				case CPUInstructionEnum.VERR:
					sbValue.AppendFormat("VERR {0}", this.aParameters[0].ToString());
					break;

				case CPUInstructionEnum.VERW:
					sbValue.AppendFormat("VERW {0}", this.aParameters[0].ToString());
					break;

				case CPUInstructionEnum.WAIT:
					sbValue.Append("WAIT");
					break;

				case CPUInstructionEnum.XCHG:
					sbValue.AppendFormat("XCHG {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;

				case CPUInstructionEnum.XLAT:
					sbValue.Append("XLATB");
					break;

				case CPUInstructionEnum.XOR:
					sbValue.AppendFormat("XOR {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;

				case CPUInstructionEnum.SWITCH:
					sbValue.AppendFormat("switch({0})", this.aParameters[0].ToString());
					break;

				case CPUInstructionEnum.WordsToDword:
					sbValue.AppendFormat("ToDword({0}, {1})", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;

				case CPUInstructionEnum.CallOverlay:
					sbValue.AppendFormat("CALLOverlay {0}:{1}(...)", this.aParameters[0].Value, this.aParameters[1].Value);
					break;

				default:
					sbValue.Append("[Unknown instruction]");
					break;
			}

			return sbValue.ToString();
		}

		private string GetSizeSuffix()
		{
			switch (this.eOperandSize)
			{
				case CPUParameterSizeEnum.UInt8:
					return "B";
				case CPUParameterSizeEnum.UInt16:
					return "W";
				case CPUParameterSizeEnum.UInt32:
					return "D";
				default:
					return "";
			}
		}

		public static int CompareInstructionByAddress(CPUInstruction i1, CPUInstruction i2)
		{
			return i1.LinearAddress.CompareTo(i2.LinearAddress);
		}
	}
}
