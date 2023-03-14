using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Disassembler.NE
{
	public class Entry
	{
		private int iType = 0;
		private string sName = null;
		private bool bExported = false;
		private bool bSharedDataSegment = false;
		private int iRingWordSize = 0;
		private int iInt3F = 0;
		private int iSegment = 0;
		private int iOffset = 0;

		public Entry(int type, string name, int flag, int offset)
			: this(type, name, flag, -1, -1, offset)
		{ }

		public Entry(int type, string name, int flag, int int3F, int segment, int offset)
		{
			this.iType = type;
			this.sName = name;
			this.bExported = (flag & 1) != 0;
			this.bSharedDataSegment = (flag & 2) != 0;
			this.iRingWordSize = (flag & 0xf8) >> 3;
			this.iInt3F = int3F;
			this.iSegment = segment;
			this.iOffset = offset;
		}

		public int Type
		{
			get
			{
				return this.iType;
			}
		}

		public string Name
		{
			get
			{
				return this.sName;
			}
		}

		public bool Exported
		{
			get
			{
				return this.bExported;
			}
		}

		public bool SharedDataSegment
		{
			get
			{
				return this.bSharedDataSegment;
			}
		}

		public int RingWordSize
		{
			get
			{
				return this.iRingWordSize;
			}
		}

		public int Int3F
		{
			get
			{
				return this.iInt3F;
			}
		}

		public int Segment
		{
			get
			{
				return this.iSegment;
			}
		}

		public int Offset
		{
			get
			{
				return this.iOffset;
			}
		}
	}
}
