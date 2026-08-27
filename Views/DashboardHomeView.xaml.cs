using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KrushiBillERP.Data;
using KrushiBillERP.Models;
using System.Globalization;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.Legends;

namespace KrushiBillERP.Views
{
    public partial class DashboardHomeView : Page
    {
        public DashboardHomeView(User user)
        {
            InitializeComponent();
            TxtWelcome.Text = $"Welcome back, {user.FullName}";

            // initial load
            _ = LoadDashboardAsync();

            // Enable mouse wheel scrolling of parent ScrollViewer when mouse is over inner data grids
            Loaded += (_, __) =>
            {
                try
                {
                    if (GridExpiries != null) GridExpiries.PreviewMouseWheel += Child_PreviewMouseWheel;
                    if (GridLowStock != null) GridLowStock.PreviewMouseWheel += Child_PreviewMouseWheel;
                }
                catch { }
            };
        }

        private DateTime RangeStart, RangeEnd;

        private async Task LoadDashboardAsync()
        {
            ShowLoading(true);
            try
            {
                SetPeriodRange("today");
                await Task.Run(() => LoadSummary());
                await Task.Run(() => LoadCharts());
                await Task.Run(() => LoadExpiriesAndLowStock());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to load dashboard data.\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ShowLoading(false);
            }
        }

        private void ShowLoading(bool loading)
        {
            if (loading) BtnRefresh.IsEnabled = false; else BtnRefresh.IsEnabled = true;
        }

        private void SetPeriodRange(string periodTag)
        {
            var now = DateTime.Now;
            switch (periodTag)
            {
                case "today":
                    RangeStart = now.Date;
                    RangeEnd = now.Date.AddDays(1).AddTicks(-1);
                    break;
                case "week":
                    var diff = (int)now.DayOfWeek; // Sunday=0
                    var monday = now.Date.AddDays(-(diff == 0 ? 6 : diff - 1));
                    RangeStart = monday;
                    RangeEnd = monday.AddDays(7).AddTicks(-1);
                    break;
                case "month":
                    RangeStart = new DateTime(now.Year, now.Month, 1);
                    RangeEnd = RangeStart.AddMonths(1).AddTicks(-1);
                    break;
                case "year":
                    RangeStart = new DateTime(now.Year, 1, 1);
                    RangeEnd = new DateTime(now.Year, 12, 31, 23, 59, 59);
                    break;
                default:
                    RangeStart = now.Date;
                    RangeEnd = now.Date.AddDays(1).AddTicks(-1);
                    break;
            }
        }

        private void LoadSummary()
        {
            Dispatcher.Invoke(() =>
            {
                TxtTotalProducts.Text = DatabaseHelper.GetTotalProducts().ToString();
            });

            var breakdown = DatabaseHelper.GetRevenueBreakdown(RangeStart, RangeEnd);
            decimal totalShopUdhar = DatabaseHelper.GetTotalOutstandingUdhar();
            Dispatcher.Invoke(() =>
            {
                TxtSalesRevenue.Text = breakdown.Total.ToString("C2");
                TxtUdharRevenue.Text = totalShopUdhar.ToString("C2");
                TxtTotalRevenue.Text = breakdown.Total.ToString("C2");
                TxtCashAmount.Text = breakdown.Cash.ToString("C2");
                TxtOnlineAmount.Text = breakdown.Online.ToString("C2");
                TxtUdharAmount.Text = breakdown.Udhar.ToString("C2");
                TxtBreakdownTotal.Text = breakdown.Total.ToString("C2");
            });
        }

        private void LoadCharts()
        {
            string periodTag = "today";
            Dispatcher.Invoke(() =>
            {
                periodTag = (CmbPeriod.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "today";
            });

            var sales = DatabaseHelper.GetSalesSeries(RangeStart, RangeEnd, periodTag);
            var purchases = DatabaseHelper.GetPurchaseSeries(RangeStart, RangeEnd, periodTag);

            decimal FindValue(IEnumerable<(string Label, decimal Value)> list, string key)
            {
                if (list == null) return 0;
                var item = list.FirstOrDefault(s => s.Label == key || s.Label.TrimStart('0') == key.TrimStart('0'));
                return item.Label != null ? item.Value : 0;
            }

            var labels = new List<string>();
            var salesValues = new List<decimal>();
            var purchaseValues = new List<decimal>();

            if (periodTag == "today")
            {
                var slots = new[]
                {
                    ("Night / Early (00:00 - 06:00)", 0, 5),
                    ("Morning (06:00 - 12:00)", 6, 11),
                    ("Afternoon (12:00 - 17:00)", 12, 16),
                    ("Evening (17:00 - 24:00)", 17, 23)
                };

                foreach (var slot in slots)
                {
                    labels.Add(slot.Item1);
                    decimal sSum = 0, pSum = 0;
                    for (int h = slot.Item2; h <= slot.Item3; h++)
                    {
                        var key = h.ToString("00");
                        sSum += FindValue(sales, key);
                        pSum += FindValue(purchases, key);
                    }
                    salesValues.Add(sSum);
                    purchaseValues.Add(pSum);
                }
            }
            else if (periodTag == "week")
            {
                var days = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
                for (int i = 1; i <= 7; i++)
                {
                    var key = (i % 7).ToString();
                    labels.Add(days[i - 1]);
                    salesValues.Add(FindValue(sales, key));
                    purchaseValues.Add(FindValue(purchases, key));
                }
            }
            else if (periodTag == "month")
            {
                int daysInMonth = RangeEnd.Day;
                var blocks = new[]
                {
                    ("Days 1 - 5", 1, 5),
                    ("Days 6 - 10", 6, 10),
                    ("Days 11 - 15", 11, 15),
                    ("Days 16 - 20", 16, 20),
                    ("Days 21 - 25", 21, 25),
                    ($"Days 26 - {daysInMonth}", 26, daysInMonth)
                };

                foreach (var block in blocks)
                {
                    labels.Add(block.Item1);
                    decimal sSum = 0, pSum = 0;
                    for (int d = block.Item2; d <= block.Item3; d++)
                    {
                        var key = d.ToString("00");
                        sSum += FindValue(sales, key);
                        pSum += FindValue(purchases, key);
                    }
                    salesValues.Add(sSum);
                    purchaseValues.Add(pSum);
                }
            }
            else // year
            {
                var months = CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedMonthNames.Take(12).ToArray();
                for (int m = 1; m <= 12; m++)
                {
                    var key = m.ToString("00");
                    labels.Add(months[m - 1]);
                    salesValues.Add(FindValue(sales, key));
                    purchaseValues.Add(FindValue(purchases, key));
                }
            }

            Dispatcher.Invoke(() => BuildSalesPurchaseChart(labels, salesValues, purchaseValues));
        }
        private void BuildSalesPurchaseChart(List<string> labels, List<decimal> salesValues, List<decimal> purchaseValues)
        {
            var model = new PlotModel
            {
                Background = OxyColors.Transparent,
                PlotAreaBorderColor = OxyColors.Transparent
            };

            var categoryAxis = new CategoryAxis
            {
                Position = AxisPosition.Left,
                StartPosition = 1,
                EndPosition = 0,
                FontSize = 11,
                TextColor = OxyColor.Parse("#334155"),
                AxislineColor = OxyColors.Transparent,
                TicklineColor = OxyColors.Transparent,
                GapWidth = 0.4,
                IsPanEnabled = false,
                IsZoomEnabled = false
            };
            foreach (var label in labels)
                categoryAxis.Labels.Add(label);
            model.Axes.Add(categoryAxis);

            var valueAxis = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                MinimumPadding = 0,
                MaximumPadding = 0.15,
                AbsoluteMinimum = 0,
                FontSize = 11,
                TextColor = OxyColor.Parse("#64748B"),
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.Parse("#F1F5F9"),
                AxislineColor = OxyColors.Transparent,
                TicklineColor = OxyColors.Transparent
            };
            valueAxis.IsPanEnabled = false;
            valueAxis.IsZoomEnabled = false;
            model.Axes.Add(valueAxis);

            // Grouped Bar chart for Sales vs Purchase overview
            var salesSeries = new BarSeries
            {
                Title = "Sales Revenue (₹)",
                FillColor = OxyColor.Parse("#2E7D32"),
                StrokeColor = OxyColors.Transparent,
                StrokeThickness = 0,
                LabelFormatString = "₹ {0:N0}",
                LabelMargin = 6,
                FontSize = 10.5
            };
            var purchaseSeries = new BarSeries
            {
                Title = "Purchase Cost (₹)",
                FillColor = OxyColor.Parse("#0284C7"),
                StrokeColor = OxyColors.Transparent,
                StrokeThickness = 0,
                LabelFormatString = "₹ {0:N0}",
                LabelMargin = 6,
                FontSize = 10.5
            };

            for (int i = 0; i < labels.Count; i++)
            {
                salesSeries.Items.Add(new BarItem((double)salesValues[i], i));
                purchaseSeries.Items.Add(new BarItem((double)purchaseValues[i], i));
            }

            model.Series.Add(salesSeries);
            model.Series.Add(purchaseSeries);

            model.Legends.Add(new OxyPlot.Legends.Legend
            {
                LegendPosition = OxyPlot.Legends.LegendPosition.TopRight,
                LegendOrientation = LegendOrientation.Horizontal,
                LegendPlacement = LegendPlacement.Outside,
                LegendFontSize = 11,
                LegendBackground = OxyColors.Transparent,
                LegendBorder = OxyColors.Transparent
            });

            PlotSalesPurchase.Model = model;

            // Interactive pan/zoom disabled to prevent user adjustments
            PlotSalesPurchase.Controller = null;
        }

        private void LoadExpiriesAndLowStock()
        {
            var expiries = DatabaseHelper.GetExpiringProductsNextDays(15);
            var expList = expiries.Select(p => new
            {
                p.Name,
                p.BatchNo,
                p.StockQty,
                ExpiryDate = p.ExpiryDate,
                DaysRemaining = p.ExpiryDate.HasValue ? (p.ExpiryDate.Value.Date - DateTime.Now.Date).Days : (int?)null,
                ExpiryStatus = p.ExpiryDate.HasValue ? (p.ExpiryDate.Value.Date <= DateTime.Now.Date ? "Expired" : "Expires Soon") : "N/A"
            }).ToList();
            Dispatcher.Invoke(() =>
            {
                GridExpiries.ItemsSource = expList;
            });

            var low = DatabaseHelper.GetLowStockProducts();
            var lowList = low.Select(p => new
            {
                p.Name,
                p.StockQty,
                p.ReorderLevel,
                StockStatus = p.StockQty == 0 ? "Out of Stock" : "Low Stock"
            }).ToList();
            Dispatcher.Invoke(() => GridLowStock.ItemsSource = lowList);
        }

        private void CmbPeriod_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var tag = (CmbPeriod.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "today";
            SetPeriodRange(tag);
            _ = Task.Run(() => { LoadSummary(); LoadCharts(); });
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            _ = LoadDashboardAsync();
        }

        private void BtnViewExpiries_Click(object sender, RoutedEventArgs e)
        {
            try { NavigationService?.Navigate(new Views.ProductsView()); } catch { }
        }

        private void BtnViewLowStock_Click(object sender, RoutedEventArgs e)
        {
            try { NavigationService?.Navigate(new Views.ProductsView()); } catch { }
        }

        // Forward mouse wheel events from inner controls to the outer ScrollViewer so the page scrolls with mouse wheel
        private void Child_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is DependencyObject dep)
            {
                var sv = FindAncestor<ScrollViewer>(dep);
                if (sv != null)
                {
                    sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta / 3.0);
                    e.Handled = true;
                }
            }
        }

        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T correctlyTyped) return correctlyTyped;
                current = VisualTreeHelper.GetParent(current);
            }
            return default;
        }
    }
}