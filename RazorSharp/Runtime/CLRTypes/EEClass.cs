using System;
using System.Runtime.InteropServices;
using RazorCommon;

namespace RazorSharp.Runtime.CLRTypes
{

	// class.h
	using BYTE = Byte;
	using QWORD = UInt64;
	using DWORD = UInt32;
	using WORD = UInt16;

	// todo: WIP
	[StructLayout(LayoutKind.Explicit)]

	// ReSharper disable once InconsistentNaming
	public unsafe struct EEClass
	{

		#region Fields

		[FieldOffset(0)] private void* m_pGuidInfo;
		[FieldOffset(8)] private void* m_rpOptionalFields;

		//** Status: verified
		[FieldOffset(16)] private void*      m_pMethodTable;
		[FieldOffset(24)] private FieldDesc* m_pFieldDescList;
		[FieldOffset(32)] private void*      m_pChunks;

		// Union
		[FieldOffset(40)] private uint m_cbNativeSize;

		// COMINTEROP
		[FieldOffset(40)] private void* ohDelegate;

		[FieldOffset(40)] private int m_ComInterfaceType;

		// End COMINTEROP
		// End Union

		// COMINTEROP
		[FieldOffset(48)] private void* m_pccwTemplate;

		// End COMINTEROP

		//** Status: verified
		[FieldOffset(56)] private DWORD m_dwAttrClass;

		//** Status: verified
		[FieldOffset(60)] private DWORD m_VMFlags;
		[FieldOffset(64)] private byte  m_NormType;
		[FieldOffset(65)] private byte  m_fFieldsArePacked;
		[FieldOffset(66)] private byte  m_cbFixedEEClassFields;

		/*
		 * Number of bytes to subtract from code:MethodTable::GetBaseSize() to get the actual number of bytes
		 * of instance fields stored in the object on the GC heap.
		 */
		//** Status: verified
		[FieldOffset(67)] private byte m_cbBaseSizePadding;

		#endregion

		#region Accessors

		public bool HasLayout => VMFlags.HasFlag(VMFlags.VmflagHaslayout);

		public DWORD Attributes => m_dwAttrClass;

		public byte BaseSizePadding => m_cbBaseSizePadding;

		// ReSharper disable once InconsistentNaming
		public VMFlags VMFlags => (VMFlags) m_VMFlags;

		#endregion

		//fixme
		private EEClassLayoutInfo* GetLayoutInfo()
		{
			return &((LayoutEEClass*) (Unsafe.AddressOf(ref this)))->m_LayoutInfo; //
		}

		public override string ToString()
		{
			var table = new ConsoleTable("Field", "Value");
			table.AddRow(nameof(m_pGuidInfo), Hex.ToHex(m_pGuidInfo));
			table.AddRow(nameof(m_rpOptionalFields), Hex.ToHex(m_rpOptionalFields));
			table.AddRow(nameof(m_pMethodTable), Hex.ToHex(m_pMethodTable));
			table.AddRow(nameof(m_pFieldDescList), Hex.ToHex(m_pFieldDescList));
			table.AddRow(nameof(m_pChunks), Hex.ToHex(m_pChunks));
			table.AddRow(nameof(m_cbNativeSize), m_cbNativeSize);
			table.AddRow(nameof(ohDelegate), Hex.ToHex(ohDelegate));
			table.AddRow(nameof(m_ComInterfaceType), m_ComInterfaceType);
			table.AddRow(nameof(m_pccwTemplate), Hex.ToHex(m_pccwTemplate));
			table.AddRow(nameof(m_dwAttrClass), Hex.ToHex(m_dwAttrClass));
			table.AddRow(nameof(m_NormType), m_NormType);
			table.AddRow(nameof(m_fFieldsArePacked), m_fFieldsArePacked);
			table.AddRow(nameof(m_cbFixedEEClassFields), m_cbFixedEEClassFields);
			table.AddRow(nameof(m_cbBaseSizePadding), m_cbBaseSizePadding);
			table.AddRow("VMFlags", VMFlags);

			return table.ToMarkDownString();
		}
	}

}