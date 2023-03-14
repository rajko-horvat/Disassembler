using System;
using System.Collections.Generic;
using System.Text;

namespace Disassembler.Decompiler
{
	// statements can have only two real values UInt16 (Word) and UInt32 (DWord), and their derivates Int16 and Int32
	// byte references are handled as conversion to Word or DWord values

	public enum ReferenceTypeEnum
	{
		Undefined,
		Offset,
		Segment
	}

	public abstract class IStatement
	{
		protected CFunction oParent;
		protected CType oValueType;
		protected ReferenceTypeEnum eReferenceType = ReferenceTypeEnum.Undefined;

		public IStatement(CFunction parent) : this(parent, CType.Void, ReferenceTypeEnum.Undefined)
		{ }

		public IStatement(CFunction parent, CType valueType) : this(parent, valueType, ReferenceTypeEnum.Undefined)
		{ }

		public IStatement(CFunction parent, CType valueType, ReferenceTypeEnum referenceType)
		{
			this.oParent = parent;
			this.oValueType = valueType;
			this.eReferenceType = referenceType;
		}

		public CFunction Parent
		{
			get { return this.oParent; }
		}

		public CType ValueType
		{
			get { return this.oValueType; }
		}

		public ReferenceTypeEnum ReferenceType
		{
			get { return this.eReferenceType; }
			set { this.eReferenceType = value; }
		}
	}
}
