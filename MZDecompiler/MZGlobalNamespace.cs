using IRB.Collections.Generic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Disassembler.Decompiler
{
	public class MZGlobalNamespace
	{
		private MZDecompiler oParent;
		private BDictionary<string, CType> aGlobalTypes = new BDictionary<string, CType>();
		private List<CVariable> aVariables = new List<CVariable>();

		private BDictionary<string, MZFunction> aFunctions = new BDictionary<string, MZFunction>();
		private BDictionary<string, MZFunction> aAPIFunctions = new BDictionary<string, MZFunction>();

		public MZGlobalNamespace(MZDecompiler parent)
		{
			this.oParent = parent;
		}

		public MZDecompiler Parent
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

		public BDictionary<string, MZFunction> Functions
		{
			get { return this.aFunctions; }
		}

		public BDictionary<string, MZFunction> APIFunctions
		{
			get { return this.aAPIFunctions; }
		}
	}
}
