namespace Disassembler.Formats.OMF
{
	public enum OMFSegmentAlignmentEnum
	{
		Absolute = 0,
		RelocatableAlignByte = 1,
		RelocatableAlignWord = 2,
		RelocatableAlignParagraph = 3,
		RelocatableAlignPage = 4,
		RelocatableAlignDWord = 5,
		NotSupported = 6,
		NotDefined = 7
	}
}
