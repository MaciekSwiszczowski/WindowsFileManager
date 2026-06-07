```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8524/25H2/2025Update/HudsonValley2)
Intel Core i5-10210U CPU 1.60GHz (Max: 2.11GHz), 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.204
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3
  Job-LTMPYP : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3

Platform=X64  Arguments=/p:Platform=x64  InvocationCount=1  
IterationCount=5  LaunchCount=1  RunStrategy=Monitoring  
WarmupCount=1  Categories=WinRT,SyncRoot  

```
| Method          | EnumerationCount | Mean        | Error       | StdDev     | Ratio | RatioSD | Allocated native memory | Native memory leak | Allocated | Alloc Ratio |
|---------------- |----------------- |------------:|------------:|-----------:|------:|--------:|------------------------:|-------------------:|----------:|------------:|
| **ReadViaRegistry** | **1**                |    **315.6 μs** |    **513.8 μs** |   **133.4 μs** |  **0.04** |    **0.01** |                    **2 KB** |               **0 KB** |   **5.45 KB** |        **1.11** |
| ReadViaWinRt    | 1                |  9,090.7 μs |  4,573.6 μs | 1,187.8 μs |  1.01 |    0.17 |                  500 KB |              51 KB |   4.92 KB |        1.00 |
|                 |                  |             |             |            |       |         |                         |                    |           |             |
| **ReadViaRegistry** | **10**               |  **1,359.4 μs** |    **627.2 μs** |   **162.9 μs** |  **0.02** |    **0.00** |                   **16 KB** |                  **-** |  **52.48 KB** |        **1.07** |
| ReadViaWinRt    | 10               | 90,582.7 μs | 36,217.3 μs | 9,405.5 μs |  1.01 |    0.13 |                4,994 KB |             508 KB |  49.24 KB |        1.00 |
