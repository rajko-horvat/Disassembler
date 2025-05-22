using Disassembler.CPU;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Disassembler
{
    public class ILImmediateValue: ILExpression
    {
		private ILValueTypeEnum valueType;
		private bool hexNotation = false;
		private uint value;

		public ILImmediateValue(CPUParameterSizeEnum valueType, uint value) : this(ILVariable.FromCPUParameterSizeEnum(valueType), value)
		{ }

		public ILImmediateValue(ILValueTypeEnum valueType, uint value)
		{
			this.valueType = valueType;
			this.value = value;
		}

		public void UnsignedToSignedValueType()
		{
			switch (this.valueType)
			{
				case ILValueTypeEnum.UInt8:
					this.valueType = ILValueTypeEnum.Int8;
					break;

				case ILValueTypeEnum.UInt16:
					this.valueType = ILValueTypeEnum.Int16;
					break;

				case ILValueTypeEnum.UInt32:
					this.valueType = ILValueTypeEnum.Int32;
					break;
			}
		}

		public void SignedToUnsignedValueType()
		{
			switch (this.valueType)
			{
				case ILValueTypeEnum.Int8:
					this.valueType = ILValueTypeEnum.UInt8;
					break;

				case ILValueTypeEnum.Int16:
					this.valueType = ILValueTypeEnum.UInt16;
					break;

				case ILValueTypeEnum.Int32:
					this.valueType = ILValueTypeEnum.UInt32;
					break;
			}
		}

		public override string ToCSString()
		{
			if (this.hexNotation)
			{
				switch (this.valueType)
				{
					case ILValueTypeEnum.UInt8:
						return $"0x{(this.value & 0xff):x}";

					case ILValueTypeEnum.Int8:
						return $"0x{((sbyte)(this.value & 0xff)):x}";

					case ILValueTypeEnum.UInt16:
						return $"0x{(this.value & 0xffff):x}";

					case ILValueTypeEnum.Int16:
						return $"0x{((short)(this.value & 0xffff)):x}";

					case ILValueTypeEnum.UInt32:
						return $"0x{(this.value & 0xffffffff):x}";

					case ILValueTypeEnum.Int32:
						return $"0x{((int)(this.value & 0xffffffff)):x}";
				}
			}
			else
			{
				switch (this.valueType)
				{
					case ILValueTypeEnum.UInt8:
						return $"{(this.value & 0xff)}";

					case ILValueTypeEnum.Int8:
						return $"{((sbyte)(this.value & 0xff))}";

					case ILValueTypeEnum.UInt16:
						return $"{(this.value & 0xffff)}";

					case ILValueTypeEnum.Int16:
						return $"{((short)(this.value & 0xffff))}";

					case ILValueTypeEnum.UInt32:
						return $"{(this.value & 0xffffffff)}";

					case ILValueTypeEnum.Int32:
						return $"{((int)(this.value & 0xffffffff))}";
				}
			}

			return "Unknown value type";
		}

		public override string ToString()
		{
			return this.ToCSString();
		}

		public ILValueTypeEnum Type
		{
			get => this.valueType;
			set => this.valueType = value;
		}

		public bool HexNotation
		{
			get => this.hexNotation;
			set => this.hexNotation = value;
		}

		public uint Value
		{
			get => this.value;
			set => this.value = value;
		}
	}
}
