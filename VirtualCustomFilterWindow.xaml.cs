using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace VirtualADGV.WPF
{
    /// <summary>
    /// A dialog window for creating complex custom filters (e.g., "Greater than X AND Less than Y").
    /// </summary>
    public partial class VirtualCustomFilterWindow : Window
    {
        /// <summary>The generated SQL-like filter condition string.</summary>
        public string FilterCondition { get; private set; } = string.Empty;
        
        /// <summary>Localization strings for the UI.</summary>
        public VirtualDataGridStrings Strings { get; private set; } = new();

        private readonly string _columnName;
        private readonly Type _dataType;
        private readonly Dictionary<string, string> _opMap = new();

        /// <summary>
        /// Initializes a new instance of the custom filter window.
        /// </summary>
        /// <param name="columnName">Name of the column to filter.</param>
        /// <param name="dataType">Data type of the column.</param>
        /// <param name="initialOp1">Optional initial operator for the first row.</param>
        /// <param name="initialOp2">Optional initial operator for the second row.</param>
        /// <param name="useAnd">Whether to default to AND logic.</param>
        /// <param name="strings">Optional custom localization strings.</param>
        public VirtualCustomFilterWindow(string columnName, Type dataType, string? initialOp1 = null, string? initialOp2 = null, bool useAnd = true, VirtualDataGridStrings? strings = null)
        {
            InitializeComponent();
            if (strings != null) Strings = strings;
            _columnName = columnName;
            _dataType = dataType;
            
            this.Title = Strings.CustomFilterTitle;
            LoadOperators();

            if (!string.IsNullOrEmpty(initialOp1)) SelectOperator(CmbOperator1, initialOp1);
            if (!string.IsNullOrEmpty(initialOp2)) SelectOperator(CmbOperator2, initialOp2);
            
            if (useAnd) RbAnd.IsChecked = true; else RbOr.IsChecked = true;
        }

        private void LoadOperators()
        {
            _opMap.Clear();
            var operators = new List<string>();

            if (_dataType == typeof(string))
            {
                AddOp(Strings.OpEquals, "eşittir", operators);
                AddOp(Strings.OpNotEquals, "eşit değildir", operators);
                AddOp(Strings.OpBeginsWith, "başlar", operators);
                AddOp(Strings.OpEndsWith, "biter", operators);
                AddOp(Strings.OpContains, "içerir", operators);
                AddOp(Strings.OpNotContains, "içermez", operators);
            }
            else if (_dataType == typeof(DateTime) || _dataType == typeof(TimeSpan))
            {
                AddOp(Strings.OpEquals, "eşittir", operators);
                AddOp(Strings.OpNotEquals, "eşit değildir", operators);
                AddOp(Strings.OpBefore, "önce", operators);
                AddOp(Strings.OpAfter, "sonra", operators);
                AddOp(Strings.OpBeforeOrEqual, "önce veya eşit", operators);
                AddOp(Strings.OpAfterOrEqual, "sonra veya eşit", operators);
            }
            else // Number
            {
                AddOp(Strings.OpEquals, "eşittir", operators);
                AddOp(Strings.OpNotEquals, "eşit değildir", operators);
                AddOp(Strings.OpGreaterThan, "büyük", operators);
                AddOp(Strings.OpGreaterThanOrEqual, "büyük veya eşittir", operators);
                AddOp(Strings.OpLessThan, "küçük", operators);
                AddOp(Strings.OpLessThanOrEqual, "küçük veya eşittir", operators);
            }

            CmbOperator1.ItemsSource = operators;
            CmbOperator2.ItemsSource = operators;
            CmbOperator1.SelectedIndex = 0;
            CmbOperator2.SelectedIndex = 0;
        }

        private void AddOp(string label, string key, List<string> list)
        {
            list.Add(label);
            _opMap[label] = key;
        }

        private void SelectOperator(System.Windows.Controls.ComboBox combo, string internalKey)
        {
            foreach (var kvp in _opMap)
            {
                if (kvp.Value == internalKey)
                {
                    combo.SelectedItem = kvp.Key;
                    break;
                }
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            string opLabel1 = CmbOperator1.SelectedItem?.ToString() ?? Strings.OpEquals;
            string op1 = _opMap.GetValueOrDefault(opLabel1, "eşittir");
            string val1 = TxtValue1.Text.Trim();

            string opLabel2 = CmbOperator2.SelectedItem?.ToString() ?? Strings.OpEquals;
            string op2 = _opMap.GetValueOrDefault(opLabel2, "eşittir");
            string val2 = TxtValue2.Text.Trim();

            string logic = RbAnd.IsChecked == true ? "AND" : "OR";

            if (string.IsNullOrEmpty(val1) && string.IsNullOrEmpty(val2))
            {
                FilterCondition = string.Empty;
                DialogResult = true;
                return;
            }

            string f1 = BuildFilterPart(op1, val1);
            string f2 = BuildFilterPart(op2, val2);

            if (!string.IsNullOrEmpty(f1) && !string.IsNullOrEmpty(f2))
            {
                FilterCondition = $"({f1} {logic} {f2})";
            }
            else if (!string.IsNullOrEmpty(f1))
            {
                FilterCondition = f1;
            }
            else
            {
                FilterCondition = f2;
            }

            DialogResult = true;
        }

        private string BuildFilterPart(string op, string val)
        {
            if (string.IsNullOrEmpty(val)) return string.Empty;

            // Sayısal değerler için virgülü noktaya çevir
            string cleanVal = val.Trim().Replace(",", ".");
            string safeVal = cleanVal.Replace("'", "''");
            string col = $"\"{_columnName}\"";

            bool isNumeric = _dataType == typeof(int) || _dataType == typeof(long) || 
                           _dataType == typeof(double) || _dataType == typeof(float) || 
                           _dataType == typeof(decimal);

            // Sayısal ise tırnaksız, değilse tırnaklı (LIKE hariç)
            string v = isNumeric ? safeVal : $"'{safeVal}'";

            return op switch
            {
                "eşittir" => $"{col} = {v}",
                "eşit değildir" => $"{col} <> {v}",
                "başlar" => $"{col} LIKE '{safeVal}%'",
                "biter" => $"{col} LIKE '%{safeVal}'",
                "içerir" => $"{col} LIKE '%{safeVal}%'",
                "içermez" => $"{col} NOT LIKE '%{safeVal}%'",
                "büyük" => $"{col} > {v}",
                "büyük veya eşittir" => $"{col} >= {v}",
                "küçük" => $"{col} < {v}",
                "küçük veya eşittir" => $"{col} <= {v}",
                "önce" => $"{col} < {v}", // Tarihler tırnak gerektirir (v zaten tırnaklı olacak)
                "sonra" => $"{col} > {v}",
                "önce veya eşit" => $"{col} <= {v}",
                "sonra veya eşit" => $"{col} >= {v}",
                _ => $"{col} = {v}"
            };
        }

        /// <summary>
        /// Sets the Dark/Light theme colors for the window.
        /// </summary>
        /// <param name="isDarkMode">True for dark mode, false for light mode.</param>
        public void SetTheme(bool isDarkMode)
        {
            var winBg = isDarkMode ? Color.FromRgb(9, 9, 11) : Colors.White;
            var winFg = isDarkMode ? Color.FromRgb(248, 250, 252) : Color.FromRgb(15, 23, 42);
            var titleBg = isDarkMode ? Color.FromRgb(24, 24, 27) : Color.FromRgb(248, 250, 252);
            var titleFg = isDarkMode ? Color.FromRgb(161, 161, 170) : Color.FromRgb(71, 85, 105);
            var controlBg = isDarkMode ? Color.FromRgb(24, 24, 27) : Color.FromRgb(241, 245, 249);
            var controlFg = isDarkMode ? Colors.White : Color.FromRgb(15, 23, 42);
            var borderColor = isDarkMode ? Color.FromRgb(39, 39, 42) : Color.FromRgb(226, 232, 240);
            var textMuted = isDarkMode ? Color.FromRgb(161, 161, 170) : Color.FromRgb(100, 116, 139);

            this.Resources["WinBg"] = new SolidColorBrush(winBg);
            this.Resources["WinFg"] = new SolidColorBrush(winFg);
            this.Resources["TitleBg"] = new SolidColorBrush(titleBg);
            this.Resources["TitleFg"] = new SolidColorBrush(titleFg);
            this.Resources["ControlBg"] = new SolidColorBrush(controlBg);
            this.Resources["ControlFg"] = new SolidColorBrush(controlFg);
            this.Resources["BorderBrush"] = new SolidColorBrush(borderColor);
            this.Resources["TextMuted"] = new SolidColorBrush(textMuted);
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
