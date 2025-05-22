namespace Disassembler.CPU
{
	public enum CPUInstructionPrefixEnum
	{
		Undefined,
		Lock,
		OperandSize,
		AddressSize,
		REPE,
		REPNE,
		ES,
		CS,
		SS,
		DS,
		FS,
		GS
	}
}
