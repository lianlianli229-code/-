using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using StickyTickets.Data;
using StickyTickets.Themes;

namespace StickyTickets.UI
{
    /// <summary>
    /// Center-screen upcoming deadline notification popup that dynamically inherits
    /// the active skin theme (BurgerShop, CakeShop, ThermalPaper, HighContrast)
    /// and closes immediately when the user clicks "知道了".
    /// </summary>
    public class DeadlineReminderWindow : Window
    {
        private readonly TicketRecord _ticket;
        private readonly int _thresholdMinutes;
        private readonly int _staggerIndex;
        private string _currentSkinName;
        private ResourceDictionary _theme;
        private TextBlock _countdownText;
        private TextBlock _subHintText;

        public TicketRecord Ticket { get { return _ticket; } }
        public Action OnDismissed { get; set; }

        public DeadlineReminderWindow(TicketRecord ticket, int thresholdMinutes, string skinName, int staggerIndex = 0)
        {
            _ticket = ticket ?? new TicketRecord { TicketNo = "#001", Title = "示例待办任务" };
            _thresholdMinutes = Math.Max(1, thresholdMinutes);
            _staggerIndex = Math.Max(0, staggerIndex);
            _currentSkinName = string.IsNullOrWhiteSpace(skinName) ? SkinThemeManager.BurgerShopSkin : skinName;
            _theme = SkinThemeManager.GetThemeDictionary(_currentSkinName);

            Title = "任务快到时提醒";
            BitmapImage logoImg = SkinThemeManager.LoadEmbeddedAppLogo();
            if (logoImg != null) Icon = logoImg;

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            Width = 396;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            BuildPopupLayout();
            RefreshCountdown(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            Loaded += (s, e) => CenterOnPrimaryWorkArea();
            KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter || e.Key == Key.Escape || e.Key == Key.Space)
                {
                    AcknowledgeAndClose();
                }
            };
            Closed += (s, e) =>
            {
                if (OnDismissed != null) OnDismissed();
            };
        }

        private void CenterOnPrimaryWorkArea()
        {
            UpdateLayout();
            Rect wa = SystemParameters.WorkArea;
            double w = ActualWidth > 0 ? ActualWidth : Width;
            double h = ActualHeight > 0 ? ActualHeight : 230;
            double offset = (_staggerIndex % 5) * 22.0;
            Left = wa.Left + (wa.Width - w) / 2.0 + offset;
            Top = wa.Top + (wa.Height - h) / 2.0 + offset;
        }

        public void ApplySkin(string skinName)
        {
            string normalized = string.IsNullOrWhiteSpace(skinName) ? SkinThemeManager.BurgerShopSkin : skinName;
            if (string.Equals(_currentSkinName, normalized, StringComparison.OrdinalIgnoreCase)) return;
            _currentSkinName = normalized;
            _theme = SkinThemeManager.GetThemeDictionary(_currentSkinName);
            BuildPopupLayout();
            RefreshCountdown(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }

        public void RefreshCountdown(long nowUnix)
        {
            if (_countdownText == null) return;
            string shapeLabel;
            string remStr = _ticket.FormatRemainingTime(nowUnix, out shapeLabel);
            _countdownText.Text = "⏳ " + remStr;

            if (_subHintText != null)
            {
                long remSec = _ticket.DeadlineUnix - nowUnix;
                if (remSec <= 0)
                {
                    _subHintText.Text = "该任务目前已到截止时间，请尽快交单或加时处理！";
                }
                else
                {
                    _subHintText.Text = string.Format("该任务距离截止已不足 {0} 分钟（当前剩余 {1}），请注意把控进度！", _thresholdMinutes, remStr);
                }
            }
        }

        private void AcknowledgeAndClose()
        {
            try { Close(); } catch { }
        }

        private Brush GetBrush(string key, string fallbackHex)
        {
            if (_theme != null && _theme.Contains(key) && _theme[key] is Brush) return (Brush)_theme[key];
            return SkinThemeManager.BrushFromHex(fallbackHex);
        }

        private void BuildPopupLayout()
        {
            bool isCake = SkinThemeManager.IsCakeShop(_currentSkinName);
            bool isCoffee = SkinThemeManager.IsCoffeeShop(_currentSkinName);
            bool isThermal = SkinThemeManager.IsThermalPaper(_currentSkinName);
            bool isVintage = SkinThemeManager.IsVintageRetro(_currentSkinName);
            bool isHighContrast = string.Equals(_currentSkinName, SkinThemeManager.HighContrastSkin, StringComparison.OrdinalIgnoreCase);

            Grid shadowWrapper = new Grid
            {
                Margin = new Thickness(12),
                Effect = new DropShadowEffect
                {
                    BlurRadius = 18,
                    ShadowDepth = 4,
                    Direction = 270,
                    Opacity = 0.28,
                    Color = Colors.Black
                }
            };

            StackPanel outerStack = new StackPanel();
            if (isThermal)
            {
                outerStack.Children.Add(BuildThermalPerforatedEdge(true));
            }
            else if (isVintage)
            {
                outerStack.Children.Add(BuildVintageSawtoothEdge(true));
            }

            Border outerFrame = new Border
            {
                Background = GetBrush("PaperBgBrush", "#FFFDF7"),
                BorderBrush = GetBrush("CheeseGoldBrush", "#F5B731"),
                BorderThickness = isThermal ? new Thickness(1, 0, 1, 0) : (isVintage ? new Thickness(2, 0, 2, 0) : ((isCake || isCoffee) ? new Thickness(2) : new Thickness(3.5))),
                CornerRadius = (isThermal || isVintage) ? new CornerRadius(0) : (isCoffee ? new CornerRadius(18) : new CornerRadius(16)),
                Padding = (isThermal || isVintage) ? new Thickness(8, 2, 8, 12) : ((isCake || isCoffee) ? new Thickness(0, 0, 0, 14) : new Thickness(8, 6, 8, 14))
            };

            StackPanel body = new StackPanel();

            // 1. Theme-matched Header Banner
            if (isCake)
            {
                body.Children.Add(BuildCakeReminderBanner());
            }
            else if (isCoffee)
            {
                body.Children.Add(BuildCoffeeReminderBanner());
            }
            else if (isThermal)
            {
                body.Children.Add(BuildThermalReminderBanner());
            }
            else if (isVintage)
            {
                body.Children.Add(BuildVintageReminderBanner());
            }
            else
            {
                if (!isHighContrast)
                {
                    body.Children.Add(BuildBurgerAwning());
                }
                body.Children.Add(BuildBurgerOrContrastBanner(isHighContrast));
            }

            // 2. Center Message Card showing TicketNo, Title, and Remaining Lead Time
            Border detailCard = new Border
            {
                Background = isThermal
                    ? SkinThemeManager.BrushFromHex("#ECEDF0")
                    : (isVintage ? SkinThemeManager.BrushFromHex("#FAF3E0") : (isCake ? SkinThemeManager.BrushFromHex("#FFFBFD") : GetBrush("CreamWhiteBrush", "#FFF6E5"))),
                BorderBrush = SkinThemeManager.BrushFromHex(
                    isCake ? "#EFE2E7" : (isCoffee ? "#E2C6A4" : (isThermal ? "#CBD5E1" : (isVintage ? "#C8B28A" : (isHighContrast ? "#111827" : "#FDE68A"))))),
                BorderThickness = new Thickness(1.2),
                CornerRadius = new CornerRadius((isThermal || isVintage) ? 4 : 10),
                Padding = new Thickness(14, 12, 14, 12),
                Margin = (isCake || isCoffee) ? new Thickness(14, 10, 14, 12) : new Thickness(6, 6, 6, 12)
            };

            StackPanel detailStack = new StackPanel();

            StackPanel titleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            Border badge = new Border
            {
                Background = SkinThemeManager.BrushFromHex(
                    isCake ? "#F9C6D6" : (isCoffee ? "#FFE7BD" : (isThermal ? "#1E293B" : (isVintage ? "#5C3317" : (isHighContrast ? "#FACC15" : "#FEF3C7"))))),
                BorderBrush = SkinThemeManager.BrushFromHex(
                    isCake ? "#F48FB1" : (isCoffee ? "#C68B51" : (isThermal ? "#0F172A" : (isVintage ? "#C59329" : (isHighContrast ? "#000000" : "#F59E0B"))))),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            badge.Child = new TextBlock
            {
                Text = _ticket.TicketNo,
                Foreground = SkinThemeManager.BrushFromHex(
                    isCake ? "#6D1A36" : (isCoffee ? "#673017" : (isThermal ? "#FFFFFF" : (isVintage ? "#FDE68A" : (isHighContrast ? "#000000" : "#92400E"))))),
                FontFamily = isThermal
                    ? new FontFamily("Consolas, Microsoft YaHei UI")
                    : new FontFamily("Microsoft YaHei UI"),
                FontWeight = FontWeights.Bold,
                FontSize = 12
            };
            titleRow.Children.Add(badge);

            titleRow.Children.Add(new TextBlock
            {
                Text = _ticket.Title,
                Foreground = GetBrush("PrimaryTextBrush", "#292524"),
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontWeight = FontWeights.ExtraBold,
                FontSize = 15,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 265,
                VerticalAlignment = VerticalAlignment.Center
            });
            detailStack.Children.Add(titleRow);

            if (isThermal || isVintage)
            {
                detailStack.Children.Add(new Line
                {
                    X1 = 0,
                    Y1 = 0,
                    X2 = 310,
                    Y2 = 0,
                    Stroke = SkinThemeManager.BrushFromHex(isVintage ? "#8BAAC9" : "#94A3B8"),
                    StrokeThickness = 1.1,
                    StrokeDashArray = isVintage ? null : new DoubleCollection { 3, 2.5 },
                    Margin = new Thickness(0, 2, 0, 6)
                });
            }

            _subHintText = new TextBlock
            {
                Text = string.Format("该任务距离截止已不足 {0} 分钟，请注意把控进度！", _thresholdMinutes),
                Foreground = GetBrush("SubtleTextBrush", "#57534E"),
                FontSize = 12.5,
                LineHeight = 19,
                TextWrapping = TextWrapping.Wrap
            };
            detailStack.Children.Add(_subHintText);

            detailCard.Child = detailStack;
            body.Children.Add(detailCard);

            // 3. Prominent "知道了" Button matching the active theme
            Border okBtnBorder = new Border
            {
                Background = SkinThemeManager.BrushFromHex(
                    isCake ? "#DB2777" : (isCoffee ? "#6D3020" : (isThermal ? "#1E293B" : (isVintage ? "#7C2D12" : (isHighContrast ? "#111827" : "#D6372B"))))),
                BorderBrush = SkinThemeManager.BrushFromHex(
                    isCake ? "#F472B6" : (isCoffee ? "#FFE7BF" : (isThermal ? "#475569" : (isVintage ? "#C59329" : (isHighContrast ? "#FACC15" : "#F5B731"))))),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius((isThermal || isVintage) ? 5 : (isCake ? 16 : (isCoffee ? 12 : 9))),
                Padding = new Thickness(34, 7, 34, 7),
                HorizontalAlignment = HorizontalAlignment.Center,
                Cursor = Cursors.Hand
            };
            okBtnBorder.Child = new TextBlock
            {
                Text = "知道了",
                Foreground = isHighContrast ? SkinThemeManager.BrushFromHex("#FACC15") : (isVintage ? SkinThemeManager.BrushFromHex("#FEF3C7") : Brushes.White),
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            okBtnBorder.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                AcknowledgeAndClose();
            };
            body.Children.Add(okBtnBorder);

            outerFrame.Child = body;
            outerStack.Children.Add(outerFrame);

            if (isThermal)
            {
                outerStack.Children.Add(BuildThermalPerforatedEdge(false));
            }
            else if (isVintage)
            {
                outerStack.Children.Add(BuildVintageSawtoothEdge(false));
            }

            shadowWrapper.Children.Add(outerStack);
            Content = shadowWrapper;
        }

        private UIElement BuildBurgerAwning()
        {
            Grid awningGrid = new Grid { Height = 14, ClipToBounds = true, Margin = new Thickness(4, 0, 4, -3) };
            StackPanel stripes = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            string[] colors = { "#D6372B", "#F08A24", "#FFF6E5", "#D6372B", "#F08A24", "#FFF6E5", "#D6372B", "#F08A24", "#FFF6E5", "#D6372B", "#F08A24" };
            for (int i = 0; i < colors.Length; i++)
            {
                Polygon stripe = new Polygon
                {
                    Fill = SkinThemeManager.BrushFromHex(colors[i]),
                    Points = new PointCollection { new Point(0, 14), new Point(9, 0), new Point(40, 0), new Point(31, 14) },
                    Margin = new Thickness(-3, 0, 0, 0)
                };
                stripes.Children.Add(stripe);
            }
            awningGrid.Children.Add(stripes);
            return awningGrid;
        }

        private UIElement BuildBurgerOrContrastBanner(bool isHighContrast)
        {
            Border banner = new Border
            {
                Background = GetBrush("TomatoRedBrush", "#D6372B"),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14, 9, 12, 10),
                Margin = new Thickness(4, 0, 4, 4),
                Cursor = Cursors.SizeAll
            };
            banner.MouseLeftButtonDown += OnDragHeader;

            Grid fg = new Grid();
            fg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel leftCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            leftCol.Children.Add(new TextBlock
            {
                Text = "⏰ 任务快到时提醒",
                Foreground = isHighContrast ? SkinThemeManager.BrushFromHex("#FACC15") : Brushes.White,
                FontWeight = FontWeights.ExtraBold,
                FontSize = 15
            });
            Grid.SetColumn(leftCol, 0);
            fg.Children.Add(leftCol);

            Border cdPlaque = new Border
            {
                Background = GetBrush("DarkRedBrush", "#7A1F1A"),
                BorderBrush = SkinThemeManager.BrushFromHex(isHighContrast ? "#FACC15" : "#F08A24"),
                BorderThickness = new Thickness(1.2),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(9, 4, 9, 4),
                VerticalAlignment = VerticalAlignment.Center
            };
            _countdownText = new TextBlock
            {
                Text = "⏳ 30:00",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 13
            };
            cdPlaque.Child = _countdownText;
            Grid.SetColumn(cdPlaque, 1);
            fg.Children.Add(cdPlaque);

            banner.Child = fg;
            return banner;
        }

        private UIElement BuildCakeReminderBanner()
        {
            LinearGradientBrush heroGrad = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.5),
                EndPoint = new Point(1, 0.5)
            };
            heroGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#F472B6"), 0.0));
            heroGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FDA4AF"), 0.5));
            heroGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FDE68A"), 1.0));

            Border banner = new Border
            {
                Background = heroGrad,
                CornerRadius = new CornerRadius(14, 14, 0, 0),
                Padding = new Thickness(16, 11, 14, 11),
                Margin = new Thickness(0, 0, 0, 2),
                Cursor = Cursors.SizeAll
            };
            banner.MouseLeftButtonDown += OnDragHeader;

            Grid fg = new Grid();
            fg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock headerTitle = new TextBlock
            {
                Text = "⏰ 任务快到时提醒",
                Foreground = SkinThemeManager.BrushFromHex("#4C0519"),
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontWeight = FontWeights.ExtraBold,
                FontSize = 15,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(headerTitle, 0);
            fg.Children.Add(headerTitle);

            Border cdPlaque = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(46, 0, 0, 0)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(9, 4, 9, 4),
                VerticalAlignment = VerticalAlignment.Center
            };
            _countdownText = new TextBlock
            {
                Text = "⏳ 30:00",
                Foreground = SkinThemeManager.BrushFromHex("#4C0519"),
                FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI"),
                FontWeight = FontWeights.Bold,
                FontSize = 13
            };
            cdPlaque.Child = _countdownText;
            Grid.SetColumn(cdPlaque, 1);
            fg.Children.Add(cdPlaque);

            banner.Child = fg;
            return banner;
        }

        private UIElement BuildCoffeeReminderBanner()
        {
            Border banner = new Border
            {
                Background = new LinearGradientBrush(
                    (Color)ColorConverter.ConvertFromString("#8E4524"),
                    (Color)ColorConverter.ConvertFromString("#6D351E"),
                    90),
                BorderBrush = SkinThemeManager.BrushFromHex("#94502B"),
                BorderThickness = new Thickness(0, 0, 0, 1.5),
                CornerRadius = new CornerRadius(16, 16, 0, 0),
                Padding = new Thickness(16, 11, 14, 11),
                Margin = new Thickness(0, 0, 0, 2),
                Cursor = Cursors.SizeAll
            };
            banner.MouseLeftButtonDown += OnDragHeader;

            Grid fg = new Grid();
            fg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock headerTitle = new TextBlock
            {
                Text = "☕ 任务快到时提醒",
                Foreground = SkinThemeManager.BrushFromHex("#FFF8E9"),
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontWeight = FontWeights.ExtraBold,
                FontSize = 15,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(headerTitle, 0);
            fg.Children.Add(headerTitle);

            Border cdPlaque = new Border
            {
                Background = SkinThemeManager.BrushFromHex("#6D3020"),
                BorderBrush = SkinThemeManager.BrushFromHex("#FFE7BF"),
                BorderThickness = new Thickness(1.8),
                CornerRadius = new CornerRadius(13),
                Padding = new Thickness(10, 4, 10, 4),
                VerticalAlignment = VerticalAlignment.Center
            };
            _countdownText = new TextBlock
            {
                Text = "⏳ 30:00",
                Foreground = SkinThemeManager.BrushFromHex("#FFF8E9"),
                FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI"),
                FontWeight = FontWeights.Bold,
                FontSize = 13
            };
            cdPlaque.Child = _countdownText;
            Grid.SetColumn(cdPlaque, 1);
            fg.Children.Add(cdPlaque);

            banner.Child = fg;
            return banner;
        }

        private UIElement BuildThermalReminderBanner()
        {
            Border banner = new Border
            {
                Background = new LinearGradientBrush(
                    (Color)ColorConverter.ConvertFromString("#1E293B"),
                    (Color)ColorConverter.ConvertFromString("#0F172A"),
                    90),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(14, 9, 12, 9),
                Margin = new Thickness(0, 4, 0, 4),
                Cursor = Cursors.SizeAll
            };
            banner.MouseLeftButtonDown += OnDragHeader;

            Grid fg = new Grid();
            fg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock headerTitle = new TextBlock
            {
                Text = "[!] 任务快到时提醒",
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Consolas, Microsoft YaHei UI"),
                FontWeight = FontWeights.Bold,
                FontSize = 14.5,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(headerTitle, 0);
            fg.Children.Add(headerTitle);

            Border cdPlaque = new Border
            {
                Background = SkinThemeManager.BrushFromHex("#0F172A"),
                BorderBrush = SkinThemeManager.BrushFromHex("#475569"),
                BorderThickness = new Thickness(1.2),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(9, 4, 9, 4),
                VerticalAlignment = VerticalAlignment.Center
            };
            _countdownText = new TextBlock
            {
                Text = "⏳ 30:00",
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Consolas, Microsoft YaHei UI"),
                FontWeight = FontWeights.Bold,
                FontSize = 13
            };
            cdPlaque.Child = _countdownText;
            Grid.SetColumn(cdPlaque, 1);
            fg.Children.Add(cdPlaque);

            banner.Child = fg;
            return banner;
        }

        private UIElement BuildThermalPerforatedEdge(bool isTop)
        {
            Canvas edge = new Canvas
            {
                Height = 10,
                ClipToBounds = true,
                Margin = isTop ? new Thickness(0, 0, 0, -2) : new Thickness(0, -2, 0, 0),
                IsHitTestVisible = false
            };

            GeometryGroup group = new GeometryGroup { FillRule = FillRule.EvenOdd };
            group.Children.Add(new RectangleGeometry(new Rect(0, 0, 372, 10)));
            for (double x = 8; x < 368; x += 14)
            {
                double cy = isTop ? 0.0 : 10.0;
                group.Children.Add(new EllipseGeometry(new Point(x, cy), 4.2, 4.2));
            }

            edge.Children.Add(new Path
            {
                Fill = SkinThemeManager.BrushFromHex("#F4F5F7"),
                Stroke = SkinThemeManager.BrushFromHex("#CBD5E1"),
                StrokeThickness = 0.8,
                Data = group
            });
            return edge;
        }

        private UIElement BuildVintageReminderBanner()
        {
            StackPanel headerGroup = new StackPanel { Margin = new Thickness(2, 4, 2, 2) };

            Border outerLeather = new Border
            {
                Background = new LinearGradientBrush(
                    (Color)ColorConverter.ConvertFromString("#613619"),
                    (Color)ColorConverter.ConvertFromString("#3E2211"),
                    90),
                BorderBrush = SkinThemeManager.BrushFromHex("#C59329"),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(3),
                Cursor = Cursors.SizeAll
            };
            outerLeather.MouseLeftButtonDown += OnDragHeader;

            Border innerBrass = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromArgb(145, 237, 210, 126)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(10, 7, 10, 7)
            };

            Grid fg = new Grid();
            fg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock headerTitle = new TextBlock
            {
                Text = "📜 任务快到时提醒",
                Foreground = SkinThemeManager.BrushFromHex("#FFFBEB"),
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontWeight = FontWeights.ExtraBold,
                FontSize = 14.5,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(headerTitle, 0);
            fg.Children.Add(headerTitle);

            Border cdPlaque = new Border
            {
                Background = SkinThemeManager.BrushFromHex("#2A1508"),
                BorderBrush = SkinThemeManager.BrushFromHex("#D4AF37"),
                BorderThickness = new Thickness(1.3),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(9, 4, 9, 4),
                VerticalAlignment = VerticalAlignment.Center
            };
            _countdownText = new TextBlock
            {
                Text = "⏳ 30:00",
                Foreground = SkinThemeManager.BrushFromHex("#FEF3C7"),
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontWeight = FontWeights.Bold,
                FontSize = 13
            };
            cdPlaque.Child = _countdownText;
            Grid.SetColumn(cdPlaque, 1);
            fg.Children.Add(cdPlaque);

            innerBrass.Child = fg;
            outerLeather.Child = innerBrass;
            headerGroup.Children.Add(outerLeather);

            headerGroup.Children.Add(new Border
            {
                Height = 1.3,
                Background = SkinThemeManager.BrushFromHex("#B91C1C"),
                Margin = new Thickness(2, 4, 2, 0),
                Opacity = 0.75
            });
            return headerGroup;
        }

        private UIElement BuildVintageSawtoothEdge(bool isTop)
        {
            Canvas sawEdge = new Canvas
            {
                Height = 9,
                ClipToBounds = true,
                Margin = isTop ? new Thickness(0, 0, 0, -1) : new Thickness(0, -1, 0, 0),
                IsHitTestVisible = false
            };

            PointCollection pts = new PointCollection();
            double baseY = isTop ? 9.0 : 0.0;
            double tipY = isTop ? 1.0 : 8.0;
            pts.Add(new Point(0, baseY));
            for (double x = 0; x < 372; x += 12)
            {
                pts.Add(new Point(x + 6, tipY));
                pts.Add(new Point(Math.Min(372, x + 12), baseY));
            }

            sawEdge.Children.Add(new Polygon
            {
                Fill = SkinThemeManager.BrushFromHex("#F7EED7"),
                Stroke = SkinThemeManager.BrushFromHex("#B8860B"),
                StrokeThickness = 1.0,
                Points = pts
            });
            return sawEdge;
        }

        private void OnDragHeader(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                try { DragMove(); } catch { }
            }
        }
    }
}
