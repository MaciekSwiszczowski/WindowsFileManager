using System.Text;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;

// EtlLeakAnalyzer — reads a BenchmarkDotNet NativeMemoryProfiler ETL and attributes unfreed native HEAP
// allocations to their allocation call stacks, so a "native memory leak" reported by BDN can be traced to the
// exact module/method that allocated and never freed.
//
// HOW IT WORKS
//   1. TraceLog.CreateFromEventTraceLogFile converts the .etl to an indexed .etlx (links call-stack events to the
//      events that caused them and resolves module/method names). Heavy + slow on large traces.
//   2. We subscribe to the NT-heap provider (HeapTraceAlloc/Free/ReAlloc) and keep a map address -> (size, stack).
//      Free removes the address; ReAlloc frees the old and adds the new. Whatever remains at the end is "leaked"
//      (allocated but never freed within the trace), attributed to its allocation stack.
//   3. Results are grouped by call-stack signature. Module-level frames resolve without PDBs (enough to name the
//      culprit DLL, e.g. propsys/clrjit); managed frames resolve to full method names.
//
// IMPORTANT LIMITATION (read before trusting the numbers)
//   BDN's reported per-op "Native memory leak" is bracketed to the MEASURED iterations only. This tool diffs the
//   WHOLE trace (startup + JIT + warmup + workload + teardown). So the survivors are dominated by process-lifetime
//   startup allocations (CLR/JIT/ICU/DI/BDN harness), and a small per-op workload leak (a few KB/op) is buried in
//   that noise. Use the keyword subset (3rd arg) to isolate a suspected path by module/method name; even then,
//   absolute "leaked" counts are approximate (reused addresses, frees on heaps/paths not matched). Treat this as a
//   "who is allocating on this path" locator, not a precise per-op leak meter. To match BDN exactly you would need
//   to restrict processing to BDN's WorkloadActual interval (e.g. via its EventSource markers in the trace).
//
// USAGE
//   dotnet run --project tools/EtlLeakAnalyzer -c Release -- <trace.etl> [topN] [keyword,keyword,...]
//     topN     number of top leaked stacks to print (default 20)
//     keywords optional comma-separated, case-insensitive frame substrings; when given, a subset report is added
//              for allocations whose stack passes through any keyword (e.g. "propsys,cldapi" or a method name).

if (args.Length < 1)
{
    Console.Error.WriteLine("usage: EtlLeakAnalyzer <trace.etl> [topN] [keyword,keyword,...]");
    return 1;
}

var etlPath = args[0];
var topN = args.Length > 1 ? int.Parse(args[1]) : 20;
string[] keywords = args.Length > 2
    ? args[2].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    : [];

Console.WriteLine($"Converting {Path.GetFileName(etlPath)} -> etlx (heavy on large traces) ...");
var etlxPath = TraceLog.CreateFromEventTraceLogFile(etlPath);
using var log = new TraceLog(etlxPath);
Console.WriteLine($"events={log.EventCount:N0}  duration={log.SessionDuration.TotalSeconds:F1}s");

var source = log.Events.GetSource();
var heap = new HeapTraceProviderTraceEventParser(source);

// Live heap allocations keyed by address. BDN scopes heap tracing to the benchmark process.
var live = new Dictionary<ulong, (long Length, CallStackIndex Stack, int Pid, string Proc)>();
long totalAlloc = 0, totalFree = 0;
long allocEvents = 0, freeEvents = 0, reallocEvents = 0;

// Subset accounting: allocations whose stack passes through any of the supplied keyword frames.
var ours = new HashSet<ulong>();
long ourAlloc = 0, ourFreed = 0;

heap.HeapTraceAlloc += data =>
{
    var len = data.AllocSize;
    if (len <= 0)
    {
        return;
    }

    allocEvents++;
    totalAlloc += len;
    var stack = data.CallStackIndex();
    live[data.AllocAddress] = (len, stack, data.ProcessID, data.ProcessName ?? "");
    if (keywords.Length > 0 && StackHasKeyword(stack))
    {
        ours.Add(data.AllocAddress);
        ourAlloc += len;
    }
};

heap.HeapTraceFree += data =>
{
    freeEvents++;
    if (live.Remove(data.FreeAddress, out var region))
    {
        totalFree += region.Length;
        if (ours.Remove(data.FreeAddress))
        {
            ourFreed += region.Length;
        }
    }
};

heap.HeapTraceReAlloc += data =>
{
    reallocEvents++;
    if (live.Remove(data.OldAllocAddress, out var region))
    {
        totalFree += region.Length;
        if (ours.Remove(data.OldAllocAddress))
        {
            ourFreed += region.Length;
        }
    }

    var len = data.NewAllocSize;
    if (len > 0)
    {
        totalAlloc += len;
        var stack = data.CallStackIndex();
        live[data.NewAllocAddress] = (len, stack, data.ProcessID, data.ProcessName ?? "");
        if (keywords.Length > 0 && StackHasKeyword(stack))
        {
            ours.Add(data.NewAllocAddress);
            ourAlloc += len;
        }
    }
};

source.Process();

Console.WriteLine($"allocEvents={allocEvents:N0} ({totalAlloc:N0} B)  freeEvents={freeEvents:N0} ({totalFree:N0} B)  reallocEvents={reallocEvents:N0}");
Console.WriteLine($"leaked allocations={live.Count:N0}  leaked bytes={live.Values.Sum(static r => r.Length):N0}");

if (keywords.Length > 0)
{
    var ourLeakedBytes = live.Where(kv => ours.Contains(kv.Key)).Sum(static kv => kv.Value.Length);
    Console.WriteLine($"\n=== keyword subset (stack matches {string.Join('/', keywords)}) ===");
    Console.WriteLine($"  allocated={ourAlloc:N0} B  freed={ourFreed:N0} B  LEAKED={ourLeakedBytes:N0} B  ({ours.Count:N0} regions still live)");
}

// Per-process leaked totals.
var byProc = live.Values
    .GroupBy(static r => (r.Pid, r.Proc))
    .Select(static g => (g.Key.Proc, g.Key.Pid, Bytes: g.Sum(static r => r.Length), Count: g.Count()))
    .OrderByDescending(static x => x.Bytes)
    .ToList();

Console.WriteLine("\n=== leaked bytes by process ===");
foreach (var p in byProc.Take(10))
{
    Console.WriteLine($"  {p.Bytes,15:N0} B  {p.Count,7:N0} regions  pid={p.Pid}  {p.Proc}");
}

// Aggregate leaked bytes by allocation call-stack signature (module!method per frame).
var sigCache = new Dictionary<CallStackIndex, string>();
var byStack = new Dictionary<string, (long Bytes, int Count)>();
var byStackOurs = new Dictionary<string, (long Bytes, int Count)>();
foreach (var kv in live)
{
    var r = kv.Value;
    var sig = StackSig(r.Stack);
    var cur = byStack.TryGetValue(sig, out var v) ? v : default;
    byStack[sig] = (cur.Bytes + r.Length, cur.Count + 1);
    if (ours.Contains(kv.Key))
    {
        var co = byStackOurs.TryGetValue(sig, out var vo) ? vo : default;
        byStackOurs[sig] = (co.Bytes + r.Length, co.Count + 1);
    }
}

if (keywords.Length > 0)
{
    Console.WriteLine($"\n=== top {topN} LEAKED stacks WITHIN keyword subset ===");
    foreach (var kv in byStackOurs.OrderByDescending(static x => x.Value.Bytes).Take(topN))
    {
        Console.WriteLine($"\n----- {kv.Value.Bytes:N0} B  ({kv.Value.Count:N0} regions) -----");
        Console.WriteLine(kv.Key);
    }
}

Console.WriteLine($"\n=== top {topN} leaked allocation stacks (all) ===");
foreach (var kv in byStack.OrderByDescending(static x => x.Value.Bytes).Take(topN))
{
    Console.WriteLine($"\n----- {kv.Value.Bytes:N0} B  ({kv.Value.Count:N0} regions) -----");
    Console.WriteLine(kv.Key);
}

return 0;

// Walks an allocation's call stack looking for any keyword as a case-insensitive substring of a frame's
// "module!method" name. Used to isolate a suspected code path by name.
bool StackHasKeyword(CallStackIndex idx)
{
    var i = idx;
    var depth = 0;
    while (i != CallStackIndex.Invalid && depth < 40)
    {
        var cai = log.CallStacks.CodeAddressIndex(i);
        if (cai != CodeAddressIndex.Invalid)
        {
            var name = log.CodeAddresses.Name(cai);
            if (!string.IsNullOrEmpty(name))
            {
                foreach (var kw in keywords)
                {
                    if (name.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
        }

        i = log.CallStacks.Caller(i);
        depth++;
    }

    return false;
}

// Builds a stable string signature for a call stack (one "module!method" per frame) for grouping leaks. Cached
// because many allocations share a stack.
string StackSig(CallStackIndex idx)
{
    if (sigCache.TryGetValue(idx, out var cached))
    {
        return cached;
    }

    var sb = new StringBuilder();
    var i = idx;
    var depth = 0;
    while (i != CallStackIndex.Invalid && depth < 30)
    {
        var cai = log.CallStacks.CodeAddressIndex(i);
        if (cai == CodeAddressIndex.Invalid)
        {
            sb.Append("    ?\n");
        }
        else
        {
            var name = log.CodeAddresses.Name(cai);
            if (string.IsNullOrEmpty(name))
            {
                name = log.CodeAddresses.ModuleFile(cai)?.Name ?? "?";
            }

            sb.Append("    ").Append(name).Append('\n');
        }

        i = log.CallStacks.Caller(i);
        depth++;
    }

    var result = sb.Length == 0 ? "    <no stack>\n" : sb.ToString();
    sigCache[idx] = result;
    return result;
}
