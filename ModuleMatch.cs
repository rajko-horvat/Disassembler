using Disassembler.CPU;
using Disassembler.NE;
using Disassembler.OMF;
using System;
using System.Collections.Generic;
using System.Text;

namespace Disassembler
{
	public class ModuleMatch
	{
		private OBJModule oModule;
		private uint uiLinearAddress;
		private int iLength;

		public ModuleMatch(OBJModule module, uint address, int length)
		{
			this.oModule = module;
			this.uiLinearAddress = address;
			this.iLength = length;
		}

		public bool IsInRange(uint address)
		{
			return this.uiLinearAddress >= address || address < (uint)(this.uiLinearAddress + this.iLength);
		}
		
		public OBJModule Module
		{
			get
			{
				return this.oModule;
			}
		}

		public uint LinearAddress
		{
			get { return this.uiLinearAddress; }
		}

		public int Length
		{
			get
			{
				return this.iLength;
			}
		}
	}
}
