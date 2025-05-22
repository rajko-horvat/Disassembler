using Disassembler.Formats.OMF;

namespace Disassembler
{
	public class ModuleMatch
	{
		private OMFOBJModule oModule;
		private uint uiLinearAddress;
		private int iLength;

		public ModuleMatch(OMFOBJModule module, uint address, int length)
		{
			this.oModule = module;
			this.uiLinearAddress = address;
			this.iLength = length;
		}

		public bool IsInRange(uint address)
		{
			return this.uiLinearAddress >= address || address < (uint)(this.uiLinearAddress + this.iLength);
		}
		
		public OMFOBJModule Module
		{
			get
			{
				return this.oModule;
			}
		}

		public uint LinearAddress
		{
			get { return this.uiLinearAddress; }
		}

		public int Length
		{
			get
			{
				return this.iLength;
			}
		}
	}
}
