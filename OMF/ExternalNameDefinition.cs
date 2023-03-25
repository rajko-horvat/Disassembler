using System;
using System.Collections.Generic;
using System.IO;

namespace Disassembler.OMF
{
	public class ExternalNameDefinition
	{
		private string sName = "";
		private int iTypeIndex = -1;

		public ExternalNameDefinition(Stream stream)
		{
			this.sName = CModule.ReadString(stream);
			this.iTypeIndex = CModule.ReadByte(stream);
		}

		public string Name
		{
			get
			{
				return this.sName;
			}
		}

		public int TypeIndex
		{
			get
			{
				return this.iTypeIndex;
			}
		}
	}
}
