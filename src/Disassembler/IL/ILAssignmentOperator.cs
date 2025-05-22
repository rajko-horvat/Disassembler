using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Disassembler
{
	public class ILAssignmentOperator : ILExpression
	{
		private ILExpression variable;
		private ILOperatorEnum op;
		private ILExpression rightOperand;

		public ILAssignmentOperator(ILExpression left, ILOperatorEnum op, ILExpression right)
		{
			this.variable = left;
			this.op = op;
			this.rightOperand = right;
		}

		public ILExpression Variable
		{
			get => this.variable;
		}

		public ILOperatorEnum Operator
		{
			get => this.op;
		}

		public ILExpression RightOperand
		{
			get => this.rightOperand;
		}

		public override string ToCSString()
		{
			return $"{this.variable.ToCSString()} {ILOperator.OperatorToString(this.op)}= {this.rightOperand.ToCSString()}";
		}

		public override string ToString()
		{
			return this.ToCSString();
		}
	}
}
