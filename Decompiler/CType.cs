using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace Disassembler.Decompiler
{
	/// <summary>
	/// Basic types in C
	/// We don't care for constants (const) here
	/// </summary>
	public enum CTypeEnum
	{
		Undefined,
		UInt8,
		Int8,
		UInt16,
		Int16,
		UInt32,
		Int32,
		Double,
		Inherited,
		Array,
		Struct,
		VariableParameters
	}

	[Serializable]
	public class CType
	{
		private string sName = null;
		private CTypeEnum eType = CTypeEnum.Undefined;
		private CType oBaseType = null;

		// only valid for struct types
		private List<CType> aMembers = new List<CType>();

		// arrays
		private int iArraySize = 0;

		public CType()
		{ }

		public CType(CTypeEnum type, string name)
		{
			this.sName = name;
			this.eType = type;
		}

		public CType(CType baseType, string name)
		{
			this.sName = name;
			this.eType = CTypeEnum.Inherited;
			this.oBaseType = baseType;
		}

		public CType(CType baseType, int size, string name)
		{
			this.sName = name;
			this.eType = CTypeEnum.Array;
			this.oBaseType = baseType;
			this.iArraySize = size;
		}

		public CType(CTypeEnum type, CType baseType, string name)
		{
			this.sName = name;
			this.eType = type;
			this.oBaseType = baseType;
		}

		public string Name
		{
			get
			{
				return this.sName;
			}
			set
			{
				this.sName = value;
			}
		}

		public CTypeEnum Type
		{
			get
			{
				return this.eType;
			}
			set
			{
				this.eType = value;
			}
		}

		/// <summary>
		/// Specifies the type pointed by the ObjectPtr
		/// </summary>
		public CType BaseType
		{
			get
			{
				return this.oBaseType;
			}
			set
			{
				this.oBaseType = value;
			}
		}

		/// <summary>
		/// Defines the members of the struct
		/// </summary>
		public List<CType> Members
		{
			get
			{
				return this.aMembers;
			}
		}

		public int ArraySize
		{
			get { return this.iArraySize; }
			set { this.iArraySize = value; }
		}

		[XmlIgnore]
		public int Size
		{
			get
			{
				int iSize = 0;

				switch (this.eType)
				{
					case CTypeEnum.Undefined:
						break;
					case CTypeEnum.UInt8:
					case CTypeEnum.Int8:
						iSize += 1;
						break;
					case CTypeEnum.UInt16:
					case CTypeEnum.Int16:
						iSize += 2;
						break;
					case CTypeEnum.UInt32:
					case CTypeEnum.Int32:
						iSize += 4;
						break;
					case CTypeEnum.Double:
						iSize += 8;
						break;
					case CTypeEnum.Struct:
						for (int i = 0; i < this.aMembers.Count; i++)
						{
							iSize += this.aMembers[i].Size;
						}
						break;
					case CTypeEnum.Inherited:
						iSize = this.oBaseType.Size;
						break;
					case CTypeEnum.Array:
						iSize = this.oBaseType.Size * this.iArraySize;
						break;
					case CTypeEnum.VariableParameters:
						// it's just a flag to specify possible additional parameters, the exact count id determined by call ..., add sp... instructions
						//Console.WriteLine("Variable parameters not yet implemented");
						break;
				}

				return iSize;
			}
		}

		// define basic types, we don't care for constants (const) here
		public static CType Void = new CType(CTypeEnum.Undefined, "void");
		public static CType Byte = new CType(CTypeEnum.UInt8, "unsigned char");
		public static CType Char = new CType(CTypeEnum.Int8, "char");
		public static CType Word = new CType(CTypeEnum.UInt16, "word");
		public static CType UInt = new CType(CTypeEnum.UInt16, "unsigned int");
		public static CType Int = new CType(CTypeEnum.Int16, "int");
		public static CType DWord = new CType(CTypeEnum.UInt32, "unsigned long");
		public static CType ULong = new CType(CTypeEnum.UInt32, "unsigned long");
		public static CType Long = new CType(CTypeEnum.Int32, "long");
		public static CType Bool = new CType(CTypeEnum.Int16, "bool");
		public static CType Double = new CType(CTypeEnum.Double, "double");
		public static CType ObjectPtr = new CType(CTypeEnum.UInt16, "void*");
		public static CType ObjectFarPtr = new CType(CTypeEnum.UInt32, "void far*");
		public static CType ObjectLinearPtr = new CType(CTypeEnum.UInt32, "void _huge*");
		public static CType FunctionFarPtr = new CType(CTypeEnum.UInt32, "void far(* _func)(void)");
		public static CType CharPtr = new CType(CTypeEnum.UInt16, CType.Char, "char*");
		public static CType CharFarPtr = new CType(CTypeEnum.UInt32, CType.Char, "char far*");
		public static CType ByteFarPtr = new CType(CTypeEnum.UInt32, CType.Byte, "byte far*");
		public static CType LPSTR = CType.CharFarPtr;
		public static CType LPCSTR = CType.CharFarPtr;
		public static CType Variable = new CType(CTypeEnum.VariableParameters, "...");
	}
}
