using System;
using System.Collections.Generic;

namespace Disassembler
{
	public enum OpCodeParameterTypeEnum
	{
		Undefined,
		SignExtend,
		OperandSize,
		ReverseDirection,
		FPUDestination,
		FPUStackAddress,
		AccumulatorWithRegister,
		Register,
		RegisterCL,
		RegisterAWithDX,
		RegisterDXWithA,
		SegmentRegister,
		SegmentRegisterNoCS,
		SegmentRegisterFSGS,
		MemoryAddressing,
		RegisterOrMemoryAddressing,
		Condition,
		AccumulatorWithImmediateValue,
		ImmediateValueWithAccumulator,
		ImmediateValue,
		ImmediateValue1,
		ImmediateValue3,
		ImmediateMemoryAddressWithAccumulator,
		RelativeValue,
		ImmediateSegmentOffset
	}

	public class OpCodeParameter
	{
		private OpCodeParameterTypeEnum eType = OpCodeParameterTypeEnum.Undefined;
		private int iMask = 0;
		private int iBitPosition = 0;
		private int iByteSize = 0;
		private List<int> aValues = new List<int>();
		private int iValueIndex = 0;
		private SegmentRegisterEnum eDefaultSegment = SegmentRegisterEnum.DS;

		public OpCodeParameter(OpCodeParameterTypeEnum type, int mask, int bitPosition)
		{
			this.eType = type;
			this.iMask = mask << bitPosition;
			this.iBitPosition = bitPosition;

			switch (type)
			{
				case OpCodeParameterTypeEnum.SignExtend:
				case OpCodeParameterTypeEnum.OperandSize:
				case OpCodeParameterTypeEnum.ReverseDirection:
				case OpCodeParameterTypeEnum.FPUDestination:
				case OpCodeParameterTypeEnum.FPUStackAddress:
				case OpCodeParameterTypeEnum.AccumulatorWithRegister:
				case OpCodeParameterTypeEnum.Register:
				case OpCodeParameterTypeEnum.Condition:
					for (int i = 0; i <= mask; i++)
					{
						this.aValues.Add(i);
					}
					break;
				case OpCodeParameterTypeEnum.MemoryAddressing:
					for (int i = 0; i < 3; i++)
					{
						for (int j = 0; j <= 7; j++)
						{
							this.aValues.Add(i << 6 | j);
						}
					}
					break;
				case OpCodeParameterTypeEnum.RegisterOrMemoryAddressing:
					for (int i = 0; i <= 3; i++)
					{
						for (int j = 0; j <= 7; j++)
						{
							this.aValues.Add(i << 6 | j);
						}
					}
					break;
				case OpCodeParameterTypeEnum.SegmentRegisterNoCS:
					// only ES, SS and DS
					for (int i = 0; i <= mask; i++)
					{
						if (i != (int)SegmentRegisterEnum.CS)
							this.aValues.Add(i);
					}
					break;
				case OpCodeParameterTypeEnum.SegmentRegister:
					if (mask == 3)
					{
						// only ES, CS, SS and DS
						for (int i = 0; i <= mask; i++)
						{
							this.aValues.Add(i);
						}
					}
					else
					{
						// include FS, GS
						for (int i = 0; i <= 5; i++)
						{
							this.aValues.Add(i);
						}
					}
					break;
				case OpCodeParameterTypeEnum.SegmentRegisterFSGS:
					this.aValues.Add((int)SegmentRegisterEnum.FS);
					this.aValues.Add((int)SegmentRegisterEnum.GS);
					break;
				default:
					break;
			}
		}

		public OpCodeParameter(OpCodeParameterTypeEnum type, int byteSize)
		{
			this.eType = type;
			this.iByteSize = byteSize;
		}

		public OpCodeParameterTypeEnum Type
		{
			get
			{
				return this.eType;
			}
		}

		public int Mask
		{
			get
			{
				return this.iMask;
			}
		}

		public int BitPosition
		{
			get
			{
				return this.iBitPosition;
			}
		}

		public int ByteSize
		{
			get
			{
				return this.iByteSize;
			}
		}

		public List<int> Values
		{
			get
			{
				return this.aValues;
			}
		}

		public int ValueIndex
		{
			get
			{
				return this.iValueIndex;
			}
			set
			{
				this.iValueIndex = value;
			}
		}

		public SegmentRegisterEnum DefaultSegment
		{
			get
			{
				return this.eDefaultSegment;
			}
			set
			{
				this.eDefaultSegment = value;
			}
		}
	}
}
