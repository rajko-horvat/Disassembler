namespace Disassembler.Formats.OMF
{
	public class OMFFixup
	{
		private OMFFixupTypeEnum eType = OMFFixupTypeEnum.Undefined;

		private OMFTargetMethodEnum eTargetMethod = OMFTargetMethodEnum.Undefined;
		private OMFFrameMethodEnum eFrameMethod = OMFFrameMethodEnum.Undefined;
		private int iThreadIndex = -1;
		private int iIndex = -1;

		private OMFFixupModeEnum eFixupMode = OMFFixupModeEnum.Undefined;
		private OMFFixupLocationTypeEnum eFixupLocationType = OMFFixupLocationTypeEnum.Undefined;
		private int iDataOffset = 0;
		private int iFrameThreadIndex = -1;
		private int iTargetThreadIndex = -1;
		private int iTargetDisplacement = 0;

		public OMFFixup(Stream stream)
		{
			int iType = OMFOBJModule.ReadByte(stream);
			if ((iType & 0x80) != 0)
			{
				// it's a Fixup subrecord
				this.eType = OMFFixupTypeEnum.Fixup;
				this.eFixupMode = (OMFFixupModeEnum)((iType & 0x40) >> 6);
				this.eFixupLocationType = (OMFFixupLocationTypeEnum)((iType & 0x3c) >> 2);
				this.iDataOffset = ((iType & 0x3) << 8) | OMFOBJModule.ReadByte(stream);

				iType = OMFOBJModule.ReadByte(stream);

				if ((iType & 0x80) != 0)
				{
					this.iFrameThreadIndex = (iType & 0x70) >> 4;
				}
				else
				{
					this.eFrameMethod = (OMFFrameMethodEnum)((iType & 0x70) >> 4);
					if ((int)this.eFrameMethod < 3)
					{
						this.iFrameThreadIndex = OMFOBJModule.ReadByte(stream);
					}
				}

				if ((iType & 0x8) != 0)
				{
					this.iTargetThreadIndex = iType & 0x7;
				}
				else
				{
					this.eTargetMethod = (OMFTargetMethodEnum)(iType & 0x3);
					this.iTargetThreadIndex = OMFOBJModule.ReadByte(stream);
					if ((iType & 0x4) == 0)
					{
						this.iTargetDisplacement = OMFOBJModule.ReadUInt16(stream);
					}
				}
			}
			else
			{
				// it's a Thread subrecord
				if ((iType & 0x40) != 0)
				{
					this.eType = OMFFixupTypeEnum.FrameThread;
					this.eFrameMethod = (OMFFrameMethodEnum)((iType & 0x1c) >> 2);
					this.iThreadIndex = iType & 0x3;

					switch (this.eFrameMethod)
					{
						case OMFFrameMethodEnum.SegDefIndex:
						case OMFFrameMethodEnum.GrpDefIndex:
						case OMFFrameMethodEnum.ExtDefIndex:
							this.iIndex = OMFOBJModule.ReadByte(stream);
							break;
					}
				}
				else
				{
					this.eType = OMFFixupTypeEnum.TargetThread;
					this.eTargetMethod = (OMFTargetMethodEnum)((iType & 0x1c) >> 2);
					this.iThreadIndex = iType & 0x3;
					this.iIndex = OMFOBJModule.ReadByte(stream);
				}
			}
		}

		public OMFFixupTypeEnum Type
		{
			get
			{
				return this.eType;
			}
		}

		public OMFTargetMethodEnum TargetMethod
		{
			get { return this.eTargetMethod; }
		}

		public OMFFrameMethodEnum FrameMethod
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

		public OMFFixupModeEnum FixupMode
		{
			get { return this.eFixupMode; }
		}

		public OMFFixupLocationTypeEnum FixupLocationType
		{
			get { return this.eFixupLocationType; }
		}

		public LocationTypeEnum ToLocationType
		{
			get
			{
				switch (this.eFixupLocationType)
				{
					case OMFFixupLocationTypeEnum.LowOrderByte:
					case OMFFixupLocationTypeEnum.HighOrderByte:
						return LocationTypeEnum.Undefined;

					case OMFFixupLocationTypeEnum.Offset16bit:
					case OMFFixupLocationTypeEnum.Offset16bit_1:
						return LocationTypeEnum.Offset16;

					case OMFFixupLocationTypeEnum.Base16bit:
						return LocationTypeEnum.Segment16;

					case OMFFixupLocationTypeEnum.LongPointer32bit:
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
					case OMFFixupLocationTypeEnum.LowOrderByte:
					case OMFFixupLocationTypeEnum.HighOrderByte:
						return 1;
					case OMFFixupLocationTypeEnum.Offset16bit:
					case OMFFixupLocationTypeEnum.Offset16bit_1:
					case OMFFixupLocationTypeEnum.Base16bit:
						return 2;
					case OMFFixupLocationTypeEnum.LongPointer32bit:
					case OMFFixupLocationTypeEnum.Offset32bit:
					case OMFFixupLocationTypeEnum.Offset32bit_1:
						return 4;
					case OMFFixupLocationTypeEnum.Pointer48bit:
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

		public static int CompareByOffset(OMFFixup item1, OMFFixup item2)
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
