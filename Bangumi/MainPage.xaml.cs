using Bangumi.Api;
using Bangumi.Common;
using Bangumi.Data;
using Bangumi.Helper;
using Bangumi.Views;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;

namespace Bangumi
{
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        private const double MinimalShellThreshold = 620;
        private const double CompactShellThreshold = 900;
        private const double HeaderCollapseDistance = 62;
        private enum PrimarySection
        {
            Home,
            Timeline,
            Collection,
            Discover,
            Rakuen,
            Group,
            Messages,
            Profile,
            Tinygrail,
            Toolbox,
            Settings
        }

        private sealed class SecondarySection
        {
            public string Title { get; set; }
            public Type PageType { get; set; }
            public object Parameter { get; set; }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public static MainPage RootPage { get; private set; }
        public bool HasDialog = false;

        private readonly List<SecondarySection> _currentSections = new List<SecondarySection>();
        private readonly Dictionary<int, Frame> _sectionFrames = new Dictionary<int, Frame>();
        private PrimarySection? _currentPrimarySection;
        private bool _isInitialized;
        private bool _canGoBack;
        private bool _isCompactPaneTemporarilyOpen;
        private bool _isTemporaryOverlayOpen;
        private ScrollViewer _activeContentScrollViewer;
        private readonly Brush _transparentBrush = new SolidColorBrush(Colors.Transparent);

        public MainPage()
        {
            InitializeComponent();
            RootPage = this;
            HomeContentFrame.Navigated += SectionFrame_Navigated;

            ApplicationView.GetForCurrentView().SetPreferredMinSize(new Size(640, 520));
            ConfigureTitleBar();

            SystemNavigationManager.GetForCurrentView().BackRequested += (sender, e) =>
            {
                if (!e.Handled)
                {
                    e.Handled = TryGoBack();
                }
            };

            Window.Current.CoreWindow.PointerPressed += (sender, args) =>
            {
                if (args.CurrentPoint.Properties.IsXButton1Pressed)
                {
                    args.Handled = TryGoBack();
                }
            };

            MessageButton.Click += (sender, e) => ShowPrimarySection(PrimarySection.Messages);

            var dispatcher = Window.Current.Dispatcher;
            NetworkHelper.NetworkChanged += async (sender, e) =>
            {
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => OnPropertyChanged(nameof(IsOffline)));
            };

            BangumiApi.Init(
                Constants.ClientId,
                Constants.ClientSecret,
                Constants.RedirectUrl,
                ApplicationData.Current.LocalFolder.Path,
                ApplicationData.Current.LocalCacheFolder.Path,
                EncryptionHelper.EncryptionAsync,
                EncryptionHelper.DecryptionAsync);

            if (SettingHelper.UseBangumiData)
            {
                BangumiData.Init(
                    Path.Combine(ApplicationData.Current.LocalFolder.Path, "bangumi-data"),
                    SettingHelper.UseBiliApp,
                    message => NotificationHelper.Notify(message));
            }
        }

        public bool IsLoading => CurrentPageStatus?.IsLoading ?? false;
        public bool IsOffline => NetworkHelper.IsOffline;
        public bool IsRefreshable => CurrentPageStatus != null;

        public bool CanGoBack
        {
            get => _canGoBack;
            private set => Set(ref _canGoBack, value);
        }

        public void PageStatusChanged()
        {
            OnPropertyChanged(nameof(IsRefreshable));
            OnPropertyChanged(nameof(IsLoading));
            UpdateBackButtonStatus();
        }

        public void ResetFrameBackStack()
        {
            if (Frame?.CanGoBack == true)
            {
                while (Frame.CanGoBack)
                {
                    Frame.BackStack.RemoveAt(Frame.BackStackDepth - 1);
                }
            }

            ShowPrimarySection(PrimarySection.Home);
        }

        public void NavigateToPage(Type type, object parameter, NavigationTransitionInfo transitionInfo)
        {
            if (type == null)
            {
                return;
            }

            if (type == typeof(ProgressPage))
            {
                ShowPrimarySection(PrimarySection.Collection);
                return;
            }

            if (type == typeof(CalendarPage))
            {
                ShowPrimarySection(PrimarySection.Timeline);
                return;
            }

            if (type == typeof(CollectionPage))
            {
                ShowPrimarySection(PrimarySection.Collection);
                return;
            }

            if (type == typeof(SearchPage))
            {
                NavigateToDiscoverSection(0);
                return;
            }

            Frame?.Navigate(type, parameter, transitionInfo);
        }

        public void SelectPlaceholderItem(string title)
        {
            SectionTitleText.Text = title;
        }

        public void SelectMainTab(int index)
        {
            if (index >= 0 && index < SectionTabList.Items.Count)
            {
                SectionTabList.SelectedIndex = index;
            }
        }

        public void NavigateToDiscoverSection(int index = 0)
        {
            ShowPrimarySection(PrimarySection.Discover);
            if (index >= 0 && index < SectionTabList.Items.Count)
            {
                SectionTabList.SelectedIndex = index;
            }
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isInitialized)
            {
                await UpdateAvatar();
                return;
            }

            _isInitialized = true;
            await UpdateAvatar();
            ShowPrimarySection(BangumiApi.BgmOAuth.IsLogin ? PrimarySection.Home : PrimarySection.Timeline);
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            if (BangumiApi.BgmOAuth.IsLogin)
            {
                string choice = string.Empty;
                var dialog = new Windows.UI.Popups.MessageDialog("确定要退出登录吗？") { Title = "注销" };
                dialog.Commands.Add(new Windows.UI.Popups.UICommand("确定", command => choice = command.Label));
                dialog.Commands.Add(new Windows.UI.Popups.UICommand("取消", command => choice = command.Label));
                await dialog.ShowAsync();

                if (choice == "确定")
                {
                    BangumiApi.BgmOAuth.DeleteUserFiles();
                    Frame.Navigate(typeof(LoginPage), null, new DrillInNavigationTransitionInfo());
                }
            }
            else
            {
                Frame.Navigate(typeof(LoginPage), null, new DrillInNavigationTransitionInfo());
            }
        }

        private void SidebarNavButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is string tag))
            {
                return;
            }

            if (IsSidebarButtonSelected(tag))
            {
                return;
            }

            switch (tag)
            {
                case "home":
                    ShowPrimarySection(PrimarySection.Home);
                    break;
                case "timeline":
                    ShowPrimarySection(PrimarySection.Timeline);
                    break;
                case "collection":
                    ShowPrimarySection(PrimarySection.Collection);
                    break;
                case "discover":
                    ShowPrimarySection(PrimarySection.Discover);
                    break;
                case "rakuen":
                    ShowPrimarySection(PrimarySection.Rakuen);
                    break;
                case "group":
                    ShowPrimarySection(PrimarySection.Group);
                    break;
                case "messages":
                    ShowPrimarySection(PrimarySection.Messages);
                    break;
                case "profile":
                    ShowPrimarySection(PrimarySection.Profile);
                    break;
                case "tinygrail":
                    ShowPrimarySection(PrimarySection.Tinygrail);
                    break;
                case "toolbox":
                    ShowPrimarySection(PrimarySection.Toolbox);
                    break;
                case "settings":
                    ShowPrimarySection(PrimarySection.Settings);
                    break;
            }
        }

        private void RootLayout_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyResponsiveShell(e.NewSize.Width);
        }

        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            RootSplitView.IsPaneOpen = !RootSplitView.IsPaneOpen;

            if (RootSplitView.DisplayMode == SplitViewDisplayMode.Overlay)
            {
                _isTemporaryOverlayOpen = RootSplitView.IsPaneOpen;
            }
            else if (RootSplitView.DisplayMode == SplitViewDisplayMode.CompactInline)
            {
                _isCompactPaneTemporarilyOpen = RootSplitView.IsPaneOpen;
            }

            UpdateSidebarVisualMode(GetSidebarExpandedVisual());
        }

        private void SidebarButton_PointerEntered(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is Button button)
            {
                ApplySidebarButtonVisualState(button);
            }
        }

        private void SidebarButton_PointerExited(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is Button button)
            {
                ApplySidebarButtonVisualState(button);
            }
        }

        private async void PageRefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshCurrentSection();
        }

        private void SearchTriggerButton_Click(object sender, RoutedEventArgs e)
        {
            RootSplitView.IsPaneOpen = true;
            if (RootSplitView.DisplayMode == SplitViewDisplayMode.Overlay)
            {
                _isTemporaryOverlayOpen = true;
            }
            else if (RootSplitView.DisplayMode == SplitViewDisplayMode.CompactInline)
            {
                _isCompactPaneTemporarilyOpen = true;
            }
            UpdateSidebarVisualMode(GetSidebarExpandedVisual());
        }

        private void GlobalSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            ShowPrimarySection(PrimarySection.Discover);
            if (SectionTabList.Items.Count > 0)
            {
                SectionTabList.SelectedIndex = 0;
            }
        }

        private void SectionTabList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SectionTabList.SelectedIndex < 0 || SectionTabList.SelectedIndex >= _currentSections.Count)
            {
                return;
            }

            ResetHeaderVisuals();
            ShowSecondarySection(SectionTabList.SelectedIndex);
            AttachSelectedSectionScrollBehavior();
            PageStatusChanged();
        }

        private void ShowPrimarySection(PrimarySection section)
        {
            var previousSection = _currentPrimarySection;
            _currentPrimarySection = section;
            SectionTitleText.Text = GetPrimaryTitle(section);
            HomeHeaderActions.Visibility = Visibility.Collapsed;
            ResetHeaderVisuals();

            if (section == PrimarySection.Home)
            {
                SectionTabList.Visibility = Visibility.Collapsed;
                HomeContentFrame.Visibility = Visibility.Visible;

                if (HomeContentFrame.Content?.GetType() != typeof(HomePage))
                {
                    var transition = HomeContentFrame.Content == null
                        ? (NavigationTransitionInfo)new EntranceNavigationTransitionInfo()
                        : new SuppressNavigationTransitionInfo();
                    HomeContentFrame.Navigate(typeof(HomePage), null, transition);
                }

                SelectNavigationItem(section);
                AnimatePrimarySectionTransition(previousSection, section);
                AttachSelectedSectionScrollBehavior();
                PageStatusChanged();
                return;
            }

            HomeContentFrame.Visibility = Visibility.Collapsed;
            SectionTabList.Visibility = Visibility.Visible;

            _currentSections.Clear();
            _currentSections.AddRange(CreateSections(section));

            BuildSectionTabs();
            SelectNavigationItem(section);

            if (_currentSections.Count > 0)
            {
                SectionTabList.SelectedIndex = 0;
                ShowSecondarySection(0);
                AttachSelectedSectionScrollBehavior();
            }
            else
            {
                SectionTabList.SelectedIndex = -1;
            }

            AnimatePrimarySectionTransition(previousSection, section);
            PageStatusChanged();
        }

        private IReadOnlyList<SecondarySection> CreateSections(PrimarySection section)
        {
            switch (section)
            {
                case PrimarySection.Home:
                    return new[]
                    {
                        new SecondarySection { Title = "发现", PageType = typeof(HomePage) }
                    };
                case PrimarySection.Timeline:
                    return new[]
                    {
                        new SecondarySection { Title = "全部", PageType = typeof(CalendarPage) },
                        Placeholder("动画", "对应时间线动画流。"),
                        Placeholder("书籍", "对应时间线书籍动态。"),
                        Placeholder("音乐", "对应时间线音乐动态。"),
                        Placeholder("游戏", "对应时间线游戏动态。"),
                        Placeholder("三次元", "对应时间线三次元动态。"),
                        Placeholder("吐槽", "对应 timeline/say 广播流。")
                    };
                case PrimarySection.Collection:
                    return new[]
                    {
                        new SecondarySection { Title = "全部", PageType = typeof(CollectionPage) },
                        Placeholder("动画", "对应动画收藏筛选。"),
                        Placeholder("书籍", "对应书籍收藏筛选。"),
                        Placeholder("音乐", "对应音乐收藏筛选。"),
                        Placeholder("游戏", "对应游戏收藏筛选。"),
                        Placeholder("三次元", "对应三次元收藏筛选。"),
                        Placeholder("目录", "对应目录收藏与目录管理。"),
                        Placeholder("人物", "对应人物收藏与角色收藏。")
                    };
                case PrimarySection.Discover:
                    return new[]
                    {
                        new SecondarySection { Title = "条目", PageType = typeof(SearchPage), Parameter = 0 },
                        new SecondarySection { Title = "动画", PageType = typeof(SearchPage), Parameter = 1 },
                        new SecondarySection { Title = "书籍", PageType = typeof(SearchPage), Parameter = 2 },
                        new SecondarySection { Title = "音乐", PageType = typeof(SearchPage), Parameter = 3 },
                        new SecondarySection { Title = "游戏", PageType = typeof(SearchPage), Parameter = 4 },
                        new SecondarySection { Title = "三次元", PageType = typeof(SearchPage), Parameter = 5 },
                        Placeholder("人物", "对应 discovery/search 人物搜索。"),
                        Placeholder("用户", "对应 discovery/search 用户搜索。")
                    };
                case PrimarySection.Rakuen:
                    return new[]
                    {
                        Placeholder("超展开", "对应 rakuen/v2 综合流。"),
                        Placeholder("条目", "对应 rakuen/topic 条目讨论。"),
                        Placeholder("目录", "对应 rakuen/board 目录讨论。"),
                        Placeholder("小组", "对应 rakuen/group 小组讨论。"),
                        Placeholder("人物", "对应人物与角色讨论。"),
                        Placeholder("日志", "对应 rakuen/blog 日志讨论。"),
                        Placeholder("历史", "对应 rakuen/history。"),
                        Placeholder("通知", "对应 rakuen/notify。"),
                        Placeholder("我的", "对应 rakuen/mine。"),
                        Placeholder("点评", "对应 rakuen/reviews。"),
                        Placeholder("赞同", "对应 rakuen/ugc-agree。"),
                        Placeholder("搜索", "对应 rakuen/search。"),
                        Placeholder("设置", "对应 rakuen/setting。")
                    };
                case PrimarySection.Group:
                    return new[]
                    {
                        Placeholder("小组广场", "对应小组广场。"),
                        Placeholder("我的小组", "对应已加入小组。"),
                        Placeholder("全部话题", "对应小组全部话题流。"),
                        Placeholder("热门讨论", "对应热门讨论。"),
                        Placeholder("回顾", "对应历史主题回顾。")
                    };
                case PrimarySection.Messages:
                    return new[]
                    {
                        Placeholder("通知", "对应消息通知聚合。"),
                        Placeholder("提醒", "对应回复和系统提醒。"),
                        Placeholder("私信", "对应 user/pm 私信。"),
                        Placeholder("广播", "对应广播与互动消息。"),
                        Placeholder("超展开提醒", "对应 Rakuen 提醒。")
                    };
                case PrimarySection.Profile:
                    return new[]
                    {
                        Placeholder("概览", "对应 user/v2 概览。"),
                        Placeholder("时光机", "对应 user/timeline。"),
                        Placeholder("收藏", "对应个人收藏概览。"),
                        Placeholder("目录", "对应 user/catalogs。"),
                        Placeholder("日志", "对应 user/blogs。"),
                        Placeholder("好友", "对应 user/friends。"),
                        Placeholder("动作", "对应 user/actions。"),
                        Placeholder("里程碑", "对应 user/milestone。"),
                        Placeholder("备份", "对应 user/backup。"),
                        Placeholder("开发者", "对应 user/dev。"),
                        Placeholder("服务器状态", "对应 user/server-status。"),
                        Placeholder("私信", "对应 user/pm。"),
                        Placeholder("Zone", "对应 user/zone。"),
                        Placeholder("SMB", "对应 user/smb。"),
                        Placeholder("Qiafan", "对应 user/qiafan。"),
                        Placeholder("赞助", "对应 user/sponsor。"),
                        Placeholder("用户设置", "对应 user/user-setting。"),
                        Placeholder("源站设置", "对应 user/origin-setting。"),
                        Placeholder("设置", "对应 user/setting。")
                    };
                case PrimarySection.Tinygrail:
                    return new[]
                    {
                        Placeholder("首页", "对应 tinygrail/index。"),
                        Placeholder("总览", "对应 tinygrail/overview。"),
                        Placeholder("ICO", "对应 tinygrail/ico。"),
                        Placeholder("交易", "对应 tinygrail/trade。"),
                        Placeholder("拍卖", "对应 tinygrail/advance-auction。"),
                        Placeholder("持仓", "对应 tinygrail/chara-assets。"),
                        Placeholder("日志", "对应 tinygrail/logs。"),
                        Placeholder("圣殿", "对应 tinygrail/temples。"),
                        Placeholder("周榜", "对应 tinygrail/top-week。"),
                        Placeholder("搜索", "对应 tinygrail/search。"),
                        Placeholder("Valhall", "对应 tinygrail/valhall。"),
                        Placeholder("成交", "对应 tinygrail/deal。"),
                        Placeholder("资产条目", "对应 tinygrail/items。"),
                        Placeholder("关系", "对应 tinygrail/relation。"),
                        Placeholder("新番", "对应 tinygrail/new-bangumi。"),
                        Placeholder("树", "对应 tinygrail/tree。"),
                        Placeholder("富豪树", "对应 tinygrail/tree-rich。"),
                        Placeholder("富豪", "对应 tinygrail/rich。"),
                        Placeholder("事务", "对应 tinygrail/transaction。"),
                        Placeholder("高级", "对应 tinygrail/advance。"),
                        Placeholder("高级提问", "对应 tinygrail/advance-ask。"),
                        Placeholder("高级拍卖2", "对应 tinygrail/advance-auction2。"),
                        Placeholder("高级竞拍", "对应 tinygrail/advance-bid。"),
                        Placeholder("高级献祭", "对应 tinygrail/advance-sacrifice。"),
                        Placeholder("高级状态", "对应 tinygrail/advance-state。"),
                        Placeholder("竞价", "对应 tinygrail/bid。"),
                        Placeholder("献祭", "对应 tinygrail/sacrifice。"),
                        Placeholder("Wiki", "对应 tinygrail/wiki。")
                    };
                case PrimarySection.Toolbox:
                    return new[]
                    {
                        Placeholder("页面索引", "对应本地整理的 czy0729/Bangumi 页面索引。"),
                        Placeholder("同步", "对应 web-view/bilibili-sync 和 douban-sync。"),
                        Placeholder("信息", "对应 web-view/information。"),
                        Placeholder("日志", "对应 web-view/log。"),
                        Placeholder("提示", "对应 web-view/tips。"),
                        Placeholder("版本", "对应 web-view/versions。"),
                        Placeholder("浏览器", "对应 web-view/web-browser。"),
                        Placeholder("Playground", "对应 web-view/playground。"),
                        Placeholder("Webhook", "对应 web-view/webhook。")
                    };
                case PrimarySection.Settings:
                    return new[]
                    {
                        Placeholder("应用设置", "当前先提供稳定的设置 UI 占位。"),
                        Placeholder("用户设置", "对应 user/user-setting。"),
                        Placeholder("源站设置", "对应 user/origin-setting。"),
                        Placeholder("超展开设置", "对应 rakuen/setting。"),
                        Placeholder("同步设置", "对应同步和外部服务设置。")
                    };
                default:
                    return Array.Empty<SecondarySection>();
            }
        }

        private SecondarySection Placeholder(string title, string description)
        {
            return new SecondarySection
            {
                Title = title,
                PageType = typeof(ShellPlaceholderPage),
                Parameter = new ShellPlaceholderPage.PlaceholderState
                {
                    Title = title,
                    Description = description
                }
            };
        }

        private void BuildSectionTabs()
        {
            SectionTabList.Items.Clear();
            _sectionFrames.Clear();

            for (int i = 0; i < _currentSections.Count; i++)
            {
                var section = _currentSections[i];
                var frame = new Frame
                {
                    Background = new SolidColorBrush(Colors.Transparent)
                };
                frame.Navigated += SectionFrame_Navigated;
                _sectionFrames[i] = frame;

                var contentHost = new Grid
                {
                    Margin = new Thickness(0),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };
                contentHost.Children.Add(frame);

                SectionTabList.Items.Add(new PivotItem
                {
                    Header = section.Title,
                    Content = contentHost
                });
            }
        }

        private Frame GetSectionFrame(int index)
        {
            if (index < 0)
            {
                return null;
            }

            return _sectionFrames.TryGetValue(index, out var frame) ? frame : null;
        }

        private void ShowSecondarySection(int index)
        {
            if (index < 0 || index >= _currentSections.Count)
            {
                return;
            }

            var section = _currentSections[index];
            var frame = GetSectionFrame(index);
            if (frame == null)
            {
                return;
            }

            try
            {
                if (frame.Content?.GetType() == section.PageType && Equals(frame.Tag, section.Parameter))
                {
                    return;
                }

                var transition = frame.Content == null
                    ? (NavigationTransitionInfo)new EntranceNavigationTransitionInfo()
                    : new SuppressNavigationTransitionInfo();

                frame.Tag = section.Parameter;
                frame.Navigate(section.PageType, section.Parameter, transition);
            }
            catch (Exception ex)
            {
                var transition = frame.Content == null
                    ? (NavigationTransitionInfo)new EntranceNavigationTransitionInfo()
                    : new SuppressNavigationTransitionInfo();

                frame.Navigate(
                    typeof(ShellPlaceholderPage),
                    new ShellPlaceholderPage.PlaceholderState
                    {
                        Title = section.Title,
                        Description = $"当前页面暂时只提供 UI 占位。\n原始页面加载失败：{ex.GetType().Name}"
                    },
                    transition);
                frame.Tag = section.Parameter;
            }
        }

        private void AnimatePrimarySectionTransition(PrimarySection? previousSection, PrimarySection currentSection)
        {
            if (HeaderHost == null || HeaderHostTransform == null)
            {
                return;
            }

            if (!previousSection.HasValue || previousSection.Value == currentSection)
            {
                HeaderHost.Opacity = 1;
                HeaderHostTransform.Y = 0;
                return;
            }

            double fromOffset = previousSection.Value < currentSection ? 22 : -22;
            HeaderHost.Opacity = 0;
            HeaderHostTransform.Y = fromOffset;

            var storyboard = new Storyboard();

            var opacityAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(380),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(opacityAnimation, HeaderHost);
            Storyboard.SetTargetProperty(opacityAnimation, "Opacity");

            var offsetAnimation = new DoubleAnimation
            {
                From = fromOffset,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(380),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(offsetAnimation, HeaderHostTransform);
            Storyboard.SetTargetProperty(offsetAnimation, "Y");

            storyboard.Children.Add(opacityAnimation);
            storyboard.Children.Add(offsetAnimation);
            storyboard.Begin();
        }

        private void SelectNavigationItem(PrimarySection section)
        {
            _currentPrimarySection = section;
            string tag = section switch
            {
                PrimarySection.Home => "home",
                PrimarySection.Timeline => "timeline",
                PrimarySection.Collection => "collection",
                PrimarySection.Discover => "discover",
                PrimarySection.Rakuen => "rakuen",
                PrimarySection.Group => "group",
                PrimarySection.Messages => "messages",
                PrimarySection.Profile => "profile",
                PrimarySection.Tinygrail => "tinygrail",
                PrimarySection.Toolbox => "toolbox",
                PrimarySection.Settings => "settings",
                _ => null
            };

            if (tag == null)
            {
                return;
            }

            foreach (var button in FindDescendantButtons(SidebarPaneRoot).Where(btn => btn.Tag is string))
            {
                bool isSelected = string.Equals(button.Tag as string, tag, StringComparison.Ordinal);
                foreach (var grid in FindDescendants<Grid>(button))
                {
                    if (grid.ColumnDefinitions.Count != 3)
                    {
                        continue;
                    }

                    foreach (var border in FindDescendants<Border>(grid))
                    {
                        if (Grid.GetColumn(border) == 0)
                        {
                            AnimateIndicator(border, isSelected);
                        }
                    }
                }

                ApplySidebarButtonVisualState(button);
            }

            if (RootSplitView.DisplayMode == SplitViewDisplayMode.Overlay)
            {
                RootSplitView.IsPaneOpen = false;
                _isTemporaryOverlayOpen = false;
            }
            else if (RootSplitView.DisplayMode == SplitViewDisplayMode.CompactInline && RootSplitView.IsPaneOpen)
            {
                RootSplitView.IsPaneOpen = false;
                _isCompactPaneTemporarilyOpen = false;
            }

            UpdateSidebarVisualMode(GetSidebarExpandedVisual());
        }

        private string GetPrimaryTitle(PrimarySection section)
        {
            switch (section)
            {
                case PrimarySection.Home: return "发现";
                case PrimarySection.Timeline: return "时间线";
                case PrimarySection.Collection: return "收藏";
                case PrimarySection.Discover: return "搜索";
                case PrimarySection.Rakuen: return "超展开";
                case PrimarySection.Group: return "小组";
                case PrimarySection.Messages: return "消息";
                case PrimarySection.Profile: return "我的";
                case PrimarySection.Tinygrail: return "小圣杯";
                case PrimarySection.Toolbox: return "工具箱";
                case PrimarySection.Settings: return "设置";
                default: return "Bangumi";
            }
        }

        private IPageStatus CurrentPageStatus
        {
            get
            {
                if (_currentPrimarySection == PrimarySection.Home)
                {
                    return HomeContentFrame?.Content as IPageStatus;
                }

                var frame = GetSectionFrame(SectionTabList.SelectedIndex);
                return frame?.Content as IPageStatus;
            }
        }

        private async Task RefreshCurrentSection()
        {
            if (CurrentPageStatus != null)
            {
                await CurrentPageStatus.Refresh();
            }
        }

        private bool TryGoBack()
        {
            if (HasDialog)
            {
                return false;
            }

            return false;
        }

        private async Task UpdateAvatar()
        {
            BitmapImage image;
            Uri avatarUri;

            if (BangumiApi.BgmOAuth.IsLogin && !NetworkHelper.IsOffline)
            {
                try
                {
                    var user = await BangumiApi.BgmApi.User();
                    ToolTipService.SetToolTip(LoginButton, $"{user.NickName}({user.UserName}@{user.Id})");
                    avatarUri = new Uri(user.Avatar.Small);
                    image = new BitmapImage(avatarUri);
                }
                catch (Exception)
                {
                    ToolTipService.SetToolTip(LoginButton, null);
                    avatarUri = new Uri("ms-appx:///Assets/Shell/avatar.png");
                    image = new BitmapImage(avatarUri);
                }
            }
            else
            {
                avatarUri = new Uri("ms-appx:///Assets/Shell/avatar.png");
                image = new BitmapImage(avatarUri);
            }

            AvaterImage.ImageSource = image;
        }

        private void ApplyResponsiveShell(double width)
        {
            if (width <= 0)
            {
                return;
            }

            bool minimal = width < MinimalShellThreshold;
            bool compact = width >= MinimalShellThreshold && width < CompactShellThreshold;

            RootSplitView.OpenPaneLength = 212;
            RootSplitView.CompactPaneLength = 56;

            if (minimal)
            {
                RootSplitView.DisplayMode = SplitViewDisplayMode.Overlay;
                RootSplitView.IsPaneOpen = _isTemporaryOverlayOpen;
            }
            else if (compact)
            {
                RootSplitView.DisplayMode = SplitViewDisplayMode.CompactInline;
                RootSplitView.IsPaneOpen = _isCompactPaneTemporarilyOpen;
            }
            else
            {
                RootSplitView.DisplayMode = SplitViewDisplayMode.Inline;
                RootSplitView.IsPaneOpen = true;
                _isTemporaryOverlayOpen = false;
                _isCompactPaneTemporarilyOpen = false;
            }

            MenuButton.Visibility = (minimal || compact) ? Visibility.Visible : Visibility.Collapsed;
            SearchHost.Visibility = width < MinimalShellThreshold ? Visibility.Collapsed : Visibility.Visible;
            SearchTriggerButton.Visibility = width < MinimalShellThreshold ? Visibility.Visible : Visibility.Collapsed;
            PaneSearchHost.Visibility = width < MinimalShellThreshold ? Visibility.Visible : Visibility.Collapsed;
            AppNameText.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            AppNameBlock.Margin = compact ? new Thickness(6, 0, 8, 0) : new Thickness(8, 0, 18, 0);
            AppNameBlock.Spacing = compact ? 6 : 8;
            MenuButton.Width = compact ? 56 : 46;
            MenuButton.Height = 48;
            AppIconImage.Width = compact ? 20 : 18;
            AppIconImage.Height = compact ? 20 : 18;

            if (compact)
            {
                AppNameBlock.VerticalAlignment = VerticalAlignment.Center;
                AppNameBlock.HorizontalAlignment = HorizontalAlignment.Left;
            }
            else
            {
                AppNameBlock.VerticalAlignment = VerticalAlignment.Center;
                AppNameBlock.HorizontalAlignment = HorizontalAlignment.Stretch;
            }

            if (width < MinimalShellThreshold)
            {
                SearchHost.Width = 0;
                GlobalSearchBox.PlaceholderText = string.Empty;
                PaneSearchBox.PlaceholderText = "搜索条目";
            }
            else if (compact)
            {
                SearchHost.Width = Math.Max(220, Math.Min(300, width - 560));
                GlobalSearchBox.PlaceholderText = SearchHost.Width < 210 ? string.Empty : "搜索条目";
            }
            else
            {
                SearchHost.Width = Math.Min(460, Math.Max(320, width * 0.21));
                GlobalSearchBox.PlaceholderText = "搜索条目";
            }

            UpdateSidebarVisualMode(GetSidebarExpandedVisual());
        }

        private bool GetSidebarExpandedVisual()
        {
            if (RootSplitView == null)
            {
                return true;
            }

            switch (RootSplitView.DisplayMode)
            {
                case SplitViewDisplayMode.CompactInline:
                    return RootSplitView.IsPaneOpen;
                case SplitViewDisplayMode.Overlay:
                    return RootSplitView.IsPaneOpen;
                default:
                    return true;
            }
        }

        private void AttachSelectedSectionScrollBehavior()
        {
            if (_activeContentScrollViewer != null)
            {
                _activeContentScrollViewer.ViewChanged -= ActiveContentScrollViewer_ViewChanged;
                _activeContentScrollViewer = null;
            }

            DependencyObject root;
            if (_currentPrimarySection == PrimarySection.Home)
            {
                root = HomeContentFrame?.Content as DependencyObject;
            }
            else
            {
                var frame = GetSectionFrame(SectionTabList.SelectedIndex);
                root = frame?.Content as DependencyObject;
            }
            _activeContentScrollViewer = FindDescendantScrollViewer(root);

            if (_activeContentScrollViewer != null)
            {
                _activeContentScrollViewer.ViewChanged += ActiveContentScrollViewer_ViewChanged;
                UpdateHeaderByScroll(_activeContentScrollViewer.VerticalOffset);
            }
            else
            {
                ResetHeaderVisuals();
            }
        }

        private void ActiveContentScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            UpdateHeaderByScroll((sender as ScrollViewer)?.VerticalOffset ?? 0);
        }

        private void UpdateHeaderByScroll(double verticalOffset)
        {
            double collapse = Math.Max(0, Math.Min(HeaderCollapseDistance, verticalOffset * 1.7));
            double progress = collapse / HeaderCollapseDistance;

            SectionHeaderGrid.Height = Math.Max(0, HeaderCollapseDistance - collapse);
            SectionTitleTransform.Y = -Math.Min(12, collapse * 0.24);
            SectionHeaderGrid.Opacity = 1 - Math.Min(1, progress * 2.2);
        }

        private void ResetHeaderVisuals()
        {
            SectionHeaderGrid.Height = HeaderCollapseDistance;
            SectionHeaderGrid.Opacity = 1;
            SectionTitleTransform.Y = 0;
        }

        private void HomeTimelineButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPrimarySection(PrimarySection.Timeline);
        }

        private void HomeCollectionButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPrimarySection(PrimarySection.Collection);
        }

        private void HomeSearchButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateToDiscoverSection(1);
        }

        private async void HomeRefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshCurrentSection();
        }

        private void SectionFrame_Navigated(object sender, Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            if (e.Content is FrameworkElement element)
            {
                element.Loaded -= SectionContentElement_Loaded;
                element.Loaded += SectionContentElement_Loaded;
            }

            PageStatusChanged();
        }

        private void SectionContentElement_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                element.Loaded -= SectionContentElement_Loaded;
            }

            _ = Window.Current.Dispatcher.RunAsync(CoreDispatcherPriority.Low, AttachSelectedSectionScrollBehavior);
        }

        private ScrollViewer FindDescendantScrollViewer(DependencyObject root)
        {
            if (root == null)
            {
                return null;
            }

            var viewers = new List<ScrollViewer>();
            CollectDescendantScrollViewers(root, viewers);
            return viewers
                .OrderByDescending(viewer => viewer.ScrollableHeight)
                .ThenByDescending(viewer => viewer.ActualHeight)
                .FirstOrDefault();
        }

        private void CollectDescendantScrollViewers(DependencyObject root, IList<ScrollViewer> viewers)
        {
            if (root == null)
            {
                return;
            }

            if (root is ScrollViewer viewer)
            {
                viewers.Add(viewer);
            }

            int childCount = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < childCount; i++)
            {
                CollectDescendantScrollViewers(VisualTreeHelper.GetChild(root, i), viewers);
            }
        }

        private IEnumerable<Button> FindDescendantButtons(DependencyObject root)
        {
            if (root == null)
            {
                yield break;
            }

            if (root is Button button)
            {
                yield return button;
            }

            int childCount = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < childCount; i++)
            {
                foreach (var childButton in FindDescendantButtons(VisualTreeHelper.GetChild(root, i)))
                {
                    yield return childButton;
                }
            }
        }

        private IEnumerable<T> FindDescendants<T>(DependencyObject root) where T : DependencyObject
        {
            if (root == null)
            {
                yield break;
            }

            if (root is T match)
            {
                yield return match;
            }

            int childCount = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < childCount; i++)
            {
                foreach (var child in FindDescendants<T>(VisualTreeHelper.GetChild(root, i)))
                {
                    yield return child;
                }
            }
        }

        private void UpdateSidebarVisualMode(bool expanded)
        {
            bool compactIconMode = !expanded;
            SidebarPaneRoot.Width = compactIconMode ? RootSplitView.CompactPaneLength : double.NaN;
            SidebarPaneRoot.HorizontalAlignment = HorizontalAlignment.Left;

            foreach (var header in FindDescendants<TextBlock>(SidebarPaneRoot)
                .Where(text => text.Style == Resources["SidebarSectionHeaderStyle"] as Style))
            {
                header.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
            }

            foreach (var button in FindDescendantButtons(SidebarPaneRoot).Where(btn => btn.Tag is string))
            {
                string buttonTag = button.Tag as string;
                button.Width = compactIconMode ? RootSplitView.CompactPaneLength : RootSplitView.OpenPaneLength;
                button.HorizontalAlignment = HorizontalAlignment.Left;
                button.Height = compactIconMode ? 38 : 38;
                button.Margin = compactIconMode ? new Thickness(0, 3, 0, 3) : new Thickness(0);
                ToolTipService.SetToolTip(button, compactIconMode ? GetNavigationLabel(buttonTag) : null);
                button.PointerEntered -= SidebarButton_PointerEntered;
                button.PointerExited -= SidebarButton_PointerExited;
                button.PointerEntered += SidebarButton_PointerEntered;
                button.PointerExited += SidebarButton_PointerExited;
                button.BorderThickness = IsSidebarButtonSelected(buttonTag)
                    ? new Thickness(0, 1, 0, 1)
                    : new Thickness(0);
                button.BorderBrush = IsSidebarButtonSelected(buttonTag)
                    ? new SolidColorBrush(Color.FromArgb(0x26, 0x00, 0x00, 0x00))
                    : _transparentBrush;

                if (button.Content is Grid contentGrid)
                {
                    var contentText = contentGrid.Children
                        .OfType<TextBlock>()
                        .FirstOrDefault(text => Grid.GetColumn(text) == 2);

                    if (contentText != null)
                    {
                        contentText.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
                        contentText.FontSize = 14;
                        contentText.FontWeight = new Windows.UI.Text.FontWeight { Weight = 400 };
                        contentText.Foreground = (Brush)Application.Current.Resources["ShellPrimaryTextBrush"];
                        contentText.Margin = new Thickness(2, 0, 0, 0);
                    }

                    contentGrid.Padding = expanded ? new Thickness(0, 0, 14, 0) : new Thickness(0);

                    if (contentGrid.ColumnDefinitions.Count == 3)
                    {
                        contentGrid.ColumnDefinitions[0].Width = new GridLength(4);
                        contentGrid.ColumnDefinitions[1].Width = new GridLength(expanded ? 50 : 52);
                        contentGrid.ColumnDefinitions[2].Width = expanded ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
                    }

                    var indicator = contentGrid.Children
                        .OfType<Border>()
                        .FirstOrDefault(border => Grid.GetColumn(border) == 0);

                    if (indicator != null)
                    {
                        indicator.Width = 4;
                        indicator.Height = 21;
                        indicator.HorizontalAlignment = HorizontalAlignment.Left;
                        indicator.VerticalAlignment = VerticalAlignment.Center;
                        indicator.Margin = new Thickness(0);
                    }

                    foreach (var icon in contentGrid.Children.OfType<IconElement>())
                    {
                        icon.HorizontalAlignment = HorizontalAlignment.Center;
                        icon.VerticalAlignment = VerticalAlignment.Center;
                        icon.Width = 22;
                        icon.Height = 22;
                    }
                }

                ApplySidebarButtonVisualState(button);
            }

            foreach (var separator in FindDescendants<Border>(SidebarPaneRoot)
                .Where(border => border.Height == 1))
            {
                separator.Margin = expanded ? new Thickness(0, 5, 0, 0) : new Thickness(0, 6, 0, 0);
            }

            if (SidebarButtonHost != null)
            {
                SidebarButtonHost.Padding = expanded ? new Thickness(0, 3, 0, 0) : new Thickness(0, 8, 0, 0);
            }

            if (SidebarFooterHost != null)
            {
                SidebarFooterHost.Margin = expanded ? new Thickness(0, 0, 0, 3) : new Thickness(0, 0, 0, 2);
            }
        }

        private void ApplySidebarButtonVisualState(Button button)
        {
            if (!(button?.Tag is string tag))
            {
                return;
            }

            bool expanded = GetSidebarExpandedVisual();
            bool isSelected = IsSidebarButtonSelected(tag);

            Brush background = _transparentBrush;
            if (isSelected)
            {
                background = expanded
                    ? (Brush)Resources["SidebarSelectedBrush"]
                    : (Brush)Resources["SidebarSelectedCompactBrush"];

                if (button.IsPointerOver)
                {
                    StartSelectedHoverAnimation(button);
                }
                else
                {
                    StopSelectedHoverAnimation(button);
                }
            }
            else if (button.IsPointerOver)
            {
                background = (Brush)Resources["SidebarHoverBrush"];
                StopSelectedHoverAnimation(button);
            }
            else
            {
                StopSelectedHoverAnimation(button);
            }

            button.Background = background;
            button.Foreground = (Brush)Application.Current.Resources["ShellPrimaryTextBrush"];
            button.BorderThickness = new Thickness(0);
            button.BorderBrush = _transparentBrush;
        }

        private void StartSelectedHoverAnimation(Button button)
        {
            if (!(button.Background is GradientBrush gradient))
            {
                return;
            }

            var targetStop = gradient.GradientStops.Count > 1 ? gradient.GradientStops[1] : null;
            if (targetStop == null)
            {
                return;
            }

            var animation = new DoubleAnimation
            {
                To = 0.5,
                Duration = new Duration(TimeSpan.FromMilliseconds(220)),
                EnableDependentAnimation = true,
                EasingFunction = new QuadraticEase()
            };
            Storyboard.SetTarget(animation, targetStop);
            Storyboard.SetTargetProperty(animation, "Offset");

            var storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            storyboard.Begin();
        }

        private void StopSelectedHoverAnimation(Button button)
        {
            if (!(button.Background is GradientBrush gradient))
            {
                return;
            }

            var targetStop = gradient.GradientStops.Count > 1 ? gradient.GradientStops[1] : null;
            if (targetStop == null)
            {
                return;
            }

            var animation = new DoubleAnimation
            {
                To = 0.34,
                Duration = new Duration(TimeSpan.FromMilliseconds(180)),
                EnableDependentAnimation = true,
                EasingFunction = new QuadraticEase()
            };
            Storyboard.SetTarget(animation, targetStop);
            Storyboard.SetTargetProperty(animation, "Offset");

            var storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            storyboard.Begin();
        }

        private void AnimateIndicator(Border indicator, bool isSelected)
        {
            if (indicator == null)
            {
                return;
            }

            var animation = new DoubleAnimation
            {
                To = isSelected ? 1 : 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(160)),
                EasingFunction = new QuadraticEase()
            };

            Storyboard.SetTarget(animation, indicator);
            Storyboard.SetTargetProperty(animation, "Opacity");

            var storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            storyboard.Begin();
        }

        private bool IsSidebarButtonSelected(string tag)
        {
            return _currentPrimarySection.HasValue &&
                string.Equals(tag, GetPrimaryTag(_currentPrimarySection.Value), StringComparison.Ordinal);
        }

        private string GetPrimaryTag(PrimarySection section)
        {
            switch (section)
            {
                case PrimarySection.Home: return "home";
                case PrimarySection.Timeline: return "timeline";
                case PrimarySection.Collection: return "collection";
                case PrimarySection.Discover: return "discover";
                case PrimarySection.Rakuen: return "rakuen";
                case PrimarySection.Group: return "group";
                case PrimarySection.Messages: return "messages";
                case PrimarySection.Profile: return "profile";
                case PrimarySection.Tinygrail: return "tinygrail";
                case PrimarySection.Toolbox: return "toolbox";
                case PrimarySection.Settings: return "settings";
                default: return string.Empty;
            }
        }

        private string GetNavigationLabel(string tag)
        {
            switch (tag)
            {
                case "home": return "发现";
                case "timeline": return "时间线";
                case "collection": return "收藏";
                case "discover": return "搜索";
                case "rakuen": return "超展开";
                case "group": return "小组";
                case "messages": return "消息";
                case "profile": return "我的";
                case "tinygrail": return "小圣杯";
                case "toolbox": return "工具箱";
                case "settings": return "设置";
                default: return string.Empty;
            }
        }

        private void ConfigureTitleBar()
        {
            var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            if (!coreTitleBar.ExtendViewIntoTitleBar)
            {
                coreTitleBar.ExtendViewIntoTitleBar = true;
            }

            coreTitleBar.LayoutMetricsChanged += CoreTitleBar_LayoutMetricsChanged;
            Window.Current.CoreWindow.Activated += CoreWindow_Activated;
            Window.Current.SetTitleBar(TitleBarDragRegion);

            UpdateTitleBarLayout(coreTitleBar);
            ApplyTitleBarButtonColors();
            ApplyResponsiveShell(Window.Current.Bounds.Width);
        }

        private void CoreTitleBar_LayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args)
        {
            UpdateTitleBarLayout(sender);
        }

        private void CoreWindow_Activated(CoreWindow sender, WindowActivatedEventArgs args)
        {
            ApplyTitleBarButtonColors();
        }

        private void UpdateTitleBarLayout(CoreApplicationViewTitleBar titleBar)
        {
            RightPaddingColumn.Width = new GridLength(titleBar.SystemOverlayRightInset);
            AppTitleBar.Height = Math.Max(48, titleBar.Height);
        }

        private void ApplyTitleBarButtonColors()
        {
            var titleBar = ApplicationView.GetForCurrentView().TitleBar;
            var foreground = Colors.White;

            titleBar.BackgroundColor = Colors.Transparent;
            titleBar.ForegroundColor = Colors.Transparent;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonForegroundColor = foreground;
            titleBar.ButtonHoverForegroundColor = foreground;
            titleBar.ButtonPressedForegroundColor = foreground;
            titleBar.ButtonHoverBackgroundColor = Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF);
            titleBar.ButtonPressedBackgroundColor = Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF);
            titleBar.ButtonInactiveForegroundColor = foreground;
        }

        private void UpdateBackButtonStatus()
        {
            CanGoBack = false;
        }

        private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool Set<T>(ref T storage, T value, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value))
            {
                return false;
            }

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
