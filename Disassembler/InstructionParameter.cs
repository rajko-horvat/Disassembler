using System;
using System.IO;
using System.Text;

namespace Disassembler
{
	public enum InstructionParameterTypeEnum
	{
		Undefined,
		Immediate,
		Relative,
		SegmentOffset,
		Register,
		SegmentRegister,
		Condition,
		MemoryAddress,
		LEAMemoryAddress,
		FPUStackAddress
	}

	public enum InstructionParameterReferenceEnum
	{
		None,
		Segment,
		Offset
	}

	public class InstructionParameter
	{
		private InstructionParameterTypeEnum eType = InstructionParameterTypeEnum.Undefined;
		private InstructionParameterReferenceEnum eReferenceType = InstructionParameterReferenceEnum.None;
		// default size is word
		private InstructionSizeEnum eSize = InstructionSizeEnum.Word;
		// default segment which memory access uses
		private SegmentRegisterEnum eDefaultDataSegment = SegmentRegisterEnum.DS;
		private SegmentRegisterEnum eDataSegment = SegmentRegisterEnum.DS;
		private ushort usSegment = 0;
		private uint uiValue = 0;
		private uint uiDisplacement = 0;

		public InstructionParameter(InstructionParameterTypeEnum type, uint value)
		{
			this.eType = type;
			this.uiValue = value;
		}

		public InstructionParameter(ushort segment, uint offset)
		{
			this.eType = InstructionParameterTypeEnum.SegmentOffset;
			this.usSegment = segment;
			this.uiValue = offset;
		}

		public InstructionParameter(InstructionParameterTypeEnum type, InstructionSizeEnum size, uint value)
		{
			this.eType = type;
			this.eSize = size;
			this.uiValue = value;
		}

		public InstructionParameter(InstructionParameterTypeEnum type, InstructionSizeEnum size, SegmentRegisterEnum segmentRegister, uint value) :
			this(type, size, segmentRegister, value, 0)
		{
		}

		public InstructionParameter(InstructionParameterTypeEnum type, InstructionSizeEnum size, SegmentRegisterEnum segmentRegister, uint value,
			uint displacement)
		{
			this.eType = type;
			this.eSize = size;
			if (type == InstructionParameterTypeEnum.MemoryAddress)
			{
				if (value == 2 || value == 3 ||
					value == 0xa || value == 0xb || value == 0xe ||
					value == 0x12 || value == 0x13 || value == 0x16)
				{
					this.eDefaultDataSegment = this.eDataSegment = SegmentRegisterEnum.SS;
				}
			}
			if (type == InstructionParameterTypeEnum.LEAMemoryAddress)
			{
				if (value == 2 || value == 3 ||
					value == 0xa || value == 0xb || value == 0xe ||
					value == 0x12 || value == 0x13 || value == 0x16)
				{
					this.eDefaultDataSegment = this.eDataSegment = SegmentRegisterEnum.SS;
				}
			}
			if (segmentRegister != SegmentRegisterEnum.Undefined)
				this.eDataSegment = segmentRegister;

			this.uiValue = value;
			this.uiDisplacement = displacement;
		}

		public InstructionParameterTypeEnum Type
		{
			get { return this.eType; }
		}

		public InstructionParameterReferenceEnum ReferenceType
		{
			get { return this.eReferenceType; }
			set { this.eReferenceType = value; }
		}

		public InstructionSizeEnum Size
		{
			get
			{
				return this.eSize;
			}
			set
			{
				this.eSize = value;
			}
		}

		public SegmentRegisterEnum DefaultDataSegment
		{
			get
			{
				return this.eDefaultDataSegment;
			}
		}

		public SegmentRegisterEnum DataSegment
		{
			get { return this.eDataSegment; }
			set { this.eDataSegment = value; }
		}

		public ushort Segment
		{
			get { return this.usSegment; }
			set { this.usSegment = value; }
		}

		public uint Value
		{
			get { return this.uiValue; }
			set { this.uiValue = value; }
		}

		public uint Displacement
		{
			get { return this.uiDisplacement; }
			set { this.uiDisplacement = value; }
		}

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder();

			switch (this.eReferenceType)
			{
				case InstructionParameterReferenceEnum.Offset:
					sb.Append("offset ");
					break;
				case InstructionParameterReferenceEnum.Segment:
					sb.Append("segment ");
					break;
				default:
					break;
			}

			switch (this.eType)
			{
				case InstructionParameterTypeEnum.Immediate:
					sb.AppendFormat("0x{0:x}", this.uiValue);
					break;
				case InstructionParameterTypeEnum.Relative:
					sb.Append(RelativeToString(this.uiValue, this.eSize));
					break;
				case InstructionParameterTypeEnum.SegmentOffset:
					sb.AppendFormat("0x{0:x4}:0x{1:x4}", (ushort)this.usSegment, (ushort)this.uiValue);
					break;
				case InstructionParameterTypeEnum.Register:
					switch (this.eSize)
					{
						case InstructionSizeEnum.Byte:
							sb.Append(Enum.GetName(typeof(RegisterEnum), this.uiValue));
							break;
						case InstructionSizeEnum.Word:
							sb.Append(Enum.GetName(typeof(RegisterEnum), this.uiValue + 0x8));
							break;
						case InstructionSizeEnum.DWord:
							sb.Append("E" + Enum.GetName(typeof(RegisterEnum), this.uiValue + 0x8));
							break;
					}
					break;
				case InstructionParameterTypeEnum.SegmentRegister:
					sb.Append(((this.eSize == InstructionSizeEnum.DWord) ? "E" : "") + Enum.GetName(typeof(SegmentRegisterEnum), this.uiValue));
					break;
				case InstructionParameterTypeEnum.Condition:
					sb.Append(Enum.GetName(typeof(ConditionEnum), this.uiValue));
					break;
				case InstructionParameterTypeEnum.MemoryAddress:
					if (this.eSize == InstructionSizeEnum.DWord)
						throw new Exception("x32 addressing mode not yet implemented");
					if (this.eDefaultDataSegment != this.eDataSegment)
					{
						// print segment only if it's different from default segment
						if (this.eDataSegment == SegmentRegisterEnum.Immediate)
						{
							sb.AppendFormat("0x{0}:", this.usSegment);
						}
						else
						{
							sb.AppendFormat("{0}:", this.eDataSegment.ToString());
						}
					}
					switch (this.uiValue)
					{
						case 0:
							sb.Append("[BX + SI]");
							break;
						case 1:
							sb.Append("[BX + DI]");
							break;
						case 2:
							sb.Append("[BP + SI]");
							break;
						case 3:
							sb.Append("[BP + DI]");
							break;
						case 4:
							sb.Append("[SI]");
							break;
						case 5:
							sb.Append("[DI]");
							break;
						case 6:
							sb.AppendFormat("[0x{0:x}]", this.uiDisplacement);
							break;
						case 7:
							sb.Append("[BX]");
							break;

						case 8:
							sb.AppendFormat("[BX + SI {0}]", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Byte));
							break;
						case 9:
							sb.AppendFormat("[BX + DI {0}]", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Byte));
							break;
						case 10:
							sb.AppendFormat("[BP + SI {0}]", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Byte));
							break;
						case 11:
							sb.AppendFormat("[BP + DI {0}]", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Byte));
							break;
						case 12:
							sb.AppendFormat("[SI {0}]", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Byte));
							break;
						case 13:
							sb.AppendFormat("[DI {0}]", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Byte));
							break;
						case 14:
							sb.AppendFormat("[BP {0}]", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Byte));
							break;
						case 15:
							sb.AppendFormat("[BX {0}]", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Byte));
							break;

						case 16:
							sb.AppendFormat("[BX + SI {0}]", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Word));
							break;
						case 17:
							sb.AppendFormat("[BX + DI {0}]", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Word));
							break;
						case 18:
							sb.AppendFormat("[BP + SI {0}]", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Word));
							break;
						case 19:
							sb.AppendFormat("[BP + DI {0}]", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Word));
							break;
						case 20:
							sb.AppendFormat("[SI {0}]", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Word));
							break;
						case 21:
							sb.AppendFormat("[DI {0}]", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Word));
							break;
						case 22:
							sb.AppendFormat("[BP {0}]", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Word));
							break;
						case 23:
							sb.AppendFormat("[BX {0}]", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Word));
							break;
					}
					break;
				case InstructionParameterTypeEnum.LEAMemoryAddress:
					if (this.eSize == InstructionSizeEnum.DWord)
						throw new Exception("x32 addressing mode not yet implemented");
					/*if (this.eDefaultDataSegment != this.eDataSegment)
					{
						// print segment only if it's different from default segment
						if (this.eDataSegment == SegmentRegisterEnum.Immediate)
						{
							sb.AppendFormat("0x{0}:", this.uiSegmentAddress);
						}
						else
						{
							sb.AppendFormat("{0}:", this.eDataSegment.ToString());
						}
					}*/
					switch (this.uiValue)
					{
						case 0:
							sb.Append("(BX + SI)");
							break;
						case 1:
							sb.Append("(BX + DI)");
							break;
						case 2:
							sb.Append("(BP + SI)");
							break;
						case 3:
							sb.Append("(BP + DI)");
							break;
						case 4:
							sb.Append("(SI)");
							break;
						case 5:
							sb.Append("(DI)");
							break;
						case 6:
							sb.AppendFormat("(0x{0:x})", this.uiDisplacement);
							break;
						case 7:
							sb.Append("(BX)");
							break;

						case 8:
							sb.AppendFormat("(BX + SI {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Byte));
							break;
						case 9:
							sb.AppendFormat("(BX + DI {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Byte));
							break;
						case 10:
							sb.AppendFormat("(BP + SI {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Byte));
							break;
						case 11:
							sb.AppendFormat("(BP + DI {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Byte));
							break;
						case 12:
							sb.AppendFormat("(SI {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Byte));
							break;
						case 13:
							sb.AppendFormat("(DI {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Byte));
							break;
						case 14:
							sb.AppendFormat("(BP {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Byte));
							break;
						case 15:
							sb.AppendFormat("(BX {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Byte));
							break;

						case 16:
							sb.AppendFormat("(BX + SI {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Word));
							break;
						case 17:
							sb.AppendFormat("(BX + DI {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Word));
							break;
						case 18:
							sb.AppendFormat("(BP + SI {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Word));
							break;
						case 19:
							sb.AppendFormat("(BP + DI {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Word));
							break;
						case 20:
							sb.AppendFormat("(SI {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Word));
							break;
						case 21:
							sb.AppendFormat("(DI {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Word));
							break;
						case 22:
							sb.AppendFormat("(BP {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Word));
							break;
						case 23:
							sb.AppendFormat("(BX {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Word));
							break;
					}
					break;
				case InstructionParameterTypeEnum.FPUStackAddress:
					sb.AppendFormat("ST({0})", this.uiValue);
					break;
				default:
					sb.Append("--");
					break;
			}

			return sb.ToString();
		}

		public string ToSourceCSText(InstructionSizeEnum size)
		{
			if (this.Type == InstructionParameterTypeEnum.MemoryAddress)
			{
				return string.Format("this.oCPU.Read{0}({1}, {2})",
					size.ToString(), GetSegmentText(), ToCSText(size));
			}

			return ToCSText(size);
		}

		public string ToSourceCSTextMZ(InstructionSizeEnum size)
		{
			if (this.Type == InstructionParameterTypeEnum.MemoryAddress)
			{
				return string.Format("this.oCPU.Read{0}({1}, {2})",
					size.ToString(), GetSegmentTextMZ(), ToCSTextMZ(size));
			}

			return ToCSTextMZ(size);
		}

		public string ToDestinationCSText(InstructionSizeEnum size, string source)
		{
			if (this.Type == InstructionParameterTypeEnum.MemoryAddress)
			{
				return string.Format("this.oCPU.Write{0}({1}, {2}, {3});",
					size.ToString(), GetSegmentText(), ToCSText(size), source);
			}

			return string.Format("{0} = {1};", ToCSText(size), source);
		}

		public string ToDestinationCSTextMZ(InstructionSizeEnum size, string source)
		{
			if (this.Type == InstructionParameterTypeEnum.MemoryAddress)
			{
				return string.Format("this.oCPU.Write{0}({1}, {2}, {3});",
					size.ToString(), GetSegmentTextMZ(), ToCSTextMZ(size), source);
			}

			return string.Format("{0} = {1};", ToCSTextMZ(size), source);
		}

		public string GetSegmentText()
		{
			switch (this.eDataSegment)
			{
				case SegmentRegisterEnum.Immediate:
					return string.Format("0x{0:x}", this.usSegment);
				case SegmentRegisterEnum.ES:
				case SegmentRegisterEnum.DS:
					return string.Format("r{0}.Word", this.eDataSegment.ToString());
				default:
					return string.Format("r{0}.Word", this.eDataSegment.ToString());
			}
		}

		public string GetSegmentTextMZ()
		{
			switch (this.eDataSegment)
			{
				case SegmentRegisterEnum.Immediate:
					return string.Format("0x{0:x}", this.usSegment);
				case SegmentRegisterEnum.ES:
				case SegmentRegisterEnum.DS:
					return string.Format("this.oCPU.{0}.Word", this.eDataSegment.ToString());
				default:
					return string.Format("this.oCPU.{0}.Word", this.eDataSegment.ToString());
			}
		}

		public string ToCSText(InstructionSizeEnum size)
		{
			StringBuilder sb = new StringBuilder();

			RegisterEnum register;
			SegmentRegisterEnum segmentRegister;

			switch (this.eType)
			{
				case InstructionParameterTypeEnum.Immediate:
					sb.AppendFormat("0x{0:x}", this.uiValue);
					break;
				case InstructionParameterTypeEnum.Register:
					switch (size)
					{
						case InstructionSizeEnum.Byte:
							register = (RegisterEnum)this.uiValue;
							switch (register)
							{
								case RegisterEnum.AL:
									sb.Append("rAX.Low");
									break;
								case RegisterEnum.CL:
									sb.Append("rCX.Low");
									break;
								case RegisterEnum.DL:
									sb.Append("rDX.Low");
									break;
								case RegisterEnum.BL:
									sb.Append("rBX.Low");
									break;
								case RegisterEnum.AH:
									sb.Append("rAX.High");
									break;
								case RegisterEnum.CH:
									sb.Append("rCX.High");
									break;
								case RegisterEnum.DH:
									sb.Append("rDX.High");
									break;
								case RegisterEnum.BH:
									sb.Append("rBX.High");
									break;
								default:
									sb.Append("--");
									break;
							}
							break;
						case InstructionSizeEnum.Word:
							register = (RegisterEnum)(this.uiValue + 0x8);
							switch (register)
							{
								case RegisterEnum.AX:
									sb.Append("rAX.Word");
									break;
								case RegisterEnum.CX:
									sb.Append("rCX.Word");
									break;
								case RegisterEnum.DX:
									sb.Append("rDX.Word");
									break;
								case RegisterEnum.BX:
									sb.Append("rBX.Word");
									break;
								case RegisterEnum.SP:
									sb.Append("rSP.Word");
									break;
								case RegisterEnum.BP:
									sb.Append("rBP.Word");
									break;
								case RegisterEnum.SI:
									sb.Append("rSI.Word");
									break;
								case RegisterEnum.DI:
									sb.Append("rDI.Word");
									break;
								default:
									sb.Append("--");
									break;
							}
							break;
						case InstructionSizeEnum.DWord:
							register = (RegisterEnum)(this.uiValue + 0x8);
							switch (register)
							{
								case RegisterEnum.AX:
									sb.Append("rAX.DWord");
									break;
								case RegisterEnum.CX:
									sb.Append("rCX.DWord");
									break;
								case RegisterEnum.DX:
									sb.Append("rDX.DWord");
									break;
								case RegisterEnum.BX:
									sb.Append("rBX.DWord");
									break;
								case RegisterEnum.SP:
									sb.Append("rSP.DWord");
									break;
								case RegisterEnum.BP:
									sb.Append("rBP.DWord");
									break;
								case RegisterEnum.SI:
									sb.Append("rSI.DWord");
									break;
								case RegisterEnum.DI:
									sb.Append("rDI.DWord");
									break;
								default:
									sb.Append("--");
									break;
							}
							break;
						default:
							sb.Append("--");
							break;
					}
					break;
				case InstructionParameterTypeEnum.SegmentRegister:
					segmentRegister = (SegmentRegisterEnum)this.uiValue;
					switch (size)
					{
						case InstructionSizeEnum.Byte:
							sb.Append("--");
							break;
						case InstructionSizeEnum.Word:
							switch (segmentRegister)
							{
								case SegmentRegisterEnum.ES:
									sb.Append("rES.Word");
									break;
								case SegmentRegisterEnum.CS:
									sb.Append("this.oParent.CPU.CS.Word");
									break;
								case SegmentRegisterEnum.DS:
									sb.Append("rDS.Word");
									break;
								case SegmentRegisterEnum.SS:
									sb.Append("this.oParent.CPU.SS.Word");
									break;
								default:
									sb.Append("--");
									break;
							}
							break;
						case InstructionSizeEnum.DWord:
							switch (segmentRegister)
							{
								case SegmentRegisterEnum.ES:
									sb.Append("rES.DWord");
									break;
								case SegmentRegisterEnum.CS:
									sb.Append("rCS.DWord");
									break;
								case SegmentRegisterEnum.DS:
									sb.Append("rDS.DWord");
									break;
								case SegmentRegisterEnum.SS:
									sb.Append("rSS.DWord");
									break;
								default:
									sb.Append("--");
									break;
							}
							break;
					}
					break;
				case InstructionParameterTypeEnum.MemoryAddress:
				case InstructionParameterTypeEnum.LEAMemoryAddress:
					if (this.eSize == InstructionSizeEnum.DWord)
						throw new Exception("x32 addressing mode not yet implemented");

					switch (this.uiValue)
					{
						case 0:
							sb.Append("(ushort)(rBX.Word + rSI.Word)");
							break;
						case 1:
							sb.Append("(ushort)(rBX.Word + rDI.Word)");
							break;
						case 2:
							sb.Append("(ushort)(rBP.Word + rSI.Word)");
							break;
						case 3:
							sb.Append("(ushort)(rBP.Word + rDI.Word)");
							break;
						case 4:
							sb.Append("rSI.Word");
							break;
						case 5:
							sb.Append("rDI.Word");
							break;
						case 6:
							sb.AppendFormat("0x{0:x}", this.uiDisplacement);
							break;
						case 7:
							sb.Append("rBX.Word");
							break;
						case 8:
							sb.AppendFormat("(ushort)(rBX.Word + rSI.Word {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Byte));
							break;
						case 9:
							sb.AppendFormat("(ushort)(rBX.Word + rDI.Word {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Byte));
							break;
						case 10:
							sb.AppendFormat("(ushort)(rBP.Word + rSI.Word {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Byte));
							break;
						case 11:
							sb.AppendFormat("(ushort)(rBP.Word + rDI.Word {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Byte));
							break;
						case 12:
							sb.AppendFormat("(ushort)(rSI.Word {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Byte));
							break;
						case 13:
							sb.AppendFormat("(ushort)(rDI.Word {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Byte));
							break;
						case 14:
							sb.AppendFormat("(ushort)(rBP.Word {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Byte));
							break;
						case 15:
							sb.AppendFormat("(ushort)(rBX.Word {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Byte));
							break;
						case 16:
							sb.AppendFormat("(ushort)(rBX.Word + rSI.Word {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Word));
							break;
						case 17:
							sb.AppendFormat("(ushort)(rBX.Word + rDI.Word {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Word));
							break;
						case 18:
							sb.AppendFormat("(ushort)(rBP.Word + rSI.Word {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Word));
							break;
						case 19:
							sb.AppendFormat("(ushort)(rBP.Word + rDI.Word {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Word));
							break;
						case 20:
							sb.AppendFormat("(ushort)(rSI.Word {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Word));
							break;
						case 21:
							sb.AppendFormat("(ushort)(rDI.Word {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Word));
							break;
						case 22:
							sb.AppendFormat("(ushort)(rBP.Word {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Word));
							break;
						case 23:
							sb.AppendFormat("(ushort)(rBX.Word {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Word));
							break;
					}
					break;
				case InstructionParameterTypeEnum.FPUStackAddress:
					sb.AppendFormat("ST({0})", this.uiValue);
					break;
				default:
					sb.Append("--");
					break;
			}

			return sb.ToString();
		}

		public string ToCSTextMZ(InstructionSizeEnum size)
		{
			StringBuilder sb = new StringBuilder();

			RegisterEnum register;
			SegmentRegisterEnum segmentRegister;

			switch (this.eType)
			{
				case InstructionParameterTypeEnum.Immediate:
					sb.AppendFormat("0x{0:x}", this.uiValue);
					break;

				case InstructionParameterTypeEnum.Register:
					switch (size)
					{
						case InstructionSizeEnum.Byte:
							register = (RegisterEnum)this.uiValue;
							switch (register)
							{
								case RegisterEnum.AL:
									sb.Append("this.oCPU.AX.Low");
									break;
								case RegisterEnum.CL:
									sb.Append("this.oCPU.CX.Low");
									break;
								case RegisterEnum.DL:
									sb.Append("this.oCPU.DX.Low");
									break;
								case RegisterEnum.BL:
									sb.Append("this.oCPU.BX.Low");
									break;
								case RegisterEnum.AH:
									sb.Append("this.oCPU.AX.High");
									break;
								case RegisterEnum.CH:
									sb.Append("this.oCPU.CX.High");
									break;
								case RegisterEnum.DH:
									sb.Append("this.oCPU.DX.High");
									break;
								case RegisterEnum.BH:
									sb.Append("this.oCPU.BX.High");
									break;
								default:
									sb.Append("--");
									break;
							}
							break;
						case InstructionSizeEnum.Word:
							register = (RegisterEnum)(this.uiValue + 0x8);
							switch (register)
							{
								case RegisterEnum.AX:
									sb.Append("this.oCPU.AX.Word");
									break;
								case RegisterEnum.CX:
									sb.Append("this.oCPU.CX.Word");
									break;
								case RegisterEnum.DX:
									sb.Append("this.oCPU.DX.Word");
									break;
								case RegisterEnum.BX:
									sb.Append("this.oCPU.BX.Word");
									break;
								case RegisterEnum.SP:
									sb.Append("this.oCPU.SP.Word");
									break;
								case RegisterEnum.BP:
									sb.Append("this.oCPU.BP.Word");
									break;
								case RegisterEnum.SI:
									sb.Append("this.oCPU.SI.Word");
									break;
								case RegisterEnum.DI:
									sb.Append("this.oCPU.DI.Word");
									break;
								default:
									sb.Append("--");
									break;
							}
							break;
						case InstructionSizeEnum.DWord:
							register = (RegisterEnum)(this.uiValue + 0x8);
							switch (register)
							{
								case RegisterEnum.AX:
									sb.Append("this.oCPU.AX.DWord");
									break;
								case RegisterEnum.CX:
									sb.Append("this.oCPU.CX.DWord");
									break;
								case RegisterEnum.DX:
									sb.Append("this.oCPU.DX.DWord");
									break;
								case RegisterEnum.BX:
									sb.Append("this.oCPU.BX.DWord");
									break;
								case RegisterEnum.SP:
									sb.Append("this.oCPU.SP.DWord");
									break;
								case RegisterEnum.BP:
									sb.Append("this.oCPU.BP.DWord");
									break;
								case RegisterEnum.SI:
									sb.Append("this.oCPU.SI.DWord");
									break;
								case RegisterEnum.DI:
									sb.Append("this.oCPU.DI.DWord");
									break;
								default:
									sb.Append("--");
									break;
							}
							break;
						default:
							sb.Append("--");
							break;
					}
					break;
				case InstructionParameterTypeEnum.SegmentRegister:
					segmentRegister = (SegmentRegisterEnum)this.uiValue;
					switch (size)
					{
						case InstructionSizeEnum.Byte:
							sb.Append("--");
							break;
						case InstructionSizeEnum.Word:
							switch (segmentRegister)
							{
								case SegmentRegisterEnum.ES:
									sb.Append("this.oCPU.ES.Word");
									break;
								case SegmentRegisterEnum.CS:
									sb.Append("this.oCPU.CS.Word");
									break;
								case SegmentRegisterEnum.DS:
									sb.Append("this.oCPU.DS.Word");
									break;
								case SegmentRegisterEnum.SS:
									sb.Append("this.oCPU.SS.Word");
									break;
								default:
									sb.Append("--");
									break;
							}
							break;
						case InstructionSizeEnum.DWord:
							switch (segmentRegister)
							{
								case SegmentRegisterEnum.ES:
									sb.Append("this.oCPU.ES.DWord");
									break;
								case SegmentRegisterEnum.CS:
									sb.Append("this.oCPU.CS.DWord");
									break;
								case SegmentRegisterEnum.DS:
									sb.Append("this.oCPU.DS.DWord");
									break;
								case SegmentRegisterEnum.SS:
									sb.Append("this.oCPU.SS.DWord");
									break;
								default:
									sb.Append("--");
									break;
							}
							break;
					}
					break;
				case InstructionParameterTypeEnum.MemoryAddress:
				case InstructionParameterTypeEnum.LEAMemoryAddress:
					if (this.eSize == InstructionSizeEnum.DWord)
						throw new Exception("x32 addressing mode not yet implemented");

					switch (this.uiValue)
					{
						case 0:
							sb.Append("(ushort)(this.oCPU.BX.Word + this.oCPU.SI.Word)");
							break;
						case 1:
							sb.Append("(ushort)(this.oCPU.BX.Word + this.oCPU.DI.Word)");
							break;
						case 2:
							sb.Append("(ushort)(this.oCPU.BP.Word + this.oCPU.SI.Word)");
							break;
						case 3:
							sb.Append("(ushort)(this.oCPU.BP.Word + this.oCPU.DI.Word)");
							break;
						case 4:
							sb.Append("this.oCPU.SI.Word");
							break;
						case 5:
							sb.Append("this.oCPU.DI.Word");
							break;
						case 6:
							sb.AppendFormat("0x{0:x}", this.uiDisplacement);
							break;
						case 7:
							sb.Append("this.oCPU.BX.Word");
							break;
						case 8:
							sb.AppendFormat("(ushort)(this.oCPU.BX.Word + this.oCPU.SI.Word {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Byte));
							break;
						case 9:
							sb.AppendFormat("(ushort)(this.oCPU.BX.Word + this.oCPU.DI.Word {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Byte));
							break;
						case 10:
							sb.AppendFormat("(ushort)(this.oCPU.BP.Word + this.oCPU.SI.Word {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Byte));
							break;
						case 11:
							sb.AppendFormat("(ushort)(this.oCPU.BP.Word + this.oCPU.DI.Word {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Byte));
							break;
						case 12:
							sb.AppendFormat("(ushort)(this.oCPU.SI.Word {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Byte));
							break;
						case 13:
							sb.AppendFormat("(ushort)(this.oCPU.DI.Word {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Byte));
							break;
						case 14:
							sb.AppendFormat("(ushort)(this.oCPU.BP.Word {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Byte));
							break;
						case 15:
							sb.AppendFormat("(ushort)(this.oCPU.BX.Word {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Byte));
							break;
						case 16:
							sb.AppendFormat("(ushort)(this.oCPU.BX.Word + this.oCPU.SI.Word {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Word));
							break;
						case 17:
							sb.AppendFormat("(ushort)(this.oCPU.BX.Word + this.oCPU.DI.Word {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Word));
							break;
						case 18:
							sb.AppendFormat("(ushort)(this.oCPU.BP.Word + this.oCPU.SI.Word {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Word));
							break;
						case 19:
							sb.AppendFormat("(ushort)(this.oCPU.BP.Word + this.oCPU.DI.Word {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Word));
							break;
						case 20:
							sb.AppendFormat("(ushort)(this.oCPU.SI.Word {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Word));
							break;
						case 21:
							sb.AppendFormat("(ushort)(this.oCPU.DI.Word {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Word));
							break;
						case 22:
							sb.AppendFormat("(ushort)(this.oCPU.BP.Word {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Word));
							break;
						case 23:
							sb.AppendFormat("(ushort)(this.oCPU.BX.Word {0})", RelativeToString(this.uiDisplacement, InstructionSizeEnum.Word));
							break;
					}
					break;
				case InstructionParameterTypeEnum.FPUStackAddress:
					sb.AppendFormat("ST({0})", this.uiValue);
					break;
				default:
					sb.Append("--");
					break;
			}

			return sb.ToString();
		}

		private string RelativeToString(uint value, InstructionSizeEnum size)
		{
			StringBuilder sbValue = new StringBuilder();
			int iValue = 0;

			switch (size)
			{
				case InstructionSizeEnum.Byte:
					value &= 0xff;
					iValue = (sbyte)(byte)value;
					break;
				case InstructionSizeEnum.Word:
					value &= 0xffff;
					iValue = (short)(ushort)value;
					break;
				case InstructionSizeEnum.DWord:
					iValue = (int)value;
					break;
			}

			if (iValue < 0)
			{
				sbValue.AppendFormat("- 0x{0:x}", -iValue);
			}
			else
			{
				sbValue.AppendFormat("+ 0x{0:x}", iValue);
			}

			return sbValue.ToString();
		}
	}
}
