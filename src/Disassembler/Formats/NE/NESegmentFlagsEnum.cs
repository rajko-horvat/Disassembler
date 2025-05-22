namespace Disassembler.Formats.NE
{
	[Flags]
	public enum NESegmentFlagsEnum
	{
		None = 0,
		DataSegment = 1,
		MemoryAllocated = 1 << 1,
		SegmentLoaded = 1 << 2,
		Reserved3 = 1 << 3,
		SegmentMoveable = 1 << 4,
		SegmentShareable = 1 << 5,
		PreloadSegment = 1 << 6,
		ExecuteOrReadOnly = 1 << 7,
		ContainsRelocationData = 1 << 8,
		Reserved9 = 1 << 9,
		Reserved10 = 1 << 10,
		Reserved11 = 1 << 11,
		SegmentDiscardable = 1 << 12,
		Reserved13 = 1 << 13,
		Reserved14 = 1 << 14,
		Reserved15 = 1 << 15
	}
}
