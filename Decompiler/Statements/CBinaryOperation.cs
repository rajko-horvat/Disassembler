using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Disassembler.Decompiler
{
	public enum BinaryOperationEnum
	{
		And,
		Or,
		Xor
	}

	public class CBinaryOperation: IStatement
	{
		private BinaryOperationEnum eOperation;
		private IStatement oLeft;
		private IStatement oRight;

		public CBinaryOperation(CFunction parent, IStatement leftParam, IStatement rightParam, BinaryOperationEnum operation) : base(parent, leftParam.ValueType)
		{
			this.oLeft = leftParam;
			this.oRight = rightParam;
			this.eOperation = operation;

			if (leftParam.ValueType != rightParam.ValueType)
				throw new Exception("Value types are not equal");
		}

		public IStatement Left
		{
			get { return this.oLeft; }
		}

		public IStatement Right
		{
			get { return this.oRight; }
		}

		public BinaryOperationEnum Operation
		{
			get { return this.eOperation; }
		}

		public string OperationText
		{
			get
			{
				switch (this.eOperation)
				{
					case BinaryOperationEnum.And:
						return "&";
					case BinaryOperationEnum.Or:
						return "|";
					case BinaryOperationEnum.Xor:
						return "^";
					default:
						throw new Exception("Invalid math operation");
				}
			}
		}

		public override string ToString()
		{
			return string.Format("({0} {1} {2})", oLeft.ToString(), this.OperationText, oRight.ToString());
		}
	}
}
