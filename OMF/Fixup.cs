using Disassembler.NE;
using System;
using System.IO;

namespace Disassembler.OMF
{
	public enum FixupTypeEnum
	{
		Undefined = -1,
		TargetThread = 0,
		FrameThread = 1,
		Fixup = 2
	}

	public enum TargetMethodEnum
	{
		Undefined = -1,
		SegDefIndex = 0,
		GrpDefIndex = 1,
		ExtDefIndex = 2,
		NotSupported3 = 3,
		SegDefIndexNoDisplacement = 4,
		GrpDefIndexNoDisplacement = 5,
		ExtDefIndexNoDisplacement = 6,
		NotSupported7 = 7
	}

	public enum FrameMethodEnum
	{
		Undefined = -1,
		SegDefIndex = 0,
		GrpDefIndex = 1,
		ExtDefIndex = 2,
		NotSupported3 = 3,
		PreviousDataRecordSegmentIndex = 4,
		TargetIndex = 5,
		NotSupported6 = 6,
		NotSupported7 = 7
	}

	public enum FixupModeEnum
	{
		Undefined = -1,
		SelfRelative = 0,
		SegmentRelative = 1
	}

	public enum FixupLocationTypeEnum
	{
		Undefined = -1,
		LowOrderByte = 0,
		Offset16bit = 1,
		Base16bit = 2,
		LongPointer32bit = 3,
		HighOrderByte = 4,
		Offset16bit_1 = 5,
		NotSupported6 = 6,
		NotSupported7 = 7,
		NotSupported8 = 8,
		Offset32bit = 9,
		NotSupported10 = 10,
		Pointer48bit = 11,
		NotSupported12 = 12,
		Offset32bit_1 = 13,
		NotSupported14 = 14,
		NotSupported15 = 15
	}

	public class Fixup
	{
		private FixupTypeEnum eType = FixupTypeEnum.Undefined;

		private TargetMethodEnum eTargetMethod = TargetMethodEnum.Undefined;
		private FrameMethodEnum eFrameMethod = FrameMethodEnum.Undefined;
		private int iThreadIndex = -1;
		private int iIndex = -1;

		private FixupModeEnum eFixupMode = FixupModeEnum.Undefined;
		private FixupLocationTypeEnum eFixupLocationType = FixupLocationTypeEnum.Undefined;
		private int iDataOffset = 0;
		private int iFrameThreadIndex = -1;
		private int iTargetThreadIndex = -1;
		private int iTargetDisplacement = 0;

		public Fixup(Stream stream)
		{
			int iType = OBJModule.ReadByte(stream);
			if ((iType & 0x80) != 0)
			{
				// it's a Fixup subrecord
				this.eType = FixupTypeEnum.Fixup;
				this.eFixupMode = (FixupModeEnum)((iType & 0x40) >> 6);
				this.eFixupLocationType = (FixupLocationTypeEnum)((iType & 0x3c) >> 2);
				this.iDataOffset = ((iType & 0x3) << 8) | OBJModule.ReadByte(stream);

				iType = OBJModule.ReadByte(stream);

				if ((iType & 0x80) != 0)
				{
					this.iFrameThreadIndex = (iType & 0x70) >> 4;
				}
				else
				{
					this.eFrameMethod = (FrameMethodEnum)((iType & 0x70) >> 4);
					if ((int)this.eFrameMethod < 3)
					{
						this.iFrameThreadIndex = OBJModule.ReadByte(stream);
					}
				}

				if ((iType & 0x8) != 0)
				{
					this.iTargetThreadIndex = iType & 0x7;
				}
				else
				{
					this.eTargetMethod = (TargetMethodEnum)(iType & 0x3);
					this.iTargetThreadIndex = OBJModule.ReadByte(stream);
					if ((iType & 0x4) == 0)
					{
						this.iTargetDisplacement = OBJModule.ReadUInt16(stream);
					}
				}
			}
			else
			{
				// it's a Thread subrecord
				if ((iType & 0x40) != 0)
				{
					this.eType = FixupTypeEnum.FrameThread;
					this.eFrameMethod = (FrameMethodEnum)((iType & 0x1c) >> 2);
					this.iThreadIndex = iType & 0x3;

					switch (this.eFrameMethod)
					{
						case FrameMethodEnum.SegDefIndex:
						case FrameMethodEnum.GrpDefIndex:
						case FrameMethodEnum.ExtDefIndex:
							this.iIndex = OBJModule.ReadByte(stream);
							break;
					}
				}
				else
				{
					this.eType = FixupTypeEnum.TargetThread;
					this.eTargetMethod = (TargetMethodEnum)((iType & 0x1c) >> 2);
					this.iThreadIndex = iType & 0x3;
					this.iIndex = OBJModule.ReadByte(stream);
				}
			}
		}

		public FixupTypeEnum Type
		{
			get
			{
				return this.eType;
			}
		}

		public TargetMethodEnum TargetMethod
		{
			get { return this.eTargetMethod; }
		}

		public FrameMethodEnum FrameMethod
		{
			get { return this.eFrameMethod; }
		}

		public int ThreadIndex
		{
			get { return this.iThreadIndex; }
		}

		public int Index
		{
			get { return this.iIndex; }
		}

		public FixupModeEnum FixupMode
		{
			get { return this.eFixupMode; }
		}

		public FixupLocationTypeEnum FixupLocationType
		{
			get { return this.eFixupLocationType; }
		}

		public LocationTypeEnum ToLocationType
		{
			get
			{
				switch (this.eFixupLocationType)
				{
					case FixupLocationTypeEnum.LowOrderByte:
					case FixupLocationTypeEnum.HighOrderByte:
						return LocationTypeEnum.Undefined;

					case FixupLocationTypeEnum.Offset16bit:
					case FixupLocationTypeEnum.Offset16bit_1:
						return LocationTypeEnum.Offset16;

					case FixupLocationTypeEnum.Base16bit:
						return LocationTypeEnum.Segment16;

					case FixupLocationTypeEnum.LongPointer32bit:
						return LocationTypeEnum.SegmentOffset32;

					default:
						return LocationTypeEnum.Undefined;
				}
			}
		}

		public int Length
		{
			get
			{
				switch (this.eFixupLocationType)
				{
					case FixupLocationTypeEnum.LowOrderByte:
					case FixupLocationTypeEnum.HighOrderByte:
						return 1;
					case FixupLocationTypeEnum.Offset16bit:
					case FixupLocationTypeEnum.Offset16bit_1:
					case FixupLocationTypeEnum.Base16bit:
						return 2;
					case FixupLocationTypeEnum.LongPointer32bit:
					case FixupLocationTypeEnum.Offset32bit:
					case FixupLocationTypeEnum.Offset32bit_1:
						return 4;
					case FixupLocationTypeEnum.Pointer48bit:
						return 6;
					default:
						return 0;
				}
			}
		}

		public int DataOffset
		{
			get { return this.iDataOffset; }
		}

		public int FrameThreadIndex
		{
			get
			{
				return this.iFrameThreadIndex;
			}
		}

		public int TargetThreadIndex
		{
			get
			{
				return this.iTargetThreadIndex;
			}
		}

		public int TargetDisplacement
		{
			get
			{
				return this.iTargetDisplacement;
			}
		}

		public static int CompareByOffset(Fixup item1, Fixup item2)
		{
			if (item1 == null)
			{
				if (item2 == null)
				{
					// If x is null and y is null, they're
					// equal.
					return 0;
				}
				// If x is null and y is not null, y
				// is greater.
				return -1;
			}
			else
			{
				// If x is not null...
				if (item2 == null)
				// ...and y is null, x is greater.
				{
					return 1;
				}
				else
				{
					if (item1.DataOffset == item2.DataOffset)
						return 0;

					if (item1.DataOffset < item2.DataOffset)
						return -1;

					return 1;
				}
			}
		}
	}
}
