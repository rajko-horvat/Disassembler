using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Disassembler
{
    public class ILGlobalVariableReference: ILExpression
    {
		private ProgramSegment parent;
		private int offset;

		public ILGlobalVariableReference(ProgramSegment parent, int offset)
		{
			this.parent = parent;
			this.offset = offset;
		}

		public override string ToCSString()
		{
			if (!this.parent.GlobalVariables.ContainsKey(this.offset))
			{
				throw new Exception("Can't find referenced global variable");
			}

			return $"this.oParent.{this.parent.Name}.{this.parent.GlobalVariables.GetValueByKey(this.offset).ToCSString()}";
		}

		public string ToCSDeclarationString()
		{
			if (!this.parent.GlobalVariables.ContainsKey(this.offset))
			{
				throw new Exception("Can't find referenced global variable");
			}

			return this.parent.GlobalVariables.GetValueByKey(this.offset).CSDeclaration;
		}

		public ProgramSegment Parent
		{
			get => this.parent;
		}

		public int Offset
		{
			get => this.offset;
		}

		public override string ToString()
		{
			return this.ToCSString();
		}
	}
}
