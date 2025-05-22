using Disassembler.CPU;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Disassembler
{
	public class ILVariable : ILExpression
	{
		private ILVariableScopeEnum scope = ILVariableScopeEnum.LocalVariable;
		private ILValueTypeEnum type = ILValueTypeEnum.Undefined;
		private int offset = 0;
		private int arraySize = 0;
		private ProgramFunction? parent;

		public ILVariable(ProgramFunction? parent, CPUParameterSizeEnum type, int offset) : this(parent, ILVariableScopeEnum.LocalVariable, FromCPUParameterSizeEnum(type), offset)
		{ }

		public ILVariable(ProgramFunction? parent, ILValueTypeEnum type, int offset) : this(parent, ILVariableScopeEnum.LocalVariable, type, offset)
		{ }

		public ILVariable(ProgramFunction? parent, ILVariableScopeEnum scope, CPUParameterSizeEnum type, int offset) : this(parent, scope, FromCPUParameterSizeEnum(type), offset)
		{ }

		public ILVariable(ProgramFunction? parent, ILVariableScopeEnum scope, ILValueTypeEnum type, int offset)
		{
			this.parent = parent;
			this.scope = scope;
			this.type = type;
			this.offset = offset;
		}

		public string Name
		{
			get
			{
				switch (this.scope)
				{
					case ILVariableScopeEnum.LocalVariable:
						return $"Local_{this.Offset:x}";

					case ILVariableScopeEnum.LocalParameter:
						return $"Param_{this.Offset:x}";

					case ILVariableScopeEnum.Global:
						return $"Global_{this.Offset:x}";

					default:
						throw new Exception($"Undefined variable scope {this.scope}");
				}
			}
		}

		override public string ToCSString()
		{
			return this.Name;
		}

		public string ToCSDeclarationString()
		{
			StringBuilder sb = new StringBuilder();

			if (this.scope == ILVariableScopeEnum.Global)
			{
				sb.Append("public ");
			}

			switch (this.type)
			{
				case ILValueTypeEnum.UInt8:
					sb.Append("byte");
					break;

				case ILValueTypeEnum.Int8:
					sb.Append("sbyte");
					break;

				case ILValueTypeEnum.Ptr16:
				case ILValueTypeEnum.UInt16:
					sb.Append("ushort");
					break;

				case ILValueTypeEnum.Int16:
					sb.Append("short");
					break;

				case ILValueTypeEnum.Ptr32:
				case ILValueTypeEnum.UInt32:
					sb.Append("uint");
					break;

				case ILValueTypeEnum.Int32:
					sb.Append("int");
					break;

				case ILValueTypeEnum.FnPtr32:
					sb.Append("(void far *");
					break;

				default:
					throw new Exception($"Undefined variable type {this.type}");
			}

			if (this.arraySize != 0)
			{
				sb.Append("[]");
			}

			switch (this.scope)
			{
				case ILVariableScopeEnum.LocalVariable:
					sb.Append($" {this.Name}");
					break;

				case ILVariableScopeEnum.LocalParameter:
					sb.Append($" {this.Name}");
					break;

				case ILVariableScopeEnum.Global:
					sb.Append($" {this.Name}");
					break;
			}

			if (this.arraySize != 0 && this.scope != ILVariableScopeEnum.LocalParameter)
			{
				switch (this.type)
				{
					case ILValueTypeEnum.UInt8:
						sb.Append($" = new byte[{this.arraySize}]");
						break;

					case ILValueTypeEnum.Int8:
						sb.Append($" = new sbyte[{this.arraySize}]");
						break;

					case ILValueTypeEnum.Ptr16:
					case ILValueTypeEnum.UInt16:
						sb.Append($" = new ushort[{this.arraySize}]");
						break;

					case ILValueTypeEnum.Int16:
						sb.Append($" = new short[{this.arraySize}]");
						break;

					case ILValueTypeEnum.Ptr32:
					case ILValueTypeEnum.UInt32:
						sb.Append($" = new uint[{this.arraySize}]");
						break;

					case ILValueTypeEnum.Int32:
						sb.Append($" = new int[{this.arraySize}]");
						break;

					case ILValueTypeEnum.FnPtr32:
						sb.Append($")()");
						break;

					default:
						throw new Exception($"Undefined variable type {this.type}");
				}
			}

			switch (this.type)
			{
				case ILValueTypeEnum.UInt8:
				case ILValueTypeEnum.Int8:
				case ILValueTypeEnum.Ptr16:
				case ILValueTypeEnum.UInt16:
				case ILValueTypeEnum.Int16:
				case ILValueTypeEnum.Ptr32:
				case ILValueTypeEnum.UInt32:
				case ILValueTypeEnum.Int32:
					break;

				case ILValueTypeEnum.FnPtr32:
					sb.Append($")()");
					break;

				default:
					throw new Exception($"Undefined variable type {this.type}");
			}

			return sb.ToString();
		}

		public override string ToString()
		{
			return this.ToCSString();
		}

		public static ILValueTypeEnum FromCPUParameterSizeEnum(CPUParameterSizeEnum parameterType)
		{
			switch (parameterType)
			{
				case CPUParameterSizeEnum.UInt8:
					return ILValueTypeEnum.UInt8;

				case CPUParameterSizeEnum.UInt16:
					return ILValueTypeEnum.UInt16;

				case CPUParameterSizeEnum.UInt32:
					return ILValueTypeEnum.UInt32;

				default:
					return ILValueTypeEnum.Undefined;
			}
		}

		public ProgramFunction? Parent
		{
			get => this.parent;
		}

		public ILVariableScopeEnum Scope
		{
			get => this.scope;
			set => this.scope = value;
		}

		public ILValueTypeEnum Type
		{
			get => this.type;
			set => this.type = value;
		}

		public int Offset
		{
			get => this.offset;
			set => this.offset = value;
		}

		public int ArraySize
		{
			get => this.arraySize;
			set => this.arraySize = value;
		}
	}
}
