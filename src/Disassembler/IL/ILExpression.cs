using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Disassembler
{
    public abstract class ILExpression
    {
		public abstract string ToCSString();
    }
}
