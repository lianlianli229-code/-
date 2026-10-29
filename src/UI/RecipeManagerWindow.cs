using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using StickyTickets.Data;
using StickyTickets.Services;
using StickyTickets.Themes;

namespace StickyTickets.UI
{
    /// <summary>
    /// Dedicated visual Recipe Library window where the user can freely add, edit, or delete
    /// workflow keywords and their corresponding fixed step lists, persisted in SQLite.
    /// </summary>
    public class RecipeManagerWindow : Window
    {
        private readonly TicketRepository _repo;
        private ListBox _recipeListBox;
        private TextBox _keywordsBox;
        private TextBox _stepsBox;
        private TextBlock _modeHintText;
        private List<RecipeRecord> _currentRecipes = new List<RecipeRecord>();
        private long _editingRecipeId = 0;

        public RecipeManagerWindow(TicketRepository repo)
        {
            _repo = repo;
            Title = "任务便签 · 📖 专属工序配方库";
            System.Windows.Media.Imaging.BitmapImage logoImg = SkinThemeManager.LoadEmbeddedAppLogo();
            if (logoImg != null) Icon = logoImg;
            Width = 520;
            Height = 520;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = SkinThemeManager.BrushFromHex("#FFFDF7");
            Topmost = true;

            BuildUi();
            ReloadList();
        }

        private void BuildUi()
        {
            Grid root = new Grid { Margin = new Thickness(16) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 1. Top Banner
            Border header = new Border
            {
                Background = SkinThemeManager.BrushFromHex("#D6372B"),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 0, 10)
            };
            header.Child = new TextBlock
            {
                Text = "🍔 固定工序配方库（写单只要包含关键词，自动设好标题并展开全部工序）",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            // 2. Saved Recipes List
            GroupBox listGroup = new GroupBox
            {
                Header = "📋 已保存的配方列表（点击任意一行即可在下方修改）",
                Padding = new Thickness(6),
                Margin = new Thickness(0, 0, 0, 10)
            };
            _recipeListBox = new ListBox { FontSize = 12.5 };
            _recipeListBox.SelectionChanged += OnRecipeSelectionChanged;
            listGroup.Content = _recipeListBox;
            Grid.SetRow(listGroup, 1);
            root.Children.Add(listGroup);

            // 3. Add / Edit Form Area
            Border editCard = new Border
            {
                Background = SkinThemeManager.BrushFromHex("#FFF6E5"),
                BorderBrush = SkinThemeManager.BrushFromHex("#F5B731"),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12)
            };
            StackPanel form = new StackPanel();

            _modeHintText = new TextBlock
            {
                Text = "✨ 新增配方模式（填写下方两项后点保存即可）",
                Foreground = SkinThemeManager.BrushFromHex("#B45309"),
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 6)
            };
            form.Children.Add(_modeHintText);

            form.Children.Add(new TextBlock
            {
                Text = "① 触发关键词（多个同义词可用顿号「、」隔开，如：周报、设计）：",
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 3)
            });
            _keywordsBox = new TextBox
            {
                Padding = new Thickness(6, 4, 6, 4),
                FontSize = 12.5,
                MaxLength = 100,
                Margin = new Thickness(0, 0, 0, 8)
            };
            form.Children.Add(_keywordsBox);

            form.Children.Add(new TextBlock
            {
                Text = "② 对应的固定工序步骤（用顿号「、」或分号「；」或换行隔开）：",
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 3)
            });
            _stepsBox = new TextBox
            {
                Padding = new Thickness(6, 4, 6, 4),
                FontSize = 12.5,
                Height = 68,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MaxLength = 1000,
                Margin = new Thickness(0, 0, 0, 10)
            };
            form.Children.Add(_stepsBox);

            WrapPanel btnRow = new WrapPanel();

            Button saveBtn = new Button
            {
                Content = "💾 保存到配方库",
                Background = SkinThemeManager.BrushFromHex("#059669"),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(14, 5, 14, 5),
                Margin = new Thickness(0, 0, 8, 0),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            saveBtn.Click += OnSaveClicked;
            btnRow.Children.Add(saveBtn);

            Button newBlankBtn = new Button
            {
                Content = "➕ 清空新建一条",
                Background = SkinThemeManager.BrushFromHex("#F5B731"),
                Foreground = SkinThemeManager.BrushFromHex("#451A03"),
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(0, 0, 8, 0),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            newBlankBtn.Click += (s, e) => ClearToNewMode();
            btnRow.Children.Add(newBlankBtn);

            Button delBtn = new Button
            {
                Content = "🗑️ 删除选中配方",
                Background = SkinThemeManager.BrushFromHex("#DC2626"),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(10, 5, 10, 5),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            delBtn.Click += OnDeleteClicked;
            btnRow.Children.Add(delBtn);

            form.Children.Add(btnRow);
            editCard.Child = form;
            Grid.SetRow(editCard, 2);
            root.Children.Add(editCard);

            Content = root;
        }

        private void ReloadList()
        {
            _currentRecipes = _repo.GetRecipes();
            _recipeListBox.Items.Clear();
            foreach (RecipeRecord r in _currentRecipes)
            {
                _recipeListBox.Items.Add(string.Format("【关键词：{0}】 ➔ {1}", r.Keywords, r.StepsText));
            }
            ClearToNewMode();
        }

        private void ClearToNewMode()
        {
            _editingRecipeId = 0;
            _recipeListBox.SelectedIndex = -1;
            _keywordsBox.Text = "";
            _stepsBox.Text = "";
            _modeHintText.Text = "✨ 新增配方模式（填写下方两项后点「保存到配方库」即可添加）";
        }

        private void OnRecipeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int idx = _recipeListBox.SelectedIndex;
            if (idx < 0 || idx >= _currentRecipes.Count) return;

            RecipeRecord sel = _currentRecipes[idx];
            _editingRecipeId = sel.Id;
            _keywordsBox.Text = sel.Keywords;
            _stepsBox.Text = sel.StepsText;
            _modeHintText.Text = string.Format("✏️ 正在修改配方：【{0}】（改完点「保存到配方库」，或点「清空新建一条」）", sel.Keywords);
        }

        private void OnSaveClicked(object sender, RoutedEventArgs e)
        {
            string kw = AudioAndUpdateService.SanitizePastedText(_keywordsBox.Text).Trim();
            string st = AudioAndUpdateService.SanitizePastedText(_stepsBox.Text).Trim();
            if (string.IsNullOrEmpty(kw) || string.IsNullOrEmpty(st))
            {
                MessageBox.Show("请填写「触发关键词」和「对应的固定工序步骤」！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _repo.SaveRecipe(_editingRecipeId, kw, st);
            AudioAndUpdateService.PlayStepCheckSound();
            ReloadList();
        }

        private void OnDeleteClicked(object sender, RoutedEventArgs e)
        {
            if (_editingRecipeId <= 0)
            {
                MessageBox.Show("请先在上方列表点击选中一条要删除的配方。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            _repo.DeleteRecipe(_editingRecipeId);
            ReloadList();
        }
    }
}
