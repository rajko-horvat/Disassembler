using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Disassembler
{
	public struct APIFunctionDefinition
	{
		private string name;
		private ProgramFunctionOptionsEnum functionOptions = ProgramFunctionOptionsEnum.Cdecl;
		private ILVariable[] parameters;
		private ILVariable returnValue;

		public APIFunctionDefinition(string name, ProgramFunctionOptionsEnum functionOptions, ILVariable[] parameters, ILVariable returnValue)
		{
			this.name = name;
			this.functionOptions = functionOptions;
			this.parameters = parameters;
			this.returnValue = returnValue;
		}

		public string Name { get => this.name; }

		private ProgramFunctionOptionsEnum FunctionOptions { get => this.functionOptions; }

		public ILVariable[] Parameters { get => this.parameters; }

		public ILVariable ReturnValue { get => this.returnValue; }
	}
}
