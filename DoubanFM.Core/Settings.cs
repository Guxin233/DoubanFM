﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Runtime.Serialization;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace DoubanFM.Core
{
	/// <summary>
	/// 偏好设置
	/// </summary>
	[Serializable]
	public class Settings : DependencyObject, ISerializable
	{
		#region 依赖项属性

		public static readonly DependencyProperty UserProperty = DependencyProperty.Register("User", typeof(User), typeof(Settings));
		public static readonly DependencyProperty RememberPasswordProperty = DependencyProperty.Register("RememberPassword", typeof(bool), typeof(Settings));
		public static readonly DependencyProperty AutoLogOnNextTimeProperty = DependencyProperty.Register("AutoLogOnNextTime", typeof(bool), typeof(Settings), new PropertyMetadata(true));
		public static readonly DependencyProperty RememberLastChannelProperty = DependencyProperty.Register("RememberLastChannel", typeof(bool), typeof(Settings), new PropertyMetadata(true));
		public static readonly DependencyProperty LastChannelProperty = DependencyProperty.Register("LastChannel", typeof(Channel), typeof(Settings));
		public static readonly DependencyProperty IsMutedProperty = DependencyProperty.Register("IsMuted", typeof(bool), typeof(Settings));
		public static readonly DependencyProperty VolumeProperty = DependencyProperty.Register("Volume", typeof(double), typeof(Settings),new PropertyMetadata(1.0));
		public static readonly DependencyProperty SlideCoverWhenMouseMoveProperty = DependencyProperty.Register("SlideCoverWhenMouseMove", typeof(bool), typeof(Settings), new PropertyMetadata(true));
		public static readonly DependencyProperty IsShadowEnabledProperty = DependencyProperty.Register("IsShadowEnabled", typeof(bool), typeof(Settings));
		public static readonly DependencyProperty AlwaysShowNotifyIconProperty = DependencyProperty.Register("AlwaysShowNotifyIcon", typeof(bool), typeof(Settings));
		public static readonly DependencyProperty AutoUpdateProperty = DependencyProperty.Register("AutoUpdate", typeof(bool), typeof(Settings), new PropertyMetadata(true));
		public static readonly DependencyProperty LastTimeCheckUpdateProperty = DependencyProperty.Register("LastTimeCheckUpdate", typeof(DateTime), typeof(Settings), new PropertyMetadata(DateTime.MinValue));
		public static readonly DependencyProperty OpenAlbumInfoWhenClickCoverProperty = DependencyProperty.Register("OpenAlbumInfoWhenClickCover", typeof(bool), typeof(Settings), new PropertyMetadata(true));
		public static readonly DependencyProperty IsSearchFilterEnabledProperty = DependencyProperty.Register("IsSearchFilterEnabled", typeof(bool), typeof(Settings), new PropertyMetadata(true));
		
		#endregion

		/// <summary>
		/// 用户
		/// </summary>
		public User User
		{
			get { return (User)GetValue(UserProperty); }
			set { SetValue(UserProperty, value); }
		}
		/// <summary>
		/// 记住密码
		/// </summary>
		public bool RememberPassword
		{
			get { return (bool)GetValue(RememberPasswordProperty); }
			set { SetValue(RememberPasswordProperty, value); }
		}
		/// <summary>
		/// 下次自动登录
		/// </summary>
		public bool AutoLogOnNextTime
		{
			get { return (bool)GetValue(AutoLogOnNextTimeProperty); }
			set { SetValue(AutoLogOnNextTimeProperty, value); }
		}
		/// <summary>
		/// 记住最后播放的频道
		/// </summary>
		public bool RememberLastChannel
		{
			get { return (bool)GetValue(RememberLastChannelProperty); }
			set { SetValue(RememberLastChannelProperty, value); }
		}
		/// <summary>
		/// 最后播放的频道
		/// </summary>
		public Channel LastChannel
		{
			get { return (Channel)GetValue(LastChannelProperty); }
			set { SetValue(LastChannelProperty, value); }
		}
		/// <summary>
		/// 静音
		/// </summary>
		public bool IsMuted
		{
			get { return (bool)GetValue(IsMutedProperty); }
			set { SetValue(IsMutedProperty, value); }
		}
		/// <summary>
		/// 音量
		/// </summary>
		public double Volume
		{
			get { return (double)GetValue(VolumeProperty); }
			set { SetValue(VolumeProperty, value); }
		}
		/// <summary>
		/// 当鼠标移动到封面上时滑动封面
		/// </summary>
		public bool SlideCoverWhenMouseMove
		{
			get { return (bool)GetValue(SlideCoverWhenMouseMoveProperty); }
			set { SetValue(SlideCoverWhenMouseMoveProperty, value); }
		}
		/// <summary>
		/// 开启窗口阴影
		/// </summary>
		public bool IsShadowEnabled
		{
			get { return (bool)GetValue(IsShadowEnabledProperty); }
			set { SetValue(IsShadowEnabledProperty, value); }
		}
		/// <summary>
		/// 总是显示托盘图标
		/// </summary>
		public bool AlwaysShowNotifyIcon
		{
			get { return (bool)GetValue(AlwaysShowNotifyIconProperty); }
			set { SetValue(AlwaysShowNotifyIconProperty, value); }
		}
		/// <summary>
		/// 自动更新
		/// </summary>
		public bool AutoUpdate
		{
			get { return (bool)GetValue(AutoUpdateProperty); }
			set { SetValue(AutoUpdateProperty, value); }
		}
		/// <summary>
		/// 最后一次检查更新的时间
		/// </summary>
		public DateTime LastTimeCheckUpdate
		{
			get { return (DateTime)GetValue(LastTimeCheckUpdateProperty); }
			set { SetValue(LastTimeCheckUpdateProperty, value); }
		}
		/// <summary>
		/// 点击封面时打开专辑的豆瓣资料页面
		/// </summary>
		public bool OpenAlbumInfoWhenClickCover
		{
			get { return (bool)GetValue(OpenAlbumInfoWhenClickCoverProperty); }
			set { SetValue(OpenAlbumInfoWhenClickCoverProperty, value); }
		}
		/// <summary>
		/// 自动剔除搜索结果中无法收听的项目
		/// </summary>
		public bool IsSearchFilterEnabled
		{
			get { return (bool)GetValue(IsSearchFilterEnabledProperty); }
			set { SetValue(IsSearchFilterEnabledProperty, value); }
		}
		/// <summary>
		/// 数据保存文件夹
		/// </summary>
		private static string _dataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\K.F.Storm\豆瓣电台\";

		internal Settings(User user)
		{
			User = user;
		}
		internal Settings(string username, string password)
			: this(new User(username, password)) { }
		internal Settings()
			: this("", "") { }

		public Settings(SerializationInfo info, StreamingContext context)
		{
			Settings def = new Settings();
			try
			{
				User = (User)info.GetValue("User", typeof(User));
			}
			catch
			{
				User = def.User;
			}
			try
			{
				RememberPassword = info.GetBoolean("RememberPassword");
			}
			catch
			{
				RememberPassword = def.RememberPassword;
			}
			try
			{
				AutoLogOnNextTime = info.GetBoolean("AutoLogOnNextTime");
			}
			catch
			{
				AutoLogOnNextTime = def.AutoLogOnNextTime;
			}
			try
			{
				RememberLastChannel = info.GetBoolean("RememberLastChannel");
			}
			catch
			{
				RememberLastChannel = def.RememberLastChannel;
			}
			try
			{
				LastChannel = (Channel)info.GetValue("LastChannel", typeof(Channel));
			}
			catch
			{
				LastChannel = def.LastChannel;
			}
			try
			{
				IsMuted = info.GetBoolean("IsMuted");
			}
			catch
			{
				IsMuted = def.IsMuted;
			}
			try
			{
				Volume = info.GetDouble("Volume");
			}
			catch
			{
				Volume = def.Volume;
			}
			try
			{
				SlideCoverWhenMouseMove = info.GetBoolean("SlideCoverWhenMouseMove");
			}
			catch
			{
				SlideCoverWhenMouseMove = def.SlideCoverWhenMouseMove;
			}
			try
			{
				IsShadowEnabled = info.GetBoolean("IsShadowEnabled");
			}
			catch
			{
				IsShadowEnabled = def.IsShadowEnabled;
			}
			try
			{
				AlwaysShowNotifyIcon = info.GetBoolean("AlwaysShowNotifyIcon");
			}
			catch
			{
				AlwaysShowNotifyIcon = def.AlwaysShowNotifyIcon;
			}
			try
			{
				AutoUpdate = info.GetBoolean("AutoUpdate");
			}
			catch
			{
				AutoUpdate = def.AutoUpdate;
			}
			try
			{
				LastTimeCheckUpdate = info.GetDateTime("LastTimeCheckUpdate");
			}
			catch
			{
				LastTimeCheckUpdate = def.LastTimeCheckUpdate;
			}
			try
			{
				OpenAlbumInfoWhenClickCover = info.GetBoolean("OpenAlbumInfoWhenClickCover");
			}
			catch
			{
				OpenAlbumInfoWhenClickCover = def.OpenAlbumInfoWhenClickCover;
			}
			try
			{
				IsSearchFilterEnabled = info.GetBoolean("IsSearchFilterEnabled");
			}
			catch
			{
				IsSearchFilterEnabled = def.IsSearchFilterEnabled;
			}
		}

		public void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue("User", User);
			info.AddValue("RememberPassword", RememberPassword);
			info.AddValue("AutoLogOnNextTime", AutoLogOnNextTime);
			info.AddValue("RememberLastChannel", RememberLastChannel);
			info.AddValue("LastChannel", LastChannel);
			info.AddValue("IsMuted", IsMuted);
			info.AddValue("Volume", Volume);
			info.AddValue("SlideCoverWhenMouseMove", SlideCoverWhenMouseMove);
			info.AddValue("IsShadowEnabled", IsShadowEnabled);
			info.AddValue("AlwaysShowNotifyIcon", AlwaysShowNotifyIcon);
			info.AddValue("AutoUpdate", AutoUpdate);
			info.AddValue("LastTimeCheckUpdate", LastTimeCheckUpdate);
			info.AddValue("OpenAlbumInfoWhenClickCover", OpenAlbumInfoWhenClickCover);
			info.AddValue("IsSearchFilterEnabled", IsSearchFilterEnabled);
		}

		/// <summary>
		/// 读取设置
		/// </summary>
		internal static Settings Load()
		{
			Settings settings = null;
			try
			{
				using (FileStream stream = File.OpenRead(_dataFolder + "Settings.dat"))
				{
					BinaryFormatter formatter = new BinaryFormatter();
					settings = (Settings)formatter.Deserialize(stream);
				}
				settings.User.Password = Encryption.Decrypt(settings.User.Password);
			}
			catch
			{
				settings = new Settings();
			}
			return settings;
		}
		/// <summary>
		/// 保存设置
		/// </summary>
		internal void Save()
		{
			string tempPassword = User.Password;
			if (!RememberPassword)
				User.Password = "";
			Channel tempLastChannel = LastChannel;
			if (!RememberLastChannel) LastChannel = null;
			try
			{
				User.Password = Encryption.Encrypt(User.Password);
				if (!Directory.Exists(_dataFolder))
					Directory.CreateDirectory(_dataFolder);
				using (FileStream stream = File.OpenWrite(_dataFolder + "Settings.dat"))
				{
					BinaryFormatter formatter = new BinaryFormatter();
					formatter.Serialize(stream, this);
				}
			}
			catch { }
			User.Password = tempPassword;
			LastChannel = tempLastChannel;
		}
	}
}
