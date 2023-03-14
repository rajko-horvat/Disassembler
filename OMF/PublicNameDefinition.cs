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
		private List<KeyValuePair<string, int>> aPublicNames = new List<KeyValuePair<string, int>>();

		public PublicNameDefinition(Stream stream, List<SegmentDefinition> segments, List<SegmentGroupDefinition> groups)
		{
			int iGroup = CModule.ReadByte(stream);
			int iSegment = CModule.ReadByte(stream);
			if (iSegment == 0)
			{
				// read Base Frame, which is ignored anyway
				this.iBaseFrame = CModule.ReadUInt16(stream);
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
				string sName = CModule.ReadString(stream);
				int iOffset = CModule.ReadUInt16(stream);
				// Type index is ignored
				CModule.ReadByte(stream);
				aPublicNames.Add(new KeyValuePair<string, int>(sName, iOffset));
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

		public List<KeyValuePair<string, int>> PublicNames
		{
			get
			{
				return this.aPublicNames;
			}
		}
	}
}
