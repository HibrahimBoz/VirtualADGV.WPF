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

            bool useAnd = RbAnd.IsChecked == true;

            if (string.IsNullOrEmpty(val1) && string.IsNullOrEmpty(val2))
            {
                FilterCondition = string.Empty;
                DialogResult = true;
                return;
            }

            // Sütun adı ve değerler FilterExpressionBuilder'da kaçışlanır; sayısal sütunda
            // geçersiz giriş (örn. "1 OR 1=1") çıplak değil tırnaklı literal olarak yazılır
            string f1 = FilterExpressionBuilder.BuildComparison(_columnName, op1, val1, _dataType);
            string f2 = FilterExpressionBuilder.BuildComparison(_columnName, op2, val2, _dataType);

            FilterCondition = FilterExpressionBuilder.CombineConditions(f1, f2, useAnd);
            DialogResult = true;
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

            this.Resources["WinBg"] = ThemeBrush.Create(winBg);
            this.Resources["WinFg"] = ThemeBrush.Create(winFg);
            this.Resources["TitleBg"] = ThemeBrush.Create(titleBg);
            this.Resources["TitleFg"] = ThemeBrush.Create(titleFg);
            this.Resources["ControlBg"] = ThemeBrush.Create(controlBg);
            this.Resources["ControlFg"] = ThemeBrush.Create(controlFg);
            this.Resources["BorderBrush"] = ThemeBrush.Create(borderColor);
            this.Resources["TextMuted"] = ThemeBrush.Create(textMuted);
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
