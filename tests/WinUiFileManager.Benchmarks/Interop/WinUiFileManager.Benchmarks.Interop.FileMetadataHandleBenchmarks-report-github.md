```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8524/25H2/2025Update/HudsonValley2)
Intel Core i5-10210U CPU 1.60GHz (Max: 2.11GHz), 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.204
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3
  Job-LTMPYP : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3

Platform=X64  Arguments=/p:Platform=x64  InvocationCount=1  
IterationCount=5  LaunchCount=1  RunStrategy=Monitoring  
WarmupCount=1  Categories=Interop  

```
| Method                        | FileCount | Mean      | Error     | StdDev    | Allocated native memory | Native memory leak | Allocated |
|------------------------------ |---------- |----------:|----------:|----------:|------------------------:|-------------------:|----------:|
| **OpenReadDisposeMetadataHandle** | **20**        |  **2.666 ms** |  **1.539 ms** | **0.3996 ms** |                    **3 KB** |                  **-** |   **1.41 KB** |
| **OpenReadDisposeMetadataHandle** | **100**       | **14.138 ms** | **13.158 ms** | **3.4171 ms** |                   **17 KB** |                  **-** |   **7.03 KB** |
