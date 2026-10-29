using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using StickyTickets.Themes;

namespace StickyTickets.UI
{
    /// <summary>
    /// Startup onboarding notice board ("新手公告栏") displaying the 6 core usage rules,
    /// with a bottom "知道了" button to acknowledge and close the window.
    /// </summary>
    public class OnboardingNoticeWindow : Window
    {
        public static readonly string[][] GuideItems = new string[][]
        {
            new string[] { "一键隐藏全部", "忙时按快捷键 Ctrl + H（或把鼠标移开顶部栏），即可一键隐藏或呼出桌面小票。按快捷键 Ctrl + G可保留当前小票仅收起顶部栏。" },
            new string[] { "新建任务与笔记", "点顶部栏「+ 新建小票」创建倒计时任务，点「📝 新建笔记」创建不限时的自由便签。" },
            new string[] { "收纳与桌面钉住", "按住小票顶部往下拖可钉在桌面，推回屏幕顶端自动吸回挂单杆收纳；点击挂单杆标签可随时收起或展开。" },
            new string[] { "配方自动拆单", "「📖 配方」用来保存常用工作流模板，写单时输入触发词回车，就能自动生成整套固定工序。" },
            new string[] { "交单获取积分", "勾选步骤后点击「🔔 叮！交单出餐」，按时完成或填写图片数量即可赚取对应积分。" },
            new string[] { "积分战报", "点击顶部「⭐ 积分」可查看今日按时完成战报与历史记录，积分代表你完成的图片张数。" }
        };

        private string _currentSkinName;
        private bool _acknowledged;

        public Action OnAcknowledged { get; set; }

        public OnboardingNoticeWindow(string skinName)
        {
            _currentSkinName = string.IsNullOrWhiteSpace(skinName) ? SkinThemeManager.BurgerShopSkin : skinName;

            Title = "任务便签 · 新手公告栏";
            BitmapImage logoImg = SkinThemeManager.LoadEmbeddedAppLogo();
            if (logoImg != null) Icon = logoImg;

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            Width = 490;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            BuildNoticeLayout();

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
                if (!_acknowledged)
                {
                    _acknowledged = true;
                    if (OnAcknowledged != null) OnAcknowledged();
                }
            };
        }

        public static string FormatPlainGuideText()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < GuideItems.Length; i++)
            {
                if (i > 0) sb.AppendLine();
                sb.Append("• ").Append(GuideItems[i][0]).Append("：").Append(GuideItems[i][1]);
            }
            return sb.ToString();
        }

        public void ApplySkin(string skinName)
        {
            string normalized = string.IsNullOrWhiteSpace(skinName) ? SkinThemeManager.BurgerShopSkin : skinName;
            if (string.Equals(_currentSkinName, normalized, StringComparison.OrdinalIgnoreCase)) return;
            _currentSkinName = normalized;
            BuildNoticeLayout();
        }

        private void CenterOnPrimaryWorkArea()
        {
            UpdateLayout();
            Rect wa = SystemParameters.WorkArea;
            double w = ActualWidth > 0 ? ActualWidth : Width;
            double h = ActualHeight > 0 ? ActualHeight : 440;
            Left = wa.Left + (wa.Width - w) / 2.0;
            Top = wa.Top + (wa.Height - h) / 2.0;
        }

        private void AcknowledgeAndClose()
        {
            if (!_acknowledged)
            {
                _acknowledged = true;
                if (OnAcknowledged != null) OnAcknowledged();
            }
            try { Close(); } catch { }
        }

        private void BuildNoticeLayout()
        {
            bool isCake = SkinThemeManager.IsCakeShop(_currentSkinName);
            bool isCoffee = SkinThemeManager.IsCoffeeShop(_currentSkinName);
            bool isThermal = SkinThemeManager.IsThermalPaper(_currentSkinName);
            bool isVintage = SkinThemeManager.IsVintageRetro(_currentSkinName);
            bool isHighContrast = string.Equals(_currentSkinName, SkinThemeManager.HighContrastSkin, StringComparison.OrdinalIgnoreCase);

            string frameBg = isHighContrast ? "#09090B" :
                             (isCake ? "#FFFBFD" :
                             (isCoffee ? "#FFFBF4" :
                             (isThermal ? "#FFFFFF" :
                             (isVintage ? "#F7EED7" : "#FFFDF7"))));
            string frameBorder = isHighContrast ? "#FACC15" :
                                 (isCake ? "#E8B7C8" :
                                 (isCoffee ? "#C8A27A" :
                                 (isThermal ? "#94A3B8" :
                                 (isVintage ? "#8B1E1E" : "#F5B731"))));
            string headerBg = isHighContrast ? "#18181B" :
                              (isCake ? "#F8D7E4" :
                              (isCoffee ? "#63301D" :
                              (isThermal ? "#1E293B" :
                              (isVintage ? "#8B1E1E" : "#D6372B"))));
            string headerFg = isCake ? "#6D1A36" : (isHighContrast ? "#FACC15" : "#FFFFFF");
            string itemCardBg = isHighContrast ? "#18181B" :
                                (isCake ? "#FFF5F8" :
                                (isCoffee ? "#FFF6E7" :
                                (isThermal ? "#F8FAFC" :
                                (isVintage ? "#FAF3E0" : "#FFF8EB"))));
            string itemBorder = isHighContrast ? "#52525B" :
                                (isCake ? "#F2D4DF" :
                                (isCoffee ? "#E5CDB0" :
                                (isThermal ? "#E2E8F0" :
                                (isVintage ? "#D8C39E" : "#FDE68A"))));
            string titleFg = isHighContrast ? "#FACC15" :
                             (isCake ? "#9D174D" :
                             (isCoffee ? "#542819" :
                             (isThermal ? "#0F172A" :
                             (isVintage ? "#7F1D1D" : "#9A3412"))));
            string bodyFg = isHighContrast ? "#F4F4F5" :
                            (isCake ? "#4A2533" :
                            (isCoffee ? "#3E2013" :
                            (isThermal ? "#334155" :
                            (isVintage ? "#292524" : "#292524"))));
            string btnBg = isHighContrast ? "#FACC15" :
                           (isCake ? "#DB2777" :
                           (isCoffee ? "#6F381F" :
                           (isThermal ? "#0F172A" :
                           (isVintage ? "#8B1E1E" : "#059669"))));
            string btnFg = isHighContrast ? "#09090B" : "#FFFFFF";

            Grid outerShadowWrap = new Grid { Margin = new Thickness(12) };
            Border card = new Border
            {
                Background = SkinThemeManager.BrushFromHex(frameBg),
                BorderBrush = SkinThemeManager.BrushFromHex(frameBorder),
                BorderThickness = new Thickness(isHighContrast ? 2.5 : 2),
                CornerRadius = new CornerRadius(isThermal ? 6 : (isVintage ? 8 : 16)),
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 18,
                    ShadowDepth = 4,
                    Opacity = 0.25
                }
            };

            StackPanel mainStack = new StackPanel();

            // Header Banner (draggable)
            Border header = new Border
            {
                Background = SkinThemeManager.BrushFromHex(headerBg),
                CornerRadius = new CornerRadius(isThermal ? 4 : (isVintage ? 6 : 14), isThermal ? 4 : (isVintage ? 6 : 14), 0, 0),
                Padding = new Thickness(16, 12, 16, 12),
                Cursor = Cursors.SizeAll
            };
            header.MouseLeftButtonDown += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    try { DragMove(); } catch { }
                }
            };

            DockPanel headerDock = new DockPanel();
            StackPanel titleRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            BitmapImage appLogo = SkinThemeManager.LoadEmbeddedAppLogo();
            if (appLogo != null)
            {
                Image logo = new Image
                {
                    Source = appLogo,
                    Width = 22,
                    Height = 22,
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                RenderOptions.SetBitmapScalingMode(logo, BitmapScalingMode.HighQuality);
                titleRow.Children.Add(logo);
            }
            titleRow.Children.Add(new TextBlock
            {
                Text = "📢 新手公告栏 · 快速上手指南",
                Foreground = SkinThemeManager.BrushFromHex(headerFg),
                FontWeight = FontWeights.Bold,
                FontSize = 15,
                VerticalAlignment = VerticalAlignment.Center
            });
            headerDock.Children.Add(titleRow);
            header.Child = headerDock;
            mainStack.Children.Add(header);

            // 6 Notice Items
            StackPanel contentStack = new StackPanel { Margin = new Thickness(16, 12, 16, 8) };
            for (int i = 0; i < GuideItems.Length; i++)
            {
                Border rowCard = new Border
                {
                    Background = SkinThemeManager.BrushFromHex(itemCardBg),
                    BorderBrush = SkinThemeManager.BrushFromHex(itemBorder),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(isThermal ? 4 : 8),
                    Padding = new Thickness(10, 7, 10, 7),
                    Margin = new Thickness(0, 0, 0, 7)
                };

                TextBlock tb = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    LineHeight = 19,
                    FontSize = 12.5,
                    Foreground = SkinThemeManager.BrushFromHex(bodyFg)
                };
                tb.Inlines.Add(new System.Windows.Documents.Run((i + 1) + ". " + GuideItems[i][0] + "：")
                {
                    FontWeight = FontWeights.Bold,
                    Foreground = SkinThemeManager.BrushFromHex(titleFg)
                });
                tb.Inlines.Add(new System.Windows.Documents.Run(GuideItems[i][1]));

                rowCard.Child = tb;
                contentStack.Children.Add(rowCard);
            }
            mainStack.Children.Add(contentStack);

            // Footer with "知道了" button
            Border footer = new Border
            {
                Padding = new Thickness(16, 4, 16, 14)
            };
            StackPanel footerStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            Button gotItBtn = new Button
            {
                Content = "知道了",
                Width = 160,
                Padding = new Thickness(18, 7, 18, 7),
                Background = SkinThemeManager.BrushFromHex(btnBg),
                Foreground = SkinThemeManager.BrushFromHex(btnFg),
                FontWeight = FontWeights.Bold,
                FontSize = 13.5,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            ApplyRoundedButtonTemplate(gotItBtn, isThermal ? 4 : (isCake ? 16 : 10));
            gotItBtn.Click += (s, e) => AcknowledgeAndClose();
            footerStack.Children.Add(gotItBtn);

            footer.Child = footerStack;
            mainStack.Children.Add(footer);

            card.Child = mainStack;
            outerShadowWrap.Children.Add(card);
            Content = outerShadowWrap;
        }

        private static void ApplyRoundedButtonTemplate(Button btn, double cornerRadius)
        {
            ControlTemplate tpl = new ControlTemplate(typeof(Button));
            FrameworkElementFactory bd = new FrameworkElementFactory(typeof(Border));
            bd.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            bd.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            bd.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            bd.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));
            bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(cornerRadius));

            FrameworkElementFactory cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            bd.AppendChild(cp);

            tpl.VisualTree = bd;
            btn.Template = tpl;
        }
    }
}
