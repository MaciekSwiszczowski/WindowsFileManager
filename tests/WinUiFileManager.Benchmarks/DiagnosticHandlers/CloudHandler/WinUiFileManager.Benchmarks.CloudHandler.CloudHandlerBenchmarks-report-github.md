```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8524/25H2/2025Update/HudsonValley2)
Intel Core i5-10210U CPU 1.60GHz (Max: 2.11GHz), 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.204
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3
  Job-PNPGZA : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3

Platform=X64  Arguments=/p:Platform=x64  RunStrategy=Monitoring  

```
| Method                           | FileCount | Mean     | Error    | StdDev   | Gen0      | Allocated native memory | Native memory leak | Gen1      | Gen2      | Allocated |
|--------------------------------- |---------- |---------:|---------:|---------:|----------:|------------------------:|-------------------:|----------:|----------:|----------:|
| **InspectorCloudDiagnosticsHandler** | **10**        |  **2.056 s** | **0.3591 s** | **0.2375 s** |         **-** |                    **1 MB** |               **0 MB** |         **-** |         **-** |   **1.02 MB** |
| **InspectorCloudDiagnosticsHandler** | **100**       | **10.461 s** | **0.8966 s** | **0.5931 s** | **5000.0000** |                    **1 MB** |               **0 MB** | **2000.0000** | **2000.0000** |  **10.17 MB** |
