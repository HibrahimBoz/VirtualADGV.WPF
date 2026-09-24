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
        public ObservableCollection<FilterItemModel> FilterItems => _filterItems;
        private readonly BulkObservableCollection<FilterItemModel> _filterItems = new();
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
        private HashSet<string> _previousSelectedValues = new HashSet<string>();
        private System.Windows.Threading.DispatcherTimer _searchTimer;
        private string _searchText = string.Empty; // SearchFilter her öğede TextBox'a gitmesin diye önbellek

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
                _searchText = string.IsNullOrWhiteSpace(TxtSearch.Text) ? string.Empty : TxtSearch.Text;
                _filterItemsView.Refresh();
                UpdateSelectAllCheckBox();
            };

            this.Unloaded += (s, e) => _searchTimer?.Stop();
        }

        private Type _columnType = typeof(string);

        /// <summary>
        /// Values that still exist under the other columns' active filters. When non-null,
        /// any distinct value NOT in this set is shown disabled (grayed, unselectable).
        /// </summary>
        private ISet<string>? _enabledValues;

        /// <summary>
        /// Initializes the popup with column data and current state.
        /// </summary>
        public void Initialize(string columnName, IEnumerable<string> distinctValues, IEnumerable<string> activeFilters, Type columnType, VirtualDataGridStrings strings, ISet<string>? enabledValues = null)
        {
            Strings = strings;
            ColumnName = columnName;
            _columnType = columnType;
            _enabledValues = enabledValues;
            // HashSet: değer başına Contains O(1) — List ile O(n·m) idi, büyük listelerde UI donuyordu
            _previousSelectedValues = new HashSet<string>(activeFilters ?? Enumerable.Empty<string>());

            BtnClearFilter.IsEnabled = _previousSelectedValues.Count > 0;
            TxtSearch.Text = "";
            _searchText = string.Empty;
            _searchTimer.Stop(); // Temizleme TextChanged tetiklediyse gereksiz ikinci Refresh'i engelle

            bool isNumeric = FilterExpressionBuilder.IsNumericType(columnType);
            bool isDate = FilterExpressionBuilder.IsDateType(columnType);

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
                bool isDate = FilterListBuilder.UsesDateTree(_columnType);

                // UI Seçimi: Tarihler (DateTime) için TreeView, Diğerleri (TimeSpan dahil) için ListBox
                LstItems.Visibility = isDate ? Visibility.Collapsed : Visibility.Visible;
                TreeViewItems.Visibility = isDate ? Visibility.Visible : Visibility.Collapsed;

                var items = FilterListBuilder.Build(distinctValues, _previousSelectedValues, _enabledValues, _columnType, Strings.EmptyValue);

                // Tek Reset bildirimi: öğe başına CollectionChanged + görünüm güncellemesi yerine bir kez
                _filterItems.ReplaceAll(items);

                UpdateSelectAllCheckBox();
            }
            catch (Exception ex)
            {
                _filterItems.ReplaceAll(Array.Empty<FilterItemModel>()); // Önceki sütunun değerleri kalmasın
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
                bool match = _searchText.Length == 0 ||
                             filterItem.DisplayText.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
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
                // Bildirimler açık kalır: yalnızca realize edilmiş (görünür) satırlar dinlediği için
                // ucuzdur ve tarih ağacında alt düğümlere de yayılır. Tüm listeyi yeniden üreten
                // Refresh() çağrısına gerek kalmaz.
                foreach (object item in _filterItemsView)
                {
                    if (item is FilterItemModel filterItem && filterItem.IsEnabled)
                    {
                        filterItem.IsSelected = isChecked;
                    }
                }
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
                if (!item.IsEnabled) continue; // Disabled (0-satır) değerler sayıma katılmaz
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
            // Tüm yaprak düğümleri topla (düz liste veya tarih ağacı)
            var leaves = new List<FilterItemModel>();
            void CollectLeaves(IEnumerable<FilterItemModel> nodes)
            {
                foreach (var n in nodes)
                {
                    if (n.Children.Count == 0) leaves.Add(n);
                    else CollectLeaves(n.Children);
                }
            }
            CollectLeaves(FilterItems);

            // Etkin (disabled olmayan) tüm değerler işaretliyse bu sütun fiilen
            // filtre uygulamıyor demektir → filtreyi temizle. Disabled değerler zaten
            // diğer sütunların filtresiyle dışarıda; bu sütuna filtre eklememeliyiz.
            bool allSelected = leaves.Where(l => l.IsEnabled).All(l => l.IsChecked == true);

            if (allSelected)
            {
                OnFilterApplied?.Invoke(ColumnName, null);
            }
            else
            {
                var selectedValues = leaves.Where(l => l.IsChecked == true).Select(l => l.Value).ToList();

                // Sütun adı ve değerler kaçışlanır; sayısal değerler yalnızca geçerli sayıysa çıplak
                // yazılır — veri/başlık içeriği SQL ifadesinin dışına taşamaz ("1=0" boş seçim içindir)
                bool isNumeric = FilterExpressionBuilder.IsNumericType(_columnType);
                OnFilterApplied?.Invoke(ColumnName, FilterExpressionBuilder.BuildInCondition(ColumnName, selectedValues, isNumeric));
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
            this.Resources["PopupBackgroundBrush"] = ThemeBrush.Create(bgColor);
            this.Resources["PopupForegroundBrush"] = ThemeBrush.Create(fgColor);
            this.Resources["PopupBorderBrush"] = ThemeBrush.Create(borderColor);
            this.Resources["ControlBackgroundBrush"] = ThemeBrush.Create(controlColor);
            this.Resources["ListBackgroundBrush"] = ThemeBrush.Create(listColor);
            this.Resources["DividerBrush"] = ThemeBrush.Create(dividerColor);
            this.Resources["SubtleForegroundBrush"] = ThemeBrush.Create(subtleColor);
            this.Resources["SearchIconBrush"] = ThemeBrush.Create(iconColor);
            this.Resources["HoverBrush"] = ThemeBrush.Create(hoverColor);
            this.Resources["UnmatchedBrush"] = ThemeBrush.Create(unmatchedColor);

            // ContextMenu'yu da güncelle (Eğer açıksa)
            if (SubMenuFilters != null)
            {
                SubMenuFilters.Background = ThemeBrush.Create(bgColor);
                SubMenuFilters.Foreground = ThemeBrush.Create(fgColor);
                SubMenuFilters.BorderBrush = ThemeBrush.Create(borderColor);
            }
        }
    }
}
