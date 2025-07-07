using Disassembler;
using Disassembler.CPU;
using Disassembler.Formats.MZ;
using Disassembler.Formats.OMF;
using IRB.Collections.Generic;
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
		string outputPath = path + $"{Path.DirectorySeparatorChar}Output";
		string codePath = outputPath + $"{Path.DirectorySeparatorChar}Code{version}";
		string dataPath = outputPath + $"{Path.DirectorySeparatorChar}Data{version}";

		if (!Directory.Exists(outputPath))
			Directory.CreateDirectory(outputPath);
		if (!Directory.Exists(codePath))
			Directory.CreateDirectory(codePath);
		if (!Directory.Exists(dataPath))
			Directory.CreateDirectory(dataPath);

		// process Main code
		Console.WriteLine("Loading EXE");

		MZExecutable mainEXE = new(string.Format("..{0}..{0}..{0}..{0}..{0}..{0}Civilization Games{0}Civ I{0}Dos{0}CIV{1}.EXE", 
			Path.DirectorySeparatorChar, version));

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
		FileStream dataFile = new($"{dataPath}/Data.bin", FileMode.Create, FileAccess.ReadWrite);
		int length = Math.Min(mainEXE.Data.Length - (int)MainProgram.ToLinearAddress(newDS, 0), newSP - 1);
		dataFile.Write(mainEXE.Data, (int)MainProgram.ToLinearAddress(newDS, 0), length);
		dataFile.Close();

		Console.WriteLine("Loading library MLIBC7");
        OMFLibrary library1 = new(string.Format("..{0}..{0}..{0}..{0}..{0}..{0}DOS Compilers{0}MSC{0}v5{0}Installed{0}MSC{0}LIB{0}MLIBC7.LIB",
            Path.DirectorySeparatorChar));

        // unique set of segment names
        BHashSet<string> segmentNames = [];
		BHashSet<string> groupNames = [];

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

		/*Console.WriteLine("Used segment names:");
		for (int i = 0; i < segmentNames.Count; i++)
		{
			Console.WriteLine(segmentNames[i]);
		}

		Console.WriteLine("Group names:");
		for (int i = 0; i < groupNames.Count; i++)
		{
			Console.WriteLine(groupNames[i]);
		}*/

		List<ModuleMatch> libraryMatches = [];
		Console.WriteLine("Matching MLIBC7");
		OMFLibrary.MatchLibrary(library1, mainEXE, libraryMatches);

		MainProgram mainProgram = new(mainEXE, libraryMatches);
		mainProgram.DefaultDS = newDS;

		#region MS C 5.1 API functions

		// Word registers
		ILValueType wordRegs = new("WORDREGS", ILBaseValueTypeEnum.Struct);
		wordRegs.MemberObjects.Add(new("ax", ILBaseValueTypeEnum.UInt16));
		wordRegs.MemberObjects.Add(new("bx", ILBaseValueTypeEnum.UInt16));
		wordRegs.MemberObjects.Add(new("cx", ILBaseValueTypeEnum.UInt16));
		wordRegs.MemberObjects.Add(new("dx", ILBaseValueTypeEnum.UInt16));
		wordRegs.MemberObjects.Add(new("si", ILBaseValueTypeEnum.UInt16));
		wordRegs.MemberObjects.Add(new("di", ILBaseValueTypeEnum.UInt16));
		wordRegs.MemberObjects.Add(new("cflag", ILBaseValueTypeEnum.UInt16));
		mainProgram.CustomValueTypes.Add(wordRegs.TypeName, wordRegs);

		// Byte registers
		ILValueType byteRegs = new("BYTEREGS", ILBaseValueTypeEnum.Struct);
		byteRegs.MemberObjects.Add(new("al", ILBaseValueTypeEnum.UInt8));
		byteRegs.MemberObjects.Add(new("ah", ILBaseValueTypeEnum.UInt8));
		byteRegs.MemberObjects.Add(new("bl", ILBaseValueTypeEnum.UInt8));
		byteRegs.MemberObjects.Add(new("bh", ILBaseValueTypeEnum.UInt8));
		byteRegs.MemberObjects.Add(new("cl", ILBaseValueTypeEnum.UInt8));
		byteRegs.MemberObjects.Add(new("ch", ILBaseValueTypeEnum.UInt8));
		byteRegs.MemberObjects.Add(new("dl", ILBaseValueTypeEnum.UInt8));
		byteRegs.MemberObjects.Add(new("dh", ILBaseValueTypeEnum.UInt8));
		mainProgram.CustomValueTypes.Add(byteRegs.TypeName, byteRegs);

		// Registers union (Overlays the corresponding word and byte registers)
		ILValueType regs = new("REGS", ILBaseValueTypeEnum.Union);
		regs.MemberObjects.Add(new("x", ILBaseValueTypeEnum.Struct, wordRegs));
		regs.MemberObjects.Add(new("h", ILBaseValueTypeEnum.Struct, byteRegs));
		mainProgram.CustomValueTypes.Add(regs.TypeName, regs);

		// Define FILE structure as a single pointer
		ILValueType file = new("FILE", ILBaseValueTypeEnum.DirectObject);
		mainProgram.CustomValueTypes.Add(file.TypeName, file);

		ILValueType filePtr = new("FILE *", ILBaseValueTypeEnum.Ptr16, file);
		mainProgram.CustomValueTypes.Add(filePtr.TypeName, filePtr);

		ILValueType diskinfo_t = new("diskinfo_t", ILBaseValueTypeEnum.Struct);
		diskinfo_t.MemberObjects.Add(new("drive", ILBaseValueTypeEnum.UInt16));
		diskinfo_t.MemberObjects.Add(new("head", ILBaseValueTypeEnum.UInt16));
		diskinfo_t.MemberObjects.Add(new("track", ILBaseValueTypeEnum.UInt16));
		diskinfo_t.MemberObjects.Add(new("sector", ILBaseValueTypeEnum.UInt16));
		diskinfo_t.MemberObjects.Add(new("nsectors", ILBaseValueTypeEnum.UInt16));
		diskinfo_t.MemberObjects.Add(new("buffer", ILBaseValueTypeEnum.Ptr32));
		mainProgram.CustomValueTypes.Add(diskinfo_t.TypeName, diskinfo_t);

		// int int86(int intnum, union _REGS * inregs, union _REGS * outregs );
		mainProgram.APIFunctions.Add("int86", new APIFunctionDefinition("int86", ProgramFunctionOptionsEnum.Cdecl,
			[new("intnum", ILVariableScopeEnum.LocalParameter, mainProgram.IntValueType, 6, 10),
				new("inregs", ILVariableScopeEnum.LocalParameter, new(ILBaseValueTypeEnum.Ptr16, regs), 8, 10),
				new("outregs", ILVariableScopeEnum.LocalParameter, new(ILBaseValueTypeEnum.Ptr16, regs), 10, 10)], 
			new(ILVariableScopeEnum.ReturnValue, mainProgram.IntValueType, 0, 10)));

		// int intdos(union REGS * inregs, union REGS * outregs);
		mainProgram.APIFunctions.Add("intdos", new APIFunctionDefinition("intdos", ProgramFunctionOptionsEnum.Cdecl,
			[new("inregs", ILVariableScopeEnum.LocalParameter, new(ILBaseValueTypeEnum.Ptr16, regs), 6, 10),
				new("outregs", ILVariableScopeEnum.LocalParameter, new(ILBaseValueTypeEnum.Ptr16, regs), 8, 10)],
			new(ILVariableScopeEnum.ReturnValue, mainProgram.IntValueType, 0, 10)));

		// void (_cdecl _interrupt far * _dos_getvect(unsigned intnum))();
		mainProgram.APIFunctions.Add("_dos_getvect", new APIFunctionDefinition("_dos_getvect", ProgramFunctionOptionsEnum.Cdecl,
			[new("intnum", ILVariableScopeEnum.LocalParameter, mainProgram.UnsignedIntValueType, 6, 10)],
			new(ILVariableScopeEnum.ReturnValue, new(ILBaseValueTypeEnum.Ptr32), 0, 10)));

		// void _dos_setvect(unsigned intnum, void (_cdecl _interrupt far * handler)());
		mainProgram.APIFunctions.Add("_dos_setvect", new APIFunctionDefinition("_dos_setvect", ProgramFunctionOptionsEnum.Cdecl,
			[new("intnum", ILVariableScopeEnum.LocalParameter, mainProgram.UnsignedIntValueType, 6, 10),
				new("handler", ILVariableScopeEnum.LocalParameter, new(ILBaseValueTypeEnum.Ptr32), 8, 10)],
			new(ILVariableScopeEnum.ReturnValue, mainProgram.VoidValueType, 0, 10)));

		// unsigned _dos_open(const char * path, unsigned mode, int * handle);
		mainProgram.APIFunctions.Add("_dos_open", new APIFunctionDefinition("_dos_open", ProgramFunctionOptionsEnum.Cdecl,
			[new("path", ILVariableScopeEnum.LocalParameter, mainProgram.StringValueType, 6, 10),
				new("mode", ILVariableScopeEnum.LocalParameter, mainProgram.UnsignedIntValueType, 8, 10),
				new("handle", ILVariableScopeEnum.LocalParameter, new(ILBaseValueTypeEnum.Ptr16, mainProgram.IntValueType), 10, 10)],
			new(ILVariableScopeEnum.ReturnValue, mainProgram.UnsignedIntValueType, 0, 10)));

		// unsigned _dos_close(int handle);
		mainProgram.APIFunctions.Add("_dos_close", new APIFunctionDefinition("_dos_close", ProgramFunctionOptionsEnum.Cdecl,
			[new("handle", ILVariableScopeEnum.LocalParameter, mainProgram.IntValueType, 6, 10)],
			new(ILVariableScopeEnum.ReturnValue, mainProgram.UnsignedIntValueType, 0, 10)));

		// unsigned _dos_read(int handle, void far *buffer, unsigned count, unsigned * numread);
		mainProgram.APIFunctions.Add("_dos_read", new APIFunctionDefinition("_dos_read", ProgramFunctionOptionsEnum.Cdecl,
			[new("handle", ILVariableScopeEnum.LocalParameter, mainProgram.IntValueType, 6, 10),
				new("buffer", ILVariableScopeEnum.LocalParameter, new(ILBaseValueTypeEnum.Ptr32, mainProgram.VoidValueType), 8, 10),
				new("count", ILVariableScopeEnum.LocalParameter, mainProgram.UnsignedIntValueType, 12, 10),
				new("numread", ILVariableScopeEnum.LocalParameter, new(ILBaseValueTypeEnum.Ptr16, mainProgram.UnsignedIntValueType), 14, 10)],
			new(ILVariableScopeEnum.ReturnValue, mainProgram.UnsignedIntValueType, 0, 10)));

		// unsigned _dos_freemem(unsigned seg); 
		mainProgram.APIFunctions.Add("_dos_freemem", new APIFunctionDefinition("_dos_freemem", ProgramFunctionOptionsEnum.Cdecl,
			[new("seg", ILVariableScopeEnum.LocalParameter, mainProgram.UnsignedIntValueType, 6, 10)],
			new(ILVariableScopeEnum.ReturnValue, mainProgram.UnsignedIntValueType, 0, 10)));

		// void _dos_getdrive(unsigned * drive);
		mainProgram.APIFunctions.Add("_dos_getdrive", new APIFunctionDefinition("_dos_getdrive", ProgramFunctionOptionsEnum.Cdecl,
			[new("drive", ILVariableScopeEnum.LocalParameter, new(ILBaseValueTypeEnum.Ptr16, mainProgram.UnsignedIntValueType), 6, 10)],
			new(ILVariableScopeEnum.ReturnValue, mainProgram.VoidValueType, 0, 10)));

		// unsigned _bios_disk(unsigned service, struct _diskinfo_t * diskinfo);
		mainProgram.APIFunctions.Add("_bios_disk", new APIFunctionDefinition("_bios_disk", ProgramFunctionOptionsEnum.Cdecl,
			[new("service", ILVariableScopeEnum.LocalParameter, mainProgram.UnsignedIntValueType, 6, 10),
				new("diskinfo", ILVariableScopeEnum.LocalParameter, new(ILBaseValueTypeEnum.Ptr16, diskinfo_t), 8, 10),],
			new(ILVariableScopeEnum.ReturnValue, mainProgram.UnsignedIntValueType, 0, 10)));

		// FILE * fopen(const char * filename, const char * mode);
		mainProgram.APIFunctions.Add("fopen", new APIFunctionDefinition("fopen", ProgramFunctionOptionsEnum.Cdecl,
			[new("filename", ILVariableScopeEnum.LocalParameter, mainProgram.StringValueType, 6, 10),
				new("mode", ILVariableScopeEnum.LocalParameter, mainProgram.StringValueType, 8, 10)],
			new(ILVariableScopeEnum.ReturnValue, filePtr, 0, 10)));

		// int fscanf(FILE * stream, const char * format [, argument ]...);
		mainProgram.APIFunctions.Add("fscanf", new APIFunctionDefinition("fscanf", ProgramFunctionOptionsEnum.Cdecl | ProgramFunctionOptionsEnum.VariableArguments,
			[new("stream", ILVariableScopeEnum.LocalParameter, filePtr, 6, 10),
				new("format", ILVariableScopeEnum.LocalParameter, mainProgram.StringValueType, 8, 10),
				new("args", ILVariableScopeEnum.LocalParameter, new(ILBaseValueTypeEnum.Ptr16, mainProgram.VoidValueType), 10, 10, true)],
			new(ILVariableScopeEnum.ReturnValue, mainProgram.IntValueType, 0, 10)));

		// unsigned int fread(void * buffer, unsigned int size, unsigned int count, FILE * stream);
		mainProgram.APIFunctions.Add("fread", new APIFunctionDefinition("fread", ProgramFunctionOptionsEnum.Cdecl,
			[new("buffer", ILVariableScopeEnum.LocalParameter, new(ILBaseValueTypeEnum.Ptr16, mainProgram.VoidValueType), 6, 10),
				new("size", ILVariableScopeEnum.LocalParameter, mainProgram.UnsignedIntValueType, 8, 10),
				new("count", ILVariableScopeEnum.LocalParameter, mainProgram.UnsignedIntValueType, 10, 10),
				new("stream", ILVariableScopeEnum.LocalParameter, filePtr, 12, 10)],
			new(ILVariableScopeEnum.ReturnValue, mainProgram.UnsignedIntValueType, 0, 10)));

		// unsigned int fwrite(const void * buffer, unsigned int size, unsigned int count, FILE * stream);
		mainProgram.APIFunctions.Add("fwrite", new APIFunctionDefinition("fwrite", ProgramFunctionOptionsEnum.Cdecl,
			[new("buffer", ILVariableScopeEnum.LocalParameter, new(ILBaseValueTypeEnum.Ptr16, mainProgram.VoidValueType), 6, 10),
				new("size", ILVariableScopeEnum.LocalParameter, mainProgram.UnsignedIntValueType, 8, 10),
				new("count", ILVariableScopeEnum.LocalParameter, mainProgram.UnsignedIntValueType, 10, 10),
				new("stream", ILVariableScopeEnum.LocalParameter, filePtr, 12, 10)],
			new(ILVariableScopeEnum.ReturnValue, mainProgram.UnsignedIntValueType, 0, 10)));

		// long ftell(FILE * stream);
		mainProgram.APIFunctions.Add("ftell", new APIFunctionDefinition("ftell", ProgramFunctionOptionsEnum.Cdecl,
			[new("stream", ILVariableScopeEnum.LocalParameter, filePtr, 6, 10)],
			new(ILVariableScopeEnum.ReturnValue, mainProgram.LongValueType, 0, 10)));

		// int fseek(FILE * stream, long offset, int origin);
		mainProgram.APIFunctions.Add("fseek", new APIFunctionDefinition("fseek", ProgramFunctionOptionsEnum.Cdecl,
			[new("stream", ILVariableScopeEnum.LocalParameter, filePtr, 6, 10),
				new("offset", ILVariableScopeEnum.LocalParameter, mainProgram.LongValueType, 8, 10),
				new("origin", ILVariableScopeEnum.LocalParameter, mainProgram.IntValueType, 12, 10)],
			new(ILVariableScopeEnum.ReturnValue, mainProgram.IntValueType, 0, 10)));

		// int fclose(FILE *stream);
		mainProgram.APIFunctions.Add("fclose", new APIFunctionDefinition("fclose", ProgramFunctionOptionsEnum.Cdecl,
			[new("stream", ILVariableScopeEnum.LocalParameter, filePtr, 6, 10)],
			new(ILVariableScopeEnum.ReturnValue, mainProgram.IntValueType, 0, 10)));

		// int open(const char * filename, int oflag [, int pmode]); 
		mainProgram.APIFunctions.Add("open", new APIFunctionDefinition("open", ProgramFunctionOptionsEnum.Cdecl,
			[new("filename", ILVariableScopeEnum.LocalParameter, mainProgram.StringValueType, 6, 10),
				new("oflag", ILVariableScopeEnum.LocalParameter, mainProgram.IntValueType, 8, 10),
				new("pmode", ILVariableScopeEnum.LocalParameter, mainProgram.IntValueType, 10, 10)],
			new(ILVariableScopeEnum.ReturnValue, mainProgram.IntValueType, 0, 10)));

		// int read(int handle, void * buffer, unsigned int count);
		mainProgram.APIFunctions.Add("read", new APIFunctionDefinition("read", ProgramFunctionOptionsEnum.Cdecl,
			[new("handle", ILVariableScopeEnum.LocalParameter, mainProgram.IntValueType, 6, 10),
				new("buffer", ILVariableScopeEnum.LocalParameter, new(ILBaseValueTypeEnum.Ptr16, mainProgram.VoidValueType), 8, 10),
				new("count", ILVariableScopeEnum.LocalParameter, mainProgram.UnsignedIntValueType, 10, 10)],
			new(ILVariableScopeEnum.ReturnValue, mainProgram.IntValueType, 0, 10)));

		// int write(int handle, void * buffer, unsigned int count);
		mainProgram.APIFunctions.Add("write", new APIFunctionDefinition("write", ProgramFunctionOptionsEnum.Cdecl,
			[new("handle", ILVariableScopeEnum.LocalParameter, mainProgram.IntValueType, 6, 10),
				new("buffer", ILVariableScopeEnum.LocalParameter, new(ILBaseValueTypeEnum.Ptr16, mainProgram.VoidValueType), 8, 10),
				new("count", ILVariableScopeEnum.LocalParameter, mainProgram.UnsignedIntValueType, 10, 10)],
			new(ILVariableScopeEnum.ReturnValue, mainProgram.IntValueType, 0, 10)));

		// long lseek(int handle, long offset, int origin);
		mainProgram.APIFunctions.Add("lseek", new APIFunctionDefinition("lseek", ProgramFunctionOptionsEnum.Cdecl,
			[new("handle", ILVariableScopeEnum.LocalParameter, mainProgram.IntValueType, 6, 10),
				new("offset", ILVariableScopeEnum.LocalParameter, mainProgram.LongValueType, 8, 10),
				new("origin", ILVariableScopeEnum.LocalParameter, mainProgram.IntValueType, 12, 10)],
			new(ILVariableScopeEnum.ReturnValue, mainProgram.LongValueType, 0, 10)));

		// int close(int handle);
		mainProgram.APIFunctions.Add("close", new APIFunctionDefinition("close", ProgramFunctionOptionsEnum.Cdecl,
			[new("handle", ILVariableScopeEnum.LocalParameter, mainProgram.IntValueType, 6, 10)],
			new(ILVariableScopeEnum.ReturnValue, mainProgram.IntValueType, 0, 10)));

		// char * strcpy(char * string1, const char * string2);
		mainProgram.APIFunctions.Add("strcpy", new APIFunctionDefinition("strcpy", ProgramFunctionOptionsEnum.Cdecl,
			[new("string1", ILVariableScopeEnum.LocalParameter, mainProgram.StringValueType, 6, 10),
				new("string2", ILVariableScopeEnum.LocalParameter, mainProgram.StringValueType, 8, 10)],
			new(ILVariableScopeEnum.ReturnValue, mainProgram.StringValueType, 0, 10)));

		// char * strcat(char * string1, const char * string2);
		mainProgram.APIFunctions.Add("strcat", new APIFunctionDefinition("strcat", ProgramFunctionOptionsEnum.Cdecl,
			[new("string1", ILVariableScopeEnum.LocalParameter, mainProgram.StringValueType, 6, 10),
				new("string2", ILVariableScopeEnum.LocalParameter, mainProgram.StringValueType, 8, 10)],
			new(ILVariableScopeEnum.ReturnValue, mainProgram.StringValueType, 0, 10)));

		// unsigned int strlen(const char * string);
		mainProgram.APIFunctions.Add("strlen", new APIFunctionDefinition("strlen", ProgramFunctionOptionsEnum.Cdecl,
			[new("string", ILVariableScopeEnum.LocalParameter, mainProgram.StringValueType, 6, 10)],
			new(ILVariableScopeEnum.ReturnValue, mainProgram.UnsignedIntValueType, 0, 10)));

		// int strnicmp(const char * string1, const char * string2, unsigned int count);
		mainProgram.APIFunctions.Add("strnicmp", new APIFunctionDefinition("strnicmp", ProgramFunctionOptionsEnum.Cdecl,
			[new("string1", ILVariableScopeEnum.LocalParameter, mainProgram.StringValueType, 6, 10),
				new("string2", ILVariableScopeEnum.LocalParameter, mainProgram.StringValueType, 8, 10),
				new("count", ILVariableScopeEnum.LocalParameter, mainProgram.UnsignedIntValueType, 10, 10)],
			new(ILVariableScopeEnum.ReturnValue, mainProgram.IntValueType, 0, 10)));

		// int stricmp(const char * string1, const char * string2);
		mainProgram.APIFunctions.Add("stricmp", new APIFunctionDefinition("stricmp", ProgramFunctionOptionsEnum.Cdecl,
			[new("string1", ILVariableScopeEnum.LocalParameter, mainProgram.StringValueType, 6, 10),
				new("string2", ILVariableScopeEnum.LocalParameter, mainProgram.StringValueType, 8, 10)],
			new(ILVariableScopeEnum.ReturnValue, mainProgram.IntValueType, 0, 10)));

		// char * strstr(const char * string1, const char * string2);
		mainProgram.APIFunctions.Add("strstr", new APIFunctionDefinition("strstr", ProgramFunctionOptionsEnum.Cdecl,
			[new("string1", ILVariableScopeEnum.LocalParameter, mainProgram.StringValueType, 6, 10),
				new("string2", ILVariableScopeEnum.LocalParameter, mainProgram.StringValueType, 8, 10)],
			new(ILVariableScopeEnum.ReturnValue, mainProgram.StringValueType, 0, 10)));

		// char * strupr(char * string);
		mainProgram.APIFunctions.Add("strupr", new APIFunctionDefinition("strupr", ProgramFunctionOptionsEnum.Cdecl,
			[new("string", ILVariableScopeEnum.LocalParameter, mainProgram.StringValueType, 6, 10)],
			new(ILVariableScopeEnum.ReturnValue, mainProgram.StringValueType, 0, 10)));

		// char * itoa(int value, char * string, int radix);
		mainProgram.APIFunctions.Add("itoa", new APIFunctionDefinition("itoa", ProgramFunctionOptionsEnum.Cdecl,
			[new("value", ILVariableScopeEnum.LocalParameter, mainProgram.IntValueType, 6, 10),
				new("string", ILVariableScopeEnum.LocalParameter, mainProgram.StringValueType, 8, 10),
				new("radix", ILVariableScopeEnum.LocalParameter, mainProgram.IntValueType, 10, 10)],
			new(ILVariableScopeEnum.ReturnValue, mainProgram.StringValueType, 0, 10)));

		// void * memcpy(void * dest, const void * src, unsigned int count);
		mainProgram.APIFunctions.Add("memcpy", new APIFunctionDefinition("memcpy", ProgramFunctionOptionsEnum.Cdecl,
			[new("dest", ILVariableScopeEnum.LocalParameter, new(ILBaseValueTypeEnum.Ptr16, mainProgram.VoidValueType), 6, 10),
				new("src", ILVariableScopeEnum.LocalParameter, new(ILBaseValueTypeEnum.Ptr16, mainProgram.VoidValueType), 8, 10),
				new("count", ILVariableScopeEnum.LocalParameter, mainProgram.UnsignedIntValueType, 10, 10)],
			new(ILVariableScopeEnum.ReturnValue, new(ILBaseValueTypeEnum.Ptr16, mainProgram.VoidValueType), 0, 10)));

		// void movedata(unsigned int srcseg, unsigned int srcoff, unsigned int destseg, unsigned int destoff, unsigned int count);
		mainProgram.APIFunctions.Add("movedata", new APIFunctionDefinition("movedata", ProgramFunctionOptionsEnum.Cdecl,
			[new("srcseg", ILVariableScopeEnum.LocalParameter, mainProgram.UnsignedIntValueType, 6, 10),
				new("srcoff", ILVariableScopeEnum.LocalParameter, mainProgram.UnsignedIntValueType, 8, 10),
				new("destseg", ILVariableScopeEnum.LocalParameter, mainProgram.UnsignedIntValueType, 10, 10),
				new("destoff", ILVariableScopeEnum.LocalParameter, mainProgram.UnsignedIntValueType, 12, 10),
				new("count", ILVariableScopeEnum.LocalParameter, mainProgram.UnsignedIntValueType, 14, 10)],
			new(ILVariableScopeEnum.ReturnValue, mainProgram.VoidValueType, 0, 10)));

		// void * memset(void * dest, int c, unsigned int count);
		mainProgram.APIFunctions.Add("memset", new APIFunctionDefinition("memset", ProgramFunctionOptionsEnum.Cdecl,
			[new("dest", ILVariableScopeEnum.LocalParameter, new(ILBaseValueTypeEnum.Ptr16, mainProgram.VoidValueType), 6, 10),
				new("c", ILVariableScopeEnum.LocalParameter, mainProgram.IntValueType, 8, 10),
				new("count", ILVariableScopeEnum.LocalParameter, mainProgram.UnsignedIntValueType, 10, 10)],
			new(ILVariableScopeEnum.ReturnValue, new(ILBaseValueTypeEnum.Ptr16, mainProgram.VoidValueType), 0, 10)));

		// int kbhit();
		mainProgram.APIFunctions.Add("kbhit", new APIFunctionDefinition("kbhit", ProgramFunctionOptionsEnum.Cdecl,
			[], new(ILVariableScopeEnum.ReturnValue, mainProgram.IntValueType, 0, 10)));

		// int getch();
		mainProgram.APIFunctions.Add("getch", new APIFunctionDefinition("getch", ProgramFunctionOptionsEnum.Cdecl,
			[], new(ILVariableScopeEnum.ReturnValue, mainProgram.IntValueType, 0, 10)));

		// int abs(int n);
		mainProgram.APIFunctions.Add("abs", new APIFunctionDefinition("abs", ProgramFunctionOptionsEnum.Cdecl,
			[new("n", ILVariableScopeEnum.LocalParameter, mainProgram.IntValueType, 6, 10)],
			new(ILVariableScopeEnum.ReturnValue, mainProgram.IntValueType, 0, 10)));

		// void srand(unsigned int seed);
		mainProgram.APIFunctions.Add("srand", new APIFunctionDefinition("srand", ProgramFunctionOptionsEnum.Cdecl,
			[new("seed", ILVariableScopeEnum.LocalParameter, mainProgram.UnsignedIntValueType, 6, 10)],
			new(ILVariableScopeEnum.ReturnValue, mainProgram.VoidValueType, 0, 10)));

		// int rand();
		mainProgram.APIFunctions.Add("rand", new APIFunctionDefinition("rand", ProgramFunctionOptionsEnum.Cdecl,
			[], new(ILVariableScopeEnum.ReturnValue, mainProgram.IntValueType, 0, 10)));

		// long time(long * timer);
		mainProgram.APIFunctions.Add("time", new APIFunctionDefinition("time", ProgramFunctionOptionsEnum.Cdecl,
			[new("timer", ILVariableScopeEnum.LocalParameter, mainProgram.LongValueType, 6, 10)],
			new(ILVariableScopeEnum.ReturnValue, mainProgram.LongValueType, 0, 10)));

		// void perror(const char * string);
		mainProgram.APIFunctions.Add("perror", new APIFunctionDefinition("perror", ProgramFunctionOptionsEnum.Cdecl,
			[new("string", ILVariableScopeEnum.LocalParameter, mainProgram.StringValueType, 6, 10)],
			new(ILVariableScopeEnum.ReturnValue, mainProgram.VoidValueType, 0, 10)));

		// void exit(int status);
		mainProgram.APIFunctions.Add("exit", new APIFunctionDefinition("exit", ProgramFunctionOptionsEnum.Cdecl,
			[new("status", ILVariableScopeEnum.LocalParameter, mainProgram.IntValueType, 6, 10)],
			new(ILVariableScopeEnum.ReturnValue, mainProgram.VoidValueType, 0, 10)));

		// _aFlshl
		mainProgram.APIFunctions.Add("_aFlshl", new APIFunctionDefinition("_aFlshl", ProgramFunctionOptionsEnum.CompilerInternal, [], 
			new(ILVariableScopeEnum.ReturnValue, mainProgram.VoidValueType, 0, 0)));

		// _aFlshr
		mainProgram.APIFunctions.Add("_aFlshr", new APIFunctionDefinition("_aFlshr", ProgramFunctionOptionsEnum.CompilerInternal, [],
			new(ILVariableScopeEnum.ReturnValue, mainProgram.VoidValueType, 0, 0)));

		// _aFlmul
		mainProgram.APIFunctions.Add("_aFlmul", new APIFunctionDefinition("_aFlmul", ProgramFunctionOptionsEnum.Pascal, 
			[new("multiplicand", ILVariableScopeEnum.LocalParameter, mainProgram.LongValueType, 6, 10),
				new("multiplier", ILVariableScopeEnum.LocalParameter, mainProgram.LongValueType, 10, 10)],
			new(ILVariableScopeEnum.ReturnValue, mainProgram.LongValueType, 0, 10)));

		#endregion

		Console.WriteLine("Disassembling");

		// we start by decompiling Main function
		ProgramFunction function = mainProgram.Disassemble(0, mainCS, mainIP, "Main");
		function.ReturnValue = new ILVariable(function, ILVariableScopeEnum.LocalParameter, mainProgram.IntValueType, 0, 10);

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

		StreamWriter objectWriter = new($"{codePath}{Path.DirectorySeparatorChar}ObjectsAsm.cs");
		StreamWriter initWriter = new($"{codePath}{Path.DirectorySeparatorChar}InitsAsm.cs");
		StreamWriter getWriter = new($"{codePath}{Path.DirectorySeparatorChar}GettersAsm.cs");

		mainProgram.AssignOrdinals();

		uint[] segmentOffsets = mainProgram.Segments.Keys.ToArray();

		Array.Sort(segmentOffsets);

		// emit Main code
		for (int i = 0; i < segmentOffsets.Length; i++)
		{
			ProgramSegment segment = mainProgram.Segments.GetValueByKey(segmentOffsets[i]);

			segment.WriteAsmCS(codePath, 0);

			objectWriter.WriteLine($"private {segment.Name} o{segment.Name};");
			initWriter.WriteLine($"this.o{segment.Name} = new {segment.Name}(this);");
			getWriter.WriteLine($"public {segment.Name} {segment.Name}");
			getWriter.WriteLine("{");
			getWriter.WriteLine($"\tget {{ return this.o{segment.Name};}}");
			getWriter.WriteLine("}");
			getWriter.WriteLine();
		}

		// Emit flow graphs
		uint ilSegment = 0x181;
		ushort ilFunction = 0x14a;

		if (mainProgram.Segments.ContainsKey(ilSegment))
		{
			ProgramSegment segment = mainProgram.Segments.GetValueByKey(ilSegment);

			if (segment.Functions.ContainsKey(ilFunction))
			{
				function = segment.Functions.GetValueByKey(ilFunction);

				if (function.FlowGraph != null)
				{
					function.FlowGraph.WriteGraphDOT("test.gv");
					function.FlowGraph.TranslateToIL();
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