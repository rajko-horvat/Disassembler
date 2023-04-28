using IRB.Collections.Generic;
using System;
using System.Collections.Generic;
using System.IO;

namespace Disassembler.OMF
{
	public class PublicNameDefinition
	{
		private SegmentGroupDefinition oSegmentGroup = null;
		private SegmentDefinition oSegment = null;
		private int iBaseFrame = 0;
		private List<BKeyValuePair<string, int>> aPublicNames = new List<BKeyValuePair<string, int>>();

		public PublicNameDefinition(Stream stream, List<SegmentDefinition> segments, List<SegmentGroupDefinition> groups)
		{
			int iGroup = OBJModule.ReadByte(stream);
			int iSegment = OBJModule.ReadByte(stream);
			if (iSegment == 0)
			{
				// read Base Frame, which is ignored anyway
				this.iBaseFrame = OBJModule.ReadUInt16(stream);
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
				string sName = OBJModule.ReadString(stream);
				int iOffset = OBJModule.ReadUInt16(stream);
				// Type index is ignored
				OBJModule.ReadByte(stream);
				aPublicNames.Add(new BKeyValuePair<string, int>(sName, iOffset));
			}
		}

		public SegmentGroupDefinition SegmentGroup
		{
			get
			{
				return this.oSegmentGroup;
			}
		}

		public SegmentDefinition Segment
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
