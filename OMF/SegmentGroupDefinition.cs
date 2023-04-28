using System;
using System.Collections.Generic;
using System.IO;

namespace Disassembler.OMF
{
	public class SegmentGroupDefinition
	{
		private string sName = null;
		private List<int> aSegmentIndexes = new List<int>();

		public SegmentGroupDefinition(Stream stream, List<string> names)
		{
			this.sName = names[OBJModule.ReadByte(stream) - 1];
			while (stream.Position < stream.Length - 1)
			{
				byte bType = OBJModule.ReadByte(stream);
				if (bType != 0xff)
				{
					throw new Exception("Unknown Group Definition Type");
				}
				aSegmentIndexes.Add(OBJModule.ReadByte(stream) - 1);
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
