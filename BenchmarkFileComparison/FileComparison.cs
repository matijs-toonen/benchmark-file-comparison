using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace BenchmarkFileComparison
{
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

        [Benchmark]
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

                // Different contents
                yield return new object[] {new FileInfo(origFile), new FileInfo(compFile)};

                // Equal contents
                yield return new object[] {new FileInfo(origFile), new FileInfo(origFile)};
            }
        }
    }
}
