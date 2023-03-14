using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Disassembler.Decompiler
{
	public class CVariableDefinition
	{
		private CType oValueType;
		private string sName;
		private IStatement oDefaultValue;
		private uint uiAddress;

		public CVariableDefinition(CType valueType, string name, IStatement defaultValue, uint address)
		{
			this.oValueType = valueType;
			this.sName = name;
			this.oDefaultValue = defaultValue;
			this.uiAddress = address;
		}

		public CType ValueType
		{
			get { return this.oValueType; }
		}

		public string Name
		{
			get { return this.sName; }
		}

		public IStatement DefaultValue
		{
			get { return this.oDefaultValue; }
			set { this.oDefaultValue = value; }
		}

		public uint Address
		{
			get { return this.uiAddress; }
		}

		public override string ToString()
		{
			if (this.oDefaultValue != null)
			{
				return string.Format("{0} {1} = {2};", this.oValueType.ToString(), this.sName, this.oDefaultValue.ToString());
			}

			return string.Format("{0} {1};", this.oValueType.ToString(), this.sName);
		}
	}
}
