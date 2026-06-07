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
| Method                             | FileCount | Mean      | Error     | StdDev   | Allocated native memory | Native memory leak | Allocated  |
|----------------------------------- |---------- |----------:|----------:|---------:|------------------------:|-------------------:|-----------:|
| **InspectorStreamsDiagnosticsHandler** | **100**       |  **70.15 ms** |  **45.88 ms** | **11.91 ms** |                       **-** |                  **-** |  **318.94 KB** |
| **InspectorStreamsDiagnosticsHandler** | **500**       | **423.11 ms** | **368.18 ms** | **95.62 ms** |                       **-** |                  **-** | **1593.94 KB** |
