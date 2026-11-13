using BenchmarkDotNet.Running;

// dotnet run -c Release --project benchmarks/ControlId.Net.Benchmarks -- --filter '*'
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
