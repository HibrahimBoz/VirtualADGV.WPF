using System;
using System.Collections.Generic;
using System.Text;

namespace VirtualADGV.WPF
{
    /// <summary>
    /// Builds the SQL-like filter conditions raised through the grid's FilterChanged event.
    /// Every identifier and literal goes through here, so column names and values that come
    /// from data (CSV headers, cell values) or from user input cannot break out of the expression.
    /// Kept free of WPF types so it can be unit-tested on any platform.
    /// </summary>
    internal static class FilterExpressionBuilder
    {
        /// <summary>Wraps a column name in double quotes, doubling any embedded quote (SQL standard).</summary>
        public static string QuoteIdentifier(string? name) => "\"" + (name ?? string.Empty).Replace("\"", "\"\"") + "\"";

        /// <summary>Wraps a value in single quotes, doubling any embedded quote (SQL standard).</summary>
        public static string QuoteString(string? value) => "'" + EscapeStringBody(value) + "'";

        /// <summary>Escapes a value for use inside an already single-quoted literal (e.g. LIKE patterns).</summary>
        public static string EscapeStringBody(string? value) => (value ?? string.Empty).Replace("'", "''");

        /// <summary>True for CLR numeric types (including their Nullable forms).</summary>
        public static bool IsNumericType(Type? type)
        {
            if (type == null) return false;
            type = Nullable.GetUnderlyingType(type) ?? type;
            return type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte) ||
                   type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort) || type == typeof(sbyte) ||
                   type == typeof(double) || type == typeof(float) || type == typeof(decimal);
        }

        /// <summary>True for DateTime/TimeSpan (including their Nullable forms).</summary>
        public static bool IsDateType(Type? type)
        {
            if (type == null) return false;
            type = Nullable.GetUnderlyingType(type) ?? type;
            return type == typeof(DateTime) || type == typeof(TimeSpan);
        }

        /// <summary>
        /// Accepts only a plain numeric literal: optional sign, ASCII digits, one decimal
        /// separator ("," is treated as "."), optional exponent. Anything else is rejected
        /// so it can never be emitted unquoted.
        /// </summary>
        public static bool TryNormalizeNumber(string? raw, out string normalized)
        {
            normalized = string.Empty;
            if (raw == null) return false;

            string s = raw.Trim().Replace(',', '.');
            int i = 0, n = s.Length;

            if (i < n && (s[i] == '+' || s[i] == '-')) i++;

            int digits = 0;
            while (i < n && char.IsAsciiDigit(s[i])) { i++; digits++; }
            if (i < n && s[i] == '.')
            {
                i++;
                while (i < n && char.IsAsciiDigit(s[i])) { i++; digits++; }
            }
            if (digits == 0) return false;

            if (i < n && (s[i] == 'e' || s[i] == 'E'))
            {
                i++;
                if (i < n && (s[i] == '+' || s[i] == '-')) i++;
                int expDigits = 0;
                while (i < n && char.IsAsciiDigit(s[i])) { i++; expDigits++; }
                if (expDigits == 0) return false;
            }

            if (i != n) return false;
            normalized = s;
            return true;
        }

        /// <summary>A valid number is emitted bare; anything else falls back to a quoted string literal.</summary>
        public static string FormatNumber(string raw) => TryNormalizeNumber(raw, out var number) ? number : QuoteString(raw);

        /// <summary>
        /// Builds "col IN (...)" for the values checked in the filter popup. An empty value
        /// matches NULL as well. No values yields "1=0".
        /// </summary>
        public static string BuildInCondition(string columnName, IReadOnlyCollection<string> selectedValues, bool isNumeric)
        {
            if (selectedValues.Count == 0) return "1=0";

            string col = QuoteIdentifier(columnName);
            var sb = new StringBuilder(col.Length + 8 + selectedValues.Count * 8);
            sb.Append(col).Append(" IN (");

            bool first = true;
            bool hasEmpty = false;
            foreach (string? raw in selectedValues)
            {
                string v = raw ?? string.Empty;
                if (v.Length == 0) hasEmpty = true;

                if (!first) sb.Append(',');
                first = false;

                if (isNumeric) sb.Append(v.Length == 0 ? "NULL" : FormatNumber(v));
                else sb.Append(QuoteString(v));
            }
            sb.Append(')');

            string condition = sb.ToString();
            return hasEmpty ? $"({condition} OR {col} IS NULL)" : condition;
        }

        /// <summary>
        /// Builds a single comparison for the custom filter dialog. <paramref name="op"/> is one of
        /// the internal operator keys used by the dialog. Returns empty when there is no value.
        /// </summary>
        public static string BuildComparison(string columnName, string op, string? value, Type? dataType)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            string val = value.Trim();
            string col = QuoteIdentifier(columnName);
            string body = EscapeStringBody(val);

            // Sayısal ise doğrulanmış çıplak sayı (virgül → nokta), değilse tırnaklı literal
            string v = IsNumericType(dataType) ? FormatNumber(val) : QuoteString(val);

            return op switch
            {
                "eşittir" => $"{col} = {v}",
                "eşit değildir" => $"{col} <> {v}",
                "başlar" => $"{col} LIKE '{body}%'",
                "biter" => $"{col} LIKE '%{body}'",
                "içerir" => $"{col} LIKE '%{body}%'",
                "içermez" => $"{col} NOT LIKE '%{body}%'",
                "büyük" => $"{col} > {v}",
                "büyük veya eşittir" => $"{col} >= {v}",
                "küçük" => $"{col} < {v}",
                "küçük veya eşittir" => $"{col} <= {v}",
                "önce" => $"{col} < {v}",
                "sonra" => $"{col} > {v}",
                "önce veya eşit" => $"{col} <= {v}",
                "sonra veya eşit" => $"{col} >= {v}",
                _ => $"{col} = {v}"
            };
        }

        /// <summary>Joins two optional comparisons with AND/OR.</summary>
        public static string CombineConditions(string first, string second, bool useAnd)
        {
            if (!string.IsNullOrEmpty(first) && !string.IsNullOrEmpty(second))
                return $"({first} {(useAnd ? "AND" : "OR")} {second})";
            return !string.IsNullOrEmpty(first) ? first : second;
        }
    }
}
