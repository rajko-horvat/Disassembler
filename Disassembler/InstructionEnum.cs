﻿using System;

namespace Disassembler
{
	public enum InstructionEnum
	{
		Undefined,
		AAA,
		AAD,
		AAM,
		AAS,
		ADC,
		ADD,
		AND,
		ARPL,
		BOUND,
		BSF,
		BSR,
		BSWAP,
		BT,
		BTC,
		BTR,
		BTS,
		CALL,
		CALLF,
		CBW,
		CLC,
		CLD,
		CLI,
		CLTS,
		CMC,
		CMP,
		CMPS,
		CMPXCHG,
		CWD,
		DAA,
		DAS,
		DEC,
		DIV,
		ENTER,
		F2XM1,
		FABS,
		FADD,
		FADDP,
		FBLD,
		FBSTP,
		FCHS,
		FCLEX,
		FCOM,
		FCOMP,
		FCOMPP,
		FCOS,
		FDECSTP,
		FDIV,
		FDIVP,
		FDIVR,
		FDIVRP,
		FFREE,
		FIADD,
		FICOM,
		FICOMP,
		FIDIV,
		FIDIVR,
		FILD,
		FIMUL,
		FINCSTP,
		FINIT,
		FIST,
		FISTP,
		FISUB,
		FISUBR,
		FLD,
		FLD1,
		FLDCW,
		FLDENV,
		FLDL2E,
		FLDL2T,
		FLDLG2,
		FLDLN2,
		FLDPI,
		FLDZ,
		FMUL,
		FMULP,
		FNOP,
		FPATAN,
		FPREM,
		FPREM1,
		FPTAN,
		FRNDINT,
		FRSTOR,
		FSAVE,
		FSCALE,
		FSIN,
		FSINCOS,
		FSQRT,
		FST,
		FSTCW,
		FSTENV,
		FSTP,
		FSTSW,
		FSUB,
		FSUBP,
		FSUBR,
		FSUBRP,
		FTST,
		FUCOM,
		FUCOMP,
		FUCOMPP,
		FXAM,
		FXCH,
		FXTRACT,
		FYL2X,
		FYL2XP1,
		HLT,
		IDIV,
		IMUL,
		IN,
		INC,
		INS,
		INT,
		INTO,
		INVD,
		INVLPG,
		IRET,
		Jcc,
		JCXZ,
		JMP,
		JMPF,
		LAHF,
		LAR,
		LDS,
		LEA,
		LEAVE,
		LES,
		LFS,
		LGDT,
		LGS,
		LIDT,
		LLDT,
		LMSW,
		LODS,
		LOOP,
		LOOPZ,
		LOOPNZ,
		LSL,
		LSS,
		LTR,
		MOV,
		MOVS,
		MOVSX,
		MOVZX,
		MUL,
		NEG,
		NOP,
		NOT,
		OR,
		OUT,
		OUTS,
		POP,
		POPA,
		POPF,
		PUSH,
		PUSHA,
		PUSHF,
		RCL,
		RCR,
		RET,
		RETF,
		ROL,
		ROR,
		SAHF,
		SAR,
		SBB,
		SCAS,
		SETcc,
		SGDT,
		SHL,
		SHLD,
		SHR,
		SHRD,
		SIDT,
		SLDT,
		SMSW,
		STC,
		STD,
		STI,
		STOS,
		STR,
		SUB,
		TEST,
		VERR,
		VERW,
		WAIT,
		XCHG,
		XLAT,
		XOR,
		// syntetic instructions - not real instructions, just synonyms
		SWITCH,
		WordsToDword,
		CallFunction,
		If,
		IfAnd,
		IfOr
	}
}
