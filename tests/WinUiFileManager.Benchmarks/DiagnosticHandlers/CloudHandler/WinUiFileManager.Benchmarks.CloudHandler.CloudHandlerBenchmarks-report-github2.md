```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8524/25H2/2025Update/HudsonValley2)
Intel Core i5-10210U CPU 1.60GHz (Max: 2.11GHz), 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.204
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3
  Job-PNPGZA : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3

Platform=X64  Arguments=/p:Platform=x64  RunStrategy=Monitoring  

```
| Method                           | FileCount | Mean     | Error     | StdDev    | Median   | Allocated native memory | Native memory leak | Allocated |
|--------------------------------- |---------- |---------:|----------:|----------:|---------:|------------------------:|-------------------:|----------:|
| **InspectorCloudDiagnosticsHandler** | **2**         | **45.64 ms** | **202.18 ms** | **133.73 ms** | **3.293 ms** |                  **786 KB** |              **38 KB** |   **10.7 KB** |
| **InspectorCloudDiagnosticsHandler** | **10**        | **33.88 ms** | **120.30 ms** |  **79.57 ms** | **9.077 ms** |                  **787 KB** |              **38 KB** |  **41.58 KB** |
