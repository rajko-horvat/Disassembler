namespace Disassembler.Formats.NE
{
	public class NEResource
	{
		private byte[] aData;
		private int iFlags = 0;
		private int iID = -1;
		private string sName = "";

		private long lPosition = 0;
		public NEResource(long position, byte[] data, int flags, int id, string name)
		{
			this.lPosition = position;

			this.aData = data;
			this.iFlags = flags;
			this.iID = id;
			this.sName = name;
		}

		public long Position
		{
			get { return this.lPosition; }
		}

		public byte[] Data
		{
			get { return this.aData; }
		}

		public int Flags
		{
			get { return this.iFlags; }
		}

		public int ID
		{
			get { return this.iID; }
		}

		public string Name
		{
			get { return this.sName; }
		}
	}
}
