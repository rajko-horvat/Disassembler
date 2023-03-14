using System;
using System.Collections.Generic;
using System.Text;

namespace Disassembler.Decompiler
{
	public enum RegisterContextTypeEnum
	{
		Undefined,
		/// <summary>
		/// Context is a reference to another context
		/// </summary>
		Reference,
		/// <summary>
		/// Context is a direct reference to a segment
		/// </summary>
		Segment,
		/// <summary>
		/// Context refers to a physical value of byte
		/// </summary>
		ByteValue,
		/// <summary>
		/// Context refers to a physical value of word
		/// </summary>
		WordValue,
		/// <summary>
		/// Context refers to a phsical location in memory determined by segment:offset which are of word size stored in NumericValue field
		/// The memory location refers to a physical value of byte
		/// </summary>
		MemoryReferenceToByte,
		/// <summary>
		/// Context refers to a phsical location in memory determined by segment:offset which are of word size stored in NumericValue field
		/// The memory location refers to a physical value of word
		/// </summary>
		MemoryReferenceToWord
	}

	public class RegisterContext
	{
		private string sName = null;
		RegisterContextTypeEnum eContextType = RegisterContextTypeEnum.Undefined;
		uint uiValue = 0;
		string sValue = null;

		public RegisterContext(string name, RegisterContextTypeEnum type)
		{
			this.eContextType = type;
		}

		public RegisterContext(string name, RegisterContextTypeEnum type, string value)
		{
			this.eContextType = type;
			this.sValue = value;
		}

		public RegisterContext(string name, RegisterContextTypeEnum type, uint value)
		{
			this.eContextType = type;
			this.uiValue = value;
		}

		public string Name
		{
			get
			{
				return this.sName;
			}
		}

		public RegisterContextTypeEnum ContextType
		{
			get
			{
				return this.eContextType;
			}
			set
			{
				this.eContextType = value;
			}
		}

		public uint NumericValue
		{
			get
			{
				return this.uiValue;
			}
			set
			{
				this.uiValue = value;
			}
		}

		public string StringValue
		{
			get
			{
				return this.sValue;
			}
			set
			{ this.sValue = value; }
		}
	}
}
