using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Disassembler.Formats.OMF
{
	public enum OMFTargetMethodEnum
	{
		Undefined = -1,
		SegDefIndex = 0,
		GrpDefIndex = 1,
		ExtDefIndex = 2,
		NotSupported3 = 3,
		SegDefIndexNoDisplacement = 4,
		GrpDefIndexNoDisplacement = 5,
		ExtDefIndexNoDisplacement = 6,
		NotSupported7 = 7
	}
}
