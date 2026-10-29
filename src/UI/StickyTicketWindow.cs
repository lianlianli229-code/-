using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using StickyTickets.Data;
using StickyTickets.Interop;
using StickyTickets.Services;
using StickyTickets.Themes;

namespace StickyTickets.UI
{
    public partial class StickyTicketWindow : Window
    {
        private readonly TicketRepository _repo;
        private readonly TicketRecord _ticket;
        private readonly Action _onChanged;
        private ResourceDictionary _theme;
        private string _currentSkinName = SkinThemeManager.BurgerShopSkin;

        private TextBlock _clipLabel;
        private TextBox _titleBox;
        private TextBlock _countdownText;
        private TextBlock _progressLabel;
        private TextBlock _progressPctText;
        private Border _progressBarFill;
        private StackPanel _stepsPanel;
        private TextBox _quickAddBox;
        private TextBox _scratchBox;
        private Border _attachedImageCard;
        private Image _attachedImageView;
        private Border _stampOverlay;
        private TextBlock _stampText;
        private TextBlock _wakeHintText;
        private RotateTransform _shakeTransform;
        private bool _temporarilyWoken;
        private bool _awaitingFirstEdit;

        public TicketRecord Ticket { get { return _ticket; } }
        public Action<long> OnDockedClicked { get; set; }
        public Action<long> OnFirstEditCompleted { get; set; }
        public Action<long> OnCollapseRequested { get; set; }
        public Func<bool> IsRailVisibleQuery { get; set; }

        public void MarkAwaitingFirstEdit()
        {
            _awaitingFirstEdit = true;
        }

        public void ClearAwaitingFirstEdit()
        {
            _awaitingFirstEdit = false;
        }

        public void CompleteFirstEditIfPending()
        {
            if (!_awaitingFirstEdit) return;
            _awaitingFirstEdit = false;
            if (OnFirstEditCompleted != null)
            {
                OnFirstEditCompleted(_ticket.Id);
            }
        }

        public void FocusInitialTicketInput()
        {
            if (_quickAddBox != null)
            {
                _quickAddBox.Focus();
            }
            else if (_titleBox != null)
            {
                _titleBox.Focus();
                _titleBox.SelectAll();
            }
        }

        public StickyTicketWindow(TicketRepository repo, TicketRecord ticket, string skinName, Action onChanged)
        {
            _repo = repo;
            _ticket = ticket;
            _onChanged = onChanged;
            _currentSkinName = string.IsNullOrWhiteSpace(skinName) ? SkinThemeManager.BurgerShopSkin : skinName;
            _theme = SkinThemeManager.GetThemeDictionary(_currentSkinName);

            WindowStyle = WindowStyle.None;
            BitmapImage logoImg = SkinThemeManager.LoadEmbeddedAppLogo();
            if (logoImg != null) Icon = logoImg;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            Width = 356;
            SizeToContent = SizeToContent.Height;

            double left = _ticket.PosX;
            double top = _ticket.PosY;
            Win32Interop.ClampToVisibleWorkArea(ref left, ref top, Width, 420);
            Left = left;
            Top = top;

            BuildTicketLayout();
            RefreshClockAndProgress(DateTimeOffset.UtcNow.ToUnixTimeSeconds(), false);
            SourceInitialized += (s, e) => ApplyClickThroughState();
            PreviewMouseDown += (s, e) =>
            {
                if (_ticket.IsDockedOnRail && OnDockedClicked != null)
                {
                    long clickedId = _ticket.Id;
                    Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
                    {
                        if (_ticket.IsDockedOnRail && OnDockedClicked != null)
                        {
                            OnDockedClicked(clickedId);
                        }
                    }));
                }
            };
        }

        private Brush GetBrush(string key, string fallbackHex)
        {
            if (_theme != null && _theme.Contains(key) && _theme[key] is Brush) return (Brush)_theme[key];
            return SkinThemeManager.BrushFromHex(fallbackHex);
        }

        public void ApplySkin(string skinName)
        {
            string normalized = string.IsNullOrWhiteSpace(skinName) ? SkinThemeManager.BurgerShopSkin : skinName;
            if (string.Equals(_currentSkinName, normalized, StringComparison.OrdinalIgnoreCase)) return;
            _currentSkinName = normalized;
            _theme = SkinThemeManager.GetThemeDictionary(_currentSkinName);
            BuildTicketLayout();
            RefreshClockAndProgress(DateTimeOffset.UtcNow.ToUnixTimeSeconds(), false);
        }

        private void BuildTicketLayout()
        {
            _cakeNoText = null;
            _cakeTitleText = null;
            _cakeHourglassText = null;
            _cakeTimeText = null;
            _cakeStepText = null;
            _cakePercentText = null;
            _cakeStatusDot = null;
            _cakeBarFill = null;
            _cakeFillColumn = null;
            _cakeEmptyColumn = null;
            _countdownText = null;
            _progressLabel = null;
            _progressPctText = null;
            _progressBarFill = null;

            Grid rootWrapper = new Grid { Margin = new Thickness(6) };
            _shakeTransform = new RotateTransform(0, 175, 10);
            rootWrapper.RenderTransform = _shakeTransform;

            StackPanel outerStack = new StackPanel();
            outerStack.Children.Add(BuildTopClipPlaque());
            outerStack.Children.Add(BuildPerforatedReceiptEdge(true));

            bool isCake = IsCakeTheme();
            bool isCoffee = IsCoffeeTheme();
            bool isThermal = IsThermalTheme();
            bool isVintage = IsVintageTheme();
            bool hasSerratedEdge = isThermal || isVintage;
            double bodySide = isCake ? 12 : (isCoffee ? 10 : 4);
            Border outerFrame = new Border
            {
                Margin = new Thickness(0),
                Background = GetBrush("PaperBgBrush", "#FFFDF7"),
                BorderBrush = GetBrush("CheeseGoldBrush", "#F5B731"),
                BorderThickness = isThermal ? new Thickness(1, 0, 1, 0) : (isVintage ? new Thickness(2, 0, 2, 0) : ((isCake || isCoffee) ? new Thickness(2) : new Thickness(3.5))),
                CornerRadius = hasSerratedEdge ? new CornerRadius(0) : (isCoffee ? new CornerRadius(18) : new CornerRadius(16)),
                Padding = hasSerratedEdge ? new Thickness(4, 0, 4, 8) : ((isCake || isCoffee) ? new Thickness(0, 0, 0, 8) : new Thickness(6, 4, 6, 8))
            };

            Grid cardGrid = new Grid();
            StackPanel body = new StackPanel();

            body.Children.Add(BuildAwningHeader());
            body.Children.Add(BuildMainRedBanner());

            if (_ticket.IsStickyNote)
            {
                BuildBlankStickyNoteBody(body);
                cardGrid.Children.Add(body);
                outerFrame.Child = cardGrid;
                outerStack.Children.Add(outerFrame);
                outerStack.Children.Add(BuildPerforatedReceiptEdge(false));
                rootWrapper.Children.Add(outerStack);
                Content = rootWrapper;
                return;
            }

            // First-run onboarding guide for pure vector icons
            UserEconomyRecord eco = _repo.GetEconomy();
            if (!eco.FirstRunGuided)
            {
                Border guide = new Border
                {
                    Background = SkinThemeManager.BrushFromHex("#FEF3C7"),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(8, 4, 8, 4),
                    Margin = new Thickness(bodySide, 0, bodySide, 4)
                };
                DockPanel gdp = new DockPanel();
                Button dismissBtn = new Button { Content = "知道了", FontSize = 10.5, Padding = new Thickness(4, 1, 4, 1) };
                DockPanel.SetDock(dismissBtn, Dock.Right);
                dismissBtn.Click += (s, e) =>
                {
                    eco.FirstRunGuided = true;
                    _repo.SaveEconomy(eco);
                    guide.Visibility = Visibility.Collapsed;
                };
                gdp.Children.Add(dismissBtn);
                gdp.Children.Add(new TextBlock
                {
                    Text = "💡 图标指南：🥩主菜(核心) · 🥬生菜(准备) · 🧀芝士(沟通) · 🌶️辣椒(注意)，点图标可切换",
                    FontSize = 11,
                    Foreground = SkinThemeManager.BrushFromHex("#92400E"),
                    TextWrapping = TextWrapping.Wrap
                });
                guide.Child = gdp;
                body.Children.Add(guide);
            }

            // Checklist #6: Cream-white rounded inner card for step progress bar (CakeShop already integrates progress inside BuildCakeShopHeaderBanner)
            if (!isCake)
            {
                Border progressCard = new Border
                {
                    Background = GetBrush("CreamWhiteBrush", "#FFF6E5"),
                    BorderBrush = SkinThemeManager.BrushFromHex(isVintage ? "#C8B28A" : (isThermal ? "#CBD5E1" : (isCoffee ? "#E2C6A4" : "#FDE68A"))),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(isVintage ? 4 : (isThermal ? 3 : 10)),
                    Padding = new Thickness(10, 6, 10, 8),
                    Margin = new Thickness(bodySide, 0, bodySide, 6)
                };
                StackPanel progStack = new StackPanel();
                DockPanel progHeader = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
                _progressPctText = new TextBlock { Text = "25%", FontWeight = FontWeights.Bold, FontSize = 12, Foreground = GetBrush("SubtleTextBrush", "#57534E") };
                DockPanel.SetDock(_progressPctText, Dock.Right);
                _progressLabel = new TextBlock { Text = "▲ 提醒 · 工序进度 1/4", FontWeight = FontWeights.Bold, FontSize = 12, Foreground = GetBrush("PrimaryTextBrush", "#292524") };
                progHeader.Children.Add(_progressPctText);
                progHeader.Children.Add(_progressLabel);
                progStack.Children.Add(progHeader);

                Border progTrack = new Border { Height = 7, Background = SkinThemeManager.BrushFromHex(isCoffee ? "#EAD6BC" : "#E7E5E4"), CornerRadius = new CornerRadius(3.5), ClipToBounds = true };
                _progressBarFill = new Border { HorizontalAlignment = HorizontalAlignment.Left, Width = 80, Background = GetBrush("CheeseGoldBrush", "#F5B731"), CornerRadius = new CornerRadius(3.5) };
                progTrack.Child = _progressBarFill;
                progStack.Children.Add(progTrack);
                progressCard.Child = progStack;
                body.Children.Add(progressCard);
            }

            // Pure vector icon Steps List
            _stepsPanel = new StackPanel { Margin = new Thickness(bodySide, 0, bodySide, 4) };
            body.Children.Add(_stepsPanel);
            RebuildStepsList();

            // Quick manual step & "/time 30m" & one-click split + Recipe Library button row
            DockPanel inputRow = new DockPanel { Margin = new Thickness(bodySide, 2, bodySide, 4) };
            Button openRecipeBtn = new Button
            {
                Content = "📖 配方",
                Background = SkinThemeManager.BrushFromHex("#7A1F1A"),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 11.5,
                Padding = new Thickness(7, 4, 7, 4),
                Margin = new Thickness(4, 0, 0, 0),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = "打开《专属工序配方库》，随时添加/修改触发关键词与固定工序"
            };
            DockPanel.SetDock(openRecipeBtn, Dock.Right);
            openRecipeBtn.Click += (s, e) =>
            {
                RecipeManagerWindow rmw = new RecipeManagerWindow(_repo);
                rmw.ShowDialog();
            };
            inputRow.Children.Add(openRecipeBtn);

            Button quickSplitBtn = new Button
            {
                Content = "🖨️ 拆单",
                Background = SkinThemeManager.BrushFromHex("#F5B731"),
                Foreground = SkinThemeManager.BrushFromHex("#451A03"),
                FontWeight = FontWeights.Bold,
                FontSize = 11.5,
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(4, 0, 0, 0),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = "按固定配方关键词或分号/句号/序号一键拆成多条工序"
            };
            DockPanel.SetDock(quickSplitBtn, Dock.Right);
            quickSplitBtn.Click += (s, e) => ProcessQuickInputAndSplit();
            inputRow.Children.Add(quickSplitBtn);

            _quickAddBox = new TextBox
            {
                Padding = new Thickness(6, 4, 6, 4),
                FontSize = 12,
                Background = SkinThemeManager.BrushFromHex(isVintage ? "#FAF3E0" : "#ECFDF5"),
                BorderBrush = isVintage ? SkinThemeManager.BrushFromHex("#C8B28A") : SkinThemeManager.BrushFromHex("#CBD5E1"),
                ToolTip = "支持 #标题；步骤1；步骤2；/time 30m 一键回车拆单！"
            };
            _quickAddBox.KeyDown += OnQuickAddKeyDown;
            inputRow.Children.Add(_quickAddBox);
            body.Children.Add(inputRow);

            // Persistent Image Attachment Panel OUTSIDE the collapsible Expander so collapsing menu never hides the image!
            body.Children.Add(BuildAttachedImagePanel(135, bodySide));

            // Collapsible Scratchpad & WIC Image Attachment
            Expander scratchExp = new Expander
            {
                Header = "📝 随手记草稿 / 粘贴图片 / AI拆单",
                FontSize = 11.5,
                Margin = new Thickness(bodySide, 0, bodySide, 4),
                IsExpanded = false
            };
            StackPanel scratchStack = new StackPanel { Margin = new Thickness(2, 4, 2, 2) };
            _scratchBox = new TextBox
            {
                Text = _ticket.ScratchNotes,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Height = 52,
                FontSize = 11.5,
                MaxLength = 2000
            };
            _scratchBox.LostFocus += (s, e) =>
            {
                string sanitized = AudioAndUpdateService.SanitizePastedText(_scratchBox.Text);
                bool changed = !string.Equals(_ticket.ScratchNotes ?? "", sanitized, StringComparison.Ordinal);
                if (!changed) return;
                _ticket.ScratchNotes = sanitized;
                if (!string.IsNullOrWhiteSpace(sanitized))
                {
                    CompleteFirstEditIfPending();
                }
                SaveAndNotify();
            };
            scratchStack.Children.Add(_scratchBox);

            WrapPanel scratchActions = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            Button pasteImgBtn = CreateMiniActionButton("🖼️ 粘贴剪贴板图片", OnPasteClipboardImage);
            Button delImgBtn = CreateMiniActionButton("🗑️ 删除贴图", OnDeleteAttachedImage);
            Button aiSplitBtn = CreateMiniActionButton("🖨️ AI拆单成工序", OnAiSplitScratchpad);
            Button snoozeBtn = CreateMiniActionButton("⏳ +15m 加时", (s, e) =>
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (_ticket.DeadlineUnix < now) _ticket.DeadlineUnix = now + 900;
                else _ticket.DeadlineUnix += 900;
                if (_ticket.StartTimeUnix <= 0) _ticket.StartTimeUnix = now;
                SaveAndNotify();
            });
            Button pinToggleBtn = CreateMiniActionButton(_ticket.IsDockedOnRail ? "📌 拽到桌面钉住" : "📎 挂回顶部挂单绳", (s, e) =>
            {
                _ticket.IsDockedOnRail = !_ticket.IsDockedOnRail;
                SaveAndNotify();
            });
            Button clickThroughBtn = CreateMiniActionButton(_ticket.IsClickThrough ? "👻 解除穿透" : "👻 开启穿透", (s, e) =>
            {
                _ticket.IsClickThrough = !_ticket.IsClickThrough;
                _ticket.IsDockedOnRail = false;
                ApplyClickThroughState();
                SaveAndNotify();
            });
            Button trashBtn = CreateMiniActionButton("🗑️ 移入回收站", (s, e) =>
            {
                _ticket.Status = "Trash";
                SaveAndNotify();
                Hide();
            });

            scratchActions.Children.Add(pasteImgBtn);
            scratchActions.Children.Add(delImgBtn);
            scratchActions.Children.Add(aiSplitBtn);
            scratchActions.Children.Add(snoozeBtn);
            scratchActions.Children.Add(pinToggleBtn);
            scratchActions.Children.Add(clickThroughBtn);
            scratchActions.Children.Add(trashBtn);
            scratchStack.Children.Add(scratchActions);
            scratchExp.Content = scratchStack;
            body.Children.Add(scratchExp);

            // Checklist #7: Footer bar with Ctrl+Alt wake hint & green "🔔 叮！交单出餐" button
            if (isCake)
            {
                body.Children.Add(new System.Windows.Shapes.Line
                {
                    X1 = 0,
                    Y1 = 0,
                    X2 = 1,
                    Y2 = 0,
                    Stretch = Stretch.Fill,
                    Stroke = SkinThemeManager.BrushFromHex("#DECBD3"),
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 3, 2.5 },
                    Margin = new Thickness(0, 4, 0, 2)
                });
            }
            Border footer = new Border
            {
                BorderBrush = SkinThemeManager.BrushFromHex(isCoffee ? "#E5CDB0" : "#D6D3D1"),
                BorderThickness = isCake ? new Thickness(0) : new Thickness(0, 1, 0, 0),
                Padding = isCake ? new Thickness(14, 6, 14, 2) : (isCoffee ? new Thickness(10, 8, 10, 2) : new Thickness(6, 8, 6, 2)),
                Margin = isCake ? new Thickness(0) : (isCoffee ? new Thickness(8, 4, 8, 0) : new Thickness(2, 4, 2, 0))
            };
            DockPanel footerDock = new DockPanel();
            StackPanel rightFooterBtns = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(rightFooterBtns, Dock.Right);

            Button collapseBtn = BuildCollapseFooterButton(
                isCake,
                isCoffee,
                isThermal,
                isVintage,
                "收起关闭当前小票（收纳回挂单绳，不删除内容）");
            rightFooterBtns.Children.Add(collapseBtn);

            Button serveBtn = new Button
            {
                Content = "🔔 叮！交单出餐",
                Background = SkinThemeManager.BrushFromHex("#059669"),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 12.5,
                Padding = new Thickness(14, 5, 14, 5),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                IsEnabled = _ticket.Status != "Served"
            };
            if (isCake || isCoffee)
            {
                ApplyRoundedButtonTemplate(serveBtn, isCake ? 14 : 10);
            }
            serveBtn.Click += OnServeTicketClicked;
            rightFooterBtns.Children.Add(serveBtn);
            footerDock.Children.Add(rightFooterBtns);

            _wakeHintText = new TextBlock
            {
                Text = _ticket.IsClickThrough ? "👻 穿透中 (按住 Ctrl+Alt 唤醒)" : "Ctrl+Alt 唤醒穿透",
                Foreground = GetBrush("SubtleTextBrush", "#57534E"),
                FontSize = 11,
                Margin = new Thickness(0, 0, 6, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            footerDock.Children.Add(_wakeHintText);
            footer.Child = footerDock;
            body.Children.Add(footer);

            cardGrid.Children.Add(body);

            // Checklist #7: Stamp overlay
            string stampHex = isVintage ? "#991B1B" : (isThermal ? "#1D4ED8" : (isCake ? "#DB2777" : (isCoffee ? "#7C3A1D" : "#DC2626")));
            Color stampBgColor = isVintage
                ? Color.FromArgb(235, 254, 243, 199)
                : (isThermal ? Color.FromArgb(235, 239, 246, 255) : (isCoffee ? Color.FromArgb(238, 255, 248, 234) : Color.FromArgb(230, 254, 242, 242)));
            _stampOverlay = new Border
            {
                BorderBrush = SkinThemeManager.BrushFromHex(stampHex),
                BorderThickness = new Thickness(3.5),
                Background = new SolidColorBrush(stampBgColor),
                CornerRadius = new CornerRadius(isThermal ? 3 : (isVintage ? 6 : 10)),
                Padding = new Thickness(14, 6, 14, 6),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false,
                RenderTransform = new RotateTransform(-12),
                Visibility = _ticket.Status == "Served" ? Visibility.Visible : Visibility.Collapsed
            };
            _stampText = new TextBlock
            {
                Text = isVintage ? "SERVED · 已出餐" : (isThermal ? "[ PAID / 已结清 ]" : "PERFECT! 100%"),
                Foreground = SkinThemeManager.BrushFromHex(stampHex),
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontWeight = FontWeights.Black,
                FontSize = 17
            };
            _stampOverlay.Child = _stampText;
            cardGrid.Children.Add(_stampOverlay);

            outerFrame.Child = cardGrid;
            outerStack.Children.Add(outerFrame);
            outerStack.Children.Add(BuildPerforatedReceiptEdge(false));
            rootWrapper.Children.Add(outerStack);
            Content = rootWrapper;
        }

        private void BuildBlankStickyNoteBody(StackPanel body)
        {
            bool isCake = IsCakeTheme();
            bool isCoffee = IsCoffeeTheme();
            bool isThermal = IsThermalTheme();
            bool isVintage = IsVintageTheme();
            Border paperSheet = new Border
            {
                Background = isThermal ? Brushes.White : (isVintage ? SkinThemeManager.BrushFromHex("#FAF3E0") : GetBrush("CreamWhiteBrush", "#FFFDF7")),
                BorderBrush = isThermal ? SkinThemeManager.BrushFromHex("#CBD5E1") : (isVintage ? SkinThemeManager.BrushFromHex("#C8B28A") : GetBrush("CheeseGoldBrush", "#FDE68A")),
                BorderThickness = isVintage ? new Thickness(2.5, 1, 1, 1) : new Thickness(1),
                CornerRadius = (isThermal || isVintage) ? new CornerRadius(2) : new CornerRadius(10),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(isCake ? 12 : (isCoffee ? 10 : 4), 2, isCake ? 12 : (isCoffee ? 10 : 4), 4)
            };
            if (isVintage)
            {
                paperSheet.BorderBrush = SkinThemeManager.BrushFromHex("#C53030");
            }

            StackPanel noteStack = new StackPanel();
            _scratchBox = new TextBox
            {
                Text = _ticket.ScratchNotes,
                AcceptsReturn = true,
                AcceptsTab = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MinHeight = 220,
                MaxHeight = 420,
                FontSize = 13.5,
                Foreground = GetBrush("PrimaryTextBrush", "#292524"),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                MaxLength = 8000,
                ToolTip = "自由书写笔记内容（自动实时保存）"
            };
            _scratchBox.TextChanged += (s, e) =>
            {
                _ticket.ScratchNotes = AudioAndUpdateService.SanitizePastedText(_scratchBox.Text);
                _repo.SaveTicket(_ticket);
            };
            _scratchBox.LostFocus += (s, e) =>
            {
                string sanitized = AudioAndUpdateService.SanitizePastedText(_scratchBox.Text);
                _ticket.ScratchNotes = sanitized;
                if (!string.IsNullOrWhiteSpace(sanitized))
                {
                    CompleteFirstEditIfPending();
                }
                SaveAndNotify();
            };
            noteStack.Children.Add(_scratchBox);
            noteStack.Children.Add(BuildAttachedImagePanel(160));

            paperSheet.Child = noteStack;
            body.Children.Add(paperSheet);

            WrapPanel noteToolsRow = new WrapPanel
            {
                Margin = new Thickness(isCake ? 12 : (isCoffee ? 10 : 6), 2, isCake ? 12 : (isCoffee ? 10 : 6), 2)
            };
            Button pasteBtn = CreateMiniActionButton("🖼️ 贴图", OnPasteClipboardImage);
            Button pinBtn = CreateMiniActionButton(_ticket.IsDockedOnRail ? "📌 钉桌面" : "📎 归轨", (s, e) =>
            {
                _ticket.IsDockedOnRail = !_ticket.IsDockedOnRail;
                SaveAndNotify();
            });
            Button ghostBtn = CreateMiniActionButton(_ticket.IsClickThrough ? "👻 解除穿透" : "👻 穿透", (s, e) =>
            {
                _ticket.IsClickThrough = !_ticket.IsClickThrough;
                _ticket.IsDockedOnRail = false;
                ApplyClickThroughState();
                SaveAndNotify();
            });
            noteToolsRow.Children.Add(pasteBtn);
            noteToolsRow.Children.Add(pinBtn);
            noteToolsRow.Children.Add(ghostBtn);
            body.Children.Add(noteToolsRow);

            if (isCake)
            {
                body.Children.Add(new System.Windows.Shapes.Line
                {
                    X1 = 0,
                    Y1 = 0,
                    X2 = 1,
                    Y2 = 0,
                    Stretch = Stretch.Fill,
                    Stroke = SkinThemeManager.BrushFromHex("#DECBD3"),
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 3, 2.5 },
                    Margin = new Thickness(0, 2, 0, 2)
                });
            }

            Border footer = new Border
            {
                BorderBrush = SkinThemeManager.BrushFromHex(isCoffee ? "#E5CDB0" : "#D6D3D1"),
                BorderThickness = isCake ? new Thickness(0) : new Thickness(0, 1, 0, 0),
                Padding = isCake ? new Thickness(14, 6, 14, 2) : (isCoffee ? new Thickness(10, 6, 10, 2) : new Thickness(6, 6, 6, 2)),
                Margin = isCake ? new Thickness(0) : (isCoffee ? new Thickness(8, 2, 8, 0) : new Thickness(2, 2, 2, 0))
            };
            DockPanel footerDock = new DockPanel();

            StackPanel rightBtns = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(rightBtns, Dock.Right);

            Button collapseNoteBtn = BuildCollapseFooterButton(
                isCake,
                isCoffee,
                isThermal,
                isVintage,
                "收起关闭当前笔记（收纳回挂单绳，不删除内容）");
            rightBtns.Children.Add(collapseNoteBtn);

            Button delNoteBtn = new Button
            {
                Content = "🗑️ 删除笔记",
                Background = SkinThemeManager.BrushFromHex("#DC2626"),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Padding = new Thickness(12, 5, 12, 5),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = "将此笔记移入回收站"
            };
            if (isCake || isCoffee)
            {
                ApplyRoundedButtonTemplate(delNoteBtn, isCake ? 14 : 10);
            }
            delNoteBtn.Click += (s, e) =>
            {
                _ticket.Status = "Trash";
                SaveAndNotify();
                Hide();
            };
            rightBtns.Children.Add(delNoteBtn);
            footerDock.Children.Add(rightBtns);

            _wakeHintText = new TextBlock
            {
                Text = "📝 自动保存",
                Foreground = GetBrush("SubtleTextBrush", "#57534E"),
                FontSize = 11,
                Margin = new Thickness(0, 0, 6, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            footerDock.Children.Add(_wakeHintText);
            footer.Child = footerDock;
            body.Children.Add(footer);
        }

        private Button BuildCollapseFooterButton(bool isCake, bool isCoffee, bool isThermal, bool isVintage, string tooltip)
        {
            string bgHex = isCake
                ? "#F5DCE6"
                : (isCoffee ? "#EAD6BC" : (isThermal ? "#E2E8F0" : (isVintage ? "#E6D5B8" : "#FDE68A")));
            string fgHex = isCake
                ? "#6D1A36"
                : (isCoffee ? "#4E2A14" : (isThermal ? "#1E293B" : (isVintage ? "#4A3525" : "#451A03")));
            string borderHex = isCake
                ? "#E6B8C9"
                : (isCoffee ? "#CFAF88" : (isThermal ? "#94A3B8" : (isVintage ? "#C8B28A" : "#F59E0B")));

            Button collapseBtn = new Button
            {
                Content = "📥 收起",
                Background = SkinThemeManager.BrushFromHex(bgHex),
                Foreground = SkinThemeManager.BrushFromHex(fgHex),
                BorderBrush = SkinThemeManager.BrushFromHex(borderHex),
                BorderThickness = new Thickness(1),
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(0, 0, 6, 0),
                Cursor = Cursors.Hand,
                ToolTip = tooltip
            };
            if (isCake || isCoffee)
            {
                ApplyRoundedButtonTemplate(collapseBtn, isCake ? 14 : 10);
            }
            collapseBtn.Click += OnCollapseTicketClicked;
            return collapseBtn;
        }

        private UIElement BuildAttachedImagePanel(double maxHeight, double sideMargin = 4)
        {
            _attachedImageCard = new Border
            {
                Background = SkinThemeManager.BrushFromHex("#F8FAFC"),
                BorderBrush = SkinThemeManager.BrushFromHex("#E2E8F0"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(4),
                Margin = new Thickness(sideMargin, 2, sideMargin, 4),
                Visibility = Visibility.Collapsed
            };

            Grid imgGrid = new Grid();
            _attachedImageView = new Image
            {
                MaxHeight = maxHeight,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            imgGrid.Children.Add(_attachedImageView);

            Button removeImgOverlayBtn = new Button
            {
                Content = "× 删图",
                FontSize = 10.5,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(6, 2, 6, 2),
                Background = new SolidColorBrush(Color.FromArgb(215, 185, 28, 28)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Cursor = Cursors.Hand,
                ToolTip = "删除这张贴图"
            };
            removeImgOverlayBtn.Click += OnDeleteAttachedImage;
            imgGrid.Children.Add(removeImgOverlayBtn);

            _attachedImageCard.Child = imgGrid;
            LoadAttachedImageIfAny();
            return _attachedImageCard;
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

        public void FocusNoteBody()
        {
            if (_scratchBox != null)
            {
                _scratchBox.Focus();
                _scratchBox.CaretIndex = _scratchBox.Text != null ? _scratchBox.Text.Length : 0;
            }
        }
    }
}
