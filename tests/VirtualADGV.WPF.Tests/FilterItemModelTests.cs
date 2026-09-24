using System.ComponentModel;
using VirtualADGV.WPF;
using Xunit;

// FilterItemModel.SuppressNotification static; testler paralel koşarsa birbirini etkiler
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace VirtualADGV.WPF.Tests;

public class FilterItemModelTests
{
    private static (FilterItemModel Year, List<FilterItemModel> Months, List<FilterItemModel> Days) BuildTree(int months, int daysPerMonth)
    {
        var year = new FilterItemModel { Value = "2024" };
        var monthList = new List<FilterItemModel>();
        var dayList = new List<FilterItemModel>();
        for (int m = 0; m < months; m++)
        {
            var month = new FilterItemModel { Value = "M" + m, Parent = year };
            year.Children.Add(month);
            monthList.Add(month);
            for (int d = 0; d < daysPerMonth; d++)
            {
                var day = new FilterItemModel { Value = $"2024-{m + 1:00}-{d + 1:00}", Parent = month };
                month.Children.Add(day);
                dayList.Add(day);
            }
        }
        return (year, monthList, dayList);
    }

    [Fact]
    public void CheckingYear_NotifiesEveryDescendant()
    {
        // Eski kodda iç içe SuppressNotification sıfırlaması yüzünden ilk ayın günleri (ve
        // her ayın günleri) PropertyChanged almıyor, açık ağaçta checkbox'lar eski kalıyordu.
        var (year, months, days) = BuildTree(months: 3, daysPerMonth: 4);
        var notified = new HashSet<FilterItemModel>();
        foreach (var n in months.Concat(days))
            n.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(FilterItemModel.IsChecked)) notified.Add((FilterItemModel)s!); };

        year.IsChecked = false;

        Assert.All(months.Concat(days), n => Assert.False(n.IsChecked));
        Assert.All(months.Concat(days), n => Assert.Contains(n, notified));
        Assert.False(FilterItemModel.SuppressNotification);
    }

    [Fact]
    public void UncheckingOneDay_MakesAncestorsIndeterminate()
    {
        var (year, months, days) = BuildTree(months: 2, daysPerMonth: 3);

        days[0].IsChecked = false;

        Assert.Null(months[0].IsChecked);
        Assert.True(months[1].IsChecked);
        Assert.Null(year.IsChecked);
    }

    [Fact]
    public void UncheckingAllDays_UnchecksParents_AndRecheckRestores()
    {
        var (year, months, days) = BuildTree(months: 2, daysPerMonth: 2);

        foreach (var d in days) d.IsChecked = false;
        Assert.All(months, m => Assert.False(m.IsChecked));
        Assert.False(year.IsChecked);

        foreach (var d in days) d.IsChecked = true;
        Assert.All(months, m => Assert.True(m.IsChecked));
        Assert.True(year.IsChecked);
    }

    [Fact]
    public void ParentChange_RaisesNotificationOnAncestors()
    {
        var (year, months, days) = BuildTree(months: 1, daysPerMonth: 2);
        var changed = new List<string>();
        year.PropertyChanged += (s, e) => changed.Add("year");
        months[0].PropertyChanged += (s, e) => changed.Add("month");

        days[0].IsChecked = false;

        Assert.Contains("month", changed);
        Assert.Contains("year", changed);
    }

    [Fact]
    public void ExternalSuppressNotification_IsNotClobbered()
    {
        var (year, _, days) = BuildTree(months: 2, daysPerMonth: 2);
        int events = 0;
        days[0].PropertyChanged += (s, e) => events++;

        FilterItemModel.SuppressNotification = true;
        try
        {
            days[0].IsChecked = false;
            days[0].UpdateParentCheckState(); // Eski kod burada bayrağı false'a çekiyordu
            year.IsChecked = false;
            Assert.True(FilterItemModel.SuppressNotification);
            Assert.Equal(0, events);
        }
        finally
        {
            FilterItemModel.SuppressNotification = false;
        }
    }
}
