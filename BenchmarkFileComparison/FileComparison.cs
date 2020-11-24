using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using System.Runtime.Intrinsics.X86;
using System.Numerics;

namespace BenchmarkFileComparison
{
    //[SimpleJob(RuntimeMoniker.NetCoreApp50)]
    [SimpleJob(RuntimeMoniker.NetCoreApp50, 1, 1, 1)]
    //[IterationCount(10)]
    [MinColumn, MaxColumn, MeanColumn]
    public class FileComparison
    {
        // ReSharper disable once InconsistentNaming
        /// <summary>
        /// Available: 25, 50, 100, 500, 1000, 5000, 100000
        /// </summary>
        private readonly int[] SIZES = {25, 1000};

        [Benchmark]
        [ArgumentsSource(nameof(Files))]
        public bool FilesAreEqual_Hash(FileInfo original, FileInfo compare)
        {
            var firstHash = MD5.Create().ComputeHash(original.OpenRead());
            var secondHash = MD5.Create().ComputeHash(compare.OpenRead());

            for (var i = 0; i < firstHash.Length; i++)
            {
                if (firstHash[i] != secondHash[i])
                    return false;
            }

            return true;
        }

        [Benchmark]
        [ArgumentsSource(nameof(Files))]
        public bool FilesAreEqual_ArraySegment(FileInfo original, FileInfo compare)
        {
            var fileBytes1 = File.ReadAllBytes(original.FullName);
            var fileBytes2 = File.ReadAllBytes(compare.FullName);

            var offset = 0;
            while (offset < (int)original.Length - offset)
            {
                var count = BYTES_TO_READ + offset;

                if (count > original.Length - offset)
                {
                    count = (int)original.Length - offset;
                }

                var segment1 = new ArraySegment<byte>(fileBytes1, offset, count);
                var segment2 = new ArraySegment<byte>(fileBytes2, offset, count);

                if (!segment1.SequenceEqual(segment2))
                {
                    return false;
                }

                offset += BYTES_TO_READ;
            }

            return true;
        }

        [Benchmark]
        [ArgumentsSource(nameof(Files))]
        public async Task<bool> Async_FilesAreEqual_ArraySegment(FileInfo original, FileInfo compare)
        {
            
            var fileBytes1 = File.ReadAllBytes(original.FullName);
            var fileBytes2 = File.ReadAllBytes(compare.FullName);

            var amountOfChunks = 10;

            var chunksSize = (int)Math.Ceiling(fileBytes1.Length / (float)amountOfChunks);


            var fileLength = (int)original.Length;

            var chunks = new List<(int, int)>(amountOfChunks);
            var offset = 0;
            var count = chunksSize;
            for (int i = 0; i < amountOfChunks; i++)
            {
                if (offset + count > fileLength)
                {
                    count = fileLength - offset;
                }

                chunks.Add((offset, count));
                offset += count;
            }
            
            Task<bool> compareFiles(int offset, int count)
            {
                var segment1 = new ArraySegment<byte>(fileBytes1, offset, count);
                var segment2 = new ArraySegment<byte>(fileBytes2, offset, count);
                if (!segment1.SequenceEqual(segment2))
                {
                    return Task.FromResult(false);
                }

                return Task.FromResult(true);
            }

            var tasks = Enumerable.Range(0, amountOfChunks).Select(i => {
                var chunk = chunks[i];
                return compareFiles(chunk.Item1, chunk.Item2);
            });

            
            foreach(var task in tasks.AsParallel())
            {
                var result = await task;
                if (!result)
                {
                    return false;
                }
            }

            return true;
        }

        [Benchmark]
        [ArgumentsSource(nameof(Files))]
        public bool FilesAreEqual_Byte(FileInfo original, FileInfo compare)
        {
            if (original.Length != compare.Length)
                return false;

            if (string.Equals(original.FullName, compare.FullName, StringComparison.OrdinalIgnoreCase))
                return true;

            using var fs1 = original.OpenRead();
            using var fs2 = compare.OpenRead();
            for (var i = 0; i < original.Length; i++)
            {
                if (fs1.ReadByte() != fs2.ReadByte())
                    return false;
            }

            return true;
        }

        // ReSharper disable once InconsistentNaming
        const int BYTES_TO_READ = sizeof(Int64);


        [Benchmark]
        [ArgumentsSource(nameof(Files))]
        public bool FilesAreEqual_ByteChunked(FileInfo original, FileInfo compare)
        {
            if (original.Length != compare.Length)
            {
                return false;
            }

            if (string.Equals(original.FullName, compare.FullName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var iterations = (int)Math.Ceiling((double)original.Length / BYTES_TO_READ);

            using var fs1 = new FileStream(original.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.WriteThrough);
            using var fs2 = new FileStream(compare.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.WriteThrough);

            var one = new byte[BYTES_TO_READ];
            var two = new byte[BYTES_TO_READ];

            for (var i = 0; i < iterations; i++)
            {
                fs1.Read(one, 0, BYTES_TO_READ);
                fs2.Read(two, 0, BYTES_TO_READ);

                if (BitConverter.ToInt64(one, 0) != BitConverter.ToInt64(two, 0))
                    return false;
            }

            return true;
        }

        [Benchmark]
        [ArgumentsSource(nameof(Files))]
        public bool FilesAreEqual_ByteChunkedStackAlloc(FileInfo original, FileInfo compare)
        {
            if (original.Length != compare.Length)
            {
                return false;
            }

            if (string.Equals(original.FullName, compare.FullName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var iterations = (int)Math.Ceiling((double)original.Length / BYTES_TO_READ);

            using var fs1 = new FileStream(original.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.WriteThrough);
            using var fs2 = new FileStream(compare.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.WriteThrough);

            Span<byte> one = stackalloc byte[BYTES_TO_READ];
            Span<byte> two = stackalloc byte[BYTES_TO_READ];

            for (var i = 0; i < iterations; i++)
            {
                fs1.Read(one);
                fs2.Read(two);

                if (BitConverter.ToInt64(one) != BitConverter.ToInt64(two))
                    return false;
            }

            return true;
        }

        const int MemoryBufferSize = 4096;
        const int FileStreamBufferSize = 8192;

        [Benchmark]
        [ArgumentsSource(nameof(Files))]
        public bool FilesAreEqual_ByteFileStreamOptions(FileInfo original, FileInfo compare)
        {
            if (original.Length != compare.Length)
            {
                return false;
            }

            if (string.Equals(original.FullName, compare.FullName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var iterations = (int)Math.Ceiling((double)original.Length / MemoryBufferSize);

            using var fs1 = new FileStream(original.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, MemoryBufferSize, FileOptions.SequentialScan);
            using var fs2 = new FileStream(compare.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, MemoryBufferSize, FileOptions.SequentialScan);

            Span<byte> one = stackalloc byte[MemoryBufferSize];
            Span<byte> two = stackalloc byte[MemoryBufferSize];

            for (var i = 0; i < iterations; i++)
            {
                fs1.Read(one);
                fs2.Read(two);

                if (BitConverter.ToInt64(one) != BitConverter.ToInt64(two))
                    return false;
            }

            return true;
        }

        [Benchmark]
        [ArgumentsSource(nameof(Files))]
        public async Task<bool> AsyncFilesAreEqual__ByteFileStreamOptions(FileInfo original, FileInfo compare)
        {
            if (original.Length != compare.Length)
            {
                return false;
            }

            if (string.Equals(original.FullName, compare.FullName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var iterations = (int)Math.Ceiling((double)original.Length / MemoryBufferSize);

            using var fs1 = new FileStream(original.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, FileStreamBufferSize, true);
            using var fs2 = new FileStream(compare.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, FileStreamBufferSize, true);

            var one = new Memory<byte>(new byte[MemoryBufferSize]);
            var two = new Memory<byte>(new byte[MemoryBufferSize]);

            for (var i = 0; i < iterations; i++)
            {
                var task1 = fs1.ReadAsync(one).AsTask();
                var task2 = fs2.ReadAsync(two).AsTask();

                await Task.WhenAll(task1, task2);

                if (BitConverter.ToInt64(one.Span) != BitConverter.ToInt64(two.Span))
                    return false;
            }

            return true;
        }

        [Benchmark]
        [ArgumentsSource(nameof(Files))]
        public bool FilesAreEqual_ByteMinFileStreamOptions(FileInfo original, FileInfo compare)
        {
            if (original.Length != compare.Length)
            {
                return false;
            }

            if (string.Equals(original.FullName, compare.FullName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var iterations = (int)Math.Ceiling((double)original.Length / MemoryBufferSize);

            using var fs1 = new FileStream(original.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, FileStreamBufferSize, FileOptions.None);
            using var fs2 = new FileStream(compare.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, FileStreamBufferSize, FileOptions.None);

            Span<byte> one = stackalloc byte[MemoryBufferSize];
            Span<byte> two = stackalloc byte[MemoryBufferSize];

            for (var i = 0; i < iterations; i++)
            {
                fs1.Read(one);
                fs2.Read(two);

                if (BitConverter.ToInt64(one) != BitConverter.ToInt64(two))
                    return false;
            }

            return true;
        }

        [Benchmark]
        [ArgumentsSource(nameof(Files))]
        public bool FilesAreEqual_Sequence(FileInfo original, FileInfo compare)
        {
            if (original.Length != compare.Length)
            {
                return false;
            }

            if (string.Equals(original.FullName, compare.FullName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var iterations = (int)Math.Ceiling((double)original.Length / MemoryBufferSize);

            using var fs1 = new FileStream(original.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, FileStreamBufferSize, FileOptions.None);
            using var fs2 = new FileStream(compare.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, FileStreamBufferSize, FileOptions.None);

            Span<byte> one = stackalloc byte[MemoryBufferSize];
            Span<byte> two = stackalloc byte[MemoryBufferSize];

            for (var i = 0; i < iterations; i++)
            {
                fs1.Read(one);
                fs2.Read(two);

                if (!one.SequenceEqual(two))
                {
                    return false;
                }
            }

            return true;
        }

        [Benchmark]
        [ArgumentsSource(nameof(Files))]
        public bool FilesAreEqual_SequenceSeq(FileInfo original, FileInfo compare)
        {
            if (original.Length != compare.Length)
            {
                return false;
            }

            if (string.Equals(original.FullName, compare.FullName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var iterations = (int)Math.Ceiling((double)original.Length / MemoryBufferSize);

            using var fs1 = new FileStream(original.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, FileStreamBufferSize, FileOptions.SequentialScan);
            using var fs2 = new FileStream(compare.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, FileStreamBufferSize, FileOptions.SequentialScan);

            Span<byte> one = stackalloc byte[MemoryBufferSize];
            Span<byte> two = stackalloc byte[MemoryBufferSize];

            for (var i = 0; i < iterations; i++)
            {
                fs1.Read(one);
                fs2.Read(two);

                if (!one.SequenceEqual(two))
                {
                    return false;
                }
            }

            return true;
        }

        [Benchmark]
        [ArgumentsSource(nameof(Files))]
        public unsafe bool FilesAreEqual_Vector(FileInfo original, FileInfo compare)
        {
            if (original.Length != compare.Length)
            {
                return false;
            }

            if (string.Equals(original.FullName, compare.FullName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var iterations = (int)Math.Ceiling((double)original.Length / MemoryBufferSize);

            using var fs1 = new FileStream(original.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, FileStreamBufferSize, FileOptions.None);
            using var fs2 = new FileStream(compare.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, FileStreamBufferSize, FileOptions.None);

            Span<byte> one = stackalloc byte[MemoryBufferSize];
            Span<byte> two = stackalloc byte[MemoryBufferSize];

            // chunks of 32 bits
            // MemoryBufferSize / 32 = 128,
            // this will compute nicely and not need any checks to secure that every bit has been checked
            int vectorSize = Vector<byte>.Count;

            for (var j = 0; j < iterations; j++)
            {
                fs1.Read(one);
                fs2.Read(two);
                
                int i = 0;
                for (; i <= one.Length - vectorSize; i += vectorSize)
                {
                    var va = new Vector<byte>(one.Slice(i, vectorSize));
                    var vb = new Vector<byte>(two.Slice(i, vectorSize));
                    if (!Vector.EqualsAll(va, vb))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        [Benchmark]
        [ArgumentsSource(nameof(Files))]
        public unsafe bool FilesAreEqual_VectorFull(FileInfo original, FileInfo compare)
        {
            if (original.Length != compare.Length)
            {
                return false;
            }

            if (string.Equals(original.FullName, compare.FullName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var iterations = (int)Math.Ceiling((double)original.Length / MemoryBufferSize);

            using var fs1 = new FileStream(original.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, FileStreamBufferSize, FileOptions.None);
            using var fs2 = new FileStream(compare.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, FileStreamBufferSize, FileOptions.None);

            Span<byte> one = stackalloc byte[MemoryBufferSize];
            Span<byte> two = stackalloc byte[MemoryBufferSize];

            for (var j = 0; j < iterations; j++)
            {
                fs1.Read(one);
                fs2.Read(two);

                var va = new Vector<byte>(one);
                var vb = new Vector<byte>(two);
                if (!Vector.EqualsAll(va, vb))
                {
                    return false;
                }
            }

            return true;
        }

        [Benchmark]
        [ArgumentsSource(nameof(Files))]
        public unsafe bool FilesAreEqual_Intrinsic(FileInfo original, FileInfo compare)
        {
            if (!Avx2.IsSupported)
            {
                throw new NotSupportedException("CPU does not support Avx2 intrinsics");
            }

            if (original.Length != compare.Length)
            {
                return false;
            }

            if (string.Equals(original.FullName, compare.FullName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var iterations = (int)Math.Ceiling((double)original.Length / MemoryBufferSize);

            using var fs1 = new FileStream(original.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, FileStreamBufferSize, FileOptions.None);
            using var fs2 = new FileStream(compare.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, FileStreamBufferSize, FileOptions.None);

            Span<byte> one = stackalloc byte[MemoryBufferSize];
            Span<byte> two = stackalloc byte[MemoryBufferSize];

            // chunks of 32 bits
            // MemoryBufferSize / 32 = 128,
            // this will compute nicely and not need any checks to secure that every bit has been checked
            const int vectorSize = 256 / 8;
            const int equalsMask = -1;

            for (var j = 0; j < iterations; j++)
            {
                fs1.Read(one);
                fs2.Read(two);

                int i = 0;
                fixed (byte* ptrA = one, ptrB = two)
                {
                    for (; i <= one.Length - vectorSize; i += vectorSize)
                    {
                        var va = Avx2.LoadVector256(ptrA + i);
                        var vb = Avx2.LoadVector256(ptrB + i);
                        var areEqual = Avx2.CompareEqual(va, vb);
                        if (Avx2.MoveMask(areEqual) != equalsMask)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int memcmp(byte[] b1, byte[] b2, long count);

        [Benchmark]
        [ArgumentsSource(nameof(Files))]
        public bool FilesAreEqual_MemCmp(FileInfo original, FileInfo compare)
        {
            if (original.Length != compare.Length)
            {
                return false;
            }

            if (string.Equals(original.FullName, compare.FullName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var iterations = (int)Math.Ceiling((double)original.Length / FileStreamBufferSize);

            using var fs1 = new FileStream(original.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, FileStreamBufferSize, FileOptions.None);
            using var fs2 = new FileStream(compare.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, FileStreamBufferSize, FileOptions.None);

            var one = new byte[FileStreamBufferSize];
            var two = new byte[FileStreamBufferSize];

            for (var i = 0; i < iterations; i++)
            {
                fs1.Read(one);
                fs2.Read(two);

                if (memcmp(one, two, one.Length) != 0)
                {
                    return false;
                }
            }

            return true;
        }

        // Copyright (c) 2008-2013 Hafthor Stefansson
        // Distributed under the MIT/X11 software license
        // Ref: http://www.opensource.org/licenses/mit-license.php.
        [Benchmark]
        [ArgumentsSource(nameof(Files))]
        public unsafe bool UnsafeCompare(FileInfo original, FileInfo compare)
        {
            if (original.Length != compare.Length)
            {
                return false;
            }

            if (string.Equals(original.FullName, compare.FullName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var iterations = (int)Math.Ceiling((double)original.Length / MemoryBufferSize);

            using var fs1 = new FileStream(original.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, FileStreamBufferSize, FileOptions.None);
            using var fs2 = new FileStream(compare.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, FileStreamBufferSize, FileOptions.None);

            Span<byte> one = stackalloc byte[MemoryBufferSize];
            Span<byte> two = stackalloc byte[MemoryBufferSize];

            for (var i = 0; i < iterations; i++)
            {
                fs1.Read(one);
                fs2.Read(two);

                fixed (byte* p1 = one, p2 = two)
                {
                    byte* x1 = p1, x2 = p2;
                    int l = one.Length;
                    for (int j = 0; j < l / 8; j++, x1 += 8, x2 += 8)
                    {

                        if (*((long*)x1) != *((long*)x2))
                        {
                            return false;
                        }

                        if ((l & 4) != 0)
                        {
                            if (*((int*)x1) != *((int*)x2))
                            {
                                return false;
                            }

                            x1 += 4; x2 += 4;
                        }

                        if ((l & 2) != 0) 
                        {
                            if (*((short*)x1) != *((short*)x2))
                            {
                                return false;
                            }

                            x1 += 2; x2 += 2; 
                        }

                        if ((l & 1) != 0)
                        {
                            if (*((byte*)x1) != *((byte*)x2))
                            {
                                return false;
                            }
                        }
                    }
                }
            }

            return true;

        }

        [Benchmark]
        [ArgumentsSource(nameof(Files))]
        public bool FilesAreEqual_MemCmpSeq(FileInfo original, FileInfo compare)
        {
            if (original.Length != compare.Length)
            {
                return false;
            }

            if (string.Equals(original.FullName, compare.FullName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var iterations = (int)Math.Ceiling((double)original.Length / FileStreamBufferSize);

            using var fs1 = new FileStream(original.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, FileStreamBufferSize, FileOptions.SequentialScan);
            using var fs2 = new FileStream(compare.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, FileStreamBufferSize, FileOptions.SequentialScan);

            var one = new byte[FileStreamBufferSize];
            var two = new byte[FileStreamBufferSize];

            for (var i = 0; i < iterations; i++)
            {
                fs1.Read(one);
                fs2.Read(two);

                if (memcmp(one, two, one.Length) != 0)
                {
                    return false;
                }
            }

            return true;
        }

        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        unsafe static extern int memcmp(byte* b1, byte* b2, long count);


        [Benchmark]
        [ArgumentsSource(nameof(Files))]
        public unsafe bool FilesAreEqual_PointerMemCmp(FileInfo original, FileInfo compare)
        {
            if (original.Length != compare.Length)
            {
                return false;
            }

            if (string.Equals(original.FullName, compare.FullName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var iterations = (int)Math.Ceiling((double)original.Length / MemoryBufferSize);

            using var fs1 = new FileStream(original.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, FileStreamBufferSize, FileOptions.None);
            using var fs2 = new FileStream(compare.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, FileStreamBufferSize, FileOptions.None);

            Span<byte> one = stackalloc byte[MemoryBufferSize];
            Span<byte> two = stackalloc byte[MemoryBufferSize];

            for (var i = 0; i < iterations; i++)
            {
                fs1.Read(one);
                fs2.Read(two);

                fixed (byte* onePointer = one, twoPointer = two)
                {
                    if (memcmp(onePointer, twoPointer, MemoryBufferSize) != 0)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        [Benchmark]
        [ArgumentsSource(nameof(Files))]
        public unsafe bool FilesAreEqual_PointerMemCmpSeq(FileInfo original, FileInfo compare)
        {
            if (original.Length != compare.Length)
            {
                return false;
            }

            if (string.Equals(original.FullName, compare.FullName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var iterations = (int)Math.Ceiling((double)original.Length / MemoryBufferSize);

            using var fs1 = new FileStream(original.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, FileStreamBufferSize, FileOptions.SequentialScan);
            using var fs2 = new FileStream(compare.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, FileStreamBufferSize, FileOptions.SequentialScan);

            Span<byte> one = stackalloc byte[MemoryBufferSize];
            Span<byte> two = stackalloc byte[MemoryBufferSize];

            for (var i = 0; i < iterations; i++)
            {
                fs1.Read(one);
                fs2.Read(two);

                fixed (byte* onePointer = one, twoPointer = two)
                {
                    if (memcmp(onePointer, twoPointer, MemoryBufferSize) != 0)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        [Benchmark]
        [ArgumentsSource(nameof(Files))]
        public unsafe bool FilesAreEqual_PointerMemCmpOneFixed(FileInfo original, FileInfo compare)
        {
            if (original.Length != compare.Length)
            {
                return false;
            }

            if (string.Equals(original.FullName, compare.FullName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var iterations = (int)Math.Ceiling((double)original.Length / MemoryBufferSize);

            using var fs1 = new FileStream(original.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, FileStreamBufferSize, FileOptions.None);
            using var fs2 = new FileStream(compare.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, FileStreamBufferSize, FileOptions.None);

            Span<byte> one = stackalloc byte[MemoryBufferSize];
            Span<byte> two = stackalloc byte[MemoryBufferSize];

            fixed (byte* onePointer = one, twoPointer = two)
            {
                for (var i = 0; i < iterations; i++)
                {
                    fs1.Read(one);
                    fs2.Read(two);
                
                    if (memcmp(onePointer, twoPointer, MemoryBufferSize) != 0)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        [Benchmark]
        [ArgumentsSource(nameof(Files))]
        public unsafe bool FilesAreEqual_PointerMemCmpOneFixedSeq(FileInfo original, FileInfo compare)
        {
            if (original.Length != compare.Length)
            {
                return false;
            }

            if (string.Equals(original.FullName, compare.FullName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var iterations = (int)Math.Ceiling((double)original.Length / MemoryBufferSize);

            using var fs1 = new FileStream(original.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, FileStreamBufferSize, FileOptions.SequentialScan);
            using var fs2 = new FileStream(compare.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, FileStreamBufferSize, FileOptions.SequentialScan);

            Span<byte> one = stackalloc byte[MemoryBufferSize];
            Span<byte> two = stackalloc byte[MemoryBufferSize];

            fixed (byte* onePointer = one, twoPointer = two)
            {
                for (var i = 0; i < iterations; i++)
                {
                    fs1.Read(one);
                    fs2.Read(two);

                    if (memcmp(onePointer, twoPointer, MemoryBufferSize) != 0)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        [Benchmark]
        [ArgumentsSource(nameof(Files))]
        public bool FilesAreEqual_ByteBatchStackAlloc(FileInfo original, FileInfo compare)
        {
            if (original.Length != compare.Length)
                return false;

            if (string.Equals(original.FullName, compare.FullName, StringComparison.OrdinalIgnoreCase))
                return true;

            var iterations = (int)Math.Ceiling((double)original.Length / BYTES_TO_READ);

            using var fs1 = original.OpenRead();
            using var fs2 = compare.OpenRead();

            Span<byte> one = stackalloc byte[BYTES_TO_READ];
            Span<byte> two = stackalloc byte[BYTES_TO_READ];

            for (var i = 0; i < iterations; i++)
            {
                fs1.Read(one);
                fs2.Read(two);

                if (BitConverter.ToInt64(one) != BitConverter.ToInt64(two))
                    return false;
            }

            return true;
        }

        [Benchmark(Baseline = true)]
        [ArgumentsSource(nameof(Files))]
        public bool FilesAreEqual_ByteBatch(FileInfo original, FileInfo compare)
        {
            if (original.Length != compare.Length)
                return false;

            if (string.Equals(original.FullName, compare.FullName, StringComparison.OrdinalIgnoreCase))
                return true;

            var iterations = (int)Math.Ceiling((double)original.Length / BYTES_TO_READ);

            using var fs1 = original.OpenRead();
            using var fs2 = compare.OpenRead();

            var one = new byte[BYTES_TO_READ];
            var two = new byte[BYTES_TO_READ];

            for (var i = 0; i < iterations; i++)
            {
                fs1.Read(one, 0, BYTES_TO_READ);
                fs2.Read(two, 0, BYTES_TO_READ);

                if (BitConverter.ToInt64(one, 0) != BitConverter.ToInt64(two, 0))
                    return false;
            }

            return true;
        }

        public IEnumerable<object[]> Files()
        {
            var currentDir = Directory.GetCurrentDirectory();

            foreach (var size in SIZES)
            {
                var origFile = Path.Combine(currentDir, "Files", $"{size}.txt");
                var compFile = Path.Combine(currentDir, "Files", $"{size}_.txt");
                var equalDiff = Path.Combine(currentDir, "Files", $"{size} - Copy.txt");

                // Different contents
                yield return new object[] {new FileInfo(origFile), new FileInfo(compFile)};

                // Equal contents
                yield return new object[] { new FileInfo(origFile), new FileInfo(equalDiff) };
            }
        }
    }
}
