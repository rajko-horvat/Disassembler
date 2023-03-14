using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Disassembler.Decompiler
{
	public class CVariable
	{
		private CDecompiler oParent;
		private CType oType;
		private string sName;
		private CScopeEnum eScope;
		private uint uiAddress;
		private int iSize;

		public CVariable(CDecompiler parent, CType valueType, string name, CScopeEnum scope, uint address, int size)
		{
			this.oParent = parent;
			this.oType = valueType;
			this.sName = name;
			this.eScope = scope;
			this.uiAddress = address;
			this.iSize = size;
		}

		public CDecompiler Parent
		{
			get { return this.oParent; }
		}

		public CType Type
		{
			get { return this.oType; }
		}

		public string Name
		{
			get { return this.sName; }
		}

		public CScopeEnum Scope
		{
			get { return this.eScope; }
		}

		public uint Address
		{
			get { return this.uiAddress; }
		}

		public int Size
		{
			get { return this.iSize; }
		}

		public override string ToString()
		{
			/*if (this.oValue != null)
			{
				return string.Format("{0} {1} = {2};", this.oType.ToString(), this.sName, this.oValue.ToString());
			}

			return string.Format("{0} {1};", this.oType.ToString(), this.sName);*/

			return "";
		}
	}
}