# VirtualADGV.WPF

Advanced virtual DataGrid for WPF with filtering, sorting, search and theming support.

## Features

- **High Performance:** Designed for large datasets (1M+ rows) using WPF virtualization.
- **Advanced Filtering:** Excel-like filtering with support for text, numbers, and dates.
- **Sorting:** Built-in sorting support.
- **Search:** Global and column-specific search with case sensitivity support.
- **Theming:** Full support for Dark and Light modes.
- **Localization:** Customizable UI strings.

## Installation

Install via NuGet:

```bash
dotnet add package VirtualADGV.WPF
```

## Usage

1. Add the namespace to your XAML:
```xml
xmlns:vadgv="clr-namespace:VirtualADGV.WPF;assembly=VirtualADGV.WPF"
```

2. Use the `VirtualDataGrid` control:
```xml
<vadgv:VirtualDataGrid x:Name="MyGrid" ItemsSource="{Binding MyDataView}" />
```

3. (Optional) Customize strings for localization:
```csharp
MyGrid.Strings.SearchPlaceholder = "Search here...";
MyGrid.Strings.Apply = "Filter Now";
```

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
