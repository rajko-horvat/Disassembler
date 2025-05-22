using Disassembler.CPU;
using System.Text;

namespace Disassembler.CPU.OpCodes
{
	public class OpCodeInstructionDefinition
	{
		private CPUInstructionPrefixEnum ePrefix = CPUInstructionPrefixEnum.Undefined;
		private CPUInstructionEnum eInstruction = CPUInstructionEnum.Undefined;
		private string sDescription = "";
		private List<OpCodeDefinition> aOpCodes = new List<OpCodeDefinition>();
		private CPUFlagsEnum eModifiedFlags = CPUFlagsEnum.Undefined;
		private CPUFlagsEnum eClearedFlags = CPUFlagsEnum.Undefined;
		private CPUFlagsEnum eSetFlags = CPUFlagsEnum.Undefined;
		private CPUFlagsEnum eUndefinedFlags = CPUFlagsEnum.Undefined;
		private CPUTypeEnum eCPU = CPUTypeEnum.Undefined;

		public OpCodeInstructionDefinition(string name, string description, string opCode,
			string cpu, string modifiedFlags, string undefinedFlags)
		{
			switch (name.ToUpper())
			{
				case "REP":
				case "REPE":
					this.ePrefix = CPUInstructionPrefixEnum.REPE;
					break;
				case "REPNE":
					this.ePrefix = CPUInstructionPrefixEnum.REPNE;
					break;
				case "LOCK":
					this.ePrefix = CPUInstructionPrefixEnum.Lock;
					break;
				case "PREFIXADDR":
					this.ePrefix = CPUInstructionPrefixEnum.AddressSize;
					break;
				case "PREFIXOPSIZE":
					this.ePrefix = CPUInstructionPrefixEnum.OperandSize;
					break;
				case "PREFIXES":
					this.ePrefix = CPUInstructionPrefixEnum.ES;
					break;
				case "PREFIXCS":
					this.ePrefix = CPUInstructionPrefixEnum.CS;
					break;
				case "PREFIXSS":
					this.ePrefix = CPUInstructionPrefixEnum.SS;
					break;
				case "PREFIXDS":
					this.ePrefix = CPUInstructionPrefixEnum.DS;
					break;
				case "PREFIXFS":
					this.ePrefix = CPUInstructionPrefixEnum.FS;
					break;
				case "PREFIXGS":
					this.ePrefix = CPUInstructionPrefixEnum.GS;
					break;
				default:
					this.eInstruction = (CPUInstructionEnum)Enum.Parse(typeof(CPUInstructionEnum), name);
					break;
			}

			this.sDescription = description;

			// parse opcode
			string[] opCodes = opCode.Split(',');
			for (int i = 0; i < opCodes.Length; i++)
			{
				string sTemp = opCodes[i].Trim();
				if (!string.IsNullOrEmpty(sTemp))
				{
					this.aOpCodes.Add(new OpCodeDefinition(sTemp));
				}
			}

			if (!string.IsNullOrEmpty(modifiedFlags))
			{
				string[] aFlags = modifiedFlags.Split(',');
				for (int i = 0; i < aFlags.Length; i++)
				{
					string flag = aFlags[i].Trim();
					if (flag.EndsWith("=0"))
					{
						this.eClearedFlags |= (CPUFlagsEnum)Enum.Parse(typeof(CPUFlagsEnum), flag.Substring(0, flag.Length - 2));
					}
					else if (flag.EndsWith("=1"))
					{
						this.eSetFlags |= (CPUFlagsEnum)Enum.Parse(typeof(CPUFlagsEnum), flag.Substring(0, flag.Length - 2));
					}
					else if (flag.Equals("All"))
					{
						this.eModifiedFlags = CPUFlagsEnum.All;
					}
					else
					{
						this.eModifiedFlags |= (CPUFlagsEnum)Enum.Parse(typeof(CPUFlagsEnum), flag);
					}
				}
			}

			if (!string.IsNullOrEmpty(undefinedFlags))
			{
				string[] aFlags = undefinedFlags.Split(',');
				for (int i = 0; i < aFlags.Length; i++)
				{
					this.eUndefinedFlags |= (CPUFlagsEnum)Enum.Parse(typeof(CPUFlagsEnum), aFlags[i].Trim());
				}
			}

			this.eCPU = (CPUTypeEnum)Enum.Parse(typeof(CPUTypeEnum), "i" + cpu);
		}

		public CPUInstructionPrefixEnum Prefix
		{
			get
			{
				return this.ePrefix;
			}
		}

		public CPUInstructionEnum Instruction
		{
			get
			{
				return this.eInstruction;
			}
		}

		public string Description
		{
			get
			{
				return this.sDescription;
			}
		}

		public List<OpCodeDefinition> OpCodes
		{
			get
			{
				return this.aOpCodes;
			}
		}

		public CPUFlagsEnum ModifiedFlags
		{
			get
			{
				return this.eModifiedFlags;
			}
		}

		public CPUFlagsEnum ClearedFlags
		{
			get
			{
				return this.eClearedFlags;
			}
		}

		public CPUFlagsEnum SetFlags
		{
			get
			{
				return this.eSetFlags;
			}
		}

		public CPUFlagsEnum UndefinedFlags
		{
			get
			{
				return this.eUndefinedFlags;
			}
		}

		public CPUTypeEnum CPU
		{
			get
			{
				return this.eCPU;
			}
		}

		public static string FlagsToString(CPUFlagsEnum flags)
		{
			StringBuilder sbFlags = new StringBuilder();
			int value = (int)flags;
			int count = 0;

			foreach (int i in Enum.GetValues(typeof(CPUFlagsEnum)))
			{
				if (i != 0 && (value & i) == i)
				{
					if (count > 0)
						sbFlags.Append(" | ");

					sbFlags.AppendFormat("FlagsEnum.{0}", Enum.GetName(typeof(CPUFlagsEnum), i));
					count++;
				}
			}
			if (count == 0)
				sbFlags.AppendFormat("FlagsEnum.{0}", Enum.GetName(typeof(CPUFlagsEnum), 0));

			return sbFlags.ToString();
		}
	}
}
