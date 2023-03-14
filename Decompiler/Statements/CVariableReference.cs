using System;
using System.Collections.Generic;
using System.Text;

namespace Disassembler.Decompiler
{
	public enum CScopeEnum
	{
		Global,
		Local,
		Parameter
	}

	public class CVariableReference : IStatement
	{
		private CType oType;
		private string sName;
		private CScopeEnum eScope;
		private uint uiAddress;

		public CVariableReference(CFunction parent, CParameter parameter, uint address) : base(parent, parameter.Type)
		{
			this.oType = parameter.Type;
			this.sName = parameter.Name;
			this.eScope = CScopeEnum.Parameter;
			this.uiAddress = address;
		}

		public CVariableReference(CFunction parent, CType valueType, string name, CScopeEnum scope, uint address) :
			base(parent, valueType)
		{
			this.oType = valueType;
			this.sName = name;
			this.eScope = scope;
			this.uiAddress = address;
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
