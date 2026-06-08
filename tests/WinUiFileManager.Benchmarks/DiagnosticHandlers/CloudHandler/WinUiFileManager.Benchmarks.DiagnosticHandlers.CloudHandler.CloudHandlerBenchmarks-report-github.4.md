```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8524/25H2/2025Update/HudsonValley2)
Intel Core i5-10210U CPU 1.60GHz (Max: 2.11GHz), 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.204
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3
  Job-LTMPYP : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3

Platform=X64  Arguments=/p:Platform=x64  InvocationCount=1  
IterationCount=5  LaunchCount=1  RunStrategy=Monitoring  
WarmupCount=1  Categories=DiagnosticHandlers  

```
| Method                           | FileCount | Mean       | Error     | StdDev   | Allocated native memory | Native memory leak | Allocated  |
|--------------------------------- |---------- |-----------:|----------:|---------:|------------------------:|-------------------:|-----------:|
| **InspectorCloudDiagnosticsHandler** | **10**        |   **232.4 ms** |  **36.52 ms** |  **9.49 ms** |                       **-** |                  **-** |  **391.13 KB** |
| **InspectorCloudDiagnosticsHandler** | **50**        | **1,094.9 ms** | **237.23 ms** | **61.61 ms** |                       **-** |                  **-** | **1883.95 KB** |
