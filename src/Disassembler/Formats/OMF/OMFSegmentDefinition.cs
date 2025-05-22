namespace Disassembler.Formats.OMF
{

	public class OMFSegmentDefinition
	{
		private OMFSegmentAlignmentEnum eAlignment = OMFSegmentAlignmentEnum.NotDefined;
		private OMFSegmentCombineEnum eCombine = OMFSegmentCombineEnum.Reserved;
		private bool bBig = false;
		private bool bPBit = false;
		private int iFrameNumber = 0;
		private int iOffset = 0;
		private int iLength = 0;
		private string sName = "";
		private string sClassName = "";
		private string sOverlayName = "";

		public OMFSegmentDefinition(Stream stream, List<string> names)
		{
			byte bAttributes = OMFOBJModule.ReadByte(stream);
			byte bAlign = (byte)((bAttributes & 0xe0) >> 5);
			byte bComb = (byte)((bAttributes & 0x1c) >> 2);
			this.bBig = (bAttributes & 0x2) != 0;
			this.bPBit = (bAttributes & 1) != 0;

			this.eAlignment = (OMFSegmentAlignmentEnum)bAlign;
			if (this.eAlignment == OMFSegmentAlignmentEnum.Absolute)
			{
				// read additional Frame number and Offset
				this.iFrameNumber = OMFOBJModule.ReadUInt16(stream);
				this.iOffset = OMFOBJModule.ReadByte(stream);
			}
			switch (bComb)
			{
				case 0:
					this.eCombine = OMFSegmentCombineEnum.Private;
					break;
				case 1:
					this.eCombine = OMFSegmentCombineEnum.Reserved;
					break;
				case 2:
					this.eCombine = OMFSegmentCombineEnum.Public;
					break;
				case 3:
					this.eCombine = OMFSegmentCombineEnum.Reserved;
					break;
				case 4:
					this.eCombine = OMFSegmentCombineEnum.Public;
					break;
				case 5:
					this.eCombine = OMFSegmentCombineEnum.Stack;
					break;
				case 6:
					this.eCombine = OMFSegmentCombineEnum.Common;
					break;
				case 7:
					this.eCombine = OMFSegmentCombineEnum.Public;
					break;
			}
			this.iLength = OMFOBJModule.ReadUInt16(stream);
			int iNameIndex = OMFOBJModule.ReadByte(stream);
			int iClassNameIndex = OMFOBJModule.ReadByte(stream);
			int iOverlayIndex = OMFOBJModule.ReadByte(stream);

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

		public OMFSegmentAlignmentEnum Alignment
		{
			get
			{
				return this.eAlignment;
			}
		}

		public OMFSegmentCombineEnum Combine
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
	}
}
