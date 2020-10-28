using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace BenchmarkFileComparison
{
    [SimpleJob(RuntimeMoniker.NetCoreApp50)]
    [IterationCount(10)]
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
        public bool FilesAreEqual_ByteBatch(FileInfo original, FileInfo compare)
        {
            if (original.Length != compare.Length)
                return false;

            if (string.Equals(original.FullName, compare.FullName, StringComparison.OrdinalIgnoreCase))
                return true;

            var iterations = (int) Math.Ceiling((double) original.Length / BYTES_TO_READ);

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