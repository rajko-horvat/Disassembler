using System;
using System.Collections.Generic;
using System.IO;

namespace Disassembler.OMF
{
	public class DataRecord
	{
		private SegmentDefinition oSegment = null;
		private int iOffset = 0;
		private byte[] aData = new byte[0];
		private List<Fixup> aFixups = new List<Fixup>();

		public DataRecord(Stream stream, List<SegmentDefinition> segments, bool iterated)
		{
			int iSegment = OBJModule.ReadByte(stream);
			if (iSegment == 0)
			{
				throw new Exception("Data Record must have segment");
			}
			else
			{
				this.oSegment = segments[iSegment - 1];
			}

			this.iOffset = OBJModule.ReadUInt16(stream);

			if (iterated)
			{
				int iLevel = 0;
				this.aData = RecursiveReadBlock(stream, ref iLevel);
			}
			else
			{
				this.aData = OBJModule.ReadBlock(stream, (int)(stream.Length - stream.Position - 1));
			}
		}

		private byte[] RecursiveReadBlock(Stream stream, ref int level)
		{
			List<byte> buffer = new List<byte>();
			int iRepeatCount = OBJModule.ReadUInt16(stream);
			int iBlockCount = OBJModule.ReadUInt16(stream);

			if (iBlockCount == 0)
			{
				int iLength = OBJModule.ReadByte(stream);
				buffer.AddRange(OBJModule.ReadBlock(stream, iLength));
			}
			else
			{
				level++;
				if (level > 100)
					throw new Exception("Too many data block iterations");

				for (int i = 0; i < iBlockCount; i++)
				{
					buffer.AddRange(RecursiveReadBlock(stream, ref level));
				}
			}

			List<byte> buffer1 = new List<byte>();

			for (int i = 0; i < iRepeatCount; i++)
			{
				buffer1.AddRange(buffer);
			}

			return buffer1.ToArray();
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

		public List<Fixup> Fixups
		{
			get
			{
				return this.aFixups;
			}
			set
			{
				this.aFixups = value;
			}
		}
	}
}
