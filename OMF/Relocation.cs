using Disassembler.NE;
using System;
using System.IO;

namespace Disassembler.OMF
{
	[Flags]
	public enum FixupItemTypeEnum : int
	{
		Thread = 1,
		Fixup = 0,
		Target = 2,
		TargetThreadLookup = 0,
		Frame = 4,
		FrameThreadLookup = 0,
		SegmentRelativeFixup = 8,
		SelfRelativeFixup = 0
	}

	public enum FixupTargetEnum
	{
		SegDefIndex = 0,
		GrpDefIndex = 1,
		ExtDefIndex = 2,
		NotSupported3 = 3,
		SegDefIndexNoDisplacement = 4,
		GrpDefIndexNoDisplacement = 5,
		ExtDefIndexNoDisplacement = 6,
		NotSupported7 = 7
	}

	public enum FixupFrameEnum
	{
		FrameBySegDefIndex = 0,
		FrameByGrpDefIndex = 1,
		FrameByExtDefIndex = 2,
		NotSupported3 = 3,
		FrameByPreviousDataRecordSegmentIndex = 4,
		FrameByTarget = 5,
		NotSupported6 = 6,
		NotSupported7 = 7
	}

	public class Fixup
	{
		private FixupItemTypeEnum eType = FixupItemTypeEnum.Fixup;

		private LocationTypeEnum eLocationType = LocationTypeEnum.Undefined;
		private int iOffset = 0;
		private int iTargetDisplacement = -1;

		private FixupTargetEnum eTargetMethod = FixupTargetEnum.SegDefIndex;
		private int iTargetIndex = -1;
		private int iTargetThread = -1;

		private FixupFrameEnum eFrameMethod = FixupFrameEnum.FrameBySegDefIndex;
		private int iFrameIndex = -1;
		private int iFrameThread = -1;

		public Fixup(Stream stream)
		{
			int iType = CModule.ReadByte(stream);
			if ((iType & 0x80) != 0)
			{
				// it's a Fixup subrecord
				this.eType = FixupItemTypeEnum.Fixup;
				if ((iType & 0x40) != 0)
				{
					this.eType |= FixupItemTypeEnum.SegmentRelativeFixup;
				}
				else
				{
					this.eType |= FixupItemTypeEnum.SelfRelativeFixup;
				}
				switch ((iType & 0x3c) >> 2)
				{
					case 1:
					case 5:
						this.eLocationType = LocationTypeEnum.Offset16;
						break;
					case 2:
						this.eLocationType = LocationTypeEnum.Segment16;
						break;
					case 3:
						this.eLocationType = LocationTypeEnum.SegmentOffset32;
						break;
					default:
						throw new Exception("Undefined Location type");
				}

				this.iOffset = ((iType & 0x3) << 8) | CModule.ReadByte(stream);

				iType = CModule.ReadByte(stream);

				if ((iType & 0x80) != 0)
				{
					this.iFrameThread = (iType & 0x30) >> 4;
					this.eType |= FixupItemTypeEnum.FrameThreadLookup;
				}
				else
				{
					this.eFrameMethod = (FixupFrameEnum)((iType & 0x70) >> 4);
					if ((int)this.eFrameMethod < 3)
					{
						this.iFrameIndex = CModule.ReadByte(stream);
					}
					this.eType |= FixupItemTypeEnum.Frame;
				}

				if ((iType & 0x8) != 0)
				{
					this.iTargetThread = iType & 0x3;
					if ((iType & 0x4) == 0)
					{
						this.iTargetDisplacement = CModule.ReadUInt16(stream);
					}
					this.eType |= FixupItemTypeEnum.TargetThreadLookup;
				}
				else
				{
					this.eTargetMethod = (FixupTargetEnum)(iType & 0x3);
					if ((int)this.eTargetMethod < 3)
					{
						this.iTargetIndex = CModule.ReadByte(stream);
					}
					this.eType |= FixupItemTypeEnum.Target;
				}
			}
			else
			{
				// it's a thread subrecord
				this.eType = FixupItemTypeEnum.Thread;
				if ((iType & 0x40) != 0)
				{
					this.eType |= FixupItemTypeEnum.Frame;
					this.eFrameMethod = (FixupFrameEnum)((iType & 0x1c) >> 2);
					if ((int)this.eFrameMethod < 3)
					{
						this.iFrameIndex = CModule.ReadByte(stream);
					}
					this.iFrameThread = iType & 0x3;
				}
				else
				{
					this.eType |= FixupItemTypeEnum.Target;
					this.eTargetMethod = (FixupTargetEnum)((iType & 0x1c) >> 2);
					if ((int)this.eTargetMethod < 3)
					{
						this.iTargetIndex = CModule.ReadByte(stream);
					}
					this.iTargetThread = iType & 0x3;
				}
			}
		}

		public bool CompareType(FixupItemTypeEnum type)
		{
			return (this.eType & type) == type;
		}

		public FixupItemTypeEnum Type
		{
			get
			{
				return this.eType;
			}
		}

		public LocationTypeEnum LocationType
		{
			get
			{
				return this.eLocationType;
			}
		}

		public int Length
		{
			get
			{
				switch (this.eLocationType)
				{
					case LocationTypeEnum.Offset16:
					case LocationTypeEnum.Segment16:
						return 2;
					case LocationTypeEnum.SegmentOffset32:
						return 4;
					default:
						return -1;					
				}
			}
		}

		public int Offset
		{
			get
			{
				return this.iOffset;
			}
		}

		public FixupTargetEnum TargetMethod
		{
			get
			{
				return this.eTargetMethod;
			}
		}

		public int TargetThread
		{
			get
			{
				return this.iTargetThread;
			}
		}

		public bool HasTargetIndex
		{
			get
			{
				return (int)this.eTargetMethod < 3;
			}
		}

		public int TargetIndex
		{
			get
			{
				return this.iTargetIndex;
			}
		}

		public bool HasTargetDisplacement
		{
			get
			{
				return this.iTargetDisplacement >= 0;
			}
		}

		public int TargetDisplacement
		{
			get
			{
				return this.iTargetDisplacement;
			}
		}

		public FixupFrameEnum FrameMethod
		{
			get
			{
				return this.eFrameMethod;
			}
		}

		public int FrameThread
		{
			get
			{
				return this.iFrameThread;
			}
		}

		public bool HasFrameIndex
		{
			get
			{
				return (int)this.eFrameMethod < 3;
			}
		}

		public int FrameIndex
		{
			get
			{
				return this.iFrameIndex;
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
					if (item1.Offset == item2.Offset)
						return 0;

					if (item1.Offset < item2.Offset)
						return -1;

					return 1;
				}
			}
		}
	}
}
