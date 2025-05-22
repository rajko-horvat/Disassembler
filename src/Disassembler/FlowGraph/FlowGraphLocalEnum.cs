namespace Disassembler
{
	[Flags]
	public enum FlowGraphLocalEnum
	{
		None = 0,
		// Registers
		AL = 0x1,
		AH = 0x2,
		BL = 0x4,
		BH = 0x8,
		CL = 0x10,
		CH = 0x20,
		DL = 0x40,
		DH = 0x80,
		AX = AL | AH,
		BX = BL | BH,
		CX = CL | CH,
		DX = DL | DH,
		SI = 0x100,
		DI = 0x200,
		// Stack registers
		BP = 0x400,
		SP = 0x800,
		// Segments
		CS = 0x1000,
		SS = 0x2000,
		DS = 0x4000,
		ES = 0x8000,
		FS = 0x10000,
		GS = 0x20000,
		// Flags
		ZFlag = 0x100000,
		CFlag = 0x200000,
		SFlag = 0x400000,
		OFlag = 0x800000,
		DFlag = 0x1000000
	}
}
