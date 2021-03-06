using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using RazorCommon;

namespace RazorSharp.Pointers
{
	using CSUnsafe = System.Runtime.CompilerServices.Unsafe;

	/// <summary>
	/// Represents a C/C++ style array using dynamic unmanaged memory allocation<para></para>
	/// This allows for creation of managed types in unmanaged memory.<para></para>
	///
	/// - Bounds checking<para></para>
	/// - Resizable<para></para>
	/// - Allocation protection<para></para>
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public sealed unsafe class AllocPointer<T> : Pointer<T>, IDisposable, IEnumerable<T>
	{
		private class AllocPointerMetadata : PointerMetadata
		{
			protected internal bool IsAllocated { get; set; }

			protected internal int AllocatedSize { get; set; }

			protected internal AllocPointerMetadata(int elementSize, bool allocated, int allocSize) : base(elementSize)
			{
				IsAllocated   = allocated;
				AllocatedSize = allocSize;
			}
		}

		#region Accessors

		/// <summary>
		/// Returns the heap address of the array's first element
		/// </summary>
		private IntPtr FirstElement {
			get {
				// move back the indexes
				return Address - (m_offset * ElementSize);
			}
		}

		/// <summary>
		/// Returns the heap address of the array's last element
		/// </summary>
		private IntPtr LastElement {
			get { return FirstElement + ((Count - 1) * ElementSize); }
		}

		/// <summary>
		/// Starting index
		/// </summary>
		public int Start => -m_offset;

		/// <summary>
		/// Ending index
		/// </summary>
		public int End => Start + (Count - 1);

		/// <summary>
		/// Whether or not this pointer is valid
		/// </summary>
		public bool IsAllocated {
			get => Metadata.IsAllocated;
			private set => Metadata.IsAllocated = value;
		}

		private AllocPointerMetadata Metadata {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => ((AllocPointerMetadata) m_metadata);
		}

		private int m_offset;

		/// <summary>
		/// Offset relative to the first element.
		/// </summary>
		public int Offset => m_offset;

		/// <summary>
		/// Allocated bytes of memory
		///
		/// When reallocating, the offset is reset.
		/// </summary>
		public int AllocatedSize {
			get => Metadata.AllocatedSize;
			set {
				int oldCount = Count;

				Address                = Marshal.ReAllocHGlobal(FirstElement, (IntPtr) value);
				m_offset               = 0;
				Metadata.AllocatedSize = value;

				int newElements = Count - oldCount;

				// We have new memory we need to initialize
				if (newElements > 0) {
					Init(oldCount, Count - 1);
				}
			}
		}

		/// <summary>
		/// Current address being pointed to
		/// </summary>
		public override IntPtr Address {
			get => IsAllocated ? base.Address : IntPtr.Zero;

			// This actually probably shouldn't be changed
			set {
				if (IsAllocated) {
					base.Address = value;
				}
			}
		}

		/// <summary>
		/// Current value being pointed to.
		/// </summary>
		public override T Value {
			get { return IsAllocated ? base.Value : default; }
			set {
				if (IsAllocated) {
					base.Value = value;
				}
			}
		}


		public override T this[int index] {
			get {
				if (IsAllocated) {
					EnsureIndexerBounds(index);
					return base[index];
				}
				else return default;
			}
			set {
				if (IsAllocated) {
					EnsureIndexerBounds(index);
					base[index] = value;
				}
			}
		}

		/// <summary>
		/// Number of currently allocated elements
		/// </summary>
		public int Count {
			get => AllocatedSize / Metadata.ElementSize;
			set => AllocatedSize = value * Metadata.ElementSize;
		}

		#endregion

		#region Bounds checking

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void MoveToStart()
		{
			Address  = FirstElement;
			m_offset = 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void MoveToEnd()
		{
			Address  = LastElement;
			m_offset = Count - 1;
		}

		private enum FixType
		{
			/// <summary>
			/// Offset was 1 past the bounds, so we moved back
			/// </summary>
			BounceBack,

			/// <summary>
			/// Offset is >1 out of bounds
			/// </summary>
			OutOfBounds,

			/// <summary>
			/// Offset is OK
			/// </summary>
			Verified
		}

		/// <summary>
		/// If the increment is 1 element over or under bounds,
		/// we'll automatically revert to the beginning or end offset.<para></para>
		///
		/// This is because when incrementing a pointer in a branching statement such as a for
		/// loop or while loop, the pointer typically goes out of bounds by 1. For convenience,
		/// we won't throw an exception, just revert the offset.<para></para>
		///
		/// However if the pointer is offset out of bounds more than 1 offset, we will throw an exception.
		/// </summary>
		private FixType EnsureOffsetBounds(int requestedOffset = 1)
		{
			// Past the end?
			if (Address.ToInt64() + (requestedOffset * ElementSize) > LastElement.ToInt64()) {
				// This is for isolated incidents when iterators
				// and pointer arithmetic move past the end by 1 element.
				//
				// So we'll automatically move to the last element instead, rather
				// than throwing an exception, just for convenience's sake.
				if (requestedOffset == 1) {
					MoveToEnd();
					return FixType.BounceBack;
				}

				return FixType.OutOfBounds;
			}

			// Before the start?
			if (Address.ToInt64() + (requestedOffset * ElementSize) < FirstElement.ToInt64()) {
				// ... and vice versa
				if (requestedOffset == -1) {
					MoveToStart();
					return FixType.BounceBack;
				}

				return FixType.OutOfBounds;
			}

			return FixType.Verified;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void EnsureIndexerBounds(int requestedIndex)
		{
			if (requestedIndex > End) {
				throw new IndexOutOfRangeException($"Requested index of {requestedIndex} > {End} [{Start} - {End}]");
			}

			if (requestedIndex < Start) {
				throw new IndexOutOfRangeException($"Requested index of {requestedIndex} < {Start} [{Start} - {End}]");
			}
		}

		#endregion

		#region Constructors

		/// <summary>
		/// Allocates a block of memory with <![CDATA[Unsafe.SizeOf<T> * elements]]> in
		/// unmanaged memory.
		/// </summary>
		/// <param name="elements">Number of elements to allocate</param>
		public AllocPointer(int elements) : this(Marshal.AllocHGlobal(elements * Unsafe.SizeOf<T>()),
			elements * Unsafe.SizeOf<T>()) { }


		// Base constructor
		private AllocPointer(IntPtr p, int bytesAlloc) : base(p,
			new AllocPointerMetadata(Unsafe.SizeOf<T>(), true, bytesAlloc))
		{
			// Initialize all memory
			Init();

		}

		#endregion

		#region Operators

		#region Arithmetic

		public static AllocPointer<T> operator +(AllocPointer<T> p, int i)
		{
			p.Increment(i);
			return p;
		}

		public static AllocPointer<T> operator -(AllocPointer<T> p, int i)
		{
			p.Decrement(i);
			return p;
		}

		public static AllocPointer<T> operator ++(AllocPointer<T> p)
		{
			p.Increment();
			return p;
		}

		public static AllocPointer<T> operator --(AllocPointer<T> p)
		{
			p.Decrement();
			return p;
		}

		#endregion

		#endregion

		#region Overrides and methods

		/// <summary>
		/// Initialize memory with default values.
		/// </summary>
		public void Init(T value = default)
		{
			Init(Start, End, value);
		}

		private void Init(int start, int end, T value = default)
		{
			// We only need to init one element
			if (start - end == 0) {
				Init(start,value);
			}

			for (int i = start; i <= end; i++) {
				Init(i,value);
			}
		}

		/// <summary>
		/// Initialize a specified element with the default value
		/// </summary>
		/// <param name="elemOffset">Specified index to initialize</param>
		/// <param name="value">Value to initialize with</param>
		public void Init(int elemOffset, T value = default)
		{
			this[elemOffset] = value;
		}

		protected override void Increment(int cnt = 1)
		{
			switch (EnsureOffsetBounds(cnt)) {
				case FixType.BounceBack:
					return;
				case FixType.OutOfBounds:
					throw new IndexOutOfRangeException();
					break;
				case FixType.Verified:
					m_offset += cnt;
					base.Increment(cnt);
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		protected override void Decrement(int cnt = 1)
		{
			switch (EnsureOffsetBounds(-cnt)) {
				case FixType.BounceBack:
					return;
				case FixType.OutOfBounds:
					throw new IndexOutOfRangeException();
					break;
				case FixType.Verified:
					m_offset -= cnt;
					base.Decrement(cnt);
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		protected override ConsoleTable ToTable()
		{
			var table = base.ToTable();
			table.AddRow("Allocated", IsAllocated);
			table.AddRow("Allocated bytes", AllocatedSize);
			table.AddRow("Count", Count);
			table.AddRow("Offset", m_offset);
			table.AddRow("First element", Hex.ToHex(FirstElement));
			table.AddRow("Last element", Hex.ToHex(LastElement));
			table.AddRow("Start",Start);
			table.AddRow("End",End);
			return table;
		}

		protected override ConsoleTable ToElementTable(int length)
		{
			var table = new ConsoleTable("Address", "Offset", "Value");

			for (int i = Start; i <= End; i++) {
				table.AddRow(Hex.ToHex(Unsafe.Offset<T>(Address, i)), i, this[i]);
			}

			return table;
		}

		private void ReleaseUnmanagedResources()
		{
			Marshal.FreeHGlobal(Address);
		}

		/// <summary>
		/// Free the allocated memory
		/// </summary>
		public void Dispose()
		{
			Logger.Log(Flags.Memory, "Freeing {0} bytes @ {1:P}", AllocatedSize, Address);
			ReleaseUnmanagedResources();

			Metadata.AllocatedSize = 0;
			IsAllocated            = false;
			base.Address           = IntPtr.Zero;
			m_offset               = 0;


			GC.SuppressFinalize(this);
		}

		public override string ToString(string format)
		{
			return this.ToString(format, CultureInfo.CurrentCulture);
		}

		/// <inheritdoc />
		/// <summary>
		/// </summary>
		/// <param name="format">E: Element table</param>
		/// <param name="formatProvider"></param>
		/// <returns></returns>
		public override string ToString(string format, IFormatProvider formatProvider)
		{
			switch (format) {
				case "O":
					return IsAllocated ? Value.ToString() : "(null)";
				case "E":
					return ToElementTable(Count).ToMarkDownString();
				case "T":
					return ToTable().ToMarkDownString();
				default:
					goto case "O";
			}
		}

		public IEnumerator<T> GetEnumerator()
		{
			for (int i = 0; i < Count; i++) {
				yield return this[i];
			}
		}

		public override string ToString()
		{

			return this.ToString("O");
		}

		~AllocPointer()
		{
			//ReleaseUnmanagedResources();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		#endregion

	}

}