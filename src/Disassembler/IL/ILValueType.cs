using Disassembler.CPU;
using System.Text;

namespace Disassembler
{
	public class ILValueType
	{
		public static readonly ILValueType Void = new ILValueType(ILBaseValueTypeEnum.Void);

		private string typeName = "";
		private ILBaseValueTypeEnum baseType = ILBaseValueTypeEnum.Void;
		
		private ILValueType referencedType = ILValueType.Void;

		private List<ILValueType> memberObjects = new();

		public ILValueType(ILBaseValueTypeEnum baseType) : this("", baseType, ILValueType.Void, 0) { }

		public ILValueType(string typeName, ILBaseValueTypeEnum baseType) : this(typeName, baseType, ILValueType.Void, 0) { }

		public ILValueType(string typeName, ILBaseValueTypeEnum baseType, ILValueType referencedType) : this(typeName, baseType, referencedType, 0) { }

		public ILValueType(ILBaseValueTypeEnum baseType, int arraySize) : this("", baseType, ILValueType.Void, arraySize) { }

		public ILValueType(ILBaseValueTypeEnum baseType, ILValueType referencedType) : this("", baseType, referencedType, 0) { }

		public ILValueType(ILBaseValueTypeEnum baseType, ILValueType referencedType, int arraySize): this("", baseType, referencedType, arraySize) { }

		public ILValueType(string typeName, ILBaseValueTypeEnum baseType, ILValueType referencedType, int arraySize)
		{
			this.typeName = typeName;
			this.baseType = baseType;
			this.referencedType = referencedType;
		}

		public string CObjectDefinition()
		{
			StringBuilder sb = new();

			switch (this.BaseType)
			{
				case ILBaseValueTypeEnum.Struct:
					sb.AppendLine($"struct {this.typeName}");
					sb.AppendLine("{");
					for (int i = 0; i < this.memberObjects.Count; i++)
					{
						if (i > 0)
						{
							sb.AppendLine(", ");
						}
						sb.Append($"{this.memberObjects[i].CSDeclaration} {this.typeName}");
					}
					sb.AppendLine();
					sb.AppendLine("}");
					break;

				case ILBaseValueTypeEnum.Union:
					sb.AppendLine($"union {this.typeName}");
					sb.AppendLine("{");
					for (int i = 0; i < this.memberObjects.Count; i++)
					{
						if (i > 0)
						{
							sb.AppendLine(", ");
						}
						sb.Append($"{this.memberObjects[i].CSDeclaration} {this.typeName}");
					}
					sb.AppendLine();
					sb.AppendLine("}");
					break;
			}

			return sb.ToString();
		}

		public string CSObjectDefinition()
		{
			StringBuilder sb = new();

			switch (this.BaseType)
			{
				case ILBaseValueTypeEnum.Struct:
					sb.AppendLine($"struct {this.typeName}");
					sb.AppendLine("{");
					for (int i = 0; i < this.memberObjects.Count; i++)
					{
						if (i > 0)
						{
							sb.AppendLine(", ");
						}
						sb.Append($"{this.memberObjects[i].CSDeclaration} {this.typeName}");
					}
					sb.AppendLine();
					sb.AppendLine("}");
					break;

				case ILBaseValueTypeEnum.Union:
					sb.AppendLine($"union {this.typeName}");
					sb.AppendLine("{");
					for (int i = 0; i < this.memberObjects.Count; i++)
					{
						if (i > 0)
						{
							sb.AppendLine(", ");
						}
						sb.Append($"{this.memberObjects[i].CSDeclaration} {this.typeName}");
					}
					sb.AppendLine();
					sb.AppendLine("}");
					break;
			}

			return sb.ToString();
		}

		public string CDeclaration
		{
			get
			{
				switch (this.baseType)
				{
					case ILBaseValueTypeEnum.UInt8:
						return "unsigned char";

					case ILBaseValueTypeEnum.Int8:
						return "char";

					case ILBaseValueTypeEnum.UInt16:
						return "unsigned int";

					case ILBaseValueTypeEnum.Int16:
						return "int";

					case ILBaseValueTypeEnum.UInt32:
						return "unsigned long";

					case ILBaseValueTypeEnum.Int32:
						return "long";

					case ILBaseValueTypeEnum.Ptr16:
						return $"{this.referencedType.CDeclaration} *";

					case ILBaseValueTypeEnum.Ptr32:
						return $"{this.referencedType.CDeclaration} far *";

					case ILBaseValueTypeEnum.FnPtr32:
						return $"{this.referencedType.CDeclaration} far *";

					case ILBaseValueTypeEnum.Struct:
						return $"struct {this.typeName}";

					case ILBaseValueTypeEnum.Union:
						return $"union {this.typeName}";

					case ILBaseValueTypeEnum.DirectObject:
						return $"{this.typeName}";

					default:
						return "void";
				}
			}
		}

		public string CSDeclaration
		{
			get
			{
				switch (this.baseType)
				{
					case ILBaseValueTypeEnum.UInt8:
						return "byte";

					case ILBaseValueTypeEnum.Int8:
						return "sbyte";

					case ILBaseValueTypeEnum.UInt16:
						return "ushort";

					case ILBaseValueTypeEnum.Int16:
						return "short";

					case ILBaseValueTypeEnum.UInt32:
						return "uint";

					case ILBaseValueTypeEnum.Int32:
						return "int";

					case ILBaseValueTypeEnum.Ptr16:
						return $"{this.referencedType.CSDeclaration} *";

					case ILBaseValueTypeEnum.Ptr32:
						return $"{this.referencedType.CSDeclaration} *";

					case ILBaseValueTypeEnum.FnPtr32:
						return $"{this.referencedType.CSDeclaration} *";

					case ILBaseValueTypeEnum.Struct:
						return $"struct {this.typeName}";

					case ILBaseValueTypeEnum.Union:
						return $"union {this.typeName}";

					case ILBaseValueTypeEnum.DirectObject:
						return $"{this.typeName}";

					default:
						return "void";
				}
			}
		}

		public string TypeName { get => this.typeName; set => this.typeName = value; }

		public ILBaseValueTypeEnum BaseType { get => this.baseType; }

		public ILValueType ReferencedType { get => this.referencedType; }

		public List<ILValueType> MemberObjects { get => this.memberObjects; }

		public int SizeOf
		{
			get
			{
				switch (this.baseType)
				{
					case ILBaseValueTypeEnum.Int8:
						return 2;

					case ILBaseValueTypeEnum.UInt8:
						return 2;

					case ILBaseValueTypeEnum.Int16:
						return 2;

					case ILBaseValueTypeEnum.UInt16:
						return 2;

					case ILBaseValueTypeEnum.Int32:
						return 4;

					case ILBaseValueTypeEnum.UInt32:
						return 4;

					case ILBaseValueTypeEnum.Ptr16:
						return 2;

					case ILBaseValueTypeEnum.Ptr32:
					case ILBaseValueTypeEnum.FnPtr32:
						return 4;

					case ILBaseValueTypeEnum.Struct:
						throw new Exception("Not implemented");

					case ILBaseValueTypeEnum.Union:
						throw new Exception("Not implemented");

					case ILBaseValueTypeEnum.DirectObject:
						return 2;
				}

				return 0;
			}
		}
	}
}