using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Disassembler
{
	public class ILFunctionCall : ILExpression
	{
		private ProgramFunction parent;
		private List<ILExpression> parameters;

		public ILFunctionCall(ProgramFunction parent, List<ILExpression> parameters)
		{
			this.parent = parent;
			this.parameters = parameters;
		}

		public override string ToCSString()
		{
			StringBuilder sb = new StringBuilder();

			sb.Append($"this.oParent.{this.parent.Segment.ToString()}.{this.parent.Name}(");

			for (int i = 0; i < this.parameters.Count; i++)
			{
				if (i > 0)
					sb.Append(", ");

				sb.Append(this.parameters[i].ToCSString());
			}
			sb.Append(")");

			return sb.ToString();
		}

		public ProgramFunction Parent
		{
			get => this.parent;
		}

		public List<ILExpression> Parameters
		{
			get => this.parameters;
		}

		public override string ToString()
		{
			return this.ToCSString();
		}
	}
}
