using System.Collections;
using System.Globalization;
using VirtualADGV.WPF;
using Xunit;

namespace VirtualADGV.WPF.Tests;

public class FilterListBuilderTests : IDisposable
{
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    public FilterListBuilderTests() => CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    public void Dispose() => CultureInfo.CurrentCulture = _culture;

    private static List<FilterItemModel> Build(IEnumerable<string> values, IEnumerable<string>? selected = null,
        ISet<string>? enabled = null, Type? type = null)
        => FilterListBuilder.Build(values, new HashSet<string>(selected ?? Array.Empty<string>()), enabled, type ?? typeof(string), "(Empty)");

    [Fact]
    public void DistinctValues_AreEnumeratedOnce()
    {
        // Host tembel bir sorgu (ör. veritabanı) geçebilir; eski kod iki kez dolaşıyordu
        var source = new CountingEnumerable(new[] { "b", "a", "c" });

        Build(source);

        Assert.Equal(1, source.EnumerationCount);
    }

    [Fact]
    public void NoActiveFilter_AllChecked()
    {
        var items = Build(new[] { "b", "a" });
        Assert.Equal(new[] { "a", "b" }, items.Select(i => i.Value));
        Assert.All(items, i => Assert.True(i.IsChecked));
    }

    [Fact]
    public void ActiveFilter_ChecksOnlySelected()
    {
        var items = Build(new[] { "a", "b", "c" }, selected: new[] { "b" });
        Assert.Equal(new bool?[] { false, true, false }, items.Select(i => i.IsChecked));
    }

    [Fact]
    public void DisabledValues_AreNeverChecked()
    {
        var items = Build(new[] { "a", "b", "c" }, enabled: new HashSet<string> { "a", "c" });
        Assert.Equal(new[] { true, false, true }, items.Select(i => i.IsEnabled));
        Assert.Equal(new bool?[] { true, false, true }, items.Select(i => i.IsChecked));
    }

    [Fact]
    public void Numeric_SortsByValue_BlankLast()
    {
        var items = Build(new[] { "10", "", "9", "1,5" }, type: typeof(double));
        Assert.Equal(new[] { "1,5", "9", "10", "" }, items.Select(i => i.Value));
        Assert.Equal("(Empty)", items[^1].DisplayText);
    }

    [Fact]
    public void NullValue_IsTreatedAsBlank()
    {
        var items = Build(new string[] { null!, "a" });
        Assert.Equal("", items[0].Value);
        Assert.Equal("(Empty)", items[0].DisplayText);
    }

    [Fact]
    public void Dates_BuildYearMonthDayTree_WithDerivedStates()
    {
        var items = Build(new[] { "2024-01-05", "2024-01-06", "2023-12-31", "", "not a date" },
            selected: new[] { "2024-01-05", "2023-12-31" }, type: typeof(DateTime));

        var y2023 = items.Single(i => i.Value == "2023");
        var y2024 = items.Single(i => i.Value == "2024");
        var jan = Assert.Single(y2024.Children);

        Assert.Equal("January", jan.Value);
        Assert.Equal(new[] { "05", "06" }, jan.Children.Select(d => d.DisplayText));
        Assert.Null(jan.IsChecked);
        Assert.Null(y2024.IsChecked);
        Assert.True(y2023.IsChecked);
        Assert.False(items.Single(i => i.Value == "").IsChecked);
        Assert.False(items.Single(i => i.Value == "not a date").IsChecked);
    }

    [Fact]
    public void Dates_ParentState_ReflectsMonthsAddedLater()
    {
        // Eski kodda: Mart işaretsiz → yıl "false" oldu; sonra eklenen tamamen seçili Kasım
        // setter tetiklemediği için yıl "false" kalıyordu (doğrusu: belirsiz).
        var items = Build(new[] { "2021-03-10", "2021-11-01" }, selected: new[] { "2021-11-01" }, type: typeof(DateTime));

        var year = Assert.Single(items);
        Assert.Equal(new bool?[] { false, true }, year.Children.Select(m => m.IsChecked));
        Assert.Null(year.IsChecked);
    }

    // ── Eski algoritmayla eşdeğerlik (rastgele girdiler) ─────────────────────────────────

    [Theory]
    [InlineData(typeof(string))]
    [InlineData(typeof(int))]
    [InlineData(typeof(DateTime))]
    public void MatchesLegacyAlgorithm(Type type)
    {
        var rng = new Random(7);
        for (int round = 0; round < 200; round++)
        {
            var values = Enumerable.Range(0, rng.Next(0, 60)).Select(_ => RandomValue(rng, type)).Distinct().ToList();
            var selected = values.Where(_ => rng.Next(3) == 0).ToList();
            ISet<string>? enabled = rng.Next(2) == 0 ? null : values.Where(_ => rng.Next(4) != 0).ToHashSet();

            var expected = LegacyBuild(values, selected, enabled, type, "(Empty)");
            var actual = Build(values, selected, enabled, type);

            // Yapı ve yaprak durumları eski algoritmayla birebir aynı olmalı. Ebeveyn (yıl/ay)
            // durumları ise eski kodda bayat kalabiliyordu (bkz. Dates_ParentState_...), bu yüzden
            // onlar çocuklardan türetilmiş olma kuralıyla doğrulanır.
            Assert.Equal(Describe(expected, parentStates: false), Describe(actual, parentStates: false));
            AssertParentStatesDerivedFromChildren(actual);
        }
    }

    [Fact]
    public void LargeInput_WithLargeActiveFilter_BuildsQuickly()
    {
        // Eski kod List.Contains ile O(n·m): 200k × 100k ≈ 10¹⁰ karşılaştırma (dakikalar)
        var values = Enumerable.Range(0, 200_000).Select(i => "v" + i).ToList();
        var selected = values.Where((_, i) => i % 2 == 0).ToList();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var items = Build(values, selected);
        sw.Stop();

        Assert.Equal(200_000, items.Count);
        Assert.Equal(100_000, items.Count(i => i.IsChecked == true));
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10), $"Took {sw.Elapsed}");
    }

    private static string RandomValue(Random rng, Type type)
    {
        if (rng.Next(15) == 0) return "";
        if (type == typeof(int)) return rng.Next(-50, 50).ToString() + (rng.Next(4) == 0 ? ",5" : "");
        if (type == typeof(DateTime))
            return rng.Next(8) == 0 ? "junk" + rng.Next(5) : new DateTime(2020 + rng.Next(3), 1 + rng.Next(12), 1 + rng.Next(28)).ToString("yyyy-MM-dd");
        return ((char)('a' + rng.Next(6))).ToString() + rng.Next(10);
    }

    private static void AssertParentStatesDerivedFromChildren(IEnumerable<FilterItemModel> nodes)
    {
        foreach (var n in nodes.Where(n => n.Children.Count > 0))
        {
            AssertParentStatesDerivedFromChildren(n.Children);
            bool? expected = n.Children.All(c => c.IsChecked == true) ? true
                           : n.Children.All(c => c.IsChecked == false) ? false
                           : null;
            Assert.Equal(expected, n.IsChecked);
        }
    }

    private static string Describe(IEnumerable<FilterItemModel> nodes, bool parentStates = true)
    {
        var sb = new System.Text.StringBuilder();
        void Walk(IEnumerable<FilterItemModel> list, int depth)
        {
            foreach (var n in list)
            {
                sb.Append(' ', depth * 2).Append(n.Value).Append('|').Append(n.DisplayText).Append('|')
                  .Append(n.Children.Count > 0 && !parentStates ? "-" : n.IsChecked?.ToString() ?? "null")
                  .Append('|').Append(n.IsEnabled).Append('\n');
                Walk(n.Children, depth + 1);
            }
        }
        Walk(nodes, 0);
        return sb.ToString();
    }

    /// <summary>The pre-fix AdvancedCustomFilterPopup.LoadDistinctValues logic, kept as a reference.</summary>
    private static List<FilterItemModel> LegacyBuild(IEnumerable<string> distinctValues, List<string> previousSelectedValues,
        ISet<string>? enabledValues, Type columnType, string emptyText)
    {
        var result = new List<FilterItemModel>();
        bool allSelected = !previousSelectedValues.Any();
        bool isNumeric = columnType == typeof(int) || columnType == typeof(long) || columnType == typeof(double) ||
                         columnType == typeof(float) || columnType == typeof(decimal);
        bool isDate = columnType == typeof(DateTime) || columnType == typeof(TimeSpan);

        List<string> sortedValues = isNumeric
            ? distinctValues.OrderBy(v =>
            {
                if (string.IsNullOrEmpty(v)) return decimal.MaxValue;
                return decimal.TryParse(v.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal d) ? d : decimal.MaxValue;
            }).ToList()
            : distinctValues.OrderBy(v => v).ToList();

        if (isDate)
        {
            var years = new Dictionary<string, FilterItemModel>();
            foreach (string val in sortedValues)
            {
                bool enabled = enabledValues == null || enabledValues.Contains(val);
                bool isSel = enabled && (allSelected || previousSelectedValues.Contains(val));

                if (string.IsNullOrEmpty(val))
                {
                    result.Add(new FilterItemModel { Value = "", IsSelected = isSel, IsEnabled = enabled, DisplayTextOverride = emptyText });
                    continue;
                }

                if (DateTime.TryParse(val, out DateTime dt) || (val.Length >= 10 && DateTime.TryParseExact(val.Substring(0, 10), "yyyy-MM-dd", null, DateTimeStyles.None, out dt)))
                {
                    string yStr = dt.Year.ToString();
                    string mStr = dt.ToString("MMMM");
                    string dStr = dt.Day.ToString("00");

                    if (!years.ContainsKey(yStr))
                    {
                        var yNode = new FilterItemModel { Value = yStr, IsExpanded = false };
                        result.Add(yNode);
                        years[yStr] = yNode;
                    }

                    var yParent = years[yStr];
                    var mParent = yParent.Children.FirstOrDefault(c => c.Value == mStr);
                    if (mParent == null)
                    {
                        mParent = new FilterItemModel { Value = mStr, Parent = yParent, IsExpanded = false };
                        yParent.Children.Add(mParent);
                    }

                    var dNode = new FilterItemModel { Value = val, Parent = mParent, IsExpanded = false, IsEnabled = enabled, DisplayTextOverride = dStr };
                    mParent.Children.Add(dNode);
                    dNode.IsChecked = isSel ? true : false;
                }
                else
                {
                    result.Add(new FilterItemModel { Value = val, IsChecked = isSel ? true : false, IsEnabled = enabled });
                }
            }
        }
        else
        {
            foreach (string val in sortedValues)
            {
                bool enabled = enabledValues == null || enabledValues.Contains(val);
                bool isSel = enabled && (allSelected || previousSelectedValues.Contains(val));
                result.Add(new FilterItemModel { Value = val, IsChecked = isSel ? true : false, IsEnabled = enabled, DisplayTextOverride = string.IsNullOrEmpty(val) ? emptyText : null });
            }
        }
        return result;
    }

    private sealed class CountingEnumerable : IEnumerable<string>
    {
        private readonly IEnumerable<string> _inner;
        public int EnumerationCount { get; private set; }
        public CountingEnumerable(IEnumerable<string> inner) => _inner = inner;
        public IEnumerator<string> GetEnumerator() { EnumerationCount++; return _inner.GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
