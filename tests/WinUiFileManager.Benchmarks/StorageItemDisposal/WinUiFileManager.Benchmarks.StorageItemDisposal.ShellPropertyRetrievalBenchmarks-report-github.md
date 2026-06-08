```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8524/25H2/2025Update/HudsonValley2)
Intel Core i5-10210U CPU 1.60GHz (Max: 2.11GHz), 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.204
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3
  Job-LTMPYP : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3

Platform=X64  Arguments=/p:Platform=x64  InvocationCount=1  
IterationCount=5  LaunchCount=1  RunStrategy=Monitoring  
WarmupCount=1  Categories=WinRT,StorageItem  

```
| Method             | FileCount | Mean      | Error    | StdDev   | Allocated native memory | Native memory leak | Allocated |
|------------------- |---------- |----------:|---------:|---------:|------------------------:|-------------------:|----------:|
| **RetrieveProperties** | **10**        |  **46.11 ms** | **12.64 ms** | **3.283 ms** |                   **22 KB** |               **8 KB** |  **57.92 KB** |
| **RetrieveProperties** | **50**        | **203.85 ms** | **26.55 ms** | **6.896 ms** |                   **23 KB** |               **8 KB** | **272.21 KB** |
