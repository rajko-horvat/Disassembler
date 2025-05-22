using Disassembler.CPU;
using Disassembler.Formats.MZ;
using Disassembler.Formats.OMF;
using IRB.Collections.Generic;

namespace Disassembler
{
	public class MainProgram
	{
		private MZExecutable oExecutable;
		private List<ModuleMatch> aLibraryMatches;
		private BDictionary<uint, ProgramSegment> programSegments = new();
		private uint defaultDS = 0;

		public MainProgram(MZExecutable executable, List<ModuleMatch> matches)
		{
			this.oExecutable = executable;
			this.aLibraryMatches = matches;

			//MainProgramFunction function = new MainProgramFunction(this, MainProgramFunctionCallTypeEnum.Cdecl, "DivisionByZero", 
			//	new List<Variable>(), CPUParameterSizeEnum.Undefined, 0, 0, 0);
		}

		public ProgramFunction? FindFunction(ushort overlay, ushort segment, ushort offset)
		{
			uint absoluteSegment = ToAbsoluteSegment(overlay, segment);

			// look into function list
			if (this.programSegments.ContainsKey(absoluteSegment) && this.programSegments.GetValueByKey(absoluteSegment).Functions.ContainsKey(offset))
			{
				return this.programSegments.GetValueByKey(absoluteSegment).Functions.GetValueByKey(offset);
			}

			if (overlay == 0)
			{
				uint absoluteAddress = MainProgram.ToLinearAddress(segment, offset);

				// look into library matches
				for (int i = 0; i < this.aLibraryMatches.Count; i++)
				{
					ModuleMatch match = this.aLibraryMatches[i];

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

									fnSegment.Functions.Add(offset, function);

									Console.WriteLine($"Adding undefined API function {function.Parent.ToString()}.{function.Name}()");

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

			if (this.programSegments.ContainsKey(absoluteSegment))
			{
				return this.programSegments.GetValueByKey(absoluteSegment);
			}
			else
			{
				ProgramSegment newSegment = new ProgramSegment(this, absoluteSegment);

				this.programSegments.Add(absoluteSegment, newSegment);

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
			MemoryStream reader = new MemoryStream(this.oExecutable.Data);
			
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

		public MZExecutable Executable
		{
			get
			{
				return this.oExecutable;
			}
		}

		public List<ModuleMatch> LibraryMatches
		{
			get
			{
				return this.aLibraryMatches;
			}
		}

		public BDictionary<uint, ProgramSegment> Segments
		{
			get => this.programSegments;
		}

		public uint DefaultDS
		{
			get => this.defaultDS;
			set => this.defaultDS = value;
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
