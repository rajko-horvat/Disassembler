namespace Disassembler.CPU
{
	[Flags]
	public enum CPUSegmentRegisterEnum
	{
		Invalid = 0x0,
		ES = 0x1,
		CS = 0x2,
		SS = 0x4,
		DS = 0x8,
		FS = 0x10,
		GS = 0x20,
		Undefined = 0x40 // For segment overrides
	}
}
