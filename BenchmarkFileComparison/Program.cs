using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace BenchmarkFileComparison
{
    class Program
    {
        static void Main(string[] args)
        {
            var config = DefaultConfig.Instance.WithSummaryStyle(SummaryStyle.Default.WithMaxParameterColumnWidth(100));
            BenchmarkRunner.Run<FileComparison>(config);
        }
    }
}
