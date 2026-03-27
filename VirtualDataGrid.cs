using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Threading.Tasks;

namespace VirtualADGV.WPF
{
    /// <summary>
    /// An advanced virtualized DataGrid with built-in filtering, sorting, and search capabilities.
    /// Optimized for large datasets (1M+ rows).
    /// </summary>
    public class VirtualDataGrid : DataGrid
    {
        /// <summary>
        /// Localization strings for the UI. Default values are in English.
        /// </summary>
        public VirtualDataGridStrings Strings { get; } = new();

        /// <summary>DependencyProperty for FilterPopup.</summary>
        public static readonly DependencyProperty FilterPopupProperty =
            DependencyProperty.Register("FilterPopup", typeof(Popup), typeof(VirtualDataGrid), new PropertyMetadata(null));

        /// <summary>Attached property to highlight a cell or column.</summary>
        public static readonly DependencyProperty IsHighlightedProperty =
            DependencyProperty.RegisterAttached("IsHighlighted", typeof(bool), typeof(VirtualDataGrid), new PropertyMetadata(false));

        /// <summary>Sets the IsHighlighted attached property.</summary>
        public static void SetIsHighlighted(DependencyObject element, bool value) => element.SetValue(IsHighlightedProperty, value);
        /// <summary>Gets the IsHighlighted attached property.</summary>
        public static bool GetIsHighlighted(DependencyObject element) => (bool)element.GetValue(IsHighlightedProperty);

        /// <summary>Attached property to indicate if a column has an active filter.</summary>
        public static readonly DependencyProperty IsColumnFilteredProperty =
            DependencyProperty.RegisterAttached("IsColumnFiltered", typeof(bool), typeof(VirtualDataGrid), new PropertyMetadata(false));

        /// <summary>Sets the IsColumnFiltered attached property.</summary>
        public static void SetIsColumnFiltered(DependencyObject element, bool value) => element.SetValue(IsColumnFilteredProperty, value);
        /// <summary>Gets the IsColumnFiltered attached property.</summary>
        public static bool GetIsColumnFiltered(DependencyObject element) => (bool)element.GetValue(IsColumnFilteredProperty);

        /// <summary>Attached property for column sort direction indicator.</summary>
        public static readonly DependencyProperty ColumnSortDirectionProperty =
            DependencyProperty.RegisterAttached("ColumnSortDirection", typeof(string), typeof(VirtualDataGrid), new PropertyMetadata("NONE"));

        /// <summary>Sets the ColumnSortDirection attached property.</summary>
        public static void SetColumnSortDirection(DependencyObject element, string value) => element.SetValue(ColumnSortDirectionProperty, value);
        /// <summary>Gets the ColumnSortDirection attached property.</summary>
        public static string GetColumnSortDirection(DependencyObject element) => (string)element.GetValue(ColumnSortDirectionProperty);

        /// <summary>
        /// Gets or sets the Popup control used for column filtering.
        /// </summary>
        public Popup FilterPopup
        {
            get { return (Popup)GetValue(FilterPopupProperty); }
            set { SetValue(FilterPopupProperty, value); }
        }

        /// <summary>Occurs when a filter is applied or cleared.</summary>
        public event EventHandler<FilterEventArgs>? FilterChanged;
        /// <summary>Occurs when a column sort is requested.</summary>
        public event EventHandler<SortEventArgs>? SortChanged;
        /// <summary>Occurs when a search operation is performed.</summary>
        public event EventHandler<SearchEventArgs>? SearchRequested;

        /// <summary>DependencyProperty for IsSearchVisible.</summary>
        public static readonly DependencyProperty IsSearchVisibleProperty =
            DependencyProperty.Register("IsSearchVisible", typeof(bool), typeof(VirtualDataGrid), new PropertyMetadata(false));

        /// <summary>
        /// Gets or sets whether the search panel is visible.
        /// </summary>
        public bool IsSearchVisible
        {
            get { return (bool)GetValue(IsSearchVisibleProperty); }
            set { SetValue(IsSearchVisibleProperty, value); }
        }

        /// <summary>Attached property to enable/disable filtering on a column.</summary>
        public static readonly DependencyProperty IsFilterEnabledProperty =
            DependencyProperty.RegisterAttached("IsFilterEnabled", typeof(bool), typeof(VirtualDataGrid), new PropertyMetadata(true));

        /// <summary>Sets the IsFilterEnabled attached property.</summary>
        public static void SetIsFilterEnabled(DependencyObject element, bool value) => element.SetValue(IsFilterEnabledProperty, value);
        /// <summary>Gets the IsFilterEnabled attached property.</summary>
        public static bool GetIsFilterEnabled(DependencyObject element) => (bool)element.GetValue(IsFilterEnabledProperty);

        /// <summary>Attached property to enable/disable sorting on a column.</summary>
        public static readonly DependencyProperty IsSortEnabledProperty =
            DependencyProperty.RegisterAttached("IsSortEnabled", typeof(bool), typeof(VirtualDataGrid), new PropertyMetadata(true));

        /// <summary>Sets the IsSortEnabled attached property.</summary>
        public static void SetIsSortEnabled(DependencyObject element, bool value) => element.SetValue(IsSortEnabledProperty, value);
        /// <summary>Gets the IsSortEnabled attached property.</summary>
        public static bool GetIsSortEnabled(DependencyObject element) => (bool)element.GetValue(IsSortEnabledProperty);

        /// <summary>
        /// Enables or disables filtering for a specific column by index.
        /// </summary>
        public void SetColumnFilterEnabled(int columnIndex, bool enabled)
        {
            if (columnIndex >= 0 && columnIndex < this.Columns.Count)
                SetIsFilterEnabled(this.Columns[columnIndex], enabled);
        }

        /// <summary>
        /// Enables or disables sorting for a specific column by index.
        /// </summary>
        public void SetColumnSortEnabled(int columnIndex, bool enabled)
        {
            if (columnIndex >= 0 && columnIndex < this.Columns.Count)
                SetIsSortEnabled(this.Columns[columnIndex], enabled);
        }

        /// <summary>
        /// Occurs when the filter popup is opening and needs data for the distinct value list.
        /// </summary>
        public event EventHandler<LoadingFilterEventArgs>? LoadingFilterValues;

        static VirtualDataGrid()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(VirtualDataGrid), new FrameworkPropertyMetadata(typeof(VirtualDataGrid)));
        }

        /// <summary>Initializes a new instance of VirtualDataGrid.</summary>
        public VirtualDataGrid()
        {
            // Prevent UI freeze on large datasets when pressing Ctrl+A
            this.CommandBindings.Add(new CommandBinding(ApplicationCommands.SelectAll, (s, e) => e.Handled = true));

            this.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnHeaderButtonClick));
            this.PreviewMouseDoubleClick += OnGridDoubleClick;
        }

        private TextBox? _searchTextBox;
        private ComboBox? _searchColumnComboBox;
        private CheckBox? _searchCaseSensitive;
        private Button? _searchButton;
        private Button? _searchResetButton;

        /// <summary>Applies the template and initializes internal controls.</summary>
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _searchTextBox = GetTemplateChild("PART_SearchTextBox") as TextBox;
            _searchColumnComboBox = GetTemplateChild("PART_SearchColumnComboBox") as ComboBox;
            _searchCaseSensitive = GetTemplateChild("PART_SearchCaseSensitive") as CheckBox;
            _searchButton = GetTemplateChild("PART_SearchButton") as Button;
            _searchResetButton = GetTemplateChild("PART_SearchResetButton") as Button;

            if (_searchButton != null)
                _searchButton.Click += (s, e) => TriggerSearch(true);

            if (_searchResetButton != null)
                _searchResetButton.Click += (s, e) => TriggerSearch(false);

            if (_searchTextBox != null)
                _searchTextBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) TriggerSearch(true); };

            UpdateSearchColumns();
        }

        /// <summary>Handles key down events, specifically Ctrl+F for search.</summary>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
            {
                this.IsSearchVisible = !this.IsSearchVisible;
                if (this.IsSearchVisible && _searchTextBox != null)
                {
                    _searchTextBox.Dispatcher.BeginInvoke(new Action(() => _searchTextBox.Focus()), System.Windows.Threading.DispatcherPriority.Input);
                }
                e.Handled = true;
                return;
            }
            base.OnKeyDown(e);
        }

        /// <summary>
        /// Asynchronously load filter values. Use this to prevent UI freezing on large datasets.
        /// </summary>
        public Func<object, LoadingFilterEventArgs, Task>? LoadingFilterValuesAsync { get; set; }

        /// <summary>
        /// Refreshes the search column dropdown list based on current grid columns.
        /// </summary>
        public void UpdateSearchColumns()
        {
            if (_searchColumnComboBox == null) return;
            var currentSelection = _searchColumnComboBox.SelectedItem as string;
            
            _searchColumnComboBox.Items.Clear();
            _searchColumnComboBox.Items.Add(Strings.AllColumns);

            foreach (var col in this.Columns)
            {
                if (col.Header != null)
                    _searchColumnComboBox.Items.Add(col.Header.ToString());
            }

            if (currentSelection != null && _searchColumnComboBox.Items.Contains(currentSelection))
            {
                _searchColumnComboBox.SelectedItem = currentSelection;
            }
            else
            {
                _searchColumnComboBox.SelectedIndex = 0;
            }
        }

        /// <summary>Updates search columns when the grid size changes (initial load helper).</summary>
        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            if (_searchColumnComboBox != null && _searchColumnComboBox.Items.Count <= 1 && this.Columns.Count > 0)
            {
                UpdateSearchColumns();
            }
        }

        private void TriggerSearch(bool searchNext)
        {
            if (SearchRequested == null) return;
            string text = _searchTextBox?.Text ?? "";
            if (string.IsNullOrEmpty(text)) return;

            string? col = _searchColumnComboBox?.SelectedItem as string;
            if (col == Strings.AllColumns) col = "";

            bool isCaseSensitive = _searchCaseSensitive?.IsChecked == true;

            var args = new SearchEventArgs
            {
                SearchText = text,
                ColumnName = col ?? "",
                IsCaseSensitive = isCaseSensitive,
                SearchNext = searchNext
            };

            SearchRequested.Invoke(this, args);
        }

        private bool _isDarkMode = true;
        /// <summary>
        /// Sets the theme (Dark/Light) for the internal popups.
        /// </summary>
        /// <param name="isDarkMode">True for dark mode, false for light mode.</param>
        public void SetTheme(bool isDarkMode)
        {
            _isDarkMode = isDarkMode;

            Color bgColor = isDarkMode ? Color.FromRgb(24, 24, 27) : Colors.White;
            Color fgColor = isDarkMode ? Color.FromRgb(248, 250, 252) : Color.FromRgb(15, 23, 42);

            this.Resources["PopupBackgroundBrush"] = new SolidColorBrush(bgColor);
            this.Resources["PopupForegroundBrush"] = new SolidColorBrush(fgColor);
        }

        private void OnGridDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.Handled) return;

            if (e.OriginalSource is DependencyObject source && FindParent<Thumb>(source) != null)
            {
                e.Handled = true;
                return;
            }

            var obj = e.OriginalSource as DependencyObject;
            var header = FindParent<DataGridColumnHeader>(obj);
            
            if (header != null)
            {
                e.Handled = true;
                SelectEntireColumn(header.Column);
                return;
            }

            var cell = FindParent<DataGridCell>(obj);
            if (cell != null || FindParent<DataGridRow>(obj) != null)
            {
                e.Handled = true; 
            }
        }

        private void SelectEntireColumn(DataGridColumn column)
        {
            if (column == null) return;

            bool current = GetIsHighlighted(column);

            foreach (var col in this.Columns) SetIsHighlighted(col, false);

            SetIsHighlighted(column, !current);

            this.Items.Refresh();
        }

        private void OnHeaderButtonClick(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is Button btn && btn.Name == "FilterButton")
            {
                var header = FindParent<DataGridColumnHeader>(btn);
                if (header == null) return;

                OnFilterButtonClick(btn, header);
            }
        }

        /// <summary>Handles the filter button click asynchronosuly.</summary>
        protected virtual async void OnFilterButtonClick(Button button, DataGridColumnHeader header)
        {
            if (FilterPopup == null) return;

            var column = header.Column;
            string colName = column.Header?.ToString() ?? "";
            if (string.IsNullOrEmpty(colName)) return;

            if (FilterPopup.Child is AdvancedCustomFilterPopup filterUI)
            {
                Type? dataType = null;
                if (this.ItemsSource is System.Data.DataView view && view.Table.Columns.Contains(colName))
                {
                    dataType = view.Table.Columns[colName].DataType;
                }

                var args = new LoadingFilterEventArgs { ColumnName = colName, ColumnType = dataType ?? typeof(string) };

                filterUI.SetTheme(_isDarkMode);
                filterUI.IsLoading = true;
                FilterPopup.PlacementTarget = button;
                FilterPopup.IsOpen = true;

                if (LoadingFilterValuesAsync != null) await LoadingFilterValuesAsync(this, args);
                else LoadingFilterValues?.Invoke(this, args);

                filterUI.Initialize(colName, args.DistinctValues ?? new List<string>(), args.ActiveFilters ?? new List<string>(), args.ColumnType ?? typeof(string), Strings);
                filterUI.IsLoading = false;

                filterUI.OnFilterApplied = (c, cond) =>
                {
                    SetIsColumnFiltered(column, !string.IsNullOrEmpty(cond));
                    FilterChanged?.Invoke(this, new FilterEventArgs { ColumnName = c, FilterString = cond });
                };
                filterUI.OnSortApplied = (c, dir) =>
                {
                    foreach (var col in this.Columns) SetColumnSortDirection(col, "NONE");

                    SetColumnSortDirection(column, dir?.ToUpper() ?? "NONE");
                    SortChanged?.Invoke(this, new SortEventArgs { ColumnName = c, SortDirection = dir });
                };
                filterUI.OnPopupClosed = () => FilterPopup.IsOpen = false;

                FilterPopup.PlacementTarget = button;
                FilterPopup.IsOpen = true;
            }
        }

        private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
        {
            if (child == null) return null;
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            T? parent = parentObject as T;
            return parent ?? FindParent<T>(parentObject);
        }
    }

    /// <summary>Arguments for filter data loading event.</summary>
    public class LoadingFilterEventArgs : EventArgs
    {
        /// <summary>Name of the column being filtered.</summary>
        public string? ColumnName { get; set; }
        /// <summary>Data type of the column.</summary>
        public Type? ColumnType { get; set; }
        /// <summary>The list of unique values returned by the host application.</summary>
        public IEnumerable<string>? DistinctValues { get; set; }
        /// <summary>The list of currently active filters (if any).</summary>
        public IEnumerable<string>? ActiveFilters { get; set; }
    }

    /// <summary>Arguments for filter changed event.</summary>
    public class FilterEventArgs : EventArgs
    {
        /// <summary>Name of the column.</summary>
        public string? ColumnName { get; set; }
        /// <summary>The new SQL-like filter string or custom condition.</summary>
        public string? FilterString { get; set; }
    }

    /// <summary>Arguments for sort changed event.</summary>
    public class SortEventArgs : EventArgs
    {
        /// <summary>Name of the column.</summary>
        public string? ColumnName { get; set; }
        /// <summary>Sort direction: "ASC" or "DESC".</summary>
        public string? SortDirection { get; set; }
    }

    /// <summary>Arguments for search requested event.</summary>
    public class SearchEventArgs : EventArgs
    {
        /// <summary>The text to find.</summary>
        public string? SearchText { get; set; }
        /// <summary>The specific column to search within (empty for all columns).</summary>
        public string? ColumnName { get; set; }
        /// <summary>Whether search is case-sensitive.</summary>
        public bool IsCaseSensitive { get; set; }
        /// <summary>True to find next occurrence, false to restart from beginning.</summary>
        public bool SearchNext { get; set; }
    }
}
