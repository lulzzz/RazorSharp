using BenchmarkDotNet.Attributes;
using RazorSharp;
using RazorSharp.Pointers;

namespace Test.Testing.Benchmarking
{

	public class PointerBenchmark
	{
		private int[] _arr = {1, 2, 3, 4, 5};

		private Pointer<int> _arrPtr;

		[GlobalSetup]
		public void Setup()
		{
			_arrPtr = Unsafe.AddressOfHeap(ref _arr, OffsetType.ArrayData);
		}

		[Benchmark]
		public void IteratePtr()
		{
			for (int i = 0; i < _arr.Length; i++) {
				var x = _arrPtr[i];
			}
		}
	}

}