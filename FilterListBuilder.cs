using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace VirtualADGV.WPF
{
    /// <summary>
    /// Builds the filter popup items (flat list, or Year > Month > Day tree for dates) from the
    /// distinct values supplied by the host. Kept free of WPF types so it can be unit-tested.
    /// </summary>
    internal static class FilterListBuilder
    {
        /// <param name="distinctValues">Enumerated exactly once (hosts may pass a lazy query).</param>
        /// <param name="previouslySelected">Values checked by this column's active filter; empty = all checked.</param>
        /// <param name="enabledValues">Values still present under other columns' filters; null = all enabled.</param>
        /// <param name="columnType">Numeric types sort by value, TimeSpan by duration, DateTime builds the date tree.</param>
        /// <param name="emptyText">Display text for the blank value.</param>
        public static List<FilterItemModel> Build(IEnumerable<string> distinctValues, ISet<string> previouslySelected,
            ISet<string>? enabledValues, Type columnType, string emptyText)
        {
            bool allSelected = previouslySelected.Count == 0;

            // OrderBy tampona alır: kaynak her dalda tek sefer dolaşılır (eski kod iki kez dolaşıyordu)
            var values = distinctValues.Select(v => v ?? string.Empty);

            if (UsesDateTree(columnType))
                return BuildDateTree(values, previouslySelected, allSelected, enabledValues, emptyText);

            List<string> sortedValues;
            if (FilterExpressionBuilder.IsNumericType(columnType))
                sortedValues = values.OrderBy(NumericSortKey).ThenBy(v => v).ToList();
            else if (FilterExpressionBuilder.IsDateType(columnType)) // TimeSpan: süreye göre düz liste
                sortedValues = values.OrderBy(DurationSortKey).ThenBy(v => v).ToList();
            else
                sortedValues = values.OrderBy(v => v).ToList();

            var roots = new List<FilterItemModel>(sortedValues.Count);
            foreach (string val in sortedValues)
            {
                bool enabled = enabledValues == null || enabledValues.Contains(val);
                bool isSel = enabled && (allSelected || previouslySelected.Contains(val));
                var item = new FilterItemModel { Value = val, IsEnabled = enabled, DisplayTextOverride = val.Length == 0 ? emptyText : null };
                item.InitializeCheckState(isSel);
                roots.Add(item);
            }
            return roots;
        }

        /// <summary>
        /// True when the column is shown as a Year > Month > Day tree. Only DateTime: a TimeSpan is a
        /// duration, and DateTime.TryParse would read "01:30:00" as a time on today's date.
        /// </summary>
        public static bool UsesDateTree(Type? columnType)
        {
            if (columnType == null) return false;
            return (Nullable.GetUnderlyingType(columnType) ?? columnType) == typeof(DateTime);
        }

        private static List<FilterItemModel> BuildDateTree(IEnumerable<string> values, ISet<string> previouslySelected,
            bool allSelected, ISet<string>? enabledValues, string emptyText)
        {
            // Her değer bir kez ayrıştırılır ve tarihe göre sıralanır: metin sırası yalnızca ISO
            // biçiminde kronolojiktir ("5.01.2024" < "10.01.2023" gibi hatalara yol açıyordu).
            // Boş değer en üstte, ayrıştırılamayanlar tarihlerden sonra (eski metin sırasıyla aynı).
            var entries = values
                .Select(v =>
                {
                    DateTime parsed = default;
                    bool isDate = v.Length > 0 && TryParseDate(v, out parsed);
                    return (Value: v, IsDate: isDate, Date: parsed);
                })
                .OrderBy(e => e.Value.Length == 0 ? 0 : e.IsDate ? 1 : 2)
                .ThenBy(e => e.Date)
                .ThenBy(e => e.Value)
                .ToList();

            var roots = new List<FilterItemModel>();

            // Hiyerarşik yapı: durumlar sona doğru tek geçişte (gün → ay → yıl) hesaplanır
            var years = new Dictionary<string, FilterItemModel>();
            var months = new Dictionary<(string Year, string Month), FilterItemModel>();

            foreach (var (val, isDate, dt) in entries)
            {
                bool enabled = enabledValues == null || enabledValues.Contains(val);
                bool isSel = enabled && (allSelected || previouslySelected.Contains(val));

                if (val.Length == 0)
                {
                    var blank = new FilterItemModel { Value = string.Empty, IsEnabled = enabled, DisplayTextOverride = emptyText };
                    blank.InitializeCheckState(isSel);
                    roots.Add(blank);
                    continue;
                }

                if (isDate)
                {
                    string yStr = dt.Year.ToString();
                    string mStr = dt.ToString("MMMM"); // October vb. veya 10
                    string dStr = dt.Day.ToString("00");

                    if (!years.TryGetValue(yStr, out var yNode))
                    {
                        yNode = new FilterItemModel { Value = yStr };
                        roots.Add(yNode);
                        years[yStr] = yNode;
                    }

                    if (!months.TryGetValue((yStr, mStr), out var mNode))
                    {
                        mNode = new FilterItemModel { Value = mStr, Parent = yNode };
                        yNode.Children.Add(mNode);
                        months[(yStr, mStr)] = mNode;
                    }

                    var dNode = new FilterItemModel { Value = val, Parent = mNode, IsEnabled = enabled, DisplayTextOverride = dStr };
                    dNode.InitializeCheckState(isSel);
                    mNode.Children.Add(dNode);
                }
                else
                {
                    var item = new FilterItemModel { Value = val, IsEnabled = enabled };
                    item.InitializeCheckState(isSel);
                    roots.Add(item);
                }
            }

            foreach (var mNode in months.Values) mNode.InitializeCheckStateFromChildren();
            foreach (var yNode in years.Values) yNode.InitializeCheckStateFromChildren();

            return roots;
        }

        /// <summary>
        /// (sıra, yaklaşık, kesin): double tüm aralığı (1E+30, ±∞) kapsar, decimal eşitlikte long/decimal
        /// hassasiyetini korur. Sayı olmayanlar (NaN dahil) sayılardan sonra, boş değer her zaman en sonda.
        /// Eski decimal.MaxValue işareti decimal.MaxValue değeriyle çakışıyordu.
        /// </summary>
        private static (int Rank, double Approx, decimal Exact) NumericSortKey(string v)
        {
            if (v.Length == 0) return (2, 0, 0);

            string s = v.Replace(",", ".");
            if (!double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double d) &&
                !double.TryParse(v, NumberStyles.Float, CultureInfo.CurrentCulture, out d)) // ör. tr-TR "∞"
                return (1, 0, 0);
            if (double.IsNaN(d)) return (1, 0, 0);

            decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal exact); // aralık dışı → 0, önemsiz
            return (0, d, exact);
        }

        private static (int Rank, TimeSpan Value) DurationSortKey(string v)
        {
            if (v.Length == 0) return (2, TimeSpan.Zero);
            return TimeSpan.TryParse(v, CultureInfo.InvariantCulture, out var ts) || TimeSpan.TryParse(v, CultureInfo.CurrentCulture, out ts)
                ? (0, ts)
                : (1, TimeSpan.Zero);
        }

        private static bool TryParseDate(string val, out DateTime dt)
        {
            return DateTime.TryParse(val, out dt) ||
                   (val.Length >= 10 && DateTime.TryParseExact(val.AsSpan(0, 10), "yyyy-MM-dd", null, DateTimeStyles.None, out dt));
        }
    }
}
