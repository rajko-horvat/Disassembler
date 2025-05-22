namespace Disassembler.CPU
{
	public enum CPUParameterTypeEnum
	{
		Undefined,
		Immediate,
		SegmentOffset,
		Register,
		SegmentRegister,
		Condition,
		MemoryAddress,
		LEAMemoryAddress,
		FPUStackAddress,
		LocalVariable,
		LocalVariableWithSI,
		LocalVariableWithDI,
		LocalParameter,
		LocalParameterWithSI,
		LocalParameterWithDI,
		LocalArray
	}
}
