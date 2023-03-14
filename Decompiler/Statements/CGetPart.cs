using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Disassembler.Decompiler
{
	public enum PartTypeEnum
	{
		LowByte,
		HighByte,
		LowWord,
		HighWord
	}

	public class CGetPart: IStatement
	{
		private PartTypeEnum ePartType;
		private IStatement oParameter;

		public CGetPart(CFunction parent, IStatement parameter, PartTypeEnum partType) : base(parent, (partType == PartTypeEnum.LowByte || partType == PartTypeEnum.HighByte) ? CType.Byte : CType.Word)
		{
			this.ePartType = partType;
			this.oParameter = parameter;
		}

		public PartTypeEnum PartType
		{
			get { return this.ePartType; }
		}

		public IStatement Parameter
		{
			get { return this.oParameter; }
		}

		public override string ToString()
		{
			switch (this.ePartType)
			{
				case PartTypeEnum.LowByte:
					return string.Format("(({0}) & 0xff)", this.oParameter.ToString());
				case PartTypeEnum.HighByte:
					return string.Format("((({0}) & 0xff00) >> 8)", this.oParameter.ToString());
				case PartTypeEnum.LowWord:
					return string.Format("(({0}) & 0xffff)", this.oParameter.ToString());
				case PartTypeEnum.HighWord:
					return string.Format("((({0}) & 0xffff0000) >> 16)", this.oParameter.ToString());
			}

			throw new Exception("Invalid part type");
		}
	}
}
