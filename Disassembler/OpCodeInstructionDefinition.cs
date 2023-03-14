using System;
using System.Collections.Generic;
using System.Text;

namespace Disassembler
{
	public class OpCodeInstructionDefinition
	{
		private InstructionPrefixEnum ePrefix = InstructionPrefixEnum.Undefined;
		private InstructionEnum eInstruction = InstructionEnum.Undefined;
		private string sDescription = null;
		private List<OpCodeDefinition> aOpCodes = new List<OpCodeDefinition>();
		private FlagsEnum eModifiedFlags = FlagsEnum.Undefined;
		private FlagsEnum eClearedFlags = FlagsEnum.Undefined;
		private FlagsEnum eSetFlags = FlagsEnum.Undefined;
		private FlagsEnum eUndefinedFlags = FlagsEnum.Undefined;
		private CPUEnum eCPU = CPUEnum.Undefined;

		public OpCodeInstructionDefinition(string name, string description, string opCode,
			string cpu, string modifiedFlags, string undefinedFlags)
		{
			switch (name.ToUpper())
			{
				case "REP":
				case "REPE":
					this.ePrefix = InstructionPrefixEnum.REPE;
					break;
				case "REPNE":
					this.ePrefix = InstructionPrefixEnum.REPNE;
					break;
				case "LOCK":
					this.ePrefix = InstructionPrefixEnum.Lock;
					break;
				case "PREFIXADDR":
					this.ePrefix = InstructionPrefixEnum.AddressSize;
					break;
				case "PREFIXOPSIZE":
					this.ePrefix = InstructionPrefixEnum.OperandSize;
					break;
				case "PREFIXES":
					this.ePrefix = InstructionPrefixEnum.ES;
					break;
				case "PREFIXCS":
					this.ePrefix = InstructionPrefixEnum.CS;
					break;
				case "PREFIXSS":
					this.ePrefix = InstructionPrefixEnum.SS;
					break;
				case "PREFIXDS":
					this.ePrefix = InstructionPrefixEnum.DS;
					break;
				case "PREFIXFS":
					this.ePrefix = InstructionPrefixEnum.FS;
					break;
				case "PREFIXGS":
					this.ePrefix = InstructionPrefixEnum.GS;
					break;
				default:
					this.eInstruction = (InstructionEnum)Enum.Parse(typeof(InstructionEnum), name);
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
						this.eClearedFlags |= (FlagsEnum)Enum.Parse(typeof(FlagsEnum), flag.Substring(0, flag.Length - 2));
					}
					else if (flag.EndsWith("=1"))
					{
						this.eSetFlags |= (FlagsEnum)Enum.Parse(typeof(FlagsEnum), flag.Substring(0, flag.Length - 2));
					}
					else if (flag.Equals("All"))
					{
						this.eModifiedFlags = FlagsEnum.All;
					}
					else
					{
						this.eModifiedFlags |= (FlagsEnum)Enum.Parse(typeof(FlagsEnum), flag);
					}
				}
			}

			if (!string.IsNullOrEmpty(undefinedFlags))
			{
				string[] aFlags = undefinedFlags.Split(',');
				for (int i = 0; i < aFlags.Length; i++)
				{
					this.eUndefinedFlags |= (FlagsEnum)Enum.Parse(typeof(FlagsEnum), aFlags[i].Trim());
				}
			}

			this.eCPU = (CPUEnum)Enum.Parse(typeof(CPUEnum), "i" + cpu);
		}

		public InstructionPrefixEnum Prefix
		{
			get
			{
				return this.ePrefix;
			}
		}

		public InstructionEnum Instruction
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

		public FlagsEnum ModifiedFlags
		{
			get
			{
				return this.eModifiedFlags;
			}
		}

		public FlagsEnum ClearedFlags
		{
			get
			{
				return this.eClearedFlags;
			}
		}

		public FlagsEnum SetFlags
		{
			get
			{
				return this.eSetFlags;
			}
		}

		public FlagsEnum UndefinedFlags
		{
			get
			{
				return this.eUndefinedFlags;
			}
		}

		public CPUEnum CPU
		{
			get
			{
				return this.eCPU;
			}
		}

		public static string FlagsToString(FlagsEnum flags)
		{
			StringBuilder sbFlags = new StringBuilder();
			int value = (int)flags;
			int count = 0;

			foreach (int i in Enum.GetValues(typeof(FlagsEnum)))
			{
				if (i != 0 && (value & i) == i)
				{
					if (count > 0)
						sbFlags.Append(" | ");

					sbFlags.AppendFormat("FlagsEnum.{0}", Enum.GetName(typeof(FlagsEnum), i));
					count++;
				}
			}
			if (count == 0)
				sbFlags.AppendFormat("FlagsEnum.{0}", Enum.GetName(typeof(FlagsEnum), 0));

			return sbFlags.ToString();
		}
	}
}
