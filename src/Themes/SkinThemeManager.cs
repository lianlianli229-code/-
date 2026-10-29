using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace StickyTickets.Themes
{
    /// <summary>
    /// ResourceDictionary-based SkinTheme architecture supporting BurgerShop, CakeShop, and HighContrast.
    /// SECURITY: Strictly uses compiled in-memory ResourceDictionaries; never calls XamlReader.Load on external files.
    /// </summary>
    public static class SkinThemeManager
    {
        public const string BurgerShopSkin = "BurgerShop";
        public const string CakeShopSkin = "CakeShop";
        public const string CoffeeShopSkin = "CoffeeShop";
        public const string ThermalPaperSkin = "ThermalPaper";
        public const string VintageRetroSkin = "VintageRetro";
        public const string HighContrastSkin = "HighContrast";

        public static bool IsCakeShop(string skinName)
        {
            return string.Equals(skinName, CakeShopSkin, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsCoffeeShop(string skinName)
        {
            return !string.IsNullOrWhiteSpace(skinName) &&
                   (skinName.IndexOf("咖啡", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    skinName.IndexOf("coffee", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public static bool IsThermalPaper(string skinName)
        {
            return string.Equals(skinName, ThermalPaperSkin, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsVintageRetro(string skinName)
        {
            return string.Equals(skinName, VintageRetroSkin, StringComparison.OrdinalIgnoreCase);
        }

        public static string NextThemeSkin(string currentSkin)
        {
            if (IsCakeShop(currentSkin)) return CoffeeShopSkin;
            if (IsCoffeeShop(currentSkin)) return ThermalPaperSkin;
            if (IsThermalPaper(currentSkin)) return VintageRetroSkin;
            if (IsVintageRetro(currentSkin)) return BurgerShopSkin;
            return CakeShopSkin;
        }

        public static ResourceDictionary GetThemeDictionary(string skinName)
        {
            ResourceDictionary dict = new ResourceDictionary();
            if (IsCakeShop(skinName))
            {
                // Original Cake Shop Theme (对照最初版参考图 media_1793287634403_9b278560.png)
                dict["TomatoRedBrush"] = BrushFromHex("#F472B6");
                dict["CheeseGoldBrush"] = BrushFromHex("#F9A8D4");
                dict["DarkRedBrush"] = BrushFromHex("#D6BA73");
                dict["CreamWhiteBrush"] = BrushFromHex("#FFF5F8");
                dict["WarmOrangeBrush"] = BrushFromHex("#FDA4AF");
                dict["PaperBgBrush"] = BrushFromHex("#FFF5F8");
                dict["PrimaryTextBrush"] = BrushFromHex("#4A2535");
                dict["SubtleTextBrush"] = BrushFromHex("#7A5564");
                dict["ClipBgBrush"] = BrushFromHex("#F9A8D4");
                dict["ClipTextBrush"] = BrushFromHex("#500724");
            }
            else if (IsCoffeeShop(skinName))
            {
                dict["TomatoRedBrush"] = BrushFromHex("#FFF4E5");   // 香草奶咖主色
                dict["CheeseGoldBrush"] = BrushFromHex("#C68B51");  // 焦糖描边
                dict["DarkRedBrush"] = BrushFromHex("#69311F");     // 浓缩咖啡深棕（计时器）
                dict["CreamWhiteBrush"] = BrushFromHex("#FFFCF5");  // 奶油色卡片
                dict["WarmOrangeBrush"] = BrushFromHex("#D58A47");  // 琥珀焦糖
                dict["PaperBgBrush"] = BrushFromHex("#FFF8EA");     // 小票背景
                dict["PrimaryTextBrush"] = BrushFromHex("#512917"); // 文字深棕
                dict["SubtleTextBrush"] = BrushFromHex("#8B6A54");  // 次级文字
                dict["ClipBgBrush"] = BrushFromHex("#FFE7BD");      // 门头夹片底色
                dict["ClipTextBrush"] = BrushFromHex("#673017");    // 门头夹片文字
            }
            else if (IsThermalPaper(skinName))
            {
                // Thermal Receipt Paper Theme (对照参考图 media_1793261894259_2995cb1d.jpg)
                dict["TomatoRedBrush"] = BrushFromHex("#1E293B");
                dict["CheeseGoldBrush"] = BrushFromHex("#CBD5E1");
                dict["DarkRedBrush"] = BrushFromHex("#0F172A");
                dict["CreamWhiteBrush"] = BrushFromHex("#ECEDF0");
                dict["WarmOrangeBrush"] = BrushFromHex("#475569");
                dict["PaperBgBrush"] = BrushFromHex("#F4F5F7");
                dict["PrimaryTextBrush"] = BrushFromHex("#1E293B");
                dict["SubtleTextBrush"] = BrushFromHex("#475569");
                dict["ClipBgBrush"] = BrushFromHex("#CBD5E1");
                dict["ClipTextBrush"] = BrushFromHex("#1E293B");
            }
            else if (IsVintageRetro(skinName))
            {
                // Skeuomorphic Vintage Diner Guest Check Theme (拟物复古小票主题)
                dict["TomatoRedBrush"] = BrushFromHex("#5C3317");   // Aged mahogany leather/ink header
                dict["CheeseGoldBrush"] = BrushFromHex("#B8860B");  // Antique brushed brass border
                dict["DarkRedBrush"] = BrushFromHex("#3A1E0C");     // Deep bronze inset plaque
                dict["CreamWhiteBrush"] = BrushFromHex("#EFE2C4");  // Aged parchment sub-card
                dict["WarmOrangeBrush"] = BrushFromHex("#9A3412");  // Vintage rust accent
                dict["PaperBgBrush"] = BrushFromHex("#F7EED7");     // Yellowed retro diner guest-check paper
                dict["PrimaryTextBrush"] = BrushFromHex("#3E2C1C"); // Deep sepia fountain-pen ink
                dict["SubtleTextBrush"] = BrushFromHex("#6B4F35");  // Warm aged sepia ink
                dict["ClipBgBrush"] = BrushFromHex("#C59329");      // Vintage brass clip
                dict["ClipTextBrush"] = BrushFromHex("#2C1808");    // Engraved dark bronze text
            }
            else if (string.Equals(skinName, HighContrastSkin, StringComparison.OrdinalIgnoreCase))
            {
                dict["TomatoRedBrush"] = BrushFromHex("#111827");
                dict["CheeseGoldBrush"] = BrushFromHex("#FACC15");
                dict["DarkRedBrush"] = BrushFromHex("#000000");
                dict["CreamWhiteBrush"] = BrushFromHex("#FFFFFF");
                dict["WarmOrangeBrush"] = BrushFromHex("#F59E0B");
                dict["PaperBgBrush"] = BrushFromHex("#FFFFFF");
                dict["PrimaryTextBrush"] = BrushFromHex("#000000");
                dict["SubtleTextBrush"] = BrushFromHex("#1F2937");
                dict["ClipBgBrush"] = BrushFromHex("#FACC15");
                dict["ClipTextBrush"] = BrushFromHex("#000000");
            }
            else
            {
                // Default Burger Shop Theme design tokens
                dict["TomatoRedBrush"] = BrushFromHex("#D6372B");
                dict["CheeseGoldBrush"] = BrushFromHex("#F5B731");
                dict["DarkRedBrush"] = BrushFromHex("#7A1F1A");
                dict["CreamWhiteBrush"] = BrushFromHex("#FFF6E5");
                dict["WarmOrangeBrush"] = BrushFromHex("#F08A24");
                dict["PaperBgBrush"] = BrushFromHex("#FFFDF7");
                dict["PrimaryTextBrush"] = BrushFromHex("#292524");
                dict["SubtleTextBrush"] = BrushFromHex("#57534E");
                dict["ClipBgBrush"] = BrushFromHex("#F5B731");
                dict["ClipTextBrush"] = BrushFromHex("#451A03");
            }
            return dict;
        }

        private static System.Windows.Media.Imaging.BitmapImage _cachedAppLogo;

        public static System.Windows.Media.Imaging.BitmapImage LoadEmbeddedAppLogo()
        {
            if (_cachedAppLogo != null) return _cachedAppLogo;
            try
            {
                System.IO.Stream stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("StickyTickets.logo.png");
                if (stream != null)
                {
                    using (stream)
                    {
                        System.Windows.Media.Imaging.BitmapImage bmp = new System.Windows.Media.Imaging.BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bmp.StreamSource = stream;
                        bmp.EndInit();
                        bmp.Freeze();
                        _cachedAppLogo = bmp;
                        return _cachedAppLogo;
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        public static SolidColorBrush BrushFromHex(string hex)
        {
            Color c = (Color)ColorConverter.ConvertFromString(hex);
            SolidColorBrush b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        public static string GetStepTooltip(string stepType, string skinName = BurgerShopSkin)
        {
            if (IsCakeShop(skinName))
            {
                switch (stepType)
                {
                    case "prep": return "🍰 蛋糕胚 (前置准备/搜集) — 点击切换类型";
                    case "comm": return "🍦 奶油甜筒 (沟通联络/交付) — 点击切换类型";
                    case "warn": return "✨ 闪耀糖星 (加急检查/注意) — 点击切换类型";
                    default: return "🍓 C位鲜草莓 (核心主工序) — 点击切换类型";
                }
            }
            if (IsCoffeeShop(skinName))
            {
                switch (stepType)
                {
                    case "prep": return "📋 备料清单（前置准备/搜集）— 点击切换类型";
                    case "comm": return "🧾 点单便签（沟通联络/交付）— 点击切换类型";
                    case "warn": return "☕ 提醒铃（加急检查/注意）— 点击切换类型";
                    default: return "🫘 精品咖啡豆（核心主工序）— 点击切换类型";
                }
            }
            if (IsThermalPaper(skinName))
            {
                switch (stepType)
                {
                    case "prep": return "[+] 前置准备/搜集 — 点击切换类型";
                    case "comm": return "[@] 沟通联络/交付 — 点击切换类型";
                    case "warn": return "[!] 重点检查/注意 — 点击切换类型";
                    default: return "[★] 核心主工序 — 点击切换类型";
                }
            }
            if (IsVintageRetro(skinName))
            {
                switch (stepType)
                {
                    case "prep": return "🟢 复古绿点 (前置准备/搜集) — 点击切换类型";
                    case "comm": return "🟡 琥珀黄点 (沟通联络/交付) — 点击切换类型";
                    case "warn": return "❗ 朱红加急印记 (重点检查/注意) — 点击切换类型";
                    default: return "🔴 朱砂红点 (核心主工序) — 点击切换类型";
                }
            }
            switch (stepType)
            {
                case "prep": return "🥬 生菜 (前置准备/搜集) — 点击切换类型";
                case "comm": return "🧀 芝士 (沟通联络/交付) — 点击切换类型";
                case "warn": return "🌶️ 辣椒 (加急检查/注意) — 点击切换类型";
                default: return "🥩 肉饼 (核心主工序) — 点击切换类型";
            }
        }

        public static string NextStepType(string current)
        {
            switch (current)
            {
                case "core": return "prep";
                case "prep": return "comm";
                case "comm": return "warn";
                default: return "core";
            }
        }

        /// <summary>
        /// Builds a pure custom vector icon (20x20 Canvas) for each step type, matching the active skin.
        /// No text suffix is rendered on the ticket face.
        /// </summary>
        public static UIElement CreatePureStepVectorIcon(string stepType, string skinName = BurgerShopSkin, string taskTitle = null)
        {
            Canvas canvas = new Canvas
            {
                Width = 20,
                Height = 20,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = Brushes.Transparent,
                ToolTip = GetStepTooltip(stepType, skinName)
            };

            if (IsCakeShop(skinName))
            {
                BuildCakeShopStepIcon(canvas, stepType);
                return canvas;
            }
            if (IsCoffeeShop(skinName))
            {
                if (!string.IsNullOrWhiteSpace(taskTitle))
                {
                    BuildCoffeeShopTaskIcon(canvas, taskTitle.Trim(), stepType);
                }
                else
                {
                    BuildCoffeeShopStepIcon(canvas, stepType);
                }
                return canvas;
            }
            if (IsVintageRetro(skinName))
            {
                BuildVintageRetroStepIcon(canvas, stepType);
                return canvas;
            }
            if (IsThermalPaper(skinName))
            {
                string sym;
                switch (stepType)
                {
                    case "prep": sym = "[+]"; break;
                    case "comm": sym = "[@]"; break;
                    case "warn": sym = "[!]"; break;
                    default: sym = "[★]"; break;
                }
                return new TextBlock
                {
                    Text = sym,
                    FontFamily = new FontFamily("Consolas, Microsoft YaHei UI"),
                    FontWeight = FontWeights.Bold,
                    FontSize = 12.5,
                    Foreground = BrushFromHex("#1E293B"),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 2, 0),
                    ToolTip = GetStepTooltip(stepType, skinName)
                };
            }

            switch (stepType)
            {
                case "prep": // Lettuce leaf (生菜)
                {
                    canvas.Children.Add(new Path
                    {
                        Fill = BrushFromHex("#22C55E"),
                        Stroke = BrushFromHex("#15803D"),
                        StrokeThickness = 1.2,
                        Data = Geometry.Parse("M 3,14 C 1,9 4,3 10,4 C 15,2 19,7 17,12 C 18,16 13,18 9,16 C 6,18 3,16 3,14 Z")
                    });
                    canvas.Children.Add(new Path
                    {
                        Stroke = BrushFromHex("#DCFCE7"),
                        StrokeThickness = 1.2,
                        Data = Geometry.Parse("M 5,14 Q 10,10 15,6 M 10,11 L 7,7 M 11,10 L 14,12")
                    });
                    break;
                }
                case "comm": // Swiss Cheese wedge (芝士)
                {
                    canvas.Children.Add(new Path
                    {
                        Fill = BrushFromHex("#FACC15"),
                        Stroke = BrushFromHex("#CA8A04"),
                        StrokeThickness = 1.2,
                        Data = Geometry.Parse("M 2,14 L 11,4 L 18,9 L 18,16 L 2,16 Z")
                    });
                    Ellipse hole1 = new Ellipse { Width = 3.5, Height = 3.5, Fill = BrushFromHex("#EAB308") };
                    Canvas.SetLeft(hole1, 7); Canvas.SetTop(hole1, 10);
                    Ellipse hole2 = new Ellipse { Width = 2.8, Height = 2.8, Fill = BrushFromHex("#EAB308") };
                    Canvas.SetLeft(hole2, 13); Canvas.SetTop(hole2, 11);
                    canvas.Children.Add(hole1);
                    canvas.Children.Add(hole2);
                    break;
                }
                case "warn": // Chili pepper (红辣椒)
                {
                    canvas.Children.Add(new Path
                    {
                        Fill = BrushFromHex("#EF4444"),
                        Stroke = BrushFromHex("#991B1B"),
                        StrokeThickness = 1.2,
                        Data = Geometry.Parse("M 13,6 C 17,9 15,16 6,17 C 3,17 2,15 4,14 C 9,13 11,9 10,6 Z")
                    });
                    canvas.Children.Add(new Path
                    {
                        Stroke = BrushFromHex("#16A34A"),
                        StrokeThickness = 1.8,
                        Data = Geometry.Parse("M 11,6 Q 12,2 15,3")
                    });
                    break;
                }
                default: // Grilled Burger Patty (肉饼)
                {
                    canvas.Children.Add(new Path
                    {
                        Fill = BrushFromHex("#991B1B"),
                        Stroke = BrushFromHex("#450A0A"),
                        StrokeThickness = 1.2,
                        Data = Geometry.Parse("M 2,10 C 2,5 18,5 18,10 C 18,15 2,15 2,10 Z")
                    });
                    canvas.Children.Add(new Path
                    {
                        Stroke = BrushFromHex("#FCA5A5"),
                        StrokeThickness = 1.2,
                        Data = Geometry.Parse("M 6,8 L 9,12 M 11,8 L 14,12")
                    });
                    break;
                }
            }
            return canvas;
        }

        private static void BuildCakeShopStepIcon(Canvas canvas, string stepType)
        {
            switch (stepType)
            {
                case "prep": // Strawberry shortcake slice (🍰 蛋糕胚)
                {
                    canvas.Children.Add(new Path
                    {
                        Fill = BrushFromHex("#FDE68A"),
                        Stroke = BrushFromHex("#D97706"),
                        StrokeThickness = 1,
                        Data = Geometry.Parse("M 3,10 L 17,7 L 17,16 L 3,16 Z")
                    });
                    canvas.Children.Add(new Path
                    {
                        Stroke = BrushFromHex("#F43F5E"),
                        StrokeThickness = 1.5,
                        Data = Geometry.Parse("M 3,13 L 17,11.5")
                    });
                    canvas.Children.Add(new Path
                    {
                        Fill = BrushFromHex("#FFF1F2"),
                        Stroke = BrushFromHex("#FB7185"),
                        StrokeThickness = 1,
                        Data = Geometry.Parse("M 3,10 L 11,4 L 17,7 Z")
                    });
                    Ellipse cherry = new Ellipse { Width = 4.5, Height = 4.5, Fill = BrushFromHex("#E11D48") };
                    Canvas.SetLeft(cherry, 9); Canvas.SetTop(cherry, 2);
                    canvas.Children.Add(cherry);
                    break;
                }
                case "comm": // Soft-serve vanilla cream cone (🍦 奶油甜筒)
                {
                    canvas.Children.Add(new Path
                    {
                        Fill = BrushFromHex("#F59E0B"),
                        Stroke = BrushFromHex("#B45309"),
                        StrokeThickness = 1,
                        Data = Geometry.Parse("M 6,11 L 14,11 L 10,18 Z")
                    });
                    canvas.Children.Add(new Path
                    {
                        Fill = BrushFromHex("#FFFBEB"),
                        Stroke = BrushFromHex("#FBBF24"),
                        StrokeThickness = 1,
                        Data = Geometry.Parse("M 5,11 C 4,8 7,7 8,6 C 8,3 12,3 12,6 C 13,7 16,8 15,11 Z")
                    });
                    break;
                }
                case "warn": // Golden sparkles (✨ 闪耀糖星)
                {
                    canvas.Children.Add(new Path
                    {
                        Fill = BrushFromHex("#FBBF24"),
                        Stroke = BrushFromHex("#D97706"),
                        StrokeThickness = 0.9,
                        Data = Geometry.Parse("M 11,2 L 13,8 L 19,10 L 13,12 L 11,18 L 9,12 L 3,10 L 9,8 Z")
                    });
                    canvas.Children.Add(new Path
                    {
                        Fill = BrushFromHex("#F472B6"),
                        Data = Geometry.Parse("M 4,2 L 5,4.5 L 7.5,5.5 L 5,6.5 L 4,9 L 3,6.5 L 0.5,5.5 L 3,4.5 Z")
                    });
                    break;
                }
                default: // Fresh red strawberry (🍓 C位草莓)
                {
                    canvas.Children.Add(new Path
                    {
                        Fill = BrushFromHex("#E11D48"),
                        Stroke = BrushFromHex("#9F1239"),
                        StrokeThickness = 1.1,
                        Data = Geometry.Parse("M 10,5 C 15,5 17,9 14,14 C 12,17 11,18 10,18 C 9,18 8,17 6,14 C 3,9 5,5 10,5 Z")
                    });
                    canvas.Children.Add(new Path
                    {
                        Fill = BrushFromHex("#16A34A"),
                        Data = Geometry.Parse("M 6,5 L 10,3 L 14,5 L 12,7 L 10,5.5 L 8,7 Z")
                    });
                    Ellipse s1 = new Ellipse { Width = 1.5, Height = 2, Fill = BrushFromHex("#FEF08A") };
                    Canvas.SetLeft(s1, 8); Canvas.SetTop(s1, 9);
                    Ellipse s2 = new Ellipse { Width = 1.5, Height = 2, Fill = BrushFromHex("#FEF08A") };
                    Canvas.SetLeft(s2, 11); Canvas.SetTop(s2, 10);
                    Ellipse s3 = new Ellipse { Width = 1.5, Height = 2, Fill = BrushFromHex("#FEF08A") };
                    Canvas.SetLeft(s3, 9.5); Canvas.SetTop(s3, 13);
                    canvas.Children.Add(s1);
                    canvas.Children.Add(s2);
                    canvas.Children.Add(s3);
                    break;
                }
            }
        }

        private static void BuildVintageRetroStepIcon(Canvas canvas, string stepType)
        {
            if (stepType == "warn")
            {
                // Hand-stamped crimson exclamation mark (❗ 朱红加急印记)
                Ellipse outerRing = new Ellipse
                {
                    Width = 15,
                    Height = 15,
                    Fill = BrushFromHex("#FDE8E8"),
                    Stroke = BrushFromHex("#991B1B"),
                    StrokeThickness = 1.4
                };
                Canvas.SetLeft(outerRing, 2.5);
                Canvas.SetTop(outerRing, 2.5);
                canvas.Children.Add(outerRing);

                canvas.Children.Add(new Path
                {
                    Fill = BrushFromHex("#B91C1C"),
                    Data = Geometry.Parse("M 8.8,5.2 L 11.2,5.2 L 10.7,11.6 L 9.3,11.6 Z")
                });
                Ellipse bangDot = new Ellipse
                {
                    Width = 2.4,
                    Height = 2.4,
                    Fill = BrushFromHex("#B91C1C")
                };
                Canvas.SetLeft(bangDot, 8.8);
                Canvas.SetTop(bangDot, 13.0);
                canvas.Children.Add(bangDot);
                return;
            }

            string fillHex;
            string strokeHex;
            string ringHex;
            switch (stepType)
            {
                case "prep": // 🟢 Vintage forest green pencil dot
                    fillHex = "#15803D";
                    strokeHex = "#14532D";
                    ringHex = "#86EFAC";
                    break;
                case "comm": // 🟡 Vintage ochre-amber dot
                    fillHex = "#D97706";
                    strokeHex = "#78350F";
                    ringHex = "#FDE68A";
                    break;
                default:     // 🔴 Vintage vermilion red seal dot
                    fillHex = "#B91C1C";
                    strokeHex = "#7F1D1D";
                    ringHex = "#FCA5A5";
                    break;
            }

            Ellipse halo = new Ellipse
            {
                Width = 14.5,
                Height = 14.5,
                Stroke = BrushFromHex(strokeHex),
                StrokeThickness = 1.0,
                Opacity = 0.55
            };
            Canvas.SetLeft(halo, 2.75);
            Canvas.SetTop(halo, 2.75);
            canvas.Children.Add(halo);

            Ellipse mainDot = new Ellipse
            {
                Width = 11.5,
                Height = 11.5,
                Fill = BrushFromHex(fillHex),
                Stroke = BrushFromHex(strokeHex),
                StrokeThickness = 1.2
            };
            Canvas.SetLeft(mainDot, 4.25);
            Canvas.SetTop(mainDot, 4.25);
            canvas.Children.Add(mainDot);

            Ellipse spec = new Ellipse
            {
                Width = 3.2,
                Height = 2.2,
                Fill = BrushFromHex(ringHex),
                Opacity = 0.75
            };
            Canvas.SetLeft(spec, 6.2);
            Canvas.SetTop(spec, 6.0);
            canvas.Children.Add(spec);
        }

        private static void BuildCoffeeShopStepIcon(Canvas canvas, string stepType)
        {
            switch (stepType)
            {
                case "prep":  // 绿色备料剪贴板
                    CoffeeRect(canvas, 3.2, 4.3, 13.6, 13.4, 2.4, "#40AB75", "#237B50", 0.9);
                    CoffeeRect(canvas, 7.1, 2.5, 5.8, 3.6, 1.2, "#EAF8EA", "#237B50", 0.8);
                    CoffeeLine(canvas, "M 6,11 L 8.5,13.6 L 14,8", "#FFFFFF", 1.8);
                    break;

                case "comm":  // 焦糖便签（沟通）
                    CoffeeRect(canvas, 4, 2.5, 12, 15, 1.5, "#FFCF83", "#BC7A2C", 1.1);
                    CoffeeLine(canvas, "M 6.5,7 L 13.5,7 M 6.5,10 L 13,10 M 6.5,13 L 11,13", "#B76D25", 1.1);
                    break;

                case "warn":  // 浅黄铃铛（注意）
                    CoffeePath(canvas, "M 6.2,13.4 L 6.2,8.4 C 6.2,3.8 13.8,3.8 13.8,8.4 L 13.8,13.4 L 16,15 L 4,15 Z", "#F9BD55", "#AB6528", 0.9);
                    CoffeeEllipse(canvas, 8.5, 15.2, 3, 2, "#9B5529");
                    CoffeeLine(canvas, "M 10,2.5 L 10,4.5", "#AB6528", 1.6);
                    break;

                default:  // 双咖啡豆（核心）
                    CoffeeEllipse(canvas, 2.3, 6.5, 9.4, 8, "#9C5A30", "#542D1A", 1);
                    CoffeeEllipse(canvas, 9.0, 5.5, 8.1, 8.1, "#7B3E22", "#542D1A", 1);
                    CoffeeLine(canvas, "M 5,8 Q 9,10 6,13 M 11,7 Q 15,10 12.5,12", "#432414", 1.2);
                    break;
            }
        }

        private static void BuildCoffeeShopTaskIcon(Canvas canvas, string taskTitle, string stepType)
        {
            switch (taskTitle ?? "")
            {
                case "准备参考图":
                    BuildCoffeeShopStepIcon(canvas, string.IsNullOrEmpty(stepType) ? "prep" : stepType);
                    break;
                case "和ai沟通理解需求":
                case "和AI沟通理解需求":
                    BuildCoffeeShopStepIcon(canvas, string.IsNullOrEmpty(stepType) ? "comm" : stepType);
                    break;
                case "提取描述词":
                    BuildCoffeeShopStepIcon(canvas, string.IsNullOrEmpty(stepType) ? "core" : stepType);
                    break;
                case "准备去生图":
                    if (!string.IsNullOrEmpty(stepType) && stepType != "prep")
                    {
                        BuildCoffeeShopStepIcon(canvas, stepType);
                        break;
                    }
                    CoffeeRect(canvas, 2.5, 3, 15, 14, 2.5, "#58BB79", "#267E53", 1);
                    CoffeeEllipse(canvas, 5.2, 5.2, 3.3, 3.3, "#FFF1AF");
                    CoffeePath(canvas, "M 4.5,14 L 8.5,9.8 L 10.8,12 L 13,9 L 16,14 Z", "#E8F6E8");
                    break;
                case "生图中":
                    if (!string.IsNullOrEmpty(stepType) && stepType != "core")
                    {
                        BuildCoffeeShopStepIcon(canvas, stepType);
                        break;
                    }
                    CoffeePath(canvas, "M 3,6 L 15,6 L 14,15 Q 9,19 4,15 Z", "#FFE5B1", "#713C23", 1);
                    CoffeePath(canvas, "M 15,8 Q 20,8 18,12 Q 17,14 14.5,13", "Transparent", "#713C23", 1.4);
                    CoffeePath(canvas, "M 5,6 Q 9,4 13,6", "Transparent", "#B16C38", 2);
                    CoffeeLine(canvas, "M 7,4 Q 5,2 7,1 M 11,4 Q 9,2 11,1", "#A36D48", 0.8);
                    break;
                case "出图检查中":
                    if (!string.IsNullOrEmpty(stepType) && stepType != "warn")
                    {
                        BuildCoffeeShopStepIcon(canvas, stepType);
                        break;
                    }
                    CoffeeRect(canvas, 3.2, 2.4, 12.3, 15.4, 1.2, "#FFF7E8", "#845033", 1);
                    CoffeeLine(canvas, "M 6,8 L 12.5,8 M 6,11 L 12.5,11 M 6,14 L 10.5,14", "#B78C63", 1);
                    CoffeeEllipse(canvas, 12.8, 1.2, 5.8, 5.8, "#DB4439", "#AC2525", 0.6);
                    CoffeeLine(canvas, "M 15.6,2.5 L 15.6,4.4", "#FFFFFF", 1.1);
                    break;
                case "上传云端":
                    if (!string.IsNullOrEmpty(stepType) && stepType != "prep")
                    {
                        BuildCoffeeShopStepIcon(canvas, stepType);
                        break;
                    }
                    CoffeePath(canvas, "M 5.2,15.6 C 1,15.4 1.5,9.8 5.3,9.4 C 6,3 14,3 15,8.6 C 19.7,8.8 20,15.5 15.6,15.6 Z", "#4CB6EC", "#287AB6", 1);
                    CoffeeLine(canvas, "M 10,15 L 10,9 M 7.5,11.6 L 10,9 L 12.5,11.6", "#FFFFFF", 1.5);
                    break;
                case "已交付":
                    if (!string.IsNullOrEmpty(stepType) && stepType != "comm")
                    {
                        BuildCoffeeShopStepIcon(canvas, stepType);
                        break;
                    }
                    CoffeePath(canvas, "M 3,6 L 10,2.5 L 17,6 L 17,15 L 10,18 L 3,15 Z", "#CE8B40", "#7B431E", 1.1);
                    CoffeeLine(canvas, "M 3,6 L 10,9 L 17,6 M 10,9 L 10,18 M 7,4.1 L 14,7.3", "#77411B", 1);
                    break;
                default:
                    BuildCoffeeShopStepIcon(canvas, stepType);
                    break;
            }
        }

        private static void CoffeePath(Canvas c, string path, string fill, string stroke = null, double thick = 1)
        {
            c.Children.Add(new Path
            {
                Data = Geometry.Parse(path),
                Fill = fill == "Transparent" ? Brushes.Transparent : BrushFromHex(fill),
                Stroke = stroke == null ? null : BrushFromHex(stroke),
                StrokeThickness = thick,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            });
        }

        private static void CoffeeLine(Canvas c, string path, string stroke, double thick)
        {
            CoffeePath(c, path, "Transparent", stroke, thick);
        }

        private static void CoffeeRect(Canvas c, double x, double y, double w, double h,
            double radius, string fill, string stroke = null, double thick = 1)
        {
            Rectangle r = new Rectangle
            {
                Width = w,
                Height = h,
                RadiusX = radius,
                RadiusY = radius,
                Fill = fill == "Transparent" ? Brushes.Transparent : BrushFromHex(fill),
                Stroke = stroke == null ? null : BrushFromHex(stroke),
                StrokeThickness = thick
            };
            Canvas.SetLeft(r, x);
            Canvas.SetTop(r, y);
            c.Children.Add(r);
        }

        private static void CoffeeEllipse(Canvas c, double x, double y, double w, double h,
            string fill, string stroke = null, double thick = 1)
        {
            Ellipse shape = new Ellipse
            {
                Width = w,
                Height = h,
                Fill = fill == "Transparent" ? Brushes.Transparent : BrushFromHex(fill),
                Stroke = stroke == null ? null : BrushFromHex(stroke),
                StrokeThickness = thick
            };
            Canvas.SetLeft(shape, x);
            Canvas.SetTop(shape, y);
            c.Children.Add(shape);
        }
    }
}
