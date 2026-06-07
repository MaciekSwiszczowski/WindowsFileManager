```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8457/25H2/2025Update/HudsonValley2)
Intel Core i5-10210U CPU 1.60GHz (Max: 2.11GHz), 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.204
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3
  Job-PNPGZA : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3

Platform=X64  Arguments=/p:Platform=x64  RunStrategy=Monitoring  

```
| Method                             | FileCount | Mean       | Error    | StdDev    | Gen0      | Allocated native memory | Native memory leak | Allocated |
|----------------------------------- |---------- |-----------:|---------:|----------:|----------:|------------------------:|-------------------:|----------:|
| **InspectorStreamsDiagnosticsHandler** | **500**       |   **478.6 ms** | **118.4 ms** |  **78.32 ms** |         **-** |                    **1 MB** |               **0 MB** |   **1.55 MB** |
| **InspectorStreamsDiagnosticsHandler** | **2000**      | **1,294.2 ms** | **166.4 ms** | **110.09 ms** | **2000.0000** |                    **1 MB** |               **0 MB** |    **6.2 MB** |
