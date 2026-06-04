```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8524/25H2/2025Update/HudsonValley2)
Intel Core i5-10210U CPU 1.60GHz (Max: 2.11GHz), 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.204
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3
  Job-LTMPYP : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3

Platform=X64  Arguments=/p:Platform=x64  InvocationCount=1  
IterationCount=5  LaunchCount=1  RunStrategy=Monitoring  
WarmupCount=1  

```
| Method                               | FileCount | Mean       | Error     | StdDev    | Gen0       | Allocated native memory | Native memory leak | Gen1       | Gen2       | Allocated |
|------------------------------------- |---------- |-----------:|----------:|----------:|-----------:|------------------------:|-------------------:|-----------:|-----------:|----------:|
| **InspectorThumbnailDiagnosticsHandler** | **5**         |   **262.1 ms** |  **53.75 ms** |  **13.96 ms** |  **4000.0000** |                       **-** |                  **-** |  **4000.0000** |  **4000.0000** |  **16.24 MB** |
| **InspectorThumbnailDiagnosticsHandler** | **20**        | **1,250.7 ms** | **593.97 ms** | **154.25 ms** | **19000.0000** |                       **-** |                  **-** | **19000.0000** | **19000.0000** |  **64.95 MB** |
