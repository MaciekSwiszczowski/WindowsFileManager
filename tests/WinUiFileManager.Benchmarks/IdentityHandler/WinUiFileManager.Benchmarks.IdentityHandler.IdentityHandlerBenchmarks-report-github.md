```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8524/25H2/2025Update/HudsonValley2)
Intel Core i5-10210U CPU 1.60GHz (Max: 2.11GHz), 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.204
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3
  Job-PNPGZA : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3

Platform=X64  Arguments=/p:Platform=x64  RunStrategy=Monitoring  

```
| Method                              | FileCount | Mean     | Error     | StdDev   | Allocated native memory | Native memory leak | Allocated  |
|------------------------------------ |---------- |---------:|----------:|---------:|------------------------:|-------------------:|-----------:|
| **InspectorIdentityDiagnosticsHandler** | **100**       | **110.1 ms** |  **32.07 ms** | **21.21 ms** |                  **757 KB** |              **20 KB** |  **422.29 KB** |
| **InspectorIdentityDiagnosticsHandler** | **500**       | **510.8 ms** | **115.31 ms** | **76.27 ms** |                  **734 KB** |              **13 KB** | **2062.69 KB** |
