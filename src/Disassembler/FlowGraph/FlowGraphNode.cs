using Disassembler.CPU;
using IRB.Collections.Generic;
using System.Reflection.Metadata;

namespace Disassembler
{
	public class FlowGraphNode
	{
		private FlowGraph flowGraph;

		private FlowGraphNodeTypeEnum nodeType;
		private uint linearAddress;
		private int ordinal = -1;
		private FlowGraphLocalEnum requiredLocals = FlowGraphLocalEnum.None;
		private FlowGraphLocalEnum definedLocals = FlowGraphLocalEnum.None;
		private BDictionary<uint, FlowGraphNode> referenceNodes = new();

		private List<int> switchValues = new();

		private List<FlowGraphNode> childNodes = new();

		private List<CPUInstruction> asmInstructions = new();

		// IL Instructions
		private List<ILExpression> ilInstructions = new List<ILExpression>();

		public FlowGraphNode(FlowGraph graph, FlowGraphNodeTypeEnum nodeType, uint address)
		{
			this.flowGraph = graph;
			this.nodeType = nodeType;
			this.linearAddress = address;
		}

		public FlowGraph Graph { get => this.flowGraph; }

		public uint LinearAddress { get => this.linearAddress; }

		public int Ordinal { get => this.ordinal; set => this.ordinal = value; }

		public FlowGraphNodeTypeEnum NodeType { get => this.nodeType; set => this.nodeType = value; }

		public FlowGraphLocalEnum RequiredLocals { get => this.requiredLocals; set => this.requiredLocals = value; }

		public FlowGraphLocalEnum DefinedLocals { get => this.definedLocals; set => this.definedLocals = value; }

		public BDictionary<uint, FlowGraphNode> ReferenceNodes { get => this.referenceNodes; }

		public List<int> SwitchValues { get => this.switchValues; }

		public List<FlowGraphNode> ChildNodes { get => this.childNodes; }

		public List<CPUInstruction> AsmInstructions { get => this.asmInstructions; }

		public List<ILExpression> ILInstructions { get => this.ilInstructions; }

		public void SetLocalRequirements()
		{
			for (int i = 0; i < this.asmInstructions.Count; i++)
			{
				CPUInstruction instruction = this.asmInstructions[i];

				FlowGraphLocalEnum required = FlowGraph.GetRequiredLocals(instruction);
				FlowGraphLocalEnum defined = FlowGraph.GetDefinedLocals(instruction);

				// AX
				if (!FlowGraph.TestLocal(this.definedLocals, required & FlowGraphLocalEnum.AL))
				{
					this.requiredLocals |= FlowGraphLocalEnum.AL;
				}

				if (!FlowGraph.TestLocal(this.definedLocals, required & FlowGraphLocalEnum.AH))
				{
					this.requiredLocals |= FlowGraphLocalEnum.AH;
				}

				// BX
				if (!FlowGraph.TestLocal(this.definedLocals, required & FlowGraphLocalEnum.BL))
				{
					this.requiredLocals |= FlowGraphLocalEnum.BL;
				}

				if (!FlowGraph.TestLocal(this.definedLocals, required & FlowGraphLocalEnum.BH))
				{
					this.requiredLocals |= FlowGraphLocalEnum.BH;
				}

				// CX
				if (!FlowGraph.TestLocal(this.definedLocals, required & FlowGraphLocalEnum.CL))
				{
					this.requiredLocals |= FlowGraphLocalEnum.CL;
				}

				if (!FlowGraph.TestLocal(this.definedLocals, required & FlowGraphLocalEnum.CH))
				{
					this.requiredLocals |= FlowGraphLocalEnum.CH;
				}

				// DX
				if (!FlowGraph.TestLocal(this.definedLocals, required & FlowGraphLocalEnum.DL))
				{
					this.requiredLocals |= FlowGraphLocalEnum.DL;
				}

				if (!FlowGraph.TestLocal(this.definedLocals, required & FlowGraphLocalEnum.DH))
				{
					this.requiredLocals |= FlowGraphLocalEnum.DH;
				}

				// SI
				if (!FlowGraph.TestLocal(this.definedLocals, required & FlowGraphLocalEnum.SI))
				{
					this.requiredLocals |= FlowGraphLocalEnum.SI;
				}

				// DI
				if (!FlowGraph.TestLocal(this.definedLocals, required & FlowGraphLocalEnum.DI))
				{
					this.requiredLocals |= FlowGraphLocalEnum.DI;
				}

				// BP
				if (!FlowGraph.TestLocal(this.definedLocals, required & FlowGraphLocalEnum.BP))
				{
					this.requiredLocals |= FlowGraphLocalEnum.BP;
				}

				// SP
				if (!FlowGraph.TestLocal(this.definedLocals, required & FlowGraphLocalEnum.SP))
				{
					this.requiredLocals |= FlowGraphLocalEnum.SP;
				}

				// CS
				if (!FlowGraph.TestLocal(this.definedLocals, required & FlowGraphLocalEnum.CS))
				{
					this.requiredLocals |= FlowGraphLocalEnum.CS;
				}

				// SS
				if (!FlowGraph.TestLocal(this.definedLocals, required & FlowGraphLocalEnum.SS))
				{
					this.requiredLocals |= FlowGraphLocalEnum.SS;
				}

				// DS
				if (!FlowGraph.TestLocal(this.definedLocals, required & FlowGraphLocalEnum.DS))
				{
					this.requiredLocals |= FlowGraphLocalEnum.DS;
				}

				// ES
				if (!FlowGraph.TestLocal(this.definedLocals, required & FlowGraphLocalEnum.ES))
				{
					this.requiredLocals |= FlowGraphLocalEnum.ES;
				}

				// FS
				if (!FlowGraph.TestLocal(this.definedLocals, required & FlowGraphLocalEnum.FS))
				{
					this.requiredLocals |= FlowGraphLocalEnum.FS;
				}

				// GS
				if (!FlowGraph.TestLocal(this.definedLocals, required & FlowGraphLocalEnum.GS))
				{
					this.requiredLocals |= FlowGraphLocalEnum.GS;
				}

				// ZFlag
				if (!FlowGraph.TestLocal(this.definedLocals, required & FlowGraphLocalEnum.ZFlag))
				{
					this.requiredLocals |= FlowGraphLocalEnum.ZFlag;
				}

				// CFlag
				if (!FlowGraph.TestLocal(this.definedLocals, required & FlowGraphLocalEnum.CFlag))
				{
					this.requiredLocals |= FlowGraphLocalEnum.CFlag;
				}

				// SFlag
				if (!FlowGraph.TestLocal(this.definedLocals, required & FlowGraphLocalEnum.SFlag))
				{
					this.requiredLocals |= FlowGraphLocalEnum.SFlag;
				}

				// OFlag
				if (!FlowGraph.TestLocal(this.definedLocals, required & FlowGraphLocalEnum.OFlag))
				{
					this.requiredLocals |= FlowGraphLocalEnum.OFlag;
				}

				// DFlag
				if (!FlowGraph.TestLocal(this.definedLocals, required & FlowGraphLocalEnum.DFlag))
				{
					this.requiredLocals |= FlowGraphLocalEnum.DFlag;
				}

				this.definedLocals |= defined;
			}
		}

		public void TranslateToIL(BDictionary<CPURegisterEnum, ILExpression> localRegisters, BDictionary<CPUSegmentRegisterEnum, uint> localSegments,
			Stack<ILExpression> localStack)
		{
			if (this.nodeType != FlowGraphNodeTypeEnum.Start && this.nodeType != FlowGraphNodeTypeEnum.End)
			{
				ProgramFunction parentFunction = this.flowGraph.Parent;
				CPUInstruction instruction;

				int instructionCount = this.asmInstructions.Count;

				this.ilInstructions.Clear();

				for (int i = 0; i < this.asmInstructions.Count; i++)
				{
					instruction = this.asmInstructions[i];
					CPUParameter parameter0;
					CPURegisterEnum register;
					ILExpression variable;
					ProgramFunction? function;

					switch (instruction.InstructionType)
					{
						case CPUInstructionEnum.SUB:
							parameter0 = instruction.Parameters[0];

							if (parameter0.Type == CPUParameterTypeEnum.Register &&
								parameter0.Type == instruction.Parameters[1].Type &&
								parameter0.Value == instruction.Parameters[1].Value)
							{
								register = parameter0.RegisterValue;
								parentFunction.Variables.Add(parentFunction.LocalVariablePosition, new ILVariable(parentFunction, ILValueTypeEnum.Int16, parentFunction.LocalVariablePosition));
								variable = new ILLocalVariableReference(parentFunction, parentFunction.LocalVariablePosition);

								if (localRegisters.ContainsKey(register))
								{
									localRegisters.SetValueByKey(register, variable);
								}
								else
								{
									localRegisters.Add(register, variable);
								}

								this.ilInstructions.Add(new ILAssignment(variable, new ILImmediateValue(parameter0.Size, 0)));

								parentFunction.LocalVariablePosition += 2;
							}
							else
							{
								register = parameter0.RegisterValue;

								if (localRegisters.ContainsKey(register))
								{
									variable = localRegisters.GetValueByKey(register);
									ILOperator op = new ILOperator(variable, ILOperatorEnum.Substract, ParameterToIL(localSegments, localRegisters, instruction.Parameters[1]));
									this.ilInstructions.Add(new ILAssignment(variable, op));
								}
								else
								{
									throw new Exception($"Use of a undefined variable '{register}'");
								}
							}
							break;

						case CPUInstructionEnum.INC:
							parameter0 = instruction.Parameters[0];
							switch (parameter0.Type)
							{
								case CPUParameterTypeEnum.Register:
									register = parameter0.RegisterValue;

									if (localRegisters.ContainsKey(register))
									{
										this.ilInstructions.Add(new ILUnaryAssignmentOperator(localRegisters.GetValueByKey(register), ILUnaryOperatorEnum.IncrementAfter));
									}
									else
									{
										throw new Exception($"Use of a undefined variable '{register}'");
									}
									break;

								default:
									throw new Exception($"INC parameter type '{parameter0.Type}' not implemented");
							}
							break;

						case CPUInstructionEnum.MOV:
							parameter0 = instruction.Parameters[0];

							switch (parameter0.Type)
							{
								case CPUParameterTypeEnum.Register:
									register = parameter0.RegisterValue;
									parentFunction.Variables.Add(parentFunction.LocalVariablePosition, new ILVariable(parentFunction, ILValueTypeEnum.Int16, parentFunction.LocalVariablePosition));
									variable = new ILLocalVariableReference(parentFunction, parentFunction.LocalVariablePosition);

									if (localRegisters.ContainsKey(register))
									{
										localRegisters.SetValueByKey(register, variable);
									}
									else
									{
										localRegisters.Add(register, variable);
									}

									this.ilInstructions.Add(new ILAssignment(variable, ParameterToIL(localSegments, localRegisters, instruction.Parameters[1])));

									parentFunction.LocalVariablePosition += 2;
									break;

								default:
									throw new Exception($"MOV left parameter type '{parameter0.Type}' not implemented");
							}
							break;

						case CPUInstructionEnum.PUSH:
							localStack.Push(ParameterToIL(localSegments, localRegisters, instruction.Parameters[0]));
							break;

						case CPUInstructionEnum.CALLF:
							parameter0 = instruction.Parameters[0];

							if (parameter0.Type == CPUParameterTypeEnum.SegmentOffset)
							{
								function = parentFunction.Segment.Parent.FindFunction(0, parameter0.Segment, (ushort)parameter0.Value);

								if (function != null)
								{
									if ((function.CallType & ProgramFunctionTypeEnum.Cdecl) == ProgramFunctionTypeEnum.Cdecl)
									{
										if (i + 1 >= this.asmInstructions.Count)
										{
											// this function call is at the end of the function body, no stack adjustment available
											List<ILExpression> parameterList = new List<ILExpression>();

											while (localStack.Count > 0)
											{
												parameterList.Add(localStack.Pop());
											}

											this.ilInstructions.Add(new ILFunctionCall(function, parameterList));
										}
										else if ((instruction = this.asmInstructions[i + 1]).InstructionType == CPUInstructionEnum.ADD &&
											instruction.OperandSize == CPUParameterSizeEnum.UInt16 &&
											instruction.Parameters.Count == 2 &&
											instruction.Parameters[0].Type == CPUParameterTypeEnum.Register &&
											instruction.Parameters[0].Value == (uint)CPURegisterEnum.SP &&
											instruction.Parameters[1].Type == CPUParameterTypeEnum.Immediate)
										{
											if (instruction.Parameters[1].Value == function.ParameterSize)
											{
												// normal Cdecl function call
												List<ILExpression> parameterList = new List<ILExpression>();

												for (int j = 0; j < function.Parameters.Count; j++)
												{
													parameterList.Add(localStack.Pop());
												}

												this.ilInstructions.Add(new ILFunctionCall(function, parameterList));
												i++;
											}
											else
											{
												throw new Exception($"The function '{function.Segment.ToString()}.{function.Name}' " +
													$"accepts {function.Parameters.Count} parameters, but {(instruction.Parameters[1].Value / 2)} parameters passed");
											}
										}
									}
									else if ((function.CallType & ProgramFunctionTypeEnum.Pascal) == ProgramFunctionTypeEnum.Pascal)
									{
										throw new Exception("Pascal function call not implemented");
									}
									else
									{
										throw new Exception($"Unsupported function call type '{function.CallType}'");
									}
								}
								else
								{
									throw new Exception($"Can't find the function at 0x{parameter0.Segment:x}:0x{parameter0.Value:x}");
								}
							}
							else
							{
								throw new Exception("Indirect function call not implemented");
							}
							break;

						default:
							throw new Exception($"Don't know how to translate '{instruction.ToString()}'");
					}
				}
			}
		}

		private ILExpression ParameterToIL(BDictionary<CPUSegmentRegisterEnum, uint> localSegments,
			BDictionary<CPURegisterEnum, ILExpression> localRegisters, CPUParameter parameter)
		{

			ProgramFunction parentFunction = this.flowGraph.Parent;

			switch (parameter.Type)
			{
				case CPUParameterTypeEnum.Immediate:
					switch (parameter.Size)
					{
						case CPUParameterSizeEnum.UInt8:
							return new ILImmediateValue(ILValueTypeEnum.UInt8, parameter.Value);

						case CPUParameterSizeEnum.UInt16:
							return new ILImmediateValue(ILValueTypeEnum.UInt16, parameter.Value);

						case CPUParameterSizeEnum.UInt32:
							return new ILImmediateValue(ILValueTypeEnum.UInt32, parameter.Value);

						default:
							throw new Exception($"Parameter size {parameter.Size} not implemented");
					}

				case CPUParameterTypeEnum.Register:
					CPURegisterEnum register = parameter.RegisterValue;
					if (localRegisters.ContainsKey(register))
					{
						return localRegisters.GetValueByKey(register);
					}
					else
					{
						throw new Exception($"Use of a undefined variable '{register}'");
					}

				case CPUParameterTypeEnum.LocalParameter:
					return new ILLocalParameterReference(parentFunction, (int)parameter.Displacement);

				case CPUParameterTypeEnum.LocalVariable:
					return new ILLocalVariableReference(parentFunction, (int)parameter.Displacement);

				case CPUParameterTypeEnum.MemoryAddress:
					if (parameter.DataSegment == CPUSegmentRegisterEnum.SS)
					{
						switch (parameter.Value)
						{
							case 0: // [BX + SI]
							case 1: // [BX + DI]
							case 4: // [SI]
							case 5: // [DI]
							case 6: // [{0}]
							case 7: // [BX]

							case 8: // [{0} + BX + SI]
							case 9: // [{0} + BX + DI]
							case 12: // [{0} + SI]
							case 13: // [{0} + DI]
							case 15: // [{0} + BX]

							case 16: // [{0} + BX + SI]
							case 17: // [{0} + BX + DI]
							case 20: // [{0} + SI]
							case 21: // [{0} + DI]
							case 23: // [{0} + BX]
								throw new Exception($"The addressing type {parameter.ToString()} is not supported on segment {parameter.DataSegment}");

							case 2: // [BP + SI]
								break;
							case 3: // [BP + DI]
								break;

							case 10: // [BP {0} + SI]
								break;
							case 11: // [BP {0} + DI]
								break;
							case 14: // [BP {0}]
								break;

							case 18: // [BP {0} + SI]
								break;
							case 19: // [BP {0} + DI]
								break;
							case 22: // [BP {0}]
								break;
						}

						throw new Exception("Not implemented");
					}
					else
					{
						if (!localSegments.ContainsKey(parameter.DataSegment))
						{
							throw new Exception($"The segment '{parameter.DataSegment}' is not defined");
						}

						ProgramSegment segment = parentFunction.Segment.Parent.FindOrCreateSegment(parentFunction.Segment.CPUOverlay, (ushort)localSegments.GetValueByKey(parameter.DataSegment));

						switch (parameter.Value)
						{
							case 2: // [BP + SI]
							case 3: // [BP + DI]

							case 10: // [BP {0} + SI]
							case 11: // [BP {0} + DI]
							case 14: // [BP {0}]

							case 18: // [BP {0} + SI]
							case 19: // [BP {0} + DI]
							case 22: // [BP {0}]
								throw new Exception($"The addressing type {parameter.ToString()} is not supported on segment {parameter.DataSegment}");

							case 0: // [BX + SI]
								break;
							case 1: // [BX + DI]
								break;
							case 4: // [SI]
								break;
							case 5: // [DI]
								break;
							case 6: // [{0}]
								segment.GetOrDefineGlobalVariable(parameter.Size, parameter.Displacement);
								return new ILGlobalVariableReference(segment, parameter.Displacement);
							case 7: // [BX]
								break;

							case 8: // [{0} + BX + SI]
								break;
							case 9: // [{0} + BX + DI]
								break;
							case 12: // [{0} + SI]
								break;
							case 13: // [{0} + DI]
								break;
							case 15: // [{0} + BX]
								break;

							case 16: // [{0} + BX + SI]
								break;
							case 17: // [{0} + BX + DI]
								break;
							case 20: // [{0} + SI]
								break;
							case 21: // [{0} + DI]
								break;
							case 23: // [{0} + BX]
								break;
						}

						throw new Exception("Not implemented");
					}

				default:
					throw new Exception($"Parameter type '{parameter.Type}' not implemented");
			}
		}
	}
}
