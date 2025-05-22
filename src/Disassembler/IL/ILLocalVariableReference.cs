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
			if (!this.parent.Variables.ContainsKey(this.offset))
			{
				throw new Exception("Can't find referenced function local variable");
			}

			return this.parent.Variables.GetValueByKey(this.offset).ToCSString();
		}

		public string ToCSDeclarationString()
		{
			if (!this.parent.Variables.ContainsKey(this.offset))
			{
				throw new Exception("Can't find referenced function local variable");
			}

			return this.parent.Variables.GetValueByKey(this.offset).ToCSDeclarationString();
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
