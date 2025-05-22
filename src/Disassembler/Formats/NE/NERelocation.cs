namespace Disassembler.Formats.NE
{
	public class NERelocation
	{
		private LocationTypeEnum eLocationType = LocationTypeEnum.Undefined;
		private NERelocationTypeEnum eRelocationType = NERelocationTypeEnum.InternalReference;
		private int iOffset=0;
		private int iParameter1 = 0;
		private int iParameter2 = 0;

		public NERelocation(Stream stream)
		{
			int iLocType = NEExecutable.ReadByte(stream);
			switch (iLocType)
			{
				case 2:
					this.eLocationType = LocationTypeEnum.Segment16;
					break;
				case 3:
					this.eLocationType = LocationTypeEnum.SegmentOffset32;
					break;
				case 5:
					this.eLocationType = LocationTypeEnum.Offset16;
					break;
				default:
					throw new Exception("Undefined Location type");
			}

			int iType = NEExecutable.ReadByte(stream);
			switch (iType)
			{
				case 0:
					this.eRelocationType = NERelocationTypeEnum.InternalReference;
					break;
				case 1:
					this.eRelocationType = NERelocationTypeEnum.ImportedOrdinal;
					break;
				case 2:
					this.eRelocationType = NERelocationTypeEnum.ImportedName;
					break;
				case 3:
					this.eRelocationType = NERelocationTypeEnum.OSFixup;
					break;
				case 4:
					this.eRelocationType = NERelocationTypeEnum.Additive;
					break;
				case 7:
					this.eRelocationType = NERelocationTypeEnum.FPFixup;
					break;
				default:
					throw new Exception("Undefined relocation type");
			}

			this.iOffset = NEExecutable.ReadUInt16(stream);
			this.iParameter1 = NEExecutable.ReadUInt16(stream);
			this.iParameter2 = NEExecutable.ReadUInt16(stream);
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

		public NERelocationTypeEnum RelocationType
		{
			get
			{
				return this.eRelocationType;
			}
		}

		public int Offset
		{
			get
			{
				return this.iOffset;
			}
		}

		public int Parameter1
		{
			get
			{
				return this.iParameter1;
			}
		}

		public int Parameter2
		{
			get
			{
				return this.iParameter2;
			}
		}

		public static int CompareByOffset(NERelocation item1, NERelocation item2)
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
