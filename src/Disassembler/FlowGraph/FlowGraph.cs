using Disassembler.CPU;
using IRB.Collections.Generic;
using System.Collections;
using System.Reflection.Metadata;
using System.Text;
using System.Xml.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Disassembler
{
	public class FlowGraph
	{
		private ProgramFunction parent;
		private string name = "";

		private FlowGraphNode? startNode = null;
		private BDictionary<uint, FlowGraphNode> endNodes = new();
		private BDictionary<uint, FlowGraphNode> nodes = new();
		private bool hasBPFrame = false;
		private bool usesSI = false;
		private bool usesDI = false;
		private bool usesDS = false;

		private FlowGraphLocalEnum requiredLocals = FlowGraphLocalEnum.None;
		// the compiler has these defined on the entry to the function
		private FlowGraphLocalEnum definedLocals = FlowGraphLocalEnum.CS | FlowGraphLocalEnum.DS | FlowGraphLocalEnum.SS | FlowGraphLocalEnum.SP;

		public FlowGraph(ProgramFunction fn)
		{
			this.parent = fn;
		}

		public FlowGraph(ProgramFunction fn, string name)
		{
			this.parent = fn;
			this.name = name;

			if (!this.parent.IsLibraryFunction)
			{
				ConstructGraph();
				DetermineFunctionBPFrame();
				ConstructLocalRequirements();
			}
		}

		private void ConstructGraph()
		{
			Queue<FlowGraphNode> unprocessedNodes = new();

			this.startNode = null;
			this.endNodes.Clear();
			this.nodes.Clear();
			this.requiredLocals = FlowGraphLocalEnum.None;
			this.definedLocals = FlowGraphLocalEnum.None;

			int instructionCount = this.parent.AsmInstructions.Count;
			int instructionPos = this.parent.GetInstructionPositionByLinearAddress(this.parent.FunctionEntryPoint);

			if (instructionPos < 0)
			{
				throw new Exception($"Expected instruction at 0x{this.parent.FunctionEntryPoint:x}");
			}

			this.startNode = CreateOrFindNode(0, FlowGraphNodeTypeEnum.Start, unprocessedNodes, false);

			FlowGraphNode currentNode = CreateOrFindNode(this.parent.FunctionEntryPoint, FlowGraphNodeTypeEnum.Block, unprocessedNodes, false);

			this.startNode.ChildNodes.Add(currentNode);

			while (instructionPos >= 0 && instructionPos < instructionCount)
			{
				CPUInstruction instruction = this.parent.AsmInstructions[instructionPos];
				CPUParameter parameter;
				FlowGraphNode newNode;
				bool blockEnd = false;

				if (instruction.Label)
				{
					if (!this.nodes.ContainsKey(instruction.LinearAddress))
					{
						newNode = CreateOrFindNode(instruction.LinearAddress, FlowGraphNodeTypeEnum.Block, unprocessedNodes, true);
						currentNode.ChildNodes.Add(newNode);

						blockEnd = true;
					}
					else if (currentNode.LinearAddress != instruction.LinearAddress)
					{
						currentNode.ChildNodes.Add(this.nodes.GetValueByKey(instruction.LinearAddress));

						blockEnd = true;
					}
				}

				if (!blockEnd)
				{
					switch (instruction.InstructionType)
					{
						// Arithmetic instructions
						case CPUInstructionEnum.ADC:
						case CPUInstructionEnum.SBB:
							currentNode.AsmInstructions.Add(instruction);
							break;

						case CPUInstructionEnum.ADD:
						case CPUInstructionEnum.SUB:
						case CPUInstructionEnum.AND:
						case CPUInstructionEnum.OR:
						case CPUInstructionEnum.XOR:
							currentNode.AsmInstructions.Add(instruction);
							break;

						case CPUInstructionEnum.DEC:
						case CPUInstructionEnum.INC:
						case CPUInstructionEnum.NEG:
						case CPUInstructionEnum.NOT:
							currentNode.AsmInstructions.Add(instruction);
							break;

						case CPUInstructionEnum.DAS:
							currentNode.AsmInstructions.Add(instruction);
							break;

						// Multiply and divide instructions
						case CPUInstructionEnum.MUL:
							currentNode.AsmInstructions.Add(instruction);
							break;

						case CPUInstructionEnum.IMUL:
							currentNode.AsmInstructions.Add(instruction);
							break;

						case CPUInstructionEnum.DIV:
						case CPUInstructionEnum.IDIV:
							currentNode.AsmInstructions.Add(instruction);
							break;

						// Shifting instructions
						case CPUInstructionEnum.SAR:
						case CPUInstructionEnum.SHL:
						case CPUInstructionEnum.SHR:
						case CPUInstructionEnum.RCR:
						case CPUInstructionEnum.RCL:
						case CPUInstructionEnum.ROL:
						case CPUInstructionEnum.ROR:
							currentNode.AsmInstructions.Add(instruction);
							break;

						// Converting instructions
						case CPUInstructionEnum.CBW:
							currentNode.AsmInstructions.Add(instruction);
							break;

						case CPUInstructionEnum.CWD:
							currentNode.AsmInstructions.Add(instruction);
							break;

						// Compare and test instructions
						case CPUInstructionEnum.CMP:
						case CPUInstructionEnum.TEST:
							currentNode.AsmInstructions.Add(instruction);
							break;

						// Load segment and offset instructions
						case CPUInstructionEnum.LDS:
							currentNode.AsmInstructions.Add(instruction);
							break;

						case CPUInstructionEnum.LES:
							currentNode.AsmInstructions.Add(instruction);
							break;

						case CPUInstructionEnum.LEA:
							currentNode.AsmInstructions.Add(instruction);
							break;

						// String instructions
						case CPUInstructionEnum.LODS:
							currentNode.AsmInstructions.Add(instruction);
							break;

						case CPUInstructionEnum.MOVS:
							currentNode.AsmInstructions.Add(instruction);
							break;

						case CPUInstructionEnum.OUTS:
							currentNode.AsmInstructions.Add(instruction);
							break;

						case CPUInstructionEnum.STOS:
							currentNode.AsmInstructions.Add(instruction);
							break;

						// Data move and convert instructions
						case CPUInstructionEnum.MOV:
							currentNode.AsmInstructions.Add(instruction);
							break;

						// Ignored instructions
						case CPUInstructionEnum.WAIT:
						case CPUInstructionEnum.NOP:
							break;

						// BP based stack frame instructions
						case CPUInstructionEnum.ENTER:
							currentNode.AsmInstructions.Add(instruction);
							break;

						case CPUInstructionEnum.LEAVE:
							currentNode.AsmInstructions.Add(instruction);
							break;

						// Stack instructions
						case CPUInstructionEnum.POP:
							currentNode.AsmInstructions.Add(instruction);
							break;

						case CPUInstructionEnum.POPA:
							currentNode.AsmInstructions.Add(instruction);
							break;

						case CPUInstructionEnum.POPF:
							currentNode.AsmInstructions.Add(instruction);
							break;

						case CPUInstructionEnum.PUSH:
							currentNode.AsmInstructions.Add(instruction);
							break;

						case CPUInstructionEnum.PUSHA:
							currentNode.AsmInstructions.Add(instruction);
							break;

						case CPUInstructionEnum.PUSHF:
							currentNode.AsmInstructions.Add(instruction);
							break;

						// Flag instructions
						case CPUInstructionEnum.CLD:
							currentNode.AsmInstructions.Add(instruction);
							break;

						case CPUInstructionEnum.STD:
							currentNode.AsmInstructions.Add(instruction);
							break;

						case CPUInstructionEnum.CLC:
							currentNode.AsmInstructions.Add(instruction);
							break;

						case CPUInstructionEnum.STC:
							currentNode.AsmInstructions.Add(instruction);
							break;

						case CPUInstructionEnum.CMC:
							currentNode.AsmInstructions.Add(instruction);
							break;

						case CPUInstructionEnum.CLI:
							currentNode.AsmInstructions.Add(instruction);
							break;

						case CPUInstructionEnum.STI:
							currentNode.AsmInstructions.Add(instruction);
							break;

						// Exchange data instruction
						case CPUInstructionEnum.XCHG:
							currentNode.AsmInstructions.Add(instruction);
							break;

						// Input and output port instructions
						case CPUInstructionEnum.IN:
							currentNode.AsmInstructions.Add(instruction);
							break;

						case CPUInstructionEnum.OUT:
							currentNode.AsmInstructions.Add(instruction);
							break;

						// Special syntetic functions
						case CPUInstructionEnum.WordsToDword:
							currentNode.AsmInstructions.Add(instruction);
							break;

						// Flow control instructions
						case CPUInstructionEnum.Jcc:
							if (currentNode.LinearAddress == instruction.LinearAddress)
							{
								currentNode.NodeType = FlowGraphNodeTypeEnum.Block;
								currentNode.AsmInstructions.Add(instruction);

								currentNode.ChildNodes.Add(CreateOrFindNode(MainProgram.ToLinearAddress(instruction.Segment, instruction.Parameters[1].Value), 
									FlowGraphNodeTypeEnum.Block, unprocessedNodes, true));

								instructionPos++;
								if (instructionPos < instructionCount)
								{
									instruction = this.parent.AsmInstructions[instructionPos];
									currentNode.ChildNodes.Add(CreateOrFindNode(instruction.LinearAddress, FlowGraphNodeTypeEnum.Block, unprocessedNodes, true));
								}
								else
								{
									throw new Exception($"Expected next instruction in {currentNode.NodeType} block");
								}
							}
							else if (!this.nodes.ContainsKey(instruction.LinearAddress))
							{
								newNode = CreateOrFindNode(instruction.LinearAddress, FlowGraphNodeTypeEnum.Block, unprocessedNodes, false);
								newNode.AsmInstructions.Add(instruction);
								currentNode.ChildNodes.Add(newNode);

								newNode.ChildNodes.Add(CreateOrFindNode(MainProgram.ToLinearAddress(instruction.Segment, instruction.Parameters[1].Value), 
									FlowGraphNodeTypeEnum.Block, unprocessedNodes, true));

								instructionPos++;
								if (instructionPos < instructionCount)
								{
									instruction = this.parent.AsmInstructions[instructionPos];
									newNode.ChildNodes.Add(CreateOrFindNode(instruction.LinearAddress, FlowGraphNodeTypeEnum.Block, unprocessedNodes, true));
								}
								else
								{
									throw new Exception($"Expected next instruction in {newNode.NodeType} block");
								}
							}
							else
							{
								currentNode.ChildNodes.Add(this.nodes.GetValueByKey(instruction.LinearAddress));
							}

							blockEnd = true;
							break;

						case CPUInstructionEnum.JCXZ:
							if (currentNode.LinearAddress == instruction.LinearAddress)
							{
								currentNode.NodeType = FlowGraphNodeTypeEnum.Block;
								currentNode.AsmInstructions.Add(instruction);

								currentNode.ChildNodes.Add(CreateOrFindNode(MainProgram.ToLinearAddress(instruction.Segment, instruction.Parameters[0].Value),
									FlowGraphNodeTypeEnum.Block, unprocessedNodes, true));

								instructionPos++;
								if (instructionPos < instructionCount)
								{
									instruction = this.parent.AsmInstructions[instructionPos];
									currentNode.ChildNodes.Add(CreateOrFindNode(instruction.LinearAddress, FlowGraphNodeTypeEnum.Block, unprocessedNodes, true));
								}
								else
								{
									throw new Exception($"Expected next instruction in {currentNode.NodeType} block");
								}
							}
							else if (!this.nodes.ContainsKey(instruction.LinearAddress))
							{
								newNode = CreateOrFindNode(instruction.LinearAddress, FlowGraphNodeTypeEnum.Block, unprocessedNodes, false);
								newNode.AsmInstructions.Add(instruction);
								currentNode.ChildNodes.Add(newNode);

								newNode.ChildNodes.Add(CreateOrFindNode(MainProgram.ToLinearAddress(instruction.Segment, instruction.Parameters[0].Value), 
									FlowGraphNodeTypeEnum.Block, unprocessedNodes, true));

								instructionPos++;
								if (instructionPos < instructionCount)
								{
									instruction = this.parent.AsmInstructions[instructionPos];
									newNode.ChildNodes.Add(CreateOrFindNode(instruction.LinearAddress, FlowGraphNodeTypeEnum.Block, unprocessedNodes, true));
								}
								else
								{
									throw new Exception($"Expected next instruction in {newNode.NodeType} block");
								}
							}
							else
							{
								currentNode.ChildNodes.Add(this.nodes.GetValueByKey(instruction.LinearAddress));
							}

							blockEnd = true;
							break;

						case CPUInstructionEnum.LOOP:
							if (currentNode.LinearAddress == instruction.LinearAddress)
							{
								currentNode.NodeType = FlowGraphNodeTypeEnum.Block;
								currentNode.AsmInstructions.Add(instruction);

								currentNode.ChildNodes.Add(CreateOrFindNode(MainProgram.ToLinearAddress(instruction.Segment, instruction.Parameters[0].Value), 
									FlowGraphNodeTypeEnum.Block, unprocessedNodes, true));

								instructionPos++;
								if (instructionPos < instructionCount)
								{
									instruction = this.parent.AsmInstructions[instructionPos];
									currentNode.ChildNodes.Add(CreateOrFindNode(instruction.LinearAddress, FlowGraphNodeTypeEnum.Block, unprocessedNodes, true));
								}
								else
								{
									throw new Exception($"Expected next instruction in {currentNode.NodeType} block");
								}
							}
							else if (!this.nodes.ContainsKey(instruction.LinearAddress))
							{
								newNode = CreateOrFindNode(instruction.LinearAddress, FlowGraphNodeTypeEnum.Block, unprocessedNodes, false);
								newNode.AsmInstructions.Add(instruction);
								currentNode.ChildNodes.Add(newNode);

								newNode.ChildNodes.Add(CreateOrFindNode(MainProgram.ToLinearAddress(instruction.Segment, instruction.Parameters[0].Value), 
									FlowGraphNodeTypeEnum.Block, unprocessedNodes, true));

								instructionPos++;
								if (instructionPos < instructionCount)
								{
									instruction = this.parent.AsmInstructions[instructionPos];
									newNode.ChildNodes.Add(CreateOrFindNode(instruction.LinearAddress, FlowGraphNodeTypeEnum.Block, unprocessedNodes, true));
								}
								else
								{
									throw new Exception($"Expected next instruction in {newNode.NodeType} block");
								}
							}
							else
							{
								currentNode.ChildNodes.Add(this.nodes.GetValueByKey(instruction.LinearAddress));
							}

							blockEnd = true;
							break;

						case CPUInstructionEnum.JMP:
							parameter = instruction.Parameters[0];
							if (parameter.Type == CPUParameterTypeEnum.Immediate)
							{
								newNode = CreateOrFindNode(MainProgram.ToLinearAddress(instruction.Segment, instruction.Parameters[0].Value), 
									FlowGraphNodeTypeEnum.Block, unprocessedNodes, true);
								currentNode.ChildNodes.Add(newNode);

								blockEnd = true;
							}
							else
							{
								currentNode.AsmInstructions.Add(instruction);
							}

							break;

						case CPUInstructionEnum.JMPF:
							parameter = instruction.Parameters[0];
							if (parameter.Type == CPUParameterTypeEnum.SegmentOffset)
							{
								newNode = CreateOrFindNode(MainProgram.ToLinearAddress(parameter.Segment, parameter.Value), FlowGraphNodeTypeEnum.Block, unprocessedNodes, true);
								currentNode.ChildNodes.Add(newNode);

								blockEnd = true;
							}
							else
							{
								currentNode.AsmInstructions.Add(instruction);
							}
							break;

						case CPUInstructionEnum.SWITCH:
							if (currentNode.LinearAddress == instruction.LinearAddress)
							{
								currentNode.NodeType = FlowGraphNodeTypeEnum.Switch;
								currentNode.AsmInstructions.Add(instruction);

								for (int i = 1; i < instruction.Parameters.Count; i++)
								{
									currentNode.SwitchValues.Add((int)instruction.Parameters[i].Value);
									currentNode.ChildNodes.Add(CreateOrFindNode(MainProgram.ToLinearAddress(instruction.Segment, (uint)instruction.Parameters[i].Displacement),
										FlowGraphNodeTypeEnum.Block, unprocessedNodes, true));
								}
							}
							else if (!this.nodes.ContainsKey(instruction.LinearAddress))
							{
								newNode = CreateOrFindNode(instruction.LinearAddress, FlowGraphNodeTypeEnum.Switch, unprocessedNodes, false);
								newNode.AsmInstructions.Add(instruction);
								currentNode.ChildNodes.Add(newNode);

								for (int i = 1; i < instruction.Parameters.Count; i++)
								{
									newNode.SwitchValues.Add((int)instruction.Parameters[i].Value);
									newNode.ChildNodes.Add(CreateOrFindNode(MainProgram.ToLinearAddress(instruction.Segment, (uint)instruction.Parameters[i].Displacement),
										FlowGraphNodeTypeEnum.Block, unprocessedNodes, true));
								}
							}
							else
							{
								currentNode.ChildNodes.Add(this.nodes.GetValueByKey(instruction.LinearAddress));
							}

							blockEnd = true;
							break;

						case CPUInstructionEnum.CALL:
							currentNode.AsmInstructions.Add(instruction);

							if (instructionPos + 1 < instructionCount)
							{
								newNode = CreateOrFindNode(this.parent.AsmInstructions[instructionPos + 1].LinearAddress, FlowGraphNodeTypeEnum.Block, unprocessedNodes, true);
								currentNode.ChildNodes.Add(newNode);
							}

							blockEnd = true;
							break;

						case CPUInstructionEnum.CALLF:
							currentNode.AsmInstructions.Add(instruction);

							if (instructionPos + 1 < instructionCount)
							{
								newNode = CreateOrFindNode(this.parent.AsmInstructions[instructionPos + 1].LinearAddress, FlowGraphNodeTypeEnum.Block, unprocessedNodes, true);
								currentNode.ChildNodes.Add(newNode);
							}

							blockEnd = true;
							break;

						case CPUInstructionEnum.CallOverlay:
							currentNode.AsmInstructions.Add(instruction);

							if (instructionPos + 1 < instructionCount)
							{
								newNode = CreateOrFindNode(this.parent.AsmInstructions[instructionPos + 1].LinearAddress, FlowGraphNodeTypeEnum.Block, unprocessedNodes, true);
								currentNode.ChildNodes.Add(newNode);
							}

							blockEnd = true;
							break;

						case CPUInstructionEnum.INT:
							currentNode.AsmInstructions.Add(instruction);

							if (instructionPos + 1 < instructionCount)
							{
								newNode = CreateOrFindNode(this.parent.AsmInstructions[instructionPos + 1].LinearAddress, FlowGraphNodeTypeEnum.Block, unprocessedNodes, true);
								currentNode.ChildNodes.Add(newNode);
							}

							blockEnd = true;
							break;

						case CPUInstructionEnum.RET:
						case CPUInstructionEnum.RETF:
						case CPUInstructionEnum.IRET:
							if (this.endNodes.ContainsKey(instruction.LinearAddress))
							{
								currentNode.ChildNodes.Add(this.endNodes.GetValueByKey(instruction.LinearAddress));
							}
							else
							{
								newNode = CreateOrFindNode(instruction.LinearAddress, FlowGraphNodeTypeEnum.End, unprocessedNodes, false);
								newNode.AsmInstructions.Add(instruction);
								this.endNodes.Add(newNode.LinearAddress, newNode);
								currentNode.ChildNodes.Add(newNode);
							}

							blockEnd = true;
							break;

						default:
							Console.WriteLine($"Unexpected instruction type '{instruction.InstructionType}'");
							break;
					}
				}

				if (blockEnd || instructionPos >= instructionCount)
				{
					instructionPos = instructionCount;

					while (unprocessedNodes.Count > 0)
					{
						FlowGraphNode node = unprocessedNodes.Dequeue();

						if (node.LinearAddress != 0)
						{
							instructionPos = this.parent.GetInstructionPositionByLinearAddress(node.LinearAddress);
							if (instructionPos < 0)
							{
								throw new Exception($"Expected next instruction in {node.NodeType} block");
							}
							currentNode = node;
							break;
						}
					}
				}
				else
				{
					instructionPos++;
				}
			}

			// update node references
			for (int i = 0; i < this.nodes.Count; i++)
			{
				this.nodes[i].Value.ReferenceNodes.Clear();
			}

			for (int i = 0; i < this.nodes.Count; i++)
			{
				FlowGraphNode node = this.nodes[i].Value;

				for (int j = 0; j < node.ChildNodes.Count; j++)
				{
					FlowGraphNode childNode = node.ChildNodes[j];

					this.nodes.GetValueByKey(childNode.LinearAddress).ReferenceNodes.Add(node.LinearAddress, node);
				}
			}

			// we want to order the nodes by their physical address for a purpose of assigning a unique ordinal number
			int ordinal = 0;
			FlowGraphNode[] sortedNodes = this.nodes.Values.ToArray();

			Array.Sort<FlowGraphNode>(sortedNodes, (item1, item2) => item1.LinearAddress.CompareTo(item2.LinearAddress));

			for (int i = 0; i < sortedNodes.Length; i++)
			{
				sortedNodes[i].Ordinal = ordinal++;
			}

			// check that all nodes have children, except end node
			for (int i = 0; i < this.nodes.Count; i++)
			{
				FlowGraphNode node = this.nodes[i].Value;

				if (node.NodeType != FlowGraphNodeTypeEnum.End && node.ChildNodes.Count == 0)
				{
					throw new Exception($"All nodes should have their children, except end node. " +
						$"The childless node address: 0x{node.LinearAddress:x} in function {this.parent.Segment.ToString()}.{this.parent.Name}");
				}
			}
		}

		private void ConstructLocalRequirements()
		{
			// construct local node requirements
			for (int i = 0; i < this.nodes.Count; i++)
			{
				this.nodes[i].Value.SetLocalRequirements();
			}
		}

		private void DetermineFunctionBPFrame()
		{
			if (this.startNode != null && this.endNodes.Count == 1)
			{
				FlowGraphNode startNode = this.startNode;
				FlowGraphNode endNode = this.endNodes[0].Value;
				CPUInstruction instruction;

				if (startNode.ReferenceNodes.Count == 0 && startNode.ChildNodes.Count == 1)
				{
					FlowGraphNode startNode1 = startNode.ChildNodes[0];

					if (endNode.ReferenceNodes.Count == 1)
					{
						FlowGraphNode endNode1 = endNode.ReferenceNodes[0].Value;

						int endNode1InstructionCount = endNode1.AsmInstructions.Count;

						if (startNode1.AsmInstructions.Count > 4 && endNode1.AsmInstructions.Count > 1 &&
							(instruction = startNode1.AsmInstructions[0]).InstructionType == CPUInstructionEnum.PUSH &&
							instruction.OperandSize == CPUParameterSizeEnum.UInt16 &&
							instruction.Parameters.Count == 1 &&
							instruction.Parameters[0].Type == CPUParameterTypeEnum.Register &&
							instruction.Parameters[0].Value == (uint)CPURegisterEnum.BP &&

							(instruction = startNode1.AsmInstructions[1]).InstructionType == CPUInstructionEnum.MOV &&
							instruction.OperandSize == CPUParameterSizeEnum.UInt16 &&
							instruction.Parameters.Count == 2 &&
							instruction.Parameters[0].Type == CPUParameterTypeEnum.Register &&
							instruction.Parameters[0].Value == (uint)CPURegisterEnum.BP &&
							instruction.Parameters[1].Type == CPUParameterTypeEnum.Register &&
							instruction.Parameters[1].Value == (uint)CPURegisterEnum.SP &&

							(instruction = endNode1.AsmInstructions[endNode1InstructionCount - 1]).InstructionType == CPUInstructionEnum.POP &&
							instruction.OperandSize == CPUParameterSizeEnum.UInt16 &&
							instruction.Parameters.Count == 1 &&
							instruction.Parameters[0].Type == CPUParameterTypeEnum.Register &&
							instruction.Parameters[0].Value == (uint)CPURegisterEnum.BP)
						{
							// function satisfies basic C language frame
							this.hasBPFrame = true;

							#region Adjust start block and end block instruction positions
							// move frame instructions to start and end block
							startNode.AsmInstructions.Add(startNode1.AsmInstructions[0]);
							startNode1.AsmInstructions.RemoveAt(0);
							startNode.AsmInstructions.Add(startNode1.AsmInstructions[0]);
							startNode1.AsmInstructions.RemoveAt(0);

							while (startNode1.AsmInstructions.Count > 0)
							{
								instruction = startNode1.AsmInstructions[0];

								if (instruction.InstructionType == CPUInstructionEnum.SUB &&
									instruction.OperandSize == CPUParameterSizeEnum.UInt16 &&
									instruction.Parameters.Count == 2 &&
									instruction.Parameters[0].Type == CPUParameterTypeEnum.Register &&
									instruction.Parameters[0].Value == (uint)CPURegisterEnum.SP &&
									instruction.Parameters[1].Type == CPUParameterTypeEnum.Immediate)
								{
									startNode.AsmInstructions.Add(instruction);
									startNode1.AsmInstructions.RemoveAt(0);

									this.parent.LocalVariableSize = (int)instruction.Parameters[1].Value;
									this.parent.LocalVariablePosition += this.parent.LocalVariableSize;
								}
								else if (instruction.InstructionType == CPUInstructionEnum.PUSH)
								{
									CPUParameter parameter = instruction.Parameters[0];

									if (parameter.Type == CPUParameterTypeEnum.Register && parameter.Size == CPUParameterSizeEnum.UInt16)
									{
										bool endBlock = true;

										switch ((CPURegisterEnum)parameter.Value)
										{
											case CPURegisterEnum.SI:
												if (this.usesSI)
												{
													throw new Exception($"In function {this.parent.Segment.ToString()}.{this.parent.Name}, the SI register is pushed more than once onto the stack");
												}
												this.usesSI = true;
												endBlock = false;

												startNode.AsmInstructions.Add(instruction);
												startNode1.AsmInstructions.RemoveAt(0);
												break;

											case CPURegisterEnum.DI:
												if (this.usesDI)
												{
													throw new Exception($"In function {this.parent.Segment.ToString()}.{this.parent.Name}, the DI register is pushed more than once onto the stack");
												}
												this.usesDI = true;
												endBlock = false;

												startNode.AsmInstructions.Add(instruction);
												startNode1.AsmInstructions.RemoveAt(0);
												break;
										}

										if (endBlock)
										{
											break;
										}
									}
									else if (parameter.Type == CPUParameterTypeEnum.SegmentRegister && parameter.Size == CPUParameterSizeEnum.UInt16 &&
										(CPUSegmentRegisterEnum)parameter.Value == CPUSegmentRegisterEnum.DS)
									{
										if (this.usesDS)
										{
											throw new Exception($"In function {this.parent.Segment.ToString()}.{this.parent.Name}, the DS segment register is pushed more than once onto the stack");
										}

										this.usesDS = true;

										startNode.AsmInstructions.Add(instruction);
										startNode1.AsmInstructions.RemoveAt(0);
									}
									else
									{
										break;
									}
								}
								else
								{
									break;
								}
							}

							endNode1InstructionCount = endNode1.AsmInstructions.Count;

							endNode.AsmInstructions.Insert(0, endNode1.AsmInstructions[endNode1InstructionCount - 1]);
							endNode1.AsmInstructions.RemoveAt(endNode1InstructionCount - 1);
							endNode1InstructionCount--;

							while (endNode1InstructionCount > 0)
							{
								instruction = endNode1.AsmInstructions[endNode1InstructionCount - 1];

								if (instruction.InstructionType == CPUInstructionEnum.MOV &&
									instruction.OperandSize == CPUParameterSizeEnum.UInt16 &&
									instruction.Parameters.Count == 2 &&
									instruction.Parameters[0].Type == CPUParameterTypeEnum.Register &&
									instruction.Parameters[0].Value == (uint)CPURegisterEnum.SP &&
									instruction.Parameters[1].Type == CPUParameterTypeEnum.Register &&
									instruction.Parameters[1].Value == (uint)CPURegisterEnum.BP)
								{
									endNode.AsmInstructions.Insert(0, instruction);
									endNode1.AsmInstructions.RemoveAt(endNode1InstructionCount - 1);
								}
								else if (instruction.InstructionType == CPUInstructionEnum.POP)
								{
									CPUParameter parameter = instruction.Parameters[0];

									if (parameter.Type == CPUParameterTypeEnum.Register && parameter.Size == CPUParameterSizeEnum.UInt16)
									{
										bool endBlock = true;

										switch ((CPURegisterEnum)parameter.Value)
										{
											case CPURegisterEnum.SI:
												if (!this.usesSI)
												{
													throw new Exception($"In function {this.parent.Segment.ToString()}.{this.parent.Name}, the SI register is not used, but is poped off the stack");
												}
												this.usesSI = true;
												endBlock = false;

												endNode.AsmInstructions.Insert(0, instruction);
												endNode1.AsmInstructions.RemoveAt(endNode1InstructionCount - 1);
												break;

											case CPURegisterEnum.DI:
												if (!this.usesDI)
												{
													throw new Exception($"In function {this.parent.Segment.ToString()}.{this.parent.Name}, the DI register is not used, but is poped off the stack");
												}
												this.usesDI = true;
												endBlock = false;

												endNode.AsmInstructions.Insert(0, instruction);
												endNode1.AsmInstructions.RemoveAt(endNode1InstructionCount - 1);
												break;
										}

										if (endBlock)
										{
											break;
										}
									}
									else if (parameter.Type == CPUParameterTypeEnum.SegmentRegister && parameter.Size == CPUParameterSizeEnum.UInt16 &&
										(CPUSegmentRegisterEnum)parameter.Value == CPUSegmentRegisterEnum.DS)
									{
										if (!this.usesDS)
										{
											throw new Exception($"In function {this.parent.Segment.ToString()}.{this.parent.Name}, the DS segment register is not used, but is poped off the stack");
										}

										this.usesDS = true;

										endNode.AsmInstructions.Insert(0, instruction);
										endNode1.AsmInstructions.RemoveAt(endNode1InstructionCount - 1);
									}
									else
									{
										break;
									}
								}
								else
								{
									break;
								}

								endNode1InstructionCount--;
							}
							#endregion
						}
					}
				}
			}
		}

		public void TranslateFunction()
		{
			// refuse to process assembly and library functions
			if (!this.parent.IsLibraryFunction && this.hasBPFrame && this.startNode != null && this.endNodes.Count > 0)
			{
				UndoCompilerOptimizations();
				TranslateToIL();
				DetermineBasicLanguageConstructionBlocks();
			}
		}

		private void UndoCompilerOptimizations()
		{
			#region Check and move ADD SP, ? instruction after each CALL instruction
			for (int i = 0; i < this.nodes.Count; i++)
			{
				FlowGraphNode node = this.nodes[i].Value;

				if (node.NodeType != FlowGraphNodeTypeEnum.Start && node.NodeType != FlowGraphNodeTypeEnum.End)
				{
					for (int j = 0; j < node.AsmInstructions.Count; j++)
					{
						CPUInstruction instruction = node.AsmInstructions[j];
						CPUInstruction? spInstruction;
						CPUParameter parameter;
						ProgramFunction? function;

						switch (instruction.InstructionType)
						{
							case CPUInstructionEnum.CALL:
								parameter = instruction.Parameters[0];
								function = this.parent.Segment.Parent.FindFunction(0, instruction.Segment, (ushort)parameter.Value);

								if (function != null)
								{
									if (function.ParameterSize > 0)
									{
										spInstruction = FindAndAdjustAddSPCallInstruction(function, node, j);

										if (spInstruction == null)
										{
											Console.WriteLine($"Warning, can't find ADD SP, ? instruction ater CALL instruction at offset 0x{instruction.Offset:x} in " +
												$"function {this.parent.Segment.ToString()}.{this.parent.Name}");
										}
										else if (spInstruction.Parameters[1].Value != function.ParameterSize)
										{
											Console.WriteLine($"Warning, parameter size doesn't match function call to {function.Segment.ToString()}.{function.Name} ({function.ParameterSize}), " +
												$"in function {this.parent.Segment.ToString()}.{this.parent.Name} at offset 0x{instruction.Offset:x} ({spInstruction.Parameters[1].Value})");
										}
									}
								}
								break;

							case CPUInstructionEnum.CALLF:
								parameter = instruction.Parameters[0];
								function = this.parent.Segment.Parent.FindFunction(0, instruction.Segment, (ushort)parameter.Value);

								if (function != null)
								{
									if (function.ParameterSize > 0)
									{
										spInstruction = FindAndAdjustAddSPCallInstruction(function, node, j);

										if (spInstruction == null)
										{
											Console.WriteLine($"Warning, can't find ADD SP, ? instruction ater CALL instruction at 0x{instruction.Offset:x} in " +
												$"function {this.parent.Segment.ToString()}.{this.parent.Name}");
										}
										else if (spInstruction.Parameters[1].Value != function.ParameterSize)
										{
											Console.WriteLine($"Warning, parameter size doesn't match function call to {function.Segment.ToString()}.{function.Name} ({function.ParameterSize}), " +
												$"in function {this.parent.Segment.ToString()}.{this.parent.Name} at offset 0x{instruction.Offset:x} ({spInstruction.Parameters[1].Value})");
										}
									}
								}
								break;

							case CPUInstructionEnum.CallOverlay:
								function = this.parent.Segment.Parent.FindFunction((ushort)instruction.Parameters[0].Value, 0, (ushort)instruction.Parameters[1].Value);

								if (function != null)
								{
									if (function.ParameterSize > 0)
									{
										spInstruction = FindAndAdjustAddSPCallInstruction(function, node, j);

										if (spInstruction == null)
										{
											Console.WriteLine($"Warning, can't find ADD SP, ? instruction ater CALL instruction at 0x{instruction.Offset:x} in " +
												$"function {this.parent.Segment.ToString()}.{this.parent.Name}");
										}
										else if (spInstruction.Parameters[1].Value != function.ParameterSize)
										{
											Console.WriteLine($"Warning, parameter size doesn't match function call to {function.Segment.ToString()}.{function.Name} ({function.ParameterSize}), " +
												$"in function {this.parent.Segment.ToString()}.{this.parent.Name} at offset 0x{instruction.Offset:x} ({spInstruction.Parameters[1].Value})");
										}
									}
								}
								break;
						}
					}
				}
			}
			#endregion

			#region Merge call parameter blocks if they are in different blocks
			for (int i = 0; i < this.nodes.Count; i++)
			{
				FlowGraphNode node = this.nodes[i].Value;

				if (node.NodeType != FlowGraphNodeTypeEnum.Start && node.NodeType != FlowGraphNodeTypeEnum.End)
				{
					for (int j = 0; j < node.AsmInstructions.Count; j++)
					{
						CPUInstruction instruction = node.AsmInstructions[j];
						CPUInstruction instruction1;
						CPUParameter parameter;
						ProgramFunction? function;

						int parameterSize = 0;

						switch (instruction.InstructionType)
						{
							case CPUInstructionEnum.CALL:
								// get parameter size if possible
								// only direct ADD SP, ? instruction is important because some functions can have different number of parameters
								if (j + 1 < node.AsmInstructions.Count)
								{
									instruction1 = node.AsmInstructions[j + 1];

									if (instruction.InstructionType == CPUInstructionEnum.ADD &&
										instruction.Parameters.Count == 2 &&
										instruction.Parameters[0].Type == CPUParameterTypeEnum.Register &&
										instruction.Parameters[0].RegisterValue == CPURegisterEnum.SP)
									{
										parameterSize = (int)instruction.Parameters[1].Value;
									}
								}

								if (parameterSize > 0)
								{
									// backtrack parameters and merge block if necessary
									CheckAndMergeCallParameterBlock(node, j, parameterSize);
								}
								break;

							case CPUInstructionEnum.CALLF:
								break;

							case CPUInstructionEnum.CallOverlay:
								break;
						}
					}
				}
			}
			#endregion
		}

		private CPUInstruction? FindAndAdjustAddSPCallInstruction(ProgramFunction targetFunction, FlowGraphNode node, int instructionIndex)
		{
			CPUInstruction? spInstruction = null;

			if (instructionIndex + 1 < node.AsmInstructions.Count)
			{
				CPUInstruction instruction = node.AsmInstructions[instructionIndex + 1];

				if (instruction.InstructionType == CPUInstructionEnum.ADD &&
					instruction.Parameters.Count == 2 &&
					instruction.Parameters[0].Type == CPUParameterTypeEnum.Register &&
					instruction.Parameters[0].RegisterValue == CPURegisterEnum.SP)
				{
					spInstruction = instruction;
				}
				else if (targetFunction.ParameterSize == 2 &&
					instruction.InstructionType == CPUInstructionEnum.POP &&
					instruction.Parameters.Count == 1 &&
					instruction.Parameters[0].Type == CPUParameterTypeEnum.Register)
				{
					spInstruction = new CPUInstruction(instruction.Segment, instruction.Offset, CPUInstructionEnum.ADD, instruction.DefaultSize);
					spInstruction.Parameters.Add(new CPUParameter(CPUParameterTypeEnum.Register, (uint)CPURegisterEnum.SP));
					spInstruction.Parameters.Add(new CPUParameter(CPUParameterTypeEnum.Immediate, CPUParameterSizeEnum.UInt16, 2));

					node.AsmInstructions[instructionIndex + 1] = spInstruction;
				}
			}

			if (spInstruction == null && node.ChildNodes.Count == 1 && targetFunction.ParameterSize > 0)
			{
				FlowGraphNode currentNode = node.ChildNodes[0];
				Queue<FlowGraphNode> parentNodes = new();

				parentNodes.Enqueue(currentNode);

				// search n levels for required instruction
				for (int i = 0; i < 2; i++)
				{
					if (currentNode.NodeType == FlowGraphNodeTypeEnum.End)
					{
						// we are forgiving missing ADD SP, ? instruction at the end of function
						CPUInstruction instruction = node.AsmInstructions[node.AsmInstructions.Count - 1];

						spInstruction = new CPUInstruction(instruction.Segment, (ushort)(instruction.Offset + 1), CPUInstructionEnum.ADD, instruction.DefaultSize);
						spInstruction.Parameters.Add(new CPUParameter(CPUParameterTypeEnum.Register, (uint)CPURegisterEnum.SP));
						spInstruction.Parameters.Add(new CPUParameter(CPUParameterTypeEnum.Immediate, CPUParameterSizeEnum.UInt16, (uint)targetFunction.ParameterSize));

						node.AsmInstructions.Add(spInstruction);
					}
					else
					{
						for (int j = 0; j < currentNode.AsmInstructions.Count; j++)
						{
							CPUInstruction instruction = currentNode.AsmInstructions[j];

							if (instruction.InstructionType == CPUInstructionEnum.ADD &&
								instruction.Parameters.Count == 2 &&
								instruction.Parameters[0].Type == CPUParameterTypeEnum.Register &&
								instruction.Parameters[0].RegisterValue == CPURegisterEnum.SP)
							{
								spInstruction = instruction;

								currentNode.AsmInstructions.RemoveAt(j);

								while (parentNodes.Count > 0)
								{
									FlowGraphNode parentNode = parentNodes.Dequeue();

									for (int k = 0; k < parentNode.ReferenceNodes.Count; k++)
									{
										FlowGraphNode referenceNode = parentNode.ReferenceNodes[k].Value;

										referenceNode.AsmInstructions.Add(spInstruction);
									}
								}
								break;
							}
							else if (targetFunction.ParameterSize == 2 &&
								instruction.InstructionType == CPUInstructionEnum.POP &&
								instruction.Parameters.Count == 1 &&
								instruction.Parameters[0].Type == CPUParameterTypeEnum.Register)
							{
								spInstruction = new CPUInstruction(instruction.Segment, instruction.Offset, CPUInstructionEnum.ADD, instruction.DefaultSize);
								spInstruction.Parameters.Add(new CPUParameter(CPUParameterTypeEnum.Register, (uint)CPURegisterEnum.SP));
								spInstruction.Parameters.Add(new CPUParameter(CPUParameterTypeEnum.Immediate, CPUParameterSizeEnum.UInt16, 2));

								currentNode.AsmInstructions.RemoveAt(j);

								while (parentNodes.Count > 0)
								{
									FlowGraphNode parentNode = parentNodes.Dequeue();

									for (int k = 0; k < parentNode.ReferenceNodes.Count; k++)
									{
										FlowGraphNode referenceNode = parentNode.ReferenceNodes[k].Value;

										referenceNode.AsmInstructions.Add(spInstruction);
									}
								}
								break;
							}
						}
					}

					if (spInstruction != null || currentNode.ChildNodes.Count != 1)
					{
						break;
					}
					else
					{
						currentNode = currentNode.ChildNodes[0];
						parentNodes.Enqueue(currentNode);
					}
				}
			}

			return spInstruction;
		}

		private void CheckAndMergeCallParameterBlock(FlowGraphNode node, int instructionIndex, int parameterSize)
		{
			int sizeCount = 0;


		}

		private void TranslateToIL()
		{
			BDictionary<CPURegisterEnum, ILExpression> localRegisters = new();
			BDictionary<CPUSegmentRegisterEnum, uint> localSegments = new();
			// track the stack state
			// sometimes the function call doesn't adjust SP (for the length of the parameters) at the end of the function
			Stack<ILExpression> localStack = new Stack<ILExpression>();

			this.parent.Variables.Clear();

			//Console.WriteLine($"Translating '{this.parent.Parent.ToString()}.{this.parent.Name}'");

			localSegments.Add(CPUSegmentRegisterEnum.DS, this.parent.Segment.Parent.DefaultDS);
			localSegments.Add(CPUSegmentRegisterEnum.ES, this.parent.Segment.Parent.DefaultDS);

			this.startNode!.TranslateToIL(localRegisters, localSegments, localStack);
		}

		private void DetermineBasicLanguageConstructionBlocks()
		{
			FlowGraphNode currentNode = this.startNode!;
			FlowGraphNode blockStartNode = this.endNodes[0].Value;
			FlowGraphNode blockEndNode = this.endNodes[0].Value;
			List<FlowGraphNode> nodes = new();
			Queue<FlowGraphNode> branches = new();

			// find the first node that is referenced
			while (currentNode.NodeType != FlowGraphNodeTypeEnum.End)
			{
				if (currentNode.ReferenceNodes.Count > 0)
				{
					blockStartNode = currentNode;
					break;
				}
				else
				{
					for (int i = 0; i < currentNode.ChildNodes.Count; i++)
					{
						branches.Enqueue(currentNode.ChildNodes[i]);
					}

					currentNode = branches.Dequeue();
				}
			}

			// find the smallest subset of nodes between referenced node and child nodes
		}

		private FlowGraphNode CreateOrFindNode(uint address, FlowGraphNodeTypeEnum blockType, Queue<FlowGraphNode> unprocessedNodes, bool queue)
		{
			FlowGraphNode node;

			if (this.nodes.ContainsKey(address))
			{
				node = this.nodes.GetValueByKey(address);
			}
			else
			{
				node = new(this, blockType, address);

				this.nodes.Add(address, node);
				if (queue)
				{
					unprocessedNodes.Enqueue(node);
				}
			}

			return node;
		}

		public static bool TestLocal(FlowGraphLocalEnum flags, FlowGraphLocalEnum flag)
		{
			return (flags & flag) == flag;
		}

		public static FlowGraphLocalEnum GetRequiredLocals(CPUInstruction instruction)
		{
			FlowGraphLocalEnum required = FlowGraphLocalEnum.None;

			switch (instruction.InstructionType)
			{
				// Arithmetic instructions
				case CPUInstructionEnum.ADC:
				case CPUInstructionEnum.SBB:
					required |= FlowGraphLocalEnum.CFlag;
					required |= SourceParameterToRequiredLocals(instruction.Parameters[0]);
					required |= SourceParameterToRequiredLocals(instruction.Parameters[1]);
					break;

				case CPUInstructionEnum.ADD:
				case CPUInstructionEnum.SUB:
				case CPUInstructionEnum.AND:
				case CPUInstructionEnum.OR:
				case CPUInstructionEnum.XOR:
					required |= SourceParameterToRequiredLocals(instruction.Parameters[0]);
					required |= SourceParameterToRequiredLocals(instruction.Parameters[1]);
					break;

				case CPUInstructionEnum.DEC:
				case CPUInstructionEnum.INC:
				case CPUInstructionEnum.NEG:
				case CPUInstructionEnum.NOT:
					required |= SourceParameterToRequiredLocals(instruction.Parameters[0]);
					break;

				case CPUInstructionEnum.DAS:
					required |= FlowGraphLocalEnum.CFlag;
					required |= FlowGraphLocalEnum.AL;
					break;

				// Multiply and divide instructions
				case CPUInstructionEnum.MUL:
					required |= SourceParameterToRequiredLocals(instruction.Parameters[1]);
					required |= SourceParameterToRequiredLocals(instruction.Parameters[2]);
					break;

				case CPUInstructionEnum.IMUL:
					required |= SourceParameterToRequiredLocals(instruction.Parameters[1]);
					required |= SourceParameterToRequiredLocals(instruction.Parameters[2]);
					break;

				case CPUInstructionEnum.DIV:
				case CPUInstructionEnum.IDIV:
					if (instruction.OperandSize == CPUParameterSizeEnum.UInt8)
					{
						required |= FlowGraphLocalEnum.AX;
						required |= SourceParameterToRequiredLocals(instruction.Parameters[0]);
					}
					else
					{
						required |= FlowGraphLocalEnum.AX;
						required |= FlowGraphLocalEnum.DX;
						required |= SourceParameterToRequiredLocals(instruction.Parameters[0]);
					}
					break;

				// Shifting instructions
				case CPUInstructionEnum.RCR:
				case CPUInstructionEnum.RCL:
					required |= FlowGraphLocalEnum.CFlag;
					required |= SourceParameterToRequiredLocals(instruction.Parameters[0]);
					required |= SourceParameterToRequiredLocals(instruction.Parameters[1]);
					break;

				case CPUInstructionEnum.SAR:
				case CPUInstructionEnum.SHL:
				case CPUInstructionEnum.SHR:
				case CPUInstructionEnum.ROL:
				case CPUInstructionEnum.ROR:
					required |= SourceParameterToRequiredLocals(instruction.Parameters[0]);
					required |= SourceParameterToRequiredLocals(instruction.Parameters[1]);
					break;

				// Converting instructions
				case CPUInstructionEnum.CBW:
					required |= FlowGraphLocalEnum.AL;
					break;

				case CPUInstructionEnum.CWD:
					required |= FlowGraphLocalEnum.AX;
					break;

				// Compare and test instructions
				case CPUInstructionEnum.CMP:
				case CPUInstructionEnum.TEST:
					required |= SourceParameterToRequiredLocals(instruction.Parameters[0]);
					required |= SourceParameterToRequiredLocals(instruction.Parameters[1]);
					break;

				// Load segment and offset instructions
				case CPUInstructionEnum.LDS:
					required |= SourceParameterToRequiredLocals(instruction.Parameters[1]);
					break;

				case CPUInstructionEnum.LES:
					required |= SourceParameterToRequiredLocals(instruction.Parameters[1]);
					break;

				case CPUInstructionEnum.LEA:
					required |= SourceParameterToRequiredLocals(instruction.Parameters[1]);
					break;

				// String instructions
				case CPUInstructionEnum.LODS:
					required |= FlowGraphLocalEnum.DFlag;
					required |= FlowGraphLocalEnum.SI;
					required |= CPUSegmentRegisterToLocal(instruction.DefaultDataSegment);
					break;

				case CPUInstructionEnum.MOVS:
					required |= FlowGraphLocalEnum.DFlag;
					required |= FlowGraphLocalEnum.DI;
					required |= FlowGraphLocalEnum.ES;
					required |= FlowGraphLocalEnum.SI;
					required |= CPUSegmentRegisterToLocal(instruction.DefaultDataSegment);
					break;

				case CPUInstructionEnum.OUTS:
					required |= FlowGraphLocalEnum.DFlag;
					required |= FlowGraphLocalEnum.SI;
					required |= CPUSegmentRegisterToLocal(instruction.DefaultDataSegment);
					required |= FlowGraphLocalEnum.DX;
					break;

				case CPUInstructionEnum.STOS:
					required |= FlowGraphLocalEnum.DFlag;
					if (instruction.OperandSize == CPUParameterSizeEnum.UInt8)
					{
						required |= FlowGraphLocalEnum.DI;
						required |= FlowGraphLocalEnum.ES;
						required |= FlowGraphLocalEnum.AL;
					}
					else
					{
						required |= FlowGraphLocalEnum.DI;
						required |= FlowGraphLocalEnum.ES;
						required |= FlowGraphLocalEnum.AX;
					}
					break;

				// Data move and convert instructions
				case CPUInstructionEnum.MOV:
					required |= DestinationParameterToRequiredLocals(instruction.Parameters[0]);
					required |= SourceParameterToRequiredLocals(instruction.Parameters[1]);
					break;

				// Ignored instructions
				case CPUInstructionEnum.WAIT:
				case CPUInstructionEnum.NOP:
					break;

				// BP based stack frame instructions
				case CPUInstructionEnum.ENTER:
					break;

				case CPUInstructionEnum.LEAVE:
					break;

				// Stack instructions
				case CPUInstructionEnum.PUSH:
					required |= SourceParameterToRequiredLocals(instruction.Parameters[0]);
					break;

				case CPUInstructionEnum.PUSHA:
					break;

				case CPUInstructionEnum.PUSHF:
					break;

				case CPUInstructionEnum.POP:
					required |= DestinationParameterToRequiredLocals(instruction.Parameters[0]);
					break;

				case CPUInstructionEnum.POPA:
					break;

				case CPUInstructionEnum.POPF:
					break;

				// Flag instructions
				case CPUInstructionEnum.CLD:
					break;

				case CPUInstructionEnum.STD:
					break;

				case CPUInstructionEnum.CLC:
					break;

				case CPUInstructionEnum.STC:
					break;

				case CPUInstructionEnum.CMC:
					required |= FlowGraphLocalEnum.CFlag;
					break;

				case CPUInstructionEnum.CLI:
					break;

				case CPUInstructionEnum.STI:
					break;

				// Exchange data instruction
				case CPUInstructionEnum.XCHG:
					required |= SourceParameterToRequiredLocals(instruction.Parameters[0]);
					required |= SourceParameterToRequiredLocals(instruction.Parameters[1]);
					break;

				// Input and output port instructions
				case CPUInstructionEnum.IN:
					required |= SourceParameterToRequiredLocals(instruction.Parameters[1]);
					break;

				case CPUInstructionEnum.OUT:
					required |= SourceParameterToRequiredLocals(instruction.Parameters[0]);
					required |= SourceParameterToRequiredLocals(instruction.Parameters[1]);
					break;

				// Special syntetic functions
				case CPUInstructionEnum.WordsToDword:
					required |= SourceParameterToRequiredLocals(instruction.Parameters[1]);
					required |= SourceParameterToRequiredLocals(instruction.Parameters[2]);
					break;

				// Flow control instructions
				case CPUInstructionEnum.Jcc:
					required |= SourceParameterToRequiredLocals(instruction.Parameters[0]);
					break;

				case CPUInstructionEnum.JCXZ:
					required |= FlowGraphLocalEnum.CX;
					break;

				case CPUInstructionEnum.LOOP:
					required |= FlowGraphLocalEnum.CX;
					break;

				case CPUInstructionEnum.JMP:
					required |= SourceParameterToRequiredLocals(instruction.Parameters[0]);
					break;

				case CPUInstructionEnum.JMPF:
					required |= SourceParameterToRequiredLocals(instruction.Parameters[0]);
					break;

				case CPUInstructionEnum.SWITCH:
					required |= SourceParameterToRequiredLocals(instruction.Parameters[0]);
					break;

				case CPUInstructionEnum.CALL:
					required |= SourceParameterToRequiredLocals(instruction.Parameters[0]);
					break;

				case CPUInstructionEnum.CALLF:
					required |= SourceParameterToRequiredLocals(instruction.Parameters[0]);
					break;

				case CPUInstructionEnum.CallOverlay:
					break;

				case CPUInstructionEnum.INT:
					required |= SourceParameterToRequiredLocals(instruction.Parameters[0]);
					break;

				case CPUInstructionEnum.RET:
				case CPUInstructionEnum.RETF:
					if (instruction.Parameters.Count > 0)
					{
						required |= SourceParameterToRequiredLocals(instruction.Parameters[0]);
					}
					break;

				case CPUInstructionEnum.IRET:
					break;

				default:
					Console.WriteLine($"Unexpected instruction type '{instruction.InstructionType}'");
					break;
			}

			return required;
		}

		public static FlowGraphLocalEnum GetDefinedLocals(CPUInstruction instruction)
		{
			FlowGraphLocalEnum defined = FlowGraphLocalEnum.None;

			switch (instruction.InstructionType)
			{
				// Arithmetic instructions
				case CPUInstructionEnum.ADC:
				case CPUInstructionEnum.SBB:
					break;

				case CPUInstructionEnum.ADD:
				case CPUInstructionEnum.SUB:
				case CPUInstructionEnum.AND:
				case CPUInstructionEnum.OR:
				case CPUInstructionEnum.XOR:
					break;

				case CPUInstructionEnum.DEC:
				case CPUInstructionEnum.INC:
				case CPUInstructionEnum.NEG:
				case CPUInstructionEnum.NOT:
					break;

				case CPUInstructionEnum.DAS:
					break;

				// Multiply and divide instructions
				case CPUInstructionEnum.MUL:
					break;

				case CPUInstructionEnum.IMUL:
					break;

				case CPUInstructionEnum.DIV:
				case CPUInstructionEnum.IDIV:
					break;

				// Shifting instructions
				case CPUInstructionEnum.RCR:
				case CPUInstructionEnum.RCL:
					break;

				case CPUInstructionEnum.SAR:
				case CPUInstructionEnum.SHL:
				case CPUInstructionEnum.SHR:
				case CPUInstructionEnum.ROL:
				case CPUInstructionEnum.ROR:
					break;

				// Converting instructions
				case CPUInstructionEnum.CBW:
					defined |= FlowGraphLocalEnum.AH;
					break;

				case CPUInstructionEnum.CWD:
					defined |= FlowGraphLocalEnum.DX;
					break;

				// Compare and test instructions
				case CPUInstructionEnum.CMP:
				case CPUInstructionEnum.TEST:
					break;

				// Load segment and offset instructions
				case CPUInstructionEnum.LDS:
					defined |= FlowGraphLocalEnum.DS;
					defined |= DestinationParameterToDefinedLocals(instruction.Parameters[0]);
					break;

				case CPUInstructionEnum.LES:
					defined |= FlowGraphLocalEnum.ES;
					defined |= DestinationParameterToDefinedLocals(instruction.Parameters[0]);
					break;

				case CPUInstructionEnum.LEA:
					defined |= DestinationParameterToDefinedLocals(instruction.Parameters[0]);
					break;

				// String instructions
				case CPUInstructionEnum.LODS:
					switch (instruction.OperandSize)
					{
						case CPUParameterSizeEnum.UInt8:
							defined |= FlowGraphLocalEnum.AL;
							break;

						case CPUParameterSizeEnum.UInt16:
						case CPUParameterSizeEnum.UInt32:
							defined |= FlowGraphLocalEnum.AX;
							break;
					}
					break;

				case CPUInstructionEnum.MOVS:
				case CPUInstructionEnum.OUTS:
				case CPUInstructionEnum.STOS:
					break;

				// Data move and convert instructions
				case CPUInstructionEnum.MOV:
					defined |= DestinationParameterToDefinedLocals(instruction.Parameters[0]);
					break;

				// Ignored instructions
				case CPUInstructionEnum.WAIT:
				case CPUInstructionEnum.NOP:
					break;

				// BP based stack frame instructions
				case CPUInstructionEnum.ENTER:
				case CPUInstructionEnum.LEAVE:
					break;

				// Stack instructions
				case CPUInstructionEnum.PUSH:
					break;

				case CPUInstructionEnum.PUSHA:
					break;

				case CPUInstructionEnum.PUSHF:
					break;

				case CPUInstructionEnum.POP:
					defined |= DestinationParameterToDefinedLocals(instruction.Parameters[0]);
					break;

				case CPUInstructionEnum.POPA:
					break;

				case CPUInstructionEnum.POPF:
					break;

				// Flag instructions
				case CPUInstructionEnum.CLD:
					break;

				case CPUInstructionEnum.STD:
					break;

				case CPUInstructionEnum.CLC:
					break;

				case CPUInstructionEnum.STC:
					break;

				case CPUInstructionEnum.CMC:
					break;

				case CPUInstructionEnum.CLI:
					break;

				case CPUInstructionEnum.STI:
					break;

				// Exchange data instruction
				case CPUInstructionEnum.XCHG:
					break;

				// Input and output port instructions
				case CPUInstructionEnum.IN:
					switch (instruction.OperandSize)
					{
						case CPUParameterSizeEnum.UInt8:
							defined |= FlowGraphLocalEnum.AL;
							break;

						case CPUParameterSizeEnum.UInt16:
						case CPUParameterSizeEnum.UInt32:
							defined |= FlowGraphLocalEnum.AX;
							break;
					}
					break;

				case CPUInstructionEnum.OUT:
					break;

				// Special syntetic functions
				case CPUInstructionEnum.WordsToDword:
					defined |= DestinationParameterToDefinedLocals(instruction.Parameters[0]);
					break;

				// Flow control instructions
				case CPUInstructionEnum.Jcc:
					break;

				case CPUInstructionEnum.JCXZ:
					break;

				case CPUInstructionEnum.LOOP:
					break;

				case CPUInstructionEnum.JMP:
					break;

				case CPUInstructionEnum.JMPF:
					break;

				case CPUInstructionEnum.SWITCH:
					break;

				case CPUInstructionEnum.CALL:
				case CPUInstructionEnum.CALLF:
				case CPUInstructionEnum.CallOverlay:
					defined |= FlowGraphLocalEnum.AX;
					defined |= FlowGraphLocalEnum.DX;
					break;

				case CPUInstructionEnum.INT:
					break;

				case CPUInstructionEnum.RET:
				case CPUInstructionEnum.RETF:
					break;

				case CPUInstructionEnum.IRET:
					break;

				default:
					Console.WriteLine($"Unexpected instruction type '{instruction.InstructionType}'");
					break;
			}

			// and now define flags
			defined |= CPUFlagsToLocals(instruction.ClearedFlags | instruction.ModifiedFlags | instruction.SetFlags | instruction.UndefinedFlags);

			return defined;
		}

		private static FlowGraphLocalEnum SourceParameterToRequiredLocals(CPUParameter parameter)
		{
			FlowGraphLocalEnum locals = FlowGraphLocalEnum.None;

			if (parameter.Size == CPUParameterSizeEnum.UInt32)
				throw new Exception("x32 addressing mode not yet implemented");

			switch (parameter.Type)
			{
				case CPUParameterTypeEnum.Immediate:
				case CPUParameterTypeEnum.SegmentOffset:
					break;

				case CPUParameterTypeEnum.Register:
					locals |= CPURegisterToLocal(parameter.RegisterValue);
					break;

				case CPUParameterTypeEnum.SegmentRegister:
					locals |= CPUSegmentRegisterToLocal(parameter.SegmentRegisterValue);
					break;

				case CPUParameterTypeEnum.Condition:
					switch ((CPUJumpConditionEnum)parameter.Value)
					{
						case CPUJumpConditionEnum.O:
						case CPUJumpConditionEnum.NO:
							locals |= FlowGraphLocalEnum.OFlag;
							break;

						case CPUJumpConditionEnum.S:
						case CPUJumpConditionEnum.NS:
							locals |= FlowGraphLocalEnum.SFlag;
							break;

						case CPUJumpConditionEnum.A:
						case CPUJumpConditionEnum.BE:
							locals |= FlowGraphLocalEnum.ZFlag | FlowGraphLocalEnum.CFlag;
							break;

						case CPUJumpConditionEnum.AE:
						case CPUJumpConditionEnum.B:
							locals |= FlowGraphLocalEnum.CFlag;
							break;

						case CPUJumpConditionEnum.E:
							locals |= FlowGraphLocalEnum.ZFlag;
							break;

						case CPUJumpConditionEnum.NE:
							locals |= FlowGraphLocalEnum.ZFlag;
							break;

						case CPUJumpConditionEnum.G:
						case CPUJumpConditionEnum.LE:
							locals |= FlowGraphLocalEnum.ZFlag | FlowGraphLocalEnum.SFlag | FlowGraphLocalEnum.OFlag;
							break;

						case CPUJumpConditionEnum.GE:
						case CPUJumpConditionEnum.L:
							locals |= FlowGraphLocalEnum.SFlag | FlowGraphLocalEnum.OFlag;
							break;


						default:
							Console.WriteLine($"Undefined local flags for '{((CPUJumpConditionEnum)parameter.Value)}'");
							break;
					}
					break;

				case CPUParameterTypeEnum.MemoryAddress:
				case CPUParameterTypeEnum.LEAMemoryAddress:
					locals |= CPUSegmentRegisterToLocal(parameter.DefaultDataSegment);

					switch (parameter.Value)
					{
						case 0:
						case 8:
						case 16:
							locals |= FlowGraphLocalEnum.BX;
							locals |= FlowGraphLocalEnum.SI;
							break;

						case 1:
						case 9:
						case 17:
							locals |= FlowGraphLocalEnum.BX;
							locals |= FlowGraphLocalEnum.DI;
							break;

						case 2:
						case 10:
						case 18:
							locals |= FlowGraphLocalEnum.BP;
							locals |= FlowGraphLocalEnum.SI;
							break;

						case 3:
						case 11:
						case 19:
							locals |= FlowGraphLocalEnum.BP;
							locals |= FlowGraphLocalEnum.DI;
							break;

						case 4:
						case 12:
						case 20:
							locals |= FlowGraphLocalEnum.SI;
							break;

						case 5:
						case 13:
						case 21:
							locals |= FlowGraphLocalEnum.DI;
							break;

						case 6:
							break;

						case 14:
						case 22:
							locals |= FlowGraphLocalEnum.BP;
							break;

						case 7:
						case 15:
						case 23:
							locals |= FlowGraphLocalEnum.BX;
							break;
					}
					break;
			}

			return locals;
		}

		private static FlowGraphLocalEnum DestinationParameterToRequiredLocals(CPUParameter parameter)
		{
			FlowGraphLocalEnum locals = FlowGraphLocalEnum.None;

			if (parameter.Size == CPUParameterSizeEnum.UInt32)
				throw new Exception("x32 addressing mode not yet implemented");

			switch (parameter.Type)
			{
				case CPUParameterTypeEnum.Immediate:
				case CPUParameterTypeEnum.SegmentOffset:
				case CPUParameterTypeEnum.Register:
				case CPUParameterTypeEnum.SegmentRegister:
				case CPUParameterTypeEnum.Condition:
					break;

				case CPUParameterTypeEnum.MemoryAddress:
				case CPUParameterTypeEnum.LEAMemoryAddress:
					locals |= CPUSegmentRegisterToLocal(parameter.DefaultDataSegment);

					switch (parameter.Value)
					{
						case 0:
						case 8:
						case 16:
							locals |= FlowGraphLocalEnum.BX;
							locals |= FlowGraphLocalEnum.SI;
							break;

						case 1:
						case 9:
						case 17:
							locals |= FlowGraphLocalEnum.BX;
							locals |= FlowGraphLocalEnum.DI;
							break;

						case 2:
						case 10:
						case 18:
							locals |= FlowGraphLocalEnum.BP;
							locals |= FlowGraphLocalEnum.SI;
							break;

						case 3:
						case 11:
						case 19:
							locals |= FlowGraphLocalEnum.BP;
							locals |= FlowGraphLocalEnum.DI;
							break;

						case 4:
						case 12:
						case 20:
							locals |= FlowGraphLocalEnum.SI;
							break;

						case 5:
						case 13:
						case 21:
							locals |= FlowGraphLocalEnum.DI;
							break;

						case 6:
							break;

						case 14:
						case 22:
							locals |= FlowGraphLocalEnum.BP;
							break;

						case 7:
						case 15:
						case 23:
							locals |= FlowGraphLocalEnum.BX;
							break;
					}
					break;
			}

			return locals;
		}

		private static FlowGraphLocalEnum DestinationParameterToDefinedLocals(CPUParameter parameter)
		{
			FlowGraphLocalEnum locals = FlowGraphLocalEnum.None;

			if (parameter.Size == CPUParameterSizeEnum.UInt32)
				throw new Exception("x32 addressing mode not yet implemented");

			switch (parameter.Type)
			{
				case CPUParameterTypeEnum.Immediate:
				case CPUParameterTypeEnum.SegmentOffset:
				case CPUParameterTypeEnum.Condition:
					break;

				case CPUParameterTypeEnum.Register:
					locals |= CPURegisterToLocal(parameter.RegisterValue);
					break;

				case CPUParameterTypeEnum.SegmentRegister:
					locals |= CPUSegmentRegisterToLocal(parameter.SegmentRegisterValue);
					break;

				case CPUParameterTypeEnum.MemoryAddress:
				case CPUParameterTypeEnum.LEAMemoryAddress:
					break;
			}

			return locals;
		}

		public static FlowGraphLocalEnum CPURegisterToLocal(CPURegisterEnum register)
		{
			switch (register)
			{
				case CPURegisterEnum.AL:
					return FlowGraphLocalEnum.AL;

				case CPURegisterEnum.AH:
					return FlowGraphLocalEnum.AH;

				case CPURegisterEnum.BL:
					return FlowGraphLocalEnum.BL;

				case CPURegisterEnum.BH:
					return FlowGraphLocalEnum.BH;

				case CPURegisterEnum.CL:
					return FlowGraphLocalEnum.CL;

				case CPURegisterEnum.CH:
					return FlowGraphLocalEnum.CH;

				case CPURegisterEnum.DL:
					return FlowGraphLocalEnum.DL;

				case CPURegisterEnum.DH:
					return FlowGraphLocalEnum.DH;

				case CPURegisterEnum.AX:
					return FlowGraphLocalEnum.AX;

				case CPURegisterEnum.BX:
					return FlowGraphLocalEnum.BX;

				case CPURegisterEnum.CX:
					return FlowGraphLocalEnum.CX;

				case CPURegisterEnum.DX:
					return FlowGraphLocalEnum.DX;

				case CPURegisterEnum.SI:
					return FlowGraphLocalEnum.SI;

				case CPURegisterEnum.DI:
					return FlowGraphLocalEnum.DI;

				case CPURegisterEnum.BP:
					return FlowGraphLocalEnum.BP;

				case CPURegisterEnum.SP:
					return FlowGraphLocalEnum.SP;

				case CPURegisterEnum.AX_DX:
					return FlowGraphLocalEnum.AX | FlowGraphLocalEnum.DX;

				default:
					return FlowGraphLocalEnum.None;
			}
		}

		public static FlowGraphLocalEnum CPUSegmentRegisterToLocal(CPUSegmentRegisterEnum segmentRegister)
		{
			switch (segmentRegister)
			{
				case CPUSegmentRegisterEnum.ES:
					return FlowGraphLocalEnum.ES;

				case CPUSegmentRegisterEnum.CS:
					return FlowGraphLocalEnum.CS;

				case CPUSegmentRegisterEnum.SS:
					return FlowGraphLocalEnum.SS;

				case CPUSegmentRegisterEnum.DS:
					return FlowGraphLocalEnum.DS;

				case CPUSegmentRegisterEnum.FS:
					return FlowGraphLocalEnum.FS;

				case CPUSegmentRegisterEnum.GS:
					return FlowGraphLocalEnum.GS;

				default:
					return FlowGraphLocalEnum.None;
			}
		}

		public static FlowGraphLocalEnum CPUFlagsToLocals(CPUFlagsEnum flags)
		{
			FlowGraphLocalEnum defined = FlowGraphLocalEnum.None;

			if (CPUInstruction.TestFlag(flags, CPUFlagsEnum.ZF))
			{
				defined |= FlowGraphLocalEnum.ZFlag;
			}

			if (CPUInstruction.TestFlag(flags, CPUFlagsEnum.CF))
			{
				defined |= FlowGraphLocalEnum.CFlag;
			}

			if (CPUInstruction.TestFlag(flags, CPUFlagsEnum.SF))
			{
				defined |= FlowGraphLocalEnum.SFlag;
			}

			if (CPUInstruction.TestFlag(flags, CPUFlagsEnum.OF))
			{
				defined |= FlowGraphLocalEnum.OFlag;
			}

			if (CPUInstruction.TestFlag(flags, CPUFlagsEnum.DF))
			{
				defined |= FlowGraphLocalEnum.DFlag;
			}

			return defined;
		}

		public void WriteGraphDOT(string fileName)
		{
			using (FileStream stream = new(fileName, FileMode.Create))
			{
				WriteGraphDOT(stream);
			}
		}

		public void WriteGraphDOT(Stream stream)
		{
			StreamWriter writer = new(stream, Encoding.ASCII);
			writer.NewLine = "\n";

			writer.WriteLine($"digraph {this.name}");
			writer.WriteLine("{");

			uint[] nodeAdresses = this.nodes.Keys.ToArray();

			if (this.startNode != null)
			{
				writer.WriteLine($"Start [shape=\"invhouse\", label=\"Start\\nRequires: {this.startNode.RequiredLocals.ToString()}\\nDefines: {this.startNode.DefinedLocals.ToString()}\"];");
			}
			else
			{
				writer.WriteLine($"Start [shape=\"invhouse\"];");
			}

			for (int i = 0; i < nodeAdresses.Length; i++)
			{
				FlowGraphNode node = this.nodes.GetValueByKey(nodeAdresses[i]);
				switch (node.NodeType)
				{
					case FlowGraphNodeTypeEnum.Start:
					case FlowGraphNodeTypeEnum.End:
						break;

					case FlowGraphNodeTypeEnum.Block:
						writer.WriteLine($"n{node.LinearAddress:x} [shape=\"box\", label=\"n{node.LinearAddress:x}\\nRequires: {node.RequiredLocals.ToString()}\\nDefines: {node.DefinedLocals.ToString()}\"];");
						break;

					case FlowGraphNodeTypeEnum.If:
						writer.WriteLine($"n{node.LinearAddress:x} [shape=\"diamond\", label=\"n{node.LinearAddress:x}\\nRequires: {node.RequiredLocals.ToString()}\\nDefines: {node.DefinedLocals.ToString()}\"];");
						break;

					case FlowGraphNodeTypeEnum.Switch:
						writer.WriteLine($"n{node.LinearAddress:x} [shape=\"hexagon\", label=\"n{node.LinearAddress:x}\\nRequires: {node.RequiredLocals.ToString()}\\nDefines: {node.DefinedLocals.ToString()}\"];");
						break;

					case FlowGraphNodeTypeEnum.DoWhile:
					case FlowGraphNodeTypeEnum.While:
					case FlowGraphNodeTypeEnum.For:
						writer.WriteLine($"n{node.LinearAddress:x} [shape=\"parallelogram\", label=\"n{node.LinearAddress:x}\\nRequires: {node.RequiredLocals.ToString()}\\nDefines: {node.DefinedLocals.ToString()}\"];");
						break;
				}

				for (int j = 0; j < node.ChildNodes.Count; j++)
				{
					FlowGraphNode childNode = node.ChildNodes[j];

					if (childNode.NodeType == FlowGraphNodeTypeEnum.End)
					{
						writer.Write($"n{node.LinearAddress:x}->End");
					}
					else if (node.NodeType == FlowGraphNodeTypeEnum.Start)
					{
						writer.Write($"Start->n{childNode.LinearAddress:x}");
					}
					else
					{
						writer.Write($"n{node.LinearAddress:x}->n{childNode.LinearAddress:x}");

						if (node.NodeType == FlowGraphNodeTypeEnum.Switch)
						{
							writer.Write($" [label=\"{node.SwitchValues[j]}\"]");
						}
					}

					writer.WriteLine(";");
				}
			}

			writer.WriteLine($"End [shape=\"house\"];");

			writer.WriteLine("}");

			writer.Flush();
			writer.Close();
		}

		public ProgramFunction Parent { get => this.parent; }

		public string Name { get => this.name; set => this.name = value; }

		public BDictionary<uint, FlowGraphNode> Nodes { get => this.nodes; }

		public FlowGraphNode? StartNode { get => this.startNode; set => this.startNode = value; }

		public BDictionary<uint, FlowGraphNode> EndNodes { get => this.endNodes; set => this.endNodes = value; }

		public FlowGraphLocalEnum RequiredLocals { get => this.requiredLocals; set => this.requiredLocals = value; }

		public FlowGraphLocalEnum DefinedLocals { get => this.definedLocals; set => this.definedLocals = value; }

		public bool BPFrame { get => this.hasBPFrame; }
	}
}
