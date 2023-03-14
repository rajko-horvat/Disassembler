using System;
using System.Collections.Generic;
using System.Text;

namespace Disassembler.Decompiler
{
	public class CParameter
	{
		private string sName = null;
		private CType oType = null;

		public CParameter(CType type)
		{
			this.oType = type;
		}

		public CParameter(CType type, string name)
		{
			this.sName = name;
			this.oType = type;
		}

		public string Name
		{
			get
			{
				return this.sName;
			}
		}

		public CType Type
		{
			get
			{
				return this.oType;
			}
		}
	}
}
