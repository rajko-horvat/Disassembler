using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Disassembler.Formats.NE
{
	[Flags]
	public enum NEContentFlagsEnum
	{
		SingleDataSegment = 1,
		MultipleDataSegments = 1 << 1,
		Reserved2 = 1 << 2,
		Reserved3 = 1 << 3,
		Undefined4 = 1 << 4,
		Undefined5 = 1 << 5,
		Undefined6 = 1 << 6,
		Undefined7 = 1 << 7,
		Reserved8 = 1 << 8,
		Reserved9 = 1 << 9,
		Undefined10 = 1 << 10,
		FirstSegmentIsLoader = 1 << 11,
		Undefined12 = 1 << 12,
		LinkErrors = 1 << 13,
		Reserved14 = 1 << 14,
		LibraryModule = 1 << 15
	}
}
