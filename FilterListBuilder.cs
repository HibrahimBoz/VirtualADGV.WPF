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
        /// <param name="columnType">Drives numeric sort and the date tree.</param>
        /// <param name="emptyText">Display text for the blank value.</param>
        public static List<FilterItemModel> Build(IEnumerable<string> distinctValues, ISet<string> previouslySelected,
            ISet<string>? enabledValues, Type columnType, string emptyText)
        {
            bool allSelected = previouslySelected.Count == 0;
            bool isNumeric = FilterExpressionBuilder.IsNumericType(columnType);
            bool isDate = FilterExpressionBuilder.IsDateType(columnType);

            // OrderBy tampona alır: kaynak tek sefer dolaşılır (eski kod iki kez dolaşıyordu)
            List<string> sortedValues = isNumeric
                ? distinctValues.Select(v => v ?? string.Empty).OrderBy(NumericSortKey).ToList()
                : distinctValues.Select(v => v ?? string.Empty).OrderBy(v => v).ToList();

            var roots = new List<FilterItemModel>(isDate ? 16 : sortedValues.Count);

            if (!isDate)
            {
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

            // Hiyerarşik yapı: durumlar sona doğru tek geçişte (gün → ay → yıl) hesaplanır
            var years = new Dictionary<string, FilterItemModel>();
            var months = new Dictionary<(string Year, string Month), FilterItemModel>();

            foreach (string val in sortedValues)
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

                if (TryParseDate(val, out DateTime dt))
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

        private static decimal NumericSortKey(string v)
        {
            if (string.IsNullOrEmpty(v)) return decimal.MaxValue;
            return decimal.TryParse(v.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal d) ? d : decimal.MaxValue;
        }

        private static bool TryParseDate(string val, out DateTime dt)
        {
            return DateTime.TryParse(val, out dt) ||
                   (val.Length >= 10 && DateTime.TryParseExact(val.AsSpan(0, 10), "yyyy-MM-dd", null, DateTimeStyles.None, out dt));
        }
    }
}
