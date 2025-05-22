namespace Disassembler.Formats.OMF
{
	public enum OMFFixupLocationTypeEnum
	{
		Undefined = -1,
		LowOrderByte = 0,
		Offset16bit = 1,
		Base16bit = 2,
		LongPointer32bit = 3,
		HighOrderByte = 4,
		Offset16bit_1 = 5,
		NotSupported6 = 6,
		NotSupported7 = 7,
		NotSupported8 = 8,
		Offset32bit = 9,
		NotSupported10 = 10,
		Pointer48bit = 11,
		NotSupported12 = 12,
		Offset32bit_1 = 13,
		NotSupported14 = 14,
		NotSupported15 = 15
	}
}
