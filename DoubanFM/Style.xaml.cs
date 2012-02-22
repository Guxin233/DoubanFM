﻿/*
 * Author : K.F.Storm
 * Email : yk000123 at sina.com
 * Website : http://www.kfstorm.com
 * */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DoubanFM
{
	public partial class Style : ResourceDictionary
	{

		private void Link_Click(object sender, RoutedEventArgs e)
		{
			// 在此处添加事件处理程序实现。
			Core.UrlHelper.OpenLink((string)((Button)sender).Tag);
		}

		/// <summary>
		/// 使控制面板切换时有渐变效果
		/// </summary>
		private void ControlPanelStyle_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (e.OriginalSource == sender)
			{
				((sender as FrameworkElement).Style.Resources["ControlPanelStoryboard"] as Storyboard).Begin(sender as FrameworkElement);
			}
		}
	}
}
