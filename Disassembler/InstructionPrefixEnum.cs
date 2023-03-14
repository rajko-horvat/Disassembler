using System;

namespace Disassembler
{
	public enum InstructionPrefixEnum
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
