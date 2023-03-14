using System;
using System.Collections.Generic;
using System.Text;

namespace Disassembler.Decompiler
{
	public class CLabel : IStatement
	{
		private string sLabel;

		public CLabel(CFunction parent, string label) : base(parent, null)
		{
			this.sLabel = label;
		}

		public override string ToString()
		{
			return sLabel;
		}
	}
}
