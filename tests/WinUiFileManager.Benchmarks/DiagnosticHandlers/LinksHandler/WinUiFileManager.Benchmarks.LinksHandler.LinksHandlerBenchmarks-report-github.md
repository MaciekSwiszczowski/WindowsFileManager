```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8524/25H2/2025Update/HudsonValley2)
Intel Core i5-10210U CPU 1.60GHz (Max: 2.11GHz), 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.204
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3
  Job-PNPGZA : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3

Platform=X64  Arguments=/p:Platform=x64  RunStrategy=Monitoring  

```
| Method                           | FileCount | Mean       | Error     | StdDev    | Allocated native memory | Native memory leak | Allocated  |
|--------------------------------- |---------- |-----------:|----------:|----------:|------------------------:|-------------------:|-----------:|
| **InspectorLinksDiagnosticsHandler** | **100**       |   **161.4 ms** |  **33.83 ms** |  **22.38 ms** |                  **759 KB** |              **19 KB** |  **437.91 KB** |
| **InspectorLinksDiagnosticsHandler** | **500**       | **1,123.2 ms** | **268.43 ms** | **177.55 ms** |                  **756 KB** |              **14 KB** | **2187.91 KB** |
