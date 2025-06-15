using System.Runtime.CompilerServices;
using System.Text;

namespace Disassembler.CPU
{
	public class CPUParameter
	{
		private CPUParameterTypeEnum eType = CPUParameterTypeEnum.Undefined;
		// default size is UInt16
		private CPUParameterSizeEnum eSize = CPUParameterSizeEnum.UInt16;
		// default segment which memory access uses
		private CPUSegmentRegisterEnum eDefaultDataSegment = CPUSegmentRegisterEnum.DS;
		private CPUSegmentRegisterEnum eDataSegment = CPUSegmentRegisterEnum.DS;
		private ushort segment = 0;
		private uint value = 0;
		private int displacement = 0;

		public CPUParameter(ushort segment, uint offset)
		{
			this.eType = CPUParameterTypeEnum.SegmentOffset;
			this.segment = segment;
			this.value = offset;
		}

		public CPUParameter(CPUParameterTypeEnum type, uint value)
		{
			this.eType = type;
			this.value = value;
		}

		public CPUParameter(CPUParameterTypeEnum type, CPUParameterSizeEnum size, uint value)
		{
			this.eType = type;
			this.eSize = size;
			this.value = value;
		}

		public CPUParameter(CPUParameterTypeEnum type, CPUParameterSizeEnum size, uint value, int displacement)
		{
			this.eType = type;
			this.eSize = size;
			this.value = value;
			this.displacement = displacement;
		}

		public CPUParameter(CPUParameterTypeEnum type, CPUParameterSizeEnum size, CPUSegmentRegisterEnum segmentRegister, uint value) :
			this(type, size, segmentRegister, value, 0)
		{
		}

		public CPUParameter(CPUParameterTypeEnum type, CPUParameterSizeEnum size, CPUSegmentRegisterEnum segmentRegister, uint value,
			int displacement)
		{
			this.eType = type;
			this.eSize = size;
			if (type == CPUParameterTypeEnum.MemoryAddress)
			{
				if (value == 2 || value == 3 ||
					value == 10 || value == 11 || value == 14 ||
					value == 18 || value == 19 || value == 22)
				{
					this.eDefaultDataSegment = this.eDataSegment = CPUSegmentRegisterEnum.SS;
				}
			}
			if (type == CPUParameterTypeEnum.LEAMemoryAddress)
			{
				if (value == 2 || value == 3 ||
					value == 10 || value == 11 || value == 14 ||
					value == 18 || value == 19 || value == 22)
				{
					this.eDefaultDataSegment = this.eDataSegment = CPUSegmentRegisterEnum.SS;
				}
			}
			if (segmentRegister != CPUSegmentRegisterEnum.Undefined)
				this.eDataSegment = segmentRegister;

			this.value = value;
			this.displacement = displacement;
		}

		public CPUParameterTypeEnum Type
		{
			get { return this.eType; }
		}

		public CPUParameterSizeEnum Size
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

		public CPUSegmentRegisterEnum DefaultDataSegment
		{
			get
			{
				return this.eDefaultDataSegment;
			}
		}

		public CPUSegmentRegisterEnum DataSegment
		{
			get { return this.eDataSegment; }
			set { this.eDataSegment = value; }
		}

		public ushort Segment
		{
			get { return this.segment; }
			set { this.segment = value; }
		}

		public uint Value
		{
			get { return this.value; }
			set { this.value = value; }
		}

		public int Displacement
		{
			get { return this.displacement; }
			set { this.displacement = value; }
		}

		public CPURegisterEnum RegisterValue
		{
			get
			{
				if (this.eType == CPUParameterTypeEnum.Register)
				{
					switch (this.eSize)
					{
						case CPUParameterSizeEnum.UInt8:
						case CPUParameterSizeEnum.UInt16:
						case CPUParameterSizeEnum.UInt32:
							return (CPURegisterEnum)this.value;

						default:
							return CPURegisterEnum.Invalid;
					}
				}
				else
				{
					throw new Exception($"Value {this.eType} is not of register type");
				}
			}
		}

		public CPUSegmentRegisterEnum SegmentRegisterValue
		{
			get
			{
				if (this.eType == CPUParameterTypeEnum.SegmentRegister)
				{
					switch (this.eSize)
					{
						case CPUParameterSizeEnum.UInt8:
						case CPUParameterSizeEnum.UInt16:
						case CPUParameterSizeEnum.UInt32:
							return (CPUSegmentRegisterEnum)this.value;

						default:
							return CPUSegmentRegisterEnum.Invalid;
					}
				}
				else
				{
					throw new Exception($"Value {this.eType} is not of segment register type");
				}
			}
		}

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder();

			switch (this.eType)
			{
				case CPUParameterTypeEnum.Immediate:
					sb.AppendFormat("0x{0:x}", this.value);
					break;

				case CPUParameterTypeEnum.SegmentOffset:
					sb.AppendFormat("0x{0:x4}:0x{1:x4}", (ushort)this.segment, (ushort)this.value);
					break;

				case CPUParameterTypeEnum.Register:
					switch (this.eSize)
					{
						case CPUParameterSizeEnum.UInt8:
							sb.Append(Enum.GetName(typeof(CPURegisterEnum), this.value));
							break;

						case CPUParameterSizeEnum.UInt16:
							sb.Append(Enum.GetName(typeof(CPURegisterEnum), this.value));
							break;

						case CPUParameterSizeEnum.UInt32:
							sb.Append("E" + Enum.GetName(typeof(CPURegisterEnum), this.value));
							break;
					}
					break;

				case CPUParameterTypeEnum.SegmentRegister:
					sb.Append(((this.eSize == CPUParameterSizeEnum.UInt32) ? "E" : "") + Enum.GetName(typeof(CPUSegmentRegisterEnum), this.value));
					break;

				case CPUParameterTypeEnum.Condition:
					sb.Append(Enum.GetName(typeof(CPUJumpConditionEnum), this.value));
					break;

				case CPUParameterTypeEnum.MemoryAddress:
					if (this.eSize == CPUParameterSizeEnum.UInt32)
						throw new Exception("x32 addressing mode not yet implemented");

					if (this.eDefaultDataSegment != this.eDataSegment)
					{
						// print segment only if it's different from default segment
						sb.AppendFormat("{0}:", this.eDataSegment.ToString());
					}

					switch (this.value)
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
							sb.AppendFormat("[{0}]", ImmediateDisplacementToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;
						case 7:
							sb.Append("[BX]");
							break;

						case 8:
							sb.AppendFormat("[{0} + BX + SI]", ImmediateDisplacementToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;
						case 9:
							sb.AppendFormat("[{0} + BX + DI]", ImmediateDisplacementToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;
						case 10:
							sb.AppendFormat("[BP {0} + SI]", RelativeToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;
						case 11:
							sb.AppendFormat("[BP {0} + DI]", RelativeToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;
						case 12:
							sb.AppendFormat("[{0} + SI]", ImmediateDisplacementToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;
						case 13:
							sb.AppendFormat("[{0} + DI]", ImmediateDisplacementToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;
						case 14:
							sb.AppendFormat("[BP {0}]", RelativeToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;
						case 15:
							sb.AppendFormat("[{0} + BX]", ImmediateDisplacementToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;

						case 16:
							sb.AppendFormat("[{0} + BX + SI]", ImmediateDisplacementToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;
						case 17:
							sb.AppendFormat("[{0} + BX + DI]", ImmediateDisplacementToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;
						case 18:
							sb.AppendFormat("[BP {0} + SI]", RelativeToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;
						case 19:
							sb.AppendFormat("[BP {0} + DI]", RelativeToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;
						case 20:
							sb.AppendFormat("[{0} + SI]", ImmediateDisplacementToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;
						case 21:
							sb.AppendFormat("[{0} + DI]", ImmediateDisplacementToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;
						case 22:
							sb.AppendFormat("[BP {0}]", RelativeToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;
						case 23:
							sb.AppendFormat("[{0} + BX]", ImmediateDisplacementToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;
					}
					break;

				case CPUParameterTypeEnum.LEAMemoryAddress:
					if (this.eSize == CPUParameterSizeEnum.UInt32)
						throw new Exception("x32 addressing mode not yet implemented");

					switch (this.value)
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
							sb.AppendFormat("({0})", ImmediateDisplacementToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;
						case 7:
							sb.Append("(BX)");
							break;

						case 8:
							sb.AppendFormat("({0} + BX + SI)", ImmediateDisplacementToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;
						case 9:
							sb.AppendFormat("({0} + BX + DI)", ImmediateDisplacementToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;
						case 10:
							sb.AppendFormat("(BP {0} + SI)", RelativeToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;
						case 11:
							sb.AppendFormat("(BP {0} + DI)", RelativeToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;
						case 12:
							sb.AppendFormat("({0} + SI)", ImmediateDisplacementToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;
						case 13:
							sb.AppendFormat("({0} + DI)", ImmediateDisplacementToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;
						case 14:
							sb.AppendFormat("(BP {0})", RelativeToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;
						case 15:
							sb.AppendFormat("({0} + BX)", ImmediateDisplacementToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;

						case 16:
							sb.AppendFormat("({0} + BX + SI)", ImmediateDisplacementToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;
						case 17:
							sb.AppendFormat("({0} + BX + DI)", ImmediateDisplacementToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;
						case 18:
							sb.AppendFormat("(BP {0} + SI)", RelativeToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;
						case 19:
							sb.AppendFormat("(BP {0} + DI)", RelativeToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;
						case 20:
							sb.AppendFormat("({0} + SI)", ImmediateDisplacementToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;
						case 21:
							sb.AppendFormat("({0} + DI)", ImmediateDisplacementToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;
						case 22:
							sb.AppendFormat("(BP {0})", RelativeToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;
						case 23:
							sb.AppendFormat("({0} + BX)", ImmediateDisplacementToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;
					}
					break;

				case CPUParameterTypeEnum.FPUStackAddress:
					sb.AppendFormat("ST({0})", this.value);
					break;

				case CPUParameterTypeEnum.LocalVariable:
					sb.AppendFormat("Local_{0:x}", this.displacement);
					break;

				case CPUParameterTypeEnum.LocalVariableWithSI:
					sb.AppendFormat("Local_{0:x}[SI]", this.displacement);
					break;

				case CPUParameterTypeEnum.LocalVariableWithDI:
					sb.AppendFormat("Local_{0:x}[DI]", this.displacement);
					break;

				case CPUParameterTypeEnum.LocalParameter:
					sb.AppendFormat("Param_{0:x}", this.displacement);
					break;

				case CPUParameterTypeEnum.LocalParameterWithSI:
					sb.AppendFormat("Param_{0:x}[SI]", this.displacement);
					break;

				case CPUParameterTypeEnum.LocalParameterWithDI:
					sb.AppendFormat("Param_{0:x}[DI]", this.displacement);
					break;

				default:
					sb.Append("--");
					break;
			}

			return sb.ToString();
		}

		public string ToSourceCSTextMZ(CPUParameterSizeEnum size)
		{
			if (this.Type == CPUParameterTypeEnum.MemoryAddress)
			{
				return string.Format("this.oCPU.Read{0}({1}, {2})",
					size.ToString(), GetSegmentTextMZ(), ToCSTextMZ(size));
			}

			return ToCSTextMZ(size);
		}

		public string ToDestinationCSTextMZ(CPUParameterSizeEnum size, string source)
		{
			if (this.Type == CPUParameterTypeEnum.MemoryAddress)
			{
				return string.Format("this.oCPU.Write{0}({1}, {2}, {3});",
					size.ToString(), GetSegmentTextMZ(), ToCSTextMZ(size), source);
			}

			return string.Format("{0} = {1};", ToCSTextMZ(size), source);
		}

		public string GetSegmentTextMZ()
		{
			return string.Format("this.oCPU.{0}.UInt16", this.eDataSegment.ToString());
		}

		public string ToCSTextMZ(CPUParameterSizeEnum size)
		{
			StringBuilder sb = new StringBuilder();

			switch (this.eType)
			{
				case CPUParameterTypeEnum.Immediate:
					sb.AppendFormat("0x{0:x}", this.value);
					break;

				case CPUParameterTypeEnum.Register:
					switch (size)
					{
						case CPUParameterSizeEnum.UInt8:
							switch (this.RegisterValue)
							{
								case CPURegisterEnum.AL:
									sb.Append("this.oCPU.AX.LowUInt8");
									break;
								case CPURegisterEnum.CL:
									sb.Append("this.oCPU.CX.LowUInt8");
									break;
								case CPURegisterEnum.DL:
									sb.Append("this.oCPU.DX.LowUInt8");
									break;
								case CPURegisterEnum.BL:
									sb.Append("this.oCPU.BX.LowUInt8");
									break;
								case CPURegisterEnum.AH:
									sb.Append("this.oCPU.AX.HighUInt8");
									break;
								case CPURegisterEnum.CH:
									sb.Append("this.oCPU.CX.HighUInt8");
									break;
								case CPURegisterEnum.DH:
									sb.Append("this.oCPU.DX.HighUInt8");
									break;
								case CPURegisterEnum.BH:
									sb.Append("this.oCPU.BX.HighUInt8");
									break;
								default:
									sb.Append("--");
									break;
							}
							break;

						case CPUParameterSizeEnum.UInt16:
							switch (this.RegisterValue)
							{
								case CPURegisterEnum.AX:
									sb.Append("this.oCPU.AX.UInt16");
									break;
								case CPURegisterEnum.CX:
									sb.Append("this.oCPU.CX.UInt16");
									break;
								case CPURegisterEnum.DX:
									sb.Append("this.oCPU.DX.UInt16");
									break;
								case CPURegisterEnum.BX:
									sb.Append("this.oCPU.BX.UInt16");
									break;
								case CPURegisterEnum.SP:
									sb.Append("this.oCPU.SP.UInt16");
									break;
								case CPURegisterEnum.BP:
									sb.Append("this.oCPU.BP.UInt16");
									break;
								case CPURegisterEnum.SI:
									sb.Append("this.oCPU.SI.UInt16");
									break;
								case CPURegisterEnum.DI:
									sb.Append("this.oCPU.DI.UInt16");
									break;
								default:
									sb.Append("--");
									break;
							}
							break;

						case CPUParameterSizeEnum.UInt32:
							switch (this.RegisterValue)
							{
								case CPURegisterEnum.AX:
									sb.Append("this.oCPU.AX.UInt32");
									break;
								case CPURegisterEnum.CX:
									sb.Append("this.oCPU.CX.UInt32");
									break;
								case CPURegisterEnum.DX:
									sb.Append("this.oCPU.DX.UInt32");
									break;
								case CPURegisterEnum.BX:
									sb.Append("this.oCPU.BX.UInt32");
									break;
								case CPURegisterEnum.SP:
									sb.Append("this.oCPU.SP.UInt32");
									break;
								case CPURegisterEnum.BP:
									sb.Append("this.oCPU.BP.UInt32");
									break;
								case CPURegisterEnum.SI:
									sb.Append("this.oCPU.SI.UInt32");
									break;
								case CPURegisterEnum.DI:
									sb.Append("this.oCPU.DI.UInt32");
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

				case CPUParameterTypeEnum.SegmentRegister:
					switch (size)
					{
						case CPUParameterSizeEnum.UInt8:
							sb.Append("--");
							break;

						case CPUParameterSizeEnum.UInt16:
						case CPUParameterSizeEnum.UInt32:
							switch (this.SegmentRegisterValue)
							{
								case CPUSegmentRegisterEnum.ES:
									sb.Append("this.oCPU.ES.UInt16");
									break;

								case CPUSegmentRegisterEnum.CS:
									sb.Append("this.oCPU.CS.UInt16");
									break;

								case CPUSegmentRegisterEnum.SS:
									sb.Append("this.oCPU.SS.UInt16");
									break;

								case CPUSegmentRegisterEnum.DS:
									sb.Append("this.oCPU.DS.UInt16");
									break;

								case CPUSegmentRegisterEnum.FS:
									sb.Append("this.oCPU.FS.UInt16");
									break;

								case CPUSegmentRegisterEnum.GS:
									sb.Append("this.oCPU.GS.UInt16");
									break;

								default:
									sb.Append("--");
									break;
							}
							break;
					}
					break;

				case CPUParameterTypeEnum.MemoryAddress:
				case CPUParameterTypeEnum.LEAMemoryAddress:
					if (this.eSize == CPUParameterSizeEnum.UInt32)
						throw new Exception("x32 addressing mode not yet implemented");

					switch (this.value)
					{
						case 0:
							sb.Append("(ushort)(this.oCPU.BX.UInt16 + this.oCPU.SI.UInt16)");
							break;

						case 1:
							sb.Append("(ushort)(this.oCPU.BX.UInt16 + this.oCPU.DI.UInt16)");
							break;

						case 2:
							sb.Append("(ushort)(this.oCPU.BP.UInt16 + this.oCPU.SI.UInt16)");
							break;

						case 3:
							sb.Append("(ushort)(this.oCPU.BP.UInt16 + this.oCPU.DI.UInt16)");
							break;

						case 4:
							sb.Append("this.oCPU.SI.UInt16");
							break;

						case 5:
							sb.Append("this.oCPU.DI.UInt16");
							break;

						case 6:
							sb.Append(ImmediateDisplacementToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;

						case 7:
							sb.Append("this.oCPU.BX.UInt16");
							break;


						case 8:
							sb.AppendFormat("(ushort)({0} + this.oCPU.BX.UInt16 + this.oCPU.SI.UInt16)", ImmediateDisplacementToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;

						case 9:
							sb.AppendFormat("(ushort)({0} + this.oCPU.BX.UInt16 + this.oCPU.DI.UInt16)", ImmediateDisplacementToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;

						case 10:
							sb.AppendFormat("(ushort)(this.oCPU.BP.UInt16 {0} + this.oCPU.SI.UInt16)", RelativeToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;

						case 11:
							sb.AppendFormat("(ushort)(this.oCPU.BP.UInt16 {0} + this.oCPU.DI.UInt16)", RelativeToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;

						case 12:
							sb.AppendFormat("(ushort)({0} + this.oCPU.SI.UInt16)", ImmediateDisplacementToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;

						case 13:
							sb.AppendFormat("(ushort)({0} + this.oCPU.DI.UInt16)", ImmediateDisplacementToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;

						case 14:
							sb.AppendFormat("(ushort)(this.oCPU.BP.UInt16 {0})", RelativeToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;

						case 15:
							sb.AppendFormat("(ushort)({0} + this.oCPU.BX.UInt16)", ImmediateDisplacementToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;


						case 16:
							sb.AppendFormat("(ushort)({0} + this.oCPU.BX.UInt16 + this.oCPU.SI.UInt16)", ImmediateDisplacementToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;

						case 17:
							sb.AppendFormat("(ushort)({0} + this.oCPU.BX.UInt16 + this.oCPU.DI.UInt16)", ImmediateDisplacementToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;

						case 18:
							sb.AppendFormat("(ushort)(this.oCPU.BP.UInt16 {0} + this.oCPU.SI.UInt16)", RelativeToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;

						case 19:
							sb.AppendFormat("(ushort)(this.oCPU.BP.UInt16 {0} + this.oCPU.DI.UInt16)", RelativeToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;

						case 20:
							sb.AppendFormat("(ushort)({0} + this.oCPU.SI.UInt16)", ImmediateDisplacementToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;

						case 21:
							sb.AppendFormat("(ushort)({0} + this.oCPU.DI.UInt16)", ImmediateDisplacementToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;

						case 22:
							sb.AppendFormat("(ushort)(this.oCPU.BP.UInt16 {0})", RelativeToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;

						case 23:
							sb.AppendFormat("(ushort)({0} + this.oCPU.BX.UInt16)", ImmediateDisplacementToString(CPUParameterSizeEnum.UInt16, this.displacement));
							break;
					}
					break;

				case CPUParameterTypeEnum.FPUStackAddress:
					sb.AppendFormat("ST({0})", this.value);
					break;

				case CPUParameterTypeEnum.LocalVariable:
					sb.AppendFormat("Local_{0:x}", this.displacement);
					break;

				case CPUParameterTypeEnum.LocalVariableWithSI:
					sb.AppendFormat("Local_{0:x}[this.oCPU.SI.UInt16]", this.displacement);
					break;

				case CPUParameterTypeEnum.LocalVariableWithDI:
					sb.AppendFormat("Local_{0:x}[this.oCPU.DI.UInt16]", this.displacement);
					break;

				case CPUParameterTypeEnum.LocalParameter:
					sb.AppendFormat("Param_{0:x}", this.displacement);
					break;

				case CPUParameterTypeEnum.LocalParameterWithSI:
					sb.AppendFormat("Param_{0:x}[this.oCPU.SI.UInt16]", this.displacement);
					break;

				case CPUParameterTypeEnum.LocalParameterWithDI:
					sb.AppendFormat("Param_{0:x}[this.oCPU.DI.UInt16]", this.displacement);
					break;

				default:
					sb.Append("--");
					break;
			}

			return sb.ToString();
		}

		public static string ImmediateDisplacementToString(CPUParameterSizeEnum size, int displacement)
		{
			switch (size)
			{
				case CPUParameterSizeEnum.UInt8:
					return $"0x{(displacement & 0xff):x}";

				case CPUParameterSizeEnum.UInt16:
					return $"0x{(displacement & 0xffff):x}";

				case CPUParameterSizeEnum.UInt32:
					return $"0x{(uint)displacement:x}";

				default:
					throw new Exception($"Unknown immediate size '{size}'");
			}
		}

		public static string RelativeToString(CPUParameterSizeEnum size, int displacement)
		{
			StringBuilder sbValue = new StringBuilder();
			int iValue = 0;

			switch (size)
			{
				case CPUParameterSizeEnum.UInt8:
					iValue = (sbyte)((byte)(displacement & 0xff));
					break;

				case CPUParameterSizeEnum.UInt16:
					iValue = (short)((ushort)(displacement & 0xffff));
					break;

				case CPUParameterSizeEnum.UInt32:
					iValue = displacement;
					break;
			}


			switch (size)
			{
				case CPUParameterSizeEnum.UInt8:
					if (iValue < 0)
					{
						sbValue.AppendFormat("- 0x{0:x}", -iValue);
					}
					else
					{
						sbValue.AppendFormat("+ 0x{0:x}", iValue);
					}
					break;

				case CPUParameterSizeEnum.UInt16:
					if (iValue < 0)
					{
						sbValue.AppendFormat("- 0x{0:x}", -iValue);
					}
					else
					{
						sbValue.AppendFormat("+ 0x{0:x}", iValue);
					}
					break;

				case CPUParameterSizeEnum.UInt32:
					if (iValue < 0)
					{
						sbValue.AppendFormat("- 0x{0:x}", -iValue);
					}
					else
					{
						sbValue.AppendFormat("+ 0x{0:x}", iValue);
					}
					break;
			}

			return sbValue.ToString();
		}

		public bool Equals(CPUParameter parameter1)
		{
			if (this.eType == parameter1.eType && this.eSize == parameter1.eSize && this.eDataSegment == parameter1.eDataSegment &&
				this.segment == parameter1.segment && this.value == parameter1.value && this.displacement == parameter1.displacement)
			{
				return true;
			}

			return false;
		}
	}
}
