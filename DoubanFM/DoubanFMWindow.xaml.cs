﻿using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DoubanFM.Core;
using System.Windows.Threading;
using System.Threading;
using System.Collections.ObjectModel;
using System.Windows.Media.Animation;
using System.Diagnostics;
using System.Windows.Forms;

namespace DoubanFM
{
    /// <summary>
    /// DoubanFMWindow.xaml 的交互逻辑
    /// </summary>
    public partial class DoubanFMWindow : Window
    {
        #region 成员变量

        /// <summary>
        /// 播放器
        /// </summary>
        private Player _player;
        /// <summary>
        /// 进度更新计时器
        /// </summary>
        private DispatcherTimer _timer;
        /// <summary>
        /// 各种无法在XAML里直接启动的Storyboard
        /// </summary>
        private Storyboard BackgroundColorStoryboard, ShowCover1Storyboard, ShowCover2Storyboard, SlideCoverRightStoryboard, SlideCoverLeftStoryboard, ChangeSongInfoStoryboard, DjChannelClickStoryboard;
        /// <summary>
        /// 滑动封面的计时器
        /// </summary>
        private DispatcherTimer _slideCoverRightTimer, _slideCoverLeftTimer;
        /// <summary>
        /// 当前显示的封面
        /// </summary>
        private Image _cover;
        /// 托盘图标
        /// </summary>
        private System.Windows.Forms.NotifyIcon notifyIcon = new System.Windows.Forms.NotifyIcon();
        /// <summary>
        /// 托盘图标右键菜单中的各个菜单项
        /// </summary>
        private System.Windows.Forms.ToolStripMenuItem notifyIcon_ShowWindow, notifyIcon_Heart, notifyIcon_Never, notifyIcon_PlayPause, notifyIcon_Next, notifyIcon_Exit;

        #endregion

        #region 构造和初始化

        public DoubanFMWindow()
        {
            InitializeComponent();
            InitNotifyIcon();

            _player = (Player)FindResource("Player");
            PbPassword.Password = _player.Settings.User.Password;
            if (_player.Settings.SlideCoverWhenMouseMove == false)
                RadioButtonSlideCoverWhenClick.IsChecked = true;
            BackgroundColorStoryboard = (Storyboard)FindResource("BackgroundColorStoryboard");
            ShowCover1Storyboard = (Storyboard)FindResource("ShowCover1Storyboard");
            ShowCover2Storyboard = (Storyboard)FindResource("ShowCover2Storyboard");
            SlideCoverRightStoryboard = (Storyboard)FindResource("SlideCoverRightStoryboard");
            SlideCoverLeftStoryboard = (Storyboard)FindResource("SlideCoverLeftStoryboard");
            ChangeSongInfoStoryboard = (Storyboard)FindResource("ChangeSongInfoStoryboard");
            DjChannelClickStoryboard = (Storyboard)FindResource("DjChannelClickStoryboard");
            _cover = Cover1;
            _timer = new DispatcherTimer();
            _timer.Interval = new TimeSpan(1000000);
            _timer.Tick += new EventHandler(timer_Tick);
            _timer.Start();
            _slideCoverRightTimer = new DispatcherTimer();
            _slideCoverRightTimer.Interval = new TimeSpan(5000000);
            _slideCoverRightTimer.Tick += new EventHandler(SlideCoverRightTimer_Tick);
            _slideCoverLeftTimer = new DispatcherTimer();
            _slideCoverLeftTimer.Interval = new TimeSpan(5000000);
            _slideCoverLeftTimer.Tick += new EventHandler(SlideCoverLeftTimer_Tick);

            _player.Initialized += new EventHandler((o, e) =>
            {
                ShowChannels();
            });
            _player.CurrentChannelChanged += new EventHandler((o, e) =>
                {
                    if (!_player.CurrentChannel.IsPersonal || _player.CurrentChannel.IsSpecial)
                        PersonalChannels.SelectedItem = null;
                    if (!_player.CurrentChannel.IsPublic)
                        PublicChannels.SelectedItem = null;
                    if (!_player.CurrentChannel.IsDj)
                        DjCates.SelectedItem = null;
                    if (!_player.CurrentChannel.IsSpecial)
                        SearchResultList.SelectedItem = null;
                    if (_player.CurrentChannel.IsPersonal && !_player.CurrentChannel.IsSpecial && _player.CurrentChannel != PersonalChannels.SelectedItem)
                    {
                        PersonalChannels.SelectedItem = _player.CurrentChannel;
                        PersonalClickStoryboard.Begin();
                    }
                    if (_player.CurrentChannel.IsPublic && _player.CurrentChannel != PublicChannels.SelectedItem)
                    {
                        PublicChannels.SelectedItem = _player.CurrentChannel;
                        PublicClickStoryboard.Begin();
                    }
                    if (_player.CurrentChannel.IsDj && DjChannels.Items.Contains(_player.CurrentChannel) && DjChannels.SelectedItem != _player.CurrentChannel)
                    {
                        DjChannels.SelectedItem = _player.CurrentChannel;
                        DjChannelClickStoryboard.Begin();
                    }
                });
            _player.CurrentSongChanged += new EventHandler((o, e) =>
            {
                Update();
                if (_player.CurrentSong.IsAd)
                    _player.Skip();
                if (_player.IsPlaying) Audio.Play();
                else Audio.Pause();
            });
            _player.Paused += new EventHandler((o, e) =>
            {
                CheckBoxPause.IsChecked = !_player.IsPlaying;
                PauseThumb.ImageSource = (ImageSource)FindResource("PlayThumbImage");
                Audio.Pause();
                notifyIcon_PlayPause.Text = "播放";
            });
            _player.Played += new EventHandler((o, e) =>
            {
                CheckBoxPause.IsChecked = !_player.IsPlaying;
                PauseThumb.ImageSource = (ImageSource)FindResource("PauseThumbImage");
                Audio.Play();
                notifyIcon_PlayPause.Text = "暂停";
            });
            _player.Stoped += new EventHandler((o, e) => { Audio.Stop(); });
            _player.UserAssistant.LogOnFailed += new EventHandler((o, e) =>
            {
                if (_player.UserAssistant.HasCaptcha)
                    Captcha.Source = new BitmapImage(new Uri(_player.UserAssistant.CaptchaUrl));
            });
            _player.UserAssistant.LogOnSucceed += new EventHandler((o, e) =>
            {
                ShowChannels();
            });
            _player.UserAssistant.LogOffSucceed += new EventHandler((o, e) =>
            {
                if (_player.UserAssistant.HasCaptcha)
                    Captcha.Source = new BitmapImage(new Uri(_player.UserAssistant.CaptchaUrl));
                ShowChannels();
            });

            _player.IsLikedChanged += new EventHandler((o, e) =>
            {
                CheckBoxLike.IsChecked = _player.IsLiked;
                if (_player.IsLikedEnabled)
                    if (_player.IsLiked)
                        LikeThumb.ImageSource = (ImageSource)FindResource("LikeThumbImage");
                    else LikeThumb.ImageSource = (ImageSource)FindResource("UnlikeThumbImage");
                else
                    LikeThumb.ImageSource = (ImageSource)FindResource("LikeThumbImage_Disabled");
                notifyIcon_Heart.Checked = _player.IsLiked;
            });
            _player.IsLikedEnabledChanged += new EventHandler((o, e) =>
            {
                if (_player.IsLikedEnabled)
                    if (_player.IsLiked)
                        LikeThumb.ImageSource = (ImageSource)FindResource("LikeThumbImage");
                    else LikeThumb.ImageSource = (ImageSource)FindResource("UnlikeThumbImage");
                else
                    LikeThumb.ImageSource = (ImageSource)FindResource("LikeThumbImage_Disabled");
                LikeThumb.IsEnabled = _player.IsLikedEnabled;
                notifyIcon_Heart.Enabled = _player.IsLikedEnabled;
            });
            _player.IsNeverEnabledChanged += new EventHandler((o, e) =>
            {
                if (_player.IsNeverEnabled)
                    NeverThumb.ImageSource = (ImageSource)FindResource("NeverThumbImage");
                else
                    NeverThumb.ImageSource = (ImageSource)FindResource("NeverThumbImage_Disabled");
                NeverThumb.IsEnabled = _player.IsNeverEnabled;
                notifyIcon_Never.Enabled = _player.IsNeverEnabled;
            });

            _player.Initialize();
        }
        /// <summary>
        /// 初始化托盘图标
        /// </summary>
        private void InitNotifyIcon()
        {
            notifyIcon.Visible = false;
            notifyIcon.Icon = Properties.Resources.NotifyIcon;
            notifyIcon.MouseClick += new System.Windows.Forms.MouseEventHandler((s, e) =>
            {
                if (e.Button == System.Windows.Forms.MouseButtons.Left)
                {
                    this.Visibility = Visibility.Visible;
                    this.Activate();
                }
            });
            System.Windows.Forms.ContextMenuStrip notifyIconMenu = new System.Windows.Forms.ContextMenuStrip();
            notifyIcon.Text = "豆瓣电台";
            notifyIcon.ContextMenuStrip = notifyIconMenu;

            notifyIconMenu.Items.Add(new ToolStripMenuItem("显示窗口"));
            notifyIcon_ShowWindow = (ToolStripMenuItem)notifyIconMenu.Items[notifyIconMenu.Items.Count - 1];
            notifyIcon_ShowWindow.Click += new EventHandler((s, e) => { this.Visibility = Visibility.Visible; });
            notifyIconMenu.Items.Add("-");
            notifyIconMenu.Items.Add(new ToolStripMenuItem("喜欢"));
            notifyIcon_Heart = (ToolStripMenuItem)notifyIconMenu.Items[notifyIconMenu.Items.Count - 1];
            notifyIcon_Heart.CheckOnClick = true;
            notifyIcon_Heart.CheckedChanged += new EventHandler((o, e) =>
            {
                _player.IsLiked = notifyIcon_Heart.Checked;
            });
            notifyIconMenu.Items.Add(new ToolStripMenuItem("不再播放"));
            notifyIcon_Never = (ToolStripMenuItem)notifyIconMenu.Items[notifyIconMenu.Items.Count - 1];
            notifyIcon_Never.Enabled = false;
            notifyIcon_Never.Click += new EventHandler((s, e) => { _player.Never(); });
            notifyIconMenu.Items.Add("-");
            notifyIconMenu.Items.Add(new ToolStripMenuItem("暂停"));
            notifyIcon_PlayPause = (ToolStripMenuItem)notifyIconMenu.Items[notifyIconMenu.Items.Count - 1];
            notifyIcon_PlayPause.Click += new EventHandler((s, e) =>
            {
                System.Windows.Forms.ToolStripItem sender = (System.Windows.Forms.ToolStripItem)s;
                if (sender.Text == "播放") _player.IsPlaying = true;
                else _player.IsPlaying = false;
            });
            notifyIconMenu.Items.Add(new ToolStripMenuItem("下一首"));
            notifyIcon_Next = (ToolStripMenuItem)notifyIconMenu.Items[notifyIconMenu.Items.Count - 1];
            notifyIcon_Next.Click += new EventHandler((s, e) => { _player.Skip(); });
            notifyIconMenu.Items.Add("-");
            notifyIconMenu.Items.Add(new ToolStripMenuItem("退出"));
            notifyIcon_Exit = (ToolStripMenuItem)notifyIconMenu.Items[notifyIconMenu.Items.Count - 1];
            notifyIcon_Exit.Click += new EventHandler((s, e) => { this.Close(); });
        }

        #endregion

        #region 其他

        /// <summary>
        /// 显示频道列表
        /// </summary>
        private void ShowChannels()
        {
            ObservableCollection<Channel> PersonalChannelsItem = new ObservableCollection<Channel>();
            if (_player.UserAssistant.IsLoggedOn)
                foreach (Cate cate in _player.ChannelInfo.Personal)
                    foreach (Channel channel in cate.Channels)
                        PersonalChannelsItem.Add(channel);
            ObservableCollection<Channel> PublicChannelsItem = new ObservableCollection<Channel>();
            foreach (Cate cate in _player.ChannelInfo.Public)
                foreach (Channel channel in cate.Channels)
                    PublicChannelsItem.Add(channel);
            ObservableCollection<Cate> DjCatesItem = new ObservableCollection<Cate>();
            foreach (Cate djcate in _player.ChannelInfo.Dj)
                DjCatesItem.Add(djcate);
            PersonalChannels.ItemsSource = PersonalChannelsItem;
            PublicChannels.ItemsSource = PublicChannelsItem;
            DjCates.ItemsSource = DjCatesItem;
        }
        /// <summary>
        /// 更新界面内容，主要是音乐信息。换音乐时自动调用。
        /// </summary>
        private void Update()
        {
            Song song = _player.CurrentSong;
            ChangeCover(song);
            try
            {
                Audio.Source = new Uri(song.FileUrl);

                Audio.IsMuted = !Audio.IsMuted;     //防止无敌静音
                Thread.Sleep(50);
                Audio.IsMuted = !Audio.IsMuted;
                Audio.Volume = _player.Settings.Volume;
            }
            catch { }
            ((StringAnimationUsingKeyFrames)ChangeSongInfoStoryboard.Children[1]).KeyFrames[0].Value = song.Title;
            ((StringAnimationUsingKeyFrames)ChangeSongInfoStoryboard.Children[2]).KeyFrames[0].Value = song.Artist;
            ((StringAnimationUsingKeyFrames)ChangeSongInfoStoryboard.Children[3]).KeyFrames[0].Value = song.Album;
            ChangeSongInfoStoryboard.Begin();
            string stringA = song.Title + " - " + song.Artist;
            string stringB = "    豆瓣电台 - " + _player.CurrentChannel.Name;
            this.Title = stringA + stringB;
            if (this.Title.Length <= 63)        //Windows限制托盘图标的提示信息最长为63个字符
                notifyIcon.Text = this.Title;
            else
            {
                notifyIcon.Text = stringA.Substring(0, Math.Max(63 - stringB.Length, 0));
                if (notifyIcon.Text.Length + stringB.Length <= 63)
                    notifyIcon.Text += stringB.Length;
                else
                    notifyIcon.Text = stringA.Substring(0, Math.Min(stringA.Length, 63));
            }
            ChannelTextBlock.Text = _player.CurrentChannel.Name;
            TotalTime.Content = TimeSpanToStringConverter.QuickConvert(song.Length);
            CurrentTime.Content = TimeSpanToStringConverter.QuickConvert(new TimeSpan(0));
            Slider.Minimum = 0;
            Slider.Maximum = song.Length.TotalSeconds;
            Slider.Value = 0;
        }

        /// <summary>
        /// 更改封面
        /// </summary>
        /// <param name="song">音乐</param>
        void ChangeCover(Song song)
        {
            try
            {
                BitmapImage bitmap = new BitmapImage(new Uri(song.Picture));
                {
                    bitmap.DownloadCompleted += new EventHandler(bitmap_DownloadCompleted);
                    bitmap.DownloadFailed += new EventHandler<ExceptionEventArgs>(bitmap_DownloadFailed);
                }
            }
            catch
            {
                BitmapImage bitmap = new BitmapImage(new Uri("pack://application:,,,/DoubanFM;component/Images/DoubanFM_NoCover.png"));
                ChangeBackground(bitmap);
                SwitchCover(bitmap);
            }
        }
        /// <summary>
        /// 根据封面颜色更换背景。封面加载成功时自动调用
        /// </summary>
        /// <param name="NewCover">新封面</param>
        void ChangeBackground(BitmapImage NewCover)
        {
            ColorAnimation animation = (ColorAnimation)BackgroundColorStoryboard.Children[0];
            Thread thread = new Thread(new ThreadStart(() =>
                {
                    Color to = ColorFunctions.GetImageColorForBackground(NewCover);
                    this.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            animation.To = to;
                            BackgroundColorStoryboard.Begin();
                        }));
                }));
            thread.IsBackground = true;
            thread.Start();
        }
        /// <summary>
        /// 更换封面。封面加载成功时自动调用
        /// </summary>
        /// <param name="NewCover">新封面</param>
        void SwitchCover(BitmapImage NewCover)
        {
            if (_cover == Cover1)
            {
                Cover2.Source = NewCover;
                _cover = Cover2;
                ShowCover2Storyboard.Begin();
            }
            else
            {
                Cover1.Source = NewCover;
                _cover = Cover1;
                ShowCover1Storyboard.Begin();
            }
        }
        #endregion

        #region 事件响应
        /// <summary>
        /// 封面加载失败时自动调用
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void bitmap_DownloadFailed(object sender, ExceptionEventArgs e)
        {
            if (((BitmapImage)sender).UriSource.AbsoluteUri.ToString() == new Uri(_player.CurrentSong.Picture).AbsoluteUri.ToString())
            {
                BitmapImage bitmap = new BitmapImage(new Uri("pack://application:,,,/DoubanFM;component/Images/DoubanFM_NoCover.png"));
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        ChangeBackground(bitmap);
                        SwitchCover(bitmap);
                    }
                    catch { }
                }));

            }
        }
        /// <summary>
        /// 封面加载成功时自动调用
        /// </summary>
        /// <param name="sender">封面BitmapImage</param>
        /// <param name="e"></param>
        void bitmap_DownloadCompleted(object sender, EventArgs e)
        {
            if (((BitmapImage)sender).UriSource.AbsoluteUri == new Uri(_player.CurrentSong.Picture).AbsoluteUri)
            {
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        ChangeBackground((BitmapImage)sender);
                        SwitchCover((BitmapImage)sender);
                    }
                    catch { }
                }));
            }
        }
        /// <summary>
        /// 主界面中按下“下一首”按钮时自动调用
        /// </summary>
        /// <param name="sender">按钮</param>
        /// <param name="e"></param>
        private void ButtonNext_Click(object sender, RoutedEventArgs e)
        {
            _player.Skip();
        }
        /// <summary>
        /// 音乐播放结束时自动调用
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Audio_MediaEnded(object sender, RoutedEventArgs e)
        {
            _player.CurrentSongFinishedPlaying();
        }
        /// <summary>
        /// 音乐遇到错误时自动调用
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Audio_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            _player.CurrentSongFinishedPlaying();
        }
        /// <summary>
        /// 计时器响应函数，用于更新时间信息。自动调用
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void timer_Tick(object sender, EventArgs e)
        {
            CurrentTime.Content = TimeSpanToStringConverter.QuickConvert(Audio.Position);
            Slider.Value = Audio.Position.TotalSeconds;
        }
        /// <summary>
        /// 修正音乐总时间。音乐加载完成时自动调用。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Audio_MediaOpened(object sender, RoutedEventArgs e)
        {
            if (Audio.NaturalDuration.HasTimeSpan)
            {
                if (Math.Abs((TimeSpanToStringConverter.QuickConvertBack((string)TotalTime.Content) - Audio.NaturalDuration.TimeSpan).TotalSeconds) > 2)
                    TotalTime.Content = TimeSpanToStringConverter.QuickConvert(Audio.NaturalDuration.TimeSpan);
                Slider.Maximum = Audio.NaturalDuration.TimeSpan.TotalSeconds;
            }
        }
        /// <summary>
        /// 任务栏“暂停/播放”按钮按下时自动调用
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PauseThumb_Click(object sender, EventArgs e)
        {
            _player.IsPlaying = !_player.IsPlaying;
        }
        /// <summary>
        /// 任务栏“下一首”按钮按下时自动调用
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NextThumb_Click(object sender, EventArgs e)
        {
            _player.Skip();
        }
        /// <summary>
        /// 保存各种信息
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closed(object sender, EventArgs e)
        {
            Audio.Close();
            notifyIcon.Dispose();
            _player.Dispose();
        }
        /// <summary>
        /// 更新密码
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PbPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            _player.Settings.User.Password = PbPassword.Password;
        }
        /// <summary>
        /// 登录
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ButtonLogOn_Click(object sender, RoutedEventArgs e)
        {
            _player.UserAssistant.LogOn(CaptchaText.Text);
        }
        /// <summary>
        /// 注销
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ButtonLogOff_Click(object sender, RoutedEventArgs e)
        {
            _player.UserAssistant.LogOff();
        }
        /// <summary>
        /// 验证码被点击
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ButtonRefreshCaptcha_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            _player.UserAssistant.Refresh();
        }

        /// <summary>
        /// 任务栏点击“喜欢”
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LikeThumb_Click(object sender, EventArgs e)
        {
            _player.IsLiked = !_player.IsLiked;
        }
        /// <summary>
        /// 主界面点击“不再播放”
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ButtonNever_Click(object sender, RoutedEventArgs e)
        {
            _player.Never();
        }
        /// <summary>
        /// 任务栏点击“不再播放”
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NeverThumb_Click(object sender, EventArgs e)
        {
            _player.Never();
        }

        /// <summary>
        /// 更换私人频道。私人频道选择改变时自动调用。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PersonalChannels_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PersonalChannels.SelectedItem != null)
                _player.CurrentChannel = (Channel)PersonalChannels.SelectedItem;
        }
        /// <summary>
        /// 更换公共频道。公共频道选择改变时自动调用。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PublicChannels_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PublicChannels.SelectedItem != null)
                _player.CurrentChannel = (Channel)PublicChannels.SelectedItem;
        }
        /// <summary>
        /// 更换DJ节目。DJ节目选择改变时自动调用。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DjChannels_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DjChannels.SelectedItem != null)
            {
                _player.CurrentDjCate = (Cate)DjCates.SelectedItem;
                _player.CurrentChannel = (Channel)DjChannels.SelectedItem;
            }
        }
        /// <summary>
        /// 当用户在某DJ频道上点击鼠标左键时，切换到DJ节目列表
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DjCates_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DjCates.SelectedItem != null)
            {
                DjChannels.ItemsSource = ((Cate)DjCates.SelectedItem).Channels;
                DjChannelClickStoryboard.Begin();
            }
        }
        /// <summary>
        /// 当用户在按钮“DJ兆赫”上点击时，去除DjCates的选择
        /// </summary>
        private void ButtonDjCates_Click(object sender, RoutedEventArgs e)
        {
            DjCates.SelectedItem = null;
        }
        /// <summary>
        /// 鼠标左键点击封面时滑动封面
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CoverGrid_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            //if (!_player.Settings.SlideCoverWhenMouseMove)
            {
                Point leftLocation = e.GetPosition(LeftPanel);
                Debug.WriteLine("LeftPanel:" + leftLocation);
                HitTestResult leftResult = VisualTreeHelper.HitTest(LeftPanel, leftLocation);
                if (leftResult != null)
                {
                    Debug.WriteLine("SlideRight");
                    SlideCoverRightStoryboard.Begin();
                    return;
                }
                Point rightLocation = e.GetPosition(RightPanel);
                Debug.WriteLine("RightPanel:" + rightLocation);
                HitTestResult rightResult = VisualTreeHelper.HitTest(RightPanel, rightLocation);
                if (rightResult != null)
                {
                    Debug.WriteLine("SlideLeft");
                    SlideCoverLeftStoryboard.Begin();
                }
            }
        }

        void SlideCoverRightTimer_Tick(object sender, EventArgs e)
        {
            SlideCoverRightStoryboard.Begin();
            _slideCoverRightTimer.Stop();
        }
        void SlideCoverLeftTimer_Tick(object sender, EventArgs e)
        {
            SlideCoverLeftStoryboard.Begin();
            _slideCoverLeftTimer.Stop();
        }

        /// <summary>
        /// 当鼠标移动时滑动封面
        /// </summary>
        private void CoverGrid_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_player.Settings.SlideCoverWhenMouseMove)
            {
                Point leftLocation = e.GetPosition(LeftPanel);
                Debug.WriteLine("LeftPanel:" + leftLocation);
                HitTestResult leftResult = VisualTreeHelper.HitTest(LeftPanel, leftLocation);
                if (leftResult != null)
                {
                    Debug.WriteLine("SlideRight");
                    _slideCoverRightTimer.Start();
                    _slideCoverLeftTimer.Stop();
                    return;
                }
                Point rightLocation = e.GetPosition(RightPanel);
                Debug.WriteLine("RightPanel:" + rightLocation);
                HitTestResult rightResult = VisualTreeHelper.HitTest(RightPanel, rightLocation);
                if (rightResult != null)
                {
                    Debug.WriteLine("SlideLeft");
                    _slideCoverLeftTimer.Start();
                    _slideCoverRightTimer.Stop();
                }
            }
        }

        private void CoverGrid_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _slideCoverRightTimer.Stop();
            _slideCoverLeftTimer.Stop();
        }

        private void ButtonMinimize_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            this.WindowState = System.Windows.WindowState.Minimized;
        }

        private void ButtonToNotifyIcon_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            this.Visibility = Visibility.Hidden;
        }

        private void ButtonExit_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_IsVisibleChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            notifyIcon.Visible = !this.IsVisible;
        }

        private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void VisitSoftwareWebsite_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Process.Start("http://kfstorm.wordpress.com/doubanfm/");
        }

        private void VisitOfficialWebsite_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Process.Start("http://douban.fm/");
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            _player.MusicSearch.StartSearch(SearchText.Text);
        }

        private void PreviousPage_Click(object sender, RoutedEventArgs e)
        {
            _player.MusicSearch.PreviousPage();
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            _player.MusicSearch.NextPage();
        }

        private void SearchResultList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (SearchResultList.SelectedItem == null) return;
            Channel channel = ((SearchItem)SearchResultList.SelectedItem).GetChannel();
            if (channel != null) _player.CurrentChannel = channel;
        }

        private void SearchText_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                Search_Click(null, null);
        }

        private void Window_Activated(object sender, System.EventArgs e)
        {
            GradientStopCollection active = (GradientStopCollection)FindResource("ActiveShadowGradientStops");
            GradientStopCollection now = (GradientStopCollection)FindResource("ShadowGradientStops");
            now.Clear();
            foreach (GradientStop g in active)
                now.Add(g);
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            GradientStopCollection inactive = (GradientStopCollection)FindResource("InactiveShadowGradientStops");
            GradientStopCollection now = (GradientStopCollection)FindResource("ShadowGradientStops");
            now.Clear();
            foreach (GradientStop g in inactive)
                now.Add(g);
        }

        private void CheckBoxPause_Checked(object sender, RoutedEventArgs e)
        {
            _player.IsPlaying = false;
        }

        private void CheckBoxPause_Unchecked(object sender, RoutedEventArgs e)
        {
            _player.IsPlaying = true;
        }

        private void CheckBoxLike_Checked(object sender, RoutedEventArgs e)
        {
            _player.IsLiked = true;
        }

        private void CheckBoxLike_Unchecked(object sender, RoutedEventArgs e)
        {
            _player.IsLiked = false;
        }

        #endregion

    }
}