using System;
using System.Collections.Generic;
using System.Text;

namespace Disassembler.Decompiler
{
	public class CNamespace : IStatement
	{
		private List<CVariableReference> aParameters = new List<CVariableReference>();
		private List<CVariableReference> aVariables = new List<CVariableReference>();

		public CNamespace(CFunction parent) : base(parent)
		{ }

		public CNamespace(CFunction parent, List<CParameter> parameters) : base(parent)
		{
			uint iPos = 0;

			for (int i = 0; i < parameters.Count; i++)
			{
				CVariableReference var = new CVariableReference(parent, parameters[i], iPos);
				this.aParameters.Add(var);
				iPos += (uint)var.Type.Size;
			}
		}

		public List<CVariableReference> Parameters
		{
			get { return this.aParameters; }
		}

		public List<CVariableReference> Variables
		{
			get { return this.aVariables; }
		}
	}
}
