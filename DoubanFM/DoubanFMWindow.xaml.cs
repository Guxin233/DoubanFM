﻿/*
 * Author : K.F.Storm
 * Email : yk000123 at sina.com
 * Website : http://www.kfstorm.com
 * */

using System;
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
using System.Windows.Shell;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO.MemoryMappedFiles;
using System.IO;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Input;
using System.Text;
using DoubanFM.NotifyIcon;
using System.Windows.Data;
using DoubanFM.Bass;
using System.Collections.Generic;

namespace DoubanFM
{
	/// <summary>
	/// 豆瓣电台的主窗口
	/// </summary>
	public partial class DoubanFMWindow : WindowBase
	{
		#region 成员变量

		/// <summary>
		/// 播放器
		/// </summary>
		private Player _player;
		/// <summary>
		/// 进度更新计时器
		/// </summary>
		private DispatcherTimer _progressRefreshTimer;
		///// <summary>
		///// 防止不执行下一首
		///// </summary>
		//private DispatcherTimer _forceNextTimer;
		/// <summary>
		/// 各种无法在XAML里直接启动的Storyboard
		/// </summary>
		private Storyboard BackgroundColorStoryboard, ShowCover1Storyboard, ShowCover2Storyboard, SlideCoverRightStoryboard, SlideCoverLeftStoryboard, ChangeSongInfoStoryboard, VolumeFadeOut, VolumeFadeIn, VolumeDirectIn, EnhancementsPanelShow, EnhancementsPanelHide;
		/// <summary>
		/// 滑动封面的计时器
		/// </summary>
		private DispatcherTimer _slideCoverRightTimer, _slideCoverLeftTimer, _leftPanelMouseLeaveTimer;
		/// <summary>
		/// 当前显示的封面
		/// </summary>
		private Image _cover;
		/// <summary>
		/// 用于进程间更换频道的内存映射文件
		/// </summary>
		private MemoryMappedFile _mappedFile;
		/// <summary>
		/// 命令
		/// </summary>
		public enum Commands { None, Like, Unlike, LikeUnlike, Never, PlayPause, Next, ShowMinimize, ShowHide, ShowLyrics, HideLyrics, ShowHideLyrics, OneKeyShare, SearchDownload, VolumeUp, VolumeDown, MuteSwitch }
		/// <summary>
		/// 热键
		/// </summary>
		public HotKeys HotKeys;
		/// <summary>
		/// 临时文件夹
		/// </summary>
		private string _tempPath = Path.Combine(Path.GetTempPath(), "DoubanFM");
		/// <summary>
		/// 桌面歌词窗口
		/// </summary>
		internal LyricsWindow _lyricsWindow;
		/// <summary>
		/// 歌词设置
		/// </summary>
		internal LyricsSetting _lyricsSetting;
		/// <summary>
		/// 分享设置
		/// </summary>
		public ShareSetting ShareSetting
		{
			get { return (ShareSetting)GetValue(ShareSettingProperty); }
			set { SetValue(ShareSettingProperty, value); }
		}

		public static readonly DependencyProperty ShareSettingProperty =
			DependencyProperty.Register("ShareSetting", typeof(ShareSetting), typeof(DoubanFMWindow), new UIPropertyMetadata(null));

		/// <summary>
		/// 原始窗口背景
		/// </summary>
		public Color OriginalBackgroundColor
		{
			get { return (Color)GetValue(OriginalBackgroundColorProperty); }
			set { SetValue(OriginalBackgroundColorProperty, value); }
		}

		public static readonly DependencyProperty OriginalBackgroundColorProperty =
			DependencyProperty.Register("OriginalBackgroundColor", typeof(Color), typeof(DoubanFMWindow), new UIPropertyMetadata(ColorConverter.ConvertFromString("#FF1960AF")));

		/// <summary>
		/// 自动更换背景
		/// </summary>
		public bool AutoBackground
		{
			get { return (bool)GetValue(AutoBackgroundProperty); }
			set { SetValue(AutoBackgroundProperty, value); }
		}

		public static readonly DependencyProperty AutoBackgroundProperty =
			DependencyProperty.Register("AutoBackground", typeof(bool), typeof(DoubanFMWindow), new UIPropertyMetadata(true, new PropertyChangedCallback((d, e) =>
				{
					DoubanFMWindow window = (DoubanFMWindow)d;
					window.OnAutoBackgroundChanged((bool)e.OldValue, (bool)e.NewValue);
				})));

		/// <summary>
		/// 音量设置
		/// </summary>
		public double Volume
		{
			get { return (double)GetValue(VolumeProperty); }
			set { SetValue(VolumeProperty, value); }
		}

		public static readonly DependencyProperty VolumeProperty =
			DependencyProperty.Register("Volume", typeof(double), typeof(DoubanFMWindow), new UIPropertyMetadata(1.0, new PropertyChangedCallback((d, e) =>
			{
				DoubanFMWindow window = (DoubanFMWindow)d;
				BassEngine.Instance.Volume = window.Volume * window.VolumeFadeParameter;
			}))
			, new ValidateValueCallback((value) =>
			{
				double v = (double)value;
				return v >= 0 && v <= 1;
			}));



		/// <summary>
		/// 音量淡入淡出参数
		/// </summary>
		public double VolumeFadeParameter
		{
			get { return (double)GetValue(VolumeFadeParameterProperty); }
			set { SetValue(VolumeFadeParameterProperty, value); }
		}

		public static readonly DependencyProperty VolumeFadeParameterProperty =
			DependencyProperty.Register("VolumeFadeParameter", typeof(double), typeof(DoubanFMWindow), new UIPropertyMetadata(1.0, new PropertyChangedCallback((d, e) =>
			{
				DoubanFMWindow window = (DoubanFMWindow)d;
				BassEngine.Instance.Volume = window.Volume * window.VolumeFadeParameter;
			}))
			, new ValidateValueCallback((value) =>
			{
				double v = (double)value;
				return v >= 0 && v <= 1;
			}));


		///// <summary>
		///// 记录最后一次切歌的时间
		///// </summary>
		//private DateTime _lastTimeChangeSong = DateTime.MaxValue;

		public bool SaveSettings = true;

		#endregion

		#region 构造和初始化

		public DoubanFMWindow()
		{
			Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 进入主窗口构造方法");

			Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " InitializeComponent");
			InitializeComponent();
			Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " InitializeComponent完成");

			Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 初始化成员变量");
			InitMemberVariables();
			Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 初始化成员变量完成");

			Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 初始化播放器设置");
			InitPlayerSettings();
			Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 初始化播放器设置完成");

			Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 清除老版本产生的临时文件");
			ClearOldTempFiles();
			Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 清除老版本产生的临时文件完成");

			Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 清除下载的安装文件");
			ClearSetupFiles();
			Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 清除下载的安装文件完成");

			Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 向播放器添加事件处理程序");
			AddPlayerEventListener();
			Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 向播放器添加事件处理程序完成");

			Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 初始化托盘图标");
			InitNotifyIcon();
			Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 初始化托盘图标完成");

			Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 初始化Timer");
			InitTimers();
			Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 初始化Timer完成");

			Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 初始化内存映射文件");
			CheckMappedFile();
			Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 初始化内存映射文件完成");

			Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 检查自动更新");
			CheckUpdateOnStartup();
			Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 检查自动更新完成");

			Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 初始化歌词");
			InitLyrics();
			Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 初始化歌词完成");

			Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 初始化分享设置");
			InitShareSetting();
			Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 初始化分享设置完成");

			Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 初始化窗口背景");
			InitBackground();
			Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 初始化窗口背景完成");

			Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 初始化键盘钩子");
			InitKeyboardHook();
			Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 初始化键盘钩子完成");
		}

		/// <summary>
		/// 初始化播放器设置
		/// </summary>
		void InitPlayerSettings()
		{
			PbPassword.Password = _player.Settings.User.Password;
			Channel channel = Channel.FromCommandLineArgs(System.Environment.GetCommandLineArgs().ToList());
			if (channel != null) _player.Settings.LastChannel = channel;
			if (_player.Settings.ScaleTransform != 1.0)
				TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);
			if (!_player.Settings.FirstTime)
			{
				FirstTimePanel.Visibility = Visibility.Collapsed;
			}
			if (!double.IsNaN(_player.Settings.LocationLeft))
			{
				this.WindowStartupLocation = System.Windows.WindowStartupLocation.Manual;
				this.Left = _player.Settings.LocationLeft;
				this.Top = _player.Settings.LocationTop;

				//防止调整分辨率后窗口超出屏幕
				if (this.Left + 50 > SystemParameters.WorkArea.Right)
					this.Left = SystemParameters.WorkArea.Right - 50;
				if (this.Top + 50 > SystemParameters.WorkArea.Bottom)
					this.Top = SystemParameters.WorkArea.Bottom - 50;
			}
		}
		/// <summary>
		/// 初始化BassEngine
		/// </summary>
		void InitBassEngine()
		{
			//歌曲播放完毕
			BassEngine.Instance.TrackEnded += delegate
			{
				Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 歌曲播放完毕");
				if (!stoped)
				{
					_player.CurrentSongFinishedPlaying();
				}
			};
			//音乐加载成功
			BassEngine.Instance.OpenSucceeded += delegate
			{
				Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 音乐加载成功");
				if (Math.Abs((TimeSpanToStringConverter.QuickConvertBack((string)TotalTime.Text) - BassEngine.Instance.ChannelLength).TotalSeconds) > 2)
					TotalTime.Text = TimeSpanToStringConverter.QuickConvert(BassEngine.Instance.ChannelLength);
				Slider.Maximum = BassEngine.Instance.ChannelLength.TotalSeconds;
			};
			//打开音乐失败
			BassEngine.Instance.OpenFailed += delegate
			{
				Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 打开音乐失败");
				_player.MediaFailed();
			};

			//绑定音量设置

			BassEngine.Instance.Volume = _player.Settings.Volume;
			BassEngine.Instance.IsMuted = _player.Settings.IsMuted;

			Binding binding = new Binding();
			binding.Source = _player;
			binding.Path = new PropertyPath("Settings.Volume");
			binding.Mode = BindingMode.TwoWay;
			this.SetBinding(VolumeProperty, binding);

			System.Windows.Data.Binding binding2 = new System.Windows.Data.Binding("IsMuted");
			binding2.Source = BassEngine.Instance;
			binding2.Mode = System.Windows.Data.BindingMode.TwoWay;
			System.Windows.Data.BindingOperations.SetBinding(_player.Settings, Settings.IsMutedProperty, binding2);
		}

		/// <summary>
		/// 键盘钩子
		/// </summary>
		private KeyboardHook hook;
		/// <summary>
		/// 初始化键盘钩子
		/// </summary>
		void InitKeyboardHook()
		{
			hook = new KeyboardHook();
			hook.KeyUp += new KeyboardHook.HookEventHandler((sender, e) =>
			{
				if (this.IsLoaded)
				{
					//按下了媒体暂停播放键
					if (e.Key == Key.MediaPlayPause)
					{
						PlayPause();
					}
					//按下了媒体下一曲目键
					else if (e.Key == Key.MediaNextTrack)
					{
						Next();
					}
				}
			});
		}
		/// <summary>
		/// 初始化窗口背景
		/// </summary>
		void InitBackground()
		{
			Binding binding = new Binding();
			binding.Source = _player;
			binding.Path = new PropertyPath("Settings.AutoBackground");
			this.SetBinding(AutoBackgroundProperty, binding);

			if (_player.Settings.AutoBackground)
			{
				OnAutoBackgroundChanged(!_player.Settings.AutoBackground, _player.Settings.AutoBackground);
			}
		}

		protected void OnAutoBackgroundChanged(bool oldValue, bool newValue)
		{
			if (!newValue)
			{
				Binding binding = new Binding();
				binding.Source = _player.Settings;
				binding.Path = new PropertyPath(Settings.BackgroundProperty);
				BindingOperations.SetBinding(SolidBackground, SolidColorBrush.ColorProperty, binding);
			}
			else
			{
				Binding binding = new Binding();
				binding.Source = this;
				binding.Path = new PropertyPath(DoubanFMWindow.OriginalBackgroundColorProperty);
				BindingOperations.SetBinding(SolidBackground, SolidColorBrush.ColorProperty, binding);
			}
		}

		/// <summary>
		/// 初始化托盘图标
		/// </summary>
		private void InitNotifyIcon()
		{
			NotifyIcon.Visibility = _player.Settings.AlwaysShowNotifyIcon ? Visibility.Visible : Visibility.Hidden;
		}

		/// <summary>
		/// 给播放器的各种事件添加处理代码
		/// </summary>
		void AddPlayerEventListener()
		{
			//启动播放器完成
			_player.Initialized += new EventHandler((o, e) =>
			{
				Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 启动播放器完成");

				Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 初始化BassEngine");
				InitBassEngine();
				SpectrumAnalyzer.RegisterSoundPlayer(BassEngine.Instance);
				Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 初始化BassEngine完成");

				Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 显示频道列表");
				ShowChannels();
				if (PersonalChannels.Items.Count == 0)
				{
					ButtonPublic.IsChecked = true;
				}
				Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 显示频道列表完成");
			});
			//频道已改变
			_player.CurrentChannelChanged += new EventHandler((o, e) =>
			{
				Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 频道已改变，当前频道为" + _player.CurrentChannel);
				CloseCurrentBalloon();
				ChangeChosenChannelList();
				//更新JumpList
				//if (!_player.CurrentChannel.IsDj)
				AddChannelToJumpList(_player.CurrentChannel);
			});
			//歌曲已改变
			_player.CurrentSongChanged += new EventHandler((o, e) =>
			{
				CloseCurrentBalloon();
				if (_player.CurrentSong != null)
				{
					Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 歌曲已改变，当前歌曲为" + _player.CurrentSong);
					BassEngine.Instance.Stop();
					stoped = false;
					VolumeDirectIn.Begin();
					Update();
					Play();			//在暂停时按下一首，加载歌曲后会结束暂停，开始播放
					BassEngine.Instance.Play();
				}
			});
			//音乐已暂停
			_player.Paused += new EventHandler((o, e) =>
			{
				Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 音乐已暂停");
				CheckBoxPause.IsChecked = !_player.IsPlaying;
				PauseThumb.ImageSource = (ImageSource)FindResource("PlayThumbImage");
				PauseThumb.Description = "播放";
				VolumeFadeOut.Begin();
				//Audio.Pause();
				//NotifyIcon_PlayPause.Text = "播放";
				//NotifyIcon_PlayPause.Image = NotifyIconImage_Play;
				//HideLyrics();
			});
			//音乐已播放
			_player.Played += new EventHandler((o, e) =>
			{
				Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 音乐已播放");
				CheckBoxPause.IsChecked = !_player.IsPlaying;
				PauseThumb.ImageSource = (ImageSource)FindResource("PauseThumbImage");
				PauseThumb.Description = "暂停";
				VolumeFadeIn.Begin();
				BassEngine.Instance.Play();
				//NotifyIcon_PlayPause.Text = "暂停";
				//NotifyIcon_PlayPause.Image = NotifyIconImage_Pause;
				//if (_player.Settings.ShowLyrics) ShowLyrics();
			});
			//音乐已停止
			_player.Stoped += new EventHandler((o, e) =>
			{
				Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 音乐已停止");
				CloseCurrentBalloon();

				stoped = true;
				VolumeFadeOut.Begin();
				SetLyrics(null);
			});
			//登录失败
			_player.UserAssistant.LogOnFailed += new EventHandler((o, e) =>
			{
				Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 登录失败");
				/*if (_player.UserAssistant.HasCaptcha)
					Captcha.Source = new BitmapImage(new Uri(_player.UserAssistant.CaptchaUrl));*/
			});
			//登录已成功
			_player.UserAssistant.LogOnSucceed += new EventHandler((o, e) =>
			{
				Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 登录已成功");
				RefreshMyChannels();
			});
			//注销已成功
			_player.UserAssistant.LogOffSucceed += new EventHandler((o, e) =>
			{
				Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 注销已成功");
				/*if (_player.UserAssistant.HasCaptcha)
					Captcha.Source = new BitmapImage(new Uri(_player.UserAssistant.CaptchaUrl));*/
				RefreshMyChannels();
			});
			//红心状态改变
			_player.IsLikedChanged += new EventHandler((o, e) =>
			{
				Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + (_player.IsLiked ? " 已加红心" : " 已去红心"));
				CheckBoxLike.IsChecked = _player.IsLiked;
				if (_player.IsLikedEnabled)
					if (_player.IsLiked)
					{
						LikeThumb.ImageSource = (ImageSource)FindResource("LikeThumbImage");
						//NotifyIcon_Heart.Image = NotifyIconImage_Like_Like;
					}
					else
					{
						LikeThumb.ImageSource = (ImageSource)FindResource("UnlikeThumbImage");
						//NotifyIcon_Heart.Image = NotifyIconImage_Like_Unlike;
					}
				else
					LikeThumb.ImageSource = (ImageSource)FindResource("LikeThumbImage_Disabled");
			});
			//红心功能启用状态改变
			_player.IsLikedEnabledChanged += new EventHandler((o, e) =>
			{
				Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + (_player.IsLikedEnabled ? " 加红心功能已启用" : " 加红心功能已禁用"));
				if (_player.IsLikedEnabled)
					if (_player.IsLiked)
						LikeThumb.ImageSource = (ImageSource)FindResource("LikeThumbImage");
					else LikeThumb.ImageSource = (ImageSource)FindResource("UnlikeThumbImage");
				else
					LikeThumb.ImageSource = (ImageSource)FindResource("LikeThumbImage_Disabled");
				LikeThumb.IsEnabled = _player.IsLikedEnabled;
				//NotifyIcon_Heart.Enabled = _player.IsLikedEnabled;
			});
			//垃圾桶功能启用状态改变
			_player.IsNeverEnabledChanged += new EventHandler((o, e) =>
			{
				Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + (_player.IsNeverEnabled ? " 垃圾桶功能已启用" : " 垃圾桶功能已禁用"));
				if (_player.IsNeverEnabled)
					NeverThumb.ImageSource = (ImageSource)FindResource("NeverThumbImage");
				else
					NeverThumb.ImageSource = (ImageSource)FindResource("NeverThumbImage_Disabled");
				NeverThumb.IsEnabled = _player.IsNeverEnabled;
				//NotifyIcon_Never.Enabled = _player.IsNeverEnabled;
			});
			//获取播放列表失败
			_player.GetPlayListFailed += new EventHandler<PlayList.PlayListEventArgs>((o, e) =>
			{
				string message = "获取播放列表失败：" + e.Message;
				Debug.WriteLine(message);
				MessageBox.Show(this, message, "程序即将关闭", MessageBoxButton.OK, MessageBoxImage.Error);
				this.Close();
			});
			//报告播放完毕的信息失败
			_player.FinishedPlayingReportFailed += new EventHandler<ErrorEventArgs>((o, e) =>
			{
				string message = e.GetException().Message;
				Debug.WriteLine(message);
				MessageBox.Show(this, message, "程序即将关闭", MessageBoxButton.OK, MessageBoxImage.Error);
				this.Close();
			});
		}

		/// <summary>
		/// 初始化成员变量
		/// </summary>
		void InitMemberVariables()
		{
			_player = (Player)FindResource("Player");
			_cover = Cover1;
			BackgroundColorStoryboard = (Storyboard)FindResource("BackgroundColorStoryboard");
			ShowCover1Storyboard = (Storyboard)FindResource("ShowCover1Storyboard");
			ShowCover2Storyboard = (Storyboard)FindResource("ShowCover2Storyboard");
			SlideCoverRightStoryboard = (Storyboard)FindResource("SlideCoverRightStoryboard");
			SlideCoverLeftStoryboard = (Storyboard)FindResource("SlideCoverLeftStoryboard");
			ChangeSongInfoStoryboard = (Storyboard)FindResource("ChangeSongInfoStoryboard");
			VolumeFadeOut = (Storyboard)FindResource("VolumeFadeOut");
			VolumeFadeIn = (Storyboard)FindResource("VolumeFadeIn");
			VolumeDirectIn = (Storyboard)FindResource("VolumeDirectIn");
			EnhancementsPanelShow = (Storyboard)FindResource("EnhancementsPanelShow");
			EnhancementsPanelHide = (Storyboard)FindResource("EnhancementsPanelHide");
		}

		/// <summary>
		/// 初始化计时器
		/// </summary>
		void InitTimers()
		{
			//_forceNextTimer = new DispatcherTimer();
			//_forceNextTimer.Interval = new TimeSpan(600000000);
			//_forceNextTimer.Tick += new EventHandler(_forceNextTimer_Tick);
			//_forceNextTimer.Start();
			_slideCoverRightTimer = new DispatcherTimer();
			_slideCoverRightTimer.Interval = new TimeSpan(5000000);
			_slideCoverRightTimer.Tick += new EventHandler(SlideCoverRightTimer_Tick);
			_slideCoverLeftTimer = new DispatcherTimer();
			_slideCoverLeftTimer.Interval = new TimeSpan(5000000);
			_slideCoverLeftTimer.Tick += new EventHandler(SlideCoverLeftTimer_Tick);
			_leftPanelMouseLeaveTimer = new DispatcherTimer();
			_leftPanelMouseLeaveTimer.Interval = TimeSpan.FromSeconds(1);
			_leftPanelMouseLeaveTimer.Tick += _leftPanelMouseLeaveTimer_Tick;
		}
		/// <summary>
		/// 定时检查内存映射文件，看是否需要更换频道
		/// </summary>
		void CheckMappedFile()
		{
			DispatcherTimer checkMappedFileTimer = new DispatcherTimer();
			checkMappedFileTimer.Interval = TimeSpan.FromMilliseconds(50);
			checkMappedFileTimer.Tick += new EventHandler((o, e) =>
			{
				try
				{
					string content = App.ReadStringFromMappedFile();
					if (content.Length > 0)
					{
						if (content == "-show")
						{
							ShowFront();
						}
						else
						{
							Channel ch = Channel.FromCommandLineArgs(content);
							if (ch != null)
							{
								if (_player.IsInitialized) _player.CurrentChannel = ch;
								else _player.Settings.LastChannel = ch;
							}
						}
						App.ClearMappedFile();
					}
				}
				catch { }
			});
			_mappedFile = MemoryMappedFile.CreateOrOpen(App._mappedFileName, 10240);
			App.ClearMappedFile();
			checkMappedFileTimer.Start();
		}
		/// <summary>
		/// 启动时检查更新
		/// </summary>
		void CheckUpdateOnStartup()
		{
			if (_player.Settings.AutoUpdate && (DateTime.Now - _player.Settings.LastTimeCheckUpdate).TotalDays > 1)
			{
				Updater updater = new Updater(_player.Settings);
				updater.StateChanged += new EventHandler((o, e) =>
				{
					switch (updater.Now)
					{
						case Updater.State.CheckFailed:
							updater.Dispose();
							break;
						case Updater.State.NoNewVersion:
							updater.Dispose();
							break;
						case Updater.State.HasNewVersion:
							ShowUpdateWindow(updater);
							break;
					}
				});
				updater.Start();
			}
		}
		/// <summary>
		/// 清理从1.3.0版本至1.4.0版本以来的临时文件
		/// 这些临时文件存放的位置不对，系统似乎不会自动删除，使得占用空间不断增大
		/// </summary>
		void ClearOldTempFiles()
		{
			try
			{
				string fileFolder = System.Environment.GetFolderPath(System.Environment.SpecialFolder.InternetCache);
				string[] musicFiles = Directory.GetFiles(fileFolder, @"*.mp3");
				foreach (var file in musicFiles)
				{
					try
					{
						File.Delete(file);
					}
					catch { }
				}
				string[] exeFiles = Directory.GetFiles(fileFolder, @"DoubanFMSetup*.exe");
				foreach (var file in exeFiles)
				{
					try
					{
						File.Delete(file);
					}
					catch { }
				}
			}
			catch { }
		}

		/// <summary>
		/// 清理自动更新时下载的安装文件
		/// </summary>
		void ClearSetupFiles()
		{
			try
			{
				string fileFolder = Path.Combine(Path.GetTempPath(), "DoubanFM");
				string[] exeFiles = Directory.GetFiles(fileFolder, @"DoubanFMSetup*.exe");
				foreach (var file in exeFiles)
				{
					try
					{
						File.Delete(file);
					}
					catch { }
				}
			}
			catch { }
		}

		/// <summary>
		/// 初始化歌词
		/// </summary>
		void InitLyrics()
		{
			_lyricsSetting = LyricsSetting.Load();
			_lyricsWindow = new LyricsWindow(_lyricsSetting);

			Binding binding = new Binding();
			binding.Source = _lyricsWindow;
			binding.Path = new PropertyPath(LyricsWindow.LyricsText1Property);
			CurrentLyrics.SetBinding(TextBlock.TextProperty, binding);

			//System.Windows.Data.Binding binding2 = new System.Windows.Data.Binding("Opacity");
			//binding2.Source = _lyricsWindow.LyricsRoot;
			//CurrentLyrics.SetBinding(TextBlock.OpacityProperty, binding2);
		}

		/// <summary>
		/// 初始化分享设置
		/// </summary>
		void InitShareSetting()
		{
			ShareSetting = ShareSetting.Load();
			ApplyShareSetting();
		}

		/// <summary>
		/// 初始化代理设置
		/// </summary>
		void InitProxy()
		{
			ApplyProxy();
		}

		#endregion

		#region 操作

		/// <summary>
		/// 播放
		/// </summary>
		public void Play()
		{
			_player.IsPlaying = true;
		}

		/// <summary>
		/// 暂停
		/// </summary>
		public void Pause()
		{
			_player.IsPlaying = false;
		}

		/// <summary>
		/// 播放/暂停
		/// </summary>
		public void PlayPause()
		{
			_player.IsPlaying = !_player.IsPlaying;
		}

		/// <summary>
		/// 将这首歌标记为喜欢
		/// </summary>
		public void Like()
		{
			if (_player.IsLikedEnabled)
				_player.IsLiked = true;
		}

		/// <summary>
		/// 不将这首歌标记为喜欢
		/// </summary>
		public void Unlike()
		{
			if (_player.IsLikedEnabled)
				_player.IsLiked = false;
		}

		/// <summary>
		/// 改变这首歌的“喜欢”标记
		/// </summary>
		public void LikeUnlike()
		{
			if (_player.IsLikedEnabled)
				_player.IsLiked = !_player.IsLiked;
		}

		/// <summary>
		/// 将这首歌标记为不再播放
		/// </summary>
		public void Never()
		{
			_player.Never();
		}

		/// <summary>
		/// 下一首
		/// </summary>
		public void Next()
		{
			_player.Skip();
		}

		/// <summary>
		/// 显示/最小化 窗口
		/// </summary>
		public void ShowMinimize()
		{
			if (this.IsVisible && this.IsActive && this.WindowState == WindowState.Normal)
				this.WindowState = WindowState.Minimized;
			else this.ShowFront();
		}

		/// <summary>
		/// 显示/隐藏 窗口
		/// </summary>
		public void ShowHide()
		{
			if (this.IsVisible && this.IsActive && this.WindowState == WindowState.Normal)
				this.HideMinimized();
			else this.ShowFront();
		}

		/// <summary>
		/// 显示歌词
		/// </summary>
		public void ShowLyrics()
		{
			_player.Settings.ShowLyrics = true;
			if (_lyricsSetting.EnableDesktopLyrics) ShowDesktopLyrics();
			if (_lyricsSetting.EnableEmbeddedLyrics) ShowEmbeddedLyrics();
		}
		/// <summary>
		/// 隐藏歌词
		/// </summary>
		public void HideLyrics()
		{
			_player.Settings.ShowLyrics = false;
			if (_lyricsSetting.EnableDesktopLyrics) HideDesktopLyrics();
			if (_lyricsSetting.EnableEmbeddedLyrics) HideEmbeddedLyrics();
		}
		/// <summary>
		/// 显示/隐藏 歌词
		/// </summary>
		public void ShowHideLyrics()
		{
			if (_player.Settings.ShowLyrics)
				HideLyrics();
			else
				ShowLyrics();
		}
		/// <summary>
		/// 一键分享
		/// </summary>
		public void OneKeyShare()
		{
			if (!ShareSetting.EnableOneKeyShare || _player.CurrentSong == null) return;
			foreach (var site in ShareSetting.OneKeyShareSites)
			{
				new Share(_player, site).Go();
			}
		}
		/// <summary>
		/// 搜索下载
		/// </summary>
		public void SearchDownload()
		{
			if (_player.CurrentSong != null)
				DownloadSearch.Search(_player.CurrentSong.Title, _player.CurrentSong.Artist, _player.CurrentSong.Album);
		}

		/// <summary>
		/// 音量增
		/// </summary>
		public void VolumeUp(double delta = 0.1)
		{
			_player.Settings.Volume = Math.Min(1.0, _player.Settings.Volume + delta);
		}

		/// <summary>
		/// 音量减
		/// </summary>
		public void VolumeDown(double delta = 0.1)
		{
			_player.Settings.Volume = Math.Max(0.0, _player.Settings.Volume - delta);
		}

		/// <summary>
		/// 静音切换
		/// </summary>
		public void MuteSwitch()
		{
			_player.Settings.IsMuted = !_player.Settings.IsMuted;
		}

		#endregion

		#region 其他
		/// <summary>
		/// 应用当前代理设置
		/// </summary>
		internal void ApplyProxy()
		{
			try
			{
				switch (_player.Settings.ProxyKind)
				{
					case Settings.ProxyKinds.Default:
						ConnectionBase.UseDefaultProxy();
						BassEngine.Instance.UseDefaultProxy();
						break;
					case Settings.ProxyKinds.None:
						ConnectionBase.DontUseProxy();
						BassEngine.Instance.DontUseProxy();
						break;
					case Settings.ProxyKinds.Custom:
						ConnectionBase.SetProxy(_player.Settings.ProxyHost, _player.Settings.ProxyPort, _player.Settings.ProxyUsername, _player.Settings.ProxyPassword);
						BassEngine.Instance.SetProxy(_player.Settings.ProxyHost, _player.Settings.ProxyPort, _player.Settings.ProxyUsername, _player.Settings.ProxyPassword);
						break;
					default:
						break;
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, ex.Message, "代理设置失败", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}
		/// <summary>
		/// 应用当前分享设置
		/// </summary>
		internal void ApplyShareSetting()
		{
			foreach (FrameworkElement button in Shares.Children)
			{
				if (ShareSetting.DisplayedSites.Contains((Share.Sites)button.Tag))
					button.Visibility = Visibility.Visible;
				else
					button.Visibility = Visibility.Collapsed;
			}
			foreach (FrameworkElement button in ((PopupControlPanel)NotifyIcon.TrayPopup).Shares.Children)
			{
				if (ShareSetting.DisplayedSites.Contains((Share.Sites)button.Tag))
					button.Visibility = Visibility.Visible;
				else
					button.Visibility = Visibility.Collapsed;
			}
		}
		/// <summary>
		/// 显示桌面歌词
		/// </summary>
		internal void ShowDesktopLyrics()
		{
			_lyricsWindow.Show();
			if (_lyricsWindow.Lyrics == null) DownloadLyrics();
		}
		/// <summary>
		/// 显示内嵌歌词
		/// </summary>
		internal void ShowEmbeddedLyrics()
		{
			LyricsPanel.Visibility = Visibility.Visible;
			if (_lyricsWindow.Lyrics == null) DownloadLyrics();
		}

		/// <summary>
		/// 隐藏桌面歌词
		/// </summary>
		internal void HideDesktopLyrics()
		{
			_lyricsWindow.Hide();
		}
		/// <summary>
		/// 隐藏内嵌歌词
		/// </summary>
		internal void HideEmbeddedLyrics()
		{
			LyricsPanel.Visibility = Visibility.Hidden;
		}

		/// <summary>
		/// 显示在最上层
		/// </summary>
		public void ShowFront()
		{
			this.Show();
			this.WindowState = WindowState.Normal;
			this.Activate();
		}
		/// <summary>
		/// 最小化后隐藏
		/// </summary>
		public void HideMinimized()
		{
			this.WindowState = WindowState.Minimized;
			this.Hide();
		}
		/// <summary>
		/// 设置歌词
		/// </summary>
		void SetLyrics(Lyrics lyrics)
		{
			if (_lyricsWindow != null)
				_lyricsWindow.Lyrics = lyrics;
		}
		/// <summary>
		/// 给热键添加逻辑
		/// </summary>
		void AddLogicToHotKeys(HotKeys hotKeys)
		{
			foreach (var keyValue in hotKeys)
			{
				HotKey hotKey = keyValue.Value;
				switch (keyValue.Key)
				{
					case Commands.None:
						break;
					case Commands.Like:
						hotKey.OnHotKey += delegate
						{
							Like();
							if (_player.CurrentSong != null && _player.IsLikedEnabled && _player.IsLiked)
								NotifyIcon.ShowCustomBalloon(new PopupLiked(), System.Windows.Controls.Primitives.PopupAnimation.Fade, 1000);
						};
						break;
					case Commands.Unlike:
						hotKey.OnHotKey += delegate { Unlike(); };
						break;
					case Commands.LikeUnlike:
						hotKey.OnHotKey += delegate { LikeUnlike(); };
						break;
					case Commands.Never:
						hotKey.OnHotKey += delegate { Never(); };
						break;
					case Commands.PlayPause:
						hotKey.OnHotKey += delegate { PlayPause(); };
						break;
					case Commands.Next:
						hotKey.OnHotKey += delegate { Next(); };
						break;
					case Commands.ShowMinimize:
						hotKey.OnHotKey += delegate { ShowMinimize(); };
						break;
					case Commands.ShowHide:
						hotKey.OnHotKey += delegate { ShowHide(); };
						break;
					case Commands.ShowLyrics:
						hotKey.OnHotKey += delegate { ShowLyrics(); };
						break;
					case Commands.HideLyrics:
						hotKey.OnHotKey += delegate { HideLyrics(); };
						break;
					case Commands.ShowHideLyrics:
						hotKey.OnHotKey += delegate { ShowHideLyrics(); };
						break;
					case Commands.OneKeyShare:
						hotKey.OnHotKey += delegate { OneKeyShare(); };
						break;
					case Commands.SearchDownload:
						hotKey.OnHotKey += delegate { SearchDownload(); };
						break;
					case Commands.VolumeUp:
						hotKey.OnHotKey += delegate { VolumeUp(); };
						break;
					case Commands.VolumeDown:
						hotKey.OnHotKey += delegate { VolumeDown(); };
						break;
					case Commands.MuteSwitch:
						hotKey.OnHotKey += delegate { MuteSwitch(); };
						break;
				}
			}
		}

		/// <summary>
		/// 暂存正在下载的歌词所对应的歌曲
		/// </summary>
		Song downloadingLyrics = null;
		/// <summary>
		/// 下载歌词
		/// </summary>
		void DownloadLyrics()
		{
			if (_player == null || _player.CurrentSong == null) return;
			if (_player.CurrentSong == downloadingLyrics) return;
			Song song = (Song)_player.CurrentSong.Clone();
			downloadingLyrics = song;
			ThreadPool.QueueUserWorkItem(new WaitCallback(o =>
			{
				Lyrics lyrics = LyricsAssistant.GetLyrics(song.Artist, song.Title);
				Dispatcher.Invoke(new Action(() =>
				{
					if (_player.CurrentSong == song) SetLyrics(lyrics);
				}));
			}));
		}
		/// <summary>
		/// 显示更新窗口
		/// </summary>
		/// <param name="updater">指定的更新器</param>
		void ShowUpdateWindow(Updater updater = null)
		{
			CheckUpdate.IsEnabled = false;
			UpdateWindow update = new UpdateWindow(updater);
			update.Closed += new EventHandler((o, e) =>
			{
				CheckUpdate.IsEnabled = true;
				_leftPanelMouseLeaveTimer.Start();
				if (update.Updater.Now == Updater.State.DownloadCompleted)
				{
					App.Current.Exit += new ExitEventHandler((oo, ee) =>
					{
						Process.Start(update.Updater.DownloadedFilePath, "/S");
					});
					//有可能是主窗口的关闭引起更新窗口的关闭，这时再关闭主窗口会出错。
					try
					{
						this.Close();
					}
					catch { }
				}
			});
			update.Show();
		}
		/// <summary>
		/// 显示频道列表
		/// </summary>
		private void ShowChannels()
		{
			RefreshMyChannels();
			PublicChannels.ItemsSource = new ObservableCollection<Channel>(_player.ChannelInfo.Public);
		}
		/// <summary>
		/// 刷新“我的电台”列表
		/// </summary>
		public void RefreshMyChannels()
		{
			ObservableCollection<Channel> PersonalChannelsItem = new ObservableCollection<Channel>();
			if (_player.UserAssistant.IsLoggedOn)
				foreach (Channel channel in _player.ChannelInfo.Personal)
					PersonalChannelsItem.Add(channel);
			foreach (Channel channel in _player.Settings.FavoriteChannels)
				PersonalChannelsItem.Add(channel);
			PersonalChannels.ItemsSource = PersonalChannelsItem;
		}
		/// <summary>
		/// 更换选中的频道列表
		/// </summary>
		private void ChangeChosenChannelList()
		{
			Channel PersonalOld = PersonalChannels.SelectedItem as Channel;
			Channel PublicOld = PublicChannels.SelectedItem as Channel;
			Channel DjOld = DjChannels.SelectedItem as Channel;
			PersonalChannels.SelectedItem = PersonalChannels.Items.OfType<Channel>().FirstOrDefault(x => x == _player.CurrentChannel);
			PublicChannels.SelectedItem = PublicChannels.Items.OfType<Channel>().FirstOrDefault(x => x == _player.CurrentChannel);
			DjChannels.SelectedItem = DjChannels.Items.OfType<Channel>().FirstOrDefault(x => x == _player.CurrentChannel);
			SearchResultList.SelectedItem = SearchResultList.Items.OfType<Channel>().FirstOrDefault(x => x == _player.CurrentChannel);
			Channel PersonalNew = PersonalChannels.SelectedItem as Channel;
			Channel PublicNew = PublicChannels.SelectedItem as Channel;
			Channel DjNew = DjChannels.SelectedItem as Channel;
			if (PersonalNew != null && PersonalNew == PersonalOld) return;
			if (PublicNew != null && PublicNew == PublicOld) return;
			if (DjNew != null && DjNew == DjOld) return;

			if (PersonalNew != null)
			{
				ButtonPersonal.IsChecked = true;
			}
			else if (PublicNew != null)
			{
				ButtonPublic.IsChecked = true;
			}
			else if (DjNew != null)
			{
				ButtonDj.IsChecked = true;
			}
		}

		List<Channel> filteredDjChannels = new List<Channel>();
		int showedDjChannelsCount = 0;

		/// <summary>
		/// 显示DJ兆赫列表
		/// </summary>
		/// <param name="searchText">搜索的文本</param>
		private void ShowDjChannels(string searchText, int count = 10)
		{
			if (!_player.IsInitialized) return;			//电台未初始化完成
			string[] words = null;
			if (searchText != null)
			{
				words = (from word in searchText.Split() where word.Length > 0 select word).ToArray();
			}
			filteredDjChannels.Clear();
			filteredDjChannels.AddRange(
				from channel in _player.ChannelInfo.Dj
				where words.All(word => channel.Name.Contains(word))
				select channel);

			ScrollViewer scrollViewer = FindVisualChild<ScrollViewer>(DjChannels);
			if (scrollViewer != null)
			{
				scrollViewer.ScrollToTop();
			}
			var collection = new ObservableCollection<Channel>(filteredDjChannels.Take(count));
			DjChannels.ItemsSource = collection;
			showedDjChannelsCount = collection.Count;

			GC.Collect();
		}

		private childItem FindVisualChild<childItem>(DependencyObject obj) where childItem : DependencyObject
		{
			for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
			{
				DependencyObject child = VisualTreeHelper.GetChild(obj, i);
				if (child != null && child is childItem)
				{
					return (childItem)child;
				}
				else
				{
					childItem childOfChild = FindVisualChild<childItem>(child);
					if (childOfChild != null)
					{
						return childOfChild;
					}
				}
			}
			return null;
		}

		/// <summary>
		/// 更新界面内容，主要是音乐信息。换音乐时自动调用。
		/// </summary>
		private void Update()
		{
			Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 获取到新的歌曲信息");
			Debug.Indent();
			Debug.WriteLine(_player.CurrentSong.ToString());
			Debug.Unindent();
			ChangeCover();
			SetLyrics(null);
			if (_player.Settings.ShowLyrics) DownloadLyrics();

			((StringAnimationUsingKeyFrames)ChangeSongInfoStoryboard.Children[1]).KeyFrames[0].Value = _player.CurrentSong.Title;
			((StringAnimationUsingKeyFrames)ChangeSongInfoStoryboard.Children[2]).KeyFrames[0].Value = _player.CurrentSong.Artist;
			((StringAnimationUsingKeyFrames)ChangeSongInfoStoryboard.Children[3]).KeyFrames[0].Value = _player.CurrentSong.Album;
			ChangeSongInfoStoryboard.Begin();

			string stringA = _player.CurrentSong.Title + " - " + _player.CurrentSong.Artist;
			string stringB = "    豆瓣电台 - " + _player.CurrentChannel.Name;
			this.Title = stringA + stringB;

			string song = _player.CurrentSong.ToString();
			if (song.Length <= 63)							//Windows限制托盘图标的提示信息最长为63个字符
			{
				NotifyIcon.ToolTipText = song;
			}
			else if (this.Title.Length <= 63)
			{
				NotifyIcon.ToolTipText = this.Title;
			}
			else
			{
				string dotdotdot = "...";
				string text = song.Substring(0, Math.Max(0, 63 - dotdotdot.Length));
				NotifyIcon.ToolTipText = text + dotdotdot;
			}

			//延迟弹出气泡
			CustomBaloon = null;
			Song lastSong = _player.CurrentSong;
			var timer = new DispatcherTimer();
			timer.Interval = TimeSpan.FromSeconds(2);
			timer.Tick += delegate
			{
				if (_player.Settings.ShowBalloonWhenSongChanged && !NotifyIcon.TrayPopupResolved.IsOpen && !NotifyIcon.TrayToolTipResolved.IsOpen)
				{
					if (!stoped && CustomBaloon == null && _player.CurrentSong == lastSong)
					{
						CustomBaloon = new NotifyIcon.BalloonSongInfo();
						var image = CustomBaloon.Cover.Source as BitmapSource;
						if (image != null && !image.IsDownloading)
						{
							CustomBaloon.Cover.Opacity = 1;
						}
						NotifyIcon.ShowCustomBalloon(CustomBaloon, System.Windows.Controls.Primitives.PopupAnimation.Fade, 5000);
					}
				}
				timer.Stop();
			};
			timer.Start();

			//ChannelTextBlock.Text = _player.CurrentChannel.Name;
			TotalTime.Text = TimeSpanToStringConverter.QuickConvert(_player.CurrentSong.Length);
			CurrentTime.Text = TimeSpanToStringConverter.QuickConvert(TimeSpan.Zero);
			Slider.Minimum = 0;
			Slider.Maximum = Math.Max(1.0, _player.CurrentSong.Length.TotalSeconds);
			Slider.Value = 0;

			BassEngine.Instance.OpenUrlAsync(_player.CurrentSong.FileUrl);
			//_lastTimeChangeSong = DateTime.Now;

			////防止无故静音
			//Audio.IsMuted = !Audio.IsMuted;
			//Audio.Volume = _player.Settings.Volume;
			//DispatcherTimer timer = new DispatcherTimer();
			//timer.Interval = TimeSpan.FromMilliseconds(50);
			//timer.Tick += new EventHandler((sender, e) =>
			//{
			//    Audio.IsMuted = !Audio.IsMuted;
			//    timer.Stop();
			//});
			//timer.Start();			
		}

		/// <summary>
		/// 弹出气泡
		/// </summary>
		public NotifyIcon.BalloonSongInfo CustomBaloon;

		/// <summary>
		/// 关闭当前气泡
		/// </summary>
		private void CloseCurrentBalloon()
		{
			if (NotifyIcon.CustomBalloon != null)
			{
				var balloon = NotifyIcon.CustomBalloon.Child as BalloonSongInfo;
				if (balloon != null)
				{
					balloon.ClearBindings();
				}
			}
			NotifyIcon.CloseBalloon();
		}

		BitmapImage bitmap;
		bool downloadFailed = false;
		bool shouldSwitchCover = false;
		/// <summary>
		/// 封面下载成功时调用以更换封面
		/// </summary>
		void OnCoverDownloadCompleted()
		{
			if (!shouldSwitchCover) return;
			if (downloadFailed)
			{
				if (bitmap != null && bitmap.UriSource != null && _player.CurrentSong != null && _player.CurrentSong.FileUrl != null && bitmap.UriSource.AbsoluteUri != new Uri(_player.CurrentSong.Picture).AbsoluteUri) return;
				shouldSwitchCover = false;
				bitmap = new BitmapImage(new Uri("pack://application:,,,/DoubanFM;component/Images/DoubanFM_NoCover.png"));
				if (bitmap.CanFreeze) bitmap.Freeze();
				ChangeBackground(bitmap);
				SwitchCover(bitmap);
			}
			else
			{
				if (bitmap == null) return;
				if (bitmap.CanFreeze) bitmap.Freeze();
				if (bitmap.UriSource != null && _player.CurrentSong != null && _player.CurrentSong.FileUrl != null && bitmap.UriSource.AbsoluteUri == new Uri(_player.CurrentSong.Picture).AbsoluteUri)
				{
					shouldSwitchCover = false;
					ChangeBackground(bitmap);
					SwitchCover(bitmap);

					((NotifyIcon.BalloonSongInfo)NotifyIcon.TrayToolTip).ShowCoverSmooth();
					((NotifyIcon.PopupControlPanel)NotifyIcon.TrayPopup).ShowCoverSmooth();
					if (CustomBaloon != null)
					{
						CustomBaloon.ShowCoverSmooth();
					}
				}
			}
		}
		/// <summary>
		/// 更改封面
		/// </summary>
		void ChangeCover()
		{
			shouldSwitchCover = false;
			downloadFailed = false;
			try
			{
				bitmap = new BitmapImage();
				//图片下载失败
				bitmap.DownloadFailed += new EventHandler<ExceptionEventArgs>((o, e) =>
				{
					if (o == bitmap)
					{
						downloadFailed = true;
					}
				});
				//似乎豆瓣的图片有问题，所以这里不直接用Uri构造一个BitmapImage
				bitmap.BeginInit();
				bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
				bitmap.UriSource = new Uri(_player.CurrentSong.Picture);
				bitmap.EndInit();
				shouldSwitchCover = true;
			}
			catch
			{
				downloadFailed = true;
				shouldSwitchCover = true;
			}
		}
		/// <summary>
		/// 将窗口背景更换为指定的颜色
		/// </summary>
		void ChangeBackground(Color color)
		{
			((ColorAnimation)BackgroundColorStoryboard.Children[0]).To = color;
			BackgroundColorStoryboard.Begin();
		}
		/// <summary>
		/// 根据封面颜色更换背景。封面加载成功时自动调用
		/// </summary>
		/// <param name="NewCover">新封面</param>
		void ChangeBackground(BitmapSource NewCover)
		{
			ColorFunctions.GetImageColorForBackgroundAsync(NewCover, new ColorFunctions.ComputeCompleteCallback((color) =>
				{
					Dispatcher.Invoke(new Action(() =>
						{
							ChangeBackground(color);
						}));
				}));
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
		/// <summary>
		/// 将频道添加到跳转列表
		/// </summary>
		private void AddChannelToJumpList(Channel channel)
		{
			JumpList jumpList = JumpList.GetJumpList(App.Current);
			if (jumpList == null) jumpList = new JumpList();
			jumpList.ShowRecentCategory = true;
			jumpList.ShowFrequentCategory = true;
			foreach (JumpTask jumpItem in jumpList.JumpItems)
			{
				if (jumpItem.Title == channel.Name) return;
			}
			JumpTask jumpTask = new JumpTask();
			jumpTask.Title = channel.Name;
			jumpTask.Description = jumpTask.Title;
			jumpTask.Arguments = channel.ToCommandLineArgs();
			JumpList.AddToRecentCategory(jumpTask);
			JumpList.SetJumpList(App.Current, jumpList);
		}

		#endregion

		#region 事件响应
		/// <summary>
		/// 主界面中按下“下一首”
		/// </summary>
		private void ButtonNext_Click(object sender, RoutedEventArgs e)
		{
			Next();
		}
		/// <summary>
		/// 计时器响应函数，用于更新时间信息和歌词
		/// </summary>
		void timer_Tick(object sender, EventArgs e)
		{
			CurrentTime.Text = TimeSpanToStringConverter.QuickConvert(BassEngine.Instance.ChannelPosition);
			Slider.Value = BassEngine.Instance.ChannelPosition.TotalSeconds;
			if (_lyricsWindow != null) _lyricsWindow.Refresh(BassEngine.Instance.ChannelPosition);
			if (isEnhancementsPanelShowing && DateTime.Now - lastTimeRightPanelMouseMove >= TimeSpan.FromSeconds(10))
			{
				isEnhancementsPanelShowing = false;
				EnhancementsPanelHide.Begin();
			}

			if (shouldSwitchCover && (bitmap == null || !bitmap.IsDownloading))
			{
				OnCoverDownloadCompleted();
			}
		}
		///// <summary>
		///// 在网络不好时有用
		///// </summary>
		//void _forceNextTimer_Tick(object sender, EventArgs e)
		//{
		//    if (Audio.NaturalDuration.HasTimeSpan)
		//        if ((Audio.Position - Audio.NaturalDuration.TimeSpan).TotalSeconds > 5)
		//        {
		//            Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 网络不好吧，显示的时间已经超过总时间了。是不是没声音啊？我换下一首了……");
		//            _player.CurrentSongFinishedPlaying();
		//            return;
		//        }
		//    if (Audio.Position == TimeSpan.Zero && DateTime.Now - _lastTimeChangeSong > TimeSpan.FromSeconds(30))
		//    {
		//        Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + "网络太慢了，加载很久都不能播放……");
		//        _player.CurrentSongFinishedPlaying();
		//        return;
		//    }
		//}
		/// <summary>
		/// 任务栏按下“暂停/播放”按钮
		/// </summary>
		private void PauseThumb_Click(object sender, EventArgs e)
		{
			PlayPause();
		}
		/// <summary>
		/// 任务栏按下“下一首”按钮
		/// </summary>
		private void NextThumb_Click(object sender, EventArgs e)
		{
			Next();
		}
		/// <summary>
		/// 保存各种信息
		/// </summary>
		private void Window_Closed(object sender, EventArgs e)
		{
			_mappedFile.Dispose();

			Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 主窗口已关闭，正在保存设置");
			if (_lyricsWindow != null)
				_lyricsWindow.Close();
			BassEngine.Instance.Stop();
			//if (Audio != null)
			//    Audio.Close();
			if (HotKeys != null)
			{
				HotKeys.UnRegister();
				if (SaveSettings)
				{
					HotKeys.Save();
				}
			}
			if (SaveSettings)
			{
				if (_lyricsSetting != null)
				{
					_lyricsSetting.Save();
				}
				if (ShareSetting != null)
				{
					ShareSetting.Save();
				}
			}
			if (NotifyIcon != null)
				NotifyIcon.Dispose();
			if (_player != null)
				_player.Dispose(SaveSettings);
		}
		/// <summary>
		/// 更新密码
		/// </summary>
		private void PbPassword_PasswordChanged(object sender, RoutedEventArgs e)
		{
			_player.Settings.User.Password = PbPassword.Password;
		}
		/// <summary>
		/// 登录
		/// </summary>
		private void ButtonLogOn_Click(object sender, RoutedEventArgs e)
		{
			_player.UserAssistant.LogOn(_player.UserAssistant.HasCaptcha ? CaptchaText.Text : null);
			CaptchaText.Text = null;
		}
		/// <summary>
		/// 注销
		/// </summary>
		private void ButtonLogOff_Click(object sender, RoutedEventArgs e)
		{
			_player.UserAssistant.LogOff();
		}
		///// <summary>
		///// 验证码被点击
		///// </summary>
		//private void ButtonRefreshCaptcha_Click(object sender, System.Windows.RoutedEventArgs e)
		//{
		//    _player.UserAssistant.Refresh();
		//}

		/// <summary>
		/// 任务栏点击“喜欢”
		/// </summary>
		private void LikeThumb_Click(object sender, EventArgs e)
		{
			LikeUnlike();
		}
		/// <summary>
		/// 主界面点击“不再播放”
		/// </summary>
		private void ButtonNever_Click(object sender, RoutedEventArgs e)
		{
			Never();
		}
		/// <summary>
		/// 任务栏点击“不再播放”
		/// </summary>
		private void NeverThumb_Click(object sender, EventArgs e)
		{
			Never();
		}

		/// <summary>
		/// 更换私人频道
		/// </summary>
		private void PersonalChannels_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (PersonalChannels.SelectedItem != null)
				_player.CurrentChannel = (Channel)PersonalChannels.SelectedItem;
		}
		/// <summary>
		/// 更换公共频道
		/// </summary>
		private void PublicChannels_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (PublicChannels.SelectedItem != null)
				_player.CurrentChannel = (Channel)PublicChannels.SelectedItem;
		}
		/// <summary>
		/// 更换DJ节目
		/// </summary>
		private void DjChannels_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (DjChannels.SelectedItem != null)
			{
				_player.CurrentChannel = (Channel)DjChannels.SelectedItem;
			}
		}
		/// <summary>
		/// 鼠标左键点击封面时滑动封面
		/// </summary>
		private void CoverGrid_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			if (IsDraging) return;
			if (e.GetPosition(CoverGrid) == new Point(0, 0)) return;		//狂甩窗口时可能会触发MouseLeftButtonUp事件……
			if (!_player.Settings.SlideCoverWhenMouseMove || !_player.Settings.OpenAlbumInfoWhenClickCover || _player.CurrentSong == null)
			{
				Point leftLocation = e.GetPosition(LeftPanel);
				//Debug.WriteLine("LeftPanel:" + leftLocation);
				HitTestResult leftResult = VisualTreeHelper.HitTest(LeftPanel, leftLocation);
				if (leftResult != null)
				{
					//Debug.WriteLine("SlideRight");
					SlideCoverRightStoryboard.Begin();
					return;
				}
				Point rightLocation = e.GetPosition(RightPanel);
				//Debug.WriteLine("RightPanel:" + rightLocation);
				HitTestResult rightResult = VisualTreeHelper.HitTest(RightPanel, rightLocation);
				if (rightResult != null)
				{
					//Debug.WriteLine("SlideLeft");
					SlideCoverLeftStoryboard.Begin();
				}
			}
			else
			{
				if (_player.CurrentSong != null && !string.IsNullOrEmpty(_player.CurrentSong.AlbumInfo))
				{
					_slideCoverLeftTimer.Stop();
					_slideCoverRightTimer.Stop();
					if (_player.CurrentSong.AlbumInfo.Contains("http://"))
						Core.UrlHelper.OpenLink(_player.CurrentSong.AlbumInfo);
					else Core.UrlHelper.OpenLink("http://music.douban.com" + _player.CurrentSong.AlbumInfo);
				}
			}
		}

		void SlideCoverRightTimer_Tick(object sender, EventArgs e)
		{
			if (!IsDraging && Mouse.LeftButton != MouseButtonState.Pressed)
			{
				SlideCoverRightStoryboard.Begin();
			}
			_slideCoverRightTimer.Stop();
		}
		void SlideCoverLeftTimer_Tick(object sender, EventArgs e)
		{
			if (!IsDraging && Mouse.LeftButton != MouseButtonState.Pressed)
			{
				SlideCoverLeftStoryboard.Begin();
			}
			_slideCoverLeftTimer.Stop();
		}

		/// <summary>
		/// 当鼠标移动时滑动封面
		/// </summary>
		private void CoverGrid_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
		{
			if (IsDraging) return;
			if (_player.Settings.SlideCoverWhenMouseMove)
			{
				Point leftLocation = e.GetPosition(LeftPanel);
				//Debug.WriteLine("LeftPanel:" + leftLocation);
				HitTestResult leftResult = VisualTreeHelper.HitTest(LeftPanel, leftLocation);
				if (leftResult != null)
				{
					//Debug.WriteLine("SlideRight");
					_slideCoverRightTimer.Start();
					_slideCoverLeftTimer.Stop();
					return;
				}
				Point rightLocation = e.GetPosition(RightPanel);
				//Debug.WriteLine("RightPanel:" + rightLocation);
				HitTestResult rightResult = VisualTreeHelper.HitTest(RightPanel, rightLocation);
				if (rightResult != null)
				{
					//Debug.WriteLine("SlideLeft");
					_slideCoverLeftTimer.Start();
					_slideCoverRightTimer.Stop();
				}
			}
		}

		/// <summary>
		/// 当鼠标移出封面时停止计时
		/// </summary>
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
			this.HideMinimized();
		}

		private void ButtonExit_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			this.Close();
		}

		private void Window_IsVisibleChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
		{
			Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + (this.IsVisible ? " 窗口可视" : " 窗口不可视"));
			if (this.IsVisible && !_player.Settings.AlwaysShowNotifyIcon)
			{
				NotifyIcon.Visibility = Visibility.Hidden;
			}
			else
			{
				//窗口关闭时IsVisiblie会自动变为false，而关闭时不应再改变托盘图标的可见性
				if (this.WindowState == System.Windows.WindowState.Minimized || this.Visibility != Visibility.Visible)
				{
					NotifyIcon.Visibility = Visibility.Visible;
				}
			}
		}

		private void CheckUpdate_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			ShowUpdateWindow();
		}

		private void VisitOfficialWebsite_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			Core.UrlHelper.OpenLink("http://douban.fm/");
		}

		private void Search_Click(object sender, RoutedEventArgs e)
		{
			_player.ChannelSearch.StartSearch(SearchText.Text);
		}

		private void PreviousPage_Click(object sender, RoutedEventArgs e)
		{
			_player.ChannelSearch.PreviousPage();
		}

		private void NextPage_Click(object sender, RoutedEventArgs e)
		{
			_player.ChannelSearch.NextPage();
		}

		private void SearchResultList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
		{
			if (SearchResultList.SelectedItem == null) return;
			Channel channel = ((ChannelSearchItem)SearchResultList.SelectedItem).GetChannel();
			if (channel != null) _player.CurrentChannel = channel;
		}

		/*private void Window_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
		{
			switch (e.Key)
			{
				case System.Windows.Input.Key.MediaPlayPause:
					PlayPause();
					break;
				case System.Windows.Input.Key.MediaNextTrack:
					Next();
					break;
			}
		}*/

		private void Feedback_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			// 在此处添加事件处理程序实现。
			Core.UrlHelper.OpenLink("http://www.kfstorm.com/blog/2011/12/01/%E8%B1%86%E7%93%A3%E7%94%B5%E5%8F%B0faq/");
		}

		private void CheckBoxShowLyrics_Checked(object sender, System.Windows.RoutedEventArgs e)
		{
			// 在此处添加事件处理程序实现。
			if (_player != null) ShowLyrics();
		}

		private void CheckBoxShowLyrics_Unchecked(object sender, RoutedEventArgs e)
		{
			if (_player != null) HideLyrics();
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " Window.Loaded事件已触发，主窗口已准备好呈现");

			Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 初始化代理设置");
			InitProxy();
			Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 初始化代理设置完成");

			Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 启动播放器");
			_player.Initialize();

			Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 加载热键设置");
			//加载热键设置
			HotKeys = HotKeys.Load();
			HotKeys.RegisterError += new EventHandler<HotKeys.RegisterErrorEventArgs>((oo, ee) =>
			{
				System.Text.StringBuilder sb = new System.Text.StringBuilder();
				foreach (var exception in ee.Exceptions)
				{
					sb.AppendLine(exception.Message);
				}
				MessageBox.Show(sb.ToString());
			});

			AddLogicToHotKeys(HotKeys);
			HotKeys.Register(this);

			Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 加载热键设置完成");

			//设置歌词显示
			if (_player.Settings.ShowLyrics)
			{
				Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 显示歌词");
				ShowLyrics();
				Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 显示歌词完成");
			}

			//开始刷新歌曲进度
			_progressRefreshTimer = new DispatcherTimer();
			_progressRefreshTimer.Interval = new TimeSpan(1000000);
			_progressRefreshTimer.Tick += new EventHandler(timer_Tick);
			_progressRefreshTimer.Start();

			lastTimeRightPanelMouseMove = DateTime.Now;
		}

		private void ButtonGeneralSetting_Click(object sender, RoutedEventArgs e)
		{
			ButtonGeneralSetting.IsEnabled = false;
			GeneralSettingWindow window = new GeneralSettingWindow();
			window.Closed += delegate { ButtonGeneralSetting.IsEnabled = true; _leftPanelMouseLeaveTimer.Start(); };
			window.Show();
		}

		private void ButtonUISetting_Click(object sender, RoutedEventArgs e)
		{
			ButtonUISetting.IsEnabled = false;
			UISettingWindow window = new UISettingWindow();
			window.Closed += delegate { ButtonUISetting.IsEnabled = true; _leftPanelMouseLeaveTimer.Start(); };
			window.Show();
		}

		private void LyricsSetting_Click(object sender, RoutedEventArgs e)
		{
			ButtonLyricsSetting.IsEnabled = false;
			LyricsSettingWindow window = new LyricsSettingWindow(_lyricsSetting);
			{
				System.Windows.Data.Binding binding = new System.Windows.Data.Binding("ShowLyrics");
				binding.Source = _player.Settings;
				window.CbShowLyrics.SetBinding(CheckBox.IsCheckedProperty, binding);
			}
			window.Closed += delegate { ButtonLyricsSetting.IsEnabled = true; _leftPanelMouseLeaveTimer.Start(); };
			window.Show();
		}

		private void ButtonShareSetting_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			ButtonShareSetting.IsEnabled = false;
			ShareSettingWindow window = new ShareSettingWindow(ShareSetting);
			window.Closed += delegate { ButtonShareSetting.IsEnabled = true; _leftPanelMouseLeaveTimer.Start(); };
			window.Show();
		}

		private void ButtonHotKeySettings_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			// 在此处添加事件处理程序实现。
			ButtonHotKeySettings.IsEnabled = false;
			HotKeys.UnRegister();
			HotKeySettingWindow hotKeyWindow = new HotKeySettingWindow(this, HotKeys);
			hotKeyWindow.Closed += new EventHandler((o, ee) =>
			{
				ButtonHotKeySettings.IsEnabled = true;
				_leftPanelMouseLeaveTimer.Start();
				HotKeys = hotKeyWindow.HotKeys;
				AddLogicToHotKeys(HotKeys);
				HotKeys.Register(this);
			});
			hotKeyWindow.Show();
		}

		private void ShareButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			// 在此处添加事件处理程序实现。
			if (_player.CurrentSong != null)
				new Share(_player, (Share.Sites)((FrameworkElement)e.Source).Tag).Go();
		}

		private void GoToHomePage_Click(object sender, RoutedEventArgs e)
		{
			Core.UrlHelper.OpenLink("http://www.kfstorm.com/blog/doubanfm/");
		}

		private void BtnCopyUrl_Click(object sender, RoutedEventArgs e)
		{
			if (_player.CurrentSong != null)
			{
				new Share(_player).Go();
				MessageBox.Show(this, "地址已复制到剪贴板", "复制成功", MessageBoxButton.OK, MessageBoxImage.Information);
			}
		}

		private void BtnOneKeyShare_Click(object sender, RoutedEventArgs e)
		{
			OneKeyShare();
		}

		private void BtnHelp_Click(object sender, RoutedEventArgs e)
		{
			BtnHelp.IsEnabled = false;
			HelpWindow window = new HelpWindow();
			window.Closed += delegate { BtnHelp.IsEnabled = true; _leftPanelMouseLeaveTimer.Start(); };
			window.Show();
		}

		private void HlPlayed_Click(object sender, RoutedEventArgs e)
		{
			Core.UrlHelper.OpenLink("http://douban.fm/mine?type=played");
		}

		private void HlLiked_Click(object sender, RoutedEventArgs e)
		{
			Core.UrlHelper.OpenLink("http://douban.fm/mine?type=liked");
		}

		private void HlBanned_Click(object sender, RoutedEventArgs e)
		{
			Core.UrlHelper.OpenLink("http://douban.fm/mine?type=banned");
		}

		private void NotifyIcon_TrayLeftMouseUp(object sender, RoutedEventArgs e)
		{
			if (this.IsVisible)
				this.HideMinimized();
			else
				this.ShowFront();
		}

		private bool stoped = false;

		private void VolumeFadeOut_Completed(object sender, EventArgs e)
		{
			if (stoped)
			{
				BassEngine.Instance.Stop();
			}
			else if (!_player.IsPlaying)
			{
				BassEngine.Instance.Pause();
			}
		}

		private void ButtonSignUp_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			Core.UrlHelper.OpenLink("http://www.douban.com/accounts/register");
		}

		bool resetting = false;
		private void BtnResetSettings_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			resetting = true;
			if (MessageBox.Show("确定要重置所有设置吗？\n重置后软件将自动重启。", "请注意", MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK)
			{
				try
				{
					//删除所有设置

					string dataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"K.F.Storm\豆瓣电台");
					if (Directory.Exists(dataFolder))
					{
						string[] files = Directory.GetFiles(dataFolder);
						foreach (var file in files)
						{
							File.Delete(file);
						}
						Directory.Delete(dataFolder);
					}

					SaveSettings = false;
					_mappedFile.Dispose();
					//关闭当前程序并启动一个新的程序
					App.Current.Shutdown();
					Process.Start(System.Reflection.Assembly.GetEntryAssembly().GetModules()[0].FullyQualifiedName);
				}
				catch (Exception ex)
				{
					MessageBox.Show(ex.Message, "重置设置失败", MessageBoxButton.OK, MessageBoxImage.Error);
				}
			}
			resetting = false;
			_leftPanelMouseLeaveTimer.Start();
		}

		private void BtnDownloadSearch_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			SearchDownload();
		}

		private void ButtonRefreshCaptcha_Click(object sender, RoutedEventArgs e)
		{
			_player.UserAssistant.UpdateCaptcha();
		}

		private void ControlPanel_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
		{
			if (e.OriginalSource == ControlPanel)
			{
				if (e.AddedItems.Contains(Account))
				{
					if (_player.UserAssistant.CurrentState == UserAssistant.State.LoggedOff)
					{
						if (!_player.UserAssistant.HasCaptcha)
						{
							_player.UserAssistant.UpdateCaptcha();
						}
					}
				}
			}
		}

		private void Window_LocationChanged(object sender, EventArgs e)
		{
			if (!this.RestoreBounds.IsEmpty)
			{
				//_player.Settings.LocationLeft = this.Left;
				//_player.Settings.LocationTop = this.Top;
				_player.Settings.LocationLeft = this.RestoreBounds.Left;
				_player.Settings.LocationTop = this.RestoreBounds.Top;
			}
		}

		private DateTime lastTimeRightPanelMouseMove = DateTime.MaxValue;
		private bool isEnhancementsPanelShowing = true;

		private void RightPanel_MouseMove(object sender, MouseEventArgs e)
		{
			lastTimeRightPanelMouseMove = DateTime.Now;
			if (!isEnhancementsPanelShowing)
			{
				isEnhancementsPanelShowing = true;
				EnhancementsPanelShow.Begin();
			}
		}

		private void BtnDonate_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			Core.UrlHelper.OpenLink("http://me.alipay.com/kfstorm");
		}

		private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
		{
			if (_player.Settings.AdjustVolumeWithMouseWheel)
			{
				if (e.Delta > 0)
				{
					VolumeUp((double)e.Delta / Mouse.MouseWheelDeltaForOneLine / 10);
				}
				else if (e.Delta < 0)
				{
					VolumeDown(-(double)e.Delta / Mouse.MouseWheelDeltaForOneLine / 10);
				}
				e.Handled = true;
			}
		}

		private void BtnSearchDj_Click(object sender, RoutedEventArgs e)
		{
			ShowDjChannels(TbSearchDj.Text);
		}

		private void DjChannels_ScrollChanged(object sender, ScrollChangedEventArgs e)
		{
			//Debug.WriteLine(string.Format("VerticalOffset: {0} ViewportHeight: {1} ExtentHeight: {2}", e.VerticalOffset, e.ViewportHeight, e.ExtentHeight));
			if (e.VerticalOffset + e.ViewportHeight >= e.ExtentHeight)
			{
				var source = DjChannels.ItemsSource as ObservableCollection<Channel>;
				if (source != null)
				{
					for (int i = showedDjChannelsCount; i < filteredDjChannels.Count && i < showedDjChannelsCount + 10; ++i)
					{
						source.Add(filteredDjChannels[i]);
					}
					showedDjChannelsCount = source.Count;
				}
			}
		}

		private void LeftPanel_MouseLeave(object sender, MouseEventArgs e)
		{
			if (NoChildren()) _leftPanelMouseLeaveTimer.Start();
		}

		private void LeftPanel_MouseEnter(object sender, MouseEventArgs e)
		{
			_leftPanelMouseLeaveTimer.Stop();
		}
		private void _leftPanelMouseLeaveTimer_Tick(object sender, EventArgs e)
		{
			if (NoChildren())
			{
				SlideCoverLeftStoryboard.Begin();
			}
			_leftPanelMouseLeaveTimer.Stop();
		}
		private bool NoChildren()
		{
			if (resetting) return false;
			List<Window> children = OwnedWindows.OfType<Window>().ToList();
			children.RemoveAll(win => win is ShadowWindow);
			return children.Count == 0;
		}

		#endregion

	}
}