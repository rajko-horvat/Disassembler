using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Disassembler.Formats.OMF
{
	public enum OMFFixupModeEnum
	{
		Undefined = -1,
		SelfRelative = 0,
		SegmentRelative = 1
	}
}
