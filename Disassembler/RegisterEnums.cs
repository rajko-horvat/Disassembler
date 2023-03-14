using System;

namespace Disassembler
{
	public enum RegisterEnum
	{
		AL = 0,
		CL = 1,
		DL = 2,
		BL = 3,
		AH = 4,
		CH = 5,
		DH = 6,
		BH = 7,
		AX = 0x8 | 0,
		CX = 0x8 | 1,
		DX = 0x8 | 2,
		BX = 0x8 | 3,
		SP = 0x8 | 4,
		BP = 0x8 | 5,
		SI = 0x8 | 6,
		DI = 0x8 | 7,
		CR0 = 0x10 | 0,
		CR2 = 0x10 | 2,
		CR3 = 0x10 | 3,
		DR0 = 0x18 | 0,
		DR1 = 0x18 | 1,
		DR2 = 0x18 | 2,
		DR3 = 0x18 | 3,
		DR6 = 0x18 | 6,
		DR7 = 0x18 | 7,
		TR3 = 0x20 | 3,
		TR4 = 0x20 | 4,
		TR5 = 0x20 | 5,
		TR6 = 0x20 | 6,
		TR7 = 0x20 | 7,
		Invalid = 0x100
	}

	public enum SegmentRegisterEnum
	{
		ES = 0,
		CS = 1,
		SS = 2,
		DS = 3,
		FS = 4,
		GS = 5,
		Invalid = 0x10,
		Undefined = 0x20,
		Immediate = 0x30
	}
}
