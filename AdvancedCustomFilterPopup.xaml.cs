using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Threading;
using System.Threading.Tasks;

namespace VirtualADGV.WPF
{
    /// <summary>
    /// Represents an item in the filter list (checkbox list or tree view).
    /// Supports hierarchical selection (Select All, Year > Month > Day).
    /// </summary>
    public class FilterItemModel : INotifyPropertyChanged
    {
        private bool? _isChecked = true;
        /// <summary>Gets or sets the checked state. Supports indeterminate state.</summary>
        public bool? IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    if (!SuppressNotification) 
                    {
                        OnPropertyChanged(nameof(IsChecked));
                        UpdateChildrenCheckState(value);
                        UpdateParentCheckState();
                    }
                }
            }
        }

        /// <summary>Backward compatibility for flat list selection.</summary>
        public bool IsSelected
        {
            get => _isChecked == true;
            set => IsChecked = value;
        }

        /// <summary>Utility to prevent recursive property changed events during bulk updates.</summary>
        public static bool SuppressNotification { get; set; } = false;

        /// <summary>The actual data value (string representation) used for filtering.</summary>
        public string Value { get; set; } = string.Empty;
        
        /// <summary>Optional display text if different from Value (e.g., "(Blank)").</summary>
        public string? DisplayTextOverride { get; set; }
        
        /// <summary>The text shown in the UI.</summary>
        public string DisplayText => DisplayTextOverride ?? (string.IsNullOrEmpty(Value) ? "(Blank)" : Value);

        private bool _isMatched = true;
        /// <summary>Whether this item matches the search text in the filter popup.</summary>
        public bool IsMatched
        {
            get => _isMatched;
            set
            {
                if (_isMatched != value)
                {
                    _isMatched = value;
                    if (!SuppressNotification) OnPropertyChanged(nameof(IsMatched));
                }
            }
        }

        private bool _isExpanded = false;
        /// <summary>Whether the tree node is expanded.</summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    if (!SuppressNotification) OnPropertyChanged(nameof(IsExpanded));
                }
            }
        }

        /// <summary>Parent node in hierarchical view.</summary>
        public FilterItemModel? Parent { get; set; }
        
        /// <summary>Child nodes in hierarchical view.</summary>
        public ObservableCollection<FilterItemModel> Children { get; } = new ObservableCollection<FilterItemModel>();

        private void UpdateChildrenCheckState(bool? state)
        {
            if (state == null || Children.Count == 0) return;
            SuppressNotification = true;
            foreach (var child in Children)
            {
                child.IsChecked = state;
                child.UpdateChildrenCheckState(state);
            }
            SuppressNotification = false;
        }

        /// <summary>Updates the parent check state based on children (None/Partial/All checked).</summary>
        public void UpdateParentCheckState()
        {
            if (Parent == null) return;
            SuppressNotification = true;
            bool hasChecked = Parent.Children.Any(c => c.IsChecked == true);
            bool hasUnchecked = Parent.Children.Any(c => c.IsChecked == false);
            bool hasIndeterminate = Parent.Children.Any(c => c.IsChecked == null);

            if (hasChecked && !hasUnchecked && !hasIndeterminate) Parent.IsChecked = true;
            else if (!hasChecked && hasUnchecked && !hasIndeterminate) Parent.IsChecked = false;
            else Parent.IsChecked = null;

            SuppressNotification = false;
            Parent.OnPropertyChanged(nameof(IsChecked));
            Parent.UpdateParentCheckState();
        }

        /// <summary>Occurs when a property value changes.</summary>
        public event PropertyChangedEventHandler? PropertyChanged;
        /// <summary>Raises the PropertyChanged event.</summary>
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// Interaction logic for AdvancedCustomFilterPopup.xaml
    /// </summary>
    public partial class AdvancedCustomFilterPopup : System.Windows.Controls.UserControl
    {
        /// <summary>DependencyProperty for IsLoading.</summary>
        public static readonly DependencyProperty IsLoadingProperty =
            DependencyProperty.Register(nameof(IsLoading), typeof(bool), typeof(AdvancedCustomFilterPopup), new PropertyMetadata(false));

        /// <summary>Gets or sets whether the loading overlay is visible.</summary>
        public bool IsLoading
        {
            get => (bool)GetValue(IsLoadingProperty);
            set => SetValue(IsLoadingProperty, value);
        }

        /// <summary>List of items available for selection in the filter list.</summary>
        public ObservableCollection<FilterItemModel> FilterItems { get; private set; } = new ObservableCollection<FilterItemModel>();
        private ICollectionView _filterItemsView;

        /// <summary>Localization strings for current language.</summary>
        public VirtualDataGridStrings Strings { get; private set; } = new();
        /// <summary>Name of the column being filtered.</summary>
        public string ColumnName { get; private set; } = string.Empty;

        /// <summary>Callback when filter is applied. Parameters: column, condition (SQL).</summary>
        public Action<string, string?>? OnFilterApplied { get; set; }
        /// <summary>Callback when sort is applied. Parameters: column, direction (ASC/DESC).</summary>
        public Action<string, string?>? OnSortApplied { get; set; }
        /// <summary>Callback when popup should close.</summary>
        public Action? OnPopupClosed { get; set; }

        private bool _isBuildingList = false;
        private List<string> _previousSelectedValues = new List<string>();
        private System.Windows.Threading.DispatcherTimer _searchTimer;

        /// <summary>Initializes a new instance of the popup.</summary>
        public AdvancedCustomFilterPopup()
        {
            InitializeComponent();
            _filterItemsView = CollectionViewSource.GetDefaultView(FilterItems);
            _filterItemsView.Filter = SearchFilter;
            LstItems.ItemsSource = _filterItemsView;
            TreeViewItems.ItemsSource = _filterItemsView;

            _searchTimer = new System.Windows.Threading.DispatcherTimer();
            _searchTimer.Interval = TimeSpan.FromMilliseconds(300);
            _searchTimer.Tick += (s, e) =>
            {
                _searchTimer.Stop();
                _filterItemsView.Refresh();
                UpdateSelectAllCheckBox();
            };

            this.Unloaded += (s, e) => _searchTimer?.Stop();
        }

        private Type _columnType = typeof(string);

        /// <summary>
        /// Initializes the popup with column data and current state.
        /// </summary>
        public void Initialize(string columnName, IEnumerable<string> distinctValues, IEnumerable<string> activeFilters, Type columnType, VirtualDataGridStrings strings)
        {
            Strings = strings;
            ColumnName = columnName;
            _columnType = columnType;
            _previousSelectedValues = activeFilters?.ToList() ?? new List<string>();

            BtnClearFilter.IsEnabled = _previousSelectedValues.Any();
            TxtSearch.Text = "";

            bool isNumeric = columnType == typeof(int) || columnType == typeof(long) ||
                            columnType == typeof(double) || columnType == typeof(float) ||
                            columnType == typeof(decimal);
            bool isDate = columnType == typeof(DateTime) || columnType == typeof(TimeSpan);

            txtMode.Text = (isNumeric || isDate) ? (isNumeric ? Strings.NumberFilters : Strings.DateFilters) : Strings.TextFilters;

            if (isNumeric)
            {
                TxtSortAsc.Text = Strings.SortSmallToLarge;
                TxtSortDesc.Text = Strings.SortLargeToSmall;
            }
            else if (isDate)
            {
                TxtSortAsc.Text = Strings.SortOldToNew;
                TxtSortDesc.Text = Strings.SortNewToOld;
            }
            else
            {
                TxtSortAsc.Text = Strings.SortAToZ;
                TxtSortDesc.Text = Strings.SortZToA;
            }

            LoadDistinctValues(distinctValues);
            BuildCustomFilterMenu(isNumeric);
        }

        private void BuildCustomFilterMenu(bool isNumeric)
        {
            SubMenuFilters.Items.Clear();

            if (isNumeric)
            {
                AddFilterMenuItem(Strings.Equals, "eşittir");
                AddFilterMenuItem(Strings.NotEquals, "eşit değildir");
                SubMenuFilters.Items.Add(new Separator());
                AddFilterMenuItem(Strings.GreaterThan, "büyük");
                AddFilterMenuItem(Strings.GreaterThanOrEqual, "büyük veya eşittir");
                AddFilterMenuItem(Strings.LessThan, "küçük");
                AddFilterMenuItem(Strings.LessThanOrEqual, "küçük veya eşittir");
                SubMenuFilters.Items.Add(new Separator());

                var betweenItem = new MenuItem { Header = Strings.Between };
                betweenItem.Click += (s, e) => OpenCustomFilter("büyük veya eşittir", "küçük veya eşittir", true);
                SubMenuFilters.Items.Add(betweenItem);
            }
            else
            {
                AddFilterMenuItem(Strings.Equals, "eşittir");
                AddFilterMenuItem(Strings.NotEquals, "eşit değildir");
                SubMenuFilters.Items.Add(new Separator());
                AddFilterMenuItem(Strings.BeginsWith, "başlar");
                AddFilterMenuItem(Strings.EndsWith, "biter");
                AddFilterMenuItem(Strings.Contains, "içerir");
                AddFilterMenuItem(Strings.NotContains, "içermez");
            }

            SubMenuFilters.Items.Add(new Separator());
            var customItem = new MenuItem { Header = Strings.CustomFilter };
            customItem.Click += (s, e) => OpenCustomFilter(null);
            SubMenuFilters.Items.Add(customItem);
        }

        private void AddFilterMenuItem(string header, string op)
        {
            var item = new MenuItem { Header = header };
            item.Click += (s, e) => OpenCustomFilter(op);
            SubMenuFilters.Items.Add(item);
        }

        private bool _isDarkMode = false;
        private void OpenCustomFilter(string? initialOp1, string? initialOp2 = null, bool useAnd = true)
        {
            var customWin = new VirtualCustomFilterWindow(ColumnName, _columnType, initialOp1, initialOp2, useAnd, Strings);
            customWin.Owner = Window.GetWindow(this);
            customWin.SetTheme(_isDarkMode); // Tema bilgisini ilet
            if (customWin.ShowDialog() == true)
            {
                if (!string.IsNullOrEmpty(customWin.FilterCondition))
                {
                    OnFilterApplied?.Invoke(ColumnName, customWin.FilterCondition);
                    OnPopupClosed?.Invoke();
                }
            }
        }

        private void BtnCustomFilter_Click(object sender, RoutedEventArgs e)
        {
            SubMenuFilters.PlacementTarget = BtnCustomFilter;
            SubMenuFilters.Placement = System.Windows.Controls.Primitives.PlacementMode.Right;
            SubMenuFilters.IsOpen = true;
        }

        private void BtnClearSort_Click(object sender, RoutedEventArgs e)
        {
            OnSortApplied?.Invoke(ColumnName, null);
            OnPopupClosed?.Invoke();
        }

        private void LoadDistinctValues(IEnumerable<string> distinctValues)
        {
            _isBuildingList = true;
            try
            {
                FilterItems.Clear();
                bool allSelected = !_previousSelectedValues.Any();

                var sortedValues = distinctValues.ToList();
                bool isNumeric = _columnType == typeof(int) || _columnType == typeof(long) ||
                                _columnType == typeof(double) || _columnType == typeof(float) ||
                                _columnType == typeof(decimal);
                bool isDate = _columnType == typeof(DateTime) || _columnType == typeof(TimeSpan);

                // UI Seçimi: Tarihler için TreeView, Diğerleri için ListBox
                LstItems.Visibility = isDate ? Visibility.Collapsed : Visibility.Visible;
                TreeViewItems.Visibility = isDate ? Visibility.Visible : Visibility.Collapsed;

                if (isNumeric)
                {
                    sortedValues = distinctValues.OrderBy(v =>
                    {
                        if (string.IsNullOrEmpty(v)) return decimal.MaxValue;
                        return decimal.TryParse(v.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal d) ? d : decimal.MaxValue;
                    }).ToList();
                }
                else
                {
                    sortedValues = distinctValues.OrderBy(v => v).ToList();
                }

                if (isDate)
                {
                    // Hiyerarşik Yapı Oluştur
                    var years = new Dictionary<string, FilterItemModel>();

                    foreach (string val in sortedValues)
                    {
                        bool isSel = allSelected || _previousSelectedValues.Contains(val);

                        if (string.IsNullOrEmpty(val))
                        {
                            FilterItems.Add(new FilterItemModel { Value = "", IsSelected = isSel, DisplayTextOverride = Strings.EmptyValue });
                            continue;
                        }

                        DateTime dt;
                        if (DateTime.TryParse(val, out dt) || (val.Length >= 10 && DateTime.TryParseExact(val.Substring(0,10), "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out dt)))
                        {
                            string yStr = dt.Year.ToString();
                            string mStr = dt.ToString("MMMM"); // October vb. veya 10
                            string dStr = dt.Day.ToString("00");

                            if (!years.ContainsKey(yStr))
                            {
                                var yNode = new FilterItemModel { Value = yStr, IsExpanded = false };
                                FilterItems.Add(yNode);
                                years[yStr] = yNode;
                            }

                            var yParent = years[yStr];
                            var mParent = yParent.Children.FirstOrDefault(c => c.Value == mStr);
                            if (mParent == null)
                            {
                                mParent = new FilterItemModel { Value = mStr, Parent = yParent, IsExpanded = false };
                                yParent.Children.Add(mParent);
                            }

                            var dNode = new FilterItemModel { Value = val, Parent = mParent, IsExpanded = false }; // Sadece leaf node'ların Value'su asıl filtre değeridir
                            dNode.Value = val; // Actually store the real exact string as Value to be used in SQL
                            // Modify display text for leaf to just be day
                            dNode.GetType().GetProperty("DisplayText")?.SetValue(dNode, dStr); 
                            // Wait, DisplayText is read-only. We'll add custom Title via an override later, but for now we'll just keep the default or use a Wrapper.
                            // Actually, let's just use Value for real SQL value, and add a separate Title property to FilterItemModel
                            
                            mParent.Children.Add(dNode);
                            dNode.IsChecked = isSel ? true : false;
                        }
                        else
                        {
                            FilterItems.Add(new FilterItemModel { Value = val, IsChecked = isSel ? true : false });
                        }
                    }

                    // Re-calculate states based on leaf nodes
                    foreach (var root in FilterItems) root.UpdateParentCheckState();
                }
                else
                {
                    // Düz Liste Oluştur
                    foreach (string val in sortedValues)
                    {
                        bool isSel = allSelected || _previousSelectedValues.Contains(val);
                        FilterItems.Add(new FilterItemModel { Value = val, IsChecked = isSel ? true : false, DisplayTextOverride = string.IsNullOrEmpty(val) ? Strings.EmptyValue : null });
                    }
                }

                UpdateSelectAllCheckBox();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"{Strings.LoadingError}: {ex.Message}");
            }
            finally
            {
                _isBuildingList = false;
            }
        }

        private bool SearchFilter(object item)
        {
            if (item is FilterItemModel filterItem)
            {
                if (string.IsNullOrWhiteSpace(TxtSearch.Text))
                {
                    filterItem.IsMatched = true;
                    return true;
                }

                bool match = filterItem.DisplayText.Contains(TxtSearch.Text, StringComparison.OrdinalIgnoreCase);
                filterItem.IsMatched = match;
                return match;
            }
            return false;
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchTimer.Stop();
            _searchTimer.Start();
        }

        private void ChkSelectAll_Click(object sender, RoutedEventArgs e)
        {
            if (_isBuildingList || FilterItems.Count == 0) return;

            bool isChecked = ChkSelectAll.IsChecked == true;
            _isBuildingList = true;

            try
            {
                // Görünür elemanları hızlıca belirlemek için ICollectionView üzerinde dön
                // .Cast<T>().ToList() yerine direkt döngü + kontrol kullanabiliriz
                FilterItemModel.SuppressNotification = true;

                foreach (object item in _filterItemsView)
                {
                    if (item is FilterItemModel filterItem)
                    {
                        filterItem.IsSelected = isChecked;
                    }
                }

                FilterItemModel.SuppressNotification = false;
                _filterItemsView.Refresh();
            }
            finally
            {
                _isBuildingList = false;
                UpdateSelectAllCheckBox();
            }
        }

        private void ChkItem_Changed(object sender, RoutedEventArgs e)
        {
            if (_isBuildingList) return;
            UpdateSelectAllCheckBox();
        }

        private void UpdateSelectAllCheckBox()
        {
            if (FilterItems.Count == 0)
            {
                ChkSelectAll.IsChecked = false;
                return;
            }

            var visibleItems = _filterItemsView.Cast<FilterItemModel>();

            // Daha hızlı kontrol için döngüden çıkış
            bool allChecked = true;
            bool anyChecked = false;
            bool totalVisible = false;

            foreach (var item in visibleItems)
            {
                totalVisible = true;
                if (item.IsSelected) anyChecked = true;
                else allChecked = false;

                if (anyChecked && !allChecked) break; // İkisi de belli olduysa çık
            }

            if (!totalVisible)
            {
                ChkSelectAll.IsChecked = false;
                return;
            }

            _isBuildingList = true;
            if (allChecked)
                ChkSelectAll.IsChecked = true;
            else if (anyChecked)
                ChkSelectAll.IsChecked = null; // Indeterminate
            else
                ChkSelectAll.IsChecked = false;
            _isBuildingList = false;
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            bool allSelected = FilterItems.All(i => i.IsSelected);

            if (allSelected)
            {
                OnFilterApplied?.Invoke(ColumnName, null);
            }
            else
            {
                var selectedValues = new List<string>();
                FilterItemModel.SuppressNotification = true;
                
                // Get all checked leaf nodes
                void GetCheckedLeaves(IEnumerable<FilterItemModel> nodes)
                {
                    foreach (var n in nodes)
                    {
                        if (n.Children.Count == 0 && n.IsChecked == true)
                            selectedValues.Add(n.Value);
                        else
                            GetCheckedLeaves(n.Children);
                    }
                }
                
                GetCheckedLeaves(FilterItems);
                FilterItemModel.SuppressNotification = false;

                if (selectedValues.Count == 0)
                {
                    OnFilterApplied?.Invoke(ColumnName, "1=0");
                }
                else
                {
                    bool isNumeric = _columnType == typeof(int) || _columnType == typeof(long) ||
                                   _columnType == typeof(double) || _columnType == typeof(float) ||
                                   _columnType == typeof(decimal);

                    IEnumerable<string> formatted;
                    if (isNumeric)
                    {
                        // Sayısal kolonlar için tırnaksız ve nokta (.) kullan
                        formatted = selectedValues.Select(v =>
                        {
                            if (string.IsNullOrEmpty(v)) return "NULL";
                            string clean = v.Replace(",", ".");
                            // Sadece sayısal kısımları alalım (bazen birim vs olabilir ama distinct listesinde temiz olmalı)
                            return clean;
                        });
                    }
                    else
                    {
                        formatted = selectedValues.Select(v => $"'{v.Replace("'", "''")}'");
                    }

                    string condition = $"\"{ColumnName}\" IN ({string.Join(",", formatted)})";

                    if (selectedValues.Contains(""))
                    {
                        condition = $"({condition} OR \"{ColumnName}\" IS NULL)";
                    }

                    OnFilterApplied?.Invoke(ColumnName, condition);
                }
            }
            OnPopupClosed?.Invoke();
        }

        private void BtnClearFilter_Click(object sender, RoutedEventArgs e)
        {
            OnFilterApplied?.Invoke(ColumnName, null);
            OnPopupClosed?.Invoke();
        }

        private void BtnSortAsc_Click(object sender, RoutedEventArgs e)
        {
            OnSortApplied?.Invoke(ColumnName, "ASC");
            OnPopupClosed?.Invoke();
        }

        private void BtnSortDesc_Click(object sender, RoutedEventArgs e)
        {
            OnSortApplied?.Invoke(ColumnName, "DESC");
            OnPopupClosed?.Invoke();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            OnPopupClosed?.Invoke();
        }

        /// <summary>Sets the Dark/Light theme colors for the popup.</summary>
        public void SetTheme(bool isDarkMode)
        {
            _isDarkMode = isDarkMode;
            Color bgColor = isDarkMode ? Color.FromRgb(24, 24, 27) : Colors.White;
            Color fgColor = isDarkMode ? Color.FromRgb(248, 250, 252) : Color.FromRgb(15, 23, 42);
            Color borderColor = isDarkMode ? Color.FromRgb(63, 63, 70) : Color.FromRgb(200, 200, 200);
            Color controlColor = isDarkMode ? Color.FromRgb(39, 39, 42) : Color.FromRgb(245, 245, 245);
            Color listColor = isDarkMode ? Color.FromRgb(9, 9, 11) : Colors.White;
            Color dividerColor = isDarkMode ? Color.FromRgb(63, 63, 70) : Color.FromRgb(230, 230, 230);
            Color subtleColor = isDarkMode ? Color.FromRgb(148, 163, 184) : Color.FromRgb(71, 85, 105);
            Color iconColor = isDarkMode ? Color.FromRgb(113, 113, 122) : Color.FromRgb(148, 163, 184);
            Color hoverColor = isDarkMode ? Color.FromRgb(63, 63, 70) : Color.FromRgb(226, 232, 240);
            Color unmatchedColor = isDarkMode ? Color.FromRgb(148, 163, 184) : Color.FromRgb(148, 163, 184);

            // Update DynamicResources
            this.Resources["PopupBackgroundBrush"] = new SolidColorBrush(bgColor);
            this.Resources["PopupForegroundBrush"] = new SolidColorBrush(fgColor);
            this.Resources["PopupBorderBrush"] = new SolidColorBrush(borderColor);
            this.Resources["ControlBackgroundBrush"] = new SolidColorBrush(controlColor);
            this.Resources["ListBackgroundBrush"] = new SolidColorBrush(listColor);
            this.Resources["DividerBrush"] = new SolidColorBrush(dividerColor);
            this.Resources["SubtleForegroundBrush"] = new SolidColorBrush(subtleColor);
            this.Resources["SearchIconBrush"] = new SolidColorBrush(iconColor);
            this.Resources["HoverBrush"] = new SolidColorBrush(hoverColor);
            this.Resources["UnmatchedBrush"] = new SolidColorBrush(unmatchedColor);

            // ContextMenu'yu da güncelle (Eğer açıksa)
            if (SubMenuFilters != null)
            {
                SubMenuFilters.Background = new SolidColorBrush(bgColor);
                SubMenuFilters.Foreground = new SolidColorBrush(fgColor);
                SubMenuFilters.BorderBrush = new SolidColorBrush(borderColor);
            }
        }
    }
}
