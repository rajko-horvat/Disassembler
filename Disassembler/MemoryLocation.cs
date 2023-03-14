using System;
using System.Collections.Generic;
using System.Text;

namespace Disassembler
{
	public class MemoryLocation
	{
		private uint uiLinearAddress = 0;
		private uint uiSegment = 0;
		private uint uiOffset = 0;

		public MemoryLocation(uint linearAddress)
		{
			this.uiLinearAddress = linearAddress;
			this.uiSegment = (linearAddress & 0xffff0000) >> 16;
			this.uiOffset = (linearAddress & 0xffff);
		}

		public MemoryLocation(uint segment, uint offset)
		{
			this.uiSegment = segment & 0xffff;
			this.uiOffset = offset;
			this.uiLinearAddress = (this.uiSegment << 16) + this.uiOffset;
		}

		public uint Segment
		{
			get
			{
				return this.uiSegment;
			}
			set
			{
				this.uiSegment = value & 0xffff;
				this.uiLinearAddress = (this.uiSegment << 16) + this.uiOffset;
			}
		}

		public uint Offset
		{
			get
			{
				return this.uiOffset;
			}
			set
			{
				this.uiOffset = value;
				this.uiLinearAddress = (this.uiSegment << 16) + this.uiOffset;
			}
		}

		public uint LinearAddress
		{
			get
			{
				return this.uiLinearAddress;
			}
			set
			{
				this.uiLinearAddress = value;
				this.uiSegment = (value & 0xffff0000) >> 16;
				this.uiOffset = (value & 0xffff);
			}
		}

		public override string ToString()
		{
			return string.Format("0x{0:x4}:0x{1:x4}", this.uiSegment, this.uiOffset);
		}
	}
}
