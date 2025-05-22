namespace Disassembler.CPU
{
	public enum CPURegisterEnum
	{
		None = 0x0,
		AL = 0x01,
		CL = 0x02,
		DL = 0x04,
		BL = 0x08,
		AH = 0x10,
		CH = 0x20,
		DH = 0x40,
		BH = 0x80,

		AX = AL | AH,
		CX = CL | CH,
		DX = DL | DH,
		BX = BL | BH,
		AX_DX = AX | DX,
		SP = 0x0100,
		BP = 0x0200,
		SI = 0x0400,
		DI = 0x0800,

		CR0 = 0x0001000,
		CR2 = 0x0002000,
		CR3 = 0x0004000,
		DR0 = 0x0008000,
		DR1 = 0x0010000,
		DR2 = 0x0020000,
		DR3 = 0x0040000,
		DR6 = 0x0080000,
		DR7 = 0x0100000,
		TR3 = 0x0200000,
		TR4 = 0x0400000,
		TR5 = 0x0800000,
		TR6 = 0x1000000,
		TR7 = 0x2000000,

		Invalid = 0x10000000
	}
}
