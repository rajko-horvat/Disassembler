using System;
using System.Collections.Generic;
using System.IO;

namespace Disassembler.OMF
{
	public enum SegmentAlignmentEnum
	{
		Absolute = 0,
		RelocatableAlignByte = 1,
		RelocatableAlignWord = 2,
		RelocatableAlignParagraph = 3,
		RelocatableAlignPage = 4,
		RelocatableAlignDWord = 5,
		NotSupported = 6,
		NotDefined = 7
	}

	public enum SegmentCombineEnum
	{
		Private = 0,
		Reserved = 1,
		Public = 2,
		Stack = 5,
		Common = 6
	}

	public class SegmentDefinition
	{
		private SegmentAlignmentEnum eAlignment = SegmentAlignmentEnum.NotDefined;
		private SegmentCombineEnum eCombine = SegmentCombineEnum.Reserved;
		private bool bBig = false;
		private bool bPBit = false;
		private int iFrameNumber = 0;
		private int iOffset = 0;
		private int iLength = 0;
		private string sName = null;
		private string sClassName = null;
		private string sOverlayName = null;
		private List<Fixup> aFixups = new List<Fixup>();

		public SegmentDefinition(Stream stream, List<string> names)
		{
			byte bAttributes = CModule.ReadByte(stream);
			byte bAlign = (byte)((bAttributes & 0xe0) >> 5);
			byte bComb = (byte)((bAttributes & 0x1c) >> 2);
			this.bBig = (bAttributes & 0x2) != 0;
			this.bPBit = (bAttributes & 1) != 0;

			this.eAlignment = (SegmentAlignmentEnum)bAlign;
			if (this.eAlignment == SegmentAlignmentEnum.Absolute)
			{
				// read additional Frame number and Offset
				this.iFrameNumber = CModule.ReadUInt16(stream);
				this.iOffset = CModule.ReadByte(stream);
			}
			switch (bComb)
			{
				case 0:
					this.eCombine = SegmentCombineEnum.Private;
					break;
				case 1:
					this.eCombine = SegmentCombineEnum.Reserved;
					break;
				case 2:
					this.eCombine = SegmentCombineEnum.Public;
					break;
				case 3:
					this.eCombine = SegmentCombineEnum.Reserved;
					break;
				case 4:
					this.eCombine = SegmentCombineEnum.Public;
					break;
				case 5:
					this.eCombine = SegmentCombineEnum.Stack;
					break;
				case 6:
					this.eCombine = SegmentCombineEnum.Common;
					break;
				case 7:
					this.eCombine = SegmentCombineEnum.Public;
					break;
			}
			this.iLength = CModule.ReadUInt16(stream);
			int iNameIndex = CModule.ReadByte(stream);
			int iClassNameIndex = CModule.ReadByte(stream);
			int iOverlayIndex = CModule.ReadByte(stream);

			if (iNameIndex > 0)
			{
				this.sName = names[iNameIndex - 1];
			}
			if (iClassNameIndex > 0)
			{
				this.sClassName = names[iClassNameIndex - 1];
			}
			if (iOverlayIndex > 0)
			{
				this.sOverlayName = names[iOverlayIndex - 1];
			}
			if (stream.Position >= stream.Length)
			{
				throw new Exception("Segment Definition Record");
			}
		}

		public SegmentAlignmentEnum Alignment
		{
			get
			{
				return this.eAlignment;
			}
		}

		public SegmentCombineEnum Combine
		{
			get
			{
				return this.eCombine;
			}
		}

		public bool Big
		{
			get
			{
				return this.bBig;
			}
		}

		public bool PBit
		{
			get
			{
				return this.bPBit;
			}
		}

		public int FrameNumber
		{
			get
			{
				return this.iFrameNumber;
			}
		}

		public int Offset
		{
			get
			{
				return this.iOffset;
			}
		}

		public string Name
		{
			get
			{
				return this.sName;
			}
		}

		public string ClassName
		{
			get
			{
				return this.sClassName;
			}
		}
		public string OverlayName
		{
			get
			{
				return this.sOverlayName;
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
