namespace Disassembler.CPU.OpCodes
{
	public class OpCodeDefinition
	{
		private int iOpCode = 0;
		private int iMask = 0;
		private List<OpCodeParameter> aParameters = new List<OpCodeParameter>();

		public OpCodeDefinition(string opcode)
		{
			int iBitPosition = 8;
			string[] aParts = opcode.Split(' ');

			for (int i = 0; i < aParts.Length; i++)
			{
				string sTemp = aParts[i].Trim();

				if (!string.IsNullOrEmpty(sTemp))
				{
					int iTemp;

					switch (sTemp)
					{
						case "s":
							iBitPosition -= 1;
							aParameters.Add(new OpCodeParameter(OpCodeParameterTypeEnum.SignExtend, 1, iBitPosition));
							break;
						case "w":
							iBitPosition -= 1;
							aParameters.Add(new OpCodeParameter(OpCodeParameterTypeEnum.OperandSize, 1, iBitPosition));
							break;
						case "D":
							iBitPosition -= 1;
							aParameters.Add(new OpCodeParameter(OpCodeParameterTypeEnum.ReverseDirection, 1, iBitPosition));
							break;
						case "areg":
							iBitPosition -= 3;
							aParameters.Add(new OpCodeParameter(OpCodeParameterTypeEnum.AccumulatorWithRegister, 7, iBitPosition));
							break;
						case "reg":
							iBitPosition -= 3;
							aParameters.Add(new OpCodeParameter(OpCodeParameterTypeEnum.Register, 7, iBitPosition));
							break;
						case "regcl":
							iBitPosition = 0;
							aParameters.Add(new OpCodeParameter(OpCodeParameterTypeEnum.RegisterCL, 0));
							break;
						case "regadx":
							iBitPosition = 0;
							aParameters.Add(new OpCodeParameter(OpCodeParameterTypeEnum.RegisterAWithDX, 0));
							break;
						case "regdxa":
							iBitPosition = 0;
							aParameters.Add(new OpCodeParameter(OpCodeParameterTypeEnum.RegisterDXWithA, 0));
							break;
						case "sreg1":
							iBitPosition -= 2;
							aParameters.Add(new OpCodeParameter(OpCodeParameterTypeEnum.SegmentRegisterNoCS, 3, iBitPosition));
							break;
						case "sreg2":
							iBitPosition -= 2;
							aParameters.Add(new OpCodeParameter(OpCodeParameterTypeEnum.SegmentRegister, 3, iBitPosition));
							break;
						case "sreg3":
							iBitPosition -= 3;
							aParameters.Add(new OpCodeParameter(OpCodeParameterTypeEnum.SegmentRegister, 7, iBitPosition));
							break;
						case "sreg4":
							iBitPosition -= 3;
							aParameters.Add(new OpCodeParameter(OpCodeParameterTypeEnum.SegmentRegisterFSGS, 7, iBitPosition));
							break;
						case "mod":
							// mod has to end with 0/m or r/m, with optional reg between
							if (i + 2 >= aParts.Length)
								throw new Exception("Invalid mod r/m addressing mode");
							iTemp = i;

							iBitPosition -= 2;
							if (aParts[iTemp + 1].Equals("reg"))
							{
								iBitPosition -= 3;
								aParameters.Add(new OpCodeParameter(OpCodeParameterTypeEnum.Register, 7, iBitPosition));

								// this consitutes whole byte
								iBitPosition -= 3;
								i +=2;
							}

							if (aParts[iTemp + 1].Equals("sreg3"))
							{
								iBitPosition -= 3;
								aParameters.Add(new OpCodeParameter(OpCodeParameterTypeEnum.SegmentRegister, 7, iBitPosition));

								// this consitutes whole byte
								iBitPosition -= 3;
								i += 2;
							}

							if (aParts[iTemp + 2].Equals("0/m"))
							{
								aParameters.Add(new OpCodeParameter(OpCodeParameterTypeEnum.MemoryAddressing, 0xc7, 0));
							}
							else if (aParts[iTemp + 2].Equals("r/m"))
							{
								aParameters.Add(new OpCodeParameter(OpCodeParameterTypeEnum.RegisterOrMemoryAddressing, 0xc7, 0));
							}
							else
							{
								throw new Exception("Invalid mod r/m addressing mode");
							}
							break;
						case "mod1":
							// mod has to end with 0/m or r/m, with optional reg between
							// this is the reverse parameter order than 'mod'
							if (i + 2 >= aParts.Length)
								throw new Exception("Invalid mod r/m addressing mode");
							iTemp = i;

							iBitPosition -= 2;
							if (aParts[iTemp + 2].Equals("0/m"))
							{
								aParameters.Add(new OpCodeParameter(OpCodeParameterTypeEnum.MemoryAddressing, 0xc7, 0));
								iBitPosition -= 3;
							}
							else if (aParts[iTemp + 2].Equals("r/m"))
							{
								aParameters.Add(new OpCodeParameter(OpCodeParameterTypeEnum.RegisterOrMemoryAddressing, 0xc7, 0));
								iBitPosition -= 3;
							}
							else
							{
								throw new Exception("Invalid mod r/m addressing mode");
							}

							if (aParts[iTemp + 1].Equals("reg"))
							{
								iBitPosition -= 3;
								aParameters.Add(new OpCodeParameter(OpCodeParameterTypeEnum.Register, 7, 3));

								i += 2;
							}
							else if (aParts[iTemp + 1].Equals("sreg3"))
							{
								iBitPosition -= 3;
								aParameters.Add(new OpCodeParameter(OpCodeParameterTypeEnum.SegmentRegister, 7, 3));

								i += 2;
							}
							break;
						case "0/m":
						case "r/m":
							// ignore those as they have been processed
							iBitPosition -= 3;
							break;
						case "tttn":
							iBitPosition -= 4;
							aParameters.Add(new OpCodeParameter(OpCodeParameterTypeEnum.Condition, 0xf, iBitPosition));
							break;
						case "uint8a":
							if (iBitPosition != 8)
								throw new Exception("Invalid Immediate value position");
							aParameters.Add(new OpCodeParameter(OpCodeParameterTypeEnum.ImmediateValueWithAccumulator, 1));
							iBitPosition = 0;
							break;
						case "uint8":
							if (iBitPosition != 8)
								throw new Exception("Invalid Immediate value position");
							aParameters.Add(new OpCodeParameter(OpCodeParameterTypeEnum.ImmediateValue, 1));
							iBitPosition = 0;
							break;
						case "uint16":
							if (iBitPosition != 8)
								throw new Exception("Invalid Immediate value position");
							aParameters.Add(new OpCodeParameter(OpCodeParameterTypeEnum.ImmediateValue, 2));
							iBitPosition = 0;
							break;
						case "mauint16":
							if (iBitPosition != 8)
								throw new Exception("Invalid Immediate value position");
							aParameters.Add(new OpCodeParameter(OpCodeParameterTypeEnum.ImmediateMemoryAddressWithAccumulator, 2));
							iBitPosition = 0;
							break;
						case "uint8/uint16":
							if (iBitPosition != 8)
								throw new Exception("Invalid Immediate value position");
							aParameters.Add(new OpCodeParameter(OpCodeParameterTypeEnum.ImmediateValue, 0));
							iBitPosition = 0;
							break;
						case "auint":
							if (iBitPosition != 8)
								throw new Exception("Invalid Immediate value position");
							aParameters.Add(new OpCodeParameter(OpCodeParameterTypeEnum.AccumulatorWithImmediateValue, 0));
							iBitPosition = 0;
							break;
						case "auint8":
							if (iBitPosition != 8)
								throw new Exception("Invalid Immediate value position");
							aParameters.Add(new OpCodeParameter(OpCodeParameterTypeEnum.AccumulatorWithImmediateValue, 1));
							iBitPosition = 0;
							break;
						case "rel8":
							if (iBitPosition != 8)
								throw new Exception("Invalid Immediate value position");
							aParameters.Add(new OpCodeParameter(OpCodeParameterTypeEnum.RelativeValue, 1));
							iBitPosition = 0;
							break;
						case "rel16":
							if (iBitPosition != 8)
								throw new Exception("Invalid Immediate value position");
							aParameters.Add(new OpCodeParameter(OpCodeParameterTypeEnum.RelativeValue, 2));
							iBitPosition = 0;
							break;
						case "uint16:uint16":
							if (iBitPosition != 8)
								throw new Exception("Invalid Immediate value position");
							aParameters.Add(new OpCodeParameter(OpCodeParameterTypeEnum.ImmediateSegmentOffset, 4));
							iBitPosition = 0;
							break;
						case "val1":
							aParameters.Add(new OpCodeParameter(OpCodeParameterTypeEnum.ImmediateValue1, 0));
							iBitPosition = 0;
							break;
						case "val3":
							aParameters.Add(new OpCodeParameter(OpCodeParameterTypeEnum.ImmediateValue3, 0));
							iBitPosition = 0;
							break;
						case "d":
							iBitPosition -= 1;
							aParameters.Add(new OpCodeParameter(OpCodeParameterTypeEnum.FPUDestination, 1, iBitPosition));
							break;
						case "ST(i)":
							iBitPosition -= 3;
							aParameters.Add(new OpCodeParameter(OpCodeParameterTypeEnum.FPUStackAddress, 7, iBitPosition));
							break;
						default:
							// has to be binary
							int iOpcodeTemp = 0;
							int iMaskTemp = 0;
							for (int j = 0; j < sTemp.Length; j++)
							{
								iBitPosition -= 1;
								iMaskTemp <<= 1;
								iOpcodeTemp <<= 1;

								iMaskTemp |= 1;
								if (sTemp[j] == '1')
								{
									iOpcodeTemp |= 1;
								}
								else if (sTemp[j] != '0')
								{
									throw new Exception("Invalid opcode bits '" + sTemp + "'");
								}
							}
							iTemp = iBitPosition;
							while (iTemp > 0)
							{
								iMaskTemp <<= 1;
								iOpcodeTemp <<= 1;
								iTemp--;
							}
							this.iOpCode |= iOpcodeTemp;
							this.iMask |= iMaskTemp;
							break;
					}
				}
			}
			if (iBitPosition != 0)
				throw new Exception("Invalid Opcode length");
		}

		public int OpCode
		{
			get
			{
				return this.iOpCode;
			}
		}

		public int Mask
		{
			get
			{
				return this.iMask;
			}
		}

		public List<OpCodeParameter> Parameters
		{
			get
			{
				return this.aParameters;
			}
		}

		public byte[] Expand()
		{
			List<byte> aBytes = new List<byte>();

			for (int i = 0; i < this.aParameters.Count; i++)
			{
				this.aParameters[i].ValueIndex = 0;
			}

			if (this.iMask == 0)
				return new byte[0];

			bool bCarry = false;
			while (!bCarry)
			{
				int iByte = this.iOpCode;

				for (int i = 0; i < this.aParameters.Count; i++)
				{
					OpCodeParameter param = this.aParameters[i];
					if (param.Values.Count > 0)
					{
						iByte |= param.Values[param.ValueIndex] << param.BitPosition;
					}
				}

				bCarry = true;
				for (int i = 0; i < this.aParameters.Count; i++)
				{
					OpCodeParameter param = this.aParameters[i];
					if (param.Values.Count > 0)
					{
						param.ValueIndex++;
						if (param.ValueIndex >= param.Values.Count)
						{
							param.ValueIndex = 0;
							bCarry = true;
						}
						else
						{
							bCarry = false;
							break;
						}
					}
				}

				aBytes.Add((byte)(iByte & 0xff));
			}

			return aBytes.ToArray();
		}
	}
}
