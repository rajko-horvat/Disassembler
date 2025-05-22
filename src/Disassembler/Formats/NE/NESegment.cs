namespace Disassembler.Formats.NE
{
	public class NESegment
	{
		private byte[] abData = new byte[0];
		private NESegmentFlagsEnum eFlags = NESegmentFlagsEnum.None;
		private int iMinimumSize = -1;
		private List<NERelocation> aRelocations = new List<NERelocation>();
		private string sNamespace = "";

		public NESegment(Stream stream, int sectorSize)
		{
			int iSegmentDataOffset = (int)NEExecutable.ReadUInt16(stream) * sectorSize;
			int iSegmentLength = NEExecutable.ReadUInt16(stream);
			this.eFlags = (NESegmentFlagsEnum)NEExecutable.ReadUInt16(stream);
			int iMinimumAllocation = NEExecutable.ReadUInt16(stream);
			if (iMinimumAllocation == 0)
				iMinimumAllocation = 65536;

			long lCurrentPisition = stream.Position;
			stream.Seek(iSegmentDataOffset, SeekOrigin.Begin);
			this.abData = new byte[iSegmentLength];
			stream.Read(abData, 0, iSegmentLength);
			if ((this.eFlags & NESegmentFlagsEnum.ContainsRelocationData) == NESegmentFlagsEnum.ContainsRelocationData)
			{
				int iRelocationCount = NEExecutable.ReadUInt16(stream);
				for (int i = 0; i < iRelocationCount; i++)
				{
					this.aRelocations.Add(new NERelocation(stream));
				}
			}
			// sort ascending by offset
			this.aRelocations.Sort(NERelocation.CompareByOffset);

			stream.Seek(lCurrentPisition, SeekOrigin.Begin);
		}

		public bool CompareFlag(NESegmentFlagsEnum flag)
		{
			return (this.eFlags & flag) == flag;
		}

		public NESegmentFlagsEnum Flags
		{
			get
			{
				return this.eFlags;
			}
		}

		public string Namespace
		{
			get { return this.sNamespace; }
			set { this.sNamespace = value; }
		}

		public byte[] Data
		{
			get
			{
				return this.abData;
			}
		}

		public int MinimumSize
		{
			get
			{
				return this.iMinimumSize;
			}
		}

		public List<NERelocation> Relocations
		{
			get
			{
				return this.aRelocations;
			}
		}
	}
}
