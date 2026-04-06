using Bangumi.Api;
using Bangumi.Api.Models;
using Bangumi.Common;
using Bangumi.ContentDialogs;
using Bangumi.Helper;
using Bangumi.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;

namespace Bangumi.Views
{
    public sealed partial class HomePage : Page, IPageStatus, INotifyPropertyChanged
    {
        private const double ShortcutItemHeight = 64;
        private const double FeaturedItemHeight = 160;
        private const double FeaturedHorizontalGap = 12;
        private const double FeaturedVerticalGap = 8;
        private static readonly SubjectType[] DiscoverySubjectTypes =
        {
            SubjectType.Anime,
            SubjectType.Book,
            SubjectType.Music,
            SubjectType.Game,
            SubjectType.Real
        };
        private const double StickyHeaderHeight = 34;
        private double _todayHeaderTop;
        private double _featuredHeaderTop;
        private double _shortcutGridHeight = ShortcutItemHeight;
        private double _shortcutCardWidth = 280;
        private double _featuredGridHeight = FeaturedItemHeight;
        private double _featuredCardWidth = 304;
        private bool _todayCanScrollLeft;
        private bool _todayCanScrollRight;
        private string _todayHeaderTitle = "今日放送";

        public event PropertyChangedEventHandler PropertyChanged;

        public CalendarViewModel ViewModel { get; } = new CalendarViewModel();

        public ObservableCollection<HomeShortcutItem> Shortcuts { get; } = new ObservableCollection<HomeShortcutItem>();

        public ObservableCollection<HomeSubjectItem> TodaySubjects { get; } = new ObservableCollection<HomeSubjectItem>();

        public ObservableCollection<HomeSubjectItem> FeaturedSubjects { get; } = new ObservableCollection<HomeSubjectItem>();

        public bool IsLoading => ViewModel.IsLoading;

        public double ShortcutGridHeight => _shortcutGridHeight;

        public double ShortcutCardWidth => _shortcutCardWidth;

        public double FeaturedGridHeight => _featuredGridHeight;

        public double FeaturedCardWidth => _featuredCardWidth;

        public string TodayHeaderTitle => _todayHeaderTitle;

        public string TodaySummary => TodaySubjects.Count == 0 ? "暂无数据" : $"共 {TodaySubjects.Count} 部";

        public Visibility EmptyStateVisibility => FeaturedSubjects.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public Visibility TodayLeftButtonVisibility => _todayCanScrollLeft ? Visibility.Visible : Visibility.Collapsed;

        public Visibility TodayRightButtonVisibility => _todayCanScrollRight ? Visibility.Visible : Visibility.Collapsed;

        public HomePage()
        {
            InitializeComponent();
            BuildShortcuts();
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        public async Task Refresh()
        {
            await ViewModel.PopulateCalendarAsync();
            RefreshCollections();
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                ViewModel.PopulateCalendarFromCache();
                RefreshCollections();
                NotifyStatusChanged();

                if (!ViewModel.IsLoading && ViewModel.CalendarCollection.Count == 0)
                {
                    await Refresh();
                }
            }
            catch (Exception ex)
            {
                NotificationHelper.Notify($"加载发现页失败：{ex.Message}", Controls.NotifyType.Error);
                ReplaceCollection(TodaySubjects, Array.Empty<HomeSubjectItem>());
                ReplaceCollection(FeaturedSubjects, Array.Empty<HomeSubjectItem>());
                NotifyStatusChanged();
            }

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Low, () =>
            {
                UpdateShortcutLayout();
                UpdateFeaturedLayout();
                UpdateSectionAnchors();
                UpdateTodayButtons();
                UpdateStickyHeader();
            });
        }

        private void BuildShortcuts()
        {
            Shortcuts.Clear();
            foreach (var item in new[]
            {
                new HomeShortcutItem("搜索", "综合搜索条目与 Subject ID 直达", "\uE721", 1),
                new HomeShortcutItem("每日放送", "按星期查看每日放送与放送时间", "\uE787", 2),
                new HomeShortcutItem("排行榜", "查看热门条目与评分排行", "\uE9D9", 3),
                new HomeShortcutItem("新番", "浏览季度新番与当前档期", "\uE7FC", 4),
                new HomeShortcutItem("浏览器", "通过条件筛选浏览条目", "\uE8A7", 5),
                new HomeShortcutItem("奖项", "查看 Bangumi 奖项与年鉴", "\uE7BE", 10),
                new HomeShortcutItem("目录", "查看目录与目录收藏内容", "\uE14C", 17),
                new HomeShortcutItem("推荐", "进入 AI 推荐与推荐结果页", "\uE734", 29)
            })
            {
                Shortcuts.Add(item);
            }
        }

        private void RefreshCollections()
        {
            var groups = ViewModel.CalendarCollection.ToList();
            var todayGroup = groups.FirstOrDefault();
            var todayItems = todayGroup?.Items ?? new List<SubjectForCalendar>();
            var featured = groups
                .SelectMany(group => group.Items ?? new List<SubjectForCalendar>())
                .GroupBy(item => item.Id)
                .Select(group => group.First())
                .Take(24)
                .ToList();

            _todayHeaderTitle = GetTodayHeaderTitle(todayGroup?.Weekday);

            ReplaceCollection(TodaySubjects, todayItems.Take(12).Select(item => MapSubject(item, true)));
            ReplaceCollection(FeaturedSubjects, featured.Select(item => MapSubject(item, false)));
            NotifyStatusChanged();

            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Low, () =>
            {
                UpdateSectionAnchors();
                UpdateFeaturedLayout();
                UpdateTodayButtons();
                UpdateStickyHeader();
            });
        }

        private HomeSubjectItem MapSubject(SubjectForCalendar item, bool today)
        {
            SubjectType resolvedType = ResolveSubjectType(item);
            string collectionText = item.Collection?.Doing > 0 ? $"{item.Collection.Doing} 人在看" : string.Empty;
            string ratingText = item.Rating?.Score > 0
                ? $"★ {item.Rating.Score:0.0}"
                : string.Empty;
            string meta = item.Collection?.Collect > 0
                ? $"{item.Collection.Collect} 收藏"
                : item.Rating?.Total > 0
                    ? $"{item.Rating.Total} 人评分"
                    : string.Empty;

            return new HomeSubjectItem
            {
                SubjectId = item.Id,
                Title = string.IsNullOrWhiteSpace(item.NameCn) ? item.Name : item.NameCn,
                Subtitle = string.IsNullOrWhiteSpace(item.NameCn) ? string.Empty : item.Name,
                Cover = item.Images?.Common,
                TypeText = resolvedType.GetDesc(),
                SubjectType = resolvedType,
                WeekdayText = item.AirWeekdayCn,
                CollectionText = collectionText,
                RatingText = ratingText,
                Meta = meta,
                RankText = item.Rank > 0 ? $"Rank #{item.Rank}" : string.Empty,
                Status = item.Status
            };
        }

        private SubjectType ResolveSubjectType(SubjectForCalendar item)
        {
            if (item == null)
            {
                return SubjectType.Anime;
            }

            int subjectId = item.Id;

            var cachedSubject = BangumiApi.BgmCache.Subject(subjectId.ToString());
            if (cachedSubject != null && IsKnownSubjectType(cachedSubject.Type))
            {
                return cachedSubject.Type;
            }

            var cachedWatchingType = BangumiApi.BgmCache.Watching()?
                .FirstOrDefault(w => w?.SubjectId == subjectId)?
                .Subject?.Type;
            if (cachedWatchingType.HasValue && IsKnownSubjectType(cachedWatchingType.Value))
            {
                return cachedWatchingType.Value;
            }

            foreach (var subjectType in DiscoverySubjectTypes)
            {
                var collectionType = BangumiApi.BgmCache.Collections(subjectType)?
                    .Collects?
                    .SelectMany(collection => collection?.Items ?? Enumerable.Empty<SubjectBaseE>())
                    .FirstOrDefault(subject => subject?.SubjectId == subjectId)?
                    .Subject?.Type;
                if (collectionType.HasValue && IsKnownSubjectType(collectionType.Value))
                {
                    return collectionType.Value;
                }
            }

            return IsKnownSubjectType(item.Type) ? item.Type : SubjectType.Anime;
        }

        private static bool IsKnownSubjectType(SubjectType subjectType)
        {
            return subjectType == SubjectType.Anime
                || subjectType == SubjectType.Book
                || subjectType == SubjectType.Music
                || subjectType == SubjectType.Game
                || subjectType == SubjectType.Real;
        }

        private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> source)
        {
            target.Clear();
            foreach (var item in source)
            {
                target.Add(item);
            }
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.IsLoading))
            {
                NotifyStatusChanged();
            }
        }

        private void ShortcutButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is HomeShortcutItem item)
            {
                MainPage.RootPage?.NavigateToDiscoverSection(item.DiscoverIndex);
            }
        }

        private void SubjectCardButton_Click(object sender, RoutedEventArgs e)
        {
            if (e?.OriginalSource is DependencyObject originalSource &&
                HasAncestorNamed(originalSource, "CollectionStatusBadge"))
            {
                return;
            }

            if (sender is FrameworkElement element && element.DataContext is HomeSubjectItem item)
            {
                MainPage.RootPage?.Frame.Navigate(typeof(EpisodePage), item.SubjectId, new DrillInNavigationTransitionInfo());
            }
        }

        private void CollectionStatusBadge_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            e.Handled = true;
        }

        private void CollectionStatusBadge_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            e.Handled = true;
        }

        private async void CollectionStatusBadge_Tapped(object sender, TappedRoutedEventArgs e)
        {
            e.Handled = true;
            if (!(sender is FrameworkElement element) || !(element.DataContext is HomeSubjectItem item))
            {
                return;
            }

            await EditCollectionStatusAsync(item);
        }

        private void ShortcutButton_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button button)
            {
                UpdateShortcutHoverState(button, "#16FFFFFF", "#24FFFFFF");
            }
        }

        private void ShortcutButton_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button button)
            {
                UpdateShortcutHoverState(button, "Transparent", "Transparent");
            }
        }

        private void ShortcutButton_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button button)
            {
                UpdateShortcutHoverState(button, "#22FFFFFF", "#38FFFFFF");
            }
        }

        private void ShortcutButton_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button button)
            {
                UpdateShortcutHoverState(button, "#16FFFFFF", "#24FFFFFF");
            }
        }

        private void CardButton_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button button)
            {
                UpdateCardHoverState(button, -6, 1, 0, 1, 1);
            }
        }

        private void CardButton_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button button)
            {
                UpdateCardHoverState(button, 0, 0, 0, 0, 0);
            }
        }

        private void CardButton_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button button)
            {
                UpdateCardHoverState(button, -3, 0.82, 0, 0.72, 0.66);
            }
        }

        private void CardButton_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button button)
            {
                UpdateCardHoverState(button, -6, 1, 0, 1, 1);
            }
        }

        private void MainScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            StickySectionHeader.Visibility = Visibility.Collapsed;
        }

        private void TodayItemsScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            UpdateTodayButtons();
        }

        private void TodayScrollLeftButton_Click(object sender, RoutedEventArgs e)
        {
            double target = Math.Max(0, TodayItemsScrollViewer.HorizontalOffset - 340);
            TodayItemsScrollViewer.ChangeView(target, null, null);
        }

        private void TodayScrollRightButton_Click(object sender, RoutedEventArgs e)
        {
            double target = Math.Min(TodayItemsScrollViewer.ScrollableWidth, TodayItemsScrollViewer.HorizontalOffset + 340);
            TodayItemsScrollViewer.ChangeView(target, null, null);
        }

        private void ContentPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateShortcutLayout();
            UpdateFeaturedLayout();
            UpdateSectionAnchors();
            UpdateTodayButtons();
            UpdateStickyHeader();
        }

        private void UpdateShortcutLayout()
        {
            if (!(ShortcutGridView.ItemsPanelRoot is ItemsWrapGrid panel) || ShortcutGridView.ActualWidth <= 0)
            {
                return;
            }

            double width = ShortcutGridView.ActualWidth;
            const double targetItemWidth = 280;
            int columns = Math.Max(1, Math.Min(Shortcuts.Count, (int)Math.Floor(width / targetItemWidth)));
            double itemWidth = Math.Floor(width / columns);
            int rows = Math.Max(1, (int)Math.Ceiling((double)Math.Max(Shortcuts.Count, 1) / columns));

            panel.ItemWidth = itemWidth;
            panel.ItemHeight = ShortcutItemHeight;
            panel.MaximumRowsOrColumns = columns;

            if (Math.Abs(_shortcutCardWidth - itemWidth) > 0.5)
            {
                _shortcutCardWidth = itemWidth;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShortcutCardWidth)));
            }

            double height = rows * ShortcutItemHeight;
            if (Math.Abs(_shortcutGridHeight - height) > 0.5)
            {
                _shortcutGridHeight = height;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShortcutGridHeight)));
            }
        }

        private void UpdateFeaturedLayout()
        {
            if (!(FeaturedGridView?.ItemsPanelRoot is ItemsWrapGrid panel) || FeaturedGridView.ActualWidth <= 0)
            {
                return;
            }

            double width = FeaturedGridView.ActualWidth;
            const double targetItemWidth = 304;
            const double minItemWidth = 272;
            int columns = Math.Max(1, Math.Min(5, (int)Math.Floor((width + FeaturedHorizontalGap) / (targetItemWidth + FeaturedHorizontalGap))));
            double itemWidth = Math.Floor((width - columns * FeaturedHorizontalGap) / columns);

            while (columns > 1 && itemWidth < minItemWidth)
            {
                columns--;
                itemWidth = Math.Floor((width - columns * FeaturedHorizontalGap) / columns);
            }

            int rows = Math.Max(1, (int)Math.Ceiling((double)Math.Max(FeaturedSubjects.Count, 1) / columns));

            panel.ItemWidth = itemWidth + FeaturedHorizontalGap;
            panel.ItemHeight = FeaturedItemHeight + FeaturedVerticalGap;
            panel.MaximumRowsOrColumns = columns;

            if (Math.Abs(_featuredCardWidth - itemWidth) > 0.5)
            {
                _featuredCardWidth = itemWidth;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FeaturedCardWidth)));
            }

            double height = rows * (FeaturedItemHeight + FeaturedVerticalGap) + 4;
            if (Math.Abs(_featuredGridHeight - height) > 0.5)
            {
                _featuredGridHeight = height;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FeaturedGridHeight)));
            }
        }

        private void UpdateSectionAnchors()
        {
            if (TodayHeaderAnchor == null || FeaturedHeaderAnchor == null || ContentPanel == null)
            {
                return;
            }

            _todayHeaderTop = GetTopRelativeToContent(TodayHeaderAnchor);
            _featuredHeaderTop = GetTopRelativeToContent(FeaturedHeaderAnchor);
        }

        private double GetTopRelativeToContent(FrameworkElement element)
        {
            try
            {
                return element.TransformToVisual(ContentPanel).TransformPoint(new Windows.Foundation.Point(0, 0)).Y;
            }
            catch
            {
                return 0;
            }
        }

        private void UpdateStickyHeader()
        {
            StickySectionHeader.Visibility = Visibility.Collapsed;
            StickySectionTransform.Y = 0;
        }

        private void UpdateTodayButtons()
        {
            if (TodayItemsScrollViewer == null)
            {
                return;
            }

            _todayCanScrollLeft = TodayItemsScrollViewer.HorizontalOffset > 1;
            _todayCanScrollRight = TodayItemsScrollViewer.HorizontalOffset < TodayItemsScrollViewer.ScrollableWidth - 1;

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TodayLeftButtonVisibility)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TodayRightButtonVisibility)));
        }

        private void NotifyStatusChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLoading)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShortcutGridHeight)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShortcutCardWidth)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FeaturedGridHeight)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FeaturedCardWidth)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TodayHeaderTitle)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TodaySummary)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EmptyStateVisibility)));
            MainPage.RootPage?.PageStatusChanged();
        }

        private static string GetTodayHeaderTitle(Weekday weekday)
        {
            if (!string.IsNullOrWhiteSpace(weekday?.Chinese))
            {
                return weekday.Chinese;
            }

            return DateTime.Today.DayOfWeek switch
            {
                DayOfWeek.Monday => "星期一",
                DayOfWeek.Tuesday => "星期二",
                DayOfWeek.Wednesday => "星期三",
                DayOfWeek.Thursday => "星期四",
                DayOfWeek.Friday => "星期五",
                DayOfWeek.Saturday => "星期六",
                DayOfWeek.Sunday => "星期日",
                _ => "今日放送"
            };
        }

        private static void UpdateShortcutHoverState(Button button, string backgroundColor, string borderColor)
        {
            if (!(button.Content is FrameworkElement root))
            {
                return;
            }

            if (root.FindName("ShortcutHoverRoot") is Border hoverRoot)
            {
                hoverRoot.Background = CreateBrush(backgroundColor);
                hoverRoot.BorderBrush = CreateBrush(borderColor);
            }
        }

        private static void UpdateCardHoverState(
            Button button,
            double translateY,
            double shadowOpacity,
            double footLightOpacity,
            double hoverTintOpacity,
            double hoverGlowOpacity)
        {
            if (!(button.Content is FrameworkElement root))
            {
                return;
            }

            if (root.FindName("CardTranslateTransform") is TranslateTransform transform)
            {
                AnimateDouble(
                    transform,
                    nameof(TranslateTransform.Y),
                    translateY,
                    translateY < transform.Y ? 180 : 240,
                    new CubicEase { EasingMode = EasingMode.EaseOut });
            }

            if (root.FindName("CardShadow") is Border shadow)
            {
                AnimateDouble(
                    shadow,
                    nameof(UIElement.Opacity),
                    shadowOpacity,
                    220,
                    new CubicEase { EasingMode = EasingMode.EaseOut });
            }

            if (root.FindName("CardFootLight") is Border footLight)
            {
                AnimateDouble(
                    footLight,
                    nameof(UIElement.Opacity),
                    footLightOpacity,
                    220,
                    new CubicEase { EasingMode = EasingMode.EaseOut });
            }

            if (root.FindName("CardHoverTint") is Border hoverTint)
            {
                AnimateDouble(
                    hoverTint,
                    nameof(UIElement.Opacity),
                    hoverTintOpacity,
                    220,
                    new CubicEase { EasingMode = EasingMode.EaseOut });
            }

            if (root.FindName("CardHoverGlow") is Border hoverGlow)
            {
                AnimateDouble(
                    hoverGlow,
                    nameof(UIElement.Opacity),
                    hoverGlowOpacity,
                    260,
                    new CubicEase { EasingMode = EasingMode.EaseOut });
            }
        }

        private static bool HasAncestorNamed(DependencyObject source, string targetName)
        {
            var current = source;
            while (current != null)
            {
                if (current is FrameworkElement element && element.Name == targetName)
                {
                    return true;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        private static void AnimateDouble(DependencyObject target, string propertyName, double to, int durationMs, EasingFunctionBase easing)
        {
            if (target == null)
            {
                return;
            }

            var animation = new DoubleAnimation
            {
                To = to,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EnableDependentAnimation = true,
                EasingFunction = easing
            };

            var storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            Storyboard.SetTarget(animation, target);
            Storyboard.SetTargetProperty(animation, propertyName);
            storyboard.Begin();
        }

        private static Brush CreateBrush(string color)
        {
            return new SolidColorBrush(color == "Transparent" ? Windows.UI.Colors.Transparent : ParseColor(color));
        }

        private static async Task EditCollectionStatusAsync(HomeSubjectItem item)
        {
            if (item == null)
            {
                return;
            }

            if (NetworkHelper.IsOffline)
            {
                NotificationHelper.Notify("无网络连接！", Controls.NotifyType.Warn);
                return;
            }

            if (!BangumiApi.BgmOAuth.IsLogin)
            {
                NotificationHelper.Notify("请先登录！", Controls.NotifyType.Warn);
                return;
            }

            var subjectStatus = BangumiApi.BgmApi.Status(item.SubjectId.ToString());
            var collectionEditContentDialog = new CollectionEditContentDialog(item.SubjectType, subjectStatus)
            {
                Title = item.Title,
            };

            MainPage.RootPage.HasDialog = true;
            if (ContentDialogResult.Primary == await collectionEditContentDialog.ShowAsync() &&
                collectionEditContentDialog.CollectionStatus != null)
            {
                try
                {
                    var collectionStatusE = await BangumiApi.BgmApi.UpdateStatus(
                        item.SubjectId.ToString(),
                        collectionEditContentDialog.CollectionStatus.Value,
                        collectionEditContentDialog.Comment,
                        collectionEditContentDialog.Rate.ToString(),
                        collectionEditContentDialog.Privacy ? "1" : "0");

                    item.Status = collectionStatusE.Status?.Id;
                    NotificationHelper.Notify(
                        $"标记 {collectionEditContentDialog.Title} {item.Status?.GetDesc(item.SubjectType)} 成功！");
                }
                catch (Exception ex)
                {
                    NotificationHelper.Notify(
                        $"标记 {collectionEditContentDialog.Title} {collectionEditContentDialog.CollectionStatus?.GetDesc(item.SubjectType)} 失败！\n" + ex.Message,
                        Controls.NotifyType.Error);
                }
            }

            MainPage.RootPage.HasDialog = false;
        }

        private static Windows.UI.Color ParseColor(string value)
        {
            value = value?.TrimStart('#');
            if (string.IsNullOrWhiteSpace(value))
            {
                return Windows.UI.Colors.Transparent;
            }

            byte a = 255;
            int index = 0;
            if (value.Length == 8)
            {
                a = Convert.ToByte(value.Substring(index, 2), 16);
                index += 2;
            }

            byte r = Convert.ToByte(value.Substring(index, 2), 16);
            byte g = Convert.ToByte(value.Substring(index + 2, 2), 16);
            byte b = Convert.ToByte(value.Substring(index + 4, 2), 16);
            return Windows.UI.Color.FromArgb(a, r, g, b);
        }
    }

    public sealed class HomeShortcutItem
    {
        public HomeShortcutItem(string title, string subtitle, string glyph, int discoverIndex)
        {
            Title = title;
            Subtitle = subtitle;
            Glyph = glyph;
            DiscoverIndex = discoverIndex;
        }

        public string Title { get; }

        public string Subtitle { get; }

        public string Glyph { get; }

        public int DiscoverIndex { get; }
    }

    public sealed class HomeSubjectItem : INotifyPropertyChanged
    {
        private CollectionStatusType? _status;

        public event PropertyChangedEventHandler PropertyChanged;

        public int SubjectId { get; set; }

        public string Title { get; set; }

        public string Subtitle { get; set; }

        public string Cover { get; set; }

        public string TypeText { get; set; }

        public SubjectType SubjectType { get; set; }

        public string WeekdayText { get; set; }

        public string CollectionText { get; set; }

        public string RatingText { get; set; }

        public string Meta { get; set; }

        public string RankText { get; set; }

        public string StatusText { get; set; }

        public CollectionStatusType? Status
        {
            get => _status;
            set
            {
                if (_status == value)
                {
                    return;
                }

                _status = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CollectionStatusText)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CollectionStatusVisibility)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CollectionStatusBrush)));
            }
        }

        public Visibility SubtitleVisibility => string.IsNullOrWhiteSpace(Subtitle) ? Visibility.Collapsed : Visibility.Visible;

        public Visibility WeekdayVisibility => string.IsNullOrWhiteSpace(WeekdayText) ? Visibility.Collapsed : Visibility.Visible;

        public Visibility CollectionVisibility => string.IsNullOrWhiteSpace(CollectionText) ? Visibility.Collapsed : Visibility.Visible;

        public Visibility RatingVisibility => string.IsNullOrWhiteSpace(RatingText) ? Visibility.Collapsed : Visibility.Visible;

        public Visibility MetaVisibility => string.IsNullOrWhiteSpace(Meta) ? Visibility.Collapsed : Visibility.Visible;

        public Visibility RankVisibility => string.IsNullOrWhiteSpace(RankText) ? Visibility.Collapsed : Visibility.Visible;

        public Visibility StatusVisibility => string.IsNullOrWhiteSpace(StatusText) ? Visibility.Collapsed : Visibility.Visible;

        public string CollectionStatusText => Status?.GetDesc(SubjectType) ?? string.Empty;

        public Visibility CollectionStatusVisibility => Status.HasValue ? Visibility.Visible : Visibility.Collapsed;

        public Brush CollectionStatusBrush => Converters.GetSolidColorBrush(Status);

        public Brush TypeBrush => new SolidColorBrush(GetTypeColor(SubjectType));

        private static Color GetTypeColor(SubjectType subjectType)
        {
            return subjectType switch
            {
                SubjectType.Anime => Color.FromArgb(255, 254, 138, 149),
                SubjectType.Book => Color.FromArgb(255, 13, 183, 243),
                SubjectType.Music => Color.FromArgb(255, 50, 200, 64),
                SubjectType.Game => Color.FromArgb(255, 254, 190, 88),
                SubjectType.Real => Color.FromArgb(255, 1, 173, 145),
                _ => Color.FromArgb(255, 254, 138, 149),
            };
        }
    }
}
