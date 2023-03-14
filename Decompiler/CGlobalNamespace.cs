using IRB.Collections.Generic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Disassembler.Decompiler
{
	public class CGlobalNamespace
	{
		private CDecompiler oParent;
		private BDictionary<string, CType> aGlobalTypes = new BDictionary<string, CType>();
		private List<CVariable> aVariables = new List<CVariable>();

		private BDictionary<string, CFunction> aFunctions = new BDictionary<string, CFunction>();
		private BDictionary<string, CFunction> aAPIFunctions = new BDictionary<string, CFunction>();

		public CGlobalNamespace(CDecompiler parent)
		{
			this.oParent = parent;
		}

		public CDecompiler Parent
		{
			get { return this.oParent; }
		}

		public BDictionary<string, CType> GlobalTypes
		{
			get { return this.aGlobalTypes; }
		}

		public List<CVariable> Variables
		{
			get { return this.aVariables; }
		}

		public BDictionary<string, CFunction> Functions
		{
			get { return this.aFunctions; }
		}

		public BDictionary<string, CFunction> APIFunctions
		{
			get { return this.aAPIFunctions; }
		}
	}
}
