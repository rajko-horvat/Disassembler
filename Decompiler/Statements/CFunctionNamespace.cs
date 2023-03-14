using System;
using System.Collections.Generic;
using System.Text;

namespace Disassembler.Decompiler
{
	public class CFunctionNamespace
	{
		private CFunction oParent;
		private List<CVariableReference> aParameters = new List<CVariableReference>();
		private List<CVariableReference> aVariables = new List<CVariableReference>();

		public CFunctionNamespace(CFunction parent)
		{
			this.oParent = parent;
		}

		public CFunctionNamespace(CFunction parent, List<CParameter> parameters)
		{
			this.oParent = parent;
			uint iPos = 0;

			for (int i = 0; i < parameters.Count; i++)
			{
				CVariableReference var = new CVariableReference(parent, parameters[i], iPos);
				this.aParameters.Add(var);
				iPos += (uint)var.Type.Size;
			}
		}

		public CFunction Parent
		{
			get { return this.oParent; }
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
