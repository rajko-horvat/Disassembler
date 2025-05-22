using IRB.Collections.Generic;

namespace Disassembler.Formats.OMF
{
	public class OMFPublicNameDefinition
	{
		private OMFSegmentGroupDefinition? oSegmentGroup = null;
		private OMFSegmentDefinition? oSegment = null;
		private int iBaseFrame = 0;
		private List<BKeyValuePair<string, int>> aPublicNames = new List<BKeyValuePair<string, int>>();

		public OMFPublicNameDefinition(Stream stream, List<OMFSegmentDefinition> segments, List<OMFSegmentGroupDefinition> groups)
		{
			int iGroup = OMFOBJModule.ReadByte(stream);
			int iSegment = OMFOBJModule.ReadByte(stream);

			if (iSegment == 0)
			{
				// read Base Frame, which is ignored anyway
				this.iBaseFrame = OMFOBJModule.ReadUInt16(stream);
			}
			else
			{
				this.oSegment = segments[iSegment - 1];
			}

			if (iGroup > 0)
			{
				this.oSegmentGroup = groups[iGroup - 1];
			}

			while (stream.Position < stream.Length - 1)
			{
				string sName = OMFOBJModule.ReadString(stream);
				int iOffset = OMFOBJModule.ReadUInt16(stream);
				// Type index is ignored
				OMFOBJModule.ReadByte(stream);
				aPublicNames.Add(new BKeyValuePair<string, int>(sName, iOffset));
			}
		}

		public OMFSegmentGroupDefinition? SegmentGroup
		{
			get
			{
				return this.oSegmentGroup;
			}
		}

		public OMFSegmentDefinition? Segment
		{
			get
			{
				return this.oSegment;
			}
		}

		public int BaseFrame
		{
			get
			{
				return this.iBaseFrame;
			}
		}

		public List<BKeyValuePair<string, int>> PublicNames
		{
			get
			{
				return this.aPublicNames;
			}
		}
	}
}
