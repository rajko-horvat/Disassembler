using System;
using System.Collections.Generic;
using System.IO;

namespace Disassembler.OMF
{
	public class SegmentGroupDefinition
	{
		private string sName = null;
		private List<int> aSegments = new List<int>();

		public SegmentGroupDefinition(Stream stream, List<string> names)
		{
			this.sName = names[CModule.ReadByte(stream) - 1];
			while (stream.Position < stream.Length - 1)
			{
				byte bType = CModule.ReadByte(stream);
				if (bType != 0xff)
				{
					throw new Exception("Unknown Group Definition Type");
				}
				aSegments.Add(CModule.ReadByte(stream) - 1);
			}
		}

		public string Name
		{
			get
			{
				return this.sName;
			}
		}

		public List<int> Segments
		{
			get
			{
				return this.aSegments;
			}
		}
	}
}
