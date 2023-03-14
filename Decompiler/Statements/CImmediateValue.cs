using System;
using System.Collections.Generic;
using System.Text;

namespace Disassembler.Decompiler
{
	public class CImmediateValue: IStatement
	{
		private uint uiValue;

		public CImmediateValue(CFunction parent, CType valueType, uint value) : base(parent, valueType)
		{
			this.uiValue = value;
		}

		public CImmediateValue(CFunction parent, CType valueType, ReferenceTypeEnum referenceType, uint value) : base(parent, valueType, referenceType)
		{
			this.uiValue = value;
		}

		public uint Value
		{
			get { return this.uiValue; }
			set { this.uiValue = value; }
		}

		public override string ToString()
		{
			switch (this.oValueType.Type)
			{
				case CTypeEnum.Int8:
					return string.Format("{0}", (sbyte)this.uiValue);
				case CTypeEnum.Int16:
					return string.Format("{0}", (short)this.uiValue);
				case CTypeEnum.Int32:
					return string.Format("{0}", (int)this.uiValue);
				case CTypeEnum.UInt8:
					return string.Format("{0}", (byte)this.uiValue);
				case CTypeEnum.UInt16:
					return string.Format("{0}", (ushort)this.uiValue);
				case CTypeEnum.UInt32:
					return string.Format("{0}", (uint)this.uiValue);
				default:
					return "Undefined immediate value type";
			}
		}
	}
}
