using System;

namespace Disassembler
{
	[Flags]
	public enum FlagsEnum
	{
		Undefined = 0,
		CF = 1,
		PF = 2,
		AF = 4,
		ZF = 8,
		SF = 0x10,
		OF = 0x20,
		IF = 0x40,
		DF = 0x80,
		All = CF | PF | AF | ZF | SF | OF | IF | DF
	}
}
