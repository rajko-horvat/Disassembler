using Disassembler.NE;
using Disassembler.OMF;
using System;
using System.Collections.Generic;
using System.Text;

namespace Disassembler
{
	public class ModuleMatch
	{
		private int iSegment = -1;
		private CModule oModule = null;
		private int iStart = 0;
		private int iLength = 0;

		public ModuleMatch(int segment, CModule module, int start, int length)
		{
			this.iSegment = segment;
			this.oModule = module;
			this.iStart = start;
			this.iLength = length;
		}

		public bool IsInRange(int address)
		{
			return this.iStart >= address || address < this.iStart + this.iLength;
		}

		public int Segment
		{
			get
			{
				return this.iSegment;
			}
		}

		public CModule Module
		{
			get
			{
				return this.oModule;
			}
		}

		public int Start
		{
			get
			{
				return this.iStart;
			}
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
