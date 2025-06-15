using Disassembler.CPU;
using Disassembler.Formats.MZ;
using Disassembler.Formats.OMF;
using IRB.Collections.Generic;

namespace Disassembler
{
	public class MainProgram
	{
		// base types
		private ILValueType voidValueType = ILValueType.Void;
		private ILValueType charValueType = new("char", ILBaseValueTypeEnum.Int8);
		private ILValueType unsignedCharValueType = new("unsigned char", ILBaseValueTypeEnum.UInt8);
		private ILValueType intValueType = new("int", ILBaseValueTypeEnum.Int16);
		private ILValueType unsignedIntValueType = new("unsigned int", ILBaseValueTypeEnum.UInt16);
		private ILValueType longValueType = new("long", ILBaseValueTypeEnum.Int32);
		private ILValueType unsignedLongValueType = new("unsigned long", ILBaseValueTypeEnum.UInt32);
		private ILValueType stringValueType = new("char*", ILBaseValueTypeEnum.Ptr16, new ILValueType(ILBaseValueTypeEnum.Int8));

		private MZExecutable executable;
		private List<ModuleMatch> libraryMatches;
		private BDictionary<uint, ProgramSegment> segments = new();
		private uint defaultDS = 0;
		private BDictionary<string, APIFunctionDefinition> apiFunctions = new();
		private BDictionary<string, ILValueType> customValueTypes = new();

		public MainProgram(MZExecutable executable, List<ModuleMatch> matches)
		{
			this.executable = executable;
			this.libraryMatches = matches;

			//MainProgramFunction function = new MainProgramFunction(this, MainProgramFunctionCallTypeEnum.Cdecl, "DivisionByZero", 
			//	new List<Variable>(), CPUParameterSizeEnum.Undefined, 0, 0, 0);
		}

		public ProgramFunction? FindFunction(ushort overlay, ushort segment, ushort offset)
		{
			uint absoluteSegment = ToAbsoluteSegment(overlay, segment);

			// look into function list
			if (this.segments.ContainsKey(absoluteSegment) && this.segments.GetValueByKey(absoluteSegment).Functions.ContainsKey(offset))
			{
				return this.segments.GetValueByKey(absoluteSegment).Functions.GetValueByKey(offset);
			}

			// try to find API function
			if (overlay == 0)
			{
				uint absoluteAddress = MainProgram.ToLinearAddress(segment, offset);

				// look into library matches
				for (int i = 0; i < this.libraryMatches.Count; i++)
				{
					ModuleMatch match = this.libraryMatches[i];

					if (absoluteAddress >= match.LinearAddress && absoluteAddress < match.LinearAddress + match.Length)
					{
						OMFOBJModule module = match.Module;

						for (int j = 0; j < module.PublicNames.Count; j++)
						{
							OMFPublicNameDefinition publicNameDef = module.PublicNames[j];

							for (int k = 0; k < publicNameDef.PublicNames.Count; k++)
							{
								if (match.LinearAddress + publicNameDef.PublicNames[k].Value == absoluteAddress)
								{
									string sName = publicNameDef.PublicNames[k].Key;
									sName = sName.StartsWith("_") ? sName.Substring(1) : sName;

									// add missing function
									ProgramSegment fnSegment = FindOrCreateSegment(overlay, segment);
									ProgramFunction function = new ProgramFunction(fnSegment, offset, sName);

									function.IsLibraryFunction = true;

									fnSegment.Functions.Add(offset, function);

									if (this.apiFunctions.ContainsKey(sName))
									{
										APIFunctionDefinition apiFunction = this.apiFunctions.GetValueByKey(sName);

										function.Parameters.Clear();
										// !!! Will have to introduce program memory model (Small, Medium, Large...)
										int parameterOffset = 6; // (function.CallType & ProgramFunctionTypeEnum.Far) == ProgramFunctionTypeEnum.Far ? 6 : 4;

										for (int l = 0; l < apiFunction.Parameters.Length; l++)
										{
											ILVariable parameter = apiFunction.Parameters[l];

											parameter.Offset = parameterOffset;
											function.Parameters.Add(parameterOffset, parameter);

											// minimum stack parameter size is 2
											parameterOffset += Math.Max(2, parameter.ValueType.SizeOf);
										}

										function.ReturnValue = apiFunction.ReturnValue;
									}
									else
									{
										Console.WriteLine($"Adding undefined API function {function.ParentSegment.Name}.{function.Name}()");
									}

									return function;
								}
							}
						}
					}
				}
			}

			return null;
		}

		public ProgramSegment FindOrCreateSegment(ushort overlay, ushort segment)
		{
			uint absoluteSegment = ToAbsoluteSegment(overlay, segment);

			if (this.segments.ContainsKey(absoluteSegment))
			{
				return this.segments.GetValueByKey(absoluteSegment);
			}
			else
			{
				ProgramSegment newSegment = new ProgramSegment(this, absoluteSegment);

				this.segments.Add(absoluteSegment, newSegment);

				return newSegment;
			}
		}

		public ProgramFunction Disassemble(ushort overlay, ushort segment, ushort offset, string? name)
		{
			ProgramFunction? function = FindFunction(overlay, segment, offset);

			if (function == null)
			{
				ProgramSegment fnSegment = FindOrCreateSegment(overlay, segment);
				function = new ProgramFunction(fnSegment, offset, name);

				fnSegment.Functions.Add(offset, function);

				function.Disassemble();
			}

			return function;
		}

		public void DisassembleOverlay()
		{
			MemoryStream reader = new MemoryStream(this.executable.Data);
			
			reader.Position = 0x30;

			ushort usCount = MZExecutable.ReadUInt16(reader);
			ushort[] aOffsets = new ushort[usCount];

			for (int i = 0; i < usCount; i++)
			{
				aOffsets[i] = MZExecutable.ReadUInt16(reader);
			}
			reader.Close();

			for (int i = 0; i < usCount; i++)
			{
				this.Disassemble(0, 0, aOffsets[i], null);
			}
			Console.WriteLine("--- Overlay offsets");
			for (int i = 0; i < usCount; i++)
			{
				Console.WriteLine($"0x{aOffsets[i]:x4}");
			}
			Console.WriteLine("--- End overlay offsets");
		}

		public void AssignOrdinals()
		{
			// enumerate Main segments and their functions
			uint[] segmentOffsets = this.segments.Keys.ToArray();
			int segmentOrdinal = 0;
			int overlayOrdinal = 0;

			Array.Sort(segmentOffsets);


			for (int i = 0; i < segmentOffsets.Length; i++)
			{
				ProgramSegment segment = this.segments.GetValueByKey(segmentOffsets[i]);

				// differentiate between overlay and segments from main exe
				if (segment.CPUOverlay > 0)
				{
					segment.Ordinal = overlayOrdinal++;
				}
				else
				{
					segment.Ordinal = segmentOrdinal++;
				}

				ushort[] functionOffsets = segment.Functions.Keys.ToArray();

				Array.Sort(functionOffsets);

				for (int j = 0; j < functionOffsets.Length; j++)
				{
					ProgramFunction function = segment.Functions.GetValueByKey(functionOffsets[j]);

					function.Ordinal = j + 1;
				}
			}

			for (int i = 0; i < this.segments.Count; i++)
			{
				ProgramSegment segment = this.segments[i].Value;

				for (int j = 0; j < segment.Functions.Count; j++)
				{
					ProgramFunction function = segment.Functions[j].Value;

					if (function.FlowGraph != null)
					{
						function.FlowGraph.TranslateFunction();
					}
				}
			}
		}

		public MZExecutable Executable
		{
			get
			{
				return this.executable;
			}
		}

		public List<ModuleMatch> LibraryMatches
		{
			get
			{
				return this.libraryMatches;
			}
		}

		public BDictionary<uint, ProgramSegment> Segments
		{
			get => this.segments;
		}

		public BDictionary<string, APIFunctionDefinition> APIFunctions { get => this.apiFunctions; }

		public uint DefaultDS
		{
			get => this.defaultDS;
			set => this.defaultDS = value;
		}

		public ILValueType VoidValueType { get => this.voidValueType; }

		public ILValueType CharValueType { get => this.charValueType; }

		public ILValueType UnsignedCharValueType { get => this.unsignedCharValueType; }

		public ILValueType IntValueType { get => this.intValueType; }

		public ILValueType UnsignedIntValueType { get => this.unsignedIntValueType; }

		public ILValueType LongValueType { get => this.longValueType; }

		public ILValueType UnsignedLongValueType { get => this.unsignedLongValueType; }

		public ILValueType StringValueType { get => this.stringValueType; }

		public BDictionary<string, ILValueType> CustomValueTypes { get => this.customValueTypes; }

		public ILValueType FromCPUParameterSizeEnum(CPUParameterSizeEnum parameterType)
		{
			switch (parameterType)
			{
				case CPUParameterSizeEnum.UInt8:
					return this.UnsignedCharValueType;

				case CPUParameterSizeEnum.UInt16:
					// the default is Int16
					return this.intValueType;

				case CPUParameterSizeEnum.UInt32:
					// the default is Int32
					return this.longValueType;

				default:
					return ILValueType.Void;
			}
		}

		public static uint ToAbsoluteSegment(ushort overlay, ushort segment)
		{
			return (uint)(((uint)overlay << 16) + segment);
		}

		public static ushort ToCPUSegment(uint segment)
		{
			return (ushort)(segment & 0xffff);
		}

		public static ushort ToCPUOverlay(uint segment)
		{
			return (ushort)((segment & 0xffff0000) >> 16);
		}

		public static uint ToLinearAddress(ushort segment, ushort offset)
		{
			return (uint)(((uint)segment << 4) + offset);
		}

		public static uint ToLinearAddress(ushort segment, uint offset)
		{
			return (uint)(((uint)segment << 4) + offset);
		}
	}
}
