using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Disassembler.NE
{
	[Flags]
	public enum SegmentFlagsEnum
	{
		None = 0,
		DataSegment = 1,
		MemoryAllocated = 1 << 1,
		SegmentLoaded = 1 << 2,
		Reserved3 = 1 << 3,
		SegmentMoveable = 1 << 4,
		SegmentShareable = 1 << 5,
		PreloadSegment = 1 << 6,
		ExecuteOrReadOnly = 1 << 7,
		ContainsRelocationData = 1 << 8,
		Reserved9 = 1 << 9,
		Reserved10 = 1 << 10,
		Reserved11 = 1 << 11,
		SegmentDiscardable = 1 << 12,
		Reserved13 = 1 << 13,
		Reserved14 = 1 << 14,
		Reserved15 = 1 << 15
	}

	public class Segment
	{
		private byte[] abData = null;
		private SegmentFlagsEnum eFlags = SegmentFlagsEnum.None;
		private int iMinimumSize = -1;
		private List<Relocation> aRelocations = new List<Relocation>();
		private string sNamespace = null;

		public Segment(Stream stream, int sectorSize)
		{
			int iSegmentDataOffset = (int)NewExecutable.ReadUInt16(stream) * sectorSize;
			int iSegmentLength = NewExecutable.ReadUInt16(stream);
			this.eFlags = (SegmentFlagsEnum)NewExecutable.ReadUInt16(stream);
			int iMinimumAllocation = NewExecutable.ReadUInt16(stream);
			if (iMinimumAllocation == 0)
				iMinimumAllocation = 65536;

			long lCurrentPisition = stream.Position;
			stream.Seek(iSegmentDataOffset, SeekOrigin.Begin);
			this.abData = new byte[iSegmentLength];
			stream.Read(abData, 0, iSegmentLength);
			if ((this.eFlags & SegmentFlagsEnum.ContainsRelocationData) == SegmentFlagsEnum.ContainsRelocationData)
			{
				int iRelocationCount = NewExecutable.ReadUInt16(stream);
				for (int i = 0; i < iRelocationCount; i++)
				{
					this.aRelocations.Add(new Relocation(stream));
				}
			}
			// sort ascending by offset
			this.aRelocations.Sort(Relocation.CompareByOffset);

			stream.Seek(lCurrentPisition, SeekOrigin.Begin);
		}

		public bool CompareFlag(SegmentFlagsEnum flag)
		{
			return (this.eFlags & flag) == flag;
		}

		public SegmentFlagsEnum Flags
		{
			get
			{
				return this.eFlags;
			}
		}

		public string Namespace
		{
			get { return this.sNamespace; }
			set { this.sNamespace = value; }
		}

		public byte[] Data
		{
			get
			{
				return this.abData;
			}
		}

		public int MinimumSize
		{
			get
			{
				return this.iMinimumSize;
			}
		}

		public List<Relocation> Relocations
		{
			get
			{
				return this.aRelocations;
			}
		}
	}
}
