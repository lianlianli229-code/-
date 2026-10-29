using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using StickyTickets.Data;
using StickyTickets.Interop;
using StickyTickets.Services;
using StickyTickets.Themes;

namespace StickyTickets.UI
{
    public class DailyReportAndWishDialog : Window
    {
        private readonly TicketRepository _repo;
        private readonly Action _onSettingsChanged;
        private ComboBox _skinCombo;
        private ComboBox _wakeKeyCombo;
        private TextBox _reminderMinutesBox;

        public ModifierWakePreset SelectedWakePreset { get; private set; }

        public DailyReportAndWishDialog(TicketRepository repo, ModifierWakePreset currentPreset, Action onSettingsChanged)
        {
            _repo = repo;
            SelectedWakePreset = currentPreset;
            _onSettingsChanged = onSettingsChanged;

            Title = "任务便签 · ⭐ 积分统计与设置";
            System.Windows.Media.Imaging.BitmapImage logoImg = SkinThemeManager.LoadEmbeddedAppLogo();
            if (logoImg != null) Icon = logoImg;
            Width = 550;
            Height = 640;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = SkinThemeManager.BrushFromHex("#FFFDF7");

            BuildContent();
        }

        private void BuildContent()
        {
            UserEconomyRecord eco = _repo.GetEconomy();
            List<TicketRecord> tickets = _repo.GetActiveTickets();
            int servedCount = 0;
            int totalScore = 0;
            foreach (TicketRecord t in tickets)
            {
                if (t.Status == "Served")
                {
                    servedCount++;
                    totalScore += t.ServedScore;
                }
            }
            int avgScore = servedCount > 0 ? totalScore / servedCount : 100;

            ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            StackPanel root = new StackPanel { Margin = new Thickness(18) };

            // 1. Header Banner
            Border header = new Border
            {
                Background = SkinThemeManager.BrushFromHex("#D6372B"),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 10)
            };
            StackPanel headerStack = new StackPanel();
            headerStack.Children.Add(new TextBlock
            {
                Text = string.Format("🏆 今日战绩：已交单 {0} 单 · 平均得分 {1}% · 今日积分 ⭐ {2}", servedCount, avgScore, eco.TotalTips),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 14
            });
            header.Child = headerStack;
            root.Children.Add(header);

            // 2. Onboarding Guide & Quick Usage Instructions (新手公告与使用说明)
            GroupBox guideGroup = new GroupBox
            {
                Header = "📢 新手使用说明（与启动新手公告栏同步）",
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 10)
            };
            StackPanel guideStack = new StackPanel();
            for (int i = 0; i < OnboardingNoticeWindow.GuideItems.Length; i++)
            {
                TextBlock itemTb = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    LineHeight = 19,
                    FontSize = 12,
                    Foreground = SkinThemeManager.BrushFromHex("#27272A"),
                    Margin = new Thickness(0, 0, 0, 5)
                };
                itemTb.Inlines.Add(new System.Windows.Documents.Run("• " + OnboardingNoticeWindow.GuideItems[i][0] + "：")
                {
                    FontWeight = FontWeights.Bold,
                    Foreground = SkinThemeManager.BrushFromHex("#9A3412")
                });
                itemTb.Inlines.Add(new System.Windows.Documents.Run(OnboardingNoticeWindow.GuideItems[i][1]));
                guideStack.Children.Add(itemTb);
            }

            WrapPanel guideFooterRow = new WrapPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            };
            Button showNoticeBtn = new Button
            {
                Content = "📢 弹出新手公告栏",
                Padding = new Thickness(10, 3, 10, 3),
                Margin = new Thickness(0, 0, 12, 0),
                Background = SkinThemeManager.BrushFromHex("#FEF3C7"),
                Foreground = SkinThemeManager.BrushFromHex("#92400E"),
                BorderBrush = SkinThemeManager.BrushFromHex("#F59E0B"),
                FontWeight = FontWeights.Bold
            };
            showNoticeBtn.Click += (s, e) =>
            {
                string selectedSkin = (_skinCombo != null ? _skinCombo.SelectedItem as string : null) ?? eco.ActiveSkin ?? SkinThemeManager.BurgerShopSkin;
                OnboardingNoticeWindow noticeWin = new OnboardingNoticeWindow(selectedSkin);
                noticeWin.Show();
            };
            guideFooterRow.Children.Add(showNoticeBtn);

            CheckBox showOnNextStartCb = new CheckBox
            {
                Content = "下次开启软件时弹出新手公告栏",
                IsChecked = !eco.OnboardingNoticeAcked,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11.5,
                Foreground = SkinThemeManager.BrushFromHex("#52525B")
            };
            showOnNextStartCb.Click += (s, e) =>
            {
                eco.OnboardingNoticeAcked = !(showOnNextStartCb.IsChecked == true);
                _repo.SaveEconomy(eco);
            };
            guideFooterRow.Children.Add(showOnNextStartCb);

            guideStack.Children.Add(guideFooterRow);
            guideGroup.Content = guideStack;
            root.Children.Add(guideGroup);

            // 2.2 Quick Input Syntax Tips
            GroupBox hotkeyGroup = new GroupBox
            {
                Header = "⌨️ 写单快捷指令补充提示",
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 10)
            };
            StackPanel hotkeyStack = new StackPanel();
            hotkeyStack.Children.Add(new TextBlock
            {
                Text = "• 【按住 Ctrl + Alt】：当小票开启「👻 穿透」后，按住该组合键可临时唤醒并拖动小票。\n" +
                       "• 【小票输入框按 Enter】：\n" +
                       "    1) 写时间（如：8h、30m、2小时）➔ 自动修改该小票倒计时；\n" +
                       "    2) 写积分（如：图片数量：15）➔ 自动设定本单积分，点「🔔 叮！交单出餐」后计入今日积分。",
                FontSize = 12,
                Foreground = SkinThemeManager.BrushFromHex("#27272A"),
                LineHeight = 19,
                TextWrapping = TextWrapping.Wrap
            });
            hotkeyGroup.Content = hotkeyStack;
            root.Children.Add(hotkeyGroup);

            // 2.5 Upcoming Deadline Reminder Configuration Area
            GroupBox reminderGroup = new GroupBox
            {
                Header = "⏰ 任务快到时通知提醒设置",
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 10)
            };
            StackPanel reminderStack = new StackPanel();
            WrapPanel remRow = new WrapPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 0, 6) };
            remRow.Children.Add(new TextBlock
            {
                Text = "当任务剩余时间 ≤ ",
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.SemiBold
            });
            _reminderMinutesBox = new TextBox
            {
                Text = eco.ReminderMinutes.ToString(),
                Width = 54,
                Padding = new Thickness(4, 2, 4, 2),
                TextAlignment = TextAlignment.Center,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(4, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            remRow.Children.Add(_reminderMinutesBox);
            remRow.Children.Add(new TextBlock
            {
                Text = " 分钟时，在屏幕正中央弹出主题通知提醒（填 0 关闭提醒）",
                VerticalAlignment = VerticalAlignment.Center
            });
            reminderStack.Children.Add(remRow);

            StackPanel remBtnRow = new StackPanel { Orientation = Orientation.Horizontal };
            Button saveRemBtn = new Button
            {
                Content = "💾 保存提醒时间",
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(0, 0, 8, 0),
                Background = SkinThemeManager.BrushFromHex("#F5B731"),
                FontWeight = FontWeights.Bold
            };
            saveRemBtn.Click += (s, e) =>
            {
                int parsedMins;
                if (!int.TryParse(_reminderMinutesBox.Text.Trim(), out parsedMins) || parsedMins < 0)
                    parsedMins = 30;
                if (parsedMins > 1440) parsedMins = 1440;
                _reminderMinutesBox.Text = parsedMins.ToString();
                eco.ReminderMinutes = parsedMins;
                _repo.SaveEconomy(eco);
                if (_onSettingsChanged != null) _onSettingsChanged();
                string tipText = parsedMins == 0
                    ? "已关闭任务快到时弹窗提醒。"
                    : string.Format("已保存：任务剩余 {0} 分钟时将在屏幕中央弹出主题通知！", parsedMins);
                MessageBox.Show(tipText, "提醒设置已保存", MessageBoxButton.OK, MessageBoxImage.Information);
            };
            remBtnRow.Children.Add(saveRemBtn);

            Button previewRemBtn = new Button
            {
                Content = "🔔 预览居中提醒效果",
                Padding = new Thickness(10, 4, 10, 4)
            };
            previewRemBtn.Click += (s, e) =>
            {
                int previewMins;
                if (!int.TryParse(_reminderMinutesBox.Text.Trim(), out previewMins) || previewMins <= 0)
                    previewMins = 30;
                string selectedSkin = (_skinCombo != null ? _skinCombo.SelectedItem as string : null) ?? eco.ActiveSkin ?? SkinThemeManager.BurgerShopSkin;
                long nowPreview = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                TicketRecord sampleTicket = new TicketRecord
                {
                    TicketNo = "#051",
                    Title = "示例待办任务（预览效果）",
                    StartTimeUnix = nowPreview - 3600,
                    DeadlineUnix = nowPreview + previewMins * 60
                };
                AudioAndUpdateService.PlayUrgentAlert();
                DeadlineReminderWindow previewWin = new DeadlineReminderWindow(sampleTicket, previewMins, selectedSkin, 0);
                previewWin.Show();
            };
            remBtnRow.Children.Add(previewRemBtn);
            reminderStack.Children.Add(remBtnRow);
            reminderGroup.Content = reminderStack;
            root.Children.Add(reminderGroup);

            // 3. UI Preferences & Recipes
            GroupBox wishGroup = new GroupBox
            {
                Header = "⚙️ 界面主题与配方设置",
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 10)
            };
            StackPanel wishStack = new StackPanel();

            StackPanel skinRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 6) };
            skinRow.Children.Add(new TextBlock { Text = "主题皮肤架构：", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
            _skinCombo = new ComboBox { Width = 220 };
            _skinCombo.Items.Add(SkinThemeManager.BurgerShopSkin);
            _skinCombo.Items.Add(SkinThemeManager.CakeShopSkin);
            _skinCombo.Items.Add(SkinThemeManager.CoffeeShopSkin);
            _skinCombo.Items.Add(SkinThemeManager.ThermalPaperSkin);
            _skinCombo.Items.Add(SkinThemeManager.VintageRetroSkin);
            _skinCombo.Items.Add(SkinThemeManager.HighContrastSkin);
            _skinCombo.SelectedItem = eco.ActiveSkin ?? SkinThemeManager.BurgerShopSkin;
            skinRow.Children.Add(_skinCombo);
            wishStack.Children.Add(skinRow);

            StackPanel wakeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 6) };
            wakeRow.Children.Add(new TextBlock { Text = "按住唤醒穿透修饰键 (防 AltGr 冲突)：", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
            _wakeKeyCombo = new ComboBox { Width = 150 };
            _wakeKeyCombo.Items.Add("Ctrl + Alt (默认)");
            _wakeKeyCombo.Items.Add("Ctrl + Shift");
            _wakeKeyCombo.Items.Add("Alt + Shift");
            _wakeKeyCombo.SelectedIndex = (int)SelectedWakePreset;
            wakeRow.Children.Add(_wakeKeyCombo);
            wishStack.Children.Add(wakeRow);

            wishStack.Children.Add(new TextBlock
            {
                Text = "🍔 固定流程配方触发词（格式：触发词=步骤1、步骤2... 每行一条，写单含触发词即自动展开）：",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 6, 0, 2)
            });
            TextBox recipeBox = new TextBox
            {
                Text = LocalAiAndOcrService.ExportRecipesAsText(),
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Height = 56,
                FontSize = 11.5,
                Margin = new Thickness(0, 0, 0, 8)
            };
            wishStack.Children.Add(recipeBox);

            Button saveBtn = new Button
            {
                Content = "💾 保存设置并刷新主题",
                Padding = new Thickness(12, 5, 12, 5),
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = SkinThemeManager.BrushFromHex("#F5B731"),
                FontWeight = FontWeights.Bold
            };
            saveBtn.Click += (s, e) =>
            {
                int parsedMins;
                if (_reminderMinutesBox != null && int.TryParse(_reminderMinutesBox.Text.Trim(), out parsedMins) && parsedMins >= 0)
                {
                    if (parsedMins > 1440) parsedMins = 1440;
                    eco.ReminderMinutes = parsedMins;
                    _reminderMinutesBox.Text = parsedMins.ToString();
                }
                eco.HideTipJarOnUi = false;
                eco.ActiveSkin = _skinCombo.SelectedItem as string ?? SkinThemeManager.BurgerShopSkin;
                SelectedWakePreset = (ModifierWakePreset)Math.Max(0, _wakeKeyCombo.SelectedIndex);
                LocalAiAndOcrService.ImportRecipesFromText(recipeBox.Text);
                _repo.SaveEconomy(eco);
                if (_onSettingsChanged != null) _onSettingsChanged();
                MessageBox.Show("设置、提醒时间与固定流程配方已保存生效！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            };
            wishStack.Children.Add(saveBtn);
            wishGroup.Content = wishStack;
            root.Children.Add(wishGroup);

            // 4. Daily Date Points Summary & TipLedger History
            GroupBox dailyGroup = new GroupBox
            {
                Header = "📅 每日积分统计档案（每日零点自动清零重新累计，按日期永久留档）",
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 0, 10)
            };
            StackPanel dailyStack = new StackPanel();
            ListBox dailyList = new ListBox { Height = 95, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) };
            foreach (string daySummary in _repo.GetDailyPointsHistorySummary())
            {
                dailyList.Items.Add(daySummary);
            }
            dailyStack.Children.Add(dailyList);

            Button clearArchiveBtn = new Button
            {
                Content = "🗑️ 清理以往所有档案",
                Padding = new Thickness(10, 4, 10, 4),
                HorizontalAlignment = HorizontalAlignment.Right,
                Background = SkinThemeManager.BrushFromHex("#FEE2E2"),
                Foreground = SkinThemeManager.BrushFromHex("#991B1B"),
                BorderBrush = SkinThemeManager.BrushFromHex("#FCA5A5"),
                FontWeight = FontWeights.Bold
            };
            clearArchiveBtn.Click += (s, e) =>
            {
                MessageBoxResult res = MessageBox.Show(
                    "确定要清理以往所有积分统计档案与历史流水记录吗？\n（当前桌面上未完成的待办小票不会受影响）",
                    "确认清理档案",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (res == MessageBoxResult.Yes)
                {
                    _repo.ClearAllPointsHistory();
                    if (_onSettingsChanged != null) _onSettingsChanged();
                    BuildContent();
                    MessageBox.Show("所有历史积分档案与流水已清理完毕！", "清理完成", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            };
            dailyStack.Children.Add(clearArchiveBtn);
            dailyGroup.Content = dailyStack;
            root.Children.Add(dailyGroup);

            GroupBox ledgerGroup = new GroupBox { Header = "📜 每单积分明细流水 (TipLedger)", Padding = new Thickness(8), Margin = new Thickness(0, 0, 0, 10) };
            ListBox ledgerList = new ListBox { Height = 105 };
            foreach (TipLedgerRecord row in _repo.GetRecentTipLedger(30))
            {
                string dt = DateTimeOffset.FromUnixTimeSeconds(row.CreatedAtUnix).ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                ledgerList.Items.Add(string.Format("[{0}] +{1} ⭐ — {2}", dt, row.Delta, row.Reason));
            }
            ledgerGroup.Content = ledgerList;
            root.Children.Add(ledgerGroup);

            // 5. Safe Backup / Restore / Update Check
            WrapPanel toolsRow = new WrapPanel { Margin = new Thickness(0, 4, 0, 10) };
            Button backupBtn = new Button { Content = "📦 导出安全备份 (.db)", Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(0, 0, 8, 4) };
            backupBtn.Click += OnExportBackup;
            Button restoreBtn = new Button { Content = "🛡️ 校验并导入备份", Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(0, 0, 8, 4) };
            restoreBtn.Click += OnImportBackup;
            Button updateBtn = new Button { Content = "🌐 检查更新 (打开官方 HTTPS 页)", Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(0, 0, 8, 4) };
            updateBtn.Click += (s, e) =>
            {
                string msg;
                AudioAndUpdateService.OpenOfficialReleasePageSafely("https://github.com/releases", out msg);
                MessageBox.Show(msg, "版本检查", MessageBoxButton.OK, MessageBoxImage.Information);
            };
            toolsRow.Children.Add(backupBtn);
            toolsRow.Children.Add(restoreBtn);
            toolsRow.Children.Add(updateBtn);
            root.Children.Add(toolsRow);

            // 6. Plaintext Storage Disclosure
            root.Children.Add(new TextBlock
            {
                Text = "🔒 安全声明：本地便签数据明文存储于当前 Windows 账户目录 (%LocalAppData%\\StickyTickets)，依赖系统账户权限隔离；日志绝不记录便签正文。",
                FontSize = 11,
                Foreground = Brushes.DimGray,
                TextWrapping = TextWrapping.Wrap
            });

            scroll.Content = root;
            Content = scroll;
        }

        private void OnExportBackup(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog
            {
                Filter = "SQLite Backup (*.db)|*.db",
                FileName = "sticky_tickets_backup_" + DateTime.Now.ToString("yyyyMMdd") + ".db"
            };
            if (sfd.ShowDialog() == true)
            {
                try
                {
                    _repo.ExportBackupSafely(sfd.FileName);
                    MessageBox.Show("在线备份导出成功！", "备份完成", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("备份导出失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void OnImportBackup(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog { Filter = "SQLite Backup (*.db)|*.db" };
            if (ofd.ShowDialog() == true)
            {
                try
                {
                    _repo.ImportValidatedBackup(ofd.FileName);
                    if (_onSettingsChanged != null) _onSettingsChanged();
                    MessageBox.Show("备份文件已通过完整性与安全校验（零触发器/零视图），恢复成功！", "恢复成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    BuildContent();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "安全拦截", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
