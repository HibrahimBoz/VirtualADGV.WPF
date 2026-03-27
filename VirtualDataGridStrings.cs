namespace VirtualADGV.WPF
{
    /// <summary>
    /// Contains all user-visible string labels for <see cref="VirtualDataGrid"/>.
    /// Set individual properties to customize the UI language.
    /// Default values are in English.
    /// </summary>
    /// <example>
    /// Turkish localization:
    /// <code>
    /// myGrid.Strings.AllColumns = "Tüm Sütunlar";
    /// myGrid.Strings.Apply = "Filtrele";
    /// myGrid.Strings.Cancel = "İptal";
    /// </code>
    /// </example>
    public class VirtualDataGridStrings
    {
        // ── Search bar ────────────────────────────────────────────
        /// <summary>Label shown in the column selector when searching all columns.</summary>
        public string AllColumns { get; set; } = "All Columns";

        /// <summary>Placeholder text inside the search box.</summary>
        public string SearchPlaceholder { get; set; } = "Search...";

        /// <summary>Label for the search box.</summary>
        public string SearchLabel { get; set; } = "Search:";

        /// <summary>Label for the column selector.</summary>
        public string ColumnLabel { get; set; } = "Column:";

        /// <summary>Label for the case sensitive checkbox.</summary>
        public string CaseSensitive { get; set; } = "Case Sensitive";

        /// <summary>Label for the find next button.</summary>
        public string FindNext { get; set; } = "Find Next";

        /// <summary>Label for the find reset/start over button.</summary>
        public string FindReset { get; set; } = "Restart Search";

        // ── Filter popup — sort buttons ───────────────────────────
        /// <summary>Sort ascending label for text columns (A → Z).</summary>
        public string SortAToZ { get; set; } = "Sort A to Z";

        /// <summary>Sort descending label for text columns (Z → A).</summary>
        public string SortZToA { get; set; } = "Sort Z to A";

        /// <summary>Sort ascending label for numeric columns.</summary>
        public string SortSmallToLarge { get; set; } = "Sort Smallest to Largest";

        /// <summary>Sort descending label for numeric columns.</summary>
        public string SortLargeToSmall { get; set; } = "Sort Largest to Smallest";

        /// <summary>Sort ascending label for date columns.</summary>
        public string SortOldToNew { get; set; } = "Sort Oldest to Newest";

        /// <summary>Sort descending label for date columns.</summary>
        public string SortNewToOld { get; set; } = "Sort Newest to Oldest";

        /// <summary>Clear sort button label.</summary>
        public string ClearSort { get; set; } = "Clear Sort";

        // ── Filter popup — filter actions ─────────────────────────
        /// <summary>Clear filter button label.</summary>
        public string ClearFilter { get; set; } = "Clear Filter";

        /// <summary>Header label for the number filters submenu.</summary>
        public string NumberFilters { get; set; } = "Number Filters";

        /// <summary>Header label for the date filters submenu.</summary>
        public string DateFilters { get; set; } = "Date Filters";

        /// <summary>Header label for the text filters submenu.</summary>
        public string TextFilters { get; set; } = "Text Filters";

        // ── Filter popup — submenu items ──────────────────────────
        /// <summary>"Equals..." menu item.</summary>
        public new string Equals { get; set; } = "Equals...";

        /// <summary>"Does Not Equal..." menu item.</summary>
        public string NotEquals { get; set; } = "Does Not Equal...";

        /// <summary>"Greater Than..." menu item.</summary>
        public string GreaterThan { get; set; } = "Greater Than...";

        /// <summary>"Greater Than or Equal..." menu item.</summary>
        public string GreaterThanOrEqual { get; set; } = "Greater Than or Equal...";

        /// <summary>"Less Than..." menu item.</summary>
        public string LessThan { get; set; } = "Less Than...";

        /// <summary>"Less Than or Equal..." menu item.</summary>
        public string LessThanOrEqual { get; set; } = "Less Than or Equal...";

        /// <summary>"Between..." menu item.</summary>
        public string Between { get; set; } = "Between...";

        /// <summary>"Begins With..." menu item.</summary>
        public string BeginsWith { get; set; } = "Begins With...";

        /// <summary>"Ends With..." menu item.</summary>
        public string EndsWith { get; set; } = "Ends With...";

        /// <summary>"Contains..." menu item.</summary>
        public string Contains { get; set; } = "Contains...";

        /// <summary>"Does Not Contain..." menu item.</summary>
        public string NotContains { get; set; } = "Does Not Contain...";

        /// <summary>"Custom Filter..." menu item.</summary>
        public string CustomFilter { get; set; } = "Custom Filter...";

        // ── Filter popup — value list ─────────────────────────────
        /// <summary>"(Select All)" checkbox label.</summary>
        public string SelectAll { get; set; } = "(Select All)";

        /// <summary>Display text for rows with an empty/null value.</summary>
        public string EmptyValue { get; set; } = "(Blank)";

        // ── Filter popup — action buttons ─────────────────────────
        /// <summary>Apply / Filter button label.</summary>
        public string Apply { get; set; } = "Filter";

        /// <summary>Cancel button label.</summary>
        public string Cancel { get; set; } = "Cancel";

        // ── Loading overlay ───────────────────────────────────────
        /// <summary>Text shown while filter values are being loaded.</summary>
        public string Loading { get; set; } = "Loading...";

        /// <summary>Error message shown when filter value loading fails.</summary>
        public string LoadingError { get; set; } = "Error loading filter data";

        // ── Custom filter window ──────────────────────────────────
        /// <summary>Title of the custom filter dialog window.</summary>
        public string CustomFilterTitle { get; set; } = "Custom AutoFilter";

        /// <summary>Criteria label shown above the filter rows.</summary>
        public string FilterCriteria { get; set; } = "Show rows where:";

        /// <summary>AND radio button label.</summary>
        public string And { get; set; } = "AND";

        /// <summary>OR radio button label.</summary>
        public string Or { get; set; } = "OR";

        /// <summary>OK button label.</summary>
        public string Ok { get; set; } = "OK";

        // ── Operator display texts (shown in combo boxes) ─────────
        /// <summary>"equals" operator.</summary>
        public string OpEquals { get; set; } = "equals";

        /// <summary>"does not equal" operator.</summary>
        public string OpNotEquals { get; set; } = "does not equal";

        /// <summary>"begins with" operator (strings).</summary>
        public string OpBeginsWith { get; set; } = "begins with";

        /// <summary>"ends with" operator (strings).</summary>
        public string OpEndsWith { get; set; } = "ends with";

        /// <summary>"contains" operator (strings).</summary>
        public string OpContains { get; set; } = "contains";

        /// <summary>"does not contain" operator (strings).</summary>
        public string OpNotContains { get; set; } = "does not contain";

        /// <summary>"greater than" operator (numbers).</summary>
        public string OpGreaterThan { get; set; } = "greater than";

        /// <summary>"greater than or equal" operator (numbers).</summary>
        public string OpGreaterThanOrEqual { get; set; } = "greater than or equal";

        /// <summary>"less than" operator (numbers).</summary>
        public string OpLessThan { get; set; } = "less than";

        /// <summary>"less than or equal" operator (numbers).</summary>
        public string OpLessThanOrEqual { get; set; } = "less than or equal";

        /// <summary>"before" operator (dates).</summary>
        public string OpBefore { get; set; } = "before";

        /// <summary>"after" operator (dates).</summary>
        public string OpAfter { get; set; } = "after";

        /// <summary>"before or equal" operator (dates).</summary>
        public string OpBeforeOrEqual { get; set; } = "before or equal";

        /// <summary>"after or equal" operator (dates).</summary>
        public string OpAfterOrEqual { get; set; } = "after or equal";
    }
}
