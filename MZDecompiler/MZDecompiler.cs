using Disassembler.CPU;
using Disassembler.MZ;
using Disassembler.NE;
using Disassembler.OMF;
using IRB.Collections.Generic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Xml.Linq;

namespace Disassembler.Decompiler
{
	public class MZDecompiler
	{
		private MZExecutable oExecutable;
		private List<ModuleMatch> aLibraryMatches;
		private MZGlobalNamespace oGlobalNamespace;

		public MZDecompiler(MZExecutable executable, List<ModuleMatch> matches)
		{
			this.oExecutable = executable;
			this.aLibraryMatches = matches;
			this.oGlobalNamespace = new MZGlobalNamespace(this);

			MZFunction function = new MZFunction(this, CallTypeEnum.Cdecl, "DivisionByZero", new List<CParameter>(), CType.Void, 0, 0, 0, 0);
			this.oGlobalNamespace.APIFunctions.Add(function.Name, function);
		}

		public MZFunction? GetFunction(int overlay, ushort segment, ushort offset, uint segmentOffset)
		{
			uint address = MemoryRegion.ToLinearAddress(segment, offset);

			// look into function list
			for (int i = 0; i < this.oGlobalNamespace.Functions.Count; i++)
			{
				MZFunction function = this.oGlobalNamespace.Functions[i].Value;
				//Console.WriteLine("Function at {0}:0x{1:x}", function.Segment, function.Offset);
				if (function.Overlay == overlay && function.LinearAddress == address)
				{
					return function;
				}
			}

			// look into module imports and C API functions
			for (int i = 0; i < this.oGlobalNamespace.APIFunctions.Count; i++)
			{
				MZFunction function = this.oGlobalNamespace.APIFunctions[i].Value;
				if (function.Overlay == overlay && function.LinearAddress == address)
				{
					return function;
				}
			}

			address -= segmentOffset;

			// look into library matches
			if (overlay == 0)
			{
				for (int i = 0; i < this.aLibraryMatches.Count; i++)
				{
					ModuleMatch match = this.aLibraryMatches[i];

					if (address >= match.LinearAddress && address < match.LinearAddress + match.Length)
					{
						OBJModule module = match.Module;
						for (int j = 0; j < module.PublicNames.Count; j++)
						{
							PublicNameDefinition publicNameDef = module.PublicNames[j];

							for (int k = 0; k < publicNameDef.PublicNames.Count; k++)
							{
								if (match.LinearAddress + publicNameDef.PublicNames[k].Value == address)
								{
									string sName = publicNameDef.PublicNames[k].Key;
									sName = sName.StartsWith("_") ? sName.Substring(1) : sName;

									// add missing function
									Console.WriteLine("Adding undefined API function {0} at 0x{1:x8}", sName, address);
									MZFunction function = new MZFunction(this, CallTypeEnum.Cdecl, sName, new List<CParameter>(), CType.Void, 
										overlay, segment, offset, segmentOffset);
									function.Disassemble(this);
									this.oGlobalNamespace.APIFunctions.Add(sName, function);

									return function;
								}
							}
						}
					}
				}
			}

			return null;
		}

		public static int CompareFunctionByAddress(MZFunction f1, MZFunction f2)
		{
			if (f1.Overlay != f2.Overlay)
				return f1.Overlay.CompareTo(f2.Overlay);

			return f1.LinearAddress.CompareTo(f2.LinearAddress);
		}

		public void Decompile(string name, CallTypeEnum callType, List<CParameter> parameters, CType returnValue,
			int overlay, ushort segment, ushort offset, uint streamOffset)
		{
			uint address = MemoryRegion.ToLinearAddress(segment, offset);

			for (int i = 0; i < this.oGlobalNamespace.Functions.Count; i++)
			{
				MZFunction function1 = this.oGlobalNamespace.Functions[i].Value;

				if (function1.Overlay == overlay && function1.LinearAddress == address)
				{
					return;
				}
			}

			MZFunction function = new MZFunction(this, callType, name, parameters, returnValue, overlay, segment, offset, streamOffset);
			this.oGlobalNamespace.Functions.Add(function.Name, function);
			function.Disassemble(this);
			//function.Decompile(this);
		}

		public void DecompileOverlay()
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
				this.Decompile($"F0_0000_{aOffsets[i]:x4}", CallTypeEnum.Undefined, new List<CParameter>(), CType.Void, 0, 0, aOffsets[i], 0);
			}
			Console.WriteLine("--- Overlay offsets");
			for (int i = 0; i < usCount; i++)
			{
				Console.WriteLine($"0x{aOffsets[i]:x4}");
			}
			Console.WriteLine("--- End overlay offsets");
		}

		public void WriteCode(string path, List<MZFunction> functions)
		{
			StreamWriter writer = new StreamWriter(path);

			if (functions.Count > 0)
			{
				string sClassName = Path.GetFileNameWithoutExtension(path);

				// sort functions by address
				functions.Sort(MZDecompiler.CompareFunctionByAddress);

				writer.WriteLine("using Disassembler;");
				writer.WriteLine();
				writer.WriteLine("namespace Civilization1");
				writer.WriteLine("{");
				writer.WriteLine("\tpublic class {0}", sClassName);
				writer.WriteLine("\t{");
				writer.WriteLine("\t\tprivate Civilization oParent;");
				writer.WriteLine("\t\tprivate CPU oCPU;");
				writer.WriteLine();
				writer.WriteLine("\t\tpublic {0}(Civilization parent)", sClassName);
				writer.WriteLine("\t\t{");
				writer.WriteLine("\t\t\tthis.oParent = parent;");
				writer.WriteLine("\t\t\tthis.oCPU = parent.CPU;");
				writer.WriteLine("\t\t}");

				for (int j = 0; j < functions.Count; j++)
				{
					MZFunction function = functions[j];

					writer.WriteLine();
					writer.WriteLine("\t\tpublic void {0}()", function.Name);
					writer.WriteLine("\t\t{");
					writer.WriteLine("\t\t\tthis.oParent.LogEnterBlock(\"'{0}'({1}) at 0x{2:x4}:0x{3:x4}\");",
						function.Name, function.CallType.ToString(), function.Segment, function.Offset);
					writer.WriteLine("\t\t\tthis.oCPU.CS.Word = 0x{0:x4}; // set this function segment", function.Segment);
					writer.WriteLine();
					writer.WriteLine("\t\t\t// function body");
					for (int k = 0; k < function.Instructions.Count; k++)
					{
						// writer.WriteLine("\t\t{0}\t{1}", function.Instructions[j].Location.ToString(), function.Instructions[j]);
						Instruction instruction = function.Instructions[k];

						if (instruction.Label)
						{
							writer.WriteLine();
							writer.WriteLine("\t\tL{0:x4}:", instruction.Offset);
						}

						uint uiOffset = 0;
						InstructionParameter parameter;
						MZFunction? function1;

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
								writer.WriteLine(parameter.ToDestinationCSTextMZ(instruction.OperandSize, string.Format("this.oCPU.{0}{1}({2}, {3})",
									instruction.InstructionType.ToString(), instruction.OperandSize.ToString(),
									parameter.ToSourceCSTextMZ(instruction.OperandSize), instruction.Parameters[1].ToSourceCSTextMZ(instruction.OperandSize))));
								break;

							case InstructionEnum.DAS:
								writer.WriteLine("\t\t\tthis.oCPU.DAS();");
								break;

							case InstructionEnum.AAA:
								writer.WriteLine("\t\t\tthis.oCPU.AAA();");
								break;

							case InstructionEnum.SAR:
							case InstructionEnum.SHL:
							case InstructionEnum.SHR:
							case InstructionEnum.RCR:
							case InstructionEnum.RCL:
							case InstructionEnum.ROL:
							case InstructionEnum.ROR:
								parameter = instruction.Parameters[0];
								writer.Write("\t\t\t");
								writer.WriteLine(parameter.ToDestinationCSTextMZ(instruction.OperandSize, string.Format("this.oCPU.{0}{1}({2}, {3})",
									instruction.InstructionType.ToString(), instruction.OperandSize.ToString(),
									parameter.ToSourceCSTextMZ(instruction.OperandSize), instruction.Parameters[1].ToSourceCSTextMZ(instruction.Parameters[1].Size))));
								break;
							case InstructionEnum.SHLD:
								parameter = instruction.Parameters[0];
								writer.Write("\t\t\t");
								writer.WriteLine(parameter.ToDestinationCSTextMZ(instruction.OperandSize, string.Format("this.oCPU.{0}1{1}({2}, {3}, {4})",
									instruction.InstructionType.ToString(), instruction.OperandSize.ToString(),
									parameter.ToSourceCSTextMZ(instruction.OperandSize), instruction.Parameters[1].ToSourceCSTextMZ(instruction.OperandSize),
									instruction.Parameters[2].ToSourceCSTextMZ(instruction.Parameters[2].Size))));
								break;

							case InstructionEnum.CBW:
								if (instruction.OperandSize == InstructionSizeEnum.Word)
								{
									writer.WriteLine("\t\t\tthis.oCPU.CBW(this.oCPU.AX);");
								}
								else
								{
									writer.WriteLine("\t\t\tthis.oCPU.CWDE(this.oCPU.AX);");
								}
								break;

							case InstructionEnum.CWD:
								if (instruction.OperandSize == InstructionSizeEnum.Word)
								{
									writer.WriteLine("\t\t\tthis.oCPU.CWD(this.oCPU.AX, this.oCPU.DX);");
								}
								else
								{
									writer.WriteLine("\t\t\tthis.oCPU.CDQ(this.oCPU.AX, this.oCPU.DX);");
								}
								break;

							case InstructionEnum.CMPS:
								writer.Write("\t\t\tthis.oCPU.");
								if (instruction.RepPrefix == InstructionPrefixEnum.REPE || instruction.RepPrefix == InstructionPrefixEnum.REPNE)
								{
									writer.WriteLine("{0}CMPS{1}(this.oCPU.ES, this.oCPU.DI, {2}, this.oCPU.SI);",
										instruction.RepPrefix.ToString(), instruction.OperandSize.ToString(), instruction.GetDefaultDataSegmentTextMZ());
								}
								else
								{
									writer.WriteLine("CMPS{0}(this.oCPU.ES, this.oCPU.DI, {1}, this.oCPU.SI);",
										instruction.OperandSize.ToString(), instruction.GetDefaultDataSegmentTextMZ());
								}
								break;

							case InstructionEnum.CMP:
							case InstructionEnum.TEST:
								parameter = instruction.Parameters[0];
								writer.WriteLine("\t\t\tthis.oCPU.{0}{1}({2}, {3});", instruction.InstructionType.ToString(), instruction.OperandSize.ToString(),
									parameter.ToSourceCSTextMZ(instruction.OperandSize), instruction.Parameters[1].ToSourceCSTextMZ(instruction.OperandSize));
								break;

							case InstructionEnum.DIV:
							case InstructionEnum.IDIV:
								if (instruction.OperandSize == InstructionSizeEnum.Byte)
								{
									writer.WriteLine("\t\t\tthis.oCPU.{0}{1}(this.oCPU.AX, {2});",
										instruction.InstructionType.ToString(), instruction.OperandSize.ToString(),
										instruction.Parameters[0].ToSourceCSTextMZ(instruction.OperandSize));
								}
								else
								{
									writer.WriteLine("\t\t\tthis.oCPU.{0}{1}(this.oCPU.AX, this.oCPU.DX, {2});",
										instruction.InstructionType.ToString(), instruction.OperandSize.ToString(),
										instruction.Parameters[0].ToSourceCSTextMZ(instruction.OperandSize));
								}
								break;

							// FPU instructions
							case InstructionEnum.FADDP:
								parameter = instruction.Parameters[0];
								writer.WriteLine("\t\t\tthis.oCPU.FADDP({0});", parameter.Value);
								break;
							case InstructionEnum.FILD:
								parameter = instruction.Parameters[0];
								switch (instruction.Bytes[0])
								{
									case 0xdf:
										if ((instruction.Bytes[1] & 0x38) == 0)
										{
											writer.WriteLine("\t\t\tthis.oCPU.FILD16({0}, {1});",
												(parameter.DataSegment == SegmentRegisterEnum.Immediate) ? string.Format("0x{0:x}", parameter.Segment) : string.Format("this.oCPU.{0}.Word", parameter.DataSegment.ToString()),
												parameter.ToCSTextMZ(instruction.OperandSize));
										}
										else
										{
											writer.WriteLine("\t\t\tthis.oCPU.FILD64({0}, {1});",
												(parameter.DataSegment == SegmentRegisterEnum.Immediate) ? string.Format("0x{0:x}", parameter.Segment) : string.Format("this.oCPU.{0}.Word", parameter.DataSegment.ToString()),
												parameter.ToCSTextMZ(instruction.OperandSize));
										}
										break;
									case 0xdb:
										writer.WriteLine("\t\t\tthis.oCPU.FILD32({0}, {1});",
											(parameter.DataSegment == SegmentRegisterEnum.Immediate) ? string.Format("0x{0:x}", parameter.Segment) : string.Format("this.oCPU.{0}.Word", parameter.DataSegment.ToString()),
											parameter.ToCSTextMZ(instruction.OperandSize));
										break;
								}
								break;
							case InstructionEnum.FMUL:
								parameter = instruction.Parameters[0];
								if (parameter.Type == InstructionParameterTypeEnum.FPUStackAddress)
								{
									writer.WriteLine("\t\t\tthis.oCPU.FMULST({0}, {1});", instruction.FPUDestination0, parameter.Value);
								}
								else
								{
									switch (instruction.Bytes[0])
									{
										case 0xd8:
											writer.WriteLine("\t\t\tthis.oCPU.FMUL32({0}, {1});",
												parameter.GetSegmentTextMZ(), parameter.ToCSTextMZ(instruction.OperandSize));
											break;
										case 0xdc:
											writer.WriteLine("\t\t\tthis.oCPU.FMUL64({0}, {1});",
												parameter.GetSegmentTextMZ(), parameter.ToCSTextMZ(instruction.OperandSize));
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
										if (instruction.OperandSize == InstructionSizeEnum.Byte)
										{
											writer.WriteLine("\t\t\tthis.oCPU.{0}{1}(this.oCPU.AX, {2});",
												instruction.InstructionType.ToString(), instruction.OperandSize.ToString(),
												parameter.ToSourceCSTextMZ(instruction.OperandSize));
										}
										else
										{
											writer.WriteLine("\t\t\tthis.oCPU.{0}{1}(this.oCPU.AX, this.oCPU.DX, {2});",
												instruction.InstructionType.ToString(), instruction.OperandSize.ToString(),
												parameter.ToSourceCSTextMZ(instruction.OperandSize));
										}
										break;
									case 2:
										parameter = instruction.Parameters[0];
										writer.Write("\t\t\t");
										writer.WriteLine(parameter.ToDestinationCSTextMZ(instruction.OperandSize, string.Format("this.oCPU.{0}1{1}({2}, {3})",
											instruction.InstructionType.ToString(), instruction.OperandSize.ToString(),
											parameter.ToSourceCSTextMZ(instruction.OperandSize), instruction.Parameters[1].ToSourceCSTextMZ(instruction.OperandSize))));
										break;
									case 3:
										parameter = instruction.Parameters[0];
										writer.Write("\t\t\t");
										writer.WriteLine(parameter.ToDestinationCSTextMZ(instruction.OperandSize, string.Format("this.oCPU.{0}1{1}({2}, {3})",
											instruction.InstructionType.ToString(), instruction.OperandSize.ToString(),
											instruction.Parameters[1].ToSourceCSTextMZ(instruction.OperandSize), instruction.Parameters[2].ToSourceCSTextMZ(instruction.OperandSize))));
										break;
									default:
										throw new Exception("Unknown IMUL instruction");
								}
								break;

							case InstructionEnum.MUL:
								if (instruction.OperandSize == InstructionSizeEnum.Byte)
								{
									writer.WriteLine("\t\t\tthis.oCPU.{0}{1}(this.oCPU.AX, {2});",
										instruction.InstructionType.ToString(), instruction.OperandSize.ToString(),
										instruction.Parameters[0].ToSourceCSTextMZ(instruction.OperandSize));
								}
								else
								{
									writer.WriteLine("\t\t\tthis.oCPU.{0}{1}(this.oCPU.DX, this.oCPU.AX, {2});",
										instruction.InstructionType.ToString(), instruction.OperandSize.ToString(),
										instruction.Parameters[0].ToSourceCSTextMZ(instruction.OperandSize));
								}
								break;

							case InstructionEnum.LDS:
								parameter = instruction.Parameters[1];
								writer.WriteLine("\t\t\t// LDS");
								writer.WriteLine("\t\t\t{0}", instruction.Parameters[0].ToDestinationCSTextMZ(instruction.OperandSize, parameter.ToSourceCSTextMZ(instruction.OperandSize)));
								writer.WriteLine("\t\t\tthis.oCPU.DS.{0} = this.oCPU.Read{0}({1}, (ushort)({2} + 2));",
									instruction.OperandSize.ToString(),
									parameter.GetSegmentTextMZ(),
									parameter.ToCSTextMZ(instruction.OperandSize));
								break;

							case InstructionEnum.LES:
								parameter = instruction.Parameters[1];
								writer.WriteLine("\t\t\t// LES");
								writer.WriteLine("\t\t\t{0}", instruction.Parameters[0].ToDestinationCSTextMZ(instruction.OperandSize, parameter.ToSourceCSTextMZ(instruction.OperandSize)));
								writer.WriteLine("\t\t\tthis.oCPU.ES.{0} = this.oCPU.Read{0}({1}, (ushort)({2} + 2));",
									instruction.OperandSize.ToString(),
									parameter.GetSegmentTextMZ(),
									parameter.ToCSTextMZ(instruction.OperandSize));
								break;

							case InstructionEnum.LEA:
								parameter = instruction.Parameters[1];
								writer.WriteLine("\t\t\t// LEA");
								writer.Write("\t\t\t");
								writer.Write(instruction.Parameters[0].ToDestinationCSTextMZ(instruction.OperandSize, parameter.ToCSTextMZ(instruction.OperandSize)));
								if (parameter.ReferenceType != InstructionParameterReferenceEnum.None)
								{
									writer.WriteLine(" // {0}", parameter.ReferenceType.ToString());
								}
								else
								{
									writer.WriteLine();
								}
								break;

							case InstructionEnum.LODS:
								writer.Write("\t\t\tthis.oCPU.");
								if (instruction.RepPrefix == InstructionPrefixEnum.REPE || instruction.RepPrefix == InstructionPrefixEnum.REPNE)
								{
									writer.WriteLine("{0}LODS{1}();", instruction.RepPrefix.ToString(), instruction.OperandSize.ToString());
								}
								else
								{
									writer.WriteLine("LODS{0}();", instruction.OperandSize.ToString());
								}
								break;

							case InstructionEnum.MOV:
								parameter = instruction.Parameters[1];
								writer.Write("\t\t\t");
								writer.Write(instruction.Parameters[0].ToDestinationCSTextMZ(instruction.OperandSize, parameter.ToSourceCSTextMZ(instruction.OperandSize)));
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
								writer.Write("\t\t\tthis.oCPU.");
								if (instruction.RepPrefix == InstructionPrefixEnum.REPE || instruction.RepPrefix == InstructionPrefixEnum.REPNE)
								{
									writer.WriteLine("{0}MOVS{1}({2}, this.oCPU.SI, this.oCPU.ES, this.oCPU.DI, this.oCPU.CX);",
										instruction.RepPrefix.ToString(), instruction.OperandSize.ToString(), instruction.GetDefaultDataSegmentTextMZ());
								}
								else
								{
									writer.WriteLine("MOVS{0}({1}, this.oCPU.SI, this.oCPU.ES, this.oCPU.DI);",
										instruction.OperandSize.ToString(), instruction.GetDefaultDataSegmentTextMZ());
								}
								break;

							case InstructionEnum.MOVSX:
							case InstructionEnum.MOVZX:
								parameter = instruction.Parameters[0];
								writer.Write("\t\t\t");
								writer.WriteLine(parameter.ToDestinationCSTextMZ(instruction.OperandSize, string.Format("this.oCPU.{0}{1}({2})",
									instruction.InstructionType.ToString(), instruction.OperandSize.ToString(),
									instruction.Parameters[1].ToSourceCSTextMZ(instruction.Parameters[1].Size))));
								break;

							case InstructionEnum.DEC:
							case InstructionEnum.INC:
							case InstructionEnum.NEG:
							case InstructionEnum.NOT:
								parameter = instruction.Parameters[0];
								writer.Write("\t\t\t");
								writer.WriteLine(parameter.ToDestinationCSTextMZ(instruction.OperandSize, string.Format("this.oCPU.{0}{1}({2})",
									instruction.InstructionType.ToString(), instruction.OperandSize.ToString(), parameter.ToSourceCSTextMZ(instruction.OperandSize))));
								break;

							case InstructionEnum.NOP:
								// ignore this instruction
								break;

							case InstructionEnum.CLI:
								writer.WriteLine("\t\t\tthis.oCPU.CLI();");
								break;

							case InstructionEnum.STI:
								writer.WriteLine("\t\t\tthis.oCPU.STI();");
								break;

							// stack instructions
							case InstructionEnum.POP:
								parameter = instruction.Parameters[0];
								/*Console.WriteLine("POP {0} in {1}.{2} ", instruction.Parameters[0].ToString(),
									segment.Namespace, function.Name);*/
								writer.Write("\t\t\t");
								writer.WriteLine(parameter.ToDestinationCSTextMZ(instruction.OperandSize, string.Format("this.oCPU.Pop{0}()",
									instruction.OperandSize.ToString())));
								break;

							case InstructionEnum.POPA:
								writer.WriteLine("\t\t\tthis.oCPU.PopA{0}(this.oCPU.AX, this.oCPU.CX, this.oCPU.DX, this.oCPU.BX, this.oCPU.SI, this.oCPU.DI);",
									instruction.OperandSize.ToString());
								break;

							case InstructionEnum.POPF:
								writer.WriteLine("\t\t\tthis.oCPU.PopF();");
								break;

							case InstructionEnum.PUSH:
								parameter = instruction.Parameters[0];
								writer.Write("\t\t\tthis.oCPU.Push{0}({1});", instruction.OperandSize.ToString(), parameter.ToSourceCSTextMZ(instruction.OperandSize));
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
								writer.WriteLine("\t\t\tthis.oCPU.PushA{0}(this.oCPU.AX, this.oCPU.CX, this.oCPU.DX, this.oCPU.BX, this.oCPU.SI, this.oCPU.DI);", instruction.OperandSize.ToString());
								break;

							case InstructionEnum.PUSHF:
								writer.WriteLine("\t\t\tthis.oCPU.PushF();");
								break;

							case InstructionEnum.CLD:
								writer.WriteLine("\t\t\tthis.oCPU.Flags.D = false;");
								break;

							case InstructionEnum.STD:
								writer.WriteLine("\t\t\tthis.oCPU.Flags.D = true;");
								break;

							case InstructionEnum.CLC:
								writer.WriteLine("\t\t\tthis.oCPU.Flags.C = false;");
								break;

							case InstructionEnum.STC:
								writer.WriteLine("\t\t\tthis.oCPU.Flags.C = true;");
								break;

							case InstructionEnum.CMC:
								writer.WriteLine("\t\t\tthis.oCPU.Flags.C = !this.oCPU.Flags.C;");
								break;

							case InstructionEnum.SCAS:
								writer.Write("\t\t\tthis.oCPU.");
								if (instruction.RepPrefix == InstructionPrefixEnum.REPE || instruction.RepPrefix == InstructionPrefixEnum.REPNE)
								{
									writer.WriteLine("{0}SCAS{1}();", 
										instruction.RepPrefix.ToString(), instruction.OperandSize.ToString());
								}
								else
								{
									writer.WriteLine("SCAS{0}();", instruction.OperandSize.ToString());
								}
								break;

							case InstructionEnum.STOS:
								writer.Write("\t\t\tthis.oCPU.");
								if (instruction.RepPrefix == InstructionPrefixEnum.REPE || instruction.RepPrefix == InstructionPrefixEnum.REPNE)
								{
									writer.WriteLine("{0}STOS{1}();", 
										instruction.RepPrefix.ToString(), instruction.OperandSize.ToString());
								}
								else
								{
									writer.WriteLine("STOS{0}();", instruction.OperandSize.ToString());
								}
								break;

							case InstructionEnum.XCHG:
								parameter = instruction.Parameters[0];
								//writer.WriteLine("\t\t// XCHG");
								if (instruction.OperandSize == InstructionSizeEnum.Byte)
								{
									writer.WriteLine("\t\t\tthis.oCPU.Temp.Low = {0};", parameter.ToSourceCSTextMZ(instruction.OperandSize));
									writer.Write("\t\t\t");
									writer.WriteLine(parameter.ToDestinationCSTextMZ(instruction.OperandSize, instruction.Parameters[1].ToSourceCSTextMZ(instruction.OperandSize)));
									parameter = instruction.Parameters[1];
									writer.Write("\t\t\t");
									writer.WriteLine(parameter.ToDestinationCSTextMZ(instruction.OperandSize, "this.oCPU.Temp.Low"));

								}
								else
								{
									writer.WriteLine("\t\t\tthis.oCPU.Temp.{0} = {1};", instruction.OperandSize.ToString(), parameter.ToSourceCSTextMZ(instruction.OperandSize));
									writer.Write("\t\t\t");
									writer.WriteLine(parameter.ToDestinationCSTextMZ(instruction.OperandSize, instruction.Parameters[1].ToSourceCSTextMZ(instruction.OperandSize)));
									parameter = instruction.Parameters[1];
									writer.Write("\t\t\t");
									writer.WriteLine(parameter.ToDestinationCSTextMZ(instruction.OperandSize, string.Format("this.oCPU.Temp.{0}", instruction.OperandSize.ToString())));
								}
								break;

							case InstructionEnum.XLAT:
								writer.WriteLine("\t\t\tthis.oCPU.XLAT(this.oCPU.AX, {0}, this.oCPU.BX);",
									instruction.GetDefaultDataSegmentTextMZ());
								break;

							// input and output port instructions
							case InstructionEnum.IN:
								writer.WriteLine("\t\t\t{0}",
									instruction.Parameters[0].ToDestinationCSTextMZ(instruction.Parameters[0].Size,
									string.Format("this.oCPU.IN{0}({1})", 
									instruction.OperandSize.ToString(),
									instruction.Parameters[1].ToSourceCSTextMZ(instruction.Parameters[1].Size))));
								break;
							case InstructionEnum.OUT:
								writer.WriteLine("\t\t\tthis.oCPU.OUT{0}({1}, {2});",
									instruction.OperandSize.ToString(),
									instruction.Parameters[0].ToSourceCSTextMZ(instruction.Parameters[0].Size), 
									instruction.Parameters[1].ToSourceCSTextMZ(instruction.Parameters[1].Size));
								break;

							case InstructionEnum.OUTS:
								writer.Write("\t\t\tthis.oCPU.");
								if (instruction.RepPrefix == InstructionPrefixEnum.REPE || instruction.RepPrefix == InstructionPrefixEnum.REPNE)
								{
									writer.WriteLine("{0}{1}{2}({3}, this.oCPU.SI, this.oCPU.CX);",
										instruction.RepPrefix.ToString(), 
										instruction.InstructionType.ToString(), instruction.OperandSize.ToString(),
										instruction.GetDefaultDataSegmentTextMZ());
								}
								else
								{
									writer.WriteLine("{0}{1}({2}, this.oCPU.SI);",
										instruction.InstructionType.ToString(), instruction.OperandSize.ToString(), 
										instruction.GetDefaultDataSegmentTextMZ());
								}
								break;

							// special syntetic functions
							case InstructionEnum.WordsToDword:
								writer.WriteLine("\t\t\tthis.oCPU.{0}.DWord = (uint)(((uint)this.oCPU.{1}.Word << 16) | (uint)this.oCPU.{2}.Word);",
									(RegisterEnum)(instruction.Parameters[0].Value + 8),
									(RegisterEnum)(instruction.Parameters[1].Value + 8),
									(RegisterEnum)(instruction.Parameters[2].Value + 8));
								break;

							// flow control instructions
							case InstructionEnum.SWITCH:
								writer.WriteLine("\t\t\tswitch({0})", instruction.Parameters[0].ToSourceCSTextMZ(instruction.OperandSize));
								writer.WriteLine("\t\t\t{");
								for (int l = 1; l < instruction.Parameters.Count; l++)
								{
									parameter = instruction.Parameters[l];
									writer.WriteLine("\t\t\t\tcase {0}:", parameter.Value);
									writer.WriteLine("\t\t\t\t\tgoto L{0:x4};", parameter.Displacement);
								}
								writer.WriteLine("\t\t\t}");
								break;

							case InstructionEnum.Jcc:
								uiOffset = MZFunction.AddRelativeToOffset(instruction, instruction.Parameters[1]);
								writer.WriteLine("\t\t\tif (this.oCPU.Flags.{0}) goto L{1:x4};",
									((ConditionEnum)instruction.Parameters[0].Value).ToString(), uiOffset);
								break;

							case InstructionEnum.JCXZ:
								uiOffset = MZFunction.AddRelativeToOffset(instruction, instruction.Parameters[0]);
								writer.WriteLine("\t\t\tif (this.oCPU.CX.Word == 0) goto L{0:x4};", uiOffset);
								break;

							case InstructionEnum.LOOP:
								uiOffset = MZFunction.AddRelativeToOffset(instruction, instruction.Parameters[0]);
								writer.WriteLine("\t\t\tif (this.oCPU.Loop(this.oCPU.CX)) goto L{0:x4};", uiOffset);
								break;

							case InstructionEnum.JMP:
								parameter = instruction.Parameters[0];
								if (parameter.Type == InstructionParameterTypeEnum.Relative)
								{
									uiOffset = MZFunction.AddRelativeToOffset(instruction, instruction.Parameters[0]);
									writer.WriteLine("\t\t\tgoto L{0:x4};", uiOffset);
								}
								else if (parameter.Type == InstructionParameterTypeEnum.Register)
								{
									writer.WriteLine("\t\t\t// Probably a switch statement - near jump to register value");
									writer.WriteLine("\t\t\tthis.oCPU.Jmp({0});", parameter.ToCSTextMZ(instruction.OperandSize));
								}
								else
								{
									writer.WriteLine("\t\t\t// Probably a switch statement - near jump to indirect address");
									writer.WriteLine("\t\t\tthis.oCPU.Jmp({0});", string.Format("this.oCPU.ReadWord({0}, {1})",
										parameter.GetSegmentTextMZ(),
										parameter.ToCSTextMZ(instruction.OperandSize)));
								}
								break;

							case InstructionEnum.CALL:
								parameter = instruction.Parameters[0];
								writer.WriteLine("\t\t\tthis.oCPU.PushWord(0x{0:x4}); // stack management - push return offset", instruction.Offset + instruction.Bytes.Count);
								writer.WriteLine($"\t\t\t// Instruction address 0x{instruction.Segment:x4}:0x{instruction.Offset:x4}, size: {instruction.Bytes.Count}");
								if (parameter.Type != InstructionParameterTypeEnum.Immediate)
								{
									writer.WriteLine("\t\t\tthis.oCPU.Call({0});", string.Format("this.oCPU.ReadWord({0}, {1})",
										parameter.GetSegmentTextMZ(),
										parameter.ToCSTextMZ(instruction.OperandSize)));
								}
								else
								{
									function1 = this.GetFunction(0, instruction.Segment, (ushort)parameter.Value, function.StreamOffset);
									if (function1 != null)
									{
										if ((function1.CallType & CallTypeEnum.Near) != CallTypeEnum.Near && (function1.CallType & CallTypeEnum.Far) == CallTypeEnum.Far)
										{
											Console.WriteLine($"Function '{function1.Name}' doesn't support near return");
										}

										if (function.Overlay != function1.Overlay || function.Segment != function1.Segment)
										{
											if (this.oGlobalNamespace.APIFunctions.ContainsKey(function1.Name))
											{
												writer.WriteLine($"\t\t\tthis.oParent.MSCAPI.{function1.Name}();");
											}
											else if (function1.Overlay > 0)
											{
												writer.WriteLine($"\t\t\tthis.oParent.Overlay_{function1.Overlay}.{function1.Name}();");
											}
											else
											{
												writer.WriteLine($"\t\t\tthis.oParent.Segment_{function1.Segment:x4}.{function1.Name}();");
											}
										}
										else
										{
											if ((this.oGlobalNamespace.APIFunctions.ContainsKey(function.Name) &&
												!this.oGlobalNamespace.APIFunctions.ContainsKey(function1.Name)) ||
												(!this.oGlobalNamespace.APIFunctions.ContainsKey(function.Name) &&
												this.oGlobalNamespace.APIFunctions.ContainsKey(function1.Name)))
											{
												if (this.oGlobalNamespace.APIFunctions.ContainsKey(function1.Name))
												{
													writer.WriteLine($"\t\t\tthis.oParent.MSCAPI.{function1.Name}();");
												}
												else if (function1.Overlay > 0)
												{
													writer.WriteLine($"\t\t\tthis.oParent.Overlay_{function1.Overlay}.{function1.Name}();");
												}
												else
												{
													writer.WriteLine($"\t\t\tthis.oParent.Segment_{function1.Segment:x4}.{function1.Name}();");
												}
											}
											else
											{
												writer.WriteLine("\t\t\t{0}();", function1.Name);
											}
										}
									}
									else
									{
										throw new Exception($"Can't find function 'F0_{parameter.Segment:x4}_{parameter.Value:x4}'");
									}
								}
								// all calls in medium memory model are far
								writer.WriteLine("\t\t\tthis.oCPU.PopWord(); // stack management - pop return offset");
								//writer.WriteLine("\t\t\tthis.oCPU.CS.Word = 0x{0:x4}; // restore this function segment", function.Segment);
								break;

							case InstructionEnum.CALLF:
								parameter = instruction.Parameters[0];
								writer.WriteLine("\t\t\tthis.oCPU.PushWord(this.oCPU.CS.Word); // stack management - push return segment");
								writer.WriteLine("\t\t\tthis.oCPU.PushWord(0x{0:x4}); // stack management - push return offset", instruction.Offset + instruction.Bytes.Count);
								writer.WriteLine($"\t\t\t// Instruction address 0x{instruction.Segment:x4}:0x{instruction.Offset:x4}, size: {instruction.Bytes.Count}");
								if (parameter.Type == InstructionParameterTypeEnum.SegmentOffset)
								{
									function1 = this.GetFunction(0, parameter.Segment, (ushort)parameter.Value, function.StreamOffset);
									if (function1 != null)
									{
										if ((function1.CallType & CallTypeEnum.Far) != CallTypeEnum.Far && (function1.CallType & CallTypeEnum.Near) == CallTypeEnum.Near)
										{
											Console.WriteLine($"Function '{function1.Name}' doesn't support far return");
										}

										if (function.Overlay != function1.Overlay || function.Segment != function1.Segment)
										{
											if (this.oGlobalNamespace.APIFunctions.ContainsKey(function1.Name))
											{
												writer.WriteLine($"\t\t\tthis.oParent.MSCAPI.{function1.Name}();");
											}
											else if (function1.Overlay > 0)
											{
												writer.WriteLine($"\t\t\tthis.oParent.Overlay_{function1.Overlay}.{function1.Name}();");
											}
											else
											{
												writer.WriteLine($"\t\t\tthis.oParent.Segment_{function1.Segment:x4}.{function1.Name}();");
											}
										}
										else
										{
											if ((this.oGlobalNamespace.APIFunctions.ContainsKey(function.Name) && 
												!this.oGlobalNamespace.APIFunctions.ContainsKey(function1.Name)) ||
												(!this.oGlobalNamespace.APIFunctions.ContainsKey(function.Name) &&
												this.oGlobalNamespace.APIFunctions.ContainsKey(function1.Name)))
											{
												if (this.oGlobalNamespace.APIFunctions.ContainsKey(function1.Name))
												{
													writer.WriteLine($"\t\t\tthis.oParent.MSCAPI.{function1.Name}();");
												}
												else if (function1.Overlay > 0)
												{
													writer.WriteLine($"\t\t\tthis.oParent.Overlay_{function1.Overlay}.{function1.Name}();");
												}
												else
												{
													writer.WriteLine($"\t\t\tthis.oParent.Segment_{function1.Segment:x4}.{function1.Name}();");
												}
											}
											else
											{
												writer.WriteLine("\t\t\t{0}();", function1.Name);
											}
										}
									}
									else
									{
										throw new Exception($"Can't find function 'F0_{parameter.Segment:x4}_{parameter.Value:x4}'");
									}
								}
								else
								{
									writer.WriteLine("\t\t\tthis.oCPU.CallF({0});", string.Format("this.oCPU.ReadDWord({0}, {1})",
										parameter.GetSegmentTextMZ(),
										parameter.ToCSTextMZ(instruction.OperandSize)));
								}
								writer.WriteLine("\t\t\tthis.oCPU.PopDWord(); // stack management - pop return offset and segment");
								writer.WriteLine("\t\t\tthis.oCPU.CS.Word = 0x{0:x4}; // restore this function segment", function.Segment);
								break;

							case InstructionEnum.CallOverlay:
								writer.WriteLine("\t\t\t// Call to overlay");
								writer.WriteLine("\t\t\tthis.oCPU.PushWord(this.oCPU.CS.Word); // stack management - push return segment");
								writer.WriteLine("\t\t\tthis.oCPU.PushWord(0x{0:x4}); // stack management - push return offset", instruction.Offset + instruction.Bytes.Count);
								function1 = this.GetFunction((int)instruction.Parameters[0].Value, 0, (ushort)instruction.Parameters[1].Value, function.StreamOffset);
								if (function1 != null)
								{
									if ((function1.CallType & CallTypeEnum.Far) != CallTypeEnum.Far && (function1.CallType & CallTypeEnum.Near) == CallTypeEnum.Near)
									{
										Console.WriteLine($"Function '{function1.Name}' doesn't support far return");
									}

									if (function.Overlay != function1.Overlay || function.Segment != function1.Segment)
									{
										if (this.oGlobalNamespace.APIFunctions.ContainsKey(function1.Name))
										{
											writer.WriteLine($"\t\t\tthis.oParent.MSCAPI.{function1.Name}();");
										}
										else if (function1.Overlay > 0)
										{
											writer.WriteLine($"\t\t\tthis.oParent.Overlay_{function1.Overlay}.{function1.Name}();");
										}
										else
										{
											writer.WriteLine($"\t\t\tthis.oParent.Segment_{function1.Segment:x4}.{function1.Name}();");
										}
									}
									else
									{
										writer.WriteLine("\t\t\t{0}();", function1.Name);
									}
								}
								else
								{
									throw new Exception($"Can't find function 'F{instruction.Parameters[0].Value}_0000_{instruction.Parameters[1].Value:x4}'");
								}
								writer.WriteLine("\t\t\tthis.oCPU.PopDWord(); // stack management - pop return offset and segment");
								writer.WriteLine("\t\t\tthis.oCPU.CS.Word = 0x{0:x4}; // restore this function segment", function.Segment);
								break;

							case InstructionEnum.JMPF:
								parameter = instruction.Parameters[0];
								//writer.WriteLine("\t\t\tthis.oCPU.PushWord(0x{0:x4}); // stack management - push return segment", instruction.Segment);
								//writer.WriteLine("\t\t\tthis.oCPU.PushWord(0x{0:x4}); // stack management - push return offset", instruction.Offset + instruction.Bytes.Count);
								writer.WriteLine($"\t\t\t// Instruction address 0x{instruction.Segment:x4}:0x{instruction.Offset:x4}, size: {instruction.Bytes.Count}");
								if (parameter.Type == InstructionParameterTypeEnum.SegmentOffset)
								{
									function1 = this.GetFunction(0, parameter.Segment, (ushort)parameter.Value, function.StreamOffset);
									if (function1 != null)
									{
										if (function.Overlay != function1.Overlay || function.Segment != function1.Segment)
										{
											if (this.oGlobalNamespace.APIFunctions.ContainsKey(function1.Name))
											{
												writer.WriteLine($"\t\t\tthis.oParent.MSCAPI.{function1.Name}();");
											}
											else if (function1.Overlay > 0)
											{
												writer.WriteLine($"\t\t\tthis.oParent.Overlay_{function1.Overlay}.{function1.Name}();");
											}
											else
											{
												writer.WriteLine($"\t\t\tthis.oParent.Segment_{function1.Segment:x4}.{function1.Name}();");
											}
										}
										else
										{
											writer.WriteLine("\t\t\t{0}();", function1.Name);
										}
									}
									else
									{
										throw new Exception($"Can't find function 'F0_{parameter.Segment:x4}_{parameter.Value:x4}'");
									}
								}
								else
								{
									writer.WriteLine("\t\t\tthis.oCPU.JmpF({0});", string.Format("this.oCPU.ReadDWord({0}, {1})",
										parameter.GetSegmentTextMZ(),
										parameter.ToCSTextMZ(instruction.OperandSize)));
								}
								//writer.WriteLine("\t\t\tthis.oCPU.PopDWord(); // stack management - pop return offset, segment");
								writer.WriteLine("\t\t\tthis.oParent.LogExitBlock(\"'{0}'\");", function.Name);
								writer.WriteLine("\t\t\treturn;");
								break;

							case InstructionEnum.RET:
								writer.WriteLine("\t\t\t// Near return");
								writer.WriteLine("\t\t\tthis.oParent.LogExitBlock(\"'{0}'\");", function.Name);
								if (k != function.Instructions.Count - 1)
									writer.WriteLine("\t\t\treturn;");
								break;

							case InstructionEnum.RETF:
								//writer.WriteLine("\t\t// end function body");
								//writer.WriteLine("\t\tthis.oCPU.PopWord();");
								//writer.WriteLine("\t\tthis.oCPU.PopWord();");
								/*if (instruction.Parameters.Count > 0)
								{
									writer.WriteLine("\t\tthis.oCPU.SP.Word += {0};", instruction.Parameters[0].ToCSTextMZ(instruction.OperandSize));
								}*/
								writer.WriteLine("\t\t\t// Far return");
								writer.WriteLine("\t\t\tthis.oParent.LogExitBlock(\"'{0}'\");", function.Name);
								if (k != function.Instructions.Count - 1)
									writer.WriteLine("\t\t\treturn;");
								break;

							case InstructionEnum.IRET:
								writer.WriteLine("\t\t\t// IRET - Pop flags and Far return");
								writer.WriteLine("\t\t\tthis.oParent.LogExitBlock(\"'{0}'\");", function.Name);
								if (k != function.Instructions.Count - 1)
									writer.WriteLine("\t\t\treturn;");
								break;

							case InstructionEnum.INT:
								writer.WriteLine("\t\t\tthis.oCPU.INT(0x{0:x2});", instruction.Parameters[0].Value);
								break;

							case InstructionEnum.If:
								uiOffset = MZFunction.AddRelativeToOffset(instruction, instruction.Parameters[3]);
								writer.WriteLine("\t\t\tif ({0} {1} {2}) goto L{3:x4};",
									instruction.Parameters[0].ToSourceCSTextMZ(instruction.Parameters[0].Size),
									ConditionToCSText((ConditionEnum)instruction.Parameters[2].Value),
									instruction.Parameters[1].ToSourceCSTextMZ(instruction.Parameters[1].Size),
									uiOffset);
								break;

							case InstructionEnum.IfAnd:
								uiOffset = MZFunction.AddRelativeToOffset(instruction, instruction.Parameters[3]);
								writer.WriteLine("\t\t\tif (({0} & {2}) {1} 0) goto L{3:x4};",
									instruction.Parameters[0].ToSourceCSTextMZ(instruction.Parameters[0].Size),
									ConditionToCSText((ConditionEnum)instruction.Parameters[2].Value),
									instruction.Parameters[1].ToSourceCSTextMZ(instruction.Parameters[1].Size),
									uiOffset);
								break;

							case InstructionEnum.IfOr:
								uiOffset = MZFunction.AddRelativeToOffset(instruction, instruction.Parameters[3]);
								writer.WriteLine("\t\t\tif ({0} {1} 0) goto L{2:x4};",
									instruction.Parameters[0].ToSourceCSTextMZ(instruction.Parameters[0].Size),
									ConditionToCSText((ConditionEnum)instruction.Parameters[2].Value),
									uiOffset);
								break;
							default:
								//throw new Exception($"Unexpected instruction type: {instruction.InstructionType}");
								Console.WriteLine($"Unexpected instruction type '{instruction.InstructionType}' " +
									$"in function '{function.Name}' at location 0x{instruction.Segment:x4}:0x{instruction.Offset:x4}");
								break;
						}
					}
					writer.WriteLine("\t\t}");
				}

				writer.WriteLine("\t}");
				writer.WriteLine("}");
			}

			writer.Close();
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

		public MZGlobalNamespace GlobalNamespace
		{
			get { return this.oGlobalNamespace; }
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
	}
}
