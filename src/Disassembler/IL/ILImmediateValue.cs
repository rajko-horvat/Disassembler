namespace Disassembler
{
    public class ILImmediateValue: ILExpression
    {
		private ILValueType valueType;
		private bool hexNotation = false;
		private uint value;

		public ILImmediateValue(ILValueType valueType, uint value)
		{
			this.valueType = valueType;
			this.value = value;
		}

		public override string ToCSString()
		{
			if (this.hexNotation)
			{
				switch (this.valueType.BaseType)
				{
					case ILBaseValueTypeEnum.UInt8:
						return $"0x{(this.value & 0xff):x}";

					case ILBaseValueTypeEnum.Int8:
						return $"0x{((sbyte)(this.value & 0xff)):x}";

					case ILBaseValueTypeEnum.UInt16:
						return $"0x{(this.value & 0xffff):x}";

					case ILBaseValueTypeEnum.Int16:
						return $"0x{((short)(this.value & 0xffff)):x}";

					case ILBaseValueTypeEnum.UInt32:
						return $"0x{(this.value & 0xffffffff):x}";

					case ILBaseValueTypeEnum.Int32:
						return $"0x{((int)(this.value & 0xffffffff)):x}";
				}
			}
			else
			{
				switch (this.valueType.BaseType)
				{
					case ILBaseValueTypeEnum.UInt8:
						return $"{(this.value & 0xff)}";

					case ILBaseValueTypeEnum.Int8:
						return $"{((sbyte)(this.value & 0xff))}";

					case ILBaseValueTypeEnum.UInt16:
						return $"{(this.value & 0xffff)}";

					case ILBaseValueTypeEnum.Int16:
						return $"{((short)(this.value & 0xffff))}";

					case ILBaseValueTypeEnum.UInt32:
						return $"{(this.value & 0xffffffff)}";

					case ILBaseValueTypeEnum.Int32:
						return $"{((int)(this.value & 0xffffffff))}";
				}
			}

			return "Unknown value type";
		}

		public override string ToString()
		{
			return this.ToCSString();
		}

		public ILValueType Type
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
