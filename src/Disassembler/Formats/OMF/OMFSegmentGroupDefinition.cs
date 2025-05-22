namespace Disassembler.Formats.OMF
{
	public class OMFSegmentGroupDefinition
	{
		private string sName = "";
		private List<int> aSegmentIndexes = new List<int>();

		public OMFSegmentGroupDefinition(Stream stream, List<string> names)
		{
			this.sName = names[OMFOBJModule.ReadByte(stream) - 1];
			while (stream.Position < stream.Length - 1)
			{
				byte bType = OMFOBJModule.ReadByte(stream);
				if (bType != 0xff)
				{
					throw new Exception("Unknown Group Definition Type");
				}
				aSegmentIndexes.Add(OMFOBJModule.ReadByte(stream) - 1);
			}
		}

		public string Name
		{
			get
			{
				return this.sName;
			}
		}

		public List<int> SegmentIndexes
		{
			get
			{
				return this.aSegmentIndexes;
			}
		}
	}
}
