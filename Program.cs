﻿using Disassembler;
using Disassembler.CPU;
using Disassembler.Decompiler;
using Disassembler.MZ;
using Disassembler.NE;
using Disassembler.OMF;
using IRB.Collections.Generic;
using System.Data.Common;
using System.Reflection;
using System.Reflection.Metadata;

internal class Program
{
	private static void Main(string[] args)
	{
		string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\Out\\";
		if (!Directory.Exists(path))
			Directory.CreateDirectory(path);
		if (!Directory.Exists(path+"Code"))
			Directory.CreateDirectory(path+"Code");
		if (!Directory.Exists(path + "Data"))
			Directory.CreateDirectory(path + "Data");

		//MakeTable();

		//UnpackDOSEXE(@"..\..\..\..\Game\Dos\Installed\civ.bak");

		ParseDOSEXE(path);

		//ParseWinEXE(path);
	}

	private static void UnpackDOSEXE(string path)
	{
		MZExecutable mzEXE = new MZExecutable(path);
		ushort usSegment1 = 0x5409;
		ushort usSegment2 = 0x1234;

		CPURegisters oRegisters1 = new CPURegisters();
		Memory oMemory1 = UnpackEXE(mzEXE, usSegment1, oRegisters1);

		CPURegisters oRegisters2 = new CPURegisters();
		Memory oMemory2 = UnpackEXE(mzEXE, usSegment2, oRegisters2);

		byte[] buffer1 = oMemory1.Blocks[3].Data;
		byte[] buffer2 = oMemory2.Blocks[3].Data;
		int iLength1 = buffer1.Length;
		int iLength2 = buffer1.Length;
		uint uiLastEmptyByte = (uint)(iLength1 - 1);

		if (iLength1 != iLength2)
			throw new Exception("Blocks are of different size");

		for (int i = iLength1 - 1; i >= 0; i--)
		{
			if (buffer1[i] != 0 || buffer2[i] != 0)
			{
				uiLastEmptyByte = (uint)(i + 1);
				break;
			}
		}

		Console.WriteLine("Last empty byte in the last block: 0x{0:x8}", uiLastEmptyByte);
		MemoryRegion.AlignBlock(ref uiLastEmptyByte); // we want this to be aligned

		Console.WriteLine("Joining the blocks");
		iLength1 = oMemory1.Blocks[2].Data.Length;
		iLength2 = (int)(oMemory2.Blocks[2].Data.Length + uiLastEmptyByte);
		oMemory1.Blocks[2].Resize(iLength2);
		oMemory2.Blocks[2].Resize(iLength2);

		Array.Copy(oMemory1.Blocks[3].Data, 0, oMemory1.Blocks[2].Data, iLength1, uiLastEmptyByte);
		oMemory1.Blocks.RemoveAt(3);
		Array.Copy(oMemory2.Blocks[3].Data, 0, oMemory2.Blocks[2].Data, iLength1, uiLastEmptyByte);
		oMemory2.Blocks.RemoveAt(3);

		buffer1 = oMemory1.Blocks[2].Data;
		buffer2 = oMemory2.Blocks[2].Data;

		/*Console.WriteLine("Writing blocks to files");
		FileStream writer = new FileStream("exe1.bin", FileMode.Create);
		writer.Write(buffer1, 0, buffer1.Length);
		writer.Close();

		writer = new FileStream("exe2.bin", FileMode.Create);
		writer.Write(buffer2, 0, buffer2.Length);
		writer.Close();*/

		// decrease minimum allocation by additional size that was added to EXE
		mzEXE.MinimumAllocation -= (ushort)(uiLastEmptyByte >> 4);

		CompareBlocksAndReconstructEXE(mzEXE, buffer1, buffer2, usSegment1, usSegment2);
		mzEXE.InitialSS = (ushort)(oRegisters1.SS.Word - usSegment1);
		mzEXE.InitialSP = oRegisters1.SP.Word;
		mzEXE.InitialIP = oRegisters1.IP.Word;
		mzEXE.InitialCS = (ushort)(oRegisters1.CS.Word - usSegment1); // - 0x10; // account for PSP (0x10)

		Console.WriteLine("Block validation passed");

		// write new EXE

		mzEXE.WriteToFile("civ.exe");
	}

	private static void ParseDOSEXE(string path)
	{
		ushort usSegmentOffset = 0x1000;
		MZExecutable mzEXE = new MZExecutable(@"..\..\..\..\..\Game\Dos\Installed\civ.exe");

		mzEXE.ApplyRelocations(usSegmentOffset);

		Library oLibrary1 = new Library(@"..\..\..\..\..\Compilers\MSC\Installed\MSC\LIB\MLIBC7.lib");

		// unique set of segment names
		BHashSet<string> aSegmentNames = new BHashSet<string>();
		BHashSet<string> aGroupNames = new BHashSet<string>();

		for (int i = 0; i < oLibrary1.Modules.Count; i++)
		{
			OBJModule module = oLibrary1.Modules[i];

			for (int j = 0; j < module.DataRecords.Count; j++)
			{
				DataRecord data = module.DataRecords[j];
				aSegmentNames.Add(data.Segment.Name);
			}

			for (int j = 0; j < module.SegmentGroups.Count; j++)
			{
				SegmentGroupDefinition group = module.SegmentGroups[j];
				aGroupNames.Add(group.Name);
			}
		}

		Console.WriteLine("Used segment names:");
		for (int i = 0; i < aSegmentNames.Count; i++)
		{
			Console.WriteLine(aSegmentNames[i]);
		}

		Console.WriteLine("Group names:");
		for (int i = 0; i < aGroupNames.Count; i++)
		{
			Console.WriteLine(aGroupNames[i]);
		}

		List<ModuleMatch> aMatches = new List<ModuleMatch>();
		Console.WriteLine("Matching MLIBC7");
		MatchLibraryToEXE(oLibrary1, mzEXE, aMatches);

		MZDecompiler oDecompiler = new MZDecompiler(mzEXE, aMatches);

		Console.WriteLine("Decompiling");

		// we start by decompiling Start function
		oDecompiler.Decompile("Start", CallTypeEnum.Undefined, new List<CParameter>(), CType.Word,
			0, (ushort)(mzEXE.InitialCS + usSegmentOffset), mzEXE.InitialIP, (uint)usSegmentOffset << 4);

		oDecompiler.Decompile($"F0_{0x2fa1:x4}_{0x644:x4}", CallTypeEnum.Undefined, new List<CParameter>(), CType.Void,
			0, 0x2fa1, 0x644, (uint)usSegmentOffset << 4);

		oDecompiler.Decompile($"F0_{0x1000:x4}_{0x1a7:x4}", CallTypeEnum.Undefined, new List<CParameter>(), CType.Void,
			0, 0x1000, 0x1a7, (uint)usSegmentOffset << 4);

		oDecompiler.Decompile($"F0_{0x3045:x4}_{0x2b44:x4}", CallTypeEnum.Undefined, new List<CParameter>(), CType.Void,
			0, 0x3045, 0x2b44, (uint)usSegmentOffset << 4);

		// Segment 0x1866
		oDecompiler.Decompile($"F0_{0x1866:x4}_{0x1169:x4}", CallTypeEnum.Undefined, new List<CParameter>(), CType.Void,
			0, 0x1866, 0x1169, (uint)usSegmentOffset << 4);
		oDecompiler.Decompile($"F0_{0x1866:x4}_{0x1280:x4}", CallTypeEnum.Undefined, new List<CParameter>(), CType.Void,
			0, 0x1866, 0x1280, (uint)usSegmentOffset << 4);
		oDecompiler.Decompile($"F0_{0x1866:x4}_{0x135a:x4}", CallTypeEnum.Undefined, new List<CParameter>(), CType.Void,
			0, 0x1866, 0x135a, (uint)usSegmentOffset << 4);
		oDecompiler.Decompile($"F0_{0x1866:x4}_{0x13a9:x4}", CallTypeEnum.Undefined, new List<CParameter>(), CType.Void,
			0, 0x1866, 0x13a9, (uint)usSegmentOffset << 4);
		oDecompiler.Decompile($"F0_{0x1866:x4}_{0x13f8:x4}", CallTypeEnum.Undefined, new List<CParameter>(), CType.Void,
			0, 0x1866, 0x13f8, (uint)usSegmentOffset << 4);
		oDecompiler.Decompile($"F0_{0x1866:x4}_{0x14a2:x4}", CallTypeEnum.Undefined, new List<CParameter>(), CType.Void,
			0, 0x1866, 0x14a2, (uint)usSegmentOffset << 4);
		oDecompiler.Decompile($"F0_{0x1866:x4}_{0x14f6:x4}", CallTypeEnum.Undefined, new List<CParameter>(), CType.Void,
			0, 0x1866, 0x14f6, (uint)usSegmentOffset << 4);
		oDecompiler.Decompile($"F0_{0x1866:x4}_{0x1560:x4}", CallTypeEnum.Undefined, new List<CParameter>(), CType.Void,
			0, 0x1866, 0x1560, (uint)usSegmentOffset << 4);
		oDecompiler.Decompile($"F0_{0x1866:x4}_{0x1593:x4}", CallTypeEnum.Undefined, new List<CParameter>(), CType.Void,
			0, 0x1866, 0x1593, (uint)usSegmentOffset << 4);
		oDecompiler.Decompile($"F0_{0x1866:x4}_{0x1610:x4}", CallTypeEnum.Undefined, new List<CParameter>(), CType.Void,
			0, 0x1866, 0x1610, (uint)usSegmentOffset << 4);
		oDecompiler.Decompile($"F0_{0x1866:x4}_{0x1643:x4}", CallTypeEnum.Undefined, new List<CParameter>(), CType.Void,
			0, 0x1866, 0x1643, (uint)usSegmentOffset << 4);
		oDecompiler.Decompile($"F0_{0x1866:x4}_{0x1676:x4}", CallTypeEnum.Undefined, new List<CParameter>(), CType.Void,
			0, 0x1866, 0x1676, (uint)usSegmentOffset << 4);

		// emit generated code
		StreamWriter writer = new StreamWriter("Out\\Code\\Objects.cs");
		StreamWriter writer1 = new StreamWriter("Out\\Code\\Inits.cs");
		StreamWriter writer2 = new StreamWriter("Out\\Code\\Getters.cs");

		// emit segments
		BDictionary<int, List<MZFunction>> oSegments = new BDictionary<int, List<MZFunction>>();
		for (int i = 0; i < oDecompiler.GlobalNamespace.Functions.Count; i++)
		{
			MZFunction function = oDecompiler.GlobalNamespace.Functions[i].Value;
			if (function.Overlay == 0)
			{
				if (oSegments.ContainsKey(function.Segment))
				{
					oSegments.GetValueByKey(function.Segment).Add(function);
				}
				else
				{
					oSegments.Add(function.Segment, new List<MZFunction>());
					oSegments.GetValueByKey(function.Segment).Add(function);
				}
			}
		}

		for (int i = 0; i < oSegments.Count; i++)
		{
			oDecompiler.WriteCode($"Out\\Code\\Segment_{oSegments[i].Key:x}.cs", oSegments[i].Value);
			writer.WriteLine($"private Segment_{oSegments[i].Key:x} oSegment_{oSegments[i].Key:x};");
			writer1.WriteLine($"this.oSegment_{oSegments[i].Key:x} = new Segment_{oSegments[i].Key:x}(this);");
			writer2.WriteLine($"public Segment_{oSegments[i].Key:x} Segment_{oSegments[i].Key:x}");
			writer2.WriteLine("{");
			writer2.WriteLine($"\tget {{ return this.oSegment_{oSegments[i].Key:x};}}");
			writer2.WriteLine("}");
			writer2.WriteLine();
		}

		// emit overlays
		oSegments.Clear();
		for (int i = 0; i < oDecompiler.GlobalNamespace.Functions.Count; i++)
		{
			MZFunction function = oDecompiler.GlobalNamespace.Functions[i].Value;
			if (function.Overlay > 0)
			{
				if (oSegments.ContainsKey(function.Overlay))
				{
					oSegments.GetValueByKey(function.Overlay).Add(function);
				}
				else
				{
					oSegments.Add(function.Overlay, new List<MZFunction>());
					oSegments.GetValueByKey(function.Overlay).Add(function);
				}
			}
		}

		for (int i = 0; i < oSegments.Count; i++)
		{
			oDecompiler.WriteCode($"Out\\Code\\Overlay_{oSegments[i].Key}.cs", oSegments[i].Value);
			writer.WriteLine($"private Overlay_{oSegments[i].Key} oOverlay_{oSegments[i].Key};");
			writer1.WriteLine($"this.oOverlay_{oSegments[i].Key} = new Overlay_{oSegments[i].Key}(this);");
			writer2.WriteLine($"public Overlay_{oSegments[i].Key} Overlay_{oSegments[i].Key}");
			writer2.WriteLine("{");
			writer2.WriteLine($"\tget {{ return this.oOverlay_{oSegments[i].Key};}}");
			writer2.WriteLine("}");
			writer2.WriteLine();
		}

		int iMaxOverlaySize = 0;
		for (int i = 0; i < mzEXE.Overlays.Count; i++)
		{
			iMaxOverlaySize = Math.Max(iMaxOverlaySize, mzEXE.Overlays[i].Data.Length);
		}
		Console.WriteLine($"Maximum overlay size in bytes: 0x{iMaxOverlaySize:x4}");

		// emit API functions
		List <MZFunction> aFunctions = new List<MZFunction>();

		for (int i = 0; i < oDecompiler.GlobalNamespace.APIFunctions.Count; i++)
		{
			aFunctions.Add(oDecompiler.GlobalNamespace.APIFunctions[i].Value);
		}

		oDecompiler.WriteCode(@"Out\Code\MSCAPI.cs", aFunctions);
		writer.WriteLine("private MSCAPI oMSCAPI;");
		writer1.WriteLine("this.oMSCAPI = new MSCAPI(this);");
		writer2.WriteLine("public MSCAPI MSCAPI");
		writer2.WriteLine("{");
		writer2.WriteLine("\tget { return this.oMSCAPI;}");
		writer2.WriteLine("}");

		// process misc.exe
		Console.WriteLine("Processing overlay Misc");
		MZExecutable miscEXE = new MZExecutable(@"..\..\..\..\..\Game\Dos\Installed\misc.exe");
		MZDecompiler oMiscDecompiler = new MZDecompiler(miscEXE, aMatches);

		oMiscDecompiler.DecompileOverlay();

		// Emit Misc functions
		aFunctions = new List<MZFunction>();
		for (int i = 0; i < oMiscDecompiler.GlobalNamespace.Functions.Count; i++)
		{
			aFunctions.Add(oMiscDecompiler.GlobalNamespace.Functions[i].Value);
		}

		oMiscDecompiler.WriteCode(@"Out\Code\Misc.cs", aFunctions);
		writer.WriteLine("private Misc oMisc;");
		writer1.WriteLine("this.oMisc = new Misc(this);");
		writer2.WriteLine();
		writer2.WriteLine("public Misc Misc");
		writer2.WriteLine("{");
		writer2.WriteLine("\tget { return this.oMisc;}");
		writer2.WriteLine("}");

		Console.WriteLine("Processing overlay VGA");
		MZExecutable vgaEXE = new MZExecutable(@"..\..\..\..\..\Game\Dos\Installed\mgraphic.exe");
		MZDecompiler oVGADecompiler = new MZDecompiler(vgaEXE, aMatches);

		oVGADecompiler.DecompileOverlay();

		/*oEGADecompiler.Decompile($"F0_0000_{0x19b8:x4}", CallTypeEnum.Undefined, new List<CParameter>(), CType.Void, 0, 0, 0x19b8, 0);
		oEGADecompiler.Decompile($"F0_0000_{0x1a08:x4}", CallTypeEnum.Undefined, new List<CParameter>(), CType.Void, 0, 0, 0x1a08, 0);
		oEGADecompiler.Decompile($"F0_0000_{0x1a3c:x4}", CallTypeEnum.Undefined, new List<CParameter>(), CType.Void, 0, 0, 0x1a3c, 0);
		oEGADecompiler.Decompile($"F0_0000_{0xcf1:x4}", CallTypeEnum.Undefined, new List<CParameter>(), CType.Void, 0, 0, 0xcf1, 0);*/

		// Emit egraphic functions
		aFunctions = new List<MZFunction>();
		for (int i = 0; i < oVGADecompiler.GlobalNamespace.Functions.Count; i++)
		{
			aFunctions.Add(oVGADecompiler.GlobalNamespace.Functions[i].Value);
		}

		oVGADecompiler.WriteCode(@"Out\Code\VGADriver.cs", aFunctions);
		writer.WriteLine("private VGADriver oVGA;");
		writer1.WriteLine("this.oVGA = new VGADriver(this);");
		writer2.WriteLine();
		writer2.WriteLine("public VGADriver VGA");
		writer2.WriteLine("{");
		writer2.WriteLine("\tget { return this.oVGA;}");
		writer2.WriteLine("}");

		Console.WriteLine("Processing overlay NSound");
		MZExecutable nsEXE = new MZExecutable(@"..\..\..\..\..\Game\Dos\Installed\nsound.cvl");
		MZDecompiler oNSecompiler = new MZDecompiler(nsEXE, aMatches);

		oNSecompiler.DecompileOverlay();

		// Emit egraphic functions
		aFunctions = new List<MZFunction>();
		for (int i = 0; i < oNSecompiler.GlobalNamespace.Functions.Count; i++)
		{
			aFunctions.Add(oNSecompiler.GlobalNamespace.Functions[i].Value);
		}

		oNSecompiler.WriteCode(@"Out\Code\NSound.cs", aFunctions);
		writer.WriteLine("private NSound oNSound;");
		writer1.WriteLine("this.oNSound = new NSound(this);");
		writer2.WriteLine();
		writer2.WriteLine("public NSound NSound");
		writer2.WriteLine("{");
		writer2.WriteLine("\tget { return this.oNSound;}");
		writer2.WriteLine("}");

		writer2.Close();
		writer1.Close();
		writer.Close();

		Console.WriteLine("Finished DOS processing");
	}

	private static void CompareBlocksAndReconstructEXE(MZExecutable exe, byte[] block1, byte[] block2, ushort segment1, ushort segment2)
	{
		if (block1.Length != block2.Length)
		{
			Console.WriteLine("Block 1 length is not equal to Block 2 length");
			return;
		}

		// also, reconstruct relocation items
		int iSegment = 0;
		int iOffset = 0;

		exe.Relocations.Clear();
		
		// we are comparing words
		for (int i = 0; i < block1.Length; i++)
		{
			if (block1[i] != block2[i])
			{
				if (block1[i + 1] != block2[i + 1])
				{
					// compare absolute offsets, not relative ones
					ushort usWord1 = (ushort)((ushort)block1[i] | (ushort)((ushort)block1[i + 1] << 8));
					usWord1 -= segment1;
					//usWord1 -= 0x10; // account for PSP (0x10)
					ushort usWord2 = (ushort)((ushort)block2[i] | (ushort)((ushort)block2[i + 1] << 8));
					usWord2 -= segment2;
					//usWord2 -= 0x10; // account for PSP (0x10)

					if (usWord1 != usWord2)
					{
						Console.WriteLine("Segment 0x{0:x4} not equal to 0x{1:x4} at 0x{2:x4}", usWord1, usWord2, i);
					}
					block1[i] = (byte)(usWord1 & 0xff);
					block1[i + 1] = (byte)((usWord1 & 0xff00) >> 8);
					block2[i] = (byte)(usWord2 & 0xff);
					block2[i + 1] = (byte)((usWord2 & 0xff00) >> 8);
					exe.Relocations.Add(new MZRelocationItem((ushort)iSegment, (ushort)iOffset));
					iOffset++;
					i++;
				}
				else
				{
					Console.WriteLine("Block 1 word is not equal in size to Block 2 at 0x{0:x4}", i);
					return;
				}
			}
			iOffset++;
			if (iOffset > 0xffff)
			{
				iSegment += 0x1000;
				iOffset -= 0x10000;
			}
		}

		exe.Data = new byte[block1.Length];
		Array.Copy(block1, exe.Data, block1.Length);
	}

	private static Memory UnpackEXE(MZExecutable exe, ushort startSegment, CPURegisters r)
	{
		Memory oMemory = new Memory();
		ushort usPSPSegment;
		ushort usMZSegment;
		ushort usDataSegment;
		uint uiMZEXELength = (uint)exe.Data.Length;
		MemoryRegion.AlignBlock(ref uiMZEXELength);

		if (startSegment < 0x10)
			throw new Exception("starting segment must be greater than 0x10");

		// blank start segment
		oMemory.AllocateParagraphs((ushort)(startSegment - 0x10), out usPSPSegment);
		oMemory.MemoryRegions.Add(new MemoryRegion(0, (startSegment - 0x10) << 4, MemoryFlagsEnum.None));

		// PSP segment
		oMemory.AllocateParagraphs(0x10, out usPSPSegment);
		oMemory.MemoryRegions.Add(new MemoryRegion(usPSPSegment, 0x100, MemoryFlagsEnum.None));

		// EXE segment
		oMemory.AllocateBlock((int)uiMZEXELength, out usMZSegment);
		oMemory.WriteBlock(usMZSegment, 0, exe.Data, 0, exe.Data.Length);

		// data segment
		oMemory.AllocateParagraphs((ushort)exe.MinimumAllocation, out usDataSegment);

		// decode EXE packer
		r.SP.Word = (ushort)exe.InitialSP;
		r.DS.Word = usPSPSegment;
		r.ES.Word = usPSPSegment;
		r.SS.Word = (ushort)(exe.InitialSS + usMZSegment);
		r.CS.Word = (ushort)(exe.InitialCS + usMZSegment);
		bool bDFlag = false;

		// Decoding phase 1

		// 2b0d:0010	mov ax, es
		r.AX.Word = r.ES.Word;
		// 2b0d:0012	add ax, 10h
		r.AX.Word += 0x10;
		// 2b0d:0015	push cs; pop ds
		r.DS.Word = r.CS.Word;
		// 2b0d:0017	mov word_2B0D_4, ax
		oMemory.WriteWord(r.DS.Word, 0x4, r.AX.Word);
		// 2b0d:001A	add     ax, word_2B0D_C
		r.AX.Word += oMemory.ReadWord(r.DS.Word, 0xc);
		// 2b0d:001E	mov es, ax
		r.ES.Word = r.AX.Word;
		// 2b0d:0020	mov cx, word_2B0D_6
		r.CX.Word = oMemory.ReadWord(r.DS.Word, 0x6);
		// 2b0d:0024	mov di, cx
		r.DI.Word = r.CX.Word;
		// 2b0d:0026	dec di
		r.DI.Word--;
		// 2b0d:0027	mov si, di
		r.SI.Word = r.DI.Word;
		// 2b0d:0029	std
		bDFlag = true;
		// 2b0d:002A	rep movsb
		while (r.CX.Word != 0)
		{
			oMemory.WriteByte(r.ES.Word, r.DI.Word, oMemory.ReadByte(r.DS.Word, r.SI.Word));
			if (bDFlag)
			{
				r.DI.Word--;
				r.SI.Word--;
			}
			else
			{
				r.DI.Word++;
				r.SI.Word++;
			}
			r.CX.Word--;
		}
		// 2b0d:002C	push    ax
		// 2b0d:002D	mov ax, 32h; push ax
		// 2b0d:0031	retf

		//oMemory.WriteWord(r.SS.Word, (ushort)(r.SP.Word - 2), r.AX.Word);
		//oMemory.WriteWord(r.SS.Word, (ushort)(r.SP.Word - 4), 0x32);
		r.CS.Word = r.AX.Word;
		r.IP.Word = 0x32;

		//oMemory.MemoryRegions.Add(new MemoryRegion(r.AX.Word, 0x32, 0x10f - 0x32, MemoryFlagsEnum.Read));
		//Console.WriteLine("Protecting block at 0x{0:x8}, size 0x{1:x4}", MemoryRegion.ToAbsolute(r.AX.Word, 0x32), 0x10f - 0x32);

		// Decoding phase 2

		// CS:IP = AX:0x32

		// 0x0032	MOV BX, ES
		r.BX.Word = r.ES.Word;
		// 0x0034	MOV AX, DS
		r.AX.Word = r.DS.Word;
		// 0x0036	DEC AX
		r.AX.Word--;
		// 0x0037	MOV DS, AX
		r.DS.Word = r.AX.Word;
		// 0x0039	MOV ES, AX
		r.ES.Word = r.AX.Word;
		// 0x003b	MOV DI, 0xf
		r.DI.Word = 0xf;
		// 0x003e	MOV CX, 0x10
		r.CX.Word = 0x10;
		// 0x0041	MOV AL, 0xff
		r.AX.Low = 0xff;
		// 0x0043	REPE SCASB
		while (r.CX.Word != 0)
		{
			byte res = (byte)(((int)r.AX.Low - (int)oMemory.ReadByte(r.ES.Word, r.DI.Word)) & 0xff);
			if (bDFlag)
			{
				r.DI.Word--;
			}
			else
			{
				r.DI.Word++;
			}
			r.CX.Word--;

			if (res != 0)
				break;
		}
		// 0x0045	INC DI
		r.DI.Word++;
		// 0x0046	MOV SI, DI
		r.SI.Word = r.DI.Word;
		// 0x0048	MOV AX, BX
		r.AX.Word = r.BX.Word;
		// 0x004a	DEC AX
		r.AX.Word--;
		// 0x004b	MOV ES, AX
		r.ES.Word = r.AX.Word;
		// 0x004d	MOV DI, 0xf
		r.DI.Word = 0xf;

		l50:
		// 0x0050	MOV CL, 0x4
		r.CX.Low = 0x4;
		// 0x0052	MOV AX, SI
		r.AX.Word = r.SI.Word;
		// 0x0054	NOT AX
		r.AX.Word = (ushort)(~r.AX.Word);
		// 0x0056	SHR AX, CL
		r.AX.Word = (ushort)(r.AX.Word >> r.CX.Low);
		// 0x0058	JE + 0x9
		if (r.AX.Word == 0) goto l63;
		// 0x005a	MOV DX, DS
		r.DX.Word = r.DS.Word;
		// 0x005c	SUB DX, AX
		r.DX.Word -= r.AX.Word;
		// 0x005e	MOV DS, DX
		r.DS.Word = r.DX.Word;
		// 0x0060	OR SI, 0xfff0
		r.SI.Word |= 0xfff0;

		l63:
		// 0x0063	MOV AX, DI
		r.AX.Word = r.DI.Word;
		// 0x0065	NOT AX
		r.AX.Word = (ushort)(~r.AX.Word);
		// 0x0067	SHR AX, CL
		r.AX.Word = (ushort)(r.AX.Word >> r.CX.Low);
		// 0x0069	JE + 0x9
		if (r.AX.Word == 0) goto l74;
		// 0x006b	MOV DX, ES
		r.DX.Word = r.ES.Word;
		// 0x006d	SUB DX, AX
		r.DX.Word -= r.AX.Word;
		// 0x006f	MOV ES, DX
		r.ES.Word = r.DX.Word;
		// 0x0071	OR DI, 0xfff0
		r.DI.Word |= 0xfff0;

		l74:
		// 0x0074	LODSB
		r.AX.Low = oMemory.ReadByte(r.DS.Word, r.SI.Word);
		if (bDFlag)
		{
			r.SI.Word--;
		}
		else
		{
			r.SI.Word++;
		}
		// 0x0075	MOV DL, AL
		r.DX.Low = r.AX.Low;
		// 0x0077	DEC SI
		r.SI.Word--;
		// 0x0078	LODSW
		r.AX.Word = oMemory.ReadWord(r.DS.Word, r.SI.Word);
		if (bDFlag)
		{
			r.SI.Word -= 2;
		}
		else
		{
			r.SI.Word += 2;
		}
		// 0x0079	MOV CX, AX
		r.CX.Word = r.AX.Word;
		// 0x007b	INC SI
		r.SI.Word++;
		// 0x007c	MOV AL, DL
		r.AX.Low = r.DX.Low;
		// 0x007e	AND AL, 0xfe
		r.AX.Low &= 0xfe;
		// 0x0080	CMP AL, 0xb0
		// 0x0082	JNE + 0x6
		if (r.AX.Low != 0xb0) goto l8a;
		// 0x0084	LODSB
		r.AX.Low = oMemory.ReadByte(r.DS.Word, r.SI.Word);
		if (bDFlag)
		{
			r.SI.Word--;
		}
		else
		{
			r.SI.Word++;
		}
		// 0x0085	REPE STOSB
		while (r.CX.Word != 0)
		{
			oMemory.WriteByte(r.ES.Word, r.DI.Word, r.AX.Low);
			if (bDFlag)
			{
				r.DI.Word--;
			}
			else
			{
				r.DI.Word++;
			}
			r.CX.Word--;
		}
		// 0x0087	JMP + 0x7
		goto l90;
		// 0x0089	NOP

		l8a:
		// 0x008a	CMP AL, 0xb2
		// 0x008c	JNE + 0x6b
		if (r.AX.Low != 0xb2) goto lf9;
		// 0x008e	REPE MOVSB
		while (r.CX.Word != 0)
		{
			oMemory.WriteByte(r.ES.Word, r.DI.Word, oMemory.ReadByte(r.DS.Word, r.SI.Word));
			if (bDFlag)
			{
				r.DI.Word--;
				r.SI.Word--;
			}
			else
			{
				r.DI.Word++;
				r.SI.Word++;
			}
			r.CX.Word--;
		}

		l90:
		// 0x0090	MOV AL, DL
		r.AX.Low = r.DX.Low;
		// 0x0092	TEST AL, 0x1
		// 0x0094	JE - 0x46
		if ((r.AX.Low & 1) == 0) goto l50;
		// 0x0096	MOV SI, 0x125
		r.SI.Word = 0x125;
		// 0x0099	PUSH CS
		// 0x009a	POP DS
		r.DS.Word = r.CS.Word;
		// 0x009b	MOV BX, [0x4]
		r.BX.Word = oMemory.ReadWord(r.DS.Word, 0x4);
		// 0x009f	CLD
		bDFlag = false;
		// 0x00a0	XOR DX, DX
		r.DX.Word = 0;

		la2:
		// 0x00a2	LODSW
		r.AX.Word = oMemory.ReadWord(r.DS.Word, r.SI.Word);
		if (bDFlag)
		{
			r.SI.Word -= 2;
		}
		else
		{
			r.SI.Word += 2;
		}
		// 0x00a3	MOV CX, AX
		r.CX.Word = r.AX.Word;
		// 0x00a5	JCXZ + 0x13
		if (r.CX.Word == 0) goto lba;
		// 0x00a7	MOV AX, DX
		r.AX.Word = r.DX.Word;
		// 0x00a9	ADD AX, BX
		r.AX.Word += r.BX.Word;
		// 0x00ab	MOV ES, AX
		r.ES.Word = r.AX.Word;

		lad:
		// 0x00ad	LODSW
		r.AX.Word = oMemory.ReadWord(r.DS.Word, r.SI.Word);
		if (bDFlag)
		{
			r.SI.Word -= 2;
		}
		else
		{
			r.SI.Word += 2;
		}
		// 0x00ae	MOV DI, AX
		r.DI.Word = r.AX.Word;
		// 0x00b0	CMP DI, 0xffff
		// 0x00b3	JE + 0x11
		if (r.DI.Word == 0xffff) goto lc6;
		// 0x00b5	ADD ES:[DI], BX
		oMemory.WriteWord(r.ES.Word, r.DI.Word, (ushort)(oMemory.ReadWord(r.ES.Word, r.DI.Word) + r.BX.Word));

		lb8:
		// 0x00b8	LOOP - 0xd
		r.CX.Word--;
		if (r.CX.Word != 0) goto lad;

		lba:
		// 0x00ba	CMP DX, 0xf000
		// 0x00be	JE + 0x16
		if (r.DX.Word == 0xf000) goto ld6;
		// 0x00c0	ADD DX, 0x1000
		r.DX.Word += 0x1000;
		// 0x00c4	JMP - 0x24
		goto la2;

		lc6:
		// 0x00c6	MOV AX, ES
		r.AX.Word = r.ES.Word;
		// 0x00c8	INC AX
		r.AX.Word++;
		// 0x00c9	MOV ES, AX
		r.ES.Word = r.AX.Word;
		// 0x00cb	SUB DI, 0x10
		r.DI.Word -= 0x10;
		// 0x00ce	ADD ES:[DI], BX
		oMemory.WriteWord(r.ES.Word, r.DI.Word, (ushort)(oMemory.ReadWord(r.ES.Word, r.DI.Word) + r.BX.Word));
		// 0x00d1	DEC AX
		r.AX.Word--;
		// 0x00d2	MOV ES, AX
		r.ES.Word = r.AX.Word;
		// 0x00d4	JMP - 0x1e
		goto lb8;

		ld6:
		// 0x00d6	MOV AX, BX
		r.AX.Word = r.BX.Word;
		// 0x00d8	MOV DI, [0x8]
		r.DI.Word = oMemory.ReadWord(r.DS.Word, 0x8);
		// 0x00dc	MOV SI, [0xa]
		r.SI.Word = oMemory.ReadWord(r.DS.Word, 0xa);
		// 0x00e0	ADD SI, AX
		r.SI.Word += r.AX.Word;
		// 0x00e2	ADD [0x2], AX
		oMemory.WriteWord(r.DS.Word, 0x2, (ushort)(oMemory.ReadWord(r.DS.Word, 0x2) + r.AX.Word));
		// 0x00e6	SUB AX, 0x10
		r.AX.Word -= 0x10;
		// 0x00e9	MOV DS, AX
		r.DS.Word = r.AX.Word;
		// 0x00eb	MOV ES, AX
		r.ES.Word = r.AX.Word;
		// 0x00ed	MOV BX, 0x0
		r.BX.Word = 0;
		// 0x00f0	CLI
		// 0x00f1	MOV SS, SI
		r.SS.Word = r.SI.Word;
		// 0x00f3	MOV SP, DI
		r.SP.Word = r.DI.Word;
		// 0x00f5	STI
		// 0x00f6	JMP far CS:[BX]
		r.IP.Word = oMemory.ReadWord(r.CS.Word, r.BX.Word);
		r.CS.Word = oMemory.ReadWord(r.CS.Word, (ushort)(r.BX.Word + 2));
		goto finished;

		lf9:
		throw new Exception("Error decoding file");

		finished:
		return oMemory;
	}

	private static void ParseWinEXE(string path)
	{
		// load libraries and modules
		OBJModule oModule = new OBJModule(@"..\..\..\..\Compilers\Borland\Installed1\BORLANDC\LIB\C0WL.obj");
		Library oLibrary1 = new Library(@"..\..\..\..\Compilers\Borland\Installed1\BORLANDC\LIB\CWL.lib");
		Library oLibrary2 = new Library(@"..\..\..\..\Compilers\Borland\Installed1\BORLANDC\LIB\MATHWL.lib");

		NewExecutable exe = new NewExecutable(@"..\..\..\..\Game\Win\Installed\civ.exe");
		//NewExecutable exe = new NewExecutable(@"C:\Users\rajko\Documents\Projects\Disassembler\Win\Installed\CIVWIN\civ.exe");

		exe.ApplyRelocations();

		//Console.WriteLine("0x{0:x8}", exe.CSIP);

		/*for (int i = 0; i < exe.Resources.Count; i++)
		{
			NEResourceTypeContainer resourceType = exe.Resources[i];

			switch (resourceType.Type)
			{
				case ResourceTypeEnum.RT_CURSOR:
					for (int j = 0; j < resourceType.Resources.Count; j++)
					{
						NEResource resource = resourceType.Resources[j];
						FileStream rscWriter;
						if (!string.IsNullOrEmpty(resource.Name))
						{
							rscWriter = new FileStream(string.Format("{0}\\Resources\\Cursor_{1:x4}_{2}.bin",
								path, resource.ID, resource.Name), FileMode.Create);
						}
						else
						{
							rscWriter = new FileStream(string.Format("{0}\\Resources\\Cursor_{1:x4}.bin",
								path, resource.ID), FileMode.Create);
						}

						rscWriter.Write(resource.Data, 0, resource.Data.Length);
						rscWriter.Flush();
						rscWriter.Close();
					}
					break;
				case ResourceTypeEnum.RT_BITMAP:
					for (int j = 0; j < resourceType.Resources.Count; j++)
					{
						NEResource resource = resourceType.Resources[j];
						FileStream rscWriter;
						if (!string.IsNullOrEmpty(resource.Name))
						{
							rscWriter = new FileStream(string.Format("{0}\\Resources\\Bitmap_{1:x4}_{2}.bin",
								path, resource.ID, resource.Name), FileMode.Create);
						}
						else
						{
							rscWriter = new FileStream(string.Format("{0}\\Resources\\Bitmap_{1:x4}.bin",
								path, resource.ID), FileMode.Create);
						}

						rscWriter.Write(resource.Data, 0, resource.Data.Length);
						rscWriter.Flush();
						rscWriter.Close();
					}
					break;
				case ResourceTypeEnum.RT_ICON:
					for (int j = 0; j < resourceType.Resources.Count; j++)
					{
						NEResource resource = resourceType.Resources[j];
						FileStream rscWriter;
						if (!string.IsNullOrEmpty(resource.Name))
						{
							rscWriter = new FileStream(string.Format("{0}\\Resources\\Icon_{1:x4}_{2}.bin",
								path, resource.ID, resource.Name), FileMode.Create);
						}
						else
						{
							rscWriter = new FileStream(string.Format("{0}\\Resources\\Icon_{1:x4}.bin",
								path, resource.ID), FileMode.Create);
						}

						rscWriter.Write(resource.Data, 0, resource.Data.Length);
						rscWriter.Flush();
						rscWriter.Close();
					}
					break;
				case ResourceTypeEnum.RT_MENU:
					for (int j = 0; j < resourceType.Resources.Count; j++)
					{
						NEResource resource = resourceType.Resources[j];
						FileStream rscWriter;
						if (!string.IsNullOrEmpty(resource.Name))
						{
							rscWriter = new FileStream(string.Format("{0}\\Resources\\Menu_{1:x4}_{2}.bin",
								path, resource.ID, resource.Name), FileMode.Create);
						}
						else
						{
							rscWriter = new FileStream(string.Format("{0}\\Resources\\Menu_{1:x4}.bin",
								path, resource.ID), FileMode.Create);
						}

						rscWriter.Write(resource.Data, 0, resource.Data.Length);
						rscWriter.Flush();
						rscWriter.Close();
					}
					break;
				case ResourceTypeEnum.RT_DIALOG:
					for (int j = 0; j < resourceType.Resources.Count; j++)
					{
						NEResource resource = resourceType.Resources[j];
						FileStream rscWriter;
						if (!string.IsNullOrEmpty(resource.Name))
						{
							rscWriter = new FileStream(string.Format("{0}\\Resources\\Dialog_{1:x4}_{2}.bin",
								path, resource.ID, resource.Name), FileMode.Create);
						}
						else
						{
							rscWriter = new FileStream(string.Format("{0}\\Resources\\Dialog_{1:x4}.bin",
								path, resource.ID), FileMode.Create);
						}

						rscWriter.Write(resource.Data, 0, resource.Data.Length);
						rscWriter.Flush();
						rscWriter.Close();
					}
					break;
				case ResourceTypeEnum.RT_STRING:
					for (int j = 0; j < resourceType.Resources.Count; j++)
					{
						NEResource resource = resourceType.Resources[j];
						FileStream rscWriter;
						if (!string.IsNullOrEmpty(resource.Name))
						{
							rscWriter = new FileStream(string.Format("{0}\\Resources\\String_{1:x4}_{2}.bin",
								path, resource.ID, resource.Name), FileMode.Create);
						}
						else
						{
							rscWriter = new FileStream(string.Format("{0}\\Resources\\String_{1:x4}.bin",
								path, resource.ID), FileMode.Create);
						}

						rscWriter.Write(resource.Data, 0, resource.Data.Length);
						rscWriter.Flush();
						rscWriter.Close();
					}
					break;
				case ResourceTypeEnum.RT_FONTDIR:
					for (int j = 0; j < resourceType.Resources.Count; j++)
					{
						NEResource resource = resourceType.Resources[j];
						FileStream rscWriter;
						if (!string.IsNullOrEmpty(resource.Name))
						{
							rscWriter = new FileStream(string.Format("{0}\\Resources\\FontDir_{1:x4}_{2}.bin",
								path, resource.ID, resource.Name), FileMode.Create);
						}
						else
						{
							rscWriter = new FileStream(string.Format("{0}\\Resources\\FontDir_{1:x4}.bin",
								path, resource.ID), FileMode.Create);
						}

						rscWriter.Write(resource.Data, 0, resource.Data.Length);
						rscWriter.Flush();
						rscWriter.Close();
					}
					break;
				case ResourceTypeEnum.RT_FONT:
					for (int j = 0; j < resourceType.Resources.Count; j++)
					{
						NEResource resource = resourceType.Resources[j];
						FileStream rscWriter;
						if (!string.IsNullOrEmpty(resource.Name))
						{
							rscWriter = new FileStream(string.Format("{0}\\Resources\\Font_{1:x4}_{2}.bin",
								path, resource.ID, resource.Name), FileMode.Create);
						}
						else
						{
							rscWriter = new FileStream(string.Format("{0}\\Resources\\Font_{1:x4}.bin",
								path, resource.ID), FileMode.Create);
						}

						rscWriter.Write(resource.Data, 0, resource.Data.Length);
						rscWriter.Flush();
						rscWriter.Close();
					}
					break;
				case ResourceTypeEnum.RT_ACCELERATOR:
					for (int j = 0; j < resourceType.Resources.Count; j++)
					{
						NEResource resource = resourceType.Resources[j];
						FileStream rscWriter;
						if (!string.IsNullOrEmpty(resource.Name))
						{
							rscWriter = new FileStream(string.Format("{0}\\Resources\\Accelerator_{1:x4}_{2}.bin",
								path, resource.ID, resource.Name), FileMode.Create);
						}
						else
						{
							rscWriter = new FileStream(string.Format("{0}\\Resources\\Accelerator_{1:x4}.bin",
								path, resource.ID), FileMode.Create);
						}

						rscWriter.Write(resource.Data, 0, resource.Data.Length);
						rscWriter.Flush();
						rscWriter.Close();
					}
					break;
				case ResourceTypeEnum.RT_RCDATA:
					for (int j = 0; j < resourceType.Resources.Count; j++)
					{
						NEResource resource = resourceType.Resources[j];
						FileStream rscWriter;
						if (!string.IsNullOrEmpty(resource.Name))
						{
							rscWriter = new FileStream(string.Format("{0}\\Resources\\RCData_{1:x4}_{2}.bin",
								path, resource.ID, resource.Name), FileMode.Create);
						}
						else
						{
							rscWriter = new FileStream(string.Format("{0}\\Resources\\RCData_{1:x4}.bin",
								path, resource.ID), FileMode.Create);
						}

						rscWriter.Write(resource.Data, 0, resource.Data.Length);
						rscWriter.Flush();
						rscWriter.Close();
					}
					break;
				case ResourceTypeEnum.RT_GROUP_CURSOR:
					for (int j = 0; j < resourceType.Resources.Count; j++)
					{
						NEResource resource = resourceType.Resources[j];
						FileStream rscWriter;
						if (!string.IsNullOrEmpty(resource.Name))
						{
							rscWriter = new FileStream(string.Format("{0}\\Resources\\GroupCursor_{1:x4}_{2}.bin",
								path, resource.ID, resource.Name), FileMode.Create);
						}
						else
						{
							rscWriter = new FileStream(string.Format("{0}\\Resources\\GroupCursor_{1:x4}.bin",
								path, resource.ID), FileMode.Create);
						}

						rscWriter.Write(resource.Data, 0, resource.Data.Length);
						rscWriter.Flush();
						rscWriter.Close();
					}
					break;
				case ResourceTypeEnum.RT_GROUP_ICON:
					for (int j = 0; j < resourceType.Resources.Count; j++)
					{
						NEResource resource = resourceType.Resources[j];
						FileStream rscWriter;
						if (!string.IsNullOrEmpty(resource.Name))
						{
							rscWriter = new FileStream(string.Format("{0}\\Resources\\GroupIcon_{1:x4}_{2}.bin",
								path, resource.ID, resource.Name), FileMode.Create);
						}
						else
						{
							rscWriter = new FileStream(string.Format("{0}\\Resources\\GroupIcon_{1:x4}.bin",
								path, resource.ID), FileMode.Create);
						}

						rscWriter.Write(resource.Data, 0, resource.Data.Length);
						rscWriter.Flush();
						rscWriter.Close();
					}
					break;
			}
		}

		StreamWriter writer = new StreamWriter(string.Format("{0}\\Resources\\Strings.txt", path));
		for (int i = 0; i < exe.ResourceStrings.Count; i++)
		{
			writer.WriteLine(exe.ResourceStrings[i]);
		}
		writer.Flush();
		writer.Close(); */

		List<ModuleMatch> aMatches = new List<ModuleMatch>();

		Console.WriteLine("Matching C0WL");
		MatchModule(oModule, exe.Segments, aMatches);
		Console.WriteLine("Matching MATHWL");
		MatchLibrary(oLibrary2, exe.Segments, aMatches);
		Console.WriteLine("Matching CWL");
		MatchLibrary(oLibrary1, exe.Segments, aMatches);

		Console.WriteLine("---");
		Console.WriteLine();

		// find WINMAIN entry
		string sWinMain = "winmain";
		int iWinMainIndex = -1;
		ushort usWinMainOffset = 0;
		ushort usWinMainSegment = 0;

		for (int i = 0; i < oModule.ExternalNames.Count; i++)
		{
			if (oModule.ExternalNames[i].Name.ToLower().Equals(sWinMain))
			{
				iWinMainIndex = i + 1;
				break;
			}
		}

		for (int i = 0; i < oModule.DataRecords.Count; i++)
		{
			for (int j = 0; j < oModule.DataRecords[i].Fixups.Count; j++)
			{
				Fixup fixup = oModule.DataRecords[i].Fixups[j];

				if (fixup.FixupLocationType == FixupLocationTypeEnum.LongPointer32bit &&
					fixup.TargetThreadIndex == iWinMainIndex &&
					fixup.TargetMethod == TargetMethodEnum.ExtDefIndex)
				{
					usWinMainOffset = (ushort)fixup.DataOffset;
					break;
				}
			}
			if (usWinMainOffset >= 0)
				break;
		}

		for (int i = 0; i < aMatches.Count; i++)
		{
			if (aMatches[i].Module.Name.Equals(oModule.Name))
			{
				uint uiAddress = aMatches[i].LinearAddress;
				int iLength = aMatches[i].Length;
				Segment segment = exe.Segments[0];

				for (int j = 0; j < segment.Relocations.Count; j++)
				{
					Relocation relocation = segment.Relocations[j];

					if (relocation.Offset >= uiAddress && relocation.Offset + relocation.Length <= uiAddress + iLength)
					{
						// relocation is within module range
						if (relocation.LocationType == LocationTypeEnum.SegmentOffset32 &&
							relocation.Offset - uiAddress == usWinMainOffset)
						{
							usWinMainSegment = (ushort)(relocation.Parameter1 - 1);
							usWinMainOffset = (ushort)relocation.Parameter2;
							break;
						}
					}
				}
				break;
			}
		}

		if (usWinMainOffset >= 0 && usWinMainSegment >= 0)
		{
			// and now we have entry to our winmain procedure...
			// create decompiler and it's context
			CDecompiler oDecompiler = new CDecompiler(exe, aMatches);

			List<CParameter> aParameters = new List<CParameter>();
			aParameters.Add(new CParameter(CType.Int, "hInstance"));
			aParameters.Add(new CParameter(CType.Int, "hPrevInstance"));
			aParameters.Add(new CParameter(CType.CharFarPtr, "lpCmdLine"));
			aParameters.Add(new CParameter(CType.Int, "nCmdShow"));

			// we start by decompiling WinMain function
			oDecompiler.Decompile("WinMain", CallTypeEnum.Undefined, aParameters, CType.Word, usWinMainSegment, usWinMainOffset);

			// decompile exports
			for (int i = 0; i < exe.Entries.Count; i++)
			{
				Entry entry = exe.Entries[i];
				oDecompiler.Decompile(string.Format("F{0}_{1:x}", (uint)entry.Segment - 1, (uint)entry.Offset),
					CallTypeEnum.Undefined, new List<CParameter>(), CType.Void, (ushort)(entry.Segment - 1), (ushort)entry.Offset);
			}

			// decompile unreferenced function, referenced by data segment
			/*byte[] abData=exe.Segments[(int)oDecompiler.DataSegment].Data;
			StringBuilder sbFunctionRef= new StringBuilder();
			Regex rxFunction = new Regex("^[a-zA-Z0-9_]+\\(\\)\\s+:\\s+[a-zA-Z0-9_]+\\.c$");
			Regex rxFunction1 = new Regex("^[a-zA-Z0-9_]+\\s+:\\s+[a-zA-Z0-9_]+\\.c$");

			for (int i = 0; i < abData.Length; i++)
			{
				if (abData[i] == 0)
				{
					sbFunctionRef.Clear();
					for (int j = i + 1; j < abData.Length; j++)
					{
						if (abData[j] == 0)
							break;
						sbFunctionRef.Append((char)abData[j]);
					}
					if (sbFunctionRef.Length > 0 && 
						(rxFunction.Match(sbFunctionRef.ToString()).Success ||
						rxFunction1.Match(sbFunctionRef.ToString()).Success))
					{
						// try to match function
						// how to find function entry point?
						Console.WriteLine(sbFunctionRef.ToString());
					}
				}
			}*/

			// sort functions by address
			oDecompiler.SortFunctionsByAddress();

			// assign namespaces
			for (int i = 0; i < exe.Segments.Count; i++)
			{
				Segment segment = exe.Segments[i];

				if (string.IsNullOrEmpty(segment.Namespace))
				{
					segment.Namespace = string.Format("Segment{0}", i);
				}
			}

			StreamWriter writer;
			List<Segment> aCodeSegments = new List<Segment>();

			for (int i = 0; i < exe.Segments.Count; i++)
			{
				Segment segment = exe.Segments[i];
				if (segment.CompareFlag(SegmentFlagsEnum.DataSegment))
					continue;

				List<CFunction> aFunctions = new List<CFunction>();

				for (int j = 0; j < oDecompiler.GlobalNamespace.Functions.Count; j++)
				{
					CFunction function = oDecompiler.GlobalNamespace.Functions[j].Value;
					if (function.Segment == (uint)i)
					{
						aFunctions.Add(function);
					}
				}
				if (aFunctions.Count > 0)
				{
					aCodeSegments.Add(segment);

					writer = new StreamWriter(string.Format("{0}Code\\{1}.cs", path, segment.Namespace));
					writer.WriteLine("using Disassembler;");
					writer.WriteLine();
					writer.WriteLine("namespace Civilization1");
					writer.WriteLine("{");
					writer.WriteLine("\tpublic class {0}", segment.Namespace);
					writer.WriteLine("\t{");
					writer.WriteLine("\t\tprivate Civilization oParent;");
					writer.WriteLine();
					writer.WriteLine("\t\tpublic {0}(Civilization parent)", segment.Namespace);
					writer.WriteLine("\t\t{");
					writer.WriteLine("\t\t\tthis.oParent = parent;");
					writer.WriteLine("\t\t}");

					for (int j = 0; j < aFunctions.Count; j++)
					{
						CFunction function = aFunctions[j];

						// skip this blank function reference function
						if (function.Segment == 41 && function.Offset == 0x3c)
							continue;

						writer.WriteLine();
						writer.WriteLine("\t\tpublic void {0}()", function.Name);
						writer.WriteLine("\t\t{");
						writer.WriteLine("\t\t\tthis.oParent.LogWriteLine(\"Entering function '{0}'({1}) at {2}:0x{3:x}, stack: 0x{4:x}\");",
							function.Name, function.CallType.ToString(), function.Segment, function.Offset, function.StackSize);

						writer.WriteLine("\t\t\tCPURegister rAX = new CPURegister();");
						writer.WriteLine("\t\t\tCPURegister rCX = new CPURegister();");
						writer.WriteLine("\t\t\tCPURegister rDX = new CPURegister();");
						writer.WriteLine("\t\t\tCPURegister rBX = new CPURegister();");
						writer.WriteLine("\t\t\tCPURegister rSI = new CPURegister();");
						writer.WriteLine("\t\t\tCPURegister rDI = new CPURegister();");
						writer.WriteLine("\t\t\tCPURegister rES = new CPURegister();");
						writer.WriteLine("\t\t\tCPURegister rDS = new CPURegister(132);");
						writer.WriteLine();
						//writer.WriteLine("\t\tthis.oParent.CPU.PushWord({0});", function.Segment);
						//writer.WriteLine("\t\tthis.oParent.CPU.PushWord(0x{0:x});", function.Offset);
						//writer.WriteLine("\t\t// function body");
						for (int k = 0; k < function.Instructions.Count; k++)
						{
							// writer.WriteLine("\t\t{0}\t{1}", function.Instructions[j].Location.ToString(), function.Instructions[j]);
							Instruction instruction = function.Instructions[k];

							if (instruction.Label)
							{
								writer.WriteLine("\t\tL{0:x8}:", instruction.LinearAddress);
							}

							uint uiOffset = 0;
							InstructionParameter parameter;
							Instruction instruction1 = null;

							switch (instruction.InstructionType)
							{
								case InstructionEnum.ADC:
								case InstructionEnum.ADD:
								case InstructionEnum.AND:
								case InstructionEnum.OR:
								case InstructionEnum.SBB:
								case InstructionEnum.SUB:
								case InstructionEnum.XOR:
									parameter = instruction.Parameters[0];
									writer.Write("\t\t\t");
									writer.WriteLine(parameter.ToDestinationCSText(instruction.OperandSize, string.Format("this.oParent.CPU.{0}{1}({2}, {3})",
										instruction.InstructionType.ToString(), instruction.OperandSize.ToString(),
										parameter.ToSourceCSText(instruction.OperandSize), instruction.Parameters[1].ToSourceCSText(instruction.OperandSize))));
									break;
								case InstructionEnum.SAR:
								case InstructionEnum.SHL:
								case InstructionEnum.SHR:
									parameter = instruction.Parameters[0];
									writer.Write("\t\t\t");
									writer.WriteLine(parameter.ToDestinationCSText(instruction.OperandSize, string.Format("this.oParent.CPU.{0}{1}({2}, {3})",
										instruction.InstructionType.ToString(), instruction.OperandSize.ToString(),
										parameter.ToSourceCSText(instruction.OperandSize), instruction.Parameters[1].ToSourceCSText(instruction.Parameters[1].Size))));
									break;
								case InstructionEnum.SHLD:
									parameter = instruction.Parameters[0];
									writer.Write("\t\t\t");
									writer.WriteLine(parameter.ToDestinationCSText(instruction.OperandSize, string.Format("this.oParent.CPU.{0}1{1}({2}, {3}, {4})",
										instruction.InstructionType.ToString(), instruction.OperandSize.ToString(),
										parameter.ToSourceCSText(instruction.OperandSize), instruction.Parameters[1].ToSourceCSText(instruction.OperandSize),
										instruction.Parameters[2].ToSourceCSText(instruction.Parameters[2].Size))));
									break;
								case InstructionEnum.CALLF:
									parameter = instruction.Parameters[0];
									if (parameter.Type == InstructionParameterTypeEnum.SegmentOffset)
									{
										CFunction function1 = oDecompiler.GetFunction(parameter.Segment, (ushort)(parameter.Value & 0xffff));
										if (function1.Segment == 0)
										{
											writer.WriteLine("\t\t\tthis.oParent.{0}.{1}();", "CAPI", function1.Name.Replace("@", ""));
										}
										else if (function1.Segment != i)
										{
											if (function1.Segment >= exe.Segments.Count)
											{
												writer.WriteLine("\t\t\t{0}",
													AppendReturn(exe, function1,
													string.Format("this.oParent.{0}.{1}()", exe.ModuleReferences[(int)(function1.Segment - exe.Segments.Count)],
													function1.Name)));
											}
											else
											{
												writer.WriteLine("\t\t\tthis.oParent.{0}.{1}();",
													exe.Segments[(int)function1.Segment].Namespace, function1.Name);
											}
										}
										else
										{
											writer.WriteLine("\t\t\t{0}();", function1.Name);
										}
									}
									else
									{
										writer.WriteLine("\t\t\tthis.oParent.CPU.Call({0});", string.Format("this.oParent.CPU.ReadDWord({0}, {1})",
											parameter.GetSegmentText(),
											parameter.ToCSText(instruction.OperandSize)));
									}
									break;
								case InstructionEnum.CBW:
									if (instruction.OperandSize == InstructionSizeEnum.Word)
									{
										writer.WriteLine("\t\t\tthis.oParent.CPU.CBW(rAX);");
									}
									else
									{
										writer.WriteLine("\t\t\tthis.oParent.CPU.CWDE(rAX);");
									}
									break;
								case InstructionEnum.CWD:
									if (instruction.OperandSize == InstructionSizeEnum.Word)
									{
										writer.WriteLine("\t\t\tthis.oParent.CPU.CWD(rAX, rDX);");
									}
									else
									{
										writer.WriteLine("\t\t\tthis.oParent.CPU.CDQ(rAX, rDX);");
									}
									break;
								case InstructionEnum.CMP:
								case InstructionEnum.TEST:
									parameter = instruction.Parameters[0];
									writer.WriteLine("\t\t\tthis.oParent.CPU.{0}{1}({2}, {3});", instruction.InstructionType.ToString(), instruction.OperandSize.ToString(),
										parameter.ToSourceCSText(instruction.OperandSize), instruction.Parameters[1].ToSourceCSText(instruction.OperandSize));
									break;
								case InstructionEnum.DIV:
								case InstructionEnum.IDIV:
									writer.WriteLine("\t\t\tthis.oParent.CPU.{0}{1}(rAX, rDX, {2});", instruction.InstructionType.ToString(), instruction.OperandSize.ToString(),
										instruction.Parameters[0].ToSourceCSText(instruction.OperandSize));
									break;

								// FPU instructions
								case InstructionEnum.FADDP:
									parameter = instruction.Parameters[0];
									writer.WriteLine("\t\t\tthis.oParent.CPU.FADDP({0});", parameter.Value);
									break;
								case InstructionEnum.FILD:
									parameter = instruction.Parameters[0];
									switch (instruction.Bytes[0])
									{
										case 0xdf:
											if ((instruction.Bytes[1] & 0x38) == 0)
											{
												writer.WriteLine("\t\t\tthis.oParent.CPU.FILD16({0}, {1});",
													(parameter.DataSegment == SegmentRegisterEnum.Immediate) ? string.Format("0x{0:x}", parameter.Segment) : string.Format("this.oParent.CPU.{0}.Word", parameter.DataSegment.ToString()),
													parameter.ToCSText(instruction.OperandSize));
											}
											else
											{
												writer.WriteLine("\t\t\tthis.oParent.CPU.FILD64({0}, {1});",
													(parameter.DataSegment == SegmentRegisterEnum.Immediate) ? string.Format("0x{0:x}", parameter.Segment) : string.Format("this.oParent.CPU.{0}.Word", parameter.DataSegment.ToString()),
													parameter.ToCSText(instruction.OperandSize));
											}
											break;
										case 0xdb:
											writer.WriteLine("\t\t\tthis.oParent.CPU.FILD32({0}, {1});",
												(parameter.DataSegment == SegmentRegisterEnum.Immediate) ? string.Format("0x{0:x}", parameter.Segment) : string.Format("this.oParent.CPU.{0}.Word", parameter.DataSegment.ToString()),
												parameter.ToCSText(instruction.OperandSize));
											break;
									}
									break;
								case InstructionEnum.FMUL:
									parameter = instruction.Parameters[0];
									if (parameter.Type == InstructionParameterTypeEnum.FPUStackAddress)
									{
										writer.WriteLine("\t\t\tthis.oParent.CPU.FMULST({0}, {1});", instruction.FPUDestination0, parameter.Value);
									}
									else
									{
										switch (instruction.Bytes[0])
										{
											case 0xd8:
												writer.WriteLine("\t\t\tthis.oParent.CPU.FMUL32({0}, {1});",
													parameter.GetSegmentText(), parameter.ToCSText(instruction.OperandSize));
												break;
											case 0xdc:
												writer.WriteLine("\t\t\tthis.oParent.CPU.FMUL64({0}, {1});",
													parameter.GetSegmentText(), parameter.ToCSText(instruction.OperandSize));
												break;
										}
									}
									break;
								case InstructionEnum.WAIT:
									// ignore this instruction
									break;

								case InstructionEnum.IMUL:
									switch (instruction.Parameters.Count)
									{
										case 1:
											parameter = instruction.Parameters[0];
											writer.WriteLine("\t\t\tthis.oParent.CPU.{0}{1}(rAX, rDX, {2});", instruction.InstructionType.ToString(), instruction.OperandSize.ToString(),
												parameter.ToSourceCSText(instruction.OperandSize));
											break;
										case 2:
											parameter = instruction.Parameters[0];
											writer.Write("\t\t\t");
											writer.WriteLine(parameter.ToDestinationCSText(instruction.OperandSize, string.Format("this.oParent.CPU.{0}1{1}({2}, {3})",
												instruction.InstructionType.ToString(), instruction.OperandSize.ToString(),
												parameter.ToSourceCSText(instruction.OperandSize), instruction.Parameters[1].ToSourceCSText(instruction.OperandSize))));
											break;
										case 3:
											parameter = instruction.Parameters[0];
											writer.Write("\t\t\t");
											writer.WriteLine(parameter.ToDestinationCSText(instruction.OperandSize, string.Format("this.oParent.CPU.{0}1{1}({2}, {3})",
												instruction.InstructionType.ToString(), instruction.OperandSize.ToString(),
												instruction.Parameters[1].ToSourceCSText(instruction.OperandSize), instruction.Parameters[2].ToSourceCSText(instruction.OperandSize))));
											break;
										default:
											throw new Exception("Unknown IMUL instruction");
									}
									break;
								case InstructionEnum.LDS:
									parameter = instruction.Parameters[1];
									writer.WriteLine("\t\t\t// LDS");
									writer.WriteLine("\t\t\t{0}", instruction.Parameters[0].ToDestinationCSText(instruction.OperandSize, parameter.ToSourceCSText(instruction.OperandSize)));
									writer.WriteLine("\t\t\trDS.{0} = this.oParent.CPU.Read{0}({1}, (ushort)({2} + 2));",
										instruction.OperandSize.ToString(),
										parameter.GetSegmentText(),
										parameter.ToCSText(instruction.OperandSize));
									break;
								case InstructionEnum.LES:
									parameter = instruction.Parameters[1];
									/*if (instruction.Parameters[0].Value != (uint)(((uint)RegisterEnum.BX) & 0x7))
									{
										Console.WriteLine("LES uses {0} in {1}.{2} ", instruction.Parameters[0].ToString(), 
											segment.Namespace, function.Name);
									}*/
									writer.WriteLine("\t\t\t// LES");
									writer.WriteLine("\t\t\t{0}", instruction.Parameters[0].ToDestinationCSText(instruction.OperandSize, parameter.ToSourceCSText(instruction.OperandSize)));
									writer.WriteLine("\t\t\trES.{0} = this.oParent.CPU.Read{0}({1}, (ushort)({2} + 2));",
										instruction.OperandSize.ToString(),
										parameter.GetSegmentText(),
										parameter.ToCSText(instruction.OperandSize));
									break;
								case InstructionEnum.LEA:
									parameter = instruction.Parameters[1];
									writer.WriteLine("\t\t\t// LEA");
									writer.Write("\t\t\t");
									writer.Write(instruction.Parameters[0].ToDestinationCSText(instruction.OperandSize, parameter.ToCSText(instruction.OperandSize)));
									if (parameter.ReferenceType != InstructionParameterReferenceEnum.None)
									{
										writer.WriteLine(" // {0}", parameter.ReferenceType.ToString());
									}
									else
									{
										writer.WriteLine();
									}
									break;
								case InstructionEnum.MOV:
									parameter = instruction.Parameters[1];
									writer.Write("\t\t\t");
									writer.Write(instruction.Parameters[0].ToDestinationCSText(instruction.OperandSize, parameter.ToSourceCSText(instruction.OperandSize)));
									if (parameter.ReferenceType != InstructionParameterReferenceEnum.None)
									{
										writer.WriteLine(" // {0}", parameter.ReferenceType.ToString());
									}
									else
									{
										writer.WriteLine();
									}
									break;
								case InstructionEnum.MOVS:
									writer.Write("\t\t\tthis.oParent.CPU.");
									if (instruction.RepPrefix == InstructionPrefixEnum.REPE || instruction.RepPrefix == InstructionPrefixEnum.REPNE)
									{
										writer.Write("{0}", instruction.RepPrefix.ToString());
									}
									writer.WriteLine("MOVS{0}({1}, rSI, rES, rDI, rCX);", instruction.OperandSize.ToString(), instruction.GetDefaultDataSegmentText());
									break;
								case InstructionEnum.MOVSX:
								case InstructionEnum.MOVZX:
									parameter = instruction.Parameters[0];
									writer.Write("\t\t\t");
									writer.WriteLine(parameter.ToDestinationCSText(instruction.OperandSize, string.Format("this.oParent.CPU.{0}{1}({2})",
										instruction.InstructionType.ToString(), instruction.OperandSize.ToString(),
										instruction.Parameters[1].ToSourceCSText(instruction.Parameters[1].Size))));
									break;
								case InstructionEnum.DEC:
								case InstructionEnum.INC:
								case InstructionEnum.NEG:
								case InstructionEnum.NOT:
									parameter = instruction.Parameters[0];
									writer.Write("\t\t\t");
									writer.WriteLine(parameter.ToDestinationCSText(instruction.OperandSize, string.Format("this.oParent.CPU.{0}{1}({2})",
										instruction.InstructionType.ToString(), instruction.OperandSize.ToString(), parameter.ToSourceCSText(instruction.OperandSize))));
									break;
								case InstructionEnum.NOP:
									// ignore this instruction
									break;

								// stack instructions
								case InstructionEnum.POP:
									parameter = instruction.Parameters[0];
									/*Console.WriteLine("POP {0} in {1}.{2} ", instruction.Parameters[0].ToString(),
										segment.Namespace, function.Name);*/
									writer.Write("\t\t\t");
									writer.WriteLine(parameter.ToDestinationCSText(instruction.OperandSize, string.Format("this.oParent.CPU.Pop{0}()",
										instruction.OperandSize.ToString())));
									break;
								case InstructionEnum.POPA:
									writer.WriteLine("\t\t\tthis.oParent.CPU.PopA{0}(rAX, rCX, rDX, rBX, rSI, rDI);", instruction.OperandSize.ToString());
									break;
								case InstructionEnum.PUSH:
									parameter = instruction.Parameters[0];
									writer.Write("\t\t\tthis.oParent.CPU.Push{0}({1});", instruction.OperandSize.ToString(), parameter.ToSourceCSText(instruction.OperandSize));
									if (parameter.ReferenceType != InstructionParameterReferenceEnum.None)
									{
										writer.WriteLine(" // {0}", parameter.ReferenceType.ToString());
									}
									else
									{
										writer.WriteLine();
									}
									break;
								case InstructionEnum.PUSHA:
									writer.WriteLine("\t\t\tthis.oParent.CPU.PushA{0}(rAX, rCX, rDX, rBX, rSI, rDI);", instruction.OperandSize.ToString());
									break;
								case InstructionEnum.STD:
									writer.WriteLine("\t\t\tthis.oParent.CPU.Flags.D = true;");
									break;
								case InstructionEnum.XCHG:
									parameter = instruction.Parameters[0];
									//writer.WriteLine("\t\t// XCHG");
									writer.WriteLine("\t\t\tthis.oParent.CPU.Temp.{0} = {1};", instruction.OperandSize.ToString(), parameter.ToSourceCSText(instruction.OperandSize));
									writer.Write("\t\t\t");
									writer.WriteLine(parameter.ToDestinationCSText(instruction.OperandSize, instruction.Parameters[1].ToSourceCSText(instruction.OperandSize)));
									parameter = instruction.Parameters[1];
									writer.Write("\t\t\t");
									writer.WriteLine(parameter.ToDestinationCSText(instruction.OperandSize, string.Format("this.oParent.CPU.Temp.{0}", instruction.OperandSize.ToString())));
									break;

								// special syntetic functions
								case InstructionEnum.WordsToDword:
									writer.WriteLine("\t\t\tr{0}.DWord = (uint)(((uint)r{1}.Word << 16) | (uint)r{2}.Word);",
										(RegisterEnum)(instruction.Parameters[0].Value + 8),
										(RegisterEnum)(instruction.Parameters[1].Value + 8),
										(RegisterEnum)(instruction.Parameters[2].Value + 8));
									break;

								// flow control instructions
								case InstructionEnum.SWITCH:
									writer.WriteLine("\t\t\tswitch({0})", instruction.Parameters[0].ToSourceCSText(instruction.OperandSize));
									writer.WriteLine("\t\t\t{");
									for (int l = 1; l < instruction.Parameters.Count; l++)
									{
										parameter = instruction.Parameters[l];
										if (l < instruction.Parameters.Count - 1)
										{
											writer.WriteLine("\t\t\t\tcase {0}:", parameter.Value);
											writer.WriteLine("\t\t\t\t\tgoto L{0:x4};", parameter.Displacement);
										}
										else
										{
											writer.WriteLine("\t\t\t\tdefault:");
											writer.WriteLine("\t\t\t\t\tgoto L{0:x4};", parameter.Displacement);
										}
									}
									writer.WriteLine("\t\t\t}");
									break;
								case InstructionEnum.Jcc:
									if (k < 1 ||
										((instruction1 = function.Instructions[k - 1]).InstructionType != InstructionEnum.CMP &&
										instruction1.InstructionType != InstructionEnum.TEST &&
										instruction1.InstructionType != InstructionEnum.OR &&
										instruction1.InstructionType != InstructionEnum.DEC &&
										instruction1.InstructionType != InstructionEnum.INC &&
										instruction1.InstructionType != InstructionEnum.SUB &&
										instruction1.InstructionType != InstructionEnum.ADD))
									{
										Console.WriteLine("Found conditional jump at 0x{1:x8}, instruction {2}, previous: {3}",
											instruction.LinearAddress, instruction.ToString(), instruction1.ToString());
									}

									uiOffset = (instruction.LinearAddress + (uint)instruction.Bytes.Count + instruction.Parameters[1].Value) & 0xffff;
									writer.WriteLine("\t\t\tif (this.oParent.CPU.Flags.{0}) goto L{1:x4};",
										((ConditionEnum)instruction.Parameters[0].Value).ToString(), uiOffset);
									break;
								case InstructionEnum.JMP:
									uiOffset = (instruction.LinearAddress + (uint)instruction.Bytes.Count + instruction.Parameters[0].Value) & 0xffff;
									writer.WriteLine("\t\t\tgoto L{0:x4};", uiOffset);
									break;
								case InstructionEnum.LOOP:
									uiOffset = (instruction.LinearAddress + (uint)instruction.Bytes.Count + instruction.Parameters[0].Value) & 0xffff;
									writer.WriteLine("\t\t\tif (this.oParent.CPU.Loop(rCX)) goto L{0:x4};", uiOffset);
									break;
								case InstructionEnum.RETF:
									//writer.WriteLine("\t\t// end function body");
									//writer.WriteLine("\t\tthis.oParent.CPU.PopWord();");
									//writer.WriteLine("\t\tthis.oParent.CPU.PopWord();");
									/*if (instruction.Parameters.Count > 0)
									{
										writer.WriteLine("\t\tthis.oParent.CPU.SP.Word += {0};", instruction.Parameters[0].ToCSText(instruction.OperandSize));
									}*/
									writer.WriteLine("\t\t\tthis.oParent.LogWriteLine(\"Exiting function '{0}'\");", function.Name);
									if (k != function.Instructions.Count - 1)
										writer.WriteLine("\t\t\treturn;");
									break;
								case InstructionEnum.If:
									uiOffset = (instruction.LinearAddress + (uint)instruction.Bytes.Count + instruction.Parameters[3].Value) & 0xffff;
									writer.WriteLine("\t\t\tif ({0} {1} {2}) goto L{3:x4};",
										instruction.Parameters[0].ToSourceCSText(instruction.Parameters[0].Size),
										ConditionToCSText((ConditionEnum)instruction.Parameters[2].Value),
										instruction.Parameters[1].ToSourceCSText(instruction.Parameters[1].Size),
										uiOffset);
									break;
								case InstructionEnum.IfAnd:
									uiOffset = (instruction.LinearAddress + (uint)instruction.Bytes.Count + instruction.Parameters[3].Value) & 0xffff;
									writer.WriteLine("\t\t\tif (({0} & {2}) {1} 0) goto L{3:x4};",
										instruction.Parameters[0].ToSourceCSText(instruction.Parameters[0].Size),
										ConditionToCSText((ConditionEnum)instruction.Parameters[2].Value),
										instruction.Parameters[1].ToSourceCSText(instruction.Parameters[1].Size),
										uiOffset);
									break;
								case InstructionEnum.IfOr:
									uiOffset = (instruction.LinearAddress + (uint)instruction.Bytes.Count + instruction.Parameters[3].Value) & 0xffff;
									writer.WriteLine("\t\t\tif ({0} {1} 0) goto L{2:x4};",
										instruction.Parameters[0].ToSourceCSText(instruction.Parameters[0].Size),
										ConditionToCSText((ConditionEnum)instruction.Parameters[2].Value),
										uiOffset);
									break;
								default:
									throw new Exception("Unexpected instruction");
							}
						}
						writer.WriteLine("\t\t}");
					}

					writer.WriteLine("\t}");
					writer.WriteLine("}");
					writer.Close();
				}
			}

			writer = new StreamWriter(string.Format("{0}{1}.cs", path, "CivilizationSegments"));
			for (int i = 0; i < aCodeSegments.Count; i++)
			{
				Segment segment = aCodeSegments[i];
				writer.WriteLine("private {0} o{0};", segment.Namespace);
			}
			writer.WriteLine();
			writer.WriteLine("#region Segment initializations");
			for (int i = 0; i < aCodeSegments.Count; i++)
			{
				Segment segment = aCodeSegments[i];
				writer.WriteLine("this.o{0} = new {0}(this);", segment.Namespace);
			}
			writer.WriteLine("#endregion");
			writer.WriteLine();
			writer.WriteLine("#region Public Segment getters");
			for (int i = 0; i < aCodeSegments.Count; i++)
			{
				Segment segment = aCodeSegments[i];
				writer.WriteLine("public {0} {0}", segment.Namespace);
				writer.WriteLine("{");
				writer.WriteLine("\tget {{ return this.o{0}; }}", segment.Namespace);
				writer.WriteLine("}");
			}
			writer.WriteLine("#endregion");
			writer.Close();

			writer = new StreamWriter(string.Format("{0}{1}.log", path, "SegmentMap"));
			uint uiSegment1 = 0;
			uint uiOffset1 = 0;

			for (int i = 0; i < oDecompiler.GlobalNamespace.Functions.Count; i++)
			{
				CFunction function = oDecompiler.GlobalNamespace.Functions[i].Value;
				Segment segment = exe.Segments[(int)function.Segment];
				Instruction lastInstruction = function.Instructions[function.Instructions.Count - 1];

				if (function.Segment != uiSegment1)
				{
					if (uiSegment1 > 0 && uiOffset1 != exe.Segments[(int)uiSegment1].Data.Length)
					{
						writer.WriteLine("Undefined space 0x{0:x} - 0x{1:x}", uiOffset1, exe.Segments[(int)uiSegment1].Data.Length - 1);
					}

					while (uiSegment1 + 1 < function.Segment)
					{
						uiSegment1++;
						writer.WriteLine();
						writer.WriteLine("Undefined segment: {0}", uiSegment1);
					}

					uiSegment1 = function.Segment;
					uiOffset1 = 0;
					if (uiSegment1 > 0)
						writer.WriteLine();

					writer.WriteLine("Segment: {0}, Size: 0x{1:x}", uiSegment1, exe.Segments[(int)uiSegment1].Data.Length);
				}

				if (function.Offset != uiOffset1)
				{
					writer.WriteLine("Undefined space 0x{0:x} - 0x{1:x}", uiOffset1, function.Offset - 1);
				}

				writer.WriteLine("('{0}'), '{1}' {2}:0x{3:x} - 0x{4:x}",
					segment.Namespace, function.Name, function.Segment, function.Offset,
					lastInstruction.LinearAddress + lastInstruction.Bytes.Count - 1);

				uiOffset1 = lastInstruction.LinearAddress + (uint)lastInstruction.Bytes.Count;
			}
			if (uiOffset1 != exe.Segments[(int)uiSegment1].Data.Length)
			{
				writer.WriteLine("Undefined space 0x{0:x} - 0x{1:x}", uiOffset1, exe.Segments[(int)uiSegment1].Data.Length - 1);
			}
			writer.Close();


			writer = new StreamWriter(string.Format("{0}{1}.cs", path, "CivilizationData"));

			for (int i = 0; i < exe.Segments.Count; i++)
			{
				Segment segment = exe.Segments[i];
				if ((segment.Flags & SegmentFlagsEnum.DataSegment) == SegmentFlagsEnum.DataSegment &&
					segment.Data.Length > 0)
				{
					writer.WriteLine("this.oParent.CPU.AddMemoryBlock(0x{0:x}0000, new byte[] {{", i);

					for (int j = 0; j < segment.Data.Length; j++)
					{
						if (j > 0)
							writer.Write(", ");

						if (j > 0 && (j & 0xf) == 0)
						{
							writer.WriteLine(" // 0x{0:x}", (j & 0xffff0) - 0x10);
						}

						byte ch = segment.Data[j];
						if (ch > 31 && ch < 127)
						{
							if (ch == (byte)'\'' || ch == '\\')
								writer.Write("(byte)'\\{0}'", (char)ch);
							else
								writer.Write("(byte)'{0}'", (char)ch);
						}
						else
							writer.Write("0x{0:x2}", ch);
					}
					writer.WriteLine("});");
					writer.WriteLine();
				}
			}

			writer.Close();

			for (int i = 0; i < exe.Segments.Count; i++)
			{
				Segment segment = exe.Segments[i];
				if ((segment.Flags & SegmentFlagsEnum.DataSegment) == SegmentFlagsEnum.DataSegment &&
					segment.Data.Length > 0)
				{
					FileStream bWriter = new FileStream($"{path}Data{Path.DirectorySeparatorChar}Data_{i:x}.bin", FileMode.Create);
					bWriter.Write(segment.Data, 0, segment.Data.Length);
					bWriter.Close();
				}
			}

			/*for (int i = 0; i < CFunction.Statistics.Count; i++)
			{
				Console.WriteLine("{0}\t{1}", CFunction.Statistics[i].Key.ToString(), CFunction.Statistics[i].Value);
			}*/

			Console.WriteLine("Finished");
		}
		else
		{
			Console.WriteLine("Can't find WinMain function!");
		}
	}

	private static string AppendReturn(NewExecutable exe, CFunction function, string call)
	{
		switch (function.ReturnValue.Size)
		{
			case 1:
			case 2:
				return "rAX.Word = " + call + ";";
			case 4:
				return "this.oParent.DWordToWords(rAX, rDX, " + call + ");";
		}

		return call + ";";
	}

	private static string ConditionToCSText(ConditionEnum condition)
	{
		switch (condition)
		{
			case ConditionEnum.B:
				return "<";
			case ConditionEnum.AE:
				return ">=";
			case ConditionEnum.E:
				return "==";
			case ConditionEnum.NE:
				return "!=";
			case ConditionEnum.BE:
				return "<=";
			case ConditionEnum.A:
				return ">";
			case ConditionEnum.L:
				return "<";
			case ConditionEnum.GE:
				return ">=";
			case ConditionEnum.LE:
				return "<=";
			case ConditionEnum.G:
				return ">";
		}

		return "!!!";
	}

	private static void MakeTable()
	{
		StreamReader file = new StreamReader(@"C:\Users\rajko\Documents\Projects\Disassembler\CPU\Instructions.txt");
		List<OpCodeInstructionDefinition> aInstructions = new List<OpCodeInstructionDefinition>();

		while (!file.EndOfStream)
		{
			string sLine = file.ReadLine();
			string[] aParts = sLine.Split('\t');
			if (aParts.Length != 4 && aParts.Length != 6)
				throw new Exception("Malformed line '" + sLine + "'");

			string sName = aParts[0].Trim();
			string sDescription = aParts[1].Trim();
			string sOpCode = aParts[2].Trim();
			string sCPU = aParts[3].Trim();
			string sModifiedFlags = null;
			string sUndefinedFlags = null;
			if (aParts.Length > 4)
			{
				sModifiedFlags = aParts[4].Trim();
				sUndefinedFlags = aParts[5].Trim();
			}

			aInstructions.Add(new OpCodeInstructionDefinition(sName, sDescription, sOpCode, sCPU, sModifiedFlags, sUndefinedFlags));
		}
		file.Close();

		BDictionary<int, List<int>> aTable = new BDictionary<int, List<int>>();

		for (int i = 0; i < aInstructions.Count; i++)
		{
			OpCodeInstructionDefinition instruction = aInstructions[i];

			if (instruction.OpCodes.Count == 0)
				throw new Exception("Instruction definition not properly initialized");

			byte[] aOpCodes = instruction.OpCodes[0].Expand();

			if (aOpCodes == null || aOpCodes.Length == 0)
				throw new Exception("Instruction definition not properly initialized");

			for (int j = 0; j < aOpCodes.Length; j++)
			{
				if (aTable.ContainsKey(aOpCodes[j]))
				{
					aTable.GetValueByKey(aOpCodes[j]).Add(i);
				}
				else
				{
					List<int> aTemp = new List<int>();
					aTemp.Add(i);
					aTable.Add(aOpCodes[j], aTemp);
				}
			}
		}

		StreamWriter writer = new StreamWriter("table.log");
		int tabs = 0;
		writer.WriteLine("{0}public partial class Instruction", Tabs(tabs));
		writer.WriteLine("{0}{{", Tabs(tabs));
		tabs++;
		writer.WriteLine("{0}private void Decode(MemoryStream stream)", Tabs(tabs));
		writer.WriteLine("{0}{{", Tabs(tabs));
		tabs++;
		writer.WriteLine("{0}bool bPrefix;", Tabs(tabs));
		writer.WriteLine("{0}bool bSignExtendImmediate = false;", Tabs(tabs));
		writer.WriteLine("{0}bool bReverseDirection = false;", Tabs(tabs));
		writer.WriteLine("{0}InstructionSizeEnum eOperandSize = this.eDefaultSize;", Tabs(tabs));
		writer.WriteLine("{0}InstructionSizeEnum eAddressSize = this.eDefaultSize;", Tabs(tabs));
		writer.WriteLine();
		writer.WriteLine("{0}do", Tabs(tabs));
		writer.WriteLine("{0}{{", Tabs(tabs));
		tabs++;
		writer.WriteLine("{0}bool bExitCase = false;", Tabs(tabs));
		writer.WriteLine("{0}this.iByte0 = stream.ReadByte();", Tabs(tabs));
		writer.WriteLine("{0}if (this.iByte0 < 0)", Tabs(tabs));
		writer.WriteLine("{0}{{", Tabs(tabs));
		writer.WriteLine("{0}bInvalid = true;", Tabs(tabs + 1));
		writer.WriteLine("{0}return;", Tabs(tabs + 1));
		writer.WriteLine("{0}}}", Tabs(tabs));
		writer.WriteLine("{0}this.aBytes.Add((byte)this.iByte0);", Tabs(tabs));
		writer.WriteLine();
		writer.WriteLine("{0}bPrefix = false;", Tabs(tabs));
		writer.WriteLine("{0}switch(this.iByte0)", Tabs(tabs));
		writer.WriteLine("{0}{{", Tabs(tabs));
		tabs++;

		for (int i = 0; i < 256; i++)
		{
			if (aTable.ContainsKey(i))
			{
				List<int> aTemp = aTable.GetValueByKey(i);

				if (i == 0x90)
				{
					// NOP instruction, clear xchg instruction
					for (int j = 0; j < aTemp.Count; j++)
					{
						if (aInstructions[aTemp[j]].Instruction != InstructionEnum.NOP)
						{
							aTemp.RemoveAt(j);
							j--;
						}
					}
				}

				if (aTemp.Count > 0)
				{
					writer.WriteLine("{0}case 0x{1:x2}:", Tabs(tabs), i);

					for (int j = i + 1; j < 256; j++)
					{
						if (aTable.ContainsKey(j))
						{
							bool bMatch = false;
							List<int> aTemp1 = aTable.GetValueByKey(j);

							if (aTemp.Count == aTemp1.Count)
							{
								bMatch = true;

								for (int k = 0; k < aTemp.Count; k++)
								{
									if (aTemp[k] != aTemp1[k])
									{
										bMatch = false;
										break;
									}
								}
							}

							if (bMatch)
							{
								// we have a match, no need to repeat it again
								writer.WriteLine("{0}case 0x{1:x2}:", Tabs(tabs), j);
								aTable.GetValueByKey(j).Clear();
							}
						}
					}

					tabs++;
					if (aTemp.Count == 1)
					{
						// only one instruction to decode
						EncodeInstruction(writer, aInstructions, aTemp[0], tabs, 0);
					}
					else
					{
						// multiple instructions to decode
						EncodeInstructions(writer, aInstructions, aTemp, tabs, 1);
					}
					writer.WriteLine("{0}break;", Tabs(tabs));
					tabs--;
				}
			}
			else
			{
				//writer.WriteLine("\t\tcase 0x{0:x2}:", i);
				//writer.WriteLine("\t\t\t// This OpCode is not defined");
				//writer.WriteLine("\t\t\tbreak;");
			}
		}
		writer.WriteLine("{0}default:", Tabs(tabs));
		writer.WriteLine("{0}bInvalid = true;", Tabs(tabs + 1));
		writer.WriteLine("{0}break;", Tabs(tabs + 1));
		tabs--;
		writer.WriteLine("{0}}}", Tabs(tabs));
		tabs--;
		writer.WriteLine("{0}}} while(bPrefix);", Tabs(tabs));
		writer.WriteLine();
		writer.WriteLine("{0}if (bReverseDirection)", Tabs(tabs));
		writer.WriteLine("{0}{{", Tabs(tabs));
		tabs++;
		writer.WriteLine("{0}InstructionParameter oTemp = this.aParameters[0];", Tabs(tabs));
		writer.WriteLine("{0}this.aParameters[0] = this.aParameters[1];", Tabs(tabs));
		writer.WriteLine("{0}this.aParameters[1] = oTemp;", Tabs(tabs));
		tabs--;
		writer.WriteLine("{0}}}", Tabs(tabs));
		tabs--;
		writer.WriteLine("{0}}}", Tabs(tabs));
		tabs--;
		writer.WriteLine("{0}}}", Tabs(tabs));
		writer.Close();
	}

	private static void EncodeInstructions(StreamWriter writer, List<OpCodeInstructionDefinition> instructions, List<int> list, int tabs, int level)
	{
		int iMask = 0;
		bool bMaskEqual = true;

		for (int i = 0; i < list.Count; i++)
		{
			int index = list[i];
			OpCodeInstructionDefinition instruction = instructions[index];

			if (level >= instruction.OpCodes.Count)
			{
				//throw new Exception(string.Format("{0}// Can't expand, level {1} too great on: {2} ({3})", Tabs(tabs), level, instruction.Instruction, index));
				level--;
				bMaskEqual = false;
				break;
			}
			if (i == 0)
			{
				iMask = instruction.OpCodes[level].Mask;
				continue;
			}

			if (iMask != instruction.OpCodes[level].Mask)
			{
				bMaskEqual = false;
				break;
			}
		}

		if (bMaskEqual)
		{
			BDictionary<int, List<int>> aTable = new BDictionary<int, List<int>>();

			for (int i = 0; i < list.Count; i++)
			{
				int index = list[i];
				OpCodeInstructionDefinition instruction = instructions[index];
				int iOpCode = instruction.OpCodes[level].OpCode;

				if (aTable.ContainsKey(iOpCode))
				{
					aTable.GetValueByKey(iOpCode).Add(index);
				}
				else
				{
					List<int> aTemp = new List<int>();
					aTemp.Add(index);
					aTable.Add(iOpCode, aTemp);
				}
			}

			writer.WriteLine("{0}this.iByte{1} = stream.ReadByte();", Tabs(tabs), level);
			writer.WriteLine("{0}if (this.iByte{1} < 0)", Tabs(tabs), level);
			writer.WriteLine("{0}{{", Tabs(tabs));
			writer.WriteLine("{0}bInvalid = true;", Tabs(tabs + 1));
			writer.WriteLine("{0}return;", Tabs(tabs + 1));
			writer.WriteLine("{0}}}", Tabs(tabs));
			writer.WriteLine("{0}this.aBytes.Add((byte)this.iByte{1});", Tabs(tabs), level);
			writer.WriteLine();
			if (iMask != 0xff)
			{
				writer.WriteLine("{0}switch(this.iByte{1} & 0x{2:x2})", Tabs(tabs), level, iMask);
			}
			else
			{
				writer.WriteLine("{0}switch(this.iByte{1})", Tabs(tabs), level);
			}
			writer.WriteLine("{0}{{", Tabs(tabs));
			tabs++;

			for (int i = 0; i < 256; i++)
			{
				if (aTable.ContainsKey(i))
				{
					List<int> aTemp = aTable.GetValueByKey(i);
					if (aTemp.Count > 0)
					{
						writer.WriteLine("{0}case 0x{1:x2}:", Tabs(tabs), i);

						for (int j = i + 1; j < 256; j++)
						{
							if (aTable.ContainsKey(j))
							{
								bool bMatch = false;
								List<int> aTemp1 = aTable.GetValueByKey(j);

								if (aTemp.Count == aTemp1.Count)
								{
									bMatch = true;

									for (int k = 0; k < aTemp.Count; k++)
									{
										if (aTemp[k] != aTemp1[k])
										{
											bMatch = false;
											break;
										}
									}
								}

								if (bMatch)
								{
									// we have a match, no need to repeat it again
									writer.WriteLine("{0}case 0x{1:x2}:", Tabs(tabs), j);
									aTable.GetValueByKey(j).Clear();
								}
							}
						}
						tabs++;

						if (aTemp.Count == 1)
						{
							// only one instruction to decode
							EncodeInstruction(writer, instructions, aTemp[0], tabs, level + 1);
						}
						else
						{
							// multiple instructions to decode
							EncodeInstructions(writer, instructions, aTemp, tabs, level + 1);
						}
						writer.WriteLine("{0}break;", Tabs(tabs));
						tabs--;
					}
				}
			}
			writer.WriteLine("{0}default:", Tabs(tabs));
			writer.WriteLine("{0}bInvalid = true;", Tabs(tabs + 1));
			writer.WriteLine("{0}break;", Tabs(tabs + 1));
			tabs--;
			writer.WriteLine("{0}}}", Tabs(tabs));
		}
		else
		{
			writer.WriteLine("{0}// Multiple instructions, alternate method", Tabs(tabs));

			// first separate instructions with mask and without mask
			// there should be no more than one instruction without mask
			BDictionary<int, List<int>> aTable1 = new BDictionary<int, List<int>>();
			List<int> aTable2 = new List<int>();

			for (int i = 0; i < list.Count; i++)
			{
				int index = list[i];
				OpCodeInstructionDefinition instruction = instructions[index];
				iMask = instruction.OpCodes[level].Mask;

				if (iMask > 0)
				{
					if (aTable1.ContainsKey(iMask))
					{
						aTable1.GetValueByKey(iMask).Add(index);
					}
					else
					{
						List<int> aTemp = new List<int>();
						aTemp.Add(index);
						aTable1.Add(iMask, aTemp);
					}
				}
				else
				{
					aTable2.Add(index);
				}
			}

			// now we have two tables, first is mask based, and the other should be a single instruction

			writer.WriteLine("{0}this.iByte{1} = stream.ReadByte();", Tabs(tabs), level);
			writer.WriteLine("{0}if (this.iByte{1} < 0)", Tabs(tabs), level);
			writer.WriteLine("{0}{{", Tabs(tabs));
			writer.WriteLine("{0}bInvalid = true;", Tabs(tabs + 1));
			writer.WriteLine("{0}return;", Tabs(tabs + 1));
			writer.WriteLine("{0}}}", Tabs(tabs));
			writer.WriteLine("{0}this.aBytes.Add((byte)this.iByte{1});", Tabs(tabs), level);
			writer.WriteLine();

			// first list differentiates by descending mask size
			for (int i = 8; i >= 0; i--)
			{
				for (int j = 0; j < aTable1.Count; j++)
				{
					int iValue = aTable1[j].Key;
					int iCount = 0;

					for (int k = 0; k < 8; k++)
					{
						if ((iValue & (1 << k)) != 0)
							iCount++;
					}
					if (iCount == i)
					{
						if (aTable1[j].Value.Count > 1)
						{
							// make a third table, the masks are all equal, just differentiate by opcode
							iMask = aTable1[j].Key;
							BDictionary<int, List<int>> aTable3 = new BDictionary<int, List<int>>();
							List<int> list1 = aTable1[j].Value;

							// order opcodes ascending
							for (int k = 0; k < 256; k++)
							{
								for (int l = 0; l < list1.Count; l++)
								{
									int index = list1[l];
									OpCodeInstructionDefinition instruction = instructions[index];
									int iOpCode = instruction.OpCodes[level].OpCode;

									if (iOpCode == k)
									{
										if (aTable3.ContainsKey(iOpCode))
										{
											aTable3.GetValueByKey(iOpCode).Add(index);
										}
										else
										{
											List<int> aTemp = new List<int>();
											aTemp.Add(index);
											aTable3.Add(iOpCode, aTemp);
										}
									}
								}
							}

							writer.WriteLine("{0}bExitCase = false;", Tabs(tabs));
							if (iMask != 0xff)
							{
								writer.WriteLine("{0}switch(this.iByte{1} & 0x{2:x2})", Tabs(tabs), level, iMask);
							}
							else
							{
								writer.WriteLine("{0}switch(this.iByte{1})", Tabs(tabs), level);
							}
							writer.WriteLine("{0}{{", Tabs(tabs));
							tabs++;

							for (int k = 0; k < aTable3.Count; k++)
							{
								if (aTable3[k].Value.Count > 1)
								{
									writer.WriteLine("{0}case 0x{1:x2}:", Tabs(tabs), aTable3[k].Key);
									tabs++;
									EncodeInstructions(writer, instructions, aTable3[k].Value, tabs, level + 1);
									writer.WriteLine("{0}bExitCase = true;", Tabs(tabs));
									writer.WriteLine("{0}break;", Tabs(tabs));
									tabs--;
								}
								else
								{
									int index = aTable3[k].Value[0];

									writer.WriteLine("{0}case 0x{1:x2}:", Tabs(tabs), aTable3[k].Key);
									tabs++;
									EncodeInstruction(writer, instructions, index, tabs, level);
									writer.WriteLine("{0}bExitCase = true;", Tabs(tabs));
									writer.WriteLine("{0}break;", Tabs(tabs));
									tabs--;
								}
							}

							writer.WriteLine("{0}default:", Tabs(tabs));
							writer.WriteLine("{0}break;", Tabs(tabs + 1));
							tabs--;
							writer.WriteLine("{0}}}", Tabs(tabs));
							writer.WriteLine("{0}if (bExitCase)", Tabs(tabs));
							writer.WriteLine("{0}break;", Tabs(tabs + 1));
						}
						else
						{
							int index = aTable1[j].Value[0];
							OpCodeInstructionDefinition instruction = instructions[index];
							OpCodeDefinition op = instruction.OpCodes[level];

							// level 0 is just NOP and XCHG
							if (level > 0)
							{
								writer.WriteLine("{0}if ((this.iByte{1} & 0x{2:x2}) == 0x{3:x2})", Tabs(tabs), level, op.Mask, op.OpCode);
								writer.WriteLine("{0}{{", Tabs(tabs));
								EncodeInstruction(writer, instructions, index, tabs + 1, level);
								writer.WriteLine("{0}break;", Tabs(tabs + 1));
								writer.WriteLine("{0}}}", Tabs(tabs));
							}
							else if (instruction.Instruction == InstructionEnum.NOP)
							{
								// special case for NOP
								EncodeInstruction(writer, instructions, index, tabs, level);
							}
						}

						aTable1.RemoveAt(j);
						j--;
					}
				}
			}

			if (aTable2.Count > 0)
			{
				if (aTable2.Count > 0)
				{
					throw new Exception("Multiple instructions encountered");
				}

				EncodeInstruction(writer, instructions, aTable2[0], tabs, level);
			}
		}
	}

	private static void EncodeInstruction(StreamWriter writer, List<OpCodeInstructionDefinition> instructions, int index, int tabs, int level)
	{
		OpCodeInstructionDefinition instruction = instructions[index];

		if (instruction.Instruction == InstructionEnum.Undefined)
		{
			writer.WriteLine("{0}// Prefix: {1}", Tabs(tabs), instruction.Prefix);

			switch (instruction.Prefix)
			{
				case InstructionPrefixEnum.Lock:
					writer.WriteLine("{0}this.bLockPrefix = true;", Tabs(tabs));
					break;
				case InstructionPrefixEnum.OperandSize:
					writer.WriteLine("{0}eOperandSize = (this.eDefaultSize == InstructionSizeEnum.Word) ? InstructionSizeEnum.DWord : InstructionSizeEnum.Word;", Tabs(tabs));
					break;
				case InstructionPrefixEnum.AddressSize:
					writer.WriteLine("{0}eAddressSize = (this.eDefaultSize == InstructionSizeEnum.Word) ? InstructionSizeEnum.DWord : InstructionSizeEnum.Word;", Tabs(tabs));
					break;
				case InstructionPrefixEnum.REPE:
					writer.WriteLine("{0}this.eRepPrefix = InstructionPrefixEnum.REPE;", Tabs(tabs));
					break;
				case InstructionPrefixEnum.REPNE:
					writer.WriteLine("{0}this.eRepPrefix = InstructionPrefixEnum.REPNE;", Tabs(tabs));
					break;
				case InstructionPrefixEnum.ES:
					writer.WriteLine("{0}this.eDefaultDataSegment = SegmentRegisterEnum.ES;", Tabs(tabs));
					break;
				case InstructionPrefixEnum.CS:
					writer.WriteLine("{0}this.eDefaultDataSegment = SegmentRegisterEnum.CS;", Tabs(tabs));
					break;
				case InstructionPrefixEnum.SS:
					writer.WriteLine("{0}this.eDefaultDataSegment = SegmentRegisterEnum.SS;", Tabs(tabs));
					break;
				case InstructionPrefixEnum.DS:
					writer.WriteLine("{0}this.eDefaultDataSegment = SegmentRegisterEnum.DS;", Tabs(tabs));
					break;
				case InstructionPrefixEnum.FS:
					writer.WriteLine("{0}this.eDefaultDataSegment = SegmentRegisterEnum.FS;", Tabs(tabs));
					break;
				case InstructionPrefixEnum.GS:
					writer.WriteLine("{0}this.eDefaultDataSegment = SegmentRegisterEnum.GS;", Tabs(tabs));
					break;
				default:
					break;
			}

			writer.WriteLine("{0}bPrefix = true;", Tabs(tabs));
		}
		else
		{
			writer.WriteLine("{0}// {1} ({2})", Tabs(tabs), instruction.Instruction, index);
			for (int i = 0; i < instruction.OpCodes.Count; i++)
			{
				OpCodeDefinition op = instruction.OpCodes[i];

				if (op.Mask != 0 || op.Parameters.Count != 1 ||
					(op.Parameters[0].Type != OpCodeParameterTypeEnum.AccumulatorWithImmediateValue &&
					op.Parameters[0].Type != OpCodeParameterTypeEnum.ImmediateValueWithAccumulator &&
					op.Parameters[0].Type != OpCodeParameterTypeEnum.ImmediateValue &&
					op.Parameters[0].Type != OpCodeParameterTypeEnum.ImmediateMemoryAddressWithAccumulator &&
					op.Parameters[0].Type != OpCodeParameterTypeEnum.RelativeValue &&
					op.Parameters[0].Type != OpCodeParameterTypeEnum.ImmediateSegmentOffset &&
					op.Parameters[0].Type != OpCodeParameterTypeEnum.RegisterCL &&
					op.Parameters[0].Type != OpCodeParameterTypeEnum.RegisterAWithDX &&
					op.Parameters[0].Type != OpCodeParameterTypeEnum.RegisterDXWithA &&
					op.Parameters[0].Type != OpCodeParameterTypeEnum.ImmediateValue1 &&
					op.Parameters[0].Type != OpCodeParameterTypeEnum.ImmediateValue3))
				{
					if (i > level)
					{
						writer.WriteLine();
						writer.WriteLine("{0}// OpCode byte: {1}", Tabs(tabs), i);
						writer.WriteLine("{0}this.iByte{1} = stream.ReadByte();", Tabs(tabs), i);
						writer.WriteLine("{0}if (this.iByte{1} < 0)", Tabs(tabs), i);
						writer.WriteLine("{0}{{", Tabs(tabs));
						writer.WriteLine("{0}bInvalid = true;", Tabs(tabs + 1));
						writer.WriteLine("{0}return;", Tabs(tabs + 1));
						writer.WriteLine("{0}}}", Tabs(tabs));
						writer.WriteLine("{0}this.aBytes.Add((byte)this.iByte{1});", Tabs(tabs), i);
						writer.WriteLine();
					}
				}
				if (i > 0 && i > level && op.Mask != 0)
				{
					writer.WriteLine("{0}if ((this.iByte{1} & 0x{2:x2}) != 0x{3:x2})", Tabs(tabs), i, op.Mask, op.OpCode);
					writer.WriteLine("{0}{{", Tabs(tabs));
					writer.WriteLine("{0}bInvalid = true;", Tabs(tabs + 1));
					writer.WriteLine("{0}}}", Tabs(tabs));
				}

				for (int k = 0; k < op.Parameters.Count; k++)
				{
					EncodeParameter(writer, op.Parameters[k], tabs, i);
				}
			}
			if (instruction.ClearedFlags != FlagsEnum.Undefined)
				writer.WriteLine("{0}this.eClearedFlags = {1};", Tabs(tabs), OpCodeInstructionDefinition.FlagsToString(instruction.ClearedFlags));
			if (instruction.SetFlags != FlagsEnum.Undefined)
				writer.WriteLine("{0}this.eSetFlags = {1};", Tabs(tabs), OpCodeInstructionDefinition.FlagsToString(instruction.SetFlags));
			if (instruction.ModifiedFlags != FlagsEnum.Undefined)
				writer.WriteLine("{0}this.eModifiedFlags = {1};", Tabs(tabs), OpCodeInstructionDefinition.FlagsToString(instruction.ModifiedFlags));
			if (instruction.UndefinedFlags != FlagsEnum.Undefined)
				writer.WriteLine("{0}this.eUndefinedFlags = {1};", Tabs(tabs), OpCodeInstructionDefinition.FlagsToString(instruction.UndefinedFlags));

			writer.WriteLine("{0}if (!bInvalid)", Tabs(tabs), instruction.Instruction);
			writer.WriteLine("{0}{{", Tabs(tabs));
			writer.WriteLine("{0}this.eCPU = CPUEnum.{1};", Tabs(tabs + 1), instruction.CPU.ToString());
			writer.WriteLine("{0}this.eInstruction = InstructionEnum.{1};", Tabs(tabs + 1), instruction.Instruction);
			writer.WriteLine("{0}this.sDescription = \"{1}\";", Tabs(tabs + 1), instruction.Description);
			writer.WriteLine("{0}}}", Tabs(tabs));
		}
	}

	private static void EncodeParameter(StreamWriter writer, OpCodeParameter param, int tabs, int level)
	{
		switch (param.Type)
		{
			case OpCodeParameterTypeEnum.SignExtend:
				writer.WriteLine("{0}bSignExtendImmediate = (this.iByte{1} & 0x{2:x2}) != 0;", Tabs(tabs), level, param.Mask);
				break;
			case OpCodeParameterTypeEnum.OperandSize:
				writer.WriteLine("{0}eOperandSize = ((this.iByte{1} & 0x{2:x2}) != 0) ? eOperandSize : InstructionSizeEnum.Byte;",
					Tabs(tabs), level, param.Mask);
				break;
			case OpCodeParameterTypeEnum.ReverseDirection:
				writer.WriteLine("{0}bReverseDirection = (this.iByte{1} & 0x{2:x2}) == 0;", Tabs(tabs), level, param.Mask);
				break;
			case OpCodeParameterTypeEnum.FPUDestination:
				writer.WriteLine("{0}this.bFPUDestination0 = (this.iByte{1} & 0x{2:x2}) == 0;", Tabs(tabs), level, param.Mask);
				break;
			case OpCodeParameterTypeEnum.FPUStackAddress:
				writer.WriteLine("{0}this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.FPUStackAddress, (uint)((this.iByte{1} & 0x{2:x2}) >> {3})));",
					Tabs(tabs), level, param.Mask, param.BitPosition);
				break;
			case OpCodeParameterTypeEnum.AccumulatorWithRegister:
				writer.WriteLine("{0}this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, 0));", Tabs(tabs));
				writer.WriteLine("{0}this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte{1} & 0x{2:x2}) >> {3})));",
					Tabs(tabs), level, param.Mask, param.BitPosition);
				break;
			case OpCodeParameterTypeEnum.Register:
				writer.WriteLine("{0}this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, (uint)((this.iByte{1} & 0x{2:x2}) >> {3})));",
					Tabs(tabs), level, param.Mask, param.BitPosition);
				break;
			case OpCodeParameterTypeEnum.RegisterCL:
				writer.WriteLine("{0}this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, InstructionSizeEnum.Byte, 1));",
					Tabs(tabs));
				break;
			case OpCodeParameterTypeEnum.RegisterAWithDX:
				writer.WriteLine("{0}this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, 0));", Tabs(tabs));
				writer.WriteLine("{0}this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, InstructionSizeEnum.Word, 2));", Tabs(tabs));
				break;
			case OpCodeParameterTypeEnum.RegisterDXWithA:
				writer.WriteLine("{0}this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, InstructionSizeEnum.Word, 2));", Tabs(tabs));
				writer.WriteLine("{0}this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, 0));", Tabs(tabs));
				break;
			case OpCodeParameterTypeEnum.SegmentRegister:
			case OpCodeParameterTypeEnum.SegmentRegisterNoCS:
			case OpCodeParameterTypeEnum.SegmentRegisterFSGS:
				writer.WriteLine("{0}this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.SegmentRegister, eOperandSize, (uint)((this.iByte{1} & 0x{2:x2}) >> {3})));",
					Tabs(tabs), level, param.Mask, param.BitPosition);
				break;
			case OpCodeParameterTypeEnum.MemoryAddressing:
				writer.WriteLine("{0}this.aParameters.Add(MemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte{1} & 0x{2:x2}));",
					Tabs(tabs), level, param.Mask);
				break;
			case OpCodeParameterTypeEnum.RegisterOrMemoryAddressing:
				writer.WriteLine("{0}this.aParameters.Add(RegisterOrMemoryAddressing(stream, this.eDefaultDataSegment, eOperandSize, eAddressSize, this.iByte{1} & 0x{2:x2}));",
					Tabs(tabs), level, param.Mask);
				break;
			case OpCodeParameterTypeEnum.Condition:
				writer.WriteLine("{0}this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Condition, (uint)((this.iByte{1} & 0x{2:x2}) >> {3})));",
					Tabs(tabs), level, param.Mask, param.BitPosition);
				break;
			case OpCodeParameterTypeEnum.AccumulatorWithImmediateValue:
				writer.WriteLine("{0}this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, 0));", Tabs(tabs));
				writer.WriteLine("{0}this.aParameters.Add(ReadImmediate(stream, {1}, eOperandSize, bSignExtendImmediate));", Tabs(tabs), param.ByteSize);
				break;
			case OpCodeParameterTypeEnum.ImmediateValueWithAccumulator:
				writer.WriteLine("{0}this.aParameters.Add(ReadImmediate(stream, {1}, eOperandSize, bSignExtendImmediate));", Tabs(tabs), param.ByteSize);
				writer.WriteLine("{0}this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, 0));", Tabs(tabs));
				break;
			case OpCodeParameterTypeEnum.ImmediateValue:
				writer.WriteLine("{0}this.aParameters.Add(ReadImmediate(stream, {1}, eOperandSize, bSignExtendImmediate));", Tabs(tabs), param.ByteSize);
				break;
			case OpCodeParameterTypeEnum.ImmediateValue1:
				writer.WriteLine("{0}this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Immediate, InstructionSizeEnum.Byte, 1));", Tabs(tabs));
				break;
			case OpCodeParameterTypeEnum.ImmediateValue3:
				writer.WriteLine("{0}this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Immediate, InstructionSizeEnum.Byte, 3));", Tabs(tabs));
				break;
			case OpCodeParameterTypeEnum.ImmediateMemoryAddressWithAccumulator:
				writer.WriteLine("{0}this.aParameters.Add(MemoryImmediate(stream, this.eDefaultDataSegment, eAddressSize));", Tabs(tabs));
				writer.WriteLine("{0}this.aParameters.Add(new InstructionParameter(InstructionParameterTypeEnum.Register, eOperandSize, 0));", Tabs(tabs));
				break;
			case OpCodeParameterTypeEnum.RelativeValue:
				writer.WriteLine("{0}this.aParameters.Add(ReadRelative(stream, {1}, eOperandSize));", Tabs(tabs), param.ByteSize);
				break;
			case OpCodeParameterTypeEnum.ImmediateSegmentOffset:
				writer.WriteLine("{0}this.aParameters.Add(ReadSegmentOffset(stream, eOperandSize));", Tabs(tabs));
				break;
		}
	}

	private static string Tabs(int level)
	{
		return new string('\t', level);
	}

	private static void MatchLibrary(Library library, List<Segment> segments, List<ModuleMatch> matches)
	{
		for (int i = 0; i < library.Modules.Count; i++)
		{
			MatchModule(library.Modules[i], segments, matches);
		}
	}

	private static void MatchModule(OBJModule module, List<Segment> segments, List<ModuleMatch> matches)
	{
		// iterate through data records that contain code
		for (int i = 0; i < module.DataRecords.Count; i++)
		{
			DataRecord moduleData = module.DataRecords[i];

			// skip module data that has less than two bytes of code
			int iLength = moduleData.Data.Length;
			for (int j = 0; j < moduleData.Fixups.Count; j++)
			{
				iLength -= moduleData.Fixups[j].Length;
			}
			if (iLength < 4)
				continue;

			if (moduleData.Segment.ClassName.Equals("CODE", StringComparison.CurrentCultureIgnoreCase))
			{
				for (int j = 0; j < segments.Count; j++)
				{
					Segment segment = segments[j];

					// skip segments that contain data or are too short
					if (!segment.CompareFlag(SegmentFlagsEnum.DataSegment) && segment.Data.Length >= moduleData.Data.Length)
					{
						int iPos = 0;
						int iPos1 = 0;
						int iRelIndex = 0;
						int iFixIndex = 0;

						for (; iPos < segment.Data.Length; iPos++)
						{
							// we have a match
							// also, continue searching for additional instances of the same module
							if (iPos1 >= moduleData.Data.Length && iFixIndex >= moduleData.Fixups.Count)
							{
								int iTemp = moduleData.Data.Length;
								matches.Add(new ModuleMatch(module, (uint)(iPos - iTemp), iTemp));
								//Console.WriteLine("Matched library module {0} in segment {1} [0x{2:x} - 0x{3:x}]", module.Name, j, iPos - iTemp, iPos - 1);
								iPos--;
								iPos1 = 0;
								iFixIndex = 0;

								// no data left to compare
								if (moduleData.Data.Length > segment.Data.Length - iPos)
									break;

								continue;
							}

							// skip floating point relocations
							while (iRelIndex < segment.Relocations.Count &&
								segment.Relocations[iRelIndex].RelocationType == RelocationTypeEnum.FPFixup)
							{
								iRelIndex++;
							}

							// three distinct categories
							// first, there are no relocations in a segment and no fixups in code
							if (segment.Relocations.Count == 0 && moduleData.Fixups.Count == 0)
							{
								if (segment.Data[iPos] == moduleData.Data[iPos1])
								{
									iPos1++;
									continue;
								}
								iPos1 = 0;
								// no data left to compare
								if (moduleData.Data.Length > segment.Data.Length - iPos)
									break;
							}
							// second, there are no relocations in a segment and some fixups in code
							else if (segment.Relocations.Count == 0 && moduleData.Fixups.Count > 0)
							{
								if (iFixIndex < moduleData.Fixups.Count &&
									moduleData.Fixups[iFixIndex].FixupLocationType == FixupLocationTypeEnum.Base16bit)
								{
									// definitely not a match
									break;
								}
								// compensate when compiler replaces far call or jump with short one
								// call/jmp far ... with nop, push cs, call/jmp near ...
								else if (iFixIndex < moduleData.Fixups.Count &&
									(moduleData.Data[iPos1] == 0x9a || moduleData.Data[iPos1] == 0xea) &&
									moduleData.Fixups[iFixIndex].FixupLocationType == FixupLocationTypeEnum.LongPointer32bit &&
									moduleData.Fixups[iFixIndex].DataOffset == iPos1 + 1 &&
									segment.Data[iPos + 1] == 0x90 && segment.Data[iPos + 2] == 0xe)
								{
									iPos += moduleData.Fixups[iFixIndex].Length;
									iPos1 += moduleData.Fixups[iFixIndex].Length + 1;
									iFixIndex++;
									continue;
								}
								// skip intra segment offset adjustments
								else if (iFixIndex < moduleData.Fixups.Count &&
									moduleData.Fixups[iFixIndex].DataOffset == iPos1 &&
									(moduleData.Fixups[iFixIndex].FixupLocationType == FixupLocationTypeEnum.Offset16bit ||
									moduleData.Fixups[iFixIndex].FixupLocationType == FixupLocationTypeEnum.Offset16bit_1))
								{
									iPos += moduleData.Fixups[iFixIndex].Length - 1;
									iPos1 += moduleData.Fixups[iFixIndex].Length;
									iFixIndex++;
									continue;
								}
								// and finaly compare only the data in the segment with module
								else if (segment.Data[iPos] == moduleData.Data[iPos1])
								{
									iPos1++;
									continue;
								}

								iPos1 = 0;
								iFixIndex = 0;

								// no data left to compare
								if (moduleData.Data.Length > segment.Data.Length - iPos)
									break;
							}
							// third, there are relocations in segment as well as fixups in code
							else
							{
								// compensate when compiler replaces far call or jump with short one
								// call/jmp far ... with nop, push cs, call/jmp near ...
								if (iFixIndex < moduleData.Fixups.Count &&
								iRelIndex < segment.Relocations.Count &&
								segment.Relocations[iRelIndex].Offset != iPos &&
								(moduleData.Data[iPos1] == 0x9a || moduleData.Data[iPos1] == 0xea) &&
								moduleData.Fixups[iFixIndex].FixupLocationType == FixupLocationTypeEnum.LongPointer32bit &&
								moduleData.Fixups[iFixIndex].DataOffset == iPos1 + 1 &&
								segment.Data[iPos] == 0x90 && segment.Data[iPos + 1] == 0xe)
								{
									iPos += moduleData.Fixups[iFixIndex].Length;
									iPos1 += moduleData.Fixups[iFixIndex].Length + 1;
									iFixIndex++;
									continue;
								}
								// no relocations for this position and data content matches
								else if (segment.Data[iPos] == moduleData.Data[iPos1] &&
									(iRelIndex >= segment.Relocations.Count || segment.Relocations[iRelIndex].Offset != iPos) &&
									(iFixIndex >= moduleData.Fixups.Count || moduleData.Fixups[iFixIndex].DataOffset != iPos1))
								{
									iPos1++;
									continue;
								}
								// segment and segment-offset fixup
								else if (iFixIndex < moduleData.Fixups.Count &&
									iRelIndex < segment.Relocations.Count &&
									segment.Relocations[iRelIndex].Offset == iPos &&
									moduleData.Fixups[iFixIndex].DataOffset == iPos1 &&
									segment.Relocations[iRelIndex].LocationType == moduleData.Fixups[iFixIndex].ToLocationType)
								{
									iPos += moduleData.Fixups[iFixIndex].Length - 1;
									iPos1 += moduleData.Fixups[iFixIndex].Length;
									iRelIndex++;
									iFixIndex++;
									continue;
								}
								// offset fixup
								else if (iFixIndex < moduleData.Fixups.Count &&
									moduleData.Fixups[iFixIndex].DataOffset == iPos1 &&
									(moduleData.Fixups[iFixIndex].FixupLocationType == FixupLocationTypeEnum.Offset16bit ||
									moduleData.Fixups[iFixIndex].FixupLocationType == FixupLocationTypeEnum.Offset16bit_1))
								{
									iPos += moduleData.Fixups[iFixIndex].Length - 1;
									iPos1 += moduleData.Fixups[iFixIndex].Length;
									iFixIndex++;
									// fixup for faulty library fixup data
									while (iFixIndex < moduleData.Fixups.Count && moduleData.Fixups[iFixIndex].DataOffset < iPos1)
									{
										iFixIndex++;
									}
									continue;
								}

								// not a match, start again
								if (iRelIndex < segment.Relocations.Count &&
									segment.Relocations[iRelIndex].Offset == iPos)
								{
									iPos += segment.Relocations[iRelIndex].Length - 1;
									iRelIndex++;
								}
								if (iPos1 > 0)
								{
									// compensate for partial match
									iPos -= iPos1;
								}
								iPos1 = 0;
								iFixIndex = 0;

								// no data left to compare
								if (moduleData.Data.Length > segment.Data.Length - iPos)
									break;
							}
						}

						// we have a match
						if (iPos1 >= moduleData.Data.Length && iFixIndex >= moduleData.Fixups.Count)
						{
							int iTemp = moduleData.Data.Length;
							matches.Add(new ModuleMatch(module, (uint)(iPos - iTemp), iTemp));
							Console.WriteLine("Matched library module {0} in segment {1} [0x{2:x} - 0x{3:x}]", module.Name, j, iPos - iTemp, iPos - 1);
						}
					}
				}
			}
		}
	}

	private static void MatchLibraryToEXE(Library library, MZExecutable exe, List<ModuleMatch> matches)
	{
		for (int i = 0; i < library.Modules.Count; i++)
		{
			MatchModuleToEXE(library.Modules[i], exe, matches);
		}
	}

	private static void MatchModuleToEXE(OBJModule module, MZExecutable exe, List<ModuleMatch> matches)
	{
		if (module.Name.EndsWith("crt0fp.asm"))
			return;

		// iterate through data records that contain code
		for (int i = 0; i < module.DataRecords.Count; i++)
		{
			DataRecord moduleData = module.DataRecords[i];

			// skip module data that has less than two bytes of code
			int iLength = moduleData.Data.Length;
			for (int j = 0; j < moduleData.Fixups.Count; j++)
			{
				iLength -= moduleData.Fixups[j].Length;
			}
			if (iLength < 4)
				continue;

			iLength = moduleData.Data.Length;
			bool bSkip = true;
			for (int j = 0; j < iLength; j++)
			{
				if (moduleData.Data[j] != 0)
				{
					bSkip = false;
					break;
				}
			}
			if (bSkip)
				continue;

			if (moduleData.Segment.ClassName.Equals("CODE", StringComparison.CurrentCultureIgnoreCase) ||
				moduleData.Segment.ClassName.Equals("_CODE", StringComparison.CurrentCultureIgnoreCase)||
				moduleData.Segment.ClassName.Equals("TEXT", StringComparison.CurrentCultureIgnoreCase) ||
				moduleData.Segment.ClassName.Equals("_TEXT", StringComparison.CurrentCultureIgnoreCase))
			{
				// skip segments that contain data or are too short
				if (exe.Data.Length >= moduleData.Data.Length)
				{
					int iPos = 0;
					int iPos1 = 0;
					int iRelIndex = 0;
					int iFixIndex = 0;

					for (; iPos < exe.Data.Length; iPos++)
					{
						// we have a match
						// also, continue searching for additional instances of the same module
						if (iPos1 >= moduleData.Data.Length && iFixIndex >= moduleData.Fixups.Count)
						{
							int iTemp = moduleData.Data.Length;
							matches.Add(new ModuleMatch(module, (uint)(iPos - iTemp), iTemp));
							Console.WriteLine("Matched library module {0} in segment {1} [0x{2:x} - 0x{3:x}]", module.Name, 0, iPos - iTemp, iPos - 1);
							iPos--;
							iPos1 = 0;
							iFixIndex = 0;

							// no data left to compare
							if (moduleData.Data.Length > exe.Data.Length - iPos)
								break;

							continue;
						}

						// three distinct categories
						// first, there are no relocations in a segment and no fixups in code
						if (exe.Relocations.Count == 0 && moduleData.Fixups.Count == 0)
						{
							if (exe.Data[iPos] == moduleData.Data[iPos1])
							{
								iPos1++;
								continue;
							}
							iPos1 = 0;
							// no data left to compare
							if (moduleData.Data.Length > exe.Data.Length - iPos)
								break;
						}
						// second, there are no relocations in a segment and some fixups in code
						else if (exe.Relocations.Count == 0 && moduleData.Fixups.Count > 0)
						{
							if (iFixIndex < moduleData.Fixups.Count &&
								moduleData.Fixups[iFixIndex].FixupLocationType == FixupLocationTypeEnum.Base16bit)
							{
								// definitely not a match
								break;
							}
							// compensate when compiler replaces far call or jump with short one
							// call/jmp far ... with nop, push cs, call/jmp near ...
							else if (iFixIndex < moduleData.Fixups.Count &&
								(moduleData.Data[iPos1] == 0x9a || moduleData.Data[iPos1] == 0xea) &&
								moduleData.Fixups[iFixIndex].FixupLocationType == FixupLocationTypeEnum.LongPointer32bit &&
								moduleData.Fixups[iFixIndex].DataOffset == iPos1 + 1 &&
								exe.Data[iPos + 1] == 0x90 && exe.Data[iPos + 2] == 0xe)
							{
								iPos += moduleData.Fixups[iFixIndex].Length;
								iPos1 += moduleData.Fixups[iFixIndex].Length + 1;
								iFixIndex++;
								continue;
							}
							// skip long pointer references
							else if (iFixIndex < moduleData.Fixups.Count &&
								moduleData.Fixups[iFixIndex].FixupLocationType == FixupLocationTypeEnum.LongPointer32bit &&
								moduleData.Fixups[iFixIndex].DataOffset == iPos1)
							{
								iPos += moduleData.Fixups[iFixIndex].Length;
								iPos1 += moduleData.Fixups[iFixIndex].Length;
								iFixIndex++;
								continue;
							}
							// skip intra segment offset adjustments
							else if (iFixIndex < moduleData.Fixups.Count &&
								moduleData.Fixups[iFixIndex].DataOffset == iPos1 &&
								(moduleData.Fixups[iFixIndex].FixupLocationType == FixupLocationTypeEnum.Offset16bit ||
								moduleData.Fixups[iFixIndex].FixupLocationType == FixupLocationTypeEnum.Offset16bit_1))
							{
								iPos += moduleData.Fixups[iFixIndex].Length - 1;
								iPos1 += moduleData.Fixups[iFixIndex].Length;
								iFixIndex++;
								continue;
							}
							// and finaly compare only the data in the segment with module
							else if (exe.Data[iPos] == moduleData.Data[iPos1])
							{
								iPos1++;
								continue;
							}

							iPos1 = 0;
							iFixIndex = 0;

							// no data left to compare
							if (moduleData.Data.Length > exe.Data.Length - iPos)
								break;
						}
						// third, there are relocations in segment as well as fixups in code
						else
						{
							// compensate when compiler replaces far call or jump with short one
							// call/jmp far ... with nop, push cs, call/jmp near ...
							if (iFixIndex < moduleData.Fixups.Count &&
							iRelIndex < exe.Relocations.Count &&
							exe.Relocations[iRelIndex].Offset != iPos &&
							(moduleData.Data[iPos1] == 0x9a || moduleData.Data[iPos1] == 0xea) &&
							moduleData.Fixups[iFixIndex].FixupLocationType == FixupLocationTypeEnum.LongPointer32bit &&
							moduleData.Fixups[iFixIndex].DataOffset == iPos1 + 1 &&
							exe.Data[iPos] == 0x90 && exe.Data[iPos + 1] == 0xe)
							{
								iPos += moduleData.Fixups[iFixIndex].Length;
								iPos1 += moduleData.Fixups[iFixIndex].Length + 1;
								iFixIndex++;
								continue;
							}
							// skip long pointer references
							if (iFixIndex < moduleData.Fixups.Count &&
							iRelIndex < exe.Relocations.Count &&
							exe.Relocations[iRelIndex].Offset != iPos &&
							moduleData.Fixups[iFixIndex].FixupLocationType == FixupLocationTypeEnum.LongPointer32bit &&
							moduleData.Fixups[iFixIndex].DataOffset == iPos1)
							{
								iPos += moduleData.Fixups[iFixIndex].Length - 1;
								iPos1 += moduleData.Fixups[iFixIndex].Length;
								iFixIndex++;
								continue;
							}

							// no relocations for this position and data content matches
							else if (exe.Data[iPos] == moduleData.Data[iPos1] &&
								(iRelIndex >= exe.Relocations.Count || exe.Relocations[iRelIndex].Offset != iPos) &&
								(iFixIndex >= moduleData.Fixups.Count || moduleData.Fixups[iFixIndex].DataOffset != iPos1))
							{
								iPos1++;
								continue;
							}
							// segment and segment-offset fixup
							else if (iFixIndex < moduleData.Fixups.Count &&
								iRelIndex < exe.Relocations.Count &&
								exe.Relocations[iRelIndex].Offset == iPos &&
								moduleData.Fixups[iFixIndex].DataOffset == iPos1)
							{
								iPos += moduleData.Fixups[iFixIndex].Length - 1;
								iPos1 += moduleData.Fixups[iFixIndex].Length;
								iRelIndex++;
								iFixIndex++;
								continue;
							}
							// offset fixup
							else if (iFixIndex < moduleData.Fixups.Count &&
								moduleData.Fixups[iFixIndex].DataOffset == iPos1 &&
								(moduleData.Fixups[iFixIndex].FixupLocationType == FixupLocationTypeEnum.Offset16bit ||
								moduleData.Fixups[iFixIndex].FixupLocationType == FixupLocationTypeEnum.Offset16bit_1))
							{
								iPos += moduleData.Fixups[iFixIndex].Length - 1;
								iPos1 += moduleData.Fixups[iFixIndex].Length;
								iFixIndex++;
								// fixup for faulty library fixup data
								while (iFixIndex < moduleData.Fixups.Count && moduleData.Fixups[iFixIndex].DataOffset < iPos1)
								{
									iFixIndex++;
								}
								continue;
							}

							// not a match, start again
							if (iRelIndex < exe.Relocations.Count &&
								exe.Relocations[iRelIndex].Offset == iPos)
							{
								iPos += 1;
								iRelIndex++;
							}
							if (iPos1 > 0)
							{
								// compensate for partial match
								iPos -= iPos1;
							}
							iPos1 = 0;
							iFixIndex = 0;

							// no data left to compare
							if (moduleData.Data.Length > exe.Data.Length - iPos)
								break;
						}
					}

					// we have a match
					if (iPos1 >= moduleData.Data.Length && iFixIndex >= moduleData.Fixups.Count)
					{
						int iTemp = moduleData.Data.Length;
						matches.Add(new ModuleMatch(module, (uint)(iPos - iTemp), iTemp));
						Console.WriteLine("Matched library module {0} in segment {1} [0x{2:x} - 0x{3:x}]", module.Name, 0, iPos - iTemp, iPos - 1);
					}
				}
			}
		}
	}
}