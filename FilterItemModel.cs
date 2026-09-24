using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace VirtualADGV.WPF
{
    /// <summary>
    /// Represents an item in the filter list (checkbox list or tree view).
    /// Supports hierarchical selection (Select All, Year > Month > Day).
    /// </summary>
    public class FilterItemModel : INotifyPropertyChanged
    {
        private static readonly PropertyChangedEventArgs IsCheckedChangedArgs = new(nameof(IsChecked));

        private bool? _isChecked = true;
        /// <summary>Gets or sets the checked state. Supports indeterminate state.</summary>
        public bool? IsChecked
        {
            get => _isChecked;
            set => SetIsChecked(value);
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

        private bool _isEnabled = true;
        /// <summary>
        /// Whether this value still exists under the other columns' active filters.
        /// Disabled (0-row) values are shown grayed and cannot be checked.
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    if (!SuppressNotification) OnPropertyChanged(nameof(IsEnabled));
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

        private void SetIsChecked(bool? value)
        {
            if (_isChecked == value) return;
            _isChecked = value;
            if (SuppressNotification) return;

            OnPropertyChanged(IsCheckedChangedArgs);
            if (value.HasValue) PropagateToChildren(value.Value);
            UpdateParentCheckState();
        }

        // Aşağı yayılım: çocuklar bildirimle güncellenir (açık düğümlerdeki checkbox'lar da
        // yenilenir) ama her biri ebeveyni yeniden hesaplamaz; ebeveyn zaten bu düğüm.
        private void PropagateToChildren(bool value)
        {
            foreach (var child in Children)
            {
                if (child._isChecked != value)
                {
                    child._isChecked = value;
                    child.OnPropertyChanged(IsCheckedChangedArgs);
                }
                child.PropagateToChildren(value);
            }
        }

        /// <summary>Updates the parent check state based on children (None/Partial/All checked).</summary>
        public void UpdateParentCheckState()
        {
            var parent = Parent;
            if (parent == null) return;

            bool? state = ComputeState(parent.Children);
            if (parent._isChecked == state) return; // Değişmediyse üst düğümler de değişmez

            parent._isChecked = state;
            if (!SuppressNotification) parent.OnPropertyChanged(IsCheckedChangedArgs);
            parent.UpdateParentCheckState();
        }

        /// <summary>Sets the state without notifications or propagation (list construction only).</summary>
        internal void InitializeCheckState(bool? value) => _isChecked = value;

        /// <summary>Derives this node's state from its children without notifications (list construction only).</summary>
        internal void InitializeCheckStateFromChildren()
        {
            if (Children.Count > 0) _isChecked = ComputeState(Children);
        }

        private static bool? ComputeState(IEnumerable<FilterItemModel> children)
        {
            bool hasChecked = false, hasUnchecked = false;
            foreach (var c in children)
            {
                if (c._isChecked == true) hasChecked = true;
                else if (c._isChecked == false) hasUnchecked = true;
                else return null;

                if (hasChecked && hasUnchecked) return null;
            }
            if (hasChecked) return true;
            if (hasUnchecked) return false;
            return null;
        }

        /// <summary>Occurs when a property value changes.</summary>
        public event PropertyChangedEventHandler? PropertyChanged;
        /// <summary>Raises the PropertyChanged event.</summary>
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private void OnPropertyChanged(PropertyChangedEventArgs args) => PropertyChanged?.Invoke(this, args);
    }
}
