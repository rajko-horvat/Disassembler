using System.Drawing;

namespace Disassembler.Formats.OMF
{
	public class OMFDataRecord
	{
		private OMFSegmentDefinition? oSegment = null;
		private int iOffset = 0;
		private byte[] aData = new byte[0];
		private List<OMFFixup> aFixups = new List<OMFFixup>();

		public OMFDataRecord(Stream stream, List<OMFSegmentDefinition> segments, bool iterated)
		{
			int iSegment = OMFOBJModule.ReadByte(stream);
			if (iSegment == 0)
			{
				throw new Exception("Data Record must have segment");
			}
			else
			{
				this.oSegment = segments[iSegment - 1];
			}

			this.iOffset = OMFOBJModule.ReadUInt16(stream);

			if (iterated)
			{
				int iLevel = 0;
				this.aData = RecursiveReadBlock(stream, ref iLevel);
			}
			else
			{
				this.aData = OMFOBJModule.ReadBlock(stream, (int)(stream.Length - stream.Position - 1));
			}
		}

		private byte[] RecursiveReadBlock(Stream stream, ref int level)
		{
			List<byte> buffer = new List<byte>();
			int iRepeatCount = OMFOBJModule.ReadUInt16(stream);
			int iBlockCount = OMFOBJModule.ReadUInt16(stream);

			if (iBlockCount == 0)
			{
				int iLength = OMFOBJModule.ReadByte(stream);
				buffer.AddRange(OMFOBJModule.ReadBlock(stream, iLength));
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

		public void IncreaseDataSize(int newSize)
		{
			if (newSize > this.aData.Length)
			{
				Array.Resize<byte>(ref this.aData, newSize);
			}
		}

		public OMFSegmentDefinition? Segment
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

		public List<OMFFixup> Fixups
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
