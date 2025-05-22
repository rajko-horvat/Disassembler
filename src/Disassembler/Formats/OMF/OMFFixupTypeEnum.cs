using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Disassembler.Formats.OMF
{
	public enum OMFFixupTypeEnum
	{
		Undefined = -1,
		TargetThread = 0,
		FrameThread = 1,
		Fixup = 2
	}
}
