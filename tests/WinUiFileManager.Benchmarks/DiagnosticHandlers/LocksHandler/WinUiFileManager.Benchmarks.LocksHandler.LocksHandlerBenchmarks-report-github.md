```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8524/25H2/2025Update/HudsonValley2)
Intel Core i5-10210U CPU 1.60GHz (Max: 2.11GHz), 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.204
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3
  Job-PNPGZA : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3

Platform=X64  Arguments=/p:Platform=x64  RunStrategy=Monitoring  

```
| Method                           | FileCount | Mean    | Error    | StdDev   | Allocated native memory | Native memory leak | Allocated |
|--------------------------------- |---------- |--------:|---------:|---------:|------------------------:|-------------------:|----------:|
| **InspectorLocksDiagnosticsHandler** | **10**        | **2.652 s** | **0.1815 s** | **0.1200 s** |                  **757 KB** |              **38 KB** |  **33.09 KB** |
| **InspectorLocksDiagnosticsHandler** | **25**        | **5.880 s** | **1.4950 s** | **0.9889 s** |                  **772 KB** |              **38 KB** |  **82.07 KB** |
