using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Disassembler
{
    public enum ILBaseValueTypeEnum
    {
		Void,
		UInt8,
		Int8,
		UInt16,
		Int16,
		UInt32,
		Int32,
		Ptr16,
		Ptr32,
		FnPtr32,
		Struct,
		Union,
		DirectObject
	}
}
