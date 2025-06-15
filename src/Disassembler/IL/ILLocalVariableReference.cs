using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Disassembler
{
    public class ILLocalVariableReference: ILExpression
    {
		private ProgramFunction parent;
		private int offset;

		public ILLocalVariableReference(ProgramFunction parent, int offset)
		{
			this.parent = parent;
			this.offset = offset;
		}

		public override string ToCSString()
		{
			if (!this.parent.LocalVariables.ContainsKey(this.offset))
			{
				throw new Exception("Can't find referenced function local variable");
			}

			return this.parent.LocalVariables.GetValueByKey(this.offset).ToCSString();
		}

		public string ToCSDeclarationString()
		{
			if (!this.parent.LocalVariables.ContainsKey(this.offset))
			{
				throw new Exception("Can't find referenced function local variable");
			}

			return this.parent.LocalVariables.GetValueByKey(this.offset).CSDeclaration;
		}

		public ProgramFunction Parent
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
