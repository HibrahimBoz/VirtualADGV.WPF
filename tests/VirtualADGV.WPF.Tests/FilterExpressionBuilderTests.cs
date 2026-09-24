using System.Text;
using System.Text.RegularExpressions;
using VirtualADGV.WPF;
using Xunit;

namespace VirtualADGV.WPF.Tests;

public class FilterExpressionBuilderTests
{
    // ── Geriye dönük uyumluluk: zararsız girdilerde çıktı eski kodla birebir aynı ──────────

    [Fact]
    public void InCondition_Text_MatchesLegacyOutput()
    {
        Assert.Equal("\"City\" IN ('Ankara','İzmir')",
            FilterExpressionBuilder.BuildInCondition("City", new[] { "Ankara", "İzmir" }, isNumeric: false));
    }

    [Fact]
    public void InCondition_TextWithBlank_AddsIsNull()
    {
        Assert.Equal("(\"City\" IN ('','Ankara') OR \"City\" IS NULL)",
            FilterExpressionBuilder.BuildInCondition("City", new[] { "", "Ankara" }, isNumeric: false));
    }

    [Fact]
    public void InCondition_Numeric_MatchesLegacyOutput()
    {
        Assert.Equal("(\"Price\" IN (1,2.5,-3,1E+20,NULL) OR \"Price\" IS NULL)",
            FilterExpressionBuilder.BuildInCondition("Price", new[] { "1", "2,5", "-3", "1E+20", "" }, isNumeric: true));
    }

    [Fact]
    public void InCondition_NoValues_IsFalseCondition()
    {
        Assert.Equal("1=0", FilterExpressionBuilder.BuildInCondition("City", Array.Empty<string>(), isNumeric: false));
    }

    [Theory]
    [InlineData("içerir", "abc", "\"Name\" LIKE '%abc%'")]
    [InlineData("başlar", "abc", "\"Name\" LIKE 'abc%'")]
    [InlineData("biter", "abc", "\"Name\" LIKE '%abc'")]
    [InlineData("içermez", "abc", "\"Name\" NOT LIKE '%abc%'")]
    [InlineData("eşittir", "  abc  ", "\"Name\" = 'abc'")]
    [InlineData("eşit değildir", "abc", "\"Name\" <> 'abc'")]
    public void Comparison_Text_MatchesLegacyOutput(string op, string value, string expected)
    {
        Assert.Equal(expected, FilterExpressionBuilder.BuildComparison("Name", op, value, typeof(string)));
    }

    [Theory]
    [InlineData("büyük", "12,5", "\"Price\" > 12.5")]
    [InlineData("büyük veya eşittir", "10", "\"Price\" >= 10")]
    [InlineData("küçük", "-0.5", "\"Price\" < -0.5")]
    [InlineData("küçük veya eşittir", "1e3", "\"Price\" <= 1e3")]
    [InlineData("eşittir", "7", "\"Price\" = 7")]
    public void Comparison_Numeric_MatchesLegacyOutput(string op, string value, string expected)
    {
        Assert.Equal(expected, FilterExpressionBuilder.BuildComparison("Price", op, value, typeof(decimal)));
    }

    [Fact]
    public void Comparison_Date_IsQuoted()
    {
        Assert.Equal("\"Date\" >= '2024-01-01'",
            FilterExpressionBuilder.BuildComparison("Date", "sonra veya eşit", "2024-01-01", typeof(DateTime)));
    }

    [Fact]
    public void Comparison_EmptyValue_IsEmpty()
    {
        Assert.Equal("", FilterExpressionBuilder.BuildComparison("Name", "eşittir", "   ", typeof(string)));
    }

    [Fact]
    public void Combine_BothAndSingle()
    {
        Assert.Equal("(a AND b)", FilterExpressionBuilder.CombineConditions("a", "b", useAnd: true));
        Assert.Equal("(a OR b)", FilterExpressionBuilder.CombineConditions("a", "b", useAnd: false));
        Assert.Equal("a", FilterExpressionBuilder.CombineConditions("a", "", useAnd: true));
        Assert.Equal("b", FilterExpressionBuilder.CombineConditions("", "b", useAnd: true));
    }

    // ── Hata düzeltmesi: metin değerlerde virgül artık noktaya çevrilmiyor ────────────────

    [Fact]
    public void Comparison_Text_KeepsComma()
    {
        Assert.Equal("\"Name\" = 'Smith, John'",
            FilterExpressionBuilder.BuildComparison("Name", "eşittir", "Smith, John", typeof(string)));
    }

    // ── Güvenlik: enjeksiyon denemeleri ──────────────────────────────────────────────────

    [Fact]
    public void ColumnName_WithQuote_IsEscaped()
    {
        // CSV başlığından gelen kötü niyetli sütun adı
        string col = "Name\" = \"Name\" OR 1=1 --";
        Assert.Equal("\"Name\"\" = \"\"Name\"\" OR 1=1 --\" IN ('x')",
            FilterExpressionBuilder.BuildInCondition(col, new[] { "x" }, isNumeric: false));
    }

    [Fact]
    public void TextValue_WithQuote_IsEscaped()
    {
        Assert.Equal("\"Name\" IN ('O''Brien')",
            FilterExpressionBuilder.BuildInCondition("Name", new[] { "O'Brien" }, isNumeric: false));
    }

    [Theory]
    [InlineData("1) OR (1=1")]
    [InlineData("0); DROP TABLE Records; --")]
    [InlineData("1 OR 1=1")]
    [InlineData("NaN")]
    [InlineData("1.234.567")]
    public void NumericInValue_NotANumber_IsQuoted(string value)
    {
        string expected = "\"Price\" IN (" + FilterExpressionBuilder.QuoteString(value) + ")";
        Assert.Equal(expected, FilterExpressionBuilder.BuildInCondition("Price", new[] { value }, isNumeric: true));
    }

    [Theory]
    [InlineData("1 OR 1=1", "\"Price\" = '1 OR 1=1'")]
    [InlineData("0; DROP TABLE Records; --", "\"Price\" = '0; DROP TABLE Records; --'")]
    [InlineData("1' OR '1'='1", "\"Price\" = '1'' OR ''1''=''1'")]
    public void NumericComparison_UserTypedInjection_IsQuoted(string typed, string expected)
    {
        Assert.Equal(expected, FilterExpressionBuilder.BuildComparison("Price", "eşittir", typed, typeof(int)));
    }

    [Fact]
    public void LikeValue_WithQuote_IsEscaped()
    {
        Assert.Equal("\"Name\" LIKE '%x'' OR ''1''=''1%'",
            FilterExpressionBuilder.BuildComparison("Name", "içerir", "x' OR '1'='1", typeof(string)));
    }

    [Theory]
    [InlineData("12", true, "12")]
    [InlineData(" -1,5 ", true, "-1.5")]
    [InlineData("+.5", true, "+.5")]
    [InlineData("5.", true, "5.")]
    [InlineData("1e-7", true, "1e-7")]
    [InlineData("", false, "")]
    [InlineData("-", false, "")]
    [InlineData(".", false, "")]
    [InlineData("1e", false, "")]
    [InlineData("١٢٣", false, "")] // ASCII dışı rakamlar SQL'de sayı değildir
    [InlineData("12abc", false, "")]
    [InlineData("1 2", false, "")]
    public void TryNormalizeNumber_AcceptsOnlyPlainLiterals(string input, bool ok, string normalized)
    {
        Assert.Equal(ok, FilterExpressionBuilder.TryNormalizeNumber(input, out var result));
        Assert.Equal(normalized, result);
    }

    [Theory]
    [InlineData(typeof(int), true)]
    [InlineData(typeof(short), true)]
    [InlineData(typeof(byte), true)]
    [InlineData(typeof(ulong), true)]
    [InlineData(typeof(decimal?), true)]
    [InlineData(typeof(string), false)]
    [InlineData(typeof(bool), false)]
    [InlineData(typeof(DateTime), false)]
    public void IsNumericType(Type type, bool expected) => Assert.Equal(expected, FilterExpressionBuilder.IsNumericType(type));

    // ── Özellik testi: rastgele kötü niyetli girdiler ifadenin yapısını asla bozamaz ─────

    private const string Alphabet = "ab1.,'\"();-= *%_\\\n\tıİ0e+";

    private static string RandomString(Random rng)
    {
        int len = rng.Next(0, 12);
        var sb = new StringBuilder(len);
        for (int i = 0; i < len; i++) sb.Append(Alphabet[rng.Next(Alphabet.Length)]);
        return sb.ToString();
    }

    [Fact]
    public void Fuzz_InCondition_StructureIsPreserved()
    {
        var rng = new Random(20260924);
        var shape = new Regex(@"^(\()?I IN \((N|S|NULL)(,(N|S|NULL))*\)( OR I IS NULL\))?$");

        for (int round = 0; round < 5000; round++)
        {
            string col = RandomString(rng);
            var values = Enumerable.Range(0, rng.Next(1, 6)).Select(_ => RandomString(rng)).ToArray();
            bool numeric = rng.Next(2) == 0;

            string sql = FilterExpressionBuilder.BuildInCondition(col, values, numeric);
            string s = SqlShape.Of(sql);

            Assert.Matches(shape, s);
            string list = Regex.Match(s, @"I IN \(([^)]*)\)").Groups[1].Value;
            Assert.Equal(values.Length, list.Split(',').Length);
        }
    }

    [Fact]
    public void Fuzz_Comparison_StructureIsPreserved()
    {
        var rng = new Random(424242);
        string[] ops = { "eşittir", "eşit değildir", "başlar", "biter", "içerir", "içermez", "büyük",
                         "büyük veya eşittir", "küçük", "küçük veya eşittir", "önce", "sonra" };
        Type[] types = { typeof(string), typeof(int), typeof(double), typeof(DateTime) };
        var shape = new Regex(@"^I (=|<>|>|>=|<|<=|LIKE|NOT LIKE) (N|S)$");

        for (int round = 0; round < 5000; round++)
        {
            string value = RandomString(rng);
            string sql = FilterExpressionBuilder.BuildComparison(RandomString(rng), ops[rng.Next(ops.Length)], value, types[rng.Next(types.Length)]);
            if (string.IsNullOrWhiteSpace(value)) { Assert.Equal("", sql); continue; }

            Assert.Matches(shape, SqlShape.Of(sql));
        }
    }
}

/// <summary>
/// Reduces a SQL expression to its structure: every '...' literal becomes S, every "..."
/// identifier becomes I (both honouring doubled-quote escapes), bare numbers become N.
/// If a value could break out of its quotes, the shape would no longer match the template.
/// </summary>
internal static class SqlShape
{
    private static readonly Regex Number = new(@"(?<![\w.])[+-]?(?:\d+\.?\d*|\.\d+)(?:[eE][+-]?\d+)?(?![\w.])");

    public static string Of(string sql)
    {
        var sb = new StringBuilder();
        int i = 0;
        while (i < sql.Length)
        {
            char c = sql[i];
            if (c != '\'' && c != '"') { sb.Append(c); i++; continue; }

            char q = c;
            i++;
            while (true)
            {
                if (i >= sql.Length) throw new Xunit.Sdk.XunitException("Unterminated literal in: " + sql);
                if (sql[i] == q)
                {
                    if (i + 1 < sql.Length && sql[i + 1] == q) { i += 2; continue; }
                    i++;
                    break;
                }
                i++;
            }
            sb.Append(q == '\'' ? 'S' : 'I');
        }
        return Number.Replace(sb.ToString(), "N");
    }
}
