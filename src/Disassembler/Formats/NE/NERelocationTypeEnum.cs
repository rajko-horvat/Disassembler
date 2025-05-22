using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Disassembler.Formats.NE
{
	public enum NERelocationTypeEnum
	{
		InternalReference,
		ImportedOrdinal,
		ImportedName,
		OSFixup,
		Additive,
		FPFixup
	}
}
