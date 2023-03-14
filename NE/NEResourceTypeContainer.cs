using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Disassembler.NE
{
	public enum ResourceTypeEnum
	{
		RT_CURSOR = 1,
		RT_BITMAP = 2,
		RT_ICON = 3,
		RT_MENU = 4,
		RT_DIALOG = 5,
		RT_STRING = 6,
		RT_FONTDIR = 7,
		RT_FONT = 8,
		RT_ACCELERATOR = 9,
		RT_RCDATA = 10,
		RT_GROUP_CURSOR = 12,
		RT_GROUP_ICON = 14
	}

	public class NEResourceTypeContainer
	{
		private ResourceTypeEnum eType = 0;
		private string sName = "";
		List<NEResource> aResources = new List<NEResource>();

		public NEResourceTypeContainer(int typeID, string name)
		{
			this.eType = (ResourceTypeEnum)typeID;
			this.sName = name;
		}

		public ResourceTypeEnum Type
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
