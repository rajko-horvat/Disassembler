﻿using Disassembler.CPU;
using IRB.Collections.Generic;

namespace Disassembler
{
	public class FlowGraphNode
	{
		private FlowGraph parentGraph;

		private FlowGraphNodeTypeEnum nodeType;
		private uint linearAddress;
		private int ordinal = -1;
		private FlowGraphLocalEnum requiredLocals = FlowGraphLocalEnum.None;
		private FlowGraphLocalEnum definedLocals = FlowGraphLocalEnum.None;
		private BDictionary<uint, FlowGraphNode> referenceNodes = [];

		private List<int> switchValues = [];

		private List<FlowGraphNode> childNodes = [];

		private List<CPUInstruction> asmInstructions = [];

		// IL Instructions
		private List<ILExpression> ilInstructions = [];
		private bool translatedToIL = false;

		public FlowGraphNode(FlowGraph graph, FlowGraphNodeTypeEnum nodeType, uint address)
		{
			this.parentGraph = graph;
			this.nodeType = nodeType;
			this.linearAddress = address;
		}

		public FlowGraph ParentGraph { get => this.parentGraph; }

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

		public bool TranslatedToIL { get => this.translatedToIL; set => this.translatedToIL = value; }

		internal void SetLocalRequirements()
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

		public void ClearIL()
		{
			this.ilInstructions.Clear();
			this.parentGraph.ParentFunction.TranslatedToIL = false;
		}

		public bool Equals(FlowGraphNode node1)
		{
			bool equal = true;

			if (this.asmInstructions.Count == node1.asmInstructions.Count)
			{
				for (int i = 0; i < this.asmInstructions.Count; i++)
				{
					if (!this.asmInstructions[i].Equals(node1.asmInstructions[i]))
					{
						equal = false;
						break;
					}
				}
			}
			else
			{
				equal = false;
			}

			return equal;
		}
	}
}
