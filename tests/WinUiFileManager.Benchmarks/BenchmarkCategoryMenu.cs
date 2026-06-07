using System.Reflection;

namespace WinUiFileManager.Benchmarks;

/// <summary>
/// Small interactive pre-filter shown before BenchmarkDotNet's own selector. It lets a local run narrow
/// benchmarks by an existing <see cref="BenchmarkCategoryAttribute"/> without replacing BenchmarkDotNet's
/// built-in menu when the user presses Enter.
/// </summary>
internal static class BenchmarkCategoryMenu
{
    public static string[] AddCategoryFilterIfSelected(string[] args, Assembly benchmarkAssembly)
    {
        if (args.Length > 0 || Console.IsInputRedirected)
        {
            return args;
        }

        var categories = GetCategoryEntries(benchmarkAssembly);
        if (categories.Length == 0)
        {
            return args;
        }

        WriteMenu(categories);
        while (true)
        {
            Console.Write("Category filter: ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                return args;
            }

            if (TryResolveCategory(input, categories, out var category))
            {
                Console.WriteLine($"Filtering benchmarks by category: {category}");
                Console.WriteLine();
                return [.. args, "--anyCategories", category];
            }

            Console.WriteLine("Unknown category. Press Enter for all benchmarks, or enter a listed number/name.");
        }
    }

    private static CategoryEntry[] GetCategoryEntries(Assembly benchmarkAssembly) =>
        benchmarkAssembly
            .GetTypes()
            .SelectMany(GetBenchmarkCategories)
            .GroupBy(static item => item.Category, StringComparer.OrdinalIgnoreCase)
            .Select(static group => new CategoryEntry(
                group.Key,
                group.Select(static item => item.BenchmarkName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .ToArray()))
            .OrderBy(static entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IEnumerable<(string Category, string BenchmarkName)> GetBenchmarkCategories(Type type)
    {
        var benchmarkName = type.Name;
        foreach (var attribute in type.GetCustomAttributes<BenchmarkCategoryAttribute>(inherit: true))
        {
            foreach (var category in attribute.Categories.Where(static category => !string.IsNullOrWhiteSpace(category)))
            {
                yield return (category, benchmarkName);
            }
        }

        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
        {
            if (!method.IsDefined(typeof(BenchmarkAttribute), inherit: true))
            {
                continue;
            }

            foreach (var attribute in method.GetCustomAttributes<BenchmarkCategoryAttribute>(inherit: true))
            {
                foreach (var category in attribute.Categories.Where(static category => !string.IsNullOrWhiteSpace(category)))
                {
                    yield return (category, benchmarkName);
                }
            }
        }
    }

    private static void WriteMenu(IReadOnlyList<CategoryEntry> categories)
    {
        Console.WriteLine("Available benchmark categories:");
        for (var i = 0; i < categories.Count; i++)
        {
            var entry = categories[i];
            Console.WriteLine($"  {i + 1}. {entry.Name}");
            Console.WriteLine($"     {string.Join(", ", entry.BenchmarkNames)}");
        }

        Console.WriteLine();
        Console.WriteLine("Press Enter to skip category filtering and show the full BenchmarkDotNet menu.");
        Console.WriteLine("Enter a category number or name to run benchmarks in that category.");
        Console.WriteLine();
    }

    private static bool TryResolveCategory(string input, IReadOnlyList<CategoryEntry> categories, out string category)
    {
        if (int.TryParse(input, out var number)
            && number >= 1
            && number <= categories.Count)
        {
            category = categories[number - 1].Name;
            return true;
        }

        foreach (var candidate in categories)
        {
            if (string.Equals(candidate.Name, input.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                category = candidate.Name;
                return true;
            }
        }

        category = string.Empty;
        return false;
    }

    private sealed record CategoryEntry(string Name, IReadOnlyList<string> BenchmarkNames);
}
