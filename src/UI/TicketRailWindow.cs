using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using StickyTickets.Data;
using StickyTickets.Interop;
using StickyTickets.Services;
using StickyTickets.Themes;

namespace StickyTickets.UI
{
    public class TicketRailWindow : Window
    {
        private readonly TicketRepository _repo;
        private readonly Dictionary<long, StickyTicketWindow> _openWindows = new Dictionary<long, StickyTicketWindow>();
        private readonly DispatcherTimer _tickTimer;

        private StackPanel _stubsPanel;
        private Grid _metalRodRow;
        private Border _railBar;
        private Button _tipJarBtn;
        private Button _skinSwitchBtn;
        private Button _unlockOrderBtn;
        private readonly HashSet<long> _collapsedRailTicketIds = new HashSet<long>();
        private readonly List<StickyTicketWindow> _orderedDockedVisibleWindows = new List<StickyTicketWindow>();
        private readonly Dictionary<long, string> _acknowledgedReminderSignatures = new Dictionary<long, string>();
        private readonly Dictionary<long, DeadlineReminderWindow> _openReminders = new Dictionary<long, DeadlineReminderWindow>();
        private OnboardingNoticeWindow _onboardingWindow;
        private int _reminderMinutes = 30;
        private string _activeSkinName = SkinThemeManager.BurgerShopSkin;
        private long _frontDockedTicketId = -1;
        private long _newTicketPendingFirstEditId = -1;
        private long _demotedFirstEditTicketId = -1;
        private long _demotedFirstEditUntilTicks = 0;
        private bool _allTicketsHidden;
        private bool _railManuallyHidden;
        private bool _railIsAutoHidden;
        private int _mouseOutsideTicks;
        private int _manualRevealGraceTicks;
        private bool _dockBottom;
        private bool _enableRedShake = true;
        private int _tickPhase;
        private ModifierWakePreset _wakePreset = ModifierWakePreset.CtrlAlt;

        public TicketRailWindow(TicketRepository repo)
        {
            _repo = repo;

            Title = "任务便签";
            System.Windows.Media.Imaging.BitmapImage logoImg = SkinThemeManager.LoadEmbeddedAppLogo();
            if (logoImg != null) Icon = logoImg;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            SizeToContent = SizeToContent.WidthAndHeight;
            ResizeMode = ResizeMode.NoResize;

            BuildRailChrome();
            Loaded += OnRailLoaded;
            Closed += OnRailClosed;

            _tickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
            _tickTimer.Tick += OnMasterTimerTick;
            _tickTimer.Start();

            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SystemEvents.TimeChanged += OnSystemTimeChanged;
        }

        private void BuildRailChrome()
        {
            Border railBar = new Border
            {
                Background = new LinearGradientBrush(
                    (Color)ColorConverter.ConvertFromString("#3F3F46"),
                    (Color)ColorConverter.ConvertFromString("#18181B"),
                    90),
                BorderBrush = SkinThemeManager.BrushFromHex("#F5B731"),
                BorderThickness = new Thickness(1.5, 0, 1.5, 2),
                CornerRadius = new CornerRadius(0, 0, 12, 12),
                Padding = new Thickness(10, 5, 10, 6)
            };

            StackPanel barRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            StackPanel brandGroup = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.SizeAll,
                Background = Brushes.Transparent,
                ToolTip = "按住可水平移动挂单绳位置（Ctrl+G 单独收起/呼出挂单绳 · Ctrl+H 隐藏/显示全部）"
            };
            System.Windows.Media.Imaging.BitmapImage brandLogo = SkinThemeManager.LoadEmbeddedAppLogo();
            if (brandLogo != null)
            {
                Image logoIcon = new Image
                {
                    Source = brandLogo,
                    Width = 18,
                    Height = 18,
                    Margin = new Thickness(0, 0, 5, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                RenderOptions.SetBitmapScalingMode(logoIcon, BitmapScalingMode.HighQuality);
                brandGroup.Children.Add(logoIcon);
            }
            TextBlock railTitle = new TextBlock
            {
                Text = "任务便签",
                Foreground = SkinThemeManager.BrushFromHex("#FDE68A"),
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            brandGroup.Children.Add(railTitle);
            brandGroup.MouseLeftButtonDown += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    try { DragMove(); } catch { }
                }
            };
            barRow.Children.Add(brandGroup);

            _stubsPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            barRow.Children.Add(_stubsPanel);

            _unlockOrderBtn = CreateRailButton("⇄ 自动排序", "#52525B", (s, e) =>
            {
                foreach (TicketRecord t in _repo.GetActiveTickets())
                {
                    if (t.IsOrderLocked)
                    {
                        t.IsOrderLocked = false;
                        _repo.SaveTicket(t);
                    }
                }
                ReloadRailAndWindows();
            });
            _unlockOrderBtn.Visibility = Visibility.Collapsed;
            barRow.Children.Add(_unlockOrderBtn);

            Button addBtn = CreateRailButton("+ 新建小票", "#D6372B", (s, e) =>
            {
                _allTicketsHidden = false;
                CreateNewTicket();
            });
            barRow.Children.Add(addBtn);

            Button addNoteBtn = CreateRailButton("📝 新建笔记", "#0F766E", (s, e) =>
            {
                _allTicketsHidden = false;
                CreateNewNote();
            });
            addNoteBtn.ToolTip = "新建一张空白可自由书写的便签笔记（沿用当前主题与挂单收纳）";
            barRow.Children.Add(addNoteBtn);

            Button recipeLibBtn = CreateRailButton("📖 配方库", "#7A1F1A", (s, e) =>
            {
                RecipeManagerWindow rmw = new RecipeManagerWindow(_repo);
                rmw.Owner = this;
                rmw.ShowDialog();
            });
            barRow.Children.Add(recipeLibBtn);

            _tipJarBtn = CreateRailButton("⭐ 积分 0", "#B45309", (s, e) => OpenReportAndSettings());
            _tipJarBtn.ToolTip = "点击打开「今日战报、每日积分档案与设置」";
            barRow.Children.Add(_tipJarBtn);

            _skinSwitchBtn = CreateRailButton("🍰 主题", "#DB2777", (s, e) =>
            {
                UserEconomyRecord eco = _repo.GetEconomy();
                eco.ActiveSkin = SkinThemeManager.NextThemeSkin(eco.ActiveSkin);
                _repo.SaveEconomy(eco);
                ReloadRailAndWindows();
            });
            _skinSwitchBtn.ToolTip = "点击循环切换：🍔 汉堡店 ➔ 🍰 蛋糕店 ➔ ☕ 咖啡店 ➔ 🧾 热敏纸 ➔ 📜 拟物复古小票";
            barRow.Children.Add(_skinSwitchBtn);

            Button dockToggleBtn = CreateRailButton("⇅ 顶底", "#3F3F46", (s, e) =>
            {
                _dockBottom = !_dockBottom;
                PositionRailNonFullWidth();
                ReloadRailAndWindows();
            });
            dockToggleBtn.ToolTip = "切换顶部 / 底部挂单绳位置";
            barRow.Children.Add(dockToggleBtn);

            Button exitBtn = CreateRailButton("×", "#7F1D1D", (s, e) => Application.Current.Shutdown());
            exitBtn.ToolTip = "安全保存并退出";
            barRow.Children.Add(exitBtn);

            _railBar = railBar;
            railBar.HorizontalAlignment = HorizontalAlignment.Center;
            railBar.Child = barRow;

            // Horizontal Metallic Order Hanging Rod (横向金属挂单杆)
            _metalRodRow = BuildHorizontalMetalRod();

            StackPanel railContainer = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            railContainer.Children.Add(railBar);
            railContainer.Children.Add(_metalRodRow);
            Content = railContainer;
        }

        private Grid BuildHorizontalMetalRod()
        {
            Grid rodGrid = new Grid
            {
                Height = 12,
                Margin = new Thickness(8, 3, 8, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            rodGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            rodGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rodGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            LinearGradientBrush steelBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };
            steelBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#F4F4F5"), 0.0));
            steelBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#D4D4D8"), 0.32));
            steelBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#71717A"), 0.76));
            steelBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#3F3F46"), 1.0));

            Border leftCap = new Border
            {
                Width = 9,
                Height = 11,
                CornerRadius = new CornerRadius(3),
                Background = SkinThemeManager.BrushFromHex("#D97706"),
                BorderBrush = SkinThemeManager.BrushFromHex("#78350F"),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(leftCap, 0);

            Border rodPipe = new Border
            {
                Height = 7,
                Background = steelBrush,
                BorderBrush = SkinThemeManager.BrushFromHex("#27272A"),
                BorderThickness = new Thickness(0, 0.8, 0, 1),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(rodPipe, 1);

            Border rightCap = new Border
            {
                Width = 9,
                Height = 11,
                CornerRadius = new CornerRadius(3),
                Background = SkinThemeManager.BrushFromHex("#D97706"),
                BorderBrush = SkinThemeManager.BrushFromHex("#78350F"),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(rightCap, 2);

            rodGrid.Children.Add(leftCap);
            rodGrid.Children.Add(rodPipe);
            rodGrid.Children.Add(rightCap);
            return rodGrid;
        }

        private Button CreateRailButton(string text, string bgHex, RoutedEventHandler onClick)
        {
            Button b = new Button
            {
                Content = text,
                Background = SkinThemeManager.BrushFromHex(bgHex),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 11.5,
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(4, 0, 0, 0),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            b.Click += onClick;
            return b;
        }

        private void OnRailLoaded(object sender, RoutedEventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            HwndSource src = HwndSource.FromHwnd(hwnd);
            if (src != null)
            {
                src.AddHook(WndProc);
                Win32Interop.RegisterCtrlH(hwnd);
                Win32Interop.RegisterCtrlG(hwnd);
            }
            ReloadRailAndWindows();
            PositionRailNonFullWidth();
            ShowOnboardingNoticeIfFirstRun();
        }

        private void ShowOnboardingNoticeIfFirstRun()
        {
            UserEconomyRecord eco = _repo.GetEconomy();
            if (eco.OnboardingNoticeAcked) return;

            string skin = string.IsNullOrWhiteSpace(eco.ActiveSkin) ? SkinThemeManager.BurgerShopSkin : eco.ActiveSkin;
            _onboardingWindow = new OnboardingNoticeWindow(skin);
            _onboardingWindow.OnAcknowledged = () =>
            {
                UserEconomyRecord latest = _repo.GetEconomy();
                latest.OnboardingNoticeAcked = true;
                _repo.SaveEconomy(latest);
                _onboardingWindow = null;
            };
            _onboardingWindow.Show();
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == Win32Interop.WM_HOTKEY)
            {
                int hotkeyId = wParam.ToInt32();
                if (hotkeyId == Win32Interop.HOTKEY_ID_HIDE_ALL)
                {
                    ToggleHideAllTickets();
                    handled = true;
                }
                else if (hotkeyId == Win32Interop.HOTKEY_ID_HIDE_RAIL_ONLY)
                {
                    ToggleHideRailOnly();
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        public void ToggleHideAllTickets()
        {
            _allTicketsHidden = !_allTicketsHidden;
            if (!_allTicketsHidden)
            {
                _railManuallyHidden = false;
                _mouseOutsideTicks = 0;
                _manualRevealGraceTicks = 30;
                SetRailAutoHidden(false);
            }
            ReloadRailAndWindows();
        }

        /// <summary>
        /// Ctrl+G: Toggles hiding ONLY the hanging rail bar while keeping all currently open tickets/notes visible on the desktop.
        /// </summary>
        public void ToggleHideRailOnly()
        {
            if (!_railIsAutoHidden)
            {
                _railManuallyHidden = true;
                SetRailAutoHidden(true);
            }
            else
            {
                _railManuallyHidden = false;
                _mouseOutsideTicks = 0;
                _manualRevealGraceTicks = 40;
                SetRailAutoHidden(false);
                ApplyDockedStackZOrder();
            }
        }

        /// <summary>
        /// Places the non-full-width rail centered horizontally at top or bottom of work area,
        /// avoiding browser tab close buttons and screen corners.
        /// </summary>
        private void PositionRailNonFullWidth()
        {
            UpdateLayout();
            Rect wa = SystemParameters.WorkArea;
            double w = ActualWidth > 0 ? ActualWidth : 520;
            Left = wa.Left + (wa.Width - w) / 2.0;
            Top = _dockBottom ? (wa.Bottom - ActualHeight) : wa.Top;
        }

        public void ReloadRailAndWindows()
        {
            UserEconomyRecord eco = _repo.GetEconomy();
            _reminderMinutes = eco.ReminderMinutes;
            _activeSkinName = string.IsNullOrWhiteSpace(eco.ActiveSkin) ? SkinThemeManager.BurgerShopSkin : eco.ActiveSkin;
            if (_onboardingWindow != null && _onboardingWindow.IsVisible)
            {
                _onboardingWindow.ApplySkin(_activeSkinName);
            }
            if (_tipJarBtn != null)
            {
                _tipJarBtn.Content = "⭐ 积分 " + eco.TotalTips;
                _tipJarBtn.Visibility = Visibility.Visible;
            }
            if (_skinSwitchBtn != null)
            {
                if (SkinThemeManager.IsCakeShop(eco.ActiveSkin))
                    _skinSwitchBtn.Content = "🍰 主题";
                else if (SkinThemeManager.IsCoffeeShop(eco.ActiveSkin))
                    _skinSwitchBtn.Content = "☕ 主题";
                else if (SkinThemeManager.IsThermalPaper(eco.ActiveSkin))
                    _skinSwitchBtn.Content = "🧾 主题";
                else if (SkinThemeManager.IsVintageRetro(eco.ActiveSkin))
                    _skinSwitchBtn.Content = "📜 主题";
                else
                    _skinSwitchBtn.Content = "🍔 主题";
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            List<TicketRecord> rawAll = _repo.GetActiveTickets();
            List<TicketRecord> all = new List<TicketRecord>();
            foreach (TicketRecord item in rawAll)
            {
                if (item.Status == "Served")
                {
                    StickyTicketWindow servedWin;
                    if (_openWindows.TryGetValue(item.Id, out servedWin) && servedWin.IsVisible)
                    {
                        servedWin.Hide();
                    }
                    continue;
                }
                all.Add(item);
            }

            // v2 Sorting rule:
            // - Served tickets go to the right.
            // - Red urgent tickets ALWAYS move to the front (overriding IsOrderLocked).
            // - Locked tickets preserve their manual RailOrder relative to other locked tickets.
            // - Unlocked tickets sort by urgency descending (Orange -> Yellow -> Green).
            all.Sort((a, b) =>
            {
                bool aServed = a.Status == "Served";
                bool bServed = b.Status == "Served";
                if (aServed != bServed) return aServed ? 1 : -1;

                UrgencyState ua = a.ComputeUrgency(now);
                UrgencyState ub = b.ComputeUrgency(now);

                bool aRed = ua == UrgencyState.Red;
                bool bRed = ub == UrgencyState.Red;
                if (aRed != bRed) return aRed ? -1 : 1;

                if (a.IsOrderLocked && b.IsOrderLocked)
                    return a.RailOrder.CompareTo(b.RailOrder);

                int uCmp = ub.CompareTo(ua);
                if (uCmp != 0) return uCmp;
                return a.RailOrder.CompareTo(b.RailOrder);
            });

            int totalDockedCount = 0;
            int totalExpandedDocked = 0;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].IsDockedOnRail)
                {
                    totalDockedCount++;
                    if (!_collapsedRailTicketIds.Contains(all[i].Id))
                    {
                        totalExpandedDocked++;
                    }
                }
            }

            if (_metalRodRow != null)
            {
                if (totalExpandedDocked > 0 && !_allTicketsHidden)
                {
                    double stepPreview = ComputeDockedStepX(totalExpandedDocked, SystemParameters.WorkArea);
                    double rowSpan = 356.0 + Math.Max(0, totalExpandedDocked - 1) * stepPreview;
                    _metalRodRow.MinWidth = Math.Max(0, rowSpan - 20.0);
                    _metalRodRow.Visibility = Visibility.Visible;
                }
                else
                {
                    _metalRodRow.MinWidth = 0;
                    _metalRodRow.Visibility = Visibility.Visible;
                }
                PositionRailNonFullWidth();
            }

            bool anyLocked = false;
            _stubsPanel.Children.Clear();
            _orderedDockedVisibleWindows.Clear();
            int dockedSlotIndex = 0;
            List<Point> occupiedDesktopPositions = new List<Point>();

            for (int i = 0; i < all.Count; i++)
            {
                TicketRecord t = all[i];
                if (t.IsOrderLocked) anyLocked = true;

                if (t.IsDockedOnRail)
                {
                    _stubsPanel.Children.Add(BuildRailStub(t, now, totalExpandedDocked, totalDockedCount));
                }

                StickyTicketWindow win;
                if (!_openWindows.TryGetValue(t.Id, out win))
                {
                    win = new StickyTicketWindow(_repo, t, eco.ActiveSkin, ReloadRailAndWindows);
                    win.OnDockedClicked = OnDockedTicketClicked;
                    win.OnFirstEditCompleted = OnTicketFirstEditCompleted;
                    win.OnCollapseRequested = CollapseSingleTicket;
                    win.IsRailVisibleQuery = () => !_railIsAutoHidden;
                    _openWindows[t.Id] = win;
                }
                else
                {
                    win.OnDockedClicked = OnDockedTicketClicked;
                    win.OnFirstEditCompleted = OnTicketFirstEditCompleted;
                    win.OnCollapseRequested = CollapseSingleTicket;
                    win.IsRailVisibleQuery = () => !_railIsAutoHidden;
                    win.ApplySkin(eco.ActiveSkin);
                }

                if (_allTicketsHidden)
                {
                    if (win.IsVisible) win.Hide();
                }
                else if (!t.IsDockedOnRail)
                {
                    StaggerOverlappingDesktopWindow(win, occupiedDesktopPositions);
                    if (!win.IsVisible) win.Show();
                }
                else
                {
                    if (!_collapsedRailTicketIds.Contains(t.Id))
                    {
                        PositionDockedTicketWindow(win, dockedSlotIndex, totalExpandedDocked);
                        dockedSlotIndex++;
                        if (!win.IsVisible) win.Show();
                        _orderedDockedVisibleWindows.Add(win);
                    }
                    else
                    {
                        if (win.IsVisible) win.Hide();
                    }
                }
            }

            ApplyDockedStackZOrder();
            SyncOpenRemindersWithActiveTickets(all);

            if (_unlockOrderBtn != null)
            {
                _unlockOrderBtn.Visibility = anyLocked ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void SyncOpenRemindersWithActiveTickets(List<TicketRecord> activeTickets)
        {
            if (_openReminders.Count == 0) return;

            HashSet<long> activeIds = new HashSet<long>();
            if (_reminderMinutes > 0 && activeTickets != null)
            {
                for (int i = 0; i < activeTickets.Count; i++)
                {
                    if (activeTickets[i].Status == "Active" && !activeTickets[i].IsStickyNote)
                    {
                        activeIds.Add(activeTickets[i].Id);
                    }
                }
            }

            List<long> staleIds = new List<long>();
            foreach (KeyValuePair<long, DeadlineReminderWindow> kv in _openReminders)
            {
                if (!activeIds.Contains(kv.Key) || kv.Value == null)
                {
                    staleIds.Add(kv.Key);
                }
                else
                {
                    kv.Value.ApplySkin(_activeSkinName);
                }
            }

            for (int i = 0; i < staleIds.Count; i++)
            {
                DeadlineReminderWindow staleWin;
                if (_openReminders.TryGetValue(staleIds[i], out staleWin) && staleWin != null)
                {
                    try { staleWin.Close(); } catch { }
                }
                _openReminders.Remove(staleIds[i]);
            }
        }

        private void ClearPendingFirstEditState(long exceptTicketId = -1)
        {
            if (_newTicketPendingFirstEditId > 0 && _newTicketPendingFirstEditId != exceptTicketId)
            {
                StickyTicketWindow pendingWin;
                if (_openWindows.TryGetValue(_newTicketPendingFirstEditId, out pendingWin) && pendingWin != null)
                {
                    pendingWin.ClearAwaitingFirstEdit();
                }
                _newTicketPendingFirstEditId = -1;
            }
        }

        private void OnTicketFirstEditCompleted(long ticketId)
        {
            if (_newTicketPendingFirstEditId == ticketId)
            {
                _newTicketPendingFirstEditId = -1;
            }
            if (_frontDockedTicketId == ticketId)
            {
                _frontDockedTicketId = -1;
            }
            _demotedFirstEditTicketId = ticketId;
            _demotedFirstEditUntilTicks = DateTime.UtcNow.Ticks + TimeSpan.FromMilliseconds(350).Ticks;

            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, new Action(() =>
            {
                ApplyDockedStackZOrder();
            }));
        }

        private void CollapseSingleTicket(long ticketId)
        {
            if (_newTicketPendingFirstEditId == ticketId)
            {
                _newTicketPendingFirstEditId = -1;
            }
            if (_frontDockedTicketId == ticketId)
            {
                _frontDockedTicketId = -1;
            }

            StickyTicketWindow win;
            if (_openWindows.TryGetValue(ticketId, out win) && win != null && win.Ticket != null)
            {
                if (!win.Ticket.IsDockedOnRail || win.Ticket.IsClickThrough)
                {
                    win.Ticket.IsDockedOnRail = true;
                    win.Ticket.IsClickThrough = false;
                    win.ApplyClickThroughState();
                    _repo.SaveTicket(win.Ticket);
                }
                if (win.IsVisible)
                {
                    win.Hide();
                }
            }

            _collapsedRailTicketIds.Add(ticketId);
            ReloadRailAndWindows();
        }

        private void OnDockedTicketClicked(long clickedTicketId)
        {
            if (clickedTicketId == _demotedFirstEditTicketId && DateTime.UtcNow.Ticks < _demotedFirstEditUntilTicks)
            {
                ApplyDockedStackZOrder();
                return;
            }
            ClearPendingFirstEditState(clickedTicketId);
            _frontDockedTicketId = clickedTicketId;
            ApplyDockedStackZOrder();
        }

        /// <summary>
        /// Enforces deterministic Z-order:
        /// 1. TicketRailWindow on top
        /// 2. Currently selected/clicked docked ticket (_frontDockedTicketId) pulled to the very front
        /// 3. All other docked tickets tucked cleanly back into their natural right-to-left stack slots
        /// </summary>
        private void ApplyDockedStackZOrder()
        {
            if (_orderedDockedVisibleWindows.Count == 0) return;

            List<Window> frontToBack = new List<Window>();
            if (!_railIsAutoHidden)
            {
                frontToBack.Add(this);
            }

            StickyTicketWindow pulledFront = null;
            if (_frontDockedTicketId > 0)
            {
                for (int i = 0; i < _orderedDockedVisibleWindows.Count; i++)
                {
                    if (_orderedDockedVisibleWindows[i].Ticket.Id == _frontDockedTicketId)
                    {
                        pulledFront = _orderedDockedVisibleWindows[i];
                        break;
                    }
                }
            }

            if (pulledFront != null)
            {
                frontToBack.Add(pulledFront);
            }

            for (int i = _orderedDockedVisibleWindows.Count - 1; i >= 0; i--)
            {
                StickyTicketWindow w = _orderedDockedVisibleWindows[i];
                if (w != pulledFront)
                {
                    frontToBack.Add(w);
                }
            }

            Win32Interop.ApplyDeterministicTopmostZOrder(frontToBack);
        }

        private UIElement BuildRailStub(TicketRecord t, long nowUnix, int totalExpandedDocked, int totalDockedCount)
        {
            UrgencyState u = t.ComputeUrgency(nowUnix);
            string shapeLabel;
            string rem = t.FormatRemainingTime(nowUnix, out shapeLabel);
            bool isExpanded = !_collapsedRailTicketIds.Contains(t.Id) && !_allTicketsHidden;

            string borderHex = isExpanded ? "#F5B731" : "#71717A";
            string dotHex = u == UrgencyState.Red ? "#EF4444" :
                            (u == UrgencyState.Orange ? "#F97316" :
                            (u == UrgencyState.Yellow ? "#FACC15" : "#10B981"));

            Border stub = new Border
            {
                Background = SkinThemeManager.BrushFromHex(isExpanded ? "#FFFDF7" : "#D4D4D8"),
                BorderBrush = SkinThemeManager.BrushFromHex(borderHex),
                BorderThickness = new Thickness(isExpanded ? 2 : 1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(7, 3, 7, 3),
                Margin = new Thickness(0, 0, 5, 0),
                Cursor = Cursors.Hand,
                ToolTip = "左键切换抽到最前/收起该小票；右键手动锁定位置并左移"
            };

            double maxStubTextWidth = totalDockedCount >= 6 ? 82 : (totalDockedCount == 5 ? 100 : (totalDockedCount == 4 ? 120 : 155));

            StackPanel sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(new TextBlock
            {
                Text = t.IsStickyNote ? "📝" : shapeLabel.Substring(0, 1),
                Foreground = SkinThemeManager.BrushFromHex(t.IsStickyNote ? "#0D9488" : dotHex),
                FontWeight = FontWeights.Black,
                FontSize = 11,
                Margin = new Thickness(0, 0, 4, 0)
            });
            sp.Children.Add(new TextBlock
            {
                Text = t.IsStickyNote
                    ? string.Format("{0} {1}", t.TicketNo.Replace("📝", ""), t.Title)
                    : string.Format("{0} {1} ({2})", t.TicketNo, t.Title, rem),
                Foreground = SkinThemeManager.BrushFromHex("#18181B"),
                FontWeight = FontWeights.Bold,
                FontSize = 11.5,
                MaxWidth = maxStubTextWidth,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            stub.Child = sp;

            stub.MouseLeftButtonDown += (s, e) =>
            {
                _allTicketsHidden = false;
                ClearPendingFirstEditState(t.Id);
                if (_collapsedRailTicketIds.Contains(t.Id))
                {
                    _collapsedRailTicketIds.Remove(t.Id);
                    _frontDockedTicketId = t.Id;
                }
                else if (totalExpandedDocked >= 5 && _frontDockedTicketId != t.Id)
                {
                    // In overlapped card-deck mode (>=5), first click pulls card to front; second click collapses it
                    _frontDockedTicketId = t.Id;
                }
                else
                {
                    _collapsedRailTicketIds.Add(t.Id);
                    if (_frontDockedTicketId == t.Id) _frontDockedTicketId = -1;
                    if (_newTicketPendingFirstEditId == t.Id) _newTicketPendingFirstEditId = -1;
                }
                ReloadRailAndWindows();
            };

            stub.MouseRightButtonDown += (s, e) =>
            {
                t.IsOrderLocked = true;
                t.RailOrder = Math.Max(0, t.RailOrder - 1);
                _repo.SaveTicket(t);
                ReloadRailAndWindows();
            };

            return stub;
        }

        private double ComputeDockedStepX(int totalCount, Rect wa)
        {
            double ticketWidth = 356.0;
            if (totalCount <= 1) return ticketWidth;

            // Only start overlapping card-deck storage when docked tickets >= 5!
            if (totalCount < 5)
            {
                double idealSideBySide = ticketWidth + 8.0;
                double maxFitWidth = Math.Max(ticketWidth + 40.0, wa.Width - 20.0);
                if (ticketWidth + (totalCount - 1) * idealSideBySide <= maxFitWidth)
                {
                    return idealSideBySide;
                }
                return (maxFitWidth - ticketWidth) / (totalCount - 1);
            }

            // 5+ tickets: Overlapping card-deck storage matching reference image 2
            double maxDeckWidth = Math.Min(wa.Width - 48.0, Math.Min(1160.0, 820.0 + (totalCount - 5) * 65.0));
            return Math.Max(72.0, (maxDeckWidth - ticketWidth) / (totalCount - 1));
        }

        private void PositionDockedTicketWindow(StickyTicketWindow win, int slotIndex, int totalCount)
        {
            Rect wa = SystemParameters.WorkArea;
            double ticketWidth = 356.0;
            double stepX = ComputeDockedStepX(totalCount, wa);

            double totalRowWidth = ticketWidth + Math.Max(0, totalCount - 1) * stepX;
            double railCenter = (Left > 0 ? Left : (wa.Left + wa.Width / 2.0)) + (ActualWidth > 0 ? ActualWidth / 2.0 : 240.0);
            double startX = railCenter - totalRowWidth / 2.0;
            startX = Math.Max(wa.Left + 16.0, Math.Min(startX, wa.Right - totalRowWidth - 16.0));

            double targetLeft = startX + slotIndex * stepX;
            // Completely flush top alignment (齐平, zero vertical stagger), clamping right onto the horizontal metal rod
            double rodBottom = (Top > 0 ? Top : wa.Top) + (ActualHeight > 0 ? ActualHeight : 48);
            double targetTop = _dockBottom ? Math.Max(wa.Top + 16, Top - 440) : (rodBottom - 11.0);

            win.Left = targetLeft;
            win.Top = targetTop;
            win.Ticket.PosX = targetLeft;
            win.Ticket.PosY = targetTop;
        }

        private void StaggerOverlappingDesktopWindow(StickyTicketWindow win, List<Point> occupied)
        {
            Rect wa = SystemParameters.WorkArea;
            double x = win.Left;
            double y = win.Top;
            bool adjusted = false;

            for (int attempt = 0; attempt < 10; attempt++)
            {
                bool clash = false;
                for (int i = 0; i < occupied.Count; i++)
                {
                    if (Math.Abs(occupied[i].X - x) < 42.0 && Math.Abs(occupied[i].Y - y) < 32.0)
                    {
                        clash = true;
                        break;
                    }
                }
                if (!clash) break;
                x += 48.0;
                y += 24.0;
                if (x + 356.0 > wa.Right - 12.0) x = wa.Left + 36.0;
                if (y + 400.0 > wa.Bottom - 12.0) y = wa.Top + 56.0;
                adjusted = true;
            }

            if (adjusted)
            {
                win.Left = x;
                win.Top = y;
                win.Ticket.PosX = x;
                win.Ticket.PosY = y;
            }
            occupied.Add(new Point(x, y));
        }

        private void CreateNewTicket()
        {
            List<TicketRecord> existing = _repo.GetActiveTickets();
            int nextNum = 41 + existing.Count;
            for (int i = 0; i < existing.Count; i++)
            {
                string tno = existing[i].TicketNo;
                if (!string.IsNullOrEmpty(tno) && tno.StartsWith("#"))
                {
                    int parsed;
                    if (int.TryParse(tno.Substring(1), out parsed) && parsed >= nextNum)
                    {
                        nextNum = parsed + 1;
                    }
                }
            }
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            TicketRecord nt = new TicketRecord
            {
                TicketNo = "#" + nextNum.ToString("D3"),
                Title = "新待办小票",
                ScratchNotes = "",
                IsDockedOnRail = true,
                RailOrder = existing.Count,
                CreatedAtUnix = now,
                StartTimeUnix = now,
                DeadlineUnix = now + 1800,
                Steps = new List<TicketStepRecord>
                {
                    new TicketStepRecord { SortOrder = 0, StepType = "core", Content = "点击修改或在下框按 Enter 新增工序", IsCompleted = false }
                }
            };
            _repo.SaveTicket(nt);
            ClearPendingFirstEditState();
            _collapsedRailTicketIds.Remove(nt.Id);
            _newTicketPendingFirstEditId = nt.Id;
            _frontDockedTicketId = nt.Id;
            ReloadRailAndWindows();
            StickyTicketWindow newWin;
            if (_openWindows.TryGetValue(nt.Id, out newWin) && newWin != null && newWin.IsVisible)
            {
                newWin.MarkAwaitingFirstEdit();
                try
                {
                    newWin.Activate();
                    newWin.FocusInitialTicketInput();
                }
                catch { }
                ApplyDockedStackZOrder();
            }
        }

        private void CreateNewNote()
        {
            List<TicketRecord> existing = _repo.GetActiveTickets();
            int noteCount = 1;
            foreach (TicketRecord item in existing)
            {
                if (item.IsStickyNote)
                {
                    noteCount++;
                    string stripped = item.TicketNo.Replace("📝#", "").Replace("NOTE#", "");
                    int parsed;
                    if (int.TryParse(stripped, out parsed) && parsed >= noteCount)
                    {
                        noteCount = parsed + 1;
                    }
                }
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            TicketRecord note = new TicketRecord
            {
                TicketNo = "📝#" + noteCount.ToString("D2"),
                Title = "空白便签",
                ScratchNotes = "",
                IsDockedOnRail = true,
                RailOrder = existing.Count,
                CreatedAtUnix = now,
                StartTimeUnix = 0,
                DeadlineUnix = 0,
                Steps = new List<TicketStepRecord>()
            };
            _repo.SaveTicket(note);
            ClearPendingFirstEditState();
            _collapsedRailTicketIds.Remove(note.Id);
            _newTicketPendingFirstEditId = note.Id;
            _frontDockedTicketId = note.Id;
            ReloadRailAndWindows();

            StickyTicketWindow newWin;
            if (_openWindows.TryGetValue(note.Id, out newWin) && newWin != null && newWin.IsVisible)
            {
                newWin.MarkAwaitingFirstEdit();
                try
                {
                    newWin.Activate();
                    newWin.FocusNoteBody();
                }
                catch { }
                ApplyDockedStackZOrder();
            }
        }

        private void OpenReportAndSettings()
        {
            DailyReportAndWishDialog dlg = new DailyReportAndWishDialog(_repo, _wakePreset, () =>
            {
                ReloadRailAndWindows();
            });
            dlg.Owner = this;
            dlg.ShowDialog();
            _wakePreset = dlg.SelectedWakePreset;
        }

        private void SetRailAutoHidden(bool autoHide)
        {
            if (_railIsAutoHidden == autoHide) return;
            _railIsAutoHidden = autoHide;

            if (autoHide)
            {
                Win32Interop.SetWindowClickThrough(this, true, hideFromAltTab: false);
                System.Windows.Media.Animation.DoubleAnimation fadeOut = new System.Windows.Media.Animation.DoubleAnimation
                {
                    To = 0.0,
                    Duration = new Duration(TimeSpan.FromMilliseconds(160))
                };
                BeginAnimation(Window.OpacityProperty, fadeOut);
            }
            else
            {
                Win32Interop.SetWindowClickThrough(this, false, hideFromAltTab: false);
                System.Windows.Media.Animation.DoubleAnimation fadeIn = new System.Windows.Media.Animation.DoubleAnimation
                {
                    To = 1.0,
                    Duration = new Duration(TimeSpan.FromMilliseconds(110))
                };
                BeginAnimation(Window.OpacityProperty, fadeIn);
            }
        }

        private void UpdateRailEdgeAutoHide()
        {
            if (OwnedWindows != null && OwnedWindows.Count > 0)
            {
                _mouseOutsideTicks = 0;
                SetRailAutoHidden(false);
                return;
            }

            if (_railManuallyHidden)
            {
                SetRailAutoHidden(true);
                return;
            }

            if (_manualRevealGraceTicks > 0)
            {
                _manualRevealGraceTicks--;
                _mouseOutsideTicks = 0;
                SetRailAutoHidden(false);
                return;
            }

            bool anyTicketVisible = false;
            foreach (StickyTicketWindow w in _openWindows.Values)
            {
                if (w != null && w.IsVisible)
                {
                    anyTicketVisible = true;
                    break;
                }
            }

            bool ticketsHiddenState = _allTicketsHidden || !anyTicketVisible;
            if (!ticketsHiddenState)
            {
                _mouseOutsideTicks = 0;
                SetRailAutoHidden(false);
                return;
            }

            Rect wa = SystemParameters.WorkArea;
            Point mousePt = Win32Interop.GetMouseScreenPosition(this);
            double railLeft = Left > 0 ? Left : (wa.Left + wa.Width * 0.25);
            double railRight = railLeft + (ActualWidth > 0 ? ActualWidth : 580.0);
            double sensorLeft = Math.Min(railLeft - 80.0, wa.Left + wa.Width * 0.18);
            double sensorRight = Math.Max(railRight + 80.0, wa.Left + wa.Width * 0.82);

            bool atEdgeSensor = _dockBottom
                ? (mousePt.Y >= wa.Bottom - 10.0 && mousePt.X >= sensorLeft && mousePt.X <= sensorRight)
                : (mousePt.Y <= wa.Top + 10.0 && mousePt.X >= sensorLeft && mousePt.X <= sensorRight);

            bool insideRailBounds = !_railIsAutoHidden &&
                mousePt.X >= (railLeft - 32.0) &&
                mousePt.X <= (railRight + 32.0) &&
                (_dockBottom
                    ? (mousePt.Y >= Top - 24.0)
                    : (mousePt.Y <= Top + (ActualHeight > 0 ? ActualHeight : 48.0) + 24.0));

            if (atEdgeSensor || insideRailBounds)
            {
                _mouseOutsideTicks = 0;
                SetRailAutoHidden(false);
            }
            else
            {
                _mouseOutsideTicks++;
                if (_mouseOutsideTicks >= 3)
                {
                    SetRailAutoHidden(true);
                }
            }
        }

        private void OnMasterTimerTick(object sender, EventArgs e)
        {
            _tickPhase++;
            IntPtr ownHwnd = new WindowInteropHelper(this).Handle;

            // 0. Edge auto-hide / snap reveal when tickets are hidden
            UpdateRailEdgeAutoHide();

            // 1. Fullscreen Do-Not-Disturb detection
            bool isFullScreen = Win32Interop.IsForegroundFullScreen(ownHwnd);
            AudioAndUpdateService.IsMuted = isFullScreen;

            // 2. Read-only modifier key check for waking click-through tickets
            bool wakeHeld = Win32Interop.AreWakeModifiersPressed(_wakePreset);

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            double shakeAngle = ((_tickPhase / 2) % 2 == 0) ? -1.2 : 1.2;

            foreach (KeyValuePair<long, StickyTicketWindow> kv in _openWindows)
            {
                kv.Value.SetModifierWakeActive(wakeHeld);
                kv.Value.RefreshClockAndProgress(now, _enableRedShake, shakeAngle);
            }

            // 3. Check upcoming deadline reminders (~once per second)
            if (_tickPhase % 8 == 0)
            {
                CheckAndShowDeadlineReminders(now);
            }
        }

        private void CheckAndShowDeadlineReminders(long now)
        {
            if (_openReminders.Count > 0)
            {
                List<long> closedIds = null;
                foreach (KeyValuePair<long, DeadlineReminderWindow> remKv in _openReminders)
                {
                    if (remKv.Value == null || !remKv.Value.IsVisible)
                    {
                        if (closedIds == null) closedIds = new List<long>();
                        closedIds.Add(remKv.Key);
                    }
                    else
                    {
                        remKv.Value.RefreshCountdown(now);
                    }
                }
                if (closedIds != null)
                {
                    for (int i = 0; i < closedIds.Count; i++)
                    {
                        _openReminders.Remove(closedIds[i]);
                    }
                }
            }

            if (_reminderMinutes <= 0) return;
            long thresholdSec = (long)_reminderMinutes * 60L;

            foreach (KeyValuePair<long, StickyTicketWindow> kv in _openWindows)
            {
                StickyTicketWindow win = kv.Value;
                if (win == null) continue;
                TicketRecord t = win.Ticket;
                if (t == null || t.IsStickyNote || t.Status != "Active" || t.DeadlineUnix <= 0)
                    continue;

                // Do not pop up over a brand-new blank placeholder ticket while the user hasn't titled it yet
                if (t.Title == "新待办小票" || t.Title == "新建待办小票")
                    continue;

                long remSec = t.DeadlineUnix - now;
                if (remSec <= 0 || remSec > thresholdSec)
                    continue;

                string sig = string.Format("{0}:{1}", t.DeadlineUnix, _reminderMinutes);
                string ackedSig;
                if (_acknowledgedReminderSignatures.TryGetValue(t.Id, out ackedSig) && ackedSig == sig)
                    continue;

                _acknowledgedReminderSignatures[t.Id] = sig;

                DeadlineReminderWindow existingPopup;
                if (_openReminders.TryGetValue(t.Id, out existingPopup) && existingPopup != null)
                {
                    try { existingPopup.Close(); } catch { }
                    _openReminders.Remove(t.Id);
                }

                long capturedId = t.Id;
                int staggerIdx = _openReminders.Count;
                DeadlineReminderWindow popup = new DeadlineReminderWindow(t, _reminderMinutes, _activeSkinName, staggerIdx);
                popup.OnDismissed = () =>
                {
                    _openReminders.Remove(capturedId);
                };
                _openReminders[t.Id] = popup;
                AudioAndUpdateService.PlayUrgentAlert();
                popup.Show();
            }
        }

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    PositionRailNonFullWidth();
                    ReloadRailAndWindows();
                }));
            }
        }

        private void OnSystemTimeChanged(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(ReloadRailAndWindows));
        }

        private void OnRailClosed(object sender, EventArgs e)
        {
            _tickTimer.Stop();
            IntPtr closeHwnd = new WindowInteropHelper(this).Handle;
            Win32Interop.UnregisterCtrlH(closeHwnd);
            Win32Interop.UnregisterCtrlG(closeHwnd);
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.TimeChanged -= OnSystemTimeChanged;
            if (_onboardingWindow != null)
            {
                try { _onboardingWindow.Close(); } catch { }
                _onboardingWindow = null;
            }
            foreach (DeadlineReminderWindow rw in _openReminders.Values)
            {
                try { rw.Close(); } catch { }
            }
            _openReminders.Clear();
            foreach (StickyTicketWindow w in _openWindows.Values)
            {
                try { w.Close(); } catch { }
            }
        }
    }
}
