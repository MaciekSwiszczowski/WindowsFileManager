```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8524/25H2/2025Update/HudsonValley2)
Intel Core i5-10210U CPU 1.60GHz (Max: 2.11GHz), 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.204
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3
  Job-PNPGZA : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3

Platform=X64  Arguments=/p:Platform=x64  RunStrategy=Monitoring  

```
| Method                             | FileCount | Mean      | Error     | StdDev    | Allocated native memory | Native memory leak | Allocated  |
|----------------------------------- |---------- |----------:|----------:|----------:|------------------------:|-------------------:|-----------:|
| **InspectorStreamsDiagnosticsHandler** | **100**       |  **68.75 ms** |  **15.22 ms** |  **10.07 ms** |                  **782 KB** |              **44 KB** |  **318.94 KB** |
| **InspectorStreamsDiagnosticsHandler** | **500**       | **336.16 ms** | **177.14 ms** | **117.17 ms** |                  **781 KB** |              **38 KB** | **1593.94 KB** |
