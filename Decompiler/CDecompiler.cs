using Disassembler.CPU;
using Disassembler.NE;
using Disassembler.OMF;
using IRB.Collections.Generic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Disassembler.Decompiler
{
	public class CDecompiler
	{
		private NewExecutable oExecutable = null;
		private List<ModuleMatch> aLibraryMatches = null;
		private CGlobalNamespace oGlobalNamespace;

		private uint uiDataSegment = 0;

		public CDecompiler(NewExecutable executable, List<ModuleMatch> matches)
		{
			this.oGlobalNamespace = new CGlobalNamespace(this);
			this.oExecutable = executable;
			this.aLibraryMatches = matches;

			// stack segment is the last segment, reserve segments used for module binding
			int iStackSegment = this.oExecutable.Segments.Count + this.oExecutable.ModuleReferences.Count;

			// the last data segment is the data segment

			for (int i = this.oExecutable.Segments.Count - 1; i >= 0; i--)
			{
				if ((this.oExecutable.Segments[i].Flags & SegmentFlagsEnum.DataSegment) == SegmentFlagsEnum.DataSegment)
				{
					this.uiDataSegment = (uint)i;
					break;
				}
			}

			if (this.uiDataSegment < 0)
				throw new Exception("Can't determine data segment");

			DefineGlobalTypes();
			DefineGlobalAPI();
			DefineModuleImports();
		}

		private void DefineGlobalTypes()
		{
			// C API types
			CType oDiskFree = new CType(CTypeEnum.Struct, "diskfree_t");
			oDiskFree.Members.Add(new CType(CType.UInt, "total_clusters"));
			oDiskFree.Members.Add(new CType(CType.UInt, "avail_clusters"));
			oDiskFree.Members.Add(new CType(CType.UInt, "sectors_per_cluster"));
			oDiskFree.Members.Add(new CType(CType.UInt, "bytes_per_sector"));

			this.oGlobalNamespace.GlobalTypes.Add(oDiskFree.Name, oDiskFree);

			CType oFile = new CType(CTypeEnum.Struct, "FILE");
			oFile.Members.Add(new CType(CType.Int, "level"));
			oFile.Members.Add(new CType(CType.UInt, "flags"));
			oFile.Members.Add(new CType(CType.Char, "fd"));
			oFile.Members.Add(new CType(CType.Byte, "hold"));
			oFile.Members.Add(new CType(CType.Int, "bsize"));
			oFile.Members.Add(new CType(CType.ByteFarPtr, "buffer"));
			oFile.Members.Add(new CType(CType.ByteFarPtr, "curp"));
			oFile.Members.Add(new CType(CType.UInt, "istemp"));
			oFile.Members.Add(new CType(CType.Int, "token"));

			this.oGlobalNamespace.GlobalTypes.Add(oFile.Name, oFile);

			// Windows API types
			this.oGlobalNamespace.GlobalTypes.Add("ATOM", new CType(CType.Word, "ATOM"));
			this.oGlobalNamespace.GlobalTypes.Add("HACCEL", new CType(CType.Word, "HACCEL"));
			this.oGlobalNamespace.GlobalTypes.Add("HBITMAP", new CType(CType.Word, "HBITMAP"));
			this.oGlobalNamespace.GlobalTypes.Add("HBRUSH", new CType(CType.Word, "HBRUSH"));
			this.oGlobalNamespace.GlobalTypes.Add("HCURSOR", new CType(CType.Word, "HCURSOR"));
			this.oGlobalNamespace.GlobalTypes.Add("HDC", new CType(CType.Word, "HDC"));
			this.oGlobalNamespace.GlobalTypes.Add("HFILE", new CType(CType.Int, "HFILE"));
			this.oGlobalNamespace.GlobalTypes.Add("HFONT", new CType(CType.Word, "HFONT"));
			this.oGlobalNamespace.GlobalTypes.Add("HGDIOBJ", new CType(CType.Word, "HGDIOBJ"));
			this.oGlobalNamespace.GlobalTypes.Add("HGLOBAL", new CType(CType.Word, "HGLOBAL"));
			this.oGlobalNamespace.GlobalTypes.Add("HICON", new CType(CType.Word, "HICON"));
			this.oGlobalNamespace.GlobalTypes.Add("HINSTANCE", new CType(CType.Word, "HINSTANCE"));
			this.oGlobalNamespace.GlobalTypes.Add("HMENU", new CType(CType.Word, "HMENU"));
			this.oGlobalNamespace.GlobalTypes.Add("HPALETTE", new CType(CType.Word, "HPALETTE"));
			this.oGlobalNamespace.GlobalTypes.Add("HPEN", new CType(CType.Word, "HPEN"));
			this.oGlobalNamespace.GlobalTypes.Add("HRSRC", new CType(CType.Word, "HRSRC"));
			this.oGlobalNamespace.GlobalTypes.Add("HWND", new CType(CType.Word, "HWND"));
			this.oGlobalNamespace.GlobalTypes.Add("WPARAM", new CType(CType.Word, "WPARAM"));

			this.oGlobalNamespace.GlobalTypes.Add("COLORREF", new CType(CType.DWord, "COLORREF"));
			this.oGlobalNamespace.GlobalTypes.Add("LPARAM", new CType(CType.Long, "LPARAM"));
			this.oGlobalNamespace.GlobalTypes.Add("LRESULT", new CType(CType.Long, "LRESULT"));

			this.oGlobalNamespace.GlobalTypes.Add("DLGPROC", new CType(CType.FunctionFarPtr, "DLGPROC"));
			this.oGlobalNamespace.GlobalTypes.Add("FARPROC", new CType(CType.FunctionFarPtr, "FARPROC"));
			this.oGlobalNamespace.GlobalTypes.Add("FONTENUMPROC", new CType(CType.FunctionFarPtr, "FONTENUMPROC"));
			this.oGlobalNamespace.GlobalTypes.Add("TIMERPROC", new CType(CType.FunctionFarPtr, "TIMERPROC"));

			this.oGlobalNamespace.GlobalTypes.Add("VoidHugePtr", new CType(CTypeEnum.UInt32, CType.Void, "void _huge*"));

			// windows structures
			CType oBitmapInfoHeader = new CType(CTypeEnum.Struct, "BITMAPINFOHEADER");
			oBitmapInfoHeader.Members.Add(new CType(CType.DWord, "biSize"));
			oBitmapInfoHeader.Members.Add(new CType(CType.Long, "biWidth"));
			oBitmapInfoHeader.Members.Add(new CType(CType.Long, "biHeight"));
			oBitmapInfoHeader.Members.Add(new CType(CType.Word, "biPlanes"));
			oBitmapInfoHeader.Members.Add(new CType(CType.Word, "biBitCount"));
			oBitmapInfoHeader.Members.Add(new CType(CType.DWord, "biCompression"));
			oBitmapInfoHeader.Members.Add(new CType(CType.DWord, "biSizeImage"));
			oBitmapInfoHeader.Members.Add(new CType(CType.Long, "biXPelsPerMeter"));
			oBitmapInfoHeader.Members.Add(new CType(CType.Long, "biYPelsPerMeter"));
			oBitmapInfoHeader.Members.Add(new CType(CType.DWord, "biClrUsed"));
			oBitmapInfoHeader.Members.Add(new CType(CType.DWord, "biClrImportant"));
			this.oGlobalNamespace.GlobalTypes.Add(oBitmapInfoHeader.Name, oBitmapInfoHeader);

			CType oRGBQuad = new CType(CTypeEnum.Struct, "RGBQUAD");
			oRGBQuad.Members.Add(new CType(CType.Byte, "rgbBlue"));
			oRGBQuad.Members.Add(new CType(CType.Byte, "rgbGreen"));
			oRGBQuad.Members.Add(new CType(CType.Byte, "rgbRed"));
			oRGBQuad.Members.Add(new CType(CType.Byte, "rgbReserved"));
			this.oGlobalNamespace.GlobalTypes.Add(oRGBQuad.Name, oRGBQuad);

			CType oBitmapInfo = new CType(CTypeEnum.Struct, "BITMAPINFO");
			oBitmapInfo.Members.Add(new CType(oBitmapInfoHeader, "bmiHeader"));
			oBitmapInfo.Members.Add(new CType(oRGBQuad, -1, "bmiColors"));
			this.oGlobalNamespace.GlobalTypes.Add(oBitmapInfo.Name, oBitmapInfo);

			CType oLOGFont = new CType(CTypeEnum.Struct, "LOGFONT");
			oLOGFont.Members.Add(new CType(CType.Int, "lfHeight"));
			oLOGFont.Members.Add(new CType(CType.Int, "lfWidth"));
			oLOGFont.Members.Add(new CType(CType.Int, "lfEscapement"));
			oLOGFont.Members.Add(new CType(CType.Int, "lfOrientation"));
			oLOGFont.Members.Add(new CType(CType.Int, "lfWeight"));
			oLOGFont.Members.Add(new CType(CType.Byte, "lfItalic"));
			oLOGFont.Members.Add(new CType(CType.Byte, "lfUnderline"));
			oLOGFont.Members.Add(new CType(CType.Byte, "lfStrikeOut"));
			oLOGFont.Members.Add(new CType(CType.Byte, "lfCharSet"));
			oLOGFont.Members.Add(new CType(CType.Byte, "lfOutPrecision"));
			oLOGFont.Members.Add(new CType(CType.Byte, "lfClipPrecision"));
			oLOGFont.Members.Add(new CType(CType.Byte, "lfQuality"));
			oLOGFont.Members.Add(new CType(CType.Byte, "lfPitchAndFamily"));
			oLOGFont.Members.Add(new CType(CType.Byte, 32, "lfFaceName"));
			this.oGlobalNamespace.GlobalTypes.Add(oLOGFont.Name, oLOGFont);

			CType oPaletteEntry = new CType(CTypeEnum.Struct, "PALETTEENTRY");
			oPaletteEntry.Members.Add(new CType(CType.Byte, "peRed"));
			oPaletteEntry.Members.Add(new CType(CType.Byte, "peGreen"));
			oPaletteEntry.Members.Add(new CType(CType.Byte, "peBlue"));
			oPaletteEntry.Members.Add(new CType(CType.Byte, "peFlags"));
			this.oGlobalNamespace.GlobalTypes.Add(oPaletteEntry.Name, oPaletteEntry);

			CType oLOGPalette = new CType(CTypeEnum.Struct, "LOGPALETTE");
			oLOGPalette.Members.Add(new CType(CType.Word, "palVersion"));
			oLOGPalette.Members.Add(new CType(CType.Word, "palNumEntries"));
			oLOGPalette.Members.Add(new CType(oPaletteEntry, -1, "palPalEntry"));
			this.oGlobalNamespace.GlobalTypes.Add(oLOGPalette.Name, oLOGPalette);

			CType oPoint = new CType(CTypeEnum.Struct, "POINT");
			oPoint.Members.Add(new CType(CType.Int, "x"));
			oPoint.Members.Add(new CType(CType.Int, "y"));
			this.oGlobalNamespace.GlobalTypes.Add(oPoint.Name, oPoint);

			CType oMsg = new CType(CTypeEnum.Struct, "MSG");
			oMsg.Members.Add(new CType(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwnd"));
			oMsg.Members.Add(new CType(CType.UInt, "message"));
			oMsg.Members.Add(new CType(this.oGlobalNamespace.GlobalTypes.GetValueByKey("WPARAM"), "wParam;"));
			oMsg.Members.Add(new CType(this.oGlobalNamespace.GlobalTypes.GetValueByKey("LPARAM"), "lParam;"));
			oMsg.Members.Add(new CType(CType.DWord, "time;"));
			oMsg.Members.Add(new CType(oPoint, "pt"));
			this.oGlobalNamespace.GlobalTypes.Add(oMsg.Name, oMsg);

			CType oOpenFileName = new CType(CTypeEnum.Struct, "OPENFILENAME");
			oOpenFileName.Members.Add(new CType(CType.DWord, "lStructSize"));
			oOpenFileName.Members.Add(new CType(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwndOwner"));
			oOpenFileName.Members.Add(new CType(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HINSTANCE"), "hInstance"));
			oOpenFileName.Members.Add(new CType(CType.LPCSTR, "lpstrFilter"));
			oOpenFileName.Members.Add(new CType(CType.LPSTR, "lpstrCustomFilter"));
			oOpenFileName.Members.Add(new CType(CType.DWord, "nMaxCustFilter"));
			oOpenFileName.Members.Add(new CType(CType.DWord, "nFilterIndex"));
			oOpenFileName.Members.Add(new CType(CType.LPSTR, "lpstrFile"));
			oOpenFileName.Members.Add(new CType(CType.DWord, "nMaxFile"));
			oOpenFileName.Members.Add(new CType(CType.LPSTR, "lpstrFileTitle"));
			oOpenFileName.Members.Add(new CType(CType.DWord, "nMaxFileTitle"));
			oOpenFileName.Members.Add(new CType(CType.LPCSTR, "lpstrInitialDir"));
			oOpenFileName.Members.Add(new CType(CType.LPCSTR, "lpstrTitle"));
			oOpenFileName.Members.Add(new CType(CType.DWord, "Flags"));
			oOpenFileName.Members.Add(new CType(CType.UInt, "nFileOffset"));
			oOpenFileName.Members.Add(new CType(CType.UInt, "nFileExtension"));
			oOpenFileName.Members.Add(new CType(CType.LPCSTR, "lpstrDefExt"));
			oOpenFileName.Members.Add(new CType(this.oGlobalNamespace.GlobalTypes.GetValueByKey("LPARAM"), "lCustData"));
			oOpenFileName.Members.Add(new CType(CType.ObjectFarPtr, "lpfnHook"));
			oOpenFileName.Members.Add(new CType(CType.LPCSTR, "lpTemplateName"));
			this.oGlobalNamespace.GlobalTypes.Add(oOpenFileName.Name, oOpenFileName);

			CType oRect = new CType(CTypeEnum.Struct, "RECT");
			oRect.Members.Add(new CType(CType.Int, "left"));
			oRect.Members.Add(new CType(CType.Int, "top"));
			oRect.Members.Add(new CType(CType.Int, "right"));
			oRect.Members.Add(new CType(CType.Int, "bottom"));
			this.oGlobalNamespace.GlobalTypes.Add(oRect.Name, oRect);

			CType oPaintStruct = new CType(CTypeEnum.Struct, "PAINTSTRUCT");
			oPaintStruct.Members.Add(new CType(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HDC"), "hdc"));
			oPaintStruct.Members.Add(new CType(CType.Bool, "fErase"));
			oPaintStruct.Members.Add(new CType(oRect, "rcPaint"));
			oPaintStruct.Members.Add(new CType(CType.Bool, "fRestore"));
			oPaintStruct.Members.Add(new CType(CType.Bool, "fIncUpdate"));
			oPaintStruct.Members.Add(new CType(CType.Byte, 16, "rgbReserved"));
			this.oGlobalNamespace.GlobalTypes.Add(oPaintStruct.Name, oPaintStruct);

			CType oTextMetric = new CType(CTypeEnum.Struct, "TEXTMETRIC");
			oTextMetric.Members.Add(new CType(CType.Int, "tmHeight"));
			oTextMetric.Members.Add(new CType(CType.Int, "tmAscent"));
			oTextMetric.Members.Add(new CType(CType.Int, "tmDescent"));
			oTextMetric.Members.Add(new CType(CType.Int, "tmInternalLeading"));
			oTextMetric.Members.Add(new CType(CType.Int, "tmExternalLeading"));
			oTextMetric.Members.Add(new CType(CType.Int, "tmAveCharWidth"));
			oTextMetric.Members.Add(new CType(CType.Int, "tmMaxCharWidth"));
			oTextMetric.Members.Add(new CType(CType.Int, "tmWeight"));
			oTextMetric.Members.Add(new CType(CType.Byte, "tmItalic"));
			oTextMetric.Members.Add(new CType(CType.Byte, "tmUnderlined"));
			oTextMetric.Members.Add(new CType(CType.Byte, "tmStruckOut"));
			oTextMetric.Members.Add(new CType(CType.Byte, "tmFirstChar"));
			oTextMetric.Members.Add(new CType(CType.Byte, "tmLastChar"));
			oTextMetric.Members.Add(new CType(CType.Byte, "tmDefaultChar"));
			oTextMetric.Members.Add(new CType(CType.Byte, "tmBreakChar"));
			oTextMetric.Members.Add(new CType(CType.Byte, "tmPitchAndFamily"));
			oTextMetric.Members.Add(new CType(CType.Byte, "tmCharSet"));
			oTextMetric.Members.Add(new CType(CType.Int, "tmOverhang"));
			oTextMetric.Members.Add(new CType(CType.Int, "tmDigitizedAspectX"));
			oTextMetric.Members.Add(new CType(CType.Int, "tmDigitizedAspectY"));
			this.oGlobalNamespace.GlobalTypes.Add(oTextMetric.Name, oTextMetric);

			CType oWNDClass = new CType(CTypeEnum.Struct, "WNDCLASS");
			oWNDClass.Members.Add(new CType(CType.UInt, "style"));
			oWNDClass.Members.Add(new CType(CType.ObjectFarPtr, "lpfnWndProc"));
			oWNDClass.Members.Add(new CType(CType.Int, "cbClsExtra"));
			oWNDClass.Members.Add(new CType(CType.Int, "cbWndExtra"));
			oWNDClass.Members.Add(new CType(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HINSTANCE"), "hInstance"));
			oWNDClass.Members.Add(new CType(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HICON"), "hIcon"));
			oWNDClass.Members.Add(new CType(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HCURSOR"), "hCursor"));
			oWNDClass.Members.Add(new CType(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HBRUSH"), "hbrBackground"));
			oWNDClass.Members.Add(new CType(CType.LPCSTR, "lpszMenuName"));
			oWNDClass.Members.Add(new CType(CType.LPCSTR, "lpszClassName"));
			this.oGlobalNamespace.GlobalTypes.Add(oWNDClass.Name, oWNDClass);

			this.oGlobalNamespace.GlobalTypes.Add("IntFarPtr", new CType(CTypeEnum.UInt32, CType.Int, "int far*"));
			this.oGlobalNamespace.GlobalTypes.Add("LongFarPtr", new CType(CTypeEnum.UInt32, CType.Long, "long far*"));

			this.oGlobalNamespace.GlobalTypes.Add("PointFarPtr", new CType(CTypeEnum.UInt32, oPoint, "POINT far*"));
			this.oGlobalNamespace.GlobalTypes.Add("RectFarPtr", new CType(CTypeEnum.UInt32, oRect, "RECT far*"));
			this.oGlobalNamespace.GlobalTypes.Add("PaintStructFarPtr", new CType(CTypeEnum.UInt32, oPaintStruct, "PAINTSTRUCT far*"));
			this.oGlobalNamespace.GlobalTypes.Add("MsgFarPtr", new CType(CTypeEnum.UInt32, oMsg, "MSG far*"));
			this.oGlobalNamespace.GlobalTypes.Add("LogFontFarPtr", new CType(CTypeEnum.UInt32, oLOGFont, "LOGFONT far*"));
			this.oGlobalNamespace.GlobalTypes.Add("TextMetricFarPtr", new CType(CTypeEnum.UInt32, oTextMetric, "TEXTMETRIC far*"));
			this.oGlobalNamespace.GlobalTypes.Add("LogPaletteFarPtr", new CType(CTypeEnum.UInt32, oLOGPalette, "LOGPALETTE far*"));
			this.oGlobalNamespace.GlobalTypes.Add("PaletteEntryFarPtr", new CType(CTypeEnum.UInt32, oPaletteEntry, "PALETTEENTRY far*"));
			this.oGlobalNamespace.GlobalTypes.Add("BitmapInfoHeaderFarPtr", new CType(CTypeEnum.UInt32, oBitmapInfoHeader, "BITMAPINFOHEADER far*"));
			this.oGlobalNamespace.GlobalTypes.Add("BitmapInfoFarPtr", new CType(CTypeEnum.UInt32, oBitmapInfo, "BITMAPINFO far*"));
			this.oGlobalNamespace.GlobalTypes.Add("OpenFilenameFarPtr", new CType(CTypeEnum.UInt32, oOpenFileName, "OPENFILENAME far*"));
		}

		private void DefineGlobalAPI()
		{
			this.oGlobalNamespace.APIFunctions.Add("_read", new CFunction(this, CallTypeEnum.Cdecl, "_read", new List<CParameter>(new CParameter[] { new CParameter(CType.Int, "__handle"), new CParameter(CType.ObjectFarPtr, "__buf"), new CParameter(CType.UInt, "__len") }), CType.Int, 0, 0));
			this.oGlobalNamespace.APIFunctions.Add("_write", new CFunction(this, CallTypeEnum.Cdecl, "_write", new List<CParameter>(new CParameter[] { new CParameter(CType.Int, "__handle"), new CParameter(CType.ObjectFarPtr, "__buf"), new CParameter(CType.UInt, "__len") }), CType.Int, 0, 0));
			this.oGlobalNamespace.APIFunctions.Add("__read", new CFunction(this, CallTypeEnum.Cdecl, "__read", new List<CParameter>(new CParameter[] { new CParameter(CType.Int, "__handle"), new CParameter(CType.ObjectFarPtr, "__buf"), new CParameter(CType.UInt, "__len") }), CType.Int, 0, 0));
			this.oGlobalNamespace.APIFunctions.Add("__write", new CFunction(this, CallTypeEnum.Cdecl, "__write", new List<CParameter>(new CParameter[] { new CParameter(CType.Int, "__handle"), new CParameter(CType.ObjectFarPtr, "__buf"), new CParameter(CType.UInt, "__len") }), CType.Int, 0, 0));
			this.oGlobalNamespace.APIFunctions.Add("_chdrive", new CFunction(this, CallTypeEnum.Cdecl, "_chdrive", new List<CParameter>(new CParameter[] { new CParameter(CType.Int, "__drive") }), CType.Int, 0, 0));
			this.oGlobalNamespace.APIFunctions.Add("_dos_getdiskfree", new CFunction(this, CallTypeEnum.Cdecl, "_dos_getdiskfree", new List<CParameter>(new CParameter[] { new CParameter(CType.UInt, "__drive"), new CParameter(new CType(CTypeEnum.UInt32, this.oGlobalNamespace.GlobalTypes.GetValueByKey("diskfree_t"), "diskfree_t far*"), "__dtable") }), CType.UInt, 0, 0));
			this.oGlobalNamespace.APIFunctions.Add("_getdrive", new CFunction(this, CallTypeEnum.Cdecl, "_getdrive", new List<CParameter>(new CParameter[] { }), CType.Int, 0, 0));
			this.oGlobalNamespace.APIFunctions.Add("chdir", new CFunction(this, CallTypeEnum.Cdecl, "chdir", new List<CParameter>(new CParameter[] { new CParameter(CType.CharFarPtr, "__path") }), CType.Int, 0, 0));
			this.oGlobalNamespace.APIFunctions.Add("close", new CFunction(this, CallTypeEnum.Cdecl, "close", new List<CParameter>(new CParameter[] { new CParameter(CType.Int, "__handle") }), CType.Int, 0, 0));
			this.oGlobalNamespace.APIFunctions.Add("exit", new CFunction(this, CallTypeEnum.Cdecl, "exit", new List<CParameter>(new CParameter[] { new CParameter(CType.Int, "__status") }), CType.Void, 0, 0));
			this.oGlobalNamespace.APIFunctions.Add("fclose", new CFunction(this, CallTypeEnum.Cdecl, "fclose", new List<CParameter>(new CParameter[] { new CParameter(new CType(CTypeEnum.UInt32, this.oGlobalNamespace.GlobalTypes.GetValueByKey("FILE"), "FILE far*"), "__stream") }), CType.Int, 0, 0));
			this.oGlobalNamespace.APIFunctions.Add("fcloseall", new CFunction(this, CallTypeEnum.Cdecl, "fcloseall", new List<CParameter>(new CParameter[] { }), CType.Int, 0, 0));
			this.oGlobalNamespace.APIFunctions.Add("fgetc", new CFunction(this, CallTypeEnum.Cdecl, "fgetc", new List<CParameter>(new CParameter[] { new CParameter(new CType(CTypeEnum.UInt32, this.oGlobalNamespace.GlobalTypes.GetValueByKey("FILE"), "FILE far*"), "__stream") }), CType.Int, 0, 0));
			this.oGlobalNamespace.APIFunctions.Add("fgetpos", new CFunction(this, CallTypeEnum.Cdecl, "fgetpos", new List<CParameter>(new CParameter[] { new CParameter(new CType(CTypeEnum.UInt32, this.oGlobalNamespace.GlobalTypes.GetValueByKey("FILE"), "FILE far*"), "__stream"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("LongFarPtr"), "__pos") }), CType.Int, 0, 0));
			this.oGlobalNamespace.APIFunctions.Add("fopen", new CFunction(this, CallTypeEnum.Cdecl, "fopen", new List<CParameter>(new CParameter[] { new CParameter(CType.CharFarPtr, "__path"), new CParameter(CType.CharFarPtr, "__mode") }), new CType(CTypeEnum.UInt32, this.oGlobalNamespace.GlobalTypes.GetValueByKey("FILE"), "FILE far*"), 0, 0));
			this.oGlobalNamespace.APIFunctions.Add("fseek", new CFunction(this, CallTypeEnum.Cdecl, "fseek", new List<CParameter>(new CParameter[] { new CParameter(new CType(CTypeEnum.UInt32, this.oGlobalNamespace.GlobalTypes.GetValueByKey("FILE"), "FILE far*"), "__stream"), new CParameter(CType.Long, "__offset"), new CParameter(CType.Int, "__whence") }), CType.Int, 0, 0));
			this.oGlobalNamespace.APIFunctions.Add("fsetpos", new CFunction(this, CallTypeEnum.Cdecl, "fsetpos", new List<CParameter>(new CParameter[] { new CParameter(new CType(CTypeEnum.UInt32, this.oGlobalNamespace.GlobalTypes.GetValueByKey("FILE"), "FILE far*"), "__stream"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("LongFarPtr"), "__pos") }), CType.Int, 0, 0));
			this.oGlobalNamespace.APIFunctions.Add("getcwd", new CFunction(this, CallTypeEnum.Cdecl, "getcwd", new List<CParameter>(new CParameter[] { new CParameter(CType.CharFarPtr, "__buf"), new CParameter(CType.Int, "__buflen") }), CType.CharFarPtr, 0, 0));
			this.oGlobalNamespace.APIFunctions.Add("itoa", new CFunction(this, CallTypeEnum.Cdecl, "itoa", new List<CParameter>(new CParameter[] { new CParameter(CType.Int, "__value"), new CParameter(CType.CharFarPtr, "__string"), new CParameter(CType.Int, "__radix") }), CType.CharFarPtr, 0, 0));
			this.oGlobalNamespace.APIFunctions.Add("memcpy", new CFunction(this, CallTypeEnum.Cdecl, "memcpy", new List<CParameter>(new CParameter[] { new CParameter(CType.ObjectFarPtr, "__dest"), new CParameter(CType.ObjectFarPtr, "__src"), new CParameter(CType.UInt, "__n") }), CType.ObjectFarPtr, 0, 0));
			this.oGlobalNamespace.APIFunctions.Add("memmove", new CFunction(this, CallTypeEnum.Cdecl, "memmove", new List<CParameter>(new CParameter[] { new CParameter(CType.ObjectFarPtr, "__dest"), new CParameter(CType.ObjectFarPtr, "__src"), new CParameter(CType.UInt, "__n") }), CType.ObjectFarPtr, 0, 0));
			this.oGlobalNamespace.APIFunctions.Add("memset", new CFunction(this, CallTypeEnum.Cdecl, "memset", new List<CParameter>(new CParameter[] { new CParameter(CType.ObjectFarPtr, "__s"), new CParameter(CType.Int, "__c"), new CParameter(CType.UInt, "__n") }), CType.ObjectFarPtr, 0, 0));
			this.oGlobalNamespace.APIFunctions.Add("open", new CFunction(this, CallTypeEnum.Cdecl, "open", new List<CParameter>(new CParameter[] { new CParameter(CType.CharFarPtr, "__path"), new CParameter(CType.Int, "__access"), new CParameter(CType.Variable, "param") }), CType.Int, 0, 0));
			this.oGlobalNamespace.APIFunctions.Add("pow", new CFunction(this, CallTypeEnum.Cdecl, "pow", new List<CParameter>(new CParameter[] { new CParameter(CType.Double, "__x"), new CParameter(CType.Double, "__y") }), CType.Double, 0, 0));
			this.oGlobalNamespace.APIFunctions.Add("qsort", new CFunction(this, CallTypeEnum.Cdecl, "qsort", new List<CParameter>(new CParameter[] { new CParameter(CType.ObjectFarPtr, "__base"), new CParameter(CType.UInt, "__nelem"), new CParameter(CType.UInt, "__width"), new CParameter(CType.FunctionFarPtr, "__fcmp") }), CType.Void, 0, 0));
			this.oGlobalNamespace.APIFunctions.Add("rand", new CFunction(this, CallTypeEnum.Cdecl, "rand", new List<CParameter>(new CParameter[] { }), CType.Int, 0, 0));
			this.oGlobalNamespace.APIFunctions.Add("sprintf", new CFunction(this, CallTypeEnum.Cdecl, "sprintf", new List<CParameter>(new CParameter[] { new CParameter(CType.CharFarPtr, "__buffer"), new CParameter(CType.CharFarPtr, "__format"), new CParameter(CType.Variable, "param") }), CType.Int, 0, 0));
			this.oGlobalNamespace.APIFunctions.Add("srand", new CFunction(this, CallTypeEnum.Cdecl, "srand", new List<CParameter>(new CParameter[] { new CParameter(CType.UInt, "__seed") }), CType.Void, 0, 0));
			this.oGlobalNamespace.APIFunctions.Add("strcat", new CFunction(this, CallTypeEnum.Cdecl, "strcat", new List<CParameter>(new CParameter[] { new CParameter(CType.CharFarPtr, "__dest"), new CParameter(CType.CharFarPtr, "__src") }), CType.CharFarPtr, 0, 0));
			this.oGlobalNamespace.APIFunctions.Add("strcmp", new CFunction(this, CallTypeEnum.Cdecl, "strcmp", new List<CParameter>(new CParameter[] { new CParameter(CType.CharFarPtr, "__s1"), new CParameter(CType.CharFarPtr, "__s2") }), CType.Int, 0, 0));
			this.oGlobalNamespace.APIFunctions.Add("stricmp", new CFunction(this, CallTypeEnum.Cdecl, "stricmp", new List<CParameter>(new CParameter[] { new CParameter(CType.CharFarPtr, "__s1"), new CParameter(CType.CharFarPtr, "__s2") }), CType.Int, 0, 0));
			this.oGlobalNamespace.APIFunctions.Add("strcpy", new CFunction(this, CallTypeEnum.Cdecl, "strcpy", new List<CParameter>(new CParameter[] { new CParameter(CType.CharFarPtr, "__dest"), new CParameter(CType.CharFarPtr, "__src") }), CType.CharFarPtr, 0, 0));
			this.oGlobalNamespace.APIFunctions.Add("strlen", new CFunction(this, CallTypeEnum.Cdecl, "strlen", new List<CParameter>(new CParameter[] { new CParameter(CType.CharFarPtr, "__s") }), CType.UInt, 0, 0));
			this.oGlobalNamespace.APIFunctions.Add("strncmp", new CFunction(this, CallTypeEnum.Cdecl, "strncmp", new List<CParameter>(new CParameter[] { new CParameter(CType.CharFarPtr, "__s1"), new CParameter(CType.CharFarPtr, "__s2"), new CParameter(CType.UInt, "__maxlen") }), CType.Int, 0, 0));
			this.oGlobalNamespace.APIFunctions.Add("strstr", new CFunction(this, CallTypeEnum.Cdecl, "strstr", new List<CParameter>(new CParameter[] { new CParameter(CType.CharFarPtr, "__s1"), new CParameter(CType.CharFarPtr, "__s2") }), CType.CharFarPtr, 0, 0));
			this.oGlobalNamespace.APIFunctions.Add("time", new CFunction(this, CallTypeEnum.Cdecl, "time", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("LongFarPtr"), "__timer") }), CType.Long, 0, 0));
			this.oGlobalNamespace.APIFunctions.Add("tolower", new CFunction(this, CallTypeEnum.Cdecl, "tolower", new List<CParameter>(new CParameter[] { new CParameter(CType.Int, "__ch") }), CType.Int, 0, 0));
			this.oGlobalNamespace.APIFunctions.Add("toupper", new CFunction(this, CallTypeEnum.Cdecl, "toupper", new List<CParameter>(new CParameter[] { new CParameter(CType.Int, "__ch") }), CType.Int, 0, 0));
		}

		private void DefineModuleImports()
		{
			ushort usKernel = (ushort)GetModuleSegment("KERNEL");
			
			this.oGlobalNamespace.APIFunctions.Add("GlobalAlloc", new CFunction(this, CallTypeEnum.Pascal, "GlobalAlloc", new List<CParameter>(new CParameter[] { new CParameter(CType.UInt, "fuAlloc"), new CParameter(CType.DWord, "cbAlloc") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("HGLOBAL"), usKernel, 15));
			this.oGlobalNamespace.APIFunctions.Add("GlobalReAlloc", new CFunction(this, CallTypeEnum.Pascal, "GlobalReAlloc", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HGLOBAL"), "hglb"), new CParameter(CType.DWord, "cbNewSize"), new CParameter(CType.UInt, "fuAlloc") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("HGLOBAL"), usKernel, 16));
			this.oGlobalNamespace.APIFunctions.Add("GlobalFree", new CFunction(this, CallTypeEnum.Pascal, "GlobalFree", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HGLOBAL"), "hglb") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("HGLOBAL"), usKernel, 17));
			this.oGlobalNamespace.APIFunctions.Add("GlobalLock", new CFunction(this, CallTypeEnum.Pascal, "GlobalLock", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HGLOBAL"), "hglb") }), CType.ObjectFarPtr, usKernel, 18));
			this.oGlobalNamespace.APIFunctions.Add("GlobalUnlock", new CFunction(this, CallTypeEnum.Pascal, "GlobalUnlock", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HGLOBAL"), "hglb") }), CType.Bool, usKernel, 19));
			this.oGlobalNamespace.APIFunctions.Add("GlobalSize", new CFunction(this, CallTypeEnum.Pascal, "GlobalSize", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HGLOBAL"), "hglb") }), CType.DWord, usKernel, 20));
			this.oGlobalNamespace.APIFunctions.Add("GlobalHandle", new CFunction(this, CallTypeEnum.Pascal, "GlobalHandle", new List<CParameter>(new CParameter[] { new CParameter(CType.UInt, "uGlobalSel") }), CType.DWord, usKernel, 21));
			this.oGlobalNamespace.APIFunctions.Add("GlobalCompact", new CFunction(this, CallTypeEnum.Pascal, "GlobalCompact", new List<CParameter>(new CParameter[] { new CParameter(CType.DWord, "dwMinFree") }), CType.DWord, usKernel, 25));
			this.oGlobalNamespace.APIFunctions.Add("GetModuleFileName", new CFunction(this, CallTypeEnum.Pascal, "GetModuleFileName", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HINSTANCE"), "hinst"), new CParameter(CType.LPSTR, "lpszFilename"), new CParameter(CType.Int, "cbFileName") }), CType.Int, usKernel, 49));
			this.oGlobalNamespace.APIFunctions.Add("MakeProcInstance", new CFunction(this, CallTypeEnum.Pascal, "MakeProcInstance", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("FARPROC"), "lpProc"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HINSTANCE"), "hinst") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("FARPROC"), usKernel, 51));
			this.oGlobalNamespace.APIFunctions.Add("FreeProcInstance", new CFunction(this, CallTypeEnum.Pascal, "FreeProcInstance", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("FARPROC"), "lpProc") }), CType.Void, usKernel, 52));
			this.oGlobalNamespace.APIFunctions.Add("FindResource", new CFunction(this, CallTypeEnum.Pascal, "FindResource", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HINSTANCE"), "hinst"), new CParameter(CType.LPCSTR, "lpszName"), new CParameter(CType.LPCSTR, "lpszType") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("HRSRC"), usKernel, 60));
			this.oGlobalNamespace.APIFunctions.Add("LoadResource", new CFunction(this, CallTypeEnum.Pascal, "LoadResource", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HINSTANCE"), "hinst"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HRSRC"), "hrsrc") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("HGLOBAL"), usKernel, 61));
			this.oGlobalNamespace.APIFunctions.Add("FreeResource", new CFunction(this, CallTypeEnum.Pascal, "FreeResource", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HGLOBAL"), "hglbResource") }), CType.Bool, usKernel, 63));
			this.oGlobalNamespace.APIFunctions.Add("_lclose", new CFunction(this, CallTypeEnum.Pascal, "_lclose", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HFILE"), "hf") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("HFILE"), usKernel, 81));
			this.oGlobalNamespace.APIFunctions.Add("_lread", new CFunction(this, CallTypeEnum.Pascal, "_lread", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HFILE"), "hf"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("VoidHugePtr"), "hpvBuffer"), new CParameter(CType.UInt, "cbBuffer") }), CType.UInt, usKernel, 82));
			this.oGlobalNamespace.APIFunctions.Add("_lcreat", new CFunction(this, CallTypeEnum.Pascal, "_lcreat", new List<CParameter>(new CParameter[] { new CParameter(CType.LPCSTR, "lpszFilename"), new CParameter(CType.Int, "fnAttribute") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("HFILE"), usKernel, 83));
			this.oGlobalNamespace.APIFunctions.Add("_llseek", new CFunction(this, CallTypeEnum.Pascal, "_llseek", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HFILE"), "hf"), new CParameter(CType.Long, "lOffset"), new CParameter(CType.Int, "nOrigin") }), CType.Long, usKernel, 84));
			this.oGlobalNamespace.APIFunctions.Add("_lopen", new CFunction(this, CallTypeEnum.Pascal, "_lopen", new List<CParameter>(new CParameter[] { new CParameter(CType.LPCSTR, "lpszFilename"), new CParameter(CType.Int, "fnOpenMode") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("HFILE"), usKernel, 85));
			this.oGlobalNamespace.APIFunctions.Add("_lwrite", new CFunction(this, CallTypeEnum.Pascal, "_lwrite", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HFILE"), "hf"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("VoidHugePtr"), "hpvBuffer"), new CParameter(CType.UInt, "cbBuffer") }), CType.UInt, usKernel, 86));
			this.oGlobalNamespace.APIFunctions.Add("lstrcpy", new CFunction(this, CallTypeEnum.Pascal, "lstrcpy", new List<CParameter>(new CParameter[] { new CParameter(CType.LPSTR, "lpszString1"), new CParameter(CType.LPCSTR, "lpszString2") }), CType.LPSTR, usKernel, 88));
			this.oGlobalNamespace.APIFunctions.Add("lstrlen", new CFunction(this, CallTypeEnum.Pascal, "lstrlen", new List<CParameter>(new CParameter[] { new CParameter(CType.LPCSTR, "lpszString") }), CType.Int, usKernel, 90));
			this.oGlobalNamespace.APIFunctions.Add("LoadLibrary", new CFunction(this, CallTypeEnum.Pascal, "LoadLibrary", new List<CParameter>(new CParameter[] { new CParameter(CType.LPCSTR, "lpszLibFileName") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("HINSTANCE"), usKernel, 95));
			this.oGlobalNamespace.APIFunctions.Add("GetDOSEnvironment", new CFunction(this, CallTypeEnum.Pascal, "GetDOSEnvironment", new List<CParameter>(new CParameter[] { }), CType.LPSTR, usKernel, 131));
			this.oGlobalNamespace.APIFunctions.Add("GetWinFlags", new CFunction(this, CallTypeEnum.Pascal, "GetWinFlags", new List<CParameter>(new CParameter[] { }), CType.DWord, usKernel, 132));
			this.oGlobalNamespace.APIFunctions.Add("GetFreeSpace", new CFunction(this, CallTypeEnum.Pascal, "GetFreeSpace", new List<CParameter>(new CParameter[] { new CParameter(CType.UInt, "fuFlags") }), CType.DWord, usKernel, 169));
			this.oGlobalNamespace.APIFunctions.Add("hmemcpy", new CFunction(this, CallTypeEnum.Pascal, "hmemcpy", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("VoidHugePtr"), "hpvDest"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("VoidHugePtr"), "hpvSource"), new CParameter(CType.Long, "cbCopy") }), CType.Void, usKernel, 348));
			this.oGlobalNamespace.APIFunctions.Add("_hread", new CFunction(this, CallTypeEnum.Pascal, "_hread", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HFILE"), "hf"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("VoidHugePtr"), "hpvBuffer"), new CParameter(CType.Long, "cbBuffer") }), CType.Long, usKernel, 349));
			this.oGlobalNamespace.APIFunctions.Add("_hwrite", new CFunction(this, CallTypeEnum.Pascal, "_hwrite", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HFILE"), "hf"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("VoidHugePtr"), "hpvBuffer"), new CParameter(CType.Long, "cbBuffer") }), CType.Long, usKernel, 350));

			ushort usUser = (ushort)GetModuleSegment("USER");

			this.oGlobalNamespace.APIFunctions.Add("MessageBox", new CFunction(this, CallTypeEnum.Pascal, "MessageBox", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwndParent"), new CParameter(CType.LPCSTR, "lpszText"), new CParameter(CType.LPCSTR, "lpszTitle"), new CParameter(CType.UInt, "fuStyle") }), CType.Int, usUser, 1));
			this.oGlobalNamespace.APIFunctions.Add("PostQuitMessage", new CFunction(this, CallTypeEnum.Pascal, "PostQuitMessage", new List<CParameter>(new CParameter[] { new CParameter(CType.Int, "nExitCode") }), CType.Void, usUser, 6));
			this.oGlobalNamespace.APIFunctions.Add("SetTimer", new CFunction(this, CallTypeEnum.Pascal, "SetTimer", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwnd"), new CParameter(CType.UInt, "idTimer"), new CParameter(CType.UInt, "uTimeout"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("TIMERPROC"), "tmprc") }), CType.UInt, usUser, 10));
			this.oGlobalNamespace.APIFunctions.Add("KillTimer", new CFunction(this, CallTypeEnum.Pascal, "KillTimer", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwnd"), new CParameter(CType.UInt, "idTimer") }), CType.Bool, usUser, 12));
			this.oGlobalNamespace.APIFunctions.Add("GetTickCount", new CFunction(this, CallTypeEnum.Pascal, "GetTickCount", new List<CParameter>(new CParameter[] { }), CType.DWord, usUser, 13));
			this.oGlobalNamespace.APIFunctions.Add("GetCursorPos", new CFunction(this, CallTypeEnum.Pascal, "GetCursorPos", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("PointFarPtr"), "lppt") }), CType.Void, usUser, 17));
			this.oGlobalNamespace.APIFunctions.Add("SetCapture", new CFunction(this, CallTypeEnum.Pascal, "SetCapture", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwnd") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), usUser, 18));
			this.oGlobalNamespace.APIFunctions.Add("ReleaseCapture", new CFunction(this, CallTypeEnum.Pascal, "ReleaseCapture", new List<CParameter>(new CParameter[] { }), CType.Void, usUser, 19));
			this.oGlobalNamespace.APIFunctions.Add("SetFocus", new CFunction(this, CallTypeEnum.Pascal, "SetFocus", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwnd") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), usUser, 22));
			this.oGlobalNamespace.APIFunctions.Add("ScreenToClient", new CFunction(this, CallTypeEnum.Pascal, "ScreenToClient", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwnd"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("PointFarPtr"), "lppt") }), CType.Void, usUser, 29));
			this.oGlobalNamespace.APIFunctions.Add("GetWindowRect", new CFunction(this, CallTypeEnum.Pascal, "GetWindowRect", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwnd"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("RectFarPtr"), "lprc") }), CType.Void, usUser, 32));
			this.oGlobalNamespace.APIFunctions.Add("GetClientRect", new CFunction(this, CallTypeEnum.Pascal, "GetClientRect", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwnd"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("RectFarPtr"), "lprc") }), CType.Void, usUser, 33));
			this.oGlobalNamespace.APIFunctions.Add("EnableWindow", new CFunction(this, CallTypeEnum.Pascal, "EnableWindow", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwnd"), new CParameter(CType.Bool, "fEnable") }), CType.Bool, usUser, 34));
			this.oGlobalNamespace.APIFunctions.Add("IsWindowEnabled", new CFunction(this, CallTypeEnum.Pascal, "IsWindowEnabled", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwnd") }), CType.Bool, usUser, 35));
			this.oGlobalNamespace.APIFunctions.Add("GetWindowText", new CFunction(this, CallTypeEnum.Pascal, "GetWindowText", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwnd"), new CParameter(CType.LPSTR, "lpsz"), new CParameter(CType.Int, "cbMax") }), CType.Int, usUser, 36));
			this.oGlobalNamespace.APIFunctions.Add("SetWindowText", new CFunction(this, CallTypeEnum.Pascal, "SetWindowText", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwnd"), new CParameter(CType.LPCSTR, "lpsz") }), CType.Void, usUser, 37));
			this.oGlobalNamespace.APIFunctions.Add("BeginPaint", new CFunction(this, CallTypeEnum.Pascal, "BeginPaint", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwnd"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("PaintStructFarPtr"), "lpps") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("HDC"), usUser, 39));
			this.oGlobalNamespace.APIFunctions.Add("EndPaint", new CFunction(this, CallTypeEnum.Pascal, "EndPaint", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwnd"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("PaintStructFarPtr"), "lpps") }), CType.Void, usUser, 40));
			this.oGlobalNamespace.APIFunctions.Add("CreateWindow", new CFunction(this, CallTypeEnum.Pascal, "CreateWindow", new List<CParameter>(new CParameter[] { new CParameter(CType.LPCSTR, "lpszClassName"), new CParameter(CType.LPCSTR, "lpszWindowName"), new CParameter(CType.DWord, "dwStyle"), new CParameter(CType.Int, "x"), new CParameter(CType.Int, "y"), new CParameter(CType.Int, "nWidth"), new CParameter(CType.Int, "nHeight"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwndParent"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HMENU"), "hmenu"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HINSTANCE"), "hinst"), new CParameter(CType.ObjectFarPtr, "lpvParam") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), usUser, 41));
			this.oGlobalNamespace.APIFunctions.Add("ShowWindow", new CFunction(this, CallTypeEnum.Pascal, "ShowWindow", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwnd"), new CParameter(CType.Int, "nCmdShow") }), CType.Bool, usUser, 42));
			this.oGlobalNamespace.APIFunctions.Add("GetParent", new CFunction(this, CallTypeEnum.Pascal, "GetParent", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwnd") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), usUser, 46));
			this.oGlobalNamespace.APIFunctions.Add("IsChild", new CFunction(this, CallTypeEnum.Pascal, "IsChild", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwndParent"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwndChild") }), CType.Bool, usUser, 48));
			this.oGlobalNamespace.APIFunctions.Add("DestroyWindow", new CFunction(this, CallTypeEnum.Pascal, "DestroyWindow", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwnd") }), CType.Bool, usUser, 53));
			this.oGlobalNamespace.APIFunctions.Add("MoveWindow", new CFunction(this, CallTypeEnum.Pascal, "MoveWindow", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwnd"), new CParameter(CType.Int, "nLeft"), new CParameter(CType.Int, "nTop"), new CParameter(CType.Int, "nWidth"), new CParameter(CType.Int, "nHeight"), new CParameter(CType.Bool, "fRepaint") }), CType.Bool, usUser, 56));
			this.oGlobalNamespace.APIFunctions.Add("RegisterClass", new CFunction(this, CallTypeEnum.Pascal, "RegisterClass", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("WNDCLASS"), "lpwc") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("ATOM"), usUser, 57));
			this.oGlobalNamespace.APIFunctions.Add("SetScrollPos", new CFunction(this, CallTypeEnum.Pascal, "SetScrollPos", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwnd"), new CParameter(CType.Int, "fnBar"), new CParameter(CType.Int, "nPos"), new CParameter(CType.Bool, "fRepaint") }), CType.Int, usUser, 62));
			this.oGlobalNamespace.APIFunctions.Add("SetScrollRange", new CFunction(this, CallTypeEnum.Pascal, "SetScrollRange", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwnd"), new CParameter(CType.Int, "fnBar"), new CParameter(CType.Int, "nMin"), new CParameter(CType.Int, "nMax"), new CParameter(CType.Bool, "fRedraw") }), CType.Void, usUser, 64));
			this.oGlobalNamespace.APIFunctions.Add("GetScrollRange", new CFunction(this, CallTypeEnum.Pascal, "GetScrollRange", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwnd"), new CParameter(CType.Int, "fnBar"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("IntFarPtr"), "lpnMinPos"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("IntFarPtr"), "lpnMaxPos") }), CType.Void, usUser, 65));
			this.oGlobalNamespace.APIFunctions.Add("GetDC", new CFunction(this, CallTypeEnum.Pascal, "GetDC", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwnd") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("HDC"), usUser, 66));
			this.oGlobalNamespace.APIFunctions.Add("ReleaseDC", new CFunction(this, CallTypeEnum.Pascal, "ReleaseDC", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwnd"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HDC"), "hdc") }), CType.Int, usUser, 68));
			this.oGlobalNamespace.APIFunctions.Add("SetCursor", new CFunction(this, CallTypeEnum.Pascal, "SetCursor", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HCURSOR"), "hcur") }), CType.Void, usUser, 69));
			this.oGlobalNamespace.APIFunctions.Add("SetRect", new CFunction(this, CallTypeEnum.Pascal, "SetRect", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("RectFarPtr"), "lprc"), new CParameter(CType.Int, "nLeft"), new CParameter(CType.Int, "nTop"), new CParameter(CType.Int, "nRight"), new CParameter(CType.Int, "nBottom") }), CType.Void, usUser, 72));
			this.oGlobalNamespace.APIFunctions.Add("PtInRect", new CFunction(this, CallTypeEnum.Pascal, "PtInRect", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("RectFarPtr"), "lprc"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("POINT"), "pt") }), CType.Bool, usUser, 76));
			this.oGlobalNamespace.APIFunctions.Add("OffsetRect", new CFunction(this, CallTypeEnum.Pascal, "OffsetRect", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("RectFarPtr"), "lprc"), new CParameter(CType.Int, "x"), new CParameter(CType.Int, "y") }), CType.Void, usUser, 77));
			this.oGlobalNamespace.APIFunctions.Add("InflateRect", new CFunction(this, CallTypeEnum.Pascal, "InflateRect", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("RectFarPtr"), "lprc"), new CParameter(CType.Int, "xAmt"), new CParameter(CType.Int, "yAmt") }), CType.Void, usUser, 78));
			this.oGlobalNamespace.APIFunctions.Add("FillRect", new CFunction(this, CallTypeEnum.Pascal, "FillRect", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HDC"), "hdc"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("RectFarPtr"), "lprc"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HBRUSH"), "hbr") }), CType.Int, usUser, 81));
			this.oGlobalNamespace.APIFunctions.Add("FrameRect", new CFunction(this, CallTypeEnum.Pascal, "FrameRect", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HDC"), "hdc"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("RectFarPtr"), "lprc"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HBRUSH"), "hbr") }), CType.Int, usUser, 83));
			this.oGlobalNamespace.APIFunctions.Add("DrawText", new CFunction(this, CallTypeEnum.Pascal, "DrawText", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HDC"), "hdc"), new CParameter(CType.LPCSTR, "lpsz"), new CParameter(CType.Int, "cb"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("RectFarPtr"), "lprc"), new CParameter(CType.UInt, "fuFormat") }), CType.Int, usUser, 85));
			this.oGlobalNamespace.APIFunctions.Add("CreateDialog", new CFunction(this, CallTypeEnum.Pascal, "CreateDialog", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HINSTANCE"), "hinst"), new CParameter(CType.LPCSTR, "lpszDlgTemp"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwndOwner"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("DLGPROC"), "dlgprc") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), usUser, 89));
			this.oGlobalNamespace.APIFunctions.Add("IsDialogMessage", new CFunction(this, CallTypeEnum.Pascal, "IsDialogMessage", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwndDlg"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("MsgFarPtr"), "lpmsg") }), CType.Bool, usUser, 90));
			this.oGlobalNamespace.APIFunctions.Add("GetDlgItem", new CFunction(this, CallTypeEnum.Pascal, "GetDlgItem", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwndDlg"), new CParameter(CType.Int, "idControl") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), usUser, 91));
			this.oGlobalNamespace.APIFunctions.Add("SendDlgItemMessage", new CFunction(this, CallTypeEnum.Pascal, "SendDlgItemMessage", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwndDlg"), new CParameter(CType.Int, "idDlgItem"), new CParameter(CType.UInt, "uMsg"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("WPARAM"), "wParam"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("LPARAM"), "lParam") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("LRESULT"), usUser, 101));
			this.oGlobalNamespace.APIFunctions.Add("MessageBeep", new CFunction(this, CallTypeEnum.Pascal, "MessageBeep", new List<CParameter>(new CParameter[] { new CParameter(CType.UInt, "uAlert") }), CType.Void, usUser, 104));
			this.oGlobalNamespace.APIFunctions.Add("DefWindowProc", new CFunction(this, CallTypeEnum.Pascal, "DefWindowProc", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwnd"), new CParameter(CType.UInt, "uMsg"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("WPARAM"), "wParam"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("LPARAM"), "lParam") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("LRESULT"), usUser, 107));
			this.oGlobalNamespace.APIFunctions.Add("PeekMessage", new CFunction(this, CallTypeEnum.Pascal, "PeekMessage", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("MsgFarPtr"), "lpmsg"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwnd"), new CParameter(CType.UInt, "uFilterFirst"), new CParameter(CType.UInt, "uFilterLast"), new CParameter(CType.UInt, "fuRemove") }), CType.Bool, usUser, 109));
			this.oGlobalNamespace.APIFunctions.Add("SendMessage", new CFunction(this, CallTypeEnum.Pascal, "SendMessage", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwnd"), new CParameter(CType.UInt, "uMsg"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("WPARAM"), "wParam"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("LPARAM"), "lParam") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("LRESULT"), usUser, 111));
			this.oGlobalNamespace.APIFunctions.Add("TranslateMessage", new CFunction(this, CallTypeEnum.Pascal, "TranslateMessage", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("MsgFarPtr"), "lpmsg") }), CType.Bool, usUser, 113));
			this.oGlobalNamespace.APIFunctions.Add("DispatchMessage", new CFunction(this, CallTypeEnum.Pascal, "DispatchMessage", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("MsgFarPtr"), "lpmsg") }), CType.Long, usUser, 114));
			this.oGlobalNamespace.APIFunctions.Add("UpdateWindow", new CFunction(this, CallTypeEnum.Pascal, "UpdateWindow", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwnd") }), CType.Void, usUser, 124));
			this.oGlobalNamespace.APIFunctions.Add("InvalidateRect", new CFunction(this, CallTypeEnum.Pascal, "InvalidateRect", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwnd"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("RectFarPtr"), "lprc"), new CParameter(CType.Bool, "fErase") }), CType.Void, usUser, 125));
			this.oGlobalNamespace.APIFunctions.Add("ValidateRect", new CFunction(this, CallTypeEnum.Pascal, "ValidateRect", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwnd"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("RectFarPtr"), "lprc") }), CType.Void, usUser, 127));
			this.oGlobalNamespace.APIFunctions.Add("GetWindowWord", new CFunction(this, CallTypeEnum.Pascal, "GetWindowWord", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwnd"), new CParameter(CType.Int, "nOffset") }), CType.Word, usUser, 133));
			this.oGlobalNamespace.APIFunctions.Add("SetWindowWord", new CFunction(this, CallTypeEnum.Pascal, "SetWindowWord", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwnd"), new CParameter(CType.Int, "nOffset"), new CParameter(CType.Word, "nVal") }), CType.Word, usUser, 134));
			this.oGlobalNamespace.APIFunctions.Add("SetWindowLong", new CFunction(this, CallTypeEnum.Pascal, "SetWindowLong", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwnd"), new CParameter(CType.Int, "nOffset"), new CParameter(CType.Long, "nVal") }), CType.Long, usUser, 136));
			this.oGlobalNamespace.APIFunctions.Add("CheckMenuItem", new CFunction(this, CallTypeEnum.Pascal, "CheckMenuItem", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HMENU"), "hmenu"), new CParameter(CType.UInt, "idCheckItem"), new CParameter(CType.UInt, "uCheck") }), CType.Bool, usUser, 154));
			this.oGlobalNamespace.APIFunctions.Add("EnableMenuItem", new CFunction(this, CallTypeEnum.Pascal, "EnableMenuItem", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HMENU"), "hmenu"), new CParameter(CType.UInt, "idEnableItem"), new CParameter(CType.UInt, "uEnable") }), CType.Bool, usUser, 155));
			this.oGlobalNamespace.APIFunctions.Add("GetMenu", new CFunction(this, CallTypeEnum.Pascal, "GetMenu", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwnd") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("HMENU"), usUser, 157));
			this.oGlobalNamespace.APIFunctions.Add("DrawMenuBar", new CFunction(this, CallTypeEnum.Pascal, "DrawMenuBar", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwnd") }), CType.Void, usUser, 160));
			this.oGlobalNamespace.APIFunctions.Add("WinHelp", new CFunction(this, CallTypeEnum.Pascal, "WinHelp", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwnd"), new CParameter(CType.LPCSTR, "lpszHelpFile"), new CParameter(CType.UInt, "fuCommand"), new CParameter(CType.DWord, "dwData") }), CType.Bool, usUser, 171));
			this.oGlobalNamespace.APIFunctions.Add("LoadCursor", new CFunction(this, CallTypeEnum.Pascal, "LoadCursor", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HINSTANCE"), "hinst"), new CParameter(CType.LPCSTR, "pszCursor") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("HCURSOR"), usUser, 173));
			this.oGlobalNamespace.APIFunctions.Add("LoadIcon", new CFunction(this, CallTypeEnum.Pascal, "LoadIcon", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HINSTANCE"), "hinst"), new CParameter(CType.LPCSTR, "pszIcon") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("HICON"), usUser, 174));
			this.oGlobalNamespace.APIFunctions.Add("LoadAccelerators", new CFunction(this, CallTypeEnum.Pascal, "LoadAccelerators", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HINSTANCE"), "hinst"), new CParameter(CType.LPCSTR, "lpszTableName") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("HACCEL"), usUser, 177));
			this.oGlobalNamespace.APIFunctions.Add("TranslateAccelerator", new CFunction(this, CallTypeEnum.Pascal, "TranslateAccelerator", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwnd"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HACCEL"), "haccl"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("MsgFarPtr"), "lpmsg") }), CType.Int, usUser, 178));
			this.oGlobalNamespace.APIFunctions.Add("GetSystemMetrics", new CFunction(this, CallTypeEnum.Pascal, "GetSystemMetrics", new List<CParameter>(new CParameter[] { new CParameter(CType.Int, "nIndex") }), CType.Int, usUser, 179));
			this.oGlobalNamespace.APIFunctions.Add("CreateDialogIndirect", new CFunction(this, CallTypeEnum.Pascal, "CreateDialogIndirect", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HINSTANCE"), "hinst"), new CParameter(CType.ObjectFarPtr, "lpvDlgTmp"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwndOwner"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("DLGPROC"), "dlgprc") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), usUser, 219));
			this.oGlobalNamespace.APIFunctions.Add("GetKeyboardState", new CFunction(this, CallTypeEnum.Pascal, "GetKeyboardState", new List<CParameter>(new CParameter[] { new CParameter(new CType(CType.Byte, 256, "byte[]"), "lpbKeyState") }), CType.Void, usUser, 222));
			this.oGlobalNamespace.APIFunctions.Add("SetKeyboardState", new CFunction(this, CallTypeEnum.Pascal, "SetKeyboardState", new List<CParameter>(new CParameter[] { new CParameter(new CType(CType.Byte, 256, "byte[]"), "lpbKeyState") }), CType.Void, usUser, 223));
			this.oGlobalNamespace.APIFunctions.Add("GetDialogBaseUnits", new CFunction(this, CallTypeEnum.Pascal, "GetDialogBaseUnits", new List<CParameter>(new CParameter[] { new CParameter(CType.Void) }), CType.DWord, usUser, 243));
			this.oGlobalNamespace.APIFunctions.Add("GetDlgCtrlID", new CFunction(this, CallTypeEnum.Pascal, "GetDlgCtrlID", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwnd") }), CType.Int, usUser, 277));
			this.oGlobalNamespace.APIFunctions.Add("SelectPalette", new CFunction(this, CallTypeEnum.Pascal, "SelectPalette", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HDC"), "hdc"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HPALETTE"), "hpal"), new CParameter(CType.Bool, "fPalBack") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("HPALETTE"), usUser, 282));
			this.oGlobalNamespace.APIFunctions.Add("RealizePalette", new CFunction(this, CallTypeEnum.Pascal, "RealizePalette", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HDC"), "hdc") }), CType.UInt, usUser, 283));
			this.oGlobalNamespace.APIFunctions.Add("GetFreeSystemResources", new CFunction(this, CallTypeEnum.Pascal, "GetFreeSystemResources", new List<CParameter>(new CParameter[] { new CParameter(CType.UInt, "fuSysResource") }), CType.UInt, usUser, 284));
			this.oGlobalNamespace.APIFunctions.Add("GetDesktopWindow", new CFunction(this, CallTypeEnum.Pascal, "GetDesktopWindow", new List<CParameter>(new CParameter[] { new CParameter(CType.Void) }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), usUser, 286));
			this.oGlobalNamespace.APIFunctions.Add("DefDlgProc", new CFunction(this, CallTypeEnum.Pascal, "DefDlgProc", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HWND"), "hwndDlg"), new CParameter(CType.UInt, "uMsg"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("WPARAM"), "wParam"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("LPARAM"), "lParam") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("LRESULT"), usUser, 308));
			this.oGlobalNamespace.APIFunctions.Add("ModifyMenu", new CFunction(this, CallTypeEnum.Pascal, "ModifyMenu", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HMENU"), "hmenu"), new CParameter(CType.UInt, "idItem"), new CParameter(CType.UInt, "fuFlags"), new CParameter(CType.UInt, "idNewItem"), new CParameter(CType.LPCSTR, "lpNewItem") }), CType.Bool, usUser, 414));

			ushort usGdi = (ushort)GetModuleSegment("GDI");

			this.oGlobalNamespace.APIFunctions.Add("SetBkColor", new CFunction(this, CallTypeEnum.Pascal, "SetBkColor", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HDC"), "hdc"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("COLORREF"), "clrref") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("COLORREF"), usGdi, 1));
			this.oGlobalNamespace.APIFunctions.Add("SetBkMode", new CFunction(this, CallTypeEnum.Pascal, "SetBkMode", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HDC"), "hdc"), new CParameter(CType.Int, "fnBkMode") }), CType.Int, usGdi, 2));
			this.oGlobalNamespace.APIFunctions.Add("SetPolyFillMode", new CFunction(this, CallTypeEnum.Pascal, "SetPolyFillMode", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HDC"), "hdc"), new CParameter(CType.Int, "fnMode") }), CType.Int, usGdi, 6));
			this.oGlobalNamespace.APIFunctions.Add("SetTextColor", new CFunction(this, CallTypeEnum.Pascal, "SetTextColor", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HDC"), "hdc"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("COLORREF"), "clrref") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("COLORREF"), usGdi, 9));
			this.oGlobalNamespace.APIFunctions.Add("LineTo", new CFunction(this, CallTypeEnum.Pascal, "LineTo", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HDC"), "hdc"), new CParameter(CType.Int, "nXEnd"), new CParameter(CType.Int, "nYEnd") }), CType.Bool, usGdi, 19));
			this.oGlobalNamespace.APIFunctions.Add("MoveTo", new CFunction(this, CallTypeEnum.Pascal, "MoveTo", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HDC"), "hdc"), new CParameter(CType.Int, "nXPos"), new CParameter(CType.Int, "nYPos") }), CType.DWord, usGdi, 20));
			this.oGlobalNamespace.APIFunctions.Add("SetPixel", new CFunction(this, CallTypeEnum.Pascal, "SetPixel", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HDC"), "hdc"), new CParameter(CType.Int, "nXPos"), new CParameter(CType.Int, "nYPos"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("COLORREF"), "clrref") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("COLORREF"), usGdi, 31));
			this.oGlobalNamespace.APIFunctions.Add("TextOut", new CFunction(this, CallTypeEnum.Pascal, "TextOut", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HDC"), "hdc"), new CParameter(CType.Int, "nXStart"), new CParameter(CType.Int, "nYStart"), new CParameter(CType.LPCSTR, "lpszString"), new CParameter(CType.Int, "cbString") }), CType.Bool, usGdi, 33));
			this.oGlobalNamespace.APIFunctions.Add("BitBlt", new CFunction(this, CallTypeEnum.Pascal, "BitBlt", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HDC"), "hdcDest"), new CParameter(CType.Int, "nXDest"), new CParameter(CType.Int, "nYDest"), new CParameter(CType.Int, "nWidth"), new CParameter(CType.Int, "nHeight"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HDC"), "hdcSrc"), new CParameter(CType.Int, "nXSrc"), new CParameter(CType.Int, "nYSrc"), new CParameter(CType.DWord, "dwRop") }), CType.Bool, usGdi, 34));
			this.oGlobalNamespace.APIFunctions.Add("Polygon", new CFunction(this, CallTypeEnum.Pascal, "Polygon", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HDC"), "hdc"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("PointFarPtr"), "lppt"), new CParameter(CType.Int, "cPoints") }), CType.Bool, usGdi, 36));
			this.oGlobalNamespace.APIFunctions.Add("Polyline", new CFunction(this, CallTypeEnum.Pascal, "Polyline", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HDC"), "hdc"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("PointFarPtr"), "lppt"), new CParameter(CType.Int, "cPoints") }), CType.Bool, usGdi, 37));
			this.oGlobalNamespace.APIFunctions.Add("SelectObject", new CFunction(this, CallTypeEnum.Pascal, "SelectObject", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HDC"), "hdc"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HGDIOBJ"), "hgdiobj") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("HGDIOBJ"), usGdi, 45));
			this.oGlobalNamespace.APIFunctions.Add("CreateBitmap", new CFunction(this, CallTypeEnum.Pascal, "CreateBitmap", new List<CParameter>(new CParameter[] { new CParameter(CType.Int, "nWidth"), new CParameter(CType.Int, "nHeight"), new CParameter(CType.UInt, "cbPlanes"), new CParameter(CType.UInt, "cbBits"), new CParameter(CType.ObjectFarPtr, "lpvBits") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("HBITMAP"), usGdi, 48));
			this.oGlobalNamespace.APIFunctions.Add("CreateCompatibleDC", new CFunction(this, CallTypeEnum.Pascal, "CreateCompatibleDC", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HDC"), "hdc") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("HDC"), usGdi, 52));
			this.oGlobalNamespace.APIFunctions.Add("CreateFont", new CFunction(this, CallTypeEnum.Pascal, "CreateFont", new List<CParameter>(new CParameter[] { new CParameter(CType.Int, "nHeight"), new CParameter(CType.Int, "nWidth"), new CParameter(CType.Int, "nEscapement"), new CParameter(CType.Int, "nOrientation"), new CParameter(CType.Int, "fnWeight"), new CParameter(CType.Byte, "fbItalic"), new CParameter(CType.Byte, "fbUnderline"), new CParameter(CType.Byte, "fbStrikeOut"), new CParameter(CType.Byte, "fbCharSet"), new CParameter(CType.Byte, "fbOutputPrecision"), new CParameter(CType.Byte, "fbClipPrecision"), new CParameter(CType.Byte, "fbQuality"), new CParameter(CType.Byte, "fbPitchAndFamily"), new CParameter(CType.LPCSTR, "lpszFace") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("HFONT"), usGdi, 56));
			this.oGlobalNamespace.APIFunctions.Add("CreateFontIndirect", new CFunction(this, CallTypeEnum.Pascal, "CreateFontIndirect", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("LogFontFarPtr"), "lplf") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("HFONT"), usGdi, 57));
			this.oGlobalNamespace.APIFunctions.Add("CreatePatternBrush", new CFunction(this, CallTypeEnum.Pascal, "CreatePatternBrush", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HBITMAP"), "hbmp") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("HBRUSH"), usGdi, 60));
			this.oGlobalNamespace.APIFunctions.Add("CreatePen", new CFunction(this, CallTypeEnum.Pascal, "CreatePen", new List<CParameter>(new CParameter[] { new CParameter(CType.Int, "fnPenStyle"), new CParameter(CType.Int, "nWidth"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("COLORREF"), "clrref") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("HPEN"), usGdi, 61));
			this.oGlobalNamespace.APIFunctions.Add("CreateSolidBrush", new CFunction(this, CallTypeEnum.Pascal, "CreateSolidBrush", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("COLORREF"), "clrref") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("HBRUSH"), usGdi, 66));
			this.oGlobalNamespace.APIFunctions.Add("DeleteDC", new CFunction(this, CallTypeEnum.Pascal, "DeleteDC", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HDC"), "hdc") }), CType.Bool, usGdi, 68));
			this.oGlobalNamespace.APIFunctions.Add("DeleteObject", new CFunction(this, CallTypeEnum.Pascal, "DeleteObject", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HGDIOBJ"), "hgdiobj") }), CType.Bool, usGdi, 69));
			this.oGlobalNamespace.APIFunctions.Add("GetBitmapBits", new CFunction(this, CallTypeEnum.Pascal, "GetBitmapBits", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HBITMAP"), "hbm"), new CParameter(CType.Long, "cbBuffer"), new CParameter(CType.ObjectFarPtr, "lpvBits") }), CType.Long, usGdi, 74));
			this.oGlobalNamespace.APIFunctions.Add("GetBkColor", new CFunction(this, CallTypeEnum.Pascal, "GetBkColor", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HDC"), "hdc") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("COLORREF"), usGdi, 75));
			this.oGlobalNamespace.APIFunctions.Add("GetBkMode", new CFunction(this, CallTypeEnum.Pascal, "GetBkMode", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HDC"), "hdc") }), CType.Int, usGdi, 76));
			this.oGlobalNamespace.APIFunctions.Add("GetDeviceCaps", new CFunction(this, CallTypeEnum.Pascal, "GetDeviceCaps", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HDC"), "hdc"), new CParameter(CType.Int, "iCapability") }), CType.Int, usGdi, 80));
			this.oGlobalNamespace.APIFunctions.Add("GetPixel", new CFunction(this, CallTypeEnum.Pascal, "GetPixel", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HDC"), "hdc"), new CParameter(CType.Int, "nXPos"), new CParameter(CType.Int, "nYPos") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("COLORREF"), usGdi, 83));
			this.oGlobalNamespace.APIFunctions.Add("GetStockObject", new CFunction(this, CallTypeEnum.Pascal, "GetStockObject", new List<CParameter>(new CParameter[] { new CParameter(CType.Int, "fnObject") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("HGDIOBJ"), usGdi, 87));
			this.oGlobalNamespace.APIFunctions.Add("GetTextColor", new CFunction(this, CallTypeEnum.Pascal, "GetTextColor", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HDC"), "hdc") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("COLORREF"), usGdi, 90));
			this.oGlobalNamespace.APIFunctions.Add("GetTextExtent", new CFunction(this, CallTypeEnum.Pascal, "GetTextExtent", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HDC"), "hdc"), new CParameter(CType.LPCSTR, "lpszString"), new CParameter(CType.Int, "cbString") }), CType.DWord, usGdi, 91));
			this.oGlobalNamespace.APIFunctions.Add("GetTextMetrics", new CFunction(this, CallTypeEnum.Pascal, "GetTextMetrics", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HDC"), "hdc"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("TextMetricFarPtr"), "lptm") }), CType.Bool, usGdi, 93));
			this.oGlobalNamespace.APIFunctions.Add("SetBitmapBits", new CFunction(this, CallTypeEnum.Pascal, "SetBitmapBits", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HBITMAP"), "hbmp"), new CParameter(CType.DWord, "cBits"), new CParameter(CType.ObjectFarPtr, "lpvBits") }), CType.Long, usGdi, 106));
			this.oGlobalNamespace.APIFunctions.Add("AddFontResource", new CFunction(this, CallTypeEnum.Pascal, "AddFontResource", new List<CParameter>(new CParameter[] { new CParameter(CType.LPCSTR, "lpszFilename") }), CType.Int, usGdi, 119));
			this.oGlobalNamespace.APIFunctions.Add("RemoveFontResource", new CFunction(this, CallTypeEnum.Pascal, "RemoveFontResource", new List<CParameter>(new CParameter[] { new CParameter(CType.LPCSTR, "lpszFile") }), CType.Bool, usGdi, 136));
			this.oGlobalNamespace.APIFunctions.Add("UnrealizeObject", new CFunction(this, CallTypeEnum.Pascal, "UnrealizeObject", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HGDIOBJ"), "hgdiobj") }), CType.Bool, usGdi, 150));
			this.oGlobalNamespace.APIFunctions.Add("GetBitmapDimension", new CFunction(this, CallTypeEnum.Pascal, "GetBitmapDimension", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HBITMAP"), "hbm") }), CType.DWord, usGdi, 162));
			this.oGlobalNamespace.APIFunctions.Add("SetBitmapDimension", new CFunction(this, CallTypeEnum.Pascal, "SetBitmapDimension", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HBITMAP"), "hbmp"), new CParameter(CType.Int, "nWidth"), new CParameter(CType.Int, "nHeight") }), CType.DWord, usGdi, 163));
			this.oGlobalNamespace.APIFunctions.Add("EnumFontFamilies", new CFunction(this, CallTypeEnum.Pascal, "EnumFontFamilies", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HDC"), "hdc"), new CParameter(CType.LPCSTR, "lpszFamily"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("FONTENUMPROC"), "fntenmprc"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("LPARAM"), "lParam") }), CType.Int, usGdi, 330));
			this.oGlobalNamespace.APIFunctions.Add("SetTextAlign", new CFunction(this, CallTypeEnum.Pascal, "SetTextAlign", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HDC"), "hdc"), new CParameter(CType.UInt, "fuAlign") }), CType.UInt, usGdi, 346));
			this.oGlobalNamespace.APIFunctions.Add("CreatePalette", new CFunction(this, CallTypeEnum.Pascal, "CreatePalette", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("LogPaletteFarPtr"), "lplgpl") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("HPALETTE"), usGdi, 360));
			this.oGlobalNamespace.APIFunctions.Add("SetPaletteEntries", new CFunction(this, CallTypeEnum.Pascal, "SetPaletteEntries", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HPALETTE"), "hpal"), new CParameter(CType.UInt, "iStart"), new CParameter(CType.UInt, "cEntries"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("PaletteEntryFarPtr"), "lppe") }), CType.UInt, usGdi, 364));
			this.oGlobalNamespace.APIFunctions.Add("AnimatePalette", new CFunction(this, CallTypeEnum.Pascal, "AnimatePalette", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HPALETTE"), "hpal"), new CParameter(CType.UInt, "iStart"), new CParameter(CType.UInt, "cEntries"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("PaletteEntryFarPtr"), "lppe") }), CType.Void, usGdi, 367));
			this.oGlobalNamespace.APIFunctions.Add("GetSystemPaletteEntries", new CFunction(this, CallTypeEnum.Pascal, "GetSystemPaletteEntries", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HDC"), "hdc"), new CParameter(CType.UInt, "iStart"), new CParameter(CType.UInt, "cEntries"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("PaletteEntryFarPtr"), "lppe") }), CType.UInt, usGdi, 375));
			this.oGlobalNamespace.APIFunctions.Add("CreateDIBitmap", new CFunction(this, CallTypeEnum.Pascal, "CreateDIBitmap", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("HDC"), "hdc"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("BitmapInfoHeaderFarPtr"), "lpbmih"), new CParameter(CType.DWord, "dwInit"), new CParameter(CType.ObjectFarPtr, "lpvBits"), new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("BitmapInfoFarPtr"), "lpbmi"), new CParameter(CType.UInt, "fnColorUse") }), this.oGlobalNamespace.GlobalTypes.GetValueByKey("HBITMAP"), usGdi, 442));

			ushort usMMSystem = (ushort)GetModuleSegment("MMSYSTEM");

			this.oGlobalNamespace.APIFunctions.Add("sndPlaySound", new CFunction(this, CallTypeEnum.Pascal, "sndPlaySound", new List<CParameter>(new CParameter[] { new CParameter(CType.LPCSTR, "lpszSoundName"), new CParameter(CType.UInt, "wFlags") }), CType.Bool, usMMSystem, 2));
			this.oGlobalNamespace.APIFunctions.Add("mciSendCommand", new CFunction(this, CallTypeEnum.Pascal, "mciSendCommand", new List<CParameter>(new CParameter[] { new CParameter(CType.UInt, "wDeviceID"), new CParameter(CType.UInt, "wMessage"), new CParameter(CType.DWord, "dwParam1"), new CParameter(CType.DWord, "dwParam2") }), CType.DWord, usMMSystem, 701));

			ushort usCommDlg = (ushort)GetModuleSegment("COMMDLG");

			this.oGlobalNamespace.APIFunctions.Add("GetOpenFileName", new CFunction(this, CallTypeEnum.Pascal, "GetOpenFileName", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("OpenFilenameFarPtr"), "lpofn") }), CType.Bool, usCommDlg, 1));
			this.oGlobalNamespace.APIFunctions.Add("GetSaveFileName", new CFunction(this, CallTypeEnum.Pascal, "GetSaveFileName", new List<CParameter>(new CParameter[] { new CParameter(this.oGlobalNamespace.GlobalTypes.GetValueByKey("OpenFilenameFarPtr"), "lpofn") }), CType.Bool, usCommDlg, 2));
			this.oGlobalNamespace.APIFunctions.Add("CommDlgExtendedError", new CFunction(this, CallTypeEnum.Pascal, "CommDlgExtendedError", new List<CParameter>(new CParameter[] { }), CType.DWord, usCommDlg, 26));
		}

		private uint GetModuleSegment(string moduleName)
		{
			for (int i = 0; i < this.oExecutable.ModuleReferences.Count; i++)
			{
				if (this.oExecutable.ModuleReferences[i].Equals(moduleName, StringComparison.CurrentCultureIgnoreCase))
				{
					return (uint)(this.oExecutable.Segments.Count + i);
				}
			}

			throw new Exception("Can't find module segment");
		}

		public CFunction GetFunction(ushort segment, ushort offset)
		{
			uint uiAddress = (uint)((uint)segment << 16 | offset);

			// look into function list
			for (int i = 0; i < this.oGlobalNamespace.Functions.Count; i++)
			{
				CFunction function = this.oGlobalNamespace.Functions[i].Value;
				//Console.WriteLine("Function at {0}:0x{1:x}", function.Segment, function.Offset);
				if (function.Segment == segment && function.Offset == offset)
				{
					return function;
				}
			}

			// look into module imports and C API functions
			for (int i = 0; i < this.oGlobalNamespace.APIFunctions.Count; i++)
			{
				CFunction function = this.oGlobalNamespace.APIFunctions[i].Value;
				if (function.Segment == segment && function.Offset == offset)
				{
					return function;
				}
			}

			// look into library matches
			for (int i = 0; i < this.aLibraryMatches.Count; i++)
			{
				ModuleMatch match = this.aLibraryMatches[i];

				if (uiAddress >= match.LinearAddress && uiAddress < match.LinearAddress + match.Length)
				{
					OBJModule module = match.Module;
					for (int j = 0; j < module.PublicNames.Count; j++)
					{
						PublicNameDefinition publicNameDef = module.PublicNames[j];
						for (int k = 0; k < publicNameDef.PublicNames.Count; k++)
						{
							if (publicNameDef.PublicNames[k].Value == uiAddress - match.LinearAddress)
							{
								string sName = publicNameDef.PublicNames[k].Key;
								sName = sName.StartsWith("_") ? sName.Substring(1) : sName;

								CFunction function = null;
								if (this.oGlobalNamespace.APIFunctions.ContainsKey(sName))
									function = this.oGlobalNamespace.APIFunctions.GetValueByKey(sName);

								if (function != null)
								{
									// Console.WriteLine("Adding C API function {0} at {1}:0x{2:x}", sName, segment, offset);
									function = new CFunction(this, CallTypeEnum.Cdecl, function.Name, function.Parameters, function.ReturnValue, segment, offset);
									this.oGlobalNamespace.APIFunctions.Add(function.Name, function);
								}
								else
								{
									// add missing function
									Console.WriteLine("Adding undefined API function {0} at {1}:0x{2:x}", sName, segment, offset);
									function = new CFunction(this, CallTypeEnum.Cdecl, sName, new List<CParameter>(), CType.Void, segment, offset);
									this.oGlobalNamespace.APIFunctions.Add(sName, function);
								}

								return function;
							}
						}
					}
				}
			}

			// is this a jump to library function
			if (segment == 0)
			{
				MemoryStream stream = new MemoryStream(this.oExecutable.Segments[(int)segment].Data);
				stream.Seek(offset, SeekOrigin.Begin);
				Instruction instruction = new Instruction(segment, offset, stream);
				stream.Close();

				if (instruction.InstructionType == InstructionEnum.JMP &&
					instruction.Parameters.Count == 1 &&
					instruction.Parameters[0].Type == InstructionParameterTypeEnum.Relative)
				{
					ushort usNewOffset = (ushort)((offset + (uint)instruction.Bytes.Count + instruction.Parameters[0].Value) & 0xffff);

					CFunction function1 = this.GetFunction(segment, usNewOffset);
					if (function1 != null)
					{
						// we found the real function, copy it
						CFunction function2 = new CFunction(this, CallTypeEnum.Cdecl, function1.Name, function1.Parameters, function1.ReturnValue, segment, offset);
						this.oGlobalNamespace.APIFunctions.Add(function1.Name, function2);

						return function2;
					}
				}

				Console.WriteLine("Can't match library module in segment {0}, offset 0x{1:x}", segment, offset);

				// add missing function
				string sName = string.Format("F{0}_{1:x}", segment, offset);
				CFunction function = new CFunction(this, CallTypeEnum.Undefined, sName, new List<CParameter>(), CType.Void, segment, offset);
				this.oGlobalNamespace.APIFunctions.Add(sName, function);
				function.Disassemble(this);

				return function;
			}

			return null;
		}

		public void SortFunctionsByAddress()
		{
			List<CFunction> aFunctions = new List<CFunction>();

			for (int i = 0; i < this.oGlobalNamespace.Functions.Count; i++)
			{
				aFunctions.Add(this.oGlobalNamespace.Functions[i].Value);
			}

			aFunctions.Sort(CompareFunctionByAddress);

			this.oGlobalNamespace.Functions.Clear();

			for (int i = 0; i < aFunctions.Count; i++)
			{
				this.oGlobalNamespace.Functions.Add(aFunctions[i].Name, aFunctions[i]);
			}
		}

		public static int CompareFunctionByAddress(CFunction f1, CFunction f2)
		{
			uint f1Address = ((f1.Segment & 0xffff) << 16) | (f1.Offset & 0xffff);
			uint f2Address = ((f2.Segment & 0xffff) << 16) | (f2.Offset & 0xffff);

			return f1Address.CompareTo(f2Address);
		}

		public void Decompile(string name, CallTypeEnum callType, List<CParameter> parameters, CType returnValue, ushort segment, ushort offset)
		{
			for (int i = 0; i < this.oGlobalNamespace.Functions.Count; i++)
			{
				CFunction function1 = this.oGlobalNamespace.Functions[i].Value;

				if (function1.Segment == segment && function1.Offset == offset)
				{
					return;
				}
			}

			CFunction function = new CFunction(this, callType, name, parameters, returnValue, segment, offset);
			this.oGlobalNamespace.Functions.Add(function.Name, function);
			function.Disassemble(this);
			function.Decompile(this);
		}

		public CGlobalNamespace GlobalNamespace
		{
			get { return this.oGlobalNamespace; }
		}

		public NewExecutable Executable
		{
			get
			{
				return this.oExecutable;
			}
		}

		public List<ModuleMatch> LibraryMatches
		{
			get
			{
				return this.aLibraryMatches;
			}
		}

		public uint DataSegment
		{
			get
			{
				return this.uiDataSegment;
			}
		}
	}
}
