namespace Disassembler.Formats.OMF
{
	public class OMFLogicalData
	{
		private OMFSegmentDefinition? oSegment = null;
		private int iOffset = 0;
		private byte[] aData = new byte[] { };

		public OMFLogicalData(Stream stream, List<OMFSegmentDefinition> segments)
		{
			int iSegment = OMFOBJModule.ReadByte(stream);
			if (iSegment == 0)
			{
				throw new Exception("Logical Enumerated Data Record must have segment");
			}
			else
			{
				this.oSegment = segments[iSegment - 1];
			}

			this.iOffset = OMFOBJModule.ReadUInt16(stream);
			this.aData = OMFOBJModule.ReadBlock(stream, (int)(stream.Length - stream.Position - 1));
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
	}
}
