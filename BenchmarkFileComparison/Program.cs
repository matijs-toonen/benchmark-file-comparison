using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace BenchmarkFileComparison
{
    class Program
    {
        static void Main(string[] args)
        {
            var config = DefaultConfig.Instance.WithSummaryStyle(SummaryStyle.Default.WithMaxParameterColumnWidth(30));
            config.AddDiagnoser(MemoryDiagnoser.Default);

            var summary = BenchmarkRunner.Run<FileComparison>(config);
        }
    }
}
