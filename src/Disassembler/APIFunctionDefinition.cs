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
		private ILVariable[] parameters;
		private ILVariable returnValue;

		public APIFunctionDefinition(string name, ILVariable[] parameters, ILVariable returnValue)
		{
			this.name = name;
			this.parameters = parameters;
			this.returnValue = returnValue;
		}

		public string Name { get => this.name; }

		public ILVariable[] Parameters { get => this.parameters; }

		public ILVariable ReturnValue { get => this.returnValue; }
	}
}
