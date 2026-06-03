# VirtualADGV.WPF

> **English** | [Türkçe](#türkçe)

An advanced, high-performance WPF DataGrid control with Excel-like filtering, multi-column sorting, search, and full dark/light theme support. Designed for large datasets (1M+ rows).

---

## Features

### Filtering
- **Excel-style filter popup** on every column header
- **Checkbox list** for text/numeric columns — select individual values
- **Hierarchical tree view** for date columns — group by Year → Month → Day
- **Custom filter dialog** — build compound conditions (e.g., `> 100 AND < 500`)
- **Cascading filter (Excel-style)** — values that no longer exist under the other columns' active filters are shown **disabled** (grayed and unselectable) via `LoadingFilterEventArgs.EnabledValues`, so out-of-view values never come pre-checked
- **Operator support:**
  - Text: equals, does not equal, begins with, ends with, contains, does not contain
  - Number: equals, does not equal, greater than, greater/less than or equal, between
  - Date: equals, before, after, before/after or equal
- Filter indicator on column header (highlighted when active)
- One-click clear filter

### Sorting
- Ascending / Descending sort per column via filter popup
- Sort direction indicator on column header
- One-click clear sort

### Search (Ctrl+F)
- Collapsible search panel toggled with `Ctrl+F`
- Search in all columns or a specific column
- Case-sensitive option
- Find next / restart from beginning

### Theming
- Full **Dark mode** and **Light mode** support
- Switched at runtime via `SetTheme(isDarkMode: bool)`
- All colors driven by `DynamicResource` — compatible with custom themes

### Performance
- Built on WPF's built-in row/column virtualization (`EnableRowVirtualization`, `EnableColumnVirtualization`)
- `LoadingFilterValuesAsync` — async data loading prevents UI freeze on large datasets
- `SelectAll` (Ctrl+A) intentionally disabled to prevent freezing on 1M+ row grids

### Localization
- All UI strings are configurable via `grid.Strings.*`
- Default: **English**
- Ships with built-in Turkish string set (see usage example)

---

## Installation

### From GitHub Packages

1. Add the source to your `nuget.config`:

```xml
<configuration>
  <packageSources>
    <add key="github-hibrahimboz" value="https://nuget.pkg.github.com/HibrahimBoz/index.json" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
  <packageSourceCredentials>
    <github-hibrahimboz>
      <add key="Username" value="YOUR_GITHUB_USERNAME" />
      <add key="ClearTextPassword" value="YOUR_GITHUB_PAT" />
    </github-hibrahimboz>
  </packageSourceCredentials>
</configuration>
```

> A GitHub Personal Access Token (PAT) with `read:packages` scope is required.

2. Install the package:

```bash
dotnet add package VirtualADGV.WPF --version 1.0.3
```

---

## Usage

### 1. Add XML namespace

```xml
xmlns:adgv="clr-namespace:VirtualADGV.WPF;assembly=VirtualADGV.WPF"
```

### 2. Declare the grid and filter popup in XAML

```xml
<!-- Filter popup (shared across all column headers) -->
<Popup x:Name="GlobalFilterPopup" StaysOpen="False" Placement="Bottom">
    <adgv:AdvancedCustomFilterPopup x:Name="FilterControl" Width="240" />
</Popup>

<!-- DataGrid -->
<adgv:VirtualDataGrid x:Name="MyGrid"
    ItemsSource="{Binding MyDataView}"
    FilterPopup="{Binding ElementName=GlobalFilterPopup}" />
```

### 3. Handle events in code-behind

```csharp
MyGrid.LoadingFilterValuesAsync = async (sender, args) =>
{
    // Return distinct values for the column from your data source
    args.DistinctValues = await GetDistinctValuesAsync(args.ColumnName);
    // Values currently selected by this column's own filter (shown checked)
    args.ActiveFilters  = GetActiveFilters(args.ColumnName);
    // OPTIONAL — cascading filter: values that still exist under the OTHER
    // columns' active filters. Anything not in this set is shown disabled
    // (grayed, unselectable). Leave null to keep every value enabled.
    args.EnabledValues  = await GetEnabledValuesAsync(args.ColumnName);
};

MyGrid.FilterChanged += (sender, args) =>
{
    // args.ColumnName  — which column
    // args.FilterString — SQL-like condition, null = cleared
    ApplyFilter(args.ColumnName, args.FilterString);
};

MyGrid.SortChanged += (sender, args) =>
{
    ApplySort(args.ColumnName, args.SortDirection); // "ASC" or "DESC"
};

MyGrid.SearchRequested += (sender, args) =>
{
    Search(args.SearchText, args.ColumnName, args.IsCaseSensitive, args.SearchNext);
};
```

### 4. Apply theme

```csharp
MyGrid.SetTheme(isDarkMode: true);
```

### 5. Localization (Turkish example)

```csharp
MyGrid.Strings.AllColumns       = "Tüm Sütunlar";
MyGrid.Strings.Apply            = "Filtrele";
MyGrid.Strings.Cancel           = "İptal";
MyGrid.Strings.SelectAll        = "(Tümünü Seç)";
MyGrid.Strings.EmptyValue       = "(Boş)";
MyGrid.Strings.NumberFilters    = "Sayı Filtreleri";
MyGrid.Strings.CustomFilter     = "Özel Filtre...";
MyGrid.Strings.OpEquals         = "eşittir";
MyGrid.Strings.OpGreaterThan    = "büyük";
// ... (see VirtualDataGridStrings.cs for all properties)
```

---

## API Reference

### `VirtualDataGrid`

| Member | Type | Description |
|--------|------|-------------|
| `FilterPopup` | `Popup` | The popup used for column filtering |
| `IsSearchVisible` | `bool` | Show/hide the search panel |
| `Strings` | `VirtualDataGridStrings` | Localization strings |
| `LoadingFilterValuesAsync` | `Func<object, LoadingFilterEventArgs, Task>` | Async filter value loader |
| `FilterChanged` | `EventHandler<FilterEventArgs>` | Fired when a filter is applied or cleared |
| `SortChanged` | `EventHandler<SortEventArgs>` | Fired when sort direction changes |
| `SearchRequested` | `EventHandler<SearchEventArgs>` | Fired when search is executed |
| `SetTheme(bool)` | method | Switch Dark/Light theme |
| `UpdateSearchColumns()` | method | Refresh search column dropdown |
| `SetColumnFilterEnabled(int, bool)` | method | Enable/disable filter per column |
| `SetColumnSortEnabled(int, bool)` | method | Enable/disable sort per column |

### `LoadingFilterEventArgs`

| Member | Type | Description |
|--------|------|-------------|
| `ColumnName` | `string` | The column whose filter popup is opening |
| `ColumnType` | `Type` | Column data type (drives numeric/date behavior) |
| `DistinctValues` | `IEnumerable<string>` | Full distinct value list to display |
| `ActiveFilters` | `IEnumerable<string>` | Values currently selected (shown checked) |
| `EnabledValues` | `ISet<string>?` | Values still present under other columns' filters; values outside this set are shown disabled. `null` = all enabled |

### Attached Properties

| Property | Target | Description |
|----------|--------|-------------|
| `IsHighlighted` | Column / Row | Visual highlight (e.g., on double-click) |
| `IsColumnFiltered` | Column | Shows filter indicator on header |
| `ColumnSortDirection` | Column | `"ASC"`, `"DESC"`, or `"NONE"` |
| `IsFilterEnabled` | Column | Allow filter popup on this column |
| `IsSortEnabled` | Column | Allow sorting on this column |

---

## Requirements

- .NET 8.0 (Windows)
- WPF (Windows Presentation Foundation)
- `ItemsSource` should be `System.Data.DataView` for automatic column type detection

---

## License

MIT — see [LICENSE](LICENSE)

---

---

# Türkçe

> [English](#virtualadgvwpf) | **Türkçe**

Büyük veri setleri (1M+ satır) için tasarlanmış, Excel benzeri filtreleme, çok sütunlu sıralama, arama ve tam dark/light tema desteği sunan gelişmiş WPF DataGrid kontrolü.

---

## Özellikler

### Filtreleme
- Her sütun başlığında **Excel tarzı filtre popup'ı**
- Metin/sayı sütunları için **checkbox listesi** — değerleri tek tek seç
- Tarih sütunları için **hiyerarşik ağaç görünümü** — Yıl → Ay → Gün gruplandırması
- **Özel filtre diyaloğu** — bileşik koşullar oluştur (örn. `> 100 VE < 500`)
- **Kademeli filtre (Excel tarzı)** — diğer sütunların aktif filtreleri altında artık var olmayan değerler `LoadingFilterEventArgs.EnabledValues` ile **disabled** (gri ve seçilemez) gösterilir; böylece tabloda görünmeyen değerler asla işaretli gelmez
- **Operatör desteği:**
  - Metin: eşittir, eşit değildir, başlar, biter, içerir, içermez
  - Sayı: eşittir, eşit değildir, büyüktür, büyük/küçük veya eşittir, arasında
  - Tarih: eşittir, önce, sonra, önce/sonra veya eşit
- Sütun başlığında filtre göstergesi (aktifken vurgulanır)
- Tek tıkla filtre temizleme

### Sıralama
- Filtre popup'ı üzerinden sütun başına Artan / Azalan sıralama
- Sütun başlığında sıralama yönü göstergesi
- Tek tıkla sıralama temizleme

### Arama (Ctrl+F)
- `Ctrl+F` ile açılıp kapanabilen arama paneli
- Tüm sütunlarda veya belirli bir sütunda arama
- Büyük/küçük harf duyarlılığı seçeneği
- İleri bul / baştan ara

### Tema
- Tam **Dark mod** ve **Light mod** desteği
- `SetTheme(isDarkMode: bool)` ile çalışma zamanında değiştirilebilir
- Tüm renkler `DynamicResource` ile tanımlı — özel temalarla uyumlu

### Performans
- WPF'in yerleşik satır/sütun sanallaştırması üzerine kurulu
- `LoadingFilterValuesAsync` — async veri yükleme, büyük veri setlerinde UI'ı dondurmaz
- 1M+ satırlı grids'te donmayı engellemek için `SelectAll` (Ctrl+A) kasıtlı devre dışı

### Lokalizasyon
- Tüm UI metinleri `grid.Strings.*` üzerinden özelleştirilebilir
- Varsayılan: **İngilizce**
- Türkçe kullanım için aşağıdaki örneğe bakın

---

## Kurulum

### GitHub Packages'tan

1. `nuget.config` dosyanıza kaynak ekleyin:

```xml
<configuration>
  <packageSources>
    <add key="github-hibrahimboz" value="https://nuget.pkg.github.com/HibrahimBoz/index.json" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
  <packageSourceCredentials>
    <github-hibrahimboz>
      <add key="Username" value="GITHUB_KULLANICI_ADINIZ" />
      <add key="ClearTextPassword" value="GITHUB_PAT_TOKENINIZ" />
    </github-hibrahimboz>
  </packageSourceCredentials>
</configuration>
```

> `read:packages` yetkisine sahip bir GitHub Personal Access Token (PAT) gereklidir.

2. Paketi yükleyin:

```bash
dotnet add package VirtualADGV.WPF --version 1.0.3
```

---

## Kullanım

### 1. XML namespace ekle

```xml
xmlns:adgv="clr-namespace:VirtualADGV.WPF;assembly=VirtualADGV.WPF"
```

### 2. XAML'de grid ve filtre popup'ını tanımla

```xml
<!-- Filtre popup'ı (tüm sütun başlıkları tarafından paylaşılır) -->
<Popup x:Name="GlobalFilterPopup" StaysOpen="False" Placement="Bottom">
    <adgv:AdvancedCustomFilterPopup x:Name="FilterControl" Width="240" />
</Popup>

<!-- DataGrid -->
<adgv:VirtualDataGrid x:Name="MyGrid"
    ItemsSource="{Binding MyDataView}"
    FilterPopup="{Binding ElementName=GlobalFilterPopup}" />
```

### 3. Olayları bağla

```csharp
MyGrid.LoadingFilterValuesAsync = async (sender, args) =>
{
    args.DistinctValues = await GetDistinctValuesAsync(args.ColumnName);
    // Bu sütunun kendi filtresinin seçtiği değerler (işaretli gelir)
    args.ActiveFilters  = GetActiveFilters(args.ColumnName);
    // İSTEĞE BAĞLI — kademeli filtre: DİĞER sütunların filtreleri altında hâlâ
    // var olan değerler. Bu kümede olmayanlar disabled (gri, seçilemez) gösterilir.
    // null bırakılırsa tüm değerler etkin kalır.
    args.EnabledValues  = await GetEnabledValuesAsync(args.ColumnName);
};

MyGrid.FilterChanged += (sender, args) =>
{
    // args.ColumnName  — hangi sütun
    // args.FilterString — SQL benzeri koşul, null = temizlendi
    FiltreyiUygula(args.ColumnName, args.FilterString);
};

MyGrid.SortChanged += (sender, args) =>
{
    SiralamayiUygula(args.ColumnName, args.SortDirection); // "ASC" veya "DESC"
};
```

### 4. Tema uygula

```csharp
MyGrid.SetTheme(isDarkMode: true);
```

### 5. Türkçe lokalizasyon

```csharp
MyGrid.Strings.AllColumns       = "Tüm Sütunlar";
MyGrid.Strings.Apply            = "Filtrele";
MyGrid.Strings.Cancel           = "İptal";
MyGrid.Strings.SelectAll        = "(Tümünü Seç)";
MyGrid.Strings.EmptyValue       = "(Boş)";
MyGrid.Strings.NumberFilters    = "Sayı Filtreleri";
MyGrid.Strings.DateFilters      = "Tarih Filtreleri";
MyGrid.Strings.TextFilters      = "Metin Filtreleri";
MyGrid.Strings.CustomFilter     = "Özel Filtre...";
MyGrid.Strings.OpEquals         = "eşittir";
MyGrid.Strings.OpGreaterThan    = "büyük";
// ... tüm property'ler için VirtualDataGridStrings.cs dosyasına bakın
```

---

## Lisans

MIT — [LICENSE](LICENSE) dosyasına bakın
