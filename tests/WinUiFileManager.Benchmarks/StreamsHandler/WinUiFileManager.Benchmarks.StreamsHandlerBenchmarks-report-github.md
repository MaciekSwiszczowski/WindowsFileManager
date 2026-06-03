```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8457/25H2/2025Update/HudsonValley2)
Intel Core i5-10210U CPU 1.60GHz (Max: 2.11GHz), 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.204
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3
  Job-PNPGZA : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3

Platform=X64  Arguments=/p:Platform=x64  RunStrategy=Monitoring  

```
| Method                             | FileCount | Mean     | Error     | StdDev    | Gen0      | Allocated native memory | Native memory leak | Allocated |
|----------------------------------- |---------- |---------:|----------:|----------:|----------:|------------------------:|-------------------:|----------:|
| **InspectorStreamsDiagnosticsHandler** | **500**       | **260.4 ms** |  **73.06 ms** |  **48.33 ms** |         **-** |                    **1 MB** |               **0 MB** |   **2.82 MB** |
| **InspectorStreamsDiagnosticsHandler** | **2000**      | **937.0 ms** | **155.57 ms** | **102.90 ms** | **3000.0000** |                    **1 MB** |               **0 MB** |  **11.28 MB** |
