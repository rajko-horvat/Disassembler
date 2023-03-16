using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Disassembler
{
	public partial class Instruction
	{
		// location of a instruction
		private MemoryLocation oLocation = new MemoryLocation(0);

		private CPUEnum eCPUType = CPUEnum.Undefined;
		private InstructionEnum eInstruction = InstructionEnum.Undefined;
		private InstructionSizeEnum eDefaultSize = InstructionSizeEnum.Word;
		private InstructionSizeEnum eOperandSize = InstructionSizeEnum.Word;
		private InstructionSizeEnum eAddressSize = InstructionSizeEnum.Word;
		private SegmentRegisterEnum eDefaultDataSegment = SegmentRegisterEnum.Undefined;

		private string sDescription = "";
		private List<InstructionParameter> aParameters = new List<InstructionParameter>();
		private bool bInvalid = false;
		private List<byte> aBytes = new List<byte>();
		// instruction bytes (not including the prefixes)
		private int iByte0 = -1;
		private int iByte1 = -1;
		private int iByte2 = -1;

		private bool bFPUDestination0 = false;

		// this instruction location is referenced by jump
		private bool bLabel = false;

		// prefixes
		private bool bLockPrefix = false;
		private InstructionPrefixEnum eRepPrefix = InstructionPrefixEnum.Undefined;

		// flags
		private FlagsEnum eClearedFlags = FlagsEnum.Undefined;
		private FlagsEnum eSetFlags = FlagsEnum.Undefined;
		private FlagsEnum eModifiedFlags = FlagsEnum.Undefined;
		private FlagsEnum eUndefinedFlags = FlagsEnum.Undefined;

		public Instruction(uint segment, uint offset, MemoryStream stream)
		{
			this.oLocation = new MemoryLocation(segment, offset);

			Decode(stream);

			// check for REP prefix validity
			if (this.eRepPrefix == InstructionPrefixEnum.REPE)
			{
				switch (this.eInstruction)
				{
					// REP/REPE is valid only for these instructions
					case InstructionEnum.CMPS:
					case InstructionEnum.LODS:
					case InstructionEnum.MOVS:
					case InstructionEnum.SCAS:
					case InstructionEnum.STOS:
					case InstructionEnum.INS:
					case InstructionEnum.OUTS:
						break;
					default:
						this.bInvalid = true;
						break;
				}
			}
			else if (this.eRepPrefix == InstructionPrefixEnum.REPNE)
			{
				switch (this.eInstruction)
				{
					// REPNE is valid only for these instructions
					case InstructionEnum.CMPS:
					case InstructionEnum.SCAS:
						break;
					default:
						this.bInvalid = true;
						break;
				}
			}
		}

		public MemoryLocation Location
		{
			get
			{
				return this.oLocation;
			}
		}

		public CPUEnum CPUType
		{
			get
			{
				return this.eCPUType;
			}
		}

		public InstructionEnum InstructionType
		{
			get { return this.eInstruction; }
			set { this.eInstruction = value; }
		}

		public InstructionSizeEnum DefaultSize
		{
			get
			{
				return this.eDefaultSize;
			}
		}

		public SegmentRegisterEnum DefaultDataSegment
		{
			get { return this.eDefaultDataSegment; }
		}

		public string GetDefaultDataSegmentText()
		{
			switch (this.eDefaultDataSegment)
			{
				case SegmentRegisterEnum.Immediate:
					return "--";
				case SegmentRegisterEnum.ES:
				case SegmentRegisterEnum.DS:
					return string.Format("r{0}", this.eDefaultDataSegment.ToString());
				case SegmentRegisterEnum.Undefined:
					return string.Format("rDS");
				default:
					return string.Format("this.oParent.CPU.{0}", this.eDefaultDataSegment.ToString());
			}
		}

		public InstructionSizeEnum OperandSize
		{
			get { return this.eOperandSize; }
			set { this.eOperandSize = value; }
		}

		public InstructionSizeEnum AddressSize
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

		public List<InstructionParameter> Parameters
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

		// prefixes
		public bool LockPrefix
		{
			get
			{
				return this.bLockPrefix;
			}
		}

		public InstructionPrefixEnum RepPrefix
		{
			get
			{
				return this.eRepPrefix;
			}
		}

		// flags
		public FlagsEnum ClearedFlags
		{
			get
			{
				return this.eClearedFlags;
			}
		}

		public FlagsEnum SetFlags
		{
			get
			{
				return this.eSetFlags;
			}
		}

		public FlagsEnum ModifiedFlags
		{
			get
			{
				return this.eModifiedFlags;
			}
		}

		public FlagsEnum UndefinedFlags
		{
			get
			{
				return this.eUndefinedFlags;
			}
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

		private uint ReadImmediate(MemoryStream stream, InstructionSizeEnum size)
		{
			switch (size)
			{
				case InstructionSizeEnum.Byte:
					return ReadByte(stream);
				case InstructionSizeEnum.Word:
					return ReadWord(stream);
				case InstructionSizeEnum.DWord:
					return ReadDWord(stream);
				default:
					return 0;
			}
		}

		private uint SignExtend(uint value, InstructionSizeEnum from, InstructionSizeEnum to)
		{
			switch (from)
			{
				case InstructionSizeEnum.Byte:
					switch (to)
					{
						case InstructionSizeEnum.Word:
							return (value & 0x80) != 0 ? value | 0xff00 : value;
						case InstructionSizeEnum.DWord:
							return (value & 0x80) != 0 ? value | 0xffffff00 : value;
						default:
							return value;
					}
				case InstructionSizeEnum.Word:
					switch (to)
					{
						case InstructionSizeEnum.Word:
							return value;
						case InstructionSizeEnum.DWord:
							return (value & 0x8000) != 0 ? value | 0xffff0000 : value;
						default:
							throw new Exception(string.Format("Can't convert from {0} to {1}", from.ToString(), to.ToString()));
					}
				default:
					throw new Exception(string.Format("Can't convert from {0} to {1}", from.ToString(), to.ToString()));
			}
		}

		private InstructionParameter RegisterOrMemoryAddressing(MemoryStream stream, SegmentRegisterEnum defaultSegment,
			InstructionSizeEnum operandSize, InstructionSizeEnum addressSize, int value)
		{
			InstructionParameter oParameter = null;
			uint mod = (uint)((value & 0xc0) >> 6);
			uint rm = (uint)(value & 0x7);

			if (addressSize != InstructionSizeEnum.Word)
			{
				Console.WriteLine("x32 addressing mode not yet implemented");
				//throw new Exception("x32 addressing mode not yet implemented");
				return new InstructionParameter(InstructionParameterTypeEnum.Undefined, 0);
			}

			switch (mod)
			{
				case 0:
					// direct displacement
					if (rm == 6)
					{
						oParameter = new InstructionParameter(InstructionParameterTypeEnum.MemoryAddress, addressSize,
							defaultSegment, mod << 3 | rm, ReadWord(stream));
					}
					else
					{
						oParameter = new InstructionParameter(InstructionParameterTypeEnum.MemoryAddress, addressSize,
							defaultSegment, mod << 3 | rm);
					}
					break;
				case 1:
					oParameter = new InstructionParameter(InstructionParameterTypeEnum.MemoryAddress, addressSize,
						defaultSegment, mod << 3 | rm, ReadByte(stream));
					break;
				case 2:
					oParameter = new InstructionParameter(InstructionParameterTypeEnum.MemoryAddress, addressSize,
						defaultSegment, mod << 3 | rm, ReadWord(stream));
					break;
				case 3:
					oParameter = new InstructionParameter(InstructionParameterTypeEnum.Register, operandSize, rm);
					break;
			}

			return oParameter;
		}

		private InstructionParameter MemoryAddressing(MemoryStream stream, SegmentRegisterEnum defaultSegment,
			InstructionSizeEnum operandSize, InstructionSizeEnum addressSize, int value)
		{
			InstructionParameter oParameter = null;
			uint mod = (uint)((value & 0xc0) >> 6);
			uint rm = (uint)(value & 0x7);

			if (addressSize != InstructionSizeEnum.Word)
				throw new Exception("x32 addressing mode not yet implemented");

			switch (mod)
			{
				case 0:
					// direct displacement
					if (rm == 6)
					{
						oParameter = new InstructionParameter(InstructionParameterTypeEnum.MemoryAddress, addressSize,
						defaultSegment, mod << 3 | rm, ReadWord(stream));
					}
					else
					{
						oParameter = new InstructionParameter(InstructionParameterTypeEnum.MemoryAddress, addressSize,
							defaultSegment, mod << 3 | rm);
					}
					break;
				case 1:
					oParameter = new InstructionParameter(InstructionParameterTypeEnum.MemoryAddress, addressSize,
						defaultSegment, mod << 3 | rm, ReadByte(stream));
					break;
				case 2:
					oParameter = new InstructionParameter(InstructionParameterTypeEnum.MemoryAddress, addressSize,
						defaultSegment, mod << 3 | rm, ReadWord(stream));
					break;
				case 3:
					this.bInvalid = true;
					oParameter = new InstructionParameter(InstructionParameterTypeEnum.Register, operandSize, rm);
					break;
			}

			return oParameter;
		}

		private InstructionParameter MemoryImmediate(MemoryStream stream, SegmentRegisterEnum defaultSegment, InstructionSizeEnum addressSize)
		{
			InstructionParameter oParameter = new InstructionParameter(InstructionParameterTypeEnum.MemoryAddress, addressSize,
				defaultSegment, 6, ReadImmediate(stream, addressSize));

			return oParameter;
		}

		private InstructionParameter ReadImmediate(MemoryStream stream, int size, InstructionSizeEnum operandSize, bool signExtend)
		{
			InstructionParameter oParameter = null;

			if (size == 0)
			{
				// determine size by operand size prefix
				switch (operandSize)
				{
					case InstructionSizeEnum.Byte:
						// read byte
						oParameter = new InstructionParameter(InstructionParameterTypeEnum.Immediate, operandSize,
							ReadByte(stream));
						break;
					case InstructionSizeEnum.Word:
						// read word
						if (signExtend)
						{
							oParameter = new InstructionParameter(InstructionParameterTypeEnum.Immediate, operandSize,
								SignExtend(ReadByte(stream), InstructionSizeEnum.Byte, InstructionSizeEnum.Word));
						}
						else
						{
							oParameter = new InstructionParameter(InstructionParameterTypeEnum.Immediate, operandSize,
								ReadWord(stream));
						}
						break;
					case InstructionSizeEnum.DWord:
						// read dword
						if (signExtend)
						{
							oParameter = new InstructionParameter(InstructionParameterTypeEnum.Immediate, operandSize,
								SignExtend(ReadByte(stream), InstructionSizeEnum.Byte, InstructionSizeEnum.DWord));
						}
						else
						{
							oParameter = new InstructionParameter(InstructionParameterTypeEnum.Immediate, operandSize,
								ReadDWord(stream));
						}
						break;
				}
			}
			else
			{
				switch (size)
				{
					case 1:
						// read byte
						oParameter = new InstructionParameter(InstructionParameterTypeEnum.Immediate,
							InstructionSizeEnum.Byte, ReadByte(stream));
						break;
					case 2:
						// read word
						oParameter = new InstructionParameter(InstructionParameterTypeEnum.Immediate,
							InstructionSizeEnum.Word, ReadWord(stream));
						break;
					case 4:
						// read dword
						oParameter = new InstructionParameter(InstructionParameterTypeEnum.Immediate,
							InstructionSizeEnum.DWord, ReadDWord(stream));
						break;
					default:
						throw new Exception("Unknown immediate size");
				}
			}

			return oParameter;
		}

		private InstructionParameter ReadRelative(MemoryStream stream, int size, InstructionSizeEnum operandSize)
		{
			InstructionParameter oParameter = null;

			if (size == 0)
			{
				throw new Exception("Invalid signed immediate size");
			}
			else
			{
				switch (size)
				{
					case 1:
						// read byte
						oParameter = new InstructionParameter(InstructionParameterTypeEnum.Relative, InstructionSizeEnum.Byte,
							SignExtend(ReadByte(stream), InstructionSizeEnum.Byte, InstructionSizeEnum.Word));
						break;
					case 2:
						// read word
						oParameter = new InstructionParameter(InstructionParameterTypeEnum.Relative, operandSize,
							SignExtend(ReadImmediate(stream, operandSize), operandSize, InstructionSizeEnum.Word));
						break;
					default:
						throw new Exception("Undefined signed immediate size");
				}
			}

			return oParameter;
		}

		private InstructionParameter ReadSegmentOffset(MemoryStream stream, InstructionSizeEnum operandSize)
		{
			ushort usOffset = ReadWord(stream);
			ushort usSegment = ReadWord(stream);
			return new InstructionParameter(usSegment, usOffset);
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
				case InstructionPrefixEnum.REPE:
					sbValue.Append("REPE ");
					break;
				case InstructionPrefixEnum.REPNE:
					sbValue.Append("REPNE ");
					break;
				default:
					break;
			}

			InstructionParameter oAcc = new InstructionParameter(InstructionParameterTypeEnum.Register, this.eOperandSize, 0);

			switch (this.eInstruction)
			{
				case InstructionEnum.AAA:
					sbValue.Append("AAA");
					break;
				case InstructionEnum.AAD:
					sbValue.Append("AAD");
					break;
				case InstructionEnum.AAM:
					sbValue.Append("AAM");
					break;
				case InstructionEnum.AAS:
					sbValue.Append("AAS");
					break;
				case InstructionEnum.ADC:
					sbValue.AppendFormat("ADC {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;
				case InstructionEnum.ADD:
					sbValue.AppendFormat("ADD {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;
				case InstructionEnum.AND:
					sbValue.AppendFormat("AND {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;
				case InstructionEnum.ARPL:
					sbValue.AppendFormat("ARPL {0}, {1}", this.aParameters[1].ToString(), this.aParameters[0].ToString());
					break;
				case InstructionEnum.BOUND:
					sbValue.AppendFormat("BOUND {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;
				case InstructionEnum.BSF:
					sbValue.AppendFormat("BSF {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;
				case InstructionEnum.BSR:
					sbValue.AppendFormat("BSR {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;
				case InstructionEnum.BSWAP:
					sbValue.AppendFormat("BSWAP {0}", this.aParameters[0].ToString());
					break;
				case InstructionEnum.BT:
					sbValue.AppendFormat("BT {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;
				case InstructionEnum.BTC:
					sbValue.AppendFormat("BTC {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;
				case InstructionEnum.BTR:
					sbValue.AppendFormat("BTR {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;
				case InstructionEnum.BTS:
					sbValue.AppendFormat("BTS {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;
				case InstructionEnum.CALL:
					sbValue.AppendFormat("CALL {0}", this.aParameters[0].ToString());
					break;
				case InstructionEnum.CALLF:
					sbValue.AppendFormat("CALL far {0}", this.aParameters[0].ToString());
					break;
				case InstructionEnum.CBW:
					if (this.eOperandSize == InstructionSizeEnum.Word)
						sbValue.Append("CBW");
					else
						sbValue.Append("CWDE");
					break;
				case InstructionEnum.CLC:
					sbValue.Append("CLC");
					break;
				case InstructionEnum.CLD:
					sbValue.Append("CLD");
					break;
				case InstructionEnum.CLI:
					sbValue.Append("CLI");
					break;
				case InstructionEnum.CLTS:
					sbValue.Append("CLTS");
					break;
				case InstructionEnum.CMC:
					sbValue.Append("CMC");
					break;
				case InstructionEnum.CMP:
					sbValue.AppendFormat("CMP {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;
				case InstructionEnum.CMPS:
					sbValue.AppendFormat("CMPS{0}", GetSizeSuffix());
					break;
				case InstructionEnum.CWD:
					if (this.eOperandSize == InstructionSizeEnum.Word)
						sbValue.Append("CWD");
					else
						sbValue.Append("CDQ");
					break;
				case InstructionEnum.DAA:
					sbValue.Append("DAA");
					break;
				case InstructionEnum.DAS:
					sbValue.Append("DAS");
					break;
				case InstructionEnum.DEC:
					sbValue.AppendFormat("DEC {0}", this.aParameters[0].ToString());
					break;
				case InstructionEnum.DIV:
					sbValue.AppendFormat("DIV {0}, {1}", oAcc.ToString(), this.aParameters[0].ToString());
					break;
				case InstructionEnum.ENTER:
					sbValue.AppendFormat("ENTER {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;
				case InstructionEnum.F2XM1:
				case InstructionEnum.FABS:
				case InstructionEnum.FADD:
				case InstructionEnum.FADDP:
				case InstructionEnum.FBLD:
				case InstructionEnum.FBSTP:
				case InstructionEnum.FCHS:
				case InstructionEnum.FCLEX:
				case InstructionEnum.FCOM:
				case InstructionEnum.FCOMP:
				case InstructionEnum.FCOMPP:
				case InstructionEnum.FCOS:
				case InstructionEnum.FDECSTP:
				case InstructionEnum.FDIV:
				case InstructionEnum.FDIVP:
				case InstructionEnum.FDIVR:
				case InstructionEnum.FDIVRP:
				case InstructionEnum.FFREE:
				case InstructionEnum.FIADD:
				case InstructionEnum.FICOM:
				case InstructionEnum.FICOMP:
				case InstructionEnum.FIDIV:
				case InstructionEnum.FIDIVR:
				case InstructionEnum.FILD:
				case InstructionEnum.FIMUL:
				case InstructionEnum.FINCSTP:
				case InstructionEnum.FINIT:
				case InstructionEnum.FIST:
				case InstructionEnum.FISTP:
				case InstructionEnum.FISUB:
				case InstructionEnum.FISUBR:
				case InstructionEnum.FLD:
				case InstructionEnum.FLD1:
				case InstructionEnum.FLDCW:
				case InstructionEnum.FLDENV:
				case InstructionEnum.FLDL2E:
				case InstructionEnum.FLDL2T:
				case InstructionEnum.FLDLG2:
				case InstructionEnum.FLDLN2:
				case InstructionEnum.FLDPI:
				case InstructionEnum.FLDZ:
				case InstructionEnum.FMUL:
				case InstructionEnum.FMULP:
				case InstructionEnum.FNOP:
				case InstructionEnum.FPATAN:
				case InstructionEnum.FPREM:
				case InstructionEnum.FPREM1:
				case InstructionEnum.FPTAN:
				case InstructionEnum.FRNDINT:
				case InstructionEnum.FRSTOR:
				case InstructionEnum.FSAVE:
				case InstructionEnum.FSCALE:
				case InstructionEnum.FSIN:
				case InstructionEnum.FSINCOS:
				case InstructionEnum.FSQRT:
				case InstructionEnum.FST:
				case InstructionEnum.FSTCW:
				case InstructionEnum.FSTENV:
				case InstructionEnum.FSTP:
				case InstructionEnum.FSTSW:
				case InstructionEnum.FSUB:
				case InstructionEnum.FSUBP:
				case InstructionEnum.FSUBR:
				case InstructionEnum.FSUBRP:
				case InstructionEnum.FTST:
				case InstructionEnum.FUCOM:
				case InstructionEnum.FUCOMP:
				case InstructionEnum.FUCOMPP:
				case InstructionEnum.FXAM:
				case InstructionEnum.FXCH:
				case InstructionEnum.FXTRACT:
				case InstructionEnum.FYL2X:
				case InstructionEnum.FYL2XP1:
					sbValue.Append(Enum.GetName(typeof(InstructionEnum), this.eInstruction));
					break;
				case InstructionEnum.HLT:
					sbValue.Append("HLT");
					break;
				case InstructionEnum.IDIV:
					sbValue.Append("IDIV ");
					if (this.eOperandSize == InstructionSizeEnum.Byte)
						sbValue.AppendFormat("{0}", this.aParameters[0].ToString());
					else
						sbValue.AppendFormat("{0}, {1}", oAcc.ToString(), this.aParameters[0].ToString());
					break;
				case InstructionEnum.IMUL:
					sbValue.Append("IMUL ");
					for (int i = 0; i < this.aParameters.Count; i++)
					{
						if (i > 0)
							sbValue.Append(", ");
						sbValue.Append(this.aParameters[i].ToString());
					}
					break;
				case InstructionEnum.IN:
					sbValue.AppendFormat("IN {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;
				case InstructionEnum.INC:
					sbValue.AppendFormat("INC {0}", this.aParameters[0].ToString());
					break;
				case InstructionEnum.INS:
					sbValue.AppendFormat("INS{0}", GetSizeSuffix());
					break;
				case InstructionEnum.INT:
					sbValue.AppendFormat("INT {0}", this.aParameters[0].ToString());
					break;
				case InstructionEnum.INTO:
					sbValue.Append("INTO");
					break;
				case InstructionEnum.IRET:
					sbValue.Append("IRET");
					break;
				case InstructionEnum.Jcc:
					sbValue.AppendFormat("J{0} {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;
				case InstructionEnum.JCXZ:
					sbValue.AppendFormat("JCXZ {0}", this.aParameters[0].ToString());
					break;
				case InstructionEnum.JMP:
					sbValue.AppendFormat("JMP {0}", this.aParameters[0].ToString());
					break;
				case InstructionEnum.JMPF:
					sbValue.AppendFormat("JMP far {0}", this.aParameters[0].ToString());
					break;
				case InstructionEnum.LAHF:
					sbValue.Append("LAHF");
					break;
				case InstructionEnum.LAR:
					sbValue.AppendFormat("LAR {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;
				case InstructionEnum.LDS:
					sbValue.AppendFormat("LDS {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;
				case InstructionEnum.LEA:
					sbValue.AppendFormat("LEA {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;
				case InstructionEnum.LEAVE:
					sbValue.Append("LEAVE");
					break;
				case InstructionEnum.LES:
					sbValue.AppendFormat("LES {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;
				case InstructionEnum.LFS:
					sbValue.AppendFormat("LFS {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;
				case InstructionEnum.LGDT:
					sbValue.AppendFormat("LGDT {0}", this.aParameters[0].ToString());
					break;
				case InstructionEnum.LGS:
					sbValue.AppendFormat("LGS {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;
				case InstructionEnum.LIDT:
					sbValue.AppendFormat("LIDT {0}", this.aParameters[0].ToString());
					break;
				case InstructionEnum.LLDT:
					sbValue.AppendFormat("LLDT {0}", this.aParameters[0].ToString());
					break;
				case InstructionEnum.LMSW:
					sbValue.AppendFormat("LMSW {0}", this.aParameters[0].ToString());
					break;
				case InstructionEnum.LODS:
					sbValue.AppendFormat("LODS{0}", GetSizeSuffix());
					break;
				case InstructionEnum.LOOP:
					sbValue.AppendFormat("LOOP {0}", this.aParameters[0].ToString());
					break;
				case InstructionEnum.LOOPZ:
					sbValue.AppendFormat("LOOPZ {0}", this.aParameters[0].ToString());
					break;
				case InstructionEnum.LOOPNZ:
					sbValue.AppendFormat("LOOPNZ {0}", this.aParameters[0].ToString());
					break;
				case InstructionEnum.LSL:
					sbValue.AppendFormat("LSL {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;
				case InstructionEnum.LSS:
					sbValue.AppendFormat("LSS {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;
				case InstructionEnum.LTR:
					sbValue.AppendFormat("LTR {0}", this.aParameters[0].ToString());
					break;
				case InstructionEnum.MOV:
					sbValue.AppendFormat("MOV {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;
				case InstructionEnum.MOVS:
					sbValue.AppendFormat("MOVS{0}", GetSizeSuffix());
					break;
				case InstructionEnum.MOVSX:
					sbValue.AppendFormat("MOVSX {0}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;
				case InstructionEnum.MOVZX:
					sbValue.AppendFormat("MOVZX {0}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;
				case InstructionEnum.MUL:
					sbValue.AppendFormat("MUL {0}, {1}", oAcc.ToString(), this.aParameters[0].ToString());
					break;
				case InstructionEnum.NEG:
					sbValue.AppendFormat("NEG {0}", this.aParameters[0].ToString());
					break;
				case InstructionEnum.NOP:
					sbValue.Append("NOP");
					break;
				case InstructionEnum.NOT:
					sbValue.AppendFormat("NOT {0}", this.aParameters[0].ToString());
					break;
				case InstructionEnum.OR:
					sbValue.AppendFormat("OR {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;
				case InstructionEnum.OUT:
					sbValue.AppendFormat("OUT {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;
				case InstructionEnum.OUTS:
					sbValue.AppendFormat("OUTS{0}", GetSizeSuffix());
					break;
				case InstructionEnum.POP:
					sbValue.AppendFormat("POP {0}", this.aParameters[0].ToString());
					break;
				case InstructionEnum.POPA:
					sbValue.Append("POPA");
					break;
				case InstructionEnum.POPF:
					sbValue.Append("POPF");
					break;
				case InstructionEnum.PUSH:
					sbValue.AppendFormat("PUSH {0}", this.aParameters[0].ToString());
					break;
				case InstructionEnum.PUSHA:
					sbValue.Append("PUSHA");
					break;
				case InstructionEnum.PUSHF:
					sbValue.Append("PUSHF");
					break;
				case InstructionEnum.RCL:
					sbValue.AppendFormat("RCL {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;
				case InstructionEnum.RCR:
					sbValue.AppendFormat("RCR {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;
				case InstructionEnum.RET:
					sbValue.Append("RET");
					if (this.aParameters.Count > 0)
					{
						sbValue.AppendFormat(" {0}", this.aParameters[0].ToString());
					}
					break;
				case InstructionEnum.RETF:
					sbValue.Append("RETF");
					if (this.aParameters.Count > 0)
					{
						sbValue.AppendFormat(" {0}", this.aParameters[0].ToString());
					}
					break;
				case InstructionEnum.ROL:
					sbValue.AppendFormat("ROL {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;
				case InstructionEnum.ROR:
					sbValue.AppendFormat("ROR {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;
				case InstructionEnum.SAHF:
					sbValue.Append("SAHF");
					break;
				case InstructionEnum.SAR:
					sbValue.AppendFormat("SAR {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;
				case InstructionEnum.SBB:
					sbValue.AppendFormat("SBB {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;
				case InstructionEnum.SCAS:
					sbValue.AppendFormat("SCAS{0}", GetSizeSuffix());
					break;
				case InstructionEnum.SETcc:
					sbValue.AppendFormat("SET{0} {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;
				case InstructionEnum.SGDT:
					sbValue.AppendFormat("SGDT {0}", this.aParameters[0].ToString());
					break;
				case InstructionEnum.SHL:
					sbValue.AppendFormat("SHL {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;
				case InstructionEnum.SHLD:
					sbValue.AppendFormat("SHLD {0}, {1}, {2}", this.aParameters[1].ToString(), this.aParameters[0].ToString(),
						this.aParameters[2].ToString());
					break;
				case InstructionEnum.SHR:
					sbValue.AppendFormat("SHR {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;
				case InstructionEnum.SHRD:
					sbValue.AppendFormat("SHRD {0}, {1}, {2}", this.aParameters[1].ToString(), this.aParameters[0].ToString(),
						this.aParameters[2].ToString());
					break;
				case InstructionEnum.SIDT:
					sbValue.AppendFormat("SIDT {0}", this.aParameters[0].ToString());
					break;
				case InstructionEnum.SLDT:
					sbValue.AppendFormat("SLDT {0}", this.aParameters[0].ToString());
					break;
				case InstructionEnum.SMSW:
					sbValue.AppendFormat("SMSW {0}", this.aParameters[0].ToString());
					break;
				case InstructionEnum.STC:
					sbValue.Append("STC");
					break;
				case InstructionEnum.STD:
					sbValue.Append("STD");
					break;
				case InstructionEnum.STI:
					sbValue.Append("STI");
					break;
				case InstructionEnum.STOS:
					sbValue.AppendFormat("STOS{0}", GetSizeSuffix());
					break;
				case InstructionEnum.STR:
					sbValue.AppendFormat("STR {0}", this.aParameters[0].ToString());
					break;
				case InstructionEnum.SUB:
					sbValue.AppendFormat("SUB {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;
				case InstructionEnum.TEST:
					sbValue.AppendFormat("TEST {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;
				case InstructionEnum.VERR:
					sbValue.AppendFormat("VERR {0}", this.aParameters[0].ToString());
					break;
				case InstructionEnum.VERW:
					sbValue.AppendFormat("VERW {0}", this.aParameters[0].ToString());
					break;
				case InstructionEnum.WAIT:
					sbValue.Append("WAIT");
					break;
				case InstructionEnum.XCHG:
					sbValue.AppendFormat("XCHG {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;
				case InstructionEnum.XLAT:
					sbValue.Append("XLATB");
					break;
				case InstructionEnum.XOR:
					sbValue.AppendFormat("XOR {0}, {1}", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;
				case InstructionEnum.SWITCH:
					sbValue.AppendFormat("switch({0})", this.aParameters[0].ToString());
					break;
				case InstructionEnum.WordsToDword:
					sbValue.AppendFormat("ToDword({0}, {1})", this.aParameters[0].ToString(), this.aParameters[1].ToString());
					break;
				case InstructionEnum.CallFunction:
					sbValue.AppendFormat("F{0}_{1:x}(...)", this.aParameters[0].SegmentAddress, this.aParameters[0].Value);
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
				case InstructionSizeEnum.Byte:
					return "B";
				case InstructionSizeEnum.Word:
					return "W";
				case InstructionSizeEnum.DWord:
					return "D";
				default:
					return "";
			}
		}

		public static int CompareInstructionByAddress(Instruction i1, Instruction i2)
		{
			return i1.Location.LinearAddress.CompareTo(i2.Location.LinearAddress);
		}
	}
}
