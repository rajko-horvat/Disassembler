using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Disassembler
{
	[Flags]
	public enum ProgramSegmentTypeEnum
	{
		None = 0,
		Code = 1,
		Data = 2
	}
}
