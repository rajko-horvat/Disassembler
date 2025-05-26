using Disassembler;
using Disassembler.CPU;
using Disassembler.Formats.MZ;
using Disassembler.Formats.OMF;
using IRB.Collections.Generic;
using System.Linq;
using System.Reflection;

internal class Program
{
	private static void Main(string[] args)
	{
		string? path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

		//OpCodeTable.ParseTable();

		path = string.IsNullOrEmpty(path) ? "" : path;

		ParseDOSEXE(path, 2);
	}

	private static void ParseDOSEXE(string path, int version)
	{
		string outputPath = path + $"\\Output";
		string codePath = outputPath + $"\\Code{version}";
		string dataPath = outputPath + $"\\Data{version}";

		if (!Directory.Exists(outputPath))
			Directory.CreateDirectory(outputPath);
		if (!Directory.Exists(codePath))
			Directory.CreateDirectory(codePath);
		if (!Directory.Exists(dataPath))
			Directory.CreateDirectory(dataPath);

		// process Main code
		Console.WriteLine("Loading EXE");
		MZExecutable mainEXE = new MZExecutable($@"..\..\..\..\..\..\Civilization Games\Civ I\Dos\CIV{version}.EXE");

		int baseAddress = mainEXE.InitialCS * 16;
		ushort newDS = mainEXE.ReadUInt16(baseAddress + mainEXE.InitialIP + 0x2);
		ushort newSP = mainEXE.InitialSP;
		ushort mainCS;
		ushort mainIP;
		ushort tempIP = mainEXE.ReadUInt16(baseAddress + mainEXE.InitialIP + 0x4f);
		ushort dataCutoffOffset = 0;

		if (mainEXE.Data[baseAddress + tempIP + 0x1f] == 0x81 && mainEXE.Data[baseAddress + tempIP + 0x20] == 0xc4 &&
			mainEXE.Data[baseAddress + tempIP + 0x67] == 0x16 && mainEXE.Data[baseAddress + tempIP + 0x68] == 0x7 &&
			mainEXE.Data[baseAddress + tempIP + 0x69] == 0xfc && mainEXE.Data[baseAddress + tempIP + 0x6a] == 0xbf &&
			mainEXE.Data[baseAddress + tempIP + 0x97] == 0x9a)
		{
			// we have the correct offset
			newSP += mainEXE.ReadUInt16(baseAddress + tempIP + 0x21);
			newSP &= 0xfffe;

			mainCS = mainEXE.ReadUInt16(baseAddress + tempIP + 0x9a);
			mainIP = mainEXE.ReadUInt16(baseAddress + tempIP + 0x98);

			// This is the offset where data segment gets filled with zeroes
			dataCutoffOffset = mainEXE.ReadUInt16(baseAddress + tempIP + 0x6b);
		}
		else
		{
			throw new Exception("File is corrupted");
		}

		Console.WriteLine("Saving data segment");
		FileStream dataFile = new FileStream($"{dataPath}\\Data.bin", FileMode.Create, FileAccess.ReadWrite);
		int length = Math.Min(mainEXE.Data.Length - (int)MainProgram.ToLinearAddress(newDS, 0), newSP - 1);
		dataFile.Write(mainEXE.Data, (int)MainProgram.ToLinearAddress(newDS, 0), length);
		dataFile.Close();

		Console.WriteLine("Loading library MLIBC7");
		OMFLibrary library1 = new OMFLibrary(@"..\..\..\..\..\..\DOS Compilers\MSC\v5\Installed\MSC\LIB\MLIBC7.LIB");

		// unique set of segment names
		BHashSet<string> segmentNames = new BHashSet<string>();
		BHashSet<string> groupNames = new BHashSet<string>();

		for (int i = 0; i < library1.Modules.Count; i++)
		{
			OMFOBJModule module = library1.Modules[i];

			for (int j = 0; j < module.DataRecords.Count; j++)
			{
				OMFDataRecord data = module.DataRecords[j];
				if (data.Segment != null)
				{
					segmentNames.Add(data.Segment.Name);
				}
			}

			for (int j = 0; j < module.SegmentGroups.Count; j++)
			{
				OMFSegmentGroupDefinition group = module.SegmentGroups[j];
				groupNames.Add(group.Name);
			}
		}

		Console.WriteLine("Used segment names:");
		for (int i = 0; i < segmentNames.Count; i++)
		{
			Console.WriteLine(segmentNames[i]);
		}

		Console.WriteLine("Group names:");
		for (int i = 0; i < groupNames.Count; i++)
		{
			Console.WriteLine(groupNames[i]);
		}

		List<ModuleMatch> libraryMatches = new List<ModuleMatch>();
		Console.WriteLine("Matching MLIBC7");
		OMFLibrary.MatchLibrary(library1, mainEXE, libraryMatches);

		MainProgram mainProgram = new MainProgram(mainEXE, libraryMatches);
		mainProgram.DefaultDS = newDS;

		Console.WriteLine("Disassembling");

		// we start by decompiling Main function
		ProgramFunction function = mainProgram.Disassemble(0, mainCS, mainIP, "Main");
		function.ReturnType = CPUParameterSizeEnum.UInt16;

		// a way to dissassemble functions which are referenced by a pointer, like interrupt functions and such
		switch (version)
		{
			case 1:
				// Timer handler referenced by Seg0, Fn1
				mainProgram.Disassemble(0, 0x0000, 0x199, "TimerHandler");

				// Error handler referenced by Seg0, Fn23
				mainProgram.Disassemble(0, 0x0000, 0x70a, "ErrorHandler");

				// Mouse handler referenced by Seg0, Fn92
				mainProgram.Disassemble(0, 0x0000, 0x17cd, "MouseHandler");

				// Referenced by Seg6; Pointers to functions in Seg6
				mainProgram.Disassemble(0, 0x0860, 0x1139, null);
				mainProgram.Disassemble(0, 0x0860, 0x1250, null);
				mainProgram.Disassemble(0, 0x0860, 0x132a, null);
				mainProgram.Disassemble(0, 0x0860, 0x1379, null);
				mainProgram.Disassemble(0, 0x0860, 0x13c8, null);
				mainProgram.Disassemble(0, 0x0860, 0x1472, null);
				mainProgram.Disassemble(0, 0x0860, 0x15e0, null);
				mainProgram.Disassemble(0, 0x0860, 0x1646, null);

				// Referenced by Seg7, Fn2; Pointers to functions in Seg6
				mainProgram.Disassemble(0, 0x0860, 0x1613, null);

				// Referenced by Seg12, Fn1; Pointers to functions in Seg6
				mainProgram.Disassemble(0, 0x0860, 0x0ee0, null);
				mainProgram.Disassemble(0, 0x0860, 0x1563, null);

				break;

			case 2:
				// Timer handler referenced by Seg0, Fn1
				mainProgram.Disassemble(0, 0x0000, 0x199, "TimerHandler");

				// Error handler referenced by Seg0, Fn23
				mainProgram.Disassemble(0, 0x0000, 0x70a, "ErrorHandler");

				// Mouse handler referenced by Seg0, Fn92
				mainProgram.Disassemble(0, 0x0000, 0x17cd, "MouseHandler");

				// Referenced by Seg6; Pointers to functions in Seg6
				mainProgram.Disassemble(0, 0x0860, 0x1139, null);
				mainProgram.Disassemble(0, 0x0860, 0x1250, null);
				mainProgram.Disassemble(0, 0x0860, 0x132a, null);
				mainProgram.Disassemble(0, 0x0860, 0x1379, null);
				mainProgram.Disassemble(0, 0x0860, 0x13c8, null);
				mainProgram.Disassemble(0, 0x0860, 0x1472, null);
				mainProgram.Disassemble(0, 0x0860, 0x15e0, null);
				mainProgram.Disassemble(0, 0x0860, 0x1646, null);

				// Referenced by Seg7, Fn2; Pointers to functions in Seg6
				mainProgram.Disassemble(0, 0x0860, 0x1613, null);

				// Referenced by Seg12, Fn1; Pointers to functions in Seg6
				mainProgram.Disassemble(0, 0x0860, 0x0ee0, null);
				mainProgram.Disassemble(0, 0x0860, 0x1563, null);

				break;

			case 3:
				// Timer handler referenced by Seg0, Fn1
				mainProgram.Disassemble(0, 0x0000, 0x1a7, "TimerHandler");

				// Error handler referenced by Seg0, Fn23
				mainProgram.Disassemble(0, 0x0000, 0x718, "ErrorHandler");

				// Mouse handler referenced by Seg0, Fn92
				mainProgram.Disassemble(0, 0x0000, 0x17db, "MouseHandler");

				// Referenced by Seg6; Pointers to functions in Seg6
				mainProgram.Disassemble(0, 0x07e6, 0x1165, null);
				mainProgram.Disassemble(0, 0x07e6, 0x127c, null);
				mainProgram.Disassemble(0, 0x07e6, 0x1356, null);
				mainProgram.Disassemble(0, 0x07e6, 0x13a5, null);
				mainProgram.Disassemble(0, 0x07e6, 0x13f4, null);
				mainProgram.Disassemble(0, 0x07e6, 0x149e, null);
				mainProgram.Disassemble(0, 0x07e6, 0x160c, null);
				mainProgram.Disassemble(0, 0x07e6, 0x1672, null);

				// Referenced by Seg7, Fn2; Pointers to functions in Seg6
				mainProgram.Disassemble(0, 0x07e6, 0x163f, null);

				// Referenced by Seg12, Fn1; Pointers to functions in Seg6
				mainProgram.Disassemble(0, 0x07e6, 0x0f0c, null);
				mainProgram.Disassemble(0, 0x07e6, 0x158f, null);

				// Referenced by Seg19, Fn10
				mainProgram.Disassemble(0, 0x1f20, 0x063a, null);

				break;

			case 4:
				// Timer handler referenced by Seg0, Fn1
				mainProgram.Disassemble(0, 0x0000, 0x1a7, "TimerHandler");

				// Error handler referenced by Seg0, Fn23
				mainProgram.Disassemble(0, 0x0000, 0x718, "ErrorHandler");

				// Mouse handler referenced by Seg0, Fn92
				mainProgram.Disassemble(0, 0x0000, 0x17db, "MouseHandler");

				// Referenced by Seg6; Pointers to functions in Seg6
				mainProgram.Disassemble(0, 0x07e6, 0x1165, null);
				mainProgram.Disassemble(0, 0x07e6, 0x127c, null);
				mainProgram.Disassemble(0, 0x07e6, 0x1356, null);
				mainProgram.Disassemble(0, 0x07e6, 0x13a5, null);
				mainProgram.Disassemble(0, 0x07e6, 0x13f4, null);
				mainProgram.Disassemble(0, 0x07e6, 0x149e, null);
				mainProgram.Disassemble(0, 0x07e6, 0x160c, null);
				mainProgram.Disassemble(0, 0x07e6, 0x1672, null);

				// Referenced by Seg7, Fn2; Pointers to functions in Seg6
				mainProgram.Disassemble(0, 0x07e6, 0x163f, null);

				// Referenced by Seg12, Fn1; Pointers to functions in Seg6
				mainProgram.Disassemble(0, 0x07e6, 0x0f0c, null);
				mainProgram.Disassemble(0, 0x07e6, 0x158f, null);

				// Referenced by Seg19, Fn10
				mainProgram.Disassemble(0, 0x1f20, 0x0640, null);

				break;

			case 5:
				// Timer handler referenced by Seg0, Fn1
				mainProgram.Disassemble(0, 0x0000, 0x1a7, "TimerHandler");

				// Error handler referenced by Seg0, Fn23
				mainProgram.Disassemble(0, 0x0000, 0x718, "ErrorHandler");

				// Mouse handler referenced by Seg0, Fn92
				mainProgram.Disassemble(0, 0x0000, 0x17db, "MouseHandler");

				// Referenced by Seg6; Pointers to functions in Seg6
				mainProgram.Disassemble(0, 0x0866, 0x1169, null);
				mainProgram.Disassemble(0, 0x0866, 0x1280, null);
				mainProgram.Disassemble(0, 0x0866, 0x135a, null);
				mainProgram.Disassemble(0, 0x0866, 0x13a9, null);
				mainProgram.Disassemble(0, 0x0866, 0x13f8, null);
				mainProgram.Disassemble(0, 0x0866, 0x14a2, null);
				mainProgram.Disassemble(0, 0x0866, 0x1610, null);
				mainProgram.Disassemble(0, 0x0866, 0x1676, null);

				// Referenced by Seg7, Fn2; Pointers to functions in Seg6
				mainProgram.Disassemble(0, 0x0866, 0x1643, null);

				// Regerenced by Seg12, Fn1; Pointers to functions in Seg6
				mainProgram.Disassemble(0, 0x0866, 0x0f10, null);
				mainProgram.Disassemble(0, 0x0866, 0x1593, null);

				// Referenced by Seg19, Fn10
				mainProgram.Disassemble(0, 0x1fa1, 0x0644, null);

				break;
		}

		Console.WriteLine("Writing code");

		StreamWriter objectWriter = new StreamWriter($"{codePath}\\ObjectsAsm.cs");
		StreamWriter initWriter = new StreamWriter($"{codePath}\\InitsAsm.cs");
		StreamWriter getWriter = new StreamWriter($"{codePath}\\GettersAms.cs");

		// enumerate Main segments and their functions
		uint[] segmentOffsets = mainProgram.Segments.Keys.ToArray();

		Array.Sort(segmentOffsets);

		for (int i = 0; i < segmentOffsets.Length; i++)
		{
			ProgramSegment segment = mainProgram.Segments.GetValueByKey(segmentOffsets[i]);
			segment.Ordinal = i;

			ushort[] functionOffsets = segment.Functions.Keys.ToArray();

			Array.Sort(functionOffsets);

			for (int j = 0; j < functionOffsets.Length; j++)
			{
				segment.Functions.GetValueByKey(functionOffsets[j]).Ordinal = j + 1;
			}
		}

		// emit Main code
		for (int i = 0; i < segmentOffsets.Length; i++)
		{
			ProgramSegment segment = mainProgram.Segments.GetValueByKey(segmentOffsets[i]);

			segment.WriteAsmCS(codePath, 0);

			objectWriter.WriteLine($"private {segment.ToString()} o{segment.ToString()};");
			initWriter.WriteLine($"this.o{segment.ToString()} = new {segment.ToString()}(this);");
			getWriter.WriteLine($"public {segment.ToString()} {segment.ToString()}");
			getWriter.WriteLine("{");
			getWriter.WriteLine($"\tget {{ return this.o{segment.ToString()};}}");
			getWriter.WriteLine("}");
			getWriter.WriteLine();
		}

		/*int iMaxOverlaySize = 0;
		for (int i = 0; i < mainEXE.Overlays.Count; i++)
		{
			iMaxOverlaySize = Math.Max(iMaxOverlaySize, mainEXE.Overlays[i].Data.Length);
		}
		Console.WriteLine($"Maximum overlay size in bytes: 0x{iMaxOverlaySize:x4}");*/

		// Emit flow graphs
		if (mainProgram.Segments.ContainsKey(0x0))
		{
			ProgramSegment segment = mainProgram.Segments.GetValueByKey(0x0);

			if (segment.Functions.ContainsKey(0x1080))
			{
				function = segment.Functions.GetValueByKey(0x1080);

				if (function.Graph != null)
				{
					//function.Graph.ConstructGraph();
					function.Graph.WriteGraphDOT("test.gv");
				}
			}
		}

		/*Console.WriteLine();
		Console.WriteLine("Translating code to IL");
		if (mainProgram.Segments.ContainsKey(0x181))
		{
			ProgramSegment segment = mainProgram.Segments.GetValueByKey(0x181);

			for (int i = 0; i < segment.Functions.Count; i++)
			{
				//segment.Functions[i].Value.TranslateToIL();
			}
		}*/

		// emit Main API functions
		/*List<ProgramFunction> aFunctions = new List<ProgramFunction>();

		for (int i = 0; i < mainProgram.GlobalNamespace.APIFunctions.Count; i++)
		{
			aFunctions.Add(mainProgram.GlobalNamespace.APIFunctions[i].Value);
		}

		// emit Main API code
		mainProgram.WriteCode(@"Out\Code\MSCAPI.cs", aFunctions);
		objectWriter.WriteLine("private MSCAPI oMSCAPI;");
		initWriter.WriteLine("this.oMSCAPI = new MSCAPI(this);");
		getWriter.WriteLine("public MSCAPI MSCAPI");
		getWriter.WriteLine("{");
		getWriter.WriteLine("\tget { return this.oMSCAPI;}");
		getWriter.WriteLine("}");*/

		// Misc Driver
		/*Console.WriteLine("Processing overlay Misc");
		MZExecutable oMiscEXE = new MZExecutable(@"..\..\..\..\..\..\Civilization Games\Civ I\Dos\Installed\misc.exe");
		Disassembler.MainProgram oMiscDisassembler = new Disassembler.MainProgram(oMiscEXE, aMatches);

		oMiscDisassembler.DisassembleOverlay();

		// Emit Misc functions
		aFunctions = new List<MainProgramFunction>();
		for (int i = 0; i < oMiscDisassembler.GlobalNamespace.Functions.Count; i++)
		{
			aFunctions.Add(oMiscDisassembler.GlobalNamespace.Functions[i].Value);
		}

		oMiscDisassembler.WriteCode(@"Out\Code\Misc.cs", aFunctions);
		objectWriter.WriteLine("private Misc oMisc;");
		initWriter.WriteLine("this.oMisc = new Misc(this);");
		getWriter.WriteLine();
		getWriter.WriteLine("public Misc Misc");
		getWriter.WriteLine("{");
		getWriter.WriteLine("\tget { return this.oMisc;}");
		getWriter.WriteLine("}");

		// VGA Driver
		Console.WriteLine("Processing overlay VGA");
		MZExecutable oVGAEXE = new MZExecutable(@"..\..\..\..\..\..\Civilization Games\Civ I\Dos\Installed\mgraphic.exe");
		Disassembler.MainProgram oVGADisassembler = new Disassembler.MainProgram(oVGAEXE, aMatches);

		oVGADisassembler.DisassembleOverlay();

		// Emit egraphic functions
		aFunctions = new List<MainProgramFunction>();
		for (int i = 0; i < oVGADisassembler.GlobalNamespace.Functions.Count; i++)
		{
			aFunctions.Add(oVGADisassembler.GlobalNamespace.Functions[i].Value);
		}

		oVGADisassembler.WriteCode(@"Out\Code\VGADriver.cs", aFunctions);
		objectWriter.WriteLine("private VGADriver oVGA;");
		initWriter.WriteLine("this.oVGA = new VGADriver(this);");
		getWriter.WriteLine();
		getWriter.WriteLine("public VGADriver VGA");
		getWriter.WriteLine("{");
		getWriter.WriteLine("\tget { return this.oVGA;}");
		getWriter.WriteLine("}");

		// NSound Driver
		Console.WriteLine("Processing overlay NSound");
		MZExecutable oNSEXE = new MZExecutable(@"..\..\..\..\..\Civ_I_Game\Dos\Installed\nsound.cvl");
		MZDisassembler oNSoundDisassembler = new MZDisassembler(oNSEXE, aMatches);

		oNSoundDisassembler.DisassembleOverlay();

		// Emit nsound functions
		aFunctions = new List<MZFunction>();
		for (int i = 0; i < oNSoundDisassembler.GlobalNamespace.Functions.Count; i++)
		{
			aFunctions.Add(oNSoundDisassembler.GlobalNamespace.Functions[i].Value);
		}

		oNSoundDisassembler.WriteCode(@"Out\Code\NSound.cs", aFunctions);
		writer.WriteLine("private NSound oNSound;");
		writer1.WriteLine("this.oNSound = new NSound(this);");
		writer2.WriteLine();
		writer2.WriteLine("public NSound NSound");
		writer2.WriteLine("{");
		writer2.WriteLine("\tget { return this.oNSound;}");
		writer2.WriteLine("}");

		// GSound Driver
		Console.WriteLine("Processing overlay GSound");
		MZExecutable oGSoundEXE = new MZExecutable(@"..\..\..\..\..\..\Civilization Games\Civ I\Dos\Installed\GSOUND.CVL");
		Disassembler.MainProgram oGSoundDisassembler = new Disassembler.MainProgram(oGSoundEXE, aMatches);

		oGSoundDisassembler.DisassembleOverlay();

		// Emit GSound functions
		aFunctions = new List<MainProgramFunction>();
		for (int i = 0; i < oGSoundDisassembler.GlobalNamespace.Functions.Count; i++)
		{
			aFunctions.Add(oGSoundDisassembler.GlobalNamespace.Functions[i].Value);
		}

		oGSoundDisassembler.WriteCode(@"Out\Code\GSound.cs", aFunctions);
		objectWriter.WriteLine("private GSound oGSound;");
		initWriter.WriteLine("this.oGSound = new GSound(this);");
		getWriter.WriteLine();
		getWriter.WriteLine("public GSound GSound");
		getWriter.WriteLine("{");
		getWriter.WriteLine("\tget { return this.oGSound;}");
		getWriter.WriteLine("}");*/

		getWriter.Close();
		initWriter.Close();
		objectWriter.Close();

		Console.WriteLine("Processing finished");
	}
}