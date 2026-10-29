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
        private Button CreateMiniActionButton(string label, RoutedEventHandler handler)
        {
            Button b = new Button
            {
                Content = label,
                FontSize = 11,
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(0, 0, 4, 4),
                Cursor = Cursors.Hand
            };
            b.Click += handler;
            return b;
        }

        private void RebuildStepsList()
        {
            if (_stepsPanel == null) return;
            _stepsPanel.Children.Clear();
            bool isCake = IsCakeTheme();
            bool isCoffee = IsCoffeeTheme();
            bool isVintage = IsVintageTheme();
            bool isThermal = IsThermalTheme();

            for (int i = 0; i < _ticket.Steps.Count; i++)
            {
                int idx = i;
                TicketStepRecord st = _ticket.Steps[i];

                Border rowBorder = new Border
                {
                    Background = isVintage
                        ? SkinThemeManager.BrushFromHex("#FAF3E0")
                        : (isThermal ? SkinThemeManager.BrushFromHex("#F8FAFC") : (isCake ? SkinThemeManager.BrushFromHex("#FFFBFD") : (isCoffee ? SkinThemeManager.BrushFromHex("#FFFCF5") : Brushes.White))),
                    BorderBrush = isVintage
                        ? SkinThemeManager.BrushFromHex("#8BAAC9")
                        : SkinThemeManager.BrushFromHex(isThermal ? "#CBD5E1" : (isCake ? "#EFE2E7" : (isCoffee ? "#EAD6BC" : "#E7E5E4"))),
                    BorderThickness = isVintage ? new Thickness(0, 0, 0, 1.2) : new Thickness(1),
                    CornerRadius = new CornerRadius(isVintage ? 0 : (isThermal ? 3 : 8)),
                    Padding = isVintage ? new Thickness(6, 6, 6, 5) : new Thickness(8, 6, 8, 6),
                    Margin = isVintage ? new Thickness(2, 0, 2, 0) : new Thickness(0, 0, 0, 5),
                    Opacity = st.IsCompleted ? 0.58 : 1.0
                };

                Grid rowGrid = new Grid();
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                CheckBox cb = new CheckBox
                {
                    IsChecked = st.IsCompleted,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 6, 0),
                    Cursor = Cursors.Hand
                };
                cb.Click += (s, e) =>
                {
                    st.IsCompleted = cb.IsChecked == true;
                    AudioAndUpdateService.PlayStepCheckSound();
                    SaveAndNotify();
                    RebuildStepsList();
                };
                Grid.SetColumn(cb, 0);
                rowGrid.Children.Add(cb);

                // Pure vector icon (NO text suffix on ticket face!)
                Border iconBtn = new Border
                {
                    Background = Brushes.Transparent,
                    Padding = new Thickness(2),
                    Margin = new Thickness(0, 0, isVintage ? 4 : 6, 0),
                    Cursor = Cursors.Hand,
                    Child = SkinThemeManager.CreatePureStepVectorIcon(st.StepType, _currentSkinName, st.Content)
                };
                iconBtn.MouseLeftButtonDown += (s, e) =>
                {
                    e.Handled = true;
                    st.StepType = SkinThemeManager.NextStepType(st.StepType);
                    SaveAndNotify();
                    RebuildStepsList();
                };
                Grid.SetColumn(iconBtn, 1);
                rowGrid.Children.Add(iconBtn);

                // Classic Vintage Diner Guest-Check Vertical Double Red Margin Column Line
                if (isVintage)
                {
                    StackPanel marginLines = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Margin = new Thickness(1, -6, 7, -5),
                        VerticalAlignment = VerticalAlignment.Stretch
                    };
                    marginLines.Children.Add(new Border
                    {
                        Width = 1.1,
                        Background = SkinThemeManager.BrushFromHex("#C53030"),
                        Opacity = 0.72
                    });
                    marginLines.Children.Add(new Border
                    {
                        Width = 0.9,
                        Margin = new Thickness(1.8, 0, 0, 0),
                        Background = SkinThemeManager.BrushFromHex("#E53E3E"),
                        Opacity = 0.58
                    });
                    Grid.SetColumn(marginLines, 2);
                    rowGrid.Children.Add(marginLines);
                }

                TextBlock contentBlock = new TextBlock
                {
                    Text = st.Content,
                    Foreground = GetBrush("PrimaryTextBrush", "#292524"),
                    FontFamily = new FontFamily("Microsoft YaHei UI"),
                    FontWeight = FontWeights.Medium,
                    FontSize = 12.5,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    TextDecorations = st.IsCompleted ? TextDecorations.Strikethrough : null
                };
                contentBlock.MouseLeftButtonDown += (s, e) =>
                {
                    st.IsCompleted = !st.IsCompleted;
                    AudioAndUpdateService.PlayStepCheckSound();
                    SaveAndNotify();
                    RebuildStepsList();
                };
                Grid.SetColumn(contentBlock, 3);
                rowGrid.Children.Add(contentBlock);

                Button delBtn = new Button
                {
                    Content = "×",
                    FontSize = 11,
                    Padding = new Thickness(4, 0, 4, 0),
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Foreground = Brushes.Gray,
                    Cursor = Cursors.Hand,
                    ToolTip = "删除此工序"
                };
                delBtn.Click += (s, e) =>
                {
                    _ticket.Steps.RemoveAt(idx);
                    SaveAndNotify();
                    RebuildStepsList();
                };
                Grid.SetColumn(delBtn, 4);
                rowGrid.Children.Add(delBtn);

                rowBorder.Child = rowGrid;
                _stepsPanel.Children.Add(rowBorder);
            }
            RefreshClockAndProgress(DateTimeOffset.UtcNow.ToUnixTimeSeconds(), false);
        }

        private void ApplySmartTitleBoxInput(bool explicitEnterCommit = false)
        {
            if (_titleBox == null) return;
            string cleaned = AudioAndUpdateService.SanitizePastedText(_titleBox.Text).Trim();
            if (string.IsNullOrWhiteSpace(cleaned)) return;

            if (_ticket.IsStickyNote)
            {
                bool noteTitleChanged = !string.Equals(_ticket.Title ?? "", cleaned, StringComparison.Ordinal);
                if (!noteTitleChanged && !explicitEnterCommit) return;
                _ticket.Title = cleaned;
                _titleBox.Text = cleaned;
                CompleteFirstEditIfPending();
                SaveAndNotify();
                return;
            }

            bool anyChanged = false;
            int durSec;
            string remText;
            if (LocalAiAndOcrService.TryParseQuickTimeCommand(cleaned, out durSec, out remText))
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                _ticket.StartTimeUnix = now;
                _ticket.DeadlineUnix = now + durSec;
                anyChanged = true;
                if (!string.IsNullOrWhiteSpace(remText))
                {
                    cleaned = remText;
                }
            }

            int imgPts;
            if (LocalAiAndOcrService.TryParseImageCountPoints(cleaned, out imgPts, out remText))
            {
                _ticket.TipEarned = imgPts;
                anyChanged = true;
                if (!string.IsNullOrWhiteSpace(remText))
                {
                    cleaned = remText;
                }
            }

            string macroTitle;
            string macroUrl;
            List<TicketStepRecord> macroSteps;
            bool isPlaceholderOnly = _ticket.Steps.Count == 0 ||
                (_ticket.Steps.Count == 1 && _ticket.Steps[0].Content.Contains("点击修改或在下框"));

            if (isPlaceholderOnly && LocalAiAndOcrService.TryExpandRecipeMacro(cleaned, out macroTitle, out macroUrl, out macroSteps))
            {
                cleaned = macroTitle;
                _ticket.Steps.Clear();
                foreach (TicketStepRecord st in macroSteps)
                {
                    st.SortOrder = _ticket.Steps.Count;
                    _ticket.Steps.Add(st);
                }
                RebuildStepsList();
                anyChanged = true;
            }

            if (!string.Equals(_ticket.Title ?? "", cleaned, StringComparison.Ordinal))
            {
                anyChanged = true;
            }

            if (!anyChanged && !explicitEnterCommit)
            {
                return;
            }

            _ticket.Title = cleaned;
            _titleBox.Text = cleaned;
            RefreshClockAndProgress(DateTimeOffset.UtcNow.ToUnixTimeSeconds(), false);
            CompleteFirstEditIfPending();
            SaveAndNotify();
        }

        private void OnQuickAddKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            ProcessQuickInputAndSplit();
        }

        private void ProcessQuickInputAndSplit()
        {
            string raw = AudioAndUpdateService.SanitizePastedText(_quickAddBox.Text).Trim();
            if (string.IsNullOrEmpty(raw)) return;

            int durSec;
            string remText;
            if (LocalAiAndOcrService.TryParseQuickTimeCommand(raw, out durSec, out remText))
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                _ticket.StartTimeUnix = now;
                _ticket.DeadlineUnix = now + durSec;
                raw = remText;
            }

            int imgPts2;
            if (LocalAiAndOcrService.TryParseImageCountPoints(raw, out imgPts2, out remText))
            {
                _ticket.TipEarned = imgPts2;
                raw = remText;
            }

            // 1. Check if input triggers a fixed Recipe Macro (e.g., "29号需求，小佳特效")
            string macroTitle;
            string macroUrl;
            List<TicketStepRecord> macroSteps;
            if (LocalAiAndOcrService.TryExpandRecipeMacro(raw, out macroTitle, out macroUrl, out macroSteps))
            {
                _ticket.Title = macroTitle;
                if (_titleBox != null) _titleBox.Text = macroTitle;
                if (!string.IsNullOrEmpty(macroUrl))
                {
                    _ticket.ScratchNotes = string.IsNullOrEmpty(_ticket.ScratchNotes)
                        ? macroUrl
                        : (_ticket.ScratchNotes + "\n" + macroUrl);
                    if (_scratchBox != null) _scratchBox.Text = _ticket.ScratchNotes;
                }

                _ticket.Steps.Clear();
                foreach (TicketStepRecord st in macroSteps)
                {
                    st.SortOrder = _ticket.Steps.Count;
                    _ticket.Steps.Add(st);
                }
                RebuildStepsList();
                _quickAddBox.Text = string.Empty;
                Keyboard.ClearFocus();
                RefreshClockAndProgress(DateTimeOffset.UtcNow.ToUnixTimeSeconds(), false);
                AudioAndUpdateService.PlayStepCheckSound();
                CompleteFirstEditIfPending();
                SaveAndNotify();
                return;
            }

            // 2. Otherwise check explicit "#标题" directive and standard delimiter splitting
            string extractedTitle;
            if (LocalAiAndOcrService.TryExtractTitleDirective(raw, out extractedTitle, out remText))
            {
                _ticket.Title = extractedTitle;
                if (_titleBox != null) _titleBox.Text = extractedTitle;
                raw = remText;
            }

            if (!string.IsNullOrWhiteSpace(raw))
            {
                if (_ticket.Steps.Count == 1 && _ticket.Steps[0].Content.Contains("点击修改或在下框"))
                {
                    _ticket.Steps.Clear();
                }

                List<TicketStepRecord> splitSteps = LocalAiAndOcrService.SplitLinesIntoValidatedSteps(raw);
                foreach (TicketStepRecord st in splitSteps)
                {
                    st.SortOrder = _ticket.Steps.Count;
                    _ticket.Steps.Add(st);
                }
                RebuildStepsList();
            }

            _quickAddBox.Text = string.Empty;
            Keyboard.ClearFocus();
            RefreshClockAndProgress(DateTimeOffset.UtcNow.ToUnixTimeSeconds(), false);
            CompleteFirstEditIfPending();
            SaveAndNotify();
        }

        private void OnPasteClipboardImage(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!Clipboard.ContainsImage())
                {
                    MessageBox.Show("剪贴板中没有检测到图片。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                BitmapSource src = Clipboard.GetImage();
                int w, h;
                byte[] png = WicImageSafety.EncodeSafePng(src, out w, out h);
                _repo.SaveTicketImage(_ticket.Id, png, w, h);
                LoadAttachedImageIfAny();
            }
            catch (Exception ex)
            {
                MessageBox.Show("图片安全检查拦截：" + ex.Message, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OnDeleteAttachedImage(object sender, RoutedEventArgs e)
        {
            try
            {
                _repo.DeleteTicketImage(_ticket.Id);
                LoadAttachedImageIfAny();
            }
            catch
            {
            }
        }

        private void LoadAttachedImageIfAny()
        {
            if (_attachedImageView == null) return;
            try
            {
                byte[] blob = _repo.GetTicketImage(_ticket.Id);
                BitmapImage img = WicImageSafety.DecodeSafeImage(blob);
                _attachedImageView.Source = img;
                Visibility vis = img != null ? Visibility.Visible : Visibility.Collapsed;
                _attachedImageView.Visibility = vis;
                if (_attachedImageCard != null) _attachedImageCard.Visibility = vis;
            }
            catch
            {
                _attachedImageView.Visibility = Visibility.Collapsed;
                if (_attachedImageCard != null) _attachedImageCard.Visibility = Visibility.Collapsed;
            }
        }

        private void OnAiSplitScratchpad(object sender, RoutedEventArgs e)
        {
            _ticket.ScratchNotes = AudioAndUpdateService.SanitizePastedText(_scratchBox.Text);
            string hint;
            List<TicketStepRecord> newSteps = LocalAiAndOcrService.ConvertScratchToStepsSafely(_ticket.ScratchNotes, out hint);
            if (newSteps.Count > 0)
            {
                foreach (TicketStepRecord s in newSteps)
                {
                    s.SortOrder = _ticket.Steps.Count;
                    _ticket.Steps.Add(s);
                }
                RebuildStepsList();
                CompleteFirstEditIfPending();
                SaveAndNotify();
            }
            MessageBox.Show(hint, "智能拆单", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OnServeTicketClicked(object sender, RoutedEventArgs e)
        {
            if (_ticket.Status == "Served") return;
            string awardMsg;
            _repo.ServeTicketAndAwardTips(_ticket, out awardMsg);
            AudioAndUpdateService.PlayServeBell();
            if (_stampText != null)
            {
                if (IsCakeTheme())
                    _stampText.Text = string.Format("🎂 甜美出炉! {0}%", _ticket.ServedScore);
                else if (IsCoffeeTheme())
                    _stampText.Text = string.Format("☕ 醇香出品! {0}%", _ticket.ServedScore);
                else if (IsThermalTheme())
                    _stampText.Text = string.Format("[ PAID / 已结清 {0}% ]", _ticket.ServedScore);
                else if (IsVintageTheme())
                    _stampText.Text = string.Format("SERVED · 已出餐 {0}%", _ticket.ServedScore);
                else
                    _stampText.Text = string.Format("PERFECT! {0}%", _ticket.ServedScore);
            }
            if (_stampOverlay != null) _stampOverlay.Visibility = Visibility.Visible;
            RebuildStepsList();
            _repo.SaveTicket(_ticket);

            // Smooth fade-out animation (stamp shows for 550ms, then fades out over 750ms and disappears!)
            System.Windows.Media.Animation.DoubleAnimation fadeOut = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                BeginTime = TimeSpan.FromMilliseconds(550),
                Duration = new Duration(TimeSpan.FromMilliseconds(750))
            };
            fadeOut.Completed += (s2, e2) =>
            {
                Hide();
                if (_onChanged != null) _onChanged();
            };
            BeginAnimation(Window.OpacityProperty, fadeOut);
        }

        private void OnCollapseTicketClicked(object sender, RoutedEventArgs e)
        {
            if (_titleBox != null)
            {
                string cleanTitle = AudioAndUpdateService.SanitizePastedText(_titleBox.Text).Trim();
                if (!string.IsNullOrEmpty(cleanTitle))
                {
                    _ticket.Title = cleanTitle;
                }
            }
            if (_scratchBox != null)
            {
                _ticket.ScratchNotes = AudioAndUpdateService.SanitizePastedText(_scratchBox.Text);
            }
            _repo.SaveTicket(_ticket);
            CompleteFirstEditIfPending();

            if (OnCollapseRequested != null)
            {
                OnCollapseRequested(_ticket.Id);
            }
            else
            {
                Hide();
                if (_onChanged != null) _onChanged();
            }
        }

        private void OnHeaderMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            try
            {
                DragMove();
                _ticket.PosX = Left;
                _ticket.PosY = Top;

                // Drag off rail (>74px from top) pins to desktop; push to top (<=62px) snaps back onto rail when rail is visible!
                bool railVisible = IsRailVisibleQuery == null || IsRailVisibleQuery();
                if (_ticket.IsDockedOnRail && Top > SystemParameters.WorkArea.Top + 74)
                {
                    _ticket.IsDockedOnRail = false;
                }
                else if (!_ticket.IsDockedOnRail && railVisible && Top <= SystemParameters.WorkArea.Top + 62)
                {
                    _ticket.IsDockedOnRail = true;
                    _ticket.IsClickThrough = false;
                    ApplyClickThroughState();
                }

                if (_clipLabel != null)
                {
                    _clipLabel.Text = _ticket.IsDockedOnRail ? "夹单位 · 展开小票" : "桌面钉住 · 上推归轨";
                }
                SaveAndNotify();
            }
            catch
            {
            }
        }

        public void ApplyClickThroughState()
        {
            bool effectiveClickThrough = _ticket.IsClickThrough && !_temporarilyWoken && !_ticket.IsDockedOnRail;
            Win32Interop.SetWindowClickThrough(this, effectiveClickThrough, hideFromAltTab: true);
            Opacity = effectiveClickThrough ? 0.72 : 1.0;
            if (_wakeHintText != null)
            {
                string ptsTag = (!_ticket.IsStickyNote && _ticket.TipEarned > 0)
                    ? string.Format("⭐ 本单积分: {0} · ", _ticket.TipEarned)
                    : "";
                if (_temporarilyWoken && _ticket.IsClickThrough)
                    _wakeHintText.Text = ptsTag + "✨ 已唤醒";
                else if (_ticket.IsClickThrough)
                    _wakeHintText.Text = ptsTag + "👻 穿透中";
                else if (_ticket.IsStickyNote)
                    _wakeHintText.Text = "📝 自动保存";
                else
                    _wakeHintText.Text = ptsTag + "Ctrl+Alt 唤醒穿透";
            }
        }

        public void SetModifierWakeActive(bool woken)
        {
            if (_temporarilyWoken == woken) return;
            _temporarilyWoken = woken;
            if (_ticket.IsClickThrough)
            {
                ApplyClickThroughState();
            }
        }

        public void RefreshClockAndProgress(long nowUnix, bool enableRedShake, double shakeAngle = 0)
        {
            ApplyClickThroughState();
            if (_ticket.IsStickyNote)
            {
                if (_countdownText != null) _countdownText.Text = "📝 便签";
                if (_cakeNoText != null) _cakeNoText.Text = _ticket.TicketNo;
                if (_cakeHourglassText != null) _cakeHourglassText.Text = "📝";
                if (_cakeTimeText != null) _cakeTimeText.Text = "便签";
                if (_titleBox != null && !_titleBox.IsKeyboardFocusWithin) _titleBox.Text = _ticket.Title;
                if (_shakeTransform != null) _shakeTransform.Angle = 0;
                return;
            }
            string shapeLabel;
            string remText = _ticket.FormatRemainingTime(nowUnix, out shapeLabel);
            if (_countdownText != null) _countdownText.Text = "⏳ " + remText;

            int total = _ticket.Steps.Count;
            int done = 0;
            for (int i = 0; i < total; i++)
            {
                if (_ticket.Steps[i].IsCompleted) done++;
            }
            int pct = total > 0 ? (done * 100) / total : (_ticket.Status == "Served" ? 100 : 0);
            UrgencyState u = _ticket.ComputeUrgency(nowUnix);

            if (IsCakeTheme())
            {
                UpdateCakeShopHeader(_ticket.TicketNo, _ticket.Title, remText, done, total, u);
            }

            if (_progressLabel != null)
                _progressLabel.Text = string.Format("{0} · 工序进度 {1}/{2}", shapeLabel, done, total);
            if (_progressPctText != null)
                _progressPctText.Text = pct + "%";
            if (_progressBarFill != null)
            {
                _progressBarFill.Width = Math.Max(6, (300.0 * pct) / 100.0);
                if (IsCoffeeTheme())
                {
                    if (u == UrgencyState.Red) _progressBarFill.Background = SkinThemeManager.BrushFromHex("#C83E2B");
                    else if (u == UrgencyState.Orange) _progressBarFill.Background = SkinThemeManager.BrushFromHex("#D97724");
                    else if (u == UrgencyState.Yellow) _progressBarFill.Background = SkinThemeManager.BrushFromHex("#D58A47");
                    else _progressBarFill.Background = SkinThemeManager.BrushFromHex("#8C532E");
                }
                else
                {
                    if (u == UrgencyState.Red) _progressBarFill.Background = SkinThemeManager.BrushFromHex("#EF4444");
                    else if (u == UrgencyState.Orange) _progressBarFill.Background = SkinThemeManager.BrushFromHex("#F97316");
                    else if (u == UrgencyState.Yellow) _progressBarFill.Background = SkinThemeManager.BrushFromHex("#F5B731");
                    else _progressBarFill.Background = SkinThemeManager.BrushFromHex("#10B981");
                }
            }

            if (_shakeTransform != null)
            {
                _shakeTransform.Angle = (enableRedShake && u == UrgencyState.Red && _ticket.Status != "Served") ? shakeAngle : 0;
            }
        }

        private void SaveAndNotify()
        {
            _repo.SaveTicket(_ticket);
            if (_onChanged != null) _onChanged();
        }
    }
}
