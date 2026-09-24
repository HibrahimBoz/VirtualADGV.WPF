using System.Globalization;
using VirtualADGV.WPF;
using Xunit;

namespace VirtualADGV.WPF.Tests;

/// <summary>
/// Sütun tipi başına davranış: hangi tip sayısal/tarih sayılır, değerleri (host'un
/// ToString() çıktısı, farklı kültürlerde) nasıl ifadeye yazılır ve listede nasıl sıralanır.
/// </summary>
public class TypeCoverageTests : IDisposable
{
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    public TypeCoverageTests() => CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    public void Dispose() => CultureInfo.CurrentCulture = _culture;

    private static readonly Type[] NumericTypeList =
    {
        typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
        typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal)
    };

    private static readonly string[] CultureNames = { "", "tr-TR", "de-DE", "en-US" };

    public static TheoryData<Type> NumericTypes()
    {
        var data = new TheoryData<Type>();
        foreach (var t in NumericTypeList)
        {
            data.Add(t);
            data.Add(typeof(Nullable<>).MakeGenericType(t));
        }
        return data;
    }

    public static TheoryData<Type, string> NumericTypesByCulture()
    {
        var data = new TheoryData<Type, string>();
        foreach (var t in NumericTypeList)
            foreach (var c in CultureNames)
                data.Add(t, c);
        return data;
    }

    /// <summary>Uç değerler dahil, tipin kendi değerleri (boxed).</summary>
    private static object[] Samples(Type t)
    {
        var rng = new Random(t.Name.Length * 31 + t.Name[0]);
        object[] fixedValues = t switch
        {
            _ when t == typeof(byte) => new object[] { byte.MinValue, byte.MaxValue, (byte)7 },
            _ when t == typeof(sbyte) => new object[] { sbyte.MinValue, sbyte.MaxValue, (sbyte)0, (sbyte)-1 },
            _ when t == typeof(short) => new object[] { short.MinValue, short.MaxValue, (short)-300 },
            _ when t == typeof(ushort) => new object[] { ushort.MinValue, ushort.MaxValue, (ushort)1000 },
            _ when t == typeof(int) => new object[] { int.MinValue, int.MaxValue, 0, -42 },
            _ when t == typeof(uint) => new object[] { uint.MinValue, uint.MaxValue, 123u },
            _ when t == typeof(long) => new object[] { long.MinValue, long.MaxValue, long.MaxValue - 1, 9007199254740993L, -1L },
            _ when t == typeof(ulong) => new object[] { ulong.MinValue, ulong.MaxValue, ulong.MaxValue - 1 },
            _ when t == typeof(float) => new object[] { float.MinValue, float.MaxValue, float.Epsilon, -3.5f, 1e-7f, 0.1f },
            _ when t == typeof(double) => new object[] { double.MinValue, double.MaxValue, double.Epsilon, -1.5, 1e-5, 1e30, -1e30, 0.1 },
            _ when t == typeof(decimal) => new object[] { decimal.MinValue, decimal.MaxValue, 12345.678m, -0.0001m, 0m },
            _ => throw new ArgumentOutOfRangeException(nameof(t))
        };

        var random = Enumerable.Range(0, 30).Select(_ => t switch
        {
            _ when t == typeof(float) => (object)(float)((rng.NextDouble() - 0.5) * 2000),
            _ when t == typeof(double) => (rng.NextDouble() - 0.5) * 2e6,
            _ when t == typeof(decimal) => Math.Round((decimal)((rng.NextDouble() - 0.5) * 2e6), 4),
            _ when t == typeof(byte) || t == typeof(ushort) || t == typeof(uint) || t == typeof(ulong)
                => Convert.ChangeType(rng.Next(0, 250), t, CultureInfo.InvariantCulture),
            _ => Convert.ChangeType(rng.Next(-120, 120), t, CultureInfo.InvariantCulture)
        });

        return fixedValues.Concat(random).ToArray();
    }

    private static string Format(object value, CultureInfo culture) => Convert.ToString(value, culture)!;

    /// <summary>Çıplak sayı token'ını orijinal tipine geri çevirip karşılaştırır.</summary>
    private static void AssertSameNumber(object expected, string token)
    {
        var inv = CultureInfo.InvariantCulture;
        switch (expected)
        {
            case float f: Assert.Equal(f, float.Parse(token, NumberStyles.Float, inv)); break;
            case double d: Assert.Equal(d, double.Parse(token, NumberStyles.Float, inv)); break;
            default: Assert.Equal(Convert.ToDecimal(expected, inv), decimal.Parse(token, NumberStyles.Float, inv)); break;
        }
    }

    // ── Tip sınıflandırması ──────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(NumericTypes))]
    public void NumericTypes_AreNumeric_NotDate(Type type)
    {
        Assert.True(FilterExpressionBuilder.IsNumericType(type));
        Assert.False(FilterExpressionBuilder.IsDateType(type));
    }

    [Theory]
    [InlineData(typeof(DateTime))]
    [InlineData(typeof(DateTime?))]
    [InlineData(typeof(TimeSpan))]
    [InlineData(typeof(TimeSpan?))]
    public void DateTypes_AreDate_NotNumeric(Type type)
    {
        Assert.True(FilterExpressionBuilder.IsDateType(type));
        Assert.False(FilterExpressionBuilder.IsNumericType(type));
    }

    [Theory]
    [InlineData(typeof(string))]
    [InlineData(typeof(bool))]
    [InlineData(typeof(bool?))]
    [InlineData(typeof(char))]
    [InlineData(typeof(Guid))]
    [InlineData(typeof(DateTimeOffset))]
    [InlineData(typeof(DateOnly))]
    [InlineData(typeof(TimeOnly))]
    [InlineData(typeof(object))]
    [InlineData(typeof(byte[]))]
    [InlineData(null)]
    public void OtherTypes_AreText(Type? type)
    {
        Assert.False(FilterExpressionBuilder.IsNumericType(type));
        Assert.False(FilterExpressionBuilder.IsDateType(type));
    }

    // ── İfade üretimi: sayısal tipler, her kültürün ToString() çıktısıyla ────────────────

    [Theory]
    [MemberData(nameof(NumericTypesByCulture))]
    public void NumericValues_AreBare_AndRoundTrip(Type type, string cultureName)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        var samples = Samples(type);
        var strings = samples.Select(v => Format(v, culture)).ToArray();

        string sql = FilterExpressionBuilder.BuildInCondition("Col", strings, isNumeric: true);

        Assert.StartsWith("\"Col\" IN (", sql);
        string[] tokens = sql["\"Col\" IN (".Length..^1].Split(',');
        Assert.Equal(samples.Length, tokens.Length);
        for (int i = 0; i < samples.Length; i++)
        {
            Assert.DoesNotContain("'", tokens[i]); // tırnaklı literal'e düşmemeli
            AssertSameNumber(samples[i], tokens[i]);
        }

        var nullableType = typeof(Nullable<>).MakeGenericType(type);
        for (int i = 0; i < samples.Length; i++)
        {
            Assert.Equal("\"Col\" >= " + tokens[i], FilterExpressionBuilder.BuildComparison("Col", "büyük veya eşittir", strings[i], type));
            Assert.Equal("\"Col\" >= " + tokens[i], FilterExpressionBuilder.BuildComparison("Col", "büyük veya eşittir", strings[i], nullableType));
        }
    }

    [Theory]
    [InlineData("", double.NaN)]
    [InlineData("", double.PositiveInfinity)]
    [InlineData("", double.NegativeInfinity)]
    [InlineData("tr-TR", double.PositiveInfinity)]
    [InlineData("tr-TR", double.NegativeInfinity)]
    public void NonFiniteDoubles_AreQuoted(string cultureName, double value)
    {
        string s = Format(value, CultureInfo.GetCultureInfo(cultureName));

        Assert.Equal($"\"Col\" IN ('{s}')", FilterExpressionBuilder.BuildInCondition("Col", new[] { s }, isNumeric: true));
        Assert.Equal($"\"Col\" = '{s}'", FilterExpressionBuilder.BuildComparison("Col", "eşittir", s, typeof(double)));
    }

    // ── İfade üretimi: sayısal olmayan tipler her zaman tırnaklı ────────────────────────

    public static TheoryData<Type, string> TextLikeValues() => new()
    {
        { typeof(bool), bool.TrueString },
        { typeof(bool?), bool.FalseString },
        { typeof(char), "'" },
        { typeof(Guid), Guid.Parse("6f9619ff-8b86-d011-b42d-00cf4fc964ff").ToString() },
        { typeof(DateTimeOffset), new DateTimeOffset(2024, 1, 5, 0, 0, 0, TimeSpan.FromHours(3)).ToString(CultureInfo.GetCultureInfo("tr-TR")) },
        { typeof(DateOnly), new DateOnly(2024, 1, 5).ToString(CultureInfo.InvariantCulture) },
        { typeof(TimeOnly), new TimeOnly(13, 45).ToString(CultureInfo.InvariantCulture) },
        { typeof(DateTime), new DateTime(2024, 1, 5, 13, 45, 0).ToString(CultureInfo.GetCultureInfo("tr-TR")) },
        { typeof(DateTime?), "2024-01-05" },
        { typeof(TimeSpan), new TimeSpan(2, 3, 4, 5).ToString() },
        { typeof(TimeSpan?), "-00:10:00" },
        { typeof(string), "1 OR 1=1" },
    };

    [Theory]
    [MemberData(nameof(TextLikeValues))]
    public void NonNumericTypes_AreQuoted(Type type, string value)
    {
        string quoted = "'" + value.Replace("'", "''") + "'";

        Assert.Equal($"\"Col\" IN ({quoted})", FilterExpressionBuilder.BuildInCondition("Col", new[] { value }, FilterExpressionBuilder.IsNumericType(type)));
        Assert.Equal($"\"Col\" <> {quoted}", FilterExpressionBuilder.BuildComparison("Col", "eşit değildir", value, type));
    }

    // ── Liste: sayısal tipler değere göre sıralanır, boş en sonda ────────────────────────

    [Theory]
    [MemberData(nameof(NumericTypesByCulture))]
    public void NumericList_SortsByValue(Type type, string cultureName)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        CultureInfo.CurrentCulture = culture;

        var samples = Samples(type).DistinctBy(v => Format(v, culture)).ToList();
        var expected = samples.OrderBy(v => v, Comparer<object>.Default).Select(v => Format(v, culture)).Append("").ToList();

        // Sabit tohumlu birkaç farklı karıştırma: sonuç giriş sırasına bağlı olmamalı
        // (eski decimal.MaxValue çakışması yalnızca bazı sıralarda görünüyordu)
        for (int seed = 0; seed < 5; seed++)
        {
            var rng = new Random(seed * 1000 + (type.Name + cultureName).Sum(c => c));
            var shuffled = expected.OrderBy(_ => rng.Next()).ToList();

            foreach (var t in new[] { type, typeof(Nullable<>).MakeGenericType(type) })
            {
                var items = FilterListBuilder.Build(shuffled, new HashSet<string>(), null, t, "(Empty)");
                Assert.Equal(expected, items.Select(i => i.Value));
                Assert.All(items, i => Assert.Empty(i.Children));
            }
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("tr-TR")]
    public void NumericList_NonFiniteValues(string cultureName)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        CultureInfo.CurrentCulture = culture;
        string F(double d) => Format(d, culture);

        var input = new[] { F(1.5), "", F(double.PositiveInfinity), F(double.NaN), F(-2), F(double.NegativeInfinity) };
        var items = FilterListBuilder.Build(input, new HashSet<string>(), null, typeof(double), "(Empty)");

        // -∞ < sayılar < +∞; NaN sayı değildir, sayılardan sonra; boş değer her zaman en sonda
        Assert.Equal(new[] { F(double.NegativeInfinity), F(-2), F(1.5), F(double.PositiveInfinity), F(double.NaN), "" },
                     items.Select(i => i.Value));
    }

    // ── Liste: tarih ağacı kültürün tarih biçiminde de kronolojik ────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("tr-TR")]
    [InlineData("de-DE")]
    [InlineData("en-US")]
    public void DateTree_IsChronological_InHostCulture(string cultureName)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        CultureInfo.CurrentCulture = culture;

        var rng = new Random(99);
        var dates = Enumerable.Range(0, 80)
            .Select(_ => new DateTime(2019 + rng.Next(6), 1 + rng.Next(12), 1 + rng.Next(28), rng.Next(24), 0, 0))
            .DistinctBy(d => d.ToString(culture))
            .ToList();
        var input = dates.Select(d => d.ToString(culture)).OrderBy(_ => rng.Next()).ToList();

        foreach (var type in new[] { typeof(DateTime), typeof(DateTime?) })
        {
            var roots = FilterListBuilder.Build(input, new HashSet<string>(), null, type, "(Empty)");

            Assert.Equal(dates.Select(d => d.Year.ToString()).Distinct().OrderBy(y => y), roots.Select(r => r.Value));

            var leaves = roots.SelectMany(y => y.Children).SelectMany(m => m.Children).ToList();
            Assert.Equal(dates.Count, leaves.Count);
            Assert.Equal(dates.OrderBy(d => d).Select(d => d.ToString(culture)), leaves.Select(l => l.Value));

            foreach (var year in roots)
                Assert.Equal(year.Children.Select(m => m.Value),
                             dates.Where(d => d.Year.ToString() == year.Value).OrderBy(d => d.Month)
                                  .Select(d => d.ToString("MMMM", culture)).Distinct());
        }
    }

    [Fact]
    public void DateTree_BlankFirst_UnparseableLast()
    {
        var roots = FilterListBuilder.Build(new[] { "zz", "2024-01-05", "", "aa", "2023-06-01" },
            new HashSet<string>(), null, typeof(DateTime), "(Empty)");

        Assert.Equal(new[] { "", "2023", "2024", "aa", "zz" }, roots.Select(r => r.Value));
    }

    // ── Liste: TimeSpan süreye göre düz liste (tarih ağacı değil) ────────────────────────

    [Theory]
    [InlineData(typeof(TimeSpan))]
    [InlineData(typeof(TimeSpan?))]
    public void TimeSpanList_IsFlat_AndSortedByDuration(Type type)
    {
        var durations = new[]
        {
            new TimeSpan(2, 3, 4, 5), TimeSpan.FromMinutes(90), TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(-10),
            TimeSpan.FromDays(10), TimeSpan.Zero, TimeSpan.FromMilliseconds(1.5)
        };
        var input = durations.Select(d => d.ToString()).Append("").Append("abc").Reverse().ToList();

        var items = FilterListBuilder.Build(input, new HashSet<string>(), null, type, "(Empty)");

        // Eskiden "01:30:00" bugünün tarihi sanılıp YIL > AY > GÜN ağacına giriyordu
        Assert.All(items, i => Assert.Empty(i.Children));
        Assert.Equal(durations.OrderBy(d => d).Select(d => d.ToString()).Append("abc").Append(""), items.Select(i => i.Value));
        Assert.Equal("(Empty)", items.Single(i => i.Value == "").DisplayText);
    }

    [Theory]
    [InlineData(typeof(TimeSpan), false)]
    [InlineData(typeof(TimeSpan?), false)]
    [InlineData(typeof(DateTime), true)]
    [InlineData(typeof(DateTime?), true)]
    [InlineData(typeof(string), false)]
    [InlineData(typeof(int), false)]
    public void UsesDateTree(Type type, bool expected) => Assert.Equal(expected, FilterListBuilder.UsesDateTree(type));

    // ── Liste: metin gibi davranan tipler kültüre göre alfabetik düz liste ───────────────

    public static TheoryData<Type, string[]> TextLikeLists() => new()
    {
        { typeof(bool), new[] { "True", "", "False" } },
        { typeof(char), new[] { "b", "'", "A", "a" } },
        { typeof(Guid), new[] { "ffffffff-0000-0000-0000-000000000000", "00000000-0000-0000-0000-000000000001" } },
        { typeof(DateTimeOffset), new[] { "01/05/2024 00:00:00 +03:00", "12/31/2023 00:00:00 +00:00" } },
        { typeof(DateOnly), new[] { "01/05/2024", "12/31/2023" } },
        { typeof(TimeOnly), new[] { "13:45", "09:30" } },
    };

    [Theory]
    [MemberData(nameof(TextLikeLists))]
    public void TextLikeTypes_AreFlat_AndSortedAsText(Type type, string[] values)
    {
        var items = FilterListBuilder.Build(values, new HashSet<string>(), null, type, "(Empty)");

        Assert.Equal(values.OrderBy(v => v, StringComparer.CurrentCulture), items.Select(i => i.Value));
        Assert.All(items, i => Assert.Empty(i.Children));
    }
}
