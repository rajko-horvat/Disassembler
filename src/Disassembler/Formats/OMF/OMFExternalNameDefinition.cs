namespace Disassembler.Formats.OMF
{
	public class OMFExternalNameDefinition
	{
		private string sName = "";
		private int iTypeIndex = -1;

		public OMFExternalNameDefinition(Stream stream)
		{
			this.sName = OMFOBJModule.ReadString(stream);
			this.iTypeIndex = OMFOBJModule.ReadByte(stream);
		}

		public string Name
		{
			get
			{
				return this.sName;
			}
		}

		public int TypeIndex
		{
			get
			{
				return this.iTypeIndex;
			}
		}
	}
}
