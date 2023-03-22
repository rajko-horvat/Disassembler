using System;
using System.Collections.Generic;
using System.IO;

namespace Disassembler.OMF
{
	public class LogicalData
	{
		private SegmentDefinition oSegment = null;
		private int iOffset = 0;
		private byte[] aData = new byte[] { };

		public LogicalData(Stream stream, List<SegmentDefinition> segments)
		{
			int iSegment = CModule.ReadByte(stream);
			if (iSegment == 0)
			{
				throw new Exception("Logical Enumerated Data Record must have segment");
			}
			else
			{
				this.oSegment = segments[iSegment - 1];
			}

			this.iOffset = CModule.ReadUInt16(stream);
			this.aData = CModule.ReadBlock(stream, (int)(stream.Length - stream.Position - 1));
		}

		public SegmentDefinition Segment
		{
			get
			{
				return this.oSegment;
			}
		}

		public int Offset
		{
			get
			{
				return this.iOffset;
			}
		}

		public byte[] Data
		{
			get
			{
				return this.aData;
			}
		}
	}
}
