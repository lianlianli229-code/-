using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using StickyTickets.Themes;

namespace StickyTickets.UI
{
    public partial class StickyTicketWindow : Window
    {
        // ===== 类级别字段：供蛋糕店主题计时器和进度动态更新 =====
        private TextBlock _cakeNoText;
        private TextBlock _cakeTitleText;
        private TextBlock _cakeHourglassText;
        private TextBlock _cakeTimeText;
        private TextBlock _cakeStepText;
        private TextBlock _cakePercentText;
        private Ellipse _cakeStatusDot;
        private Border _cakeBarFill;
        private ColumnDefinition _cakeFillColumn;
        private ColumnDefinition _cakeEmptyColumn;

        private Brush CakeBrush(string hex)
        {
            return SkinThemeManager.BrushFromHex(hex);
        }

        private bool IsCakeTheme()
        {
            return SkinThemeManager.IsCakeShop(_currentSkinName);
        }

        private bool IsCoffeeTheme()
        {
            return SkinThemeManager.IsCoffeeShop(_currentSkinName);
        }

        private bool IsThermalTheme()
        {
            return SkinThemeManager.IsThermalPaper(_currentSkinName);
        }

        private bool IsVintageTheme()
        {
            return SkinThemeManager.IsVintageRetro(_currentSkinName);
        }

        public UIElement BuildPerforatedReceiptEdge(bool isTop)
        {
            if (IsVintageTheme())
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
                for (double x = 0; x < 344; x += 12)
                {
                    pts.Add(new Point(x + 6, tipY));
                    pts.Add(new Point(Math.Min(344, x + 12), baseY));
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

            if (!IsThermalTheme())
            {
                return new Border { Height = 0 };
            }

            Canvas edge = new Canvas
            {
                Height = 10,
                ClipToBounds = true,
                Margin = isTop ? new Thickness(0, 0, 0, -2) : new Thickness(0, -2, 0, 0),
                IsHitTestVisible = false
            };

            // Draw thermal paper strip with semicircular punch-outs along top or bottom
            GeometryGroup group = new GeometryGroup { FillRule = FillRule.EvenOdd };
            group.Children.Add(new RectangleGeometry(new Rect(0, 0, 344, 10)));
            for (double x = 8; x < 340; x += 14)
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

        private UIElement BuildTopClipPlaque()
        {
            bool isCake = IsCakeTheme();
            bool isCoffee = IsCoffeeTheme();
            bool isThermal = IsThermalTheme();
            bool isVintage = IsVintageTheme();

            Brush bgBrush;
            if (isCake)
            {
                bgBrush = SkinThemeManager.BrushFromHex("#F9A8D4");
            }
            else if (isCoffee)
            {
                bgBrush = GetBrush("ClipBgBrush", "#FFE7BD");
            }
            else if (isThermal)
            {
                bgBrush = new LinearGradientBrush(
                    (Color)ColorConverter.ConvertFromString("#E2E8F0"),
                    (Color)ColorConverter.ConvertFromString("#CBD5E1"),
                    90);
            }
            else if (isVintage)
            {
                LinearGradientBrush brassGrad = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0, 1)
                };
                brassGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#EDD27E"), 0.0));
                brassGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#C59329"), 0.55));
                brassGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#8C6214"), 1.0));
                bgBrush = brassGrad;
            }
            else
            {
                bgBrush = GetBrush("ClipBgBrush", "#F5B731");
            }

            string borderHex = isThermal
                ? "#94A3B8"
                : (isVintage ? "#5C3D06" : (isCake ? "#F472B6" : (isCoffee ? "#A65C30" : "#B45309")));

            Border clip = new Border
            {
                Background = bgBrush,
                BorderBrush = SkinThemeManager.BrushFromHex(borderHex),
                BorderThickness = (isCake || isCoffee) ? new Thickness(1, 1, 1, 0) : new Thickness(1.5),
                CornerRadius = (isCake || isCoffee)
                    ? new CornerRadius(8, 8, 0, 0)
                    : ((isThermal || isVintage) ? new CornerRadius(7, 7, 3, 3) : new CornerRadius(12, 12, 6, 6)),
                Padding = (isCake || isCoffee) ? new Thickness(15, 3, 15, 4) : new Thickness(14, 3, 14, 3),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = (isCake || isCoffee) ? new Thickness(0, 0, 0, -2) : new Thickness(0, 0, 0, -6),
                Cursor = System.Windows.Input.Cursors.SizeAll,
                ToolTip = "按住往下拖出挂单绳即可钉在桌面；推回屏幕顶部自动吸回挂单绳"
            };
            Panel.SetZIndex(clip, 10);

            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            if (isCake)
            {
                row.Children.Add(CreateMiniPaperclipIcon());
            }
            else if (isCoffee)
            {
                row.Children.Add(CreateMiniCoffeeCupIcon());
            }
            else if (isThermal)
            {
                // Metallic vertical staple/paperclip + mini receipt icon (对照参考图 media_1793261894259_2995cb1d.jpg)
                Border staple = new Border
                {
                    Width = 3.5,
                    Height = 14,
                    CornerRadius = new CornerRadius(1.5),
                    Background = SkinThemeManager.BrushFromHex("#64748B"),
                    Margin = new Thickness(0, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                row.Children.Add(staple);
                row.Children.Add(CreateMiniReceiptIcon());
            }
            else if (isVintage)
            {
                row.Children.Add(CreateRivetDot("#FEF3C7"));
                row.Children.Add(CreateMiniVintageScrollIcon());
            }
            else
            {
                row.Children.Add(CreateRivetDot("#FFFBEB"));
                row.Children.Add(CreateMiniBurgerIcon());
            }

            _clipLabel = new TextBlock
            {
                Text = _ticket.IsDockedOnRail ? "夹单位 · 展开小票" : "桌面钉住 · 上推归轨",
                Foreground = GetBrush("ClipTextBrush", isThermal ? "#1E293B" : (isVintage ? "#2C1808" : (isCake ? "#500724" : (isCoffee ? "#673017" : "#451A03")))),
                FontWeight = FontWeights.Bold,
                FontSize = 11.5,
                Margin = new Thickness(5, 0, (isCake || isCoffee) ? 0 : 5, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            row.Children.Add(_clipLabel);
            if (!isCake && !isCoffee && !isThermal) row.Children.Add(CreateRivetDot(isVintage ? "#FEF3C7" : "#FFFBEB"));

            clip.Child = row;
            clip.MouseLeftButtonDown += OnHeaderMouseDown;
            return clip;
        }

        private UIElement CreateMiniPaperclipIcon()
        {
            Canvas c = new Canvas { Width = 12, Height = 14, Margin = new Thickness(0, 0, 2, 0), VerticalAlignment = VerticalAlignment.Center };
            c.Children.Add(new Path
            {
                Stroke = SkinThemeManager.BrushFromHex("#64748B"),
                StrokeThickness = 1.35,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Data = Geometry.Parse("M 4,4.5 L 4,10.2 C 4,12.2 8.5,12.2 8.5,10.2 L 8.5,3.2 C 8.5,1.2 2.5,1.2 2.5,3.2 L 2.5,9.5")
            });
            return c;
        }

        private UIElement CreateMiniVintageScrollIcon()
        {
            Canvas c = new Canvas { Width = 14, Height = 14, Margin = new Thickness(2, 0, 2, 0) };
            c.Children.Add(new Path
            {
                Fill = SkinThemeManager.BrushFromHex("#F7EED7"),
                Stroke = SkinThemeManager.BrushFromHex("#4A2C18"),
                StrokeThickness = 1.1,
                Data = Geometry.Parse("M 2,2 L 12,2 L 12,12 L 9.5,10.8 L 7,12 L 4.5,10.8 L 2,12 Z")
            });
            c.Children.Add(new Path
            {
                Stroke = SkinThemeManager.BrushFromHex("#B91C1C"),
                StrokeThickness = 1,
                Data = Geometry.Parse("M 4.5,2 L 4.5,11")
            });
            c.Children.Add(new Path
            {
                Stroke = SkinThemeManager.BrushFromHex("#5C3317"),
                StrokeThickness = 0.9,
                Data = Geometry.Parse("M 6,5 L 10.5,5 M 6,7.8 L 10.5,7.8")
            });
            return c;
        }

        private UIElement CreateMiniReceiptIcon()
        {
            Canvas c = new Canvas { Width = 13, Height = 14, Margin = new Thickness(1, 0, 2, 0) };
            c.Children.Add(new Path
            {
                Stroke = SkinThemeManager.BrushFromHex("#475569"),
                StrokeThickness = 1.2,
                Fill = SkinThemeManager.BrushFromHex("#F8FAFC"),
                Data = Geometry.Parse("M 2,1 L 11,1 L 11,13 L 9,11.5 L 6.5,13 L 4,11.5 L 2,13 Z")
            });
            c.Children.Add(new Path
            {
                Stroke = SkinThemeManager.BrushFromHex("#64748B"),
                StrokeThickness = 1,
                Data = Geometry.Parse("M 4,4.5 L 9,4.5 M 4,7 L 9,7 M 4,9.5 L 7.5,9.5")
            });
            return c;
        }

        private UIElement CreateRivetDot(string hex)
        {
            return new Ellipse
            {
                Width = 5,
                Height = 5,
                Fill = SkinThemeManager.BrushFromHex(hex),
                Stroke = SkinThemeManager.BrushFromHex("#92400E"),
                StrokeThickness = 0.8,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 2, 0)
            };
        }

        private UIElement CreateMiniBurgerIcon()
        {
            Canvas c = new Canvas { Width = 14, Height = 14, Margin = new Thickness(3, 0, 1, 0) };
            c.Children.Add(new Path { Fill = SkinThemeManager.BrushFromHex("#D97706"), Data = Geometry.Parse("M 1,6 C 1,1 13,1 13,6 Z") });
            c.Children.Add(new Path { Fill = SkinThemeManager.BrushFromHex("#16A34A"), Data = Geometry.Parse("M 1,6 L 13,6 L 13,7.5 L 1,7.5 Z") });
            c.Children.Add(new Path { Fill = SkinThemeManager.BrushFromHex("#7F1D1D"), Data = Geometry.Parse("M 1,7.5 L 13,7.5 L 13,9.5 L 1,9.5 Z") });
            c.Children.Add(new Path { Fill = SkinThemeManager.BrushFromHex("#D97706"), Data = Geometry.Parse("M 1,9.5 L 13,9.5 C 13,12 1,12 1,9.5 Z") });
            return c;
        }

        private UIElement CreateMiniCupcakeIcon()
        {
            Canvas c = new Canvas { Width = 14, Height = 14, Margin = new Thickness(2, 0, 2, 0) };
            c.Children.Add(new Path { Fill = SkinThemeManager.BrushFromHex("#F472B6"), Data = Geometry.Parse("M 3,8 L 11,8 L 9.5,13 L 4.5,13 Z") });
            c.Children.Add(new Path { Fill = SkinThemeManager.BrushFromHex("#FFF1F2"), Stroke = SkinThemeManager.BrushFromHex("#E11D48"), StrokeThickness = 0.8, Data = Geometry.Parse("M 2.5,8 C 2,5 5,4 7,4 C 9,4 12,5 11.5,8 Z") });
            Ellipse topBerry = new Ellipse { Width = 3, Height = 3, Fill = SkinThemeManager.BrushFromHex("#E11D48") };
            Canvas.SetLeft(topBerry, 5.5); Canvas.SetTop(topBerry, 1.5);
            c.Children.Add(topBerry);
            return c;
        }

        private UIElement CreateMiniCoffeeCupIcon()
        {
            Canvas c = new Canvas { Width = 16, Height = 16, Margin = new Thickness(2, 0, 2, 0) };
            CafeLine(c, "M 5,4 Q 3,2 5,0.6 M 9,4 Q 7,2 9,0.6", "#96572D", 0.9);
            CafeShape(c, "M 2.5,5 L 12,5 L 11,11 Q 7.2,13.8 3.5,11 Z", "#FDF5E6", "#754023", 1);
            CafeShape(c, "M 12,6.5 Q 16.5,6 15,9.1 Q 14,11 11.5,10.2", "Transparent", "#754023", 1.15);
            CafeLine(c, "M 2.5,13 L 13,13", "#754023", 1.1);
            CafeEllipse(c, 5, 7.1, 4.5, 3, "#A85E31");
            return c;
        }

        private UIElement BuildAwningHeader()
        {
            if (IsCakeTheme() || IsCoffeeTheme() || IsThermalTheme() || IsVintageTheme())
            {
                return new Border { Height = 0 };
            }

            Grid awningGrid = new Grid { Height = 16, ClipToBounds = true, Margin = new Thickness(4, 2, 4, -4) };
            StackPanel stripes = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            string[] colors = { "#D6372B", "#F08A24", "#FFF6E5", "#D6372B", "#F08A24", "#FFF6E5", "#D6372B", "#F08A24", "#FFF6E5", "#D6372B" };
            for (int i = 0; i < colors.Length; i++)
            {
                Polygon stripe = new Polygon
                {
                    Fill = SkinThemeManager.BrushFromHex(colors[i]),
                    Points = new PointCollection { new Point(0, 16), new Point(10, 0), new Point(42, 0), new Point(32, 16) },
                    Margin = new Thickness(-3, 0, 0, 0)
                };
                stripes.Children.Add(stripe);
            }
            awningGrid.Children.Add(stripes);

            Ellipse leftRivet = new Ellipse { Width = 6, Height = 6, Fill = SkinThemeManager.BrushFromHex("#FDE047"), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
            Ellipse rightRivet = new Ellipse { Width = 6, Height = 6, Fill = SkinThemeManager.BrushFromHex("#FDE047"), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
            awningGrid.Children.Add(leftRivet);
            awningGrid.Children.Add(rightRivet);
            return awningGrid;
        }

        private UIElement BuildMainRedBanner()
        {
            if (IsCakeTheme())
            {
                return BuildCakeShopHeaderBanner();
            }
            if (IsCoffeeTheme())
            {
                return BuildCoffeeShopHeaderBanner();
            }
            if (IsThermalTheme())
            {
                return BuildThermalPaperHeaderBanner();
            }
            if (IsVintageTheme())
            {
                return BuildVintageRetroHeaderBanner();
            }

            Border banner = new Border
            {
                Background = GetBrush("TomatoRedBrush", "#D6372B"),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 8, 12, 10),
                Margin = new Thickness(4, 0, 4, 6),
                ClipToBounds = true,
                Cursor = System.Windows.Input.Cursors.SizeAll
            };
            banner.MouseLeftButtonDown += OnHeaderMouseDown;

            Grid layerGrid = new Grid();

            Canvas bgCanvas = new Canvas { IsHitTestVisible = false };
            int[,] seeds = { { 14, 6 }, { 42, 4 }, { 78, 8 }, { 116, 5 }, { 155, 7 }, { 190, 4 }, { 230, 8 }, { 28, 16 }, { 96, 15 }, { 140, 14 }, { 210, 15 } };
            for (int i = 0; i < seeds.GetLength(0); i++)
            {
                Ellipse seed = new Ellipse
                {
                    Width = 5,
                    Height = 2.6,
                    Fill = new SolidColorBrush(Color.FromArgb(145, 255, 246, 229)),
                    RenderTransform = new RotateTransform((i % 2 == 0) ? -22 : 18)
                };
                Canvas.SetLeft(seed, seeds[i, 0]);
                Canvas.SetTop(seed, seeds[i, 1]);
                bgCanvas.Children.Add(seed);
            }

            Canvas burgerSil = CreateBurgerSilhouetteCanvas();
            Canvas.SetLeft(burgerSil, 178);
            Canvas.SetTop(burgerSil, 6);
            bgCanvas.Children.Add(burgerSil);
            layerGrid.Children.Add(bgCanvas);

            Grid fg = BuildBannerForegroundContent(Brushes.White, SkinThemeManager.BrushFromHex("#FEF3C7"), false);
            layerGrid.Children.Add(fg);
            banner.Child = layerGrid;
            return banner;
        }

        // ===== 蛋糕店主题主表头（最初版纯净粉橘奶黄渐变 + 纸面圆球进度条） =====
        private UIElement BuildCakeShopHeaderBanner()
        {
            bool isNote = _ticket.IsStickyNote;
            StackPanel headerStack = new StackPanel
            {
                Margin = new Thickness(0, 0, 0, isNote ? 4 : 2)
            };

            LinearGradientBrush heroGrad = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.5),
                EndPoint = new Point(1, 0.5)
            };
            heroGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#F472B6"), 0.0));
            heroGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FDA4AF"), 0.5));
            heroGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FDE68A"), 1.0));

            Border hero = new Border
            {
                Background = heroGrad,
                CornerRadius = new CornerRadius(14, 14, 0, 0),
                Padding = new Thickness(16, 10, 14, 11),
                Cursor = Cursors.SizeAll
            };
            hero.MouseLeftButtonDown += OnHeaderMouseDown;

            Grid content = new Grid();
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel titlePanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };

            _cakeNoText = new TextBlock
            {
                Text = _ticket.TicketNo,
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = CakeBrush("#4C0519"),
                Opacity = 0.85
            };

            _cakeTitleText = new TextBlock
            {
                Text = _ticket.Title,
                Visibility = Visibility.Collapsed
            };

            _titleBox = new TextBox
            {
                Text = _ticket.Title,
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 15.5,
                FontWeight = FontWeights.Bold,
                Foreground = CakeBrush("#4C0519"),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                MaxLength = 60,
                Margin = new Thickness(0, 2, 0, 0),
                ToolTip = "可直接输入标题 + 时间 + 图片数量（如：活动海报设计 图片数量：15 8h），按回车自动设倒计时与积分！"
            };
            _titleBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    ApplySmartTitleBoxInput(true);
                    Keyboard.ClearFocus();
                }
            };
            _titleBox.LostFocus += (s, e) => ApplySmartTitleBoxInput(false);

            titlePanel.Children.Add(_cakeNoText);
            titlePanel.Children.Add(_titleBox);
            Grid.SetColumn(titlePanel, 0);
            content.Children.Add(titlePanel);

            Border timer = new Border
            {
                Padding = new Thickness(9, 4, 9, 4),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromArgb(46, 0, 0, 0))
            };

            StackPanel timerContent = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _cakeHourglassText = new TextBlock
            {
                Text = isNote ? "📝" : "⏳",
                FontSize = 12.5,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0)
            };

            _cakeTimeText = new TextBlock
            {
                Text = isNote ? "便签" : "24:15",
                FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI"),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = CakeBrush("#4C0519"),
                VerticalAlignment = VerticalAlignment.Center
            };

            timerContent.Children.Add(_cakeHourglassText);
            timerContent.Children.Add(_cakeTimeText);
            timer.Child = timerContent;

            Grid.SetColumn(timer, 1);
            content.Children.Add(timer);

            hero.Child = content;
            headerStack.Children.Add(hero);

            if (!isNote)
            {
                StackPanel progressPanel = new StackPanel
                {
                    Margin = new Thickness(16, 10, 16, 4)
                };

                Grid labelRow = new Grid();
                labelRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                labelRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                labelRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                _cakeStatusDot = new Ellipse
                {
                    Width = 11,
                    Height = 11,
                    Fill = CreateCakeSphereBrush("#FEF08A", "#EAB308", "#CA8A04"),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 6, 0)
                };
                Grid.SetColumn(_cakeStatusDot, 0);
                labelRow.Children.Add(_cakeStatusDot);

                _cakeStepText = new TextBlock
                {
                    Text = "临近 · 工序进度 1/4",
                    FontFamily = new FontFamily("Microsoft YaHei UI"),
                    FontSize = 12.5,
                    Foreground = CakeBrush("#6D4C5B"),
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                Grid.SetColumn(_cakeStepText, 1);
                labelRow.Children.Add(_cakeStepText);

                _cakePercentText = new TextBlock
                {
                    Text = "25%",
                    FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI"),
                    FontSize = 12.5,
                    Foreground = CakeBrush("#6D4C5B"),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0)
                };
                Grid.SetColumn(_cakePercentText, 2);
                labelRow.Children.Add(_cakePercentText);

                progressPanel.Children.Add(labelRow);

                Border track = new Border
                {
                    Height = 6,
                    CornerRadius = new CornerRadius(3),
                    Background = new SolidColorBrush(Color.FromArgb(26, 0, 0, 0)),
                    Margin = new Thickness(0, 6, 0, 0),
                    ClipToBounds = true
                };

                Grid barGrid = new Grid();
                _cakeFillColumn = new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) };
                _cakeEmptyColumn = new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) };
                barGrid.ColumnDefinitions.Add(_cakeFillColumn);
                barGrid.ColumnDefinitions.Add(_cakeEmptyColumn);

                _cakeBarFill = new Border
                {
                    Background = CakeBrush("#FBBF24"),
                    CornerRadius = new CornerRadius(3)
                };
                Grid.SetColumn(_cakeBarFill, 0);
                barGrid.Children.Add(_cakeBarFill);

                track.Child = barGrid;
                progressPanel.Children.Add(track);
                headerStack.Children.Add(progressPanel);
            }

            return headerStack;
        }

        private Brush CreateCakeSphereBrush(string lightHex, string midHex, string darkHex)
        {
            RadialGradientBrush sphere = new RadialGradientBrush
            {
                GradientOrigin = new Point(0.32, 0.30),
                Center = new Point(0.5, 0.5),
                RadiusX = 0.55,
                RadiusY = 0.55
            };
            sphere.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(lightHex), 0.0));
            sphere.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(midHex), 0.62));
            sphere.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(darkHex), 1.0));
            return sphere;
        }

        private void UpdateCakeShopHeader(
            string number,
            string title,
            string remText,
            int completed,
            int total,
            StickyTickets.Data.UrgencyState urgencyState)
        {
            if (_cakeNoText == null)
                return;

            _cakeNoText.Text = number;
            if (_cakeTitleText != null) _cakeTitleText.Text = title;
            if (_titleBox != null && !_titleBox.IsKeyboardFocusWithin)
            {
                _titleBox.Text = title;
            }

            if (_cakeTimeText != null)
            {
                _cakeTimeText.Text = remText;
            }

            string urgencyText = urgencyState == StickyTickets.Data.UrgencyState.Red ? "催单" :
                                 (urgencyState == StickyTickets.Data.UrgencyState.Orange ? "紧迫" :
                                 (urgencyState == StickyTickets.Data.UrgencyState.Yellow ? "临近" : "充裕"));

            if (_cakeStatusDot != null)
            {
                if (urgencyState == StickyTickets.Data.UrgencyState.Red)
                    _cakeStatusDot.Fill = CreateCakeSphereBrush("#FECACA", "#EF4444", "#B91C1C");
                else if (urgencyState == StickyTickets.Data.UrgencyState.Orange)
                    _cakeStatusDot.Fill = CreateCakeSphereBrush("#FED7AA", "#F97316", "#C2410C");
                else if (urgencyState == StickyTickets.Data.UrgencyState.Yellow)
                    _cakeStatusDot.Fill = CreateCakeSphereBrush("#FEF08A", "#EAB308", "#CA8A04");
                else
                    _cakeStatusDot.Fill = CreateCakeSphereBrush("#A7F3D0", "#10B981", "#059669");
            }

            if (_cakeBarFill != null)
            {
                if (urgencyState == StickyTickets.Data.UrgencyState.Red)
                    _cakeBarFill.Background = CakeBrush("#EF4444");
                else if (urgencyState == StickyTickets.Data.UrgencyState.Orange)
                    _cakeBarFill.Background = CakeBrush("#F97316");
                else if (urgencyState == StickyTickets.Data.UrgencyState.Yellow)
                    _cakeBarFill.Background = CakeBrush("#FBBF24");
                else
                    _cakeBarFill.Background = CakeBrush("#10B981");
            }

            double ratio = total <= 0
                ? (_ticket.Status == "Served" ? 1.0 : 0.0)
                : Math.Max(0, Math.Min(1, (double)completed / total));

            int percent = (int)Math.Round(ratio * 100);

            if (_cakeStepText != null)
                _cakeStepText.Text = string.Format("{0} · 工序进度 {1}/{2}", urgencyText, completed, total);

            if (_cakePercentText != null)
                _cakePercentText.Text = string.Format("{0}%", percent);

            if (_cakeFillColumn != null)
                _cakeFillColumn.Width = new GridLength(ratio, GridUnitType.Star);

            if (_cakeEmptyColumn != null)
                _cakeEmptyColumn.Width = new GridLength(1 - ratio, GridUnitType.Star);
        }

        /// <summary>
        /// Thermal Receipt Paper Header Banner matching reference image media_1793261894259_2995cb1d.jpg:
        /// - Deep slate-navy ink header (#1E293B to #0F172A)
        /// - Left diagonal receipt security stripes
        /// - Horizontal dashed line under TicketNo + vertical dashed divider before countdown box
        /// </summary>
        private UIElement BuildThermalPaperHeaderBanner()
        {
            Border banner = new Border
            {
                Background = new LinearGradientBrush(
                    (Color)ColorConverter.ConvertFromString("#1E293B"),
                    (Color)ColorConverter.ConvertFromString("#0F172A"),
                    90),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(14, 9, 12, 10),
                Margin = new Thickness(0, 0, 0, 6),
                ClipToBounds = true,
                Cursor = System.Windows.Input.Cursors.SizeAll
            };
            banner.MouseLeftButtonDown += OnHeaderMouseDown;

            Grid layerGrid = new Grid();

            // Left edge subtle diagonal receipt hash stripes
            Canvas stripesCanvas = new Canvas { IsHitTestVisible = false, Width = 16, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(-14, -9, 0, -10) };
            for (int sy = -6; sy < 72; sy += 10)
            {
                stripesCanvas.Children.Add(new Path
                {
                    Stroke = SkinThemeManager.BrushFromHex("#334155"),
                    StrokeThickness = 2.5,
                    Data = Geometry.Parse(string.Format("M 0,{0} L 10,{1}", sy + 8, sy))
                });
            }
            layerGrid.Children.Add(stripesCanvas);

            Grid fg = new Grid();
            fg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            fg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            titleStack.Children.Add(new TextBlock
            {
                Text = _ticket.TicketNo,
                Foreground = SkinThemeManager.BrushFromHex("#E2E8F0"),
                FontFamily = new FontFamily("Consolas, Microsoft YaHei UI"),
                FontWeight = FontWeights.Bold,
                FontSize = 12
            });

            // Horizontal dashed line between #TicketNo and Title
            Line hDash = new Line
            {
                X1 = 0,
                Y1 = 0,
                X2 = 195,
                Y2 = 0,
                Stroke = SkinThemeManager.BrushFromHex("#64748B"),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 3, 2.5 },
                Margin = new Thickness(0, 3, 8, 3)
            };
            titleStack.Children.Add(hDash);

            _titleBox = new TextBox
            {
                Text = _ticket.Title,
                Foreground = Brushes.White,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.ExtraBold,
                FontSize = 15.5,
                MaxLength = 60,
                Padding = new Thickness(0),
                ToolTip = "可直接输入标题 + 时间 + 图片数量（如：活动海报设计 图片数量：15 8h），按回车自动设倒计时与积分！"
            };
            _titleBox.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                {
                    ApplySmartTitleBoxInput(true);
                    System.Windows.Input.Keyboard.ClearFocus();
                }
            };
            _titleBox.LostFocus += (s, e) => ApplySmartTitleBoxInput(false);
            titleStack.Children.Add(_titleBox);
            Grid.SetColumn(titleStack, 0);
            fg.Children.Add(titleStack);

            // Vertical dashed divider line
            Line vDash = new Line
            {
                X1 = 0,
                Y1 = 2,
                X2 = 0,
                Y2 = 44,
                Stroke = SkinThemeManager.BrushFromHex("#64748B"),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 3, 2.5 },
                Margin = new Thickness(6, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(vDash, 1);
            fg.Children.Add(vDash);

            Border cdPlaque = new Border
            {
                Background = SkinThemeManager.BrushFromHex("#0F172A"),
                BorderBrush = SkinThemeManager.BrushFromHex("#475569"),
                BorderThickness = new Thickness(1.2),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 5, 10, 5),
                VerticalAlignment = VerticalAlignment.Center
            };
            _countdownText = new TextBlock
            {
                Text = "⏳ 24:15",
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Consolas, Microsoft YaHei UI"),
                FontWeight = FontWeights.Bold,
                FontSize = 13.5,
                VerticalAlignment = VerticalAlignment.Center
            };
            cdPlaque.Child = _countdownText;
            Grid.SetColumn(cdPlaque, 2);
            fg.Children.Add(cdPlaque);

            layerGrid.Children.Add(fg);
            banner.Child = layerGrid;
            return banner;
        }

        /// <summary>
        /// Skeuomorphic Vintage Diner Guest Check Header Banner (拟物复古小票主题):
        /// - Aged mahogany-espresso diner header with antique brass double frame & corner rivets
        /// - Serif / typewriter "GUEST CHECK" order header + antique brass riveted countdown plate
        /// - Classic American diner double-crimson horizontal rule lines beneath header
        /// </summary>
        private UIElement BuildVintageRetroHeaderBanner()
        {
            StackPanel headerGroup = new StackPanel { Margin = new Thickness(2, 2, 2, 4) };

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
                Cursor = System.Windows.Input.Cursors.SizeAll
            };
            outerLeather.MouseLeftButtonDown += OnHeaderMouseDown;

            Border innerBrassHairline = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromArgb(145, 237, 210, 126)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(9, 6, 9, 7)
            };

            Grid fg = new Grid();
            fg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            titleStack.Children.Add(new TextBlock
            {
                Text = _ticket.TicketNo + " · GUEST CHECK",
                Foreground = SkinThemeManager.BrushFromHex("#FDE68A"),
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontWeight = FontWeights.Bold,
                FontSize = 11.5
            });

            Line brassRule = new Line
            {
                X1 = 0,
                Y1 = 0,
                X2 = 188,
                Y2 = 0,
                Stroke = new SolidColorBrush(Color.FromArgb(130, 212, 175, 55)),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                Margin = new Thickness(0, 2.5, 8, 2.5)
            };
            titleStack.Children.Add(brassRule);

            _titleBox = new TextBox
            {
                Text = _ticket.Title,
                Foreground = SkinThemeManager.BrushFromHex("#FFFBEB"),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontWeight = FontWeights.ExtraBold,
                FontSize = 15.5,
                MaxLength = 60,
                Padding = new Thickness(0),
                ToolTip = "可直接输入标题 + 时间 + 图片数量（如：活动海报设计 图片数量：15 8h），按回车自动设倒计时与积分！"
            };
            _titleBox.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                {
                    ApplySmartTitleBoxInput(true);
                    System.Windows.Input.Keyboard.ClearFocus();
                }
            };
            _titleBox.LostFocus += (s, e) => ApplySmartTitleBoxInput(false);
            titleStack.Children.Add(_titleBox);
            Grid.SetColumn(titleStack, 0);
            fg.Children.Add(titleStack);

            Border brassPlaque = new Border
            {
                Background = SkinThemeManager.BrushFromHex("#2A1508"),
                BorderBrush = SkinThemeManager.BrushFromHex("#D4AF37"),
                BorderThickness = new Thickness(1.4),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 4, 8, 4),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            StackPanel cdRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            cdRow.Children.Add(CreateRivetDot("#FDE68A"));
            _countdownText = new TextBlock
            {
                Text = "⏳ 24:15",
                Foreground = SkinThemeManager.BrushFromHex("#FEF3C7"),
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Margin = new Thickness(3, 0, 3, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            cdRow.Children.Add(_countdownText);
            cdRow.Children.Add(CreateRivetDot("#FDE68A"));
            brassPlaque.Child = cdRow;
            Grid.SetColumn(brassPlaque, 1);
            fg.Children.Add(brassPlaque);

            innerBrassHairline.Child = fg;
            outerLeather.Child = innerBrassHairline;
            headerGroup.Children.Add(outerLeather);

            // Classic American Diner Guest-Check Double Crimson Horizontal Rule
            Border redRule1 = new Border
            {
                Height = 1.4,
                Background = SkinThemeManager.BrushFromHex("#B91C1C"),
                Margin = new Thickness(2, 4, 2, 1.5),
                Opacity = 0.78
            };
            Border redRule2 = new Border
            {
                Height = 0.9,
                Background = SkinThemeManager.BrushFromHex("#DC2626"),
                Margin = new Thickness(2, 0, 2, 0),
                Opacity = 0.65
            };
            headerGroup.Children.Add(redRule1);
            headerGroup.Children.Add(redRule2);

            return headerGroup;
        }

        private Grid BuildBannerForegroundContent(Brush titleBrush, Brush noBrush, bool isCakeStyle)
        {
            bool isCoffee = IsCoffeeTheme();
            Grid fg = new Grid();
            fg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            titleStack.Children.Add(new TextBlock
            {
                Text = _ticket.TicketNo,
                Foreground = noBrush,
                FontWeight = FontWeights.Bold,
                FontSize = isCoffee ? 12.5 : 12
            });

            _titleBox = new TextBox
            {
                Text = _ticket.Title,
                Foreground = titleBrush,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.ExtraBold,
                FontSize = isCoffee ? 18 : 15.5,
                MaxLength = 60,
                Padding = new Thickness(0),
                Margin = isCoffee ? new Thickness(0, 2, 0, 0) : new Thickness(0),
                ToolTip = "可直接输入标题 + 时间 + 图片数量（如：活动海报设计 图片数量：15 8h），按回车自动设倒计时与积分！"
            };
            _titleBox.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                {
                    ApplySmartTitleBoxInput(true);
                    System.Windows.Input.Keyboard.ClearFocus();
                }
            };
            _titleBox.LostFocus += (s, e) => ApplySmartTitleBoxInput(false);
            titleStack.Children.Add(_titleBox);
            Grid.SetColumn(titleStack, 0);
            fg.Children.Add(titleStack);

            Border cdPlaque = new Border
            {
                Background = isCoffee
                    ? SkinThemeManager.BrushFromHex("#6D3020")
                    : (isCakeStyle
                        ? (Brush)new LinearGradientBrush((Color)ColorConverter.ConvertFromString("#EAD3A8"), (Color)ColorConverter.ConvertFromString("#DEC08A"), 90)
                        : GetBrush("DarkRedBrush", "#7A1F1A")),
                BorderBrush = SkinThemeManager.BrushFromHex(isCoffee ? "#FFE7BF" : (isCakeStyle ? "#D4B275" : "#F08A24")),
                BorderThickness = new Thickness(isCoffee ? 1.8 : 1.2),
                CornerRadius = new CornerRadius(isCoffee ? 13 : (isCakeStyle ? 10 : 8)),
                Padding = isCoffee ? new Thickness(11, 5, 11, 5) : new Thickness(9, 4, 9, 4),
                VerticalAlignment = isCoffee ? VerticalAlignment.Center : VerticalAlignment.Top,
                Margin = new Thickness(8, isCoffee ? 0 : 2, 0, 0)
            };
            StackPanel cdRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            if (!isCakeStyle && !isCoffee) cdRow.Children.Add(CreateRivetDot("#FCA5A5"));
            _countdownText = new TextBlock
            {
                Text = "⏳ 24:15",
                Foreground = isCoffee
                    ? SkinThemeManager.BrushFromHex("#FFF8E9")
                    : (isCakeStyle ? SkinThemeManager.BrushFromHex("#6D1A36") : Brushes.White),
                FontWeight = FontWeights.Bold,
                FontSize = isCoffee ? 13.5 : 13,
                Margin = new Thickness(isCoffee ? 1 : 3, 0, isCoffee ? 1 : 3, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            cdRow.Children.Add(_countdownText);
            if (!isCakeStyle && !isCoffee) cdRow.Children.Add(CreateRivetDot("#FCA5A5"));
            cdPlaque.Child = cdRow;
            Grid.SetColumn(cdPlaque, 1);
            fg.Children.Add(cdPlaque);

            return fg;
        }

        // ===== 咖啡店主题主表头：紧凑奶咖墙面 + 右下微笑拿铁杯（已去除顶部 COFFEE 门头并大幅缩矮高度） =====
        private UIElement BuildCoffeeShopHeaderBanner()
        {
            Border banner = new Border
            {
                Height = 96,
                Margin = new Thickness(0, 0, 0, 4),
                Background = SkinThemeManager.BrushFromHex("#FFF6E7"),
                BorderBrush = SkinThemeManager.BrushFromHex("#DEC09A"),
                BorderThickness = new Thickness(0, 0, 0, 1.2),
                CornerRadius = new CornerRadius(16, 16, 0, 0),
                ClipToBounds = true,
                Cursor = Cursors.SizeAll
            };
            banner.MouseLeftButtonDown += OnHeaderMouseDown;

            Grid scene = new Grid { ClipToBounds = true };

            Viewbox back = new Viewbox
            {
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Stretch = Stretch.Fill,
                IsHitTestVisible = false
            };
            Canvas backCanvas = new Canvas { Width = 440, Height = 96 };
            CafeShape(backCanvas,
                "M 0,0 L 440,0 L 440,22 C 330,16 260,30 175,24 C 105,19 52,28 0,20 Z",
                "#F8DFB6");
            CafeShape(backCanvas,
                "M 0,80 Q 90,87 175,82 T 330,85 T 440,81 L 440,96 L 0,96 Z",
                "#F5E0BC");
            back.Child = backCanvas;
            scene.Children.Add(back);

            Viewbox leftLeaves = new Viewbox
            {
                Width = 22,
                Height = 32,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0),
                Stretch = Stretch.Fill,
                IsHitTestVisible = false
            };
            leftLeaves.Child = CreateCoffeeShopLeavesArt();
            scene.Children.Add(leftLeaves);

            Viewbox coffeeArt = new Viewbox
            {
                Width = 112,
                Height = 36,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 6, 1),
                Stretch = Stretch.Fill,
                IsHitTestVisible = false
            };
            coffeeArt.Child = CreateCoffeeShopCounterArt();
            scene.Children.Add(coffeeArt);

            Grid fg = BuildBannerForegroundContent(
                SkinThemeManager.BrushFromHex("#542819"),
                SkinThemeManager.BrushFromHex("#63301D"),
                isCakeStyle: false);
            fg.Margin = new Thickness(14, 9, 10, 39);
            fg.VerticalAlignment = VerticalAlignment.Stretch;
            fg.HorizontalAlignment = HorizontalAlignment.Stretch;
            Panel.SetZIndex(fg, 6);
            scene.Children.Add(fg);

            banner.Child = scene;
            return banner;
        }

        private Canvas CreateCoffeeShopAwningArt()
        {
            Canvas c = new Canvas { Width = 440, Height = 77 };

            CafeShape(c,
                "M 0,0 L 440,0 L 440,60 Q 220,75 0,60 Z",
                "#71371D", "#542714", 1);

            for (int i = 0; i < 8; i++)
            {
                Path stripe = new Path
                {
                    Data = Geometry.Parse(
                        "M 0,8 L 55,8 L 55,53 Q 55,68 41,68 Q 27.5,68 27.5,56 " +
                        "Q 27.5,68 14,68 Q 0,68 0,53 Z"),
                    Fill = SkinThemeManager.BrushFromHex(i % 2 == 0 ? "#8E4524" : "#FFE7BC"),
                    Stroke = SkinThemeManager.BrushFromHex("#6D351E"),
                    StrokeThickness = 1
                };
                Canvas.SetLeft(stripe, i * 55);
                c.Children.Add(stripe);
            }

            CafeShape(c, "M 0,5 L 440,5", "Transparent", "#B97B48", 2);

            Border logo = new Border
            {
                Width = 156,
                Height = 46,
                CornerRadius = new CornerRadius(11),
                Background = SkinThemeManager.BrushFromHex("#FFE8C0"),
                BorderBrush = SkinThemeManager.BrushFromHex("#A65C30"),
                BorderThickness = new Thickness(2)
            };
            StackPanel logoContent = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            logoContent.Children.Add(new TextBlock
            {
                Text = "☕",
                FontSize = 22,
                Margin = new Thickness(0, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            logoContent.Children.Add(new TextBlock
            {
                Text = "COFFEE",
                FontSize = 22,
                FontWeight = FontWeights.ExtraBold,
                FontFamily = new FontFamily("Segoe UI"),
                Foreground = SkinThemeManager.BrushFromHex("#673018"),
                VerticalAlignment = VerticalAlignment.Center
            });
            logo.Child = logoContent;
            Canvas.SetLeft(logo, 142);
            Canvas.SetTop(logo, 3);
            c.Children.Add(logo);

            return c;
        }

        private Canvas CreateCoffeeShopCounterArt()
        {
            Canvas c = new Canvas { Width = 165, Height = 62 };

            CafeLine(c, "M 145,56 Q 151,35 158,22", "#607F3A", 2);
            CafeShape(c, "M 151,38 Q 156,20 164,23 Q 163,38 151,38 Z", "#7F9C4B", "#527338", 1);
            CafeShape(c, "M 148,48 Q 137,32 132,39 Q 133,49 148,48 Z", "#8DB15A", "#567538", 1);

            CafeShape(c, "M 7,12 Q 7,8 11,8 L 57,8 Q 61,8 61,12 L 61,57 L 7,57 Z", "#B77739", "#74401F", 2);
            CafeShape(c, "M 12,13 L 56,13 L 56,49 L 12,49 Z", "#33372C", "#6B462B", 1);
            TextBlock boardText = new TextBlock
            {
                Text = "Coffee\n  ~",
                FontFamily = new FontFamily("Segoe Print, Microsoft YaHei UI"),
                FontSize = 12,
                Foreground = SkinThemeManager.BrushFromHex("#FFF1D2"),
                TextAlignment = TextAlignment.Center,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(boardText, 17);
            Canvas.SetTop(boardText, 17);
            c.Children.Add(boardText);
            CafeLine(c, "M 19,56 L 14,61 M 48,56 L 54,61", "#74401F", 2.5);

            CafeEllipse(c, 74, 53, 70, 7, "#CFA06E", "#7B482A", 0.8);
            CafeShape(c, "M 126,24 C 146,17 150,42 128,43", "Transparent", "#86522E", 5);
            CafeShape(c, "M 85,22 L 131,22 L 127,52 Q 107,59 89,52 Z", "#FFF8E8", "#8B512D", 2);
            CafeEllipse(c, 85, 19, 46, 10, "#BA794B", "#8B512D", 1);
            CafeShape(c, "M 94,24 Q 107,17 117,25 Q 107,31 94,24 Z", "#FFF1D3");
            CafeEllipse(c, 98, 37, 3, 3, "#784021");
            CafeEllipse(c, 115, 37, 3, 3, "#784021");
            CafeLine(c, "M 102,44 Q 108,50 114,44", "#87492D", 1.5);
            CafeEllipse(c, 93, 43, 3.2, 2.3, "#F3B49F");
            CafeEllipse(c, 119, 43, 3.2, 2.3, "#F3B49F");

            CafeShape(c,
                "M 89,21 Q 85,13 97,12 Q 98,4 106,5 Q 114,2 117,11 " +
                "Q 128,10 128,21 Z", "#FFFDF6", "#E6C7A3", 0.8);
            CafeLine(c, "M 99,2 Q 94,-2 99,-7 M 114,2 Q 110,-3 115,-7", "#D9B28A", 1.3);

            return c;
        }

        private Canvas CreateCoffeeShopLeavesArt()
        {
            Canvas c = new Canvas { Width = 36, Height = 54 };
            CafeLine(c, "M 9,54 Q 20,30 14,0", "#668443", 2);
            CafeShape(c, "M 15,23 Q -3,14 2,4 Q 19,7 15,23 Z", "#93B158", "#597638", 1.2);
            CafeShape(c, "M 15,34 Q 30,18 35,26 Q 28,37 15,34 Z", "#82A44D", "#597638", 1.2);
            CafeShape(c, "M 12,47 Q -3,31 1,25 Q 17,30 12,47 Z", "#96B75E", "#597638", 1.2);
            return c;
        }

        private static void CafeShape(Canvas c, string data, string fill,
            string stroke = null, double strokeWidth = 1)
        {
            c.Children.Add(new Path
            {
                Data = Geometry.Parse(data),
                Fill = fill == "Transparent" ? Brushes.Transparent : SkinThemeManager.BrushFromHex(fill),
                Stroke = stroke == null ? null : SkinThemeManager.BrushFromHex(stroke),
                StrokeThickness = strokeWidth,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            });
        }

        private static void CafeLine(Canvas c, string data, string color, double width)
        {
            CafeShape(c, data, "Transparent", color, width);
        }

        private static void CafeEllipse(Canvas c, double x, double y, double width, double height,
            string fill, string stroke = null, double strokeWidth = 1)
        {
            Ellipse e = new Ellipse
            {
                Width = width,
                Height = height,
                Fill = SkinThemeManager.BrushFromHex(fill),
                Stroke = stroke == null ? null : SkinThemeManager.BrushFromHex(stroke),
                StrokeThickness = strokeWidth
            };
            Canvas.SetLeft(e, x);
            Canvas.SetTop(e, y);
            c.Children.Add(e);
        }

        private Canvas CreateBurgerSilhouetteCanvas()
        {
            Canvas c = new Canvas { Width = 64, Height = 46, Opacity = 0.36 };
            Brush orange = GetBrush("WarmOrangeBrush", "#F08A24");
            c.Children.Add(new Path { Fill = orange, Data = Geometry.Parse("M 4,20 C 4,2 60,2 60,20 Z") });
            c.Children.Add(new Path { Fill = orange, Data = Geometry.Parse("M 2,23 Q 16,20 32,23 Q 48,26 62,23 L 62,27 L 2,27 Z") });
            c.Children.Add(new Path { Fill = orange, Data = Geometry.Parse("M 4,30 L 60,30 C 60,40 4,40 4,30 Z") });
            return c;
        }
    }
}
