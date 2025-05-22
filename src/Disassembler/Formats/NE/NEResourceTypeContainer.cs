namespace Disassembler.Formats.NE
{
	public class NEResourceTypeContainer
	{
		private NEResourceTypeEnum eType = 0;
		private string sName = "";
		List<NEResource> aResources = new List<NEResource>();

		public NEResourceTypeContainer(int typeID, string name)
		{
			this.eType = (NEResourceTypeEnum)typeID;
			this.sName = name;
		}

		public NEResourceTypeEnum Type
		{
			get { return this.eType; }
		}

		public string Name
		{
			get
			{ return this.sName; }
		}

		public List<NEResource> Resources
		{
			get { return this.aResources; }
		}
	}
}
