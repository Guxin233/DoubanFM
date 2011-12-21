﻿/*
 * Author : K.F.Storm
 * Email : yk000123 at sina.com
 * Website : http://www.kfstorm.com
 * */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Threading;

namespace DoubanFM.Core
{
	/// <summary>
	/// 提供音乐搜索的功能
	/// </summary>
	public class MusicSearch : DependencyObject
	{
		#region 依赖项属性

		public static readonly DependencyProperty IsSearchFinishedProperty = DependencyProperty.Register("IsSearchFinished", typeof(bool), typeof(MusicSearch), new PropertyMetadata(true));
		public static readonly DependencyProperty IsPreviousPageEnabledProperty = DependencyProperty.Register("IsPreviousPageEnabled", typeof(bool), typeof(MusicSearch));
		public static readonly DependencyProperty IsNextPageEnabledProperty = DependencyProperty.Register("IsNextPageEnabled", typeof(bool), typeof(MusicSearch));
		public static readonly DependencyProperty ShowNoResultHintProperty = DependencyProperty.Register("ShowNoResultHint", typeof(bool), typeof(MusicSearch), new PropertyMetadata(true));
		public static readonly DependencyProperty SearchResultProperty = DependencyProperty.Register("SearchResult", typeof(IEnumerable<SearchItem>), typeof(MusicSearch));
		
		#endregion

		#region 属性

		/// <summary>
		/// 获取一个值，该值指示是否搜索完成
		/// </summary>
		public bool IsSearchFinished
		{
			get { return (bool)GetValue(IsSearchFinishedProperty); }
			private set
			{
				if (IsSearchFinished != value)
				{
					bool last = IsSearchFinished;
					SetValue(IsSearchFinishedProperty, value);
					SetValue(ShowNoResultHintProperty, IsSearchFinished && (SearchResult == null || SearchResult.Count() == 0));
					if (value == true)
						RaiseSearchFinishedEvent();
				}
			}
		}
		/// <summary>
		/// 获取搜索结果
		/// </summary>
		public IEnumerable<SearchItem> SearchResult
		{
			get { return (IEnumerable<SearchItem>)GetValue(SearchResultProperty); }
			private set
			{
				SetValue(SearchResultProperty, value);
				SetValue(ShowNoResultHintProperty, IsSearchFinished && (SearchResult == null || SearchResult.Count() == 0));
			}
		}
		/// <summary>
		/// 获取上一页是否可用
		/// </summary>
		public bool IsPreviousPageEnabled { get { return (bool)GetValue(IsPreviousPageEnabledProperty); } }
		/// <summary>
		/// 获取下一页是否可用
		/// </summary>
		public bool IsNextPageEnabled { get { return (bool)GetValue(IsNextPageEnabledProperty); } }
		private string _previousPageLink;
		/// <summary>
		/// 上一页的链接
		/// </summary>
		private string PreviousPageLink
		{
			get { return _previousPageLink; }
			set
			{
				_previousPageLink = value;
				SetValue(IsPreviousPageEnabledProperty, !string.IsNullOrEmpty(_previousPageLink));
			}
		}
		private string _nextPageLink;
		/// <summary>
		/// 下一页的链接
		/// </summary>
		private string NextPageLink
		{
			get { return _nextPageLink; }
			set
			{
				_nextPageLink = value;
				SetValue(IsNextPageEnabledProperty, !string.IsNullOrEmpty(_nextPageLink));
			}
		}
		/// <summary>
		/// 是否显示没有搜索结果的提示
		/// </summary>
		public bool ShowNoResultHint
		{
			get { return (bool)GetValue(ShowNoResultHintProperty); }
		}
		/// <summary>
		/// 设置
		/// </summary>
		public Settings Settings { get; private set; }

		#endregion

		#region 成员变量

		/// <summary>
		/// 获取搜索文本
		/// </summary>
		string _searchText;
		/// <summary>
		/// 搜索第几页
		/// </summary>
		int _page;
		
		#endregion

		internal MusicSearch(Settings settings)
			:base()
		{
			Settings = settings;
		}

		#region 事件

		/// <summary>
		/// 当搜索完成时发生。
		/// </summary>
		public event EventHandler SearchFinished;
		/// <summary>
		/// 触发SearchFinished事件。
		/// </summary>
		private void RaiseSearchFinishedEvent()
		{
			if (SearchFinished != null)
				SearchFinished(this, EventArgs.Empty);
		}

		#endregion

		#region 搜索操作

		/// <summary>
		/// 开始新搜索
		/// </summary>
		/// <param name="text">搜索内容</param>
		public void StartSearch(string text)
		{
			_searchText = text;
			_page = 1;
			Search();
		}
		/// <summary>
		/// 上一页
		/// </summary>
		public void PreviousPage()
		{
			--_page;
			Search();
		}

		/// <summary>
		/// 下一页
		/// </summary>
		public void NextPage()
		{
			++_page;
			Search();
		}

		#endregion

		#region 成员方法

		/// <summary>
		/// 搜索
		/// </summary>
		/// <param name="text">搜索文本</param>
		/// <param name="start">Start值</param>
		void Search()
		{
			if (IsSearchFinished == false)return;
			SearchResult = null;
			IsSearchFinished = false;
			PreviousPageLink = null;
			NextPageLink = null;
			ThreadPool.QueueUserWorkItem(new WaitCallback((state)=>
				{
					Parameters parameters = new Parameters();
					parameters.Add("start", ((_page - 1) * 15).ToString());
					parameters.Add("search_text", _searchText);
					string url = ConnectionBase.ConstructUrlWithParameters("http://music.douban.com/subject_search", parameters);
					string file = new ConnectionBase().Get(url);
					var searhResult = GetSearchItems(file);
					var previous = GetPreviousPageLink(file);
					var next = GetNextPageLink(file);
					Dispatcher.Invoke(new Action(() =>
						{
							SearchResult = searhResult;
							PreviousPageLink = previous;
							NextPageLink = next;
							IsSearchFinished = true;
						}));
				}));
		}
		/// <summary>
		/// 获取搜索的结果
		/// </summary>
		/// <returns></returns>
		private IEnumerable<SearchItem> GetSearchItems(string file)
		{
			List<SearchItem> items = new List<SearchItem>();
			try
			{
				bool isSearchFilterEnabled = true;
				Dispatcher.Invoke(new Action(() => { isSearchFilterEnabled = Settings.IsSearchFilterEnabled; }));

				//找出艺术家
				MatchCollection mc = Regex.Matches(file, @"<div class=\""result-item musician\"".*?>.*?</h3>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
				foreach(Match mm in mc)
				{
					string temp = mm.Groups[0].Value;
					string titleTemp = Regex.Match(temp, @"<a.*?class=\""nbg\"".*?/?>", RegexOptions.IgnoreCase | RegexOptions.Singleline).Groups[0].Value;
					string title = Regex.Match(titleTemp, @".*?title=\""([^\""]+)\""", RegexOptions.IgnoreCase | RegexOptions.Singleline).Groups[1].Value;
					string link = Regex.Match(titleTemp, @".*?href=\""([^\""]+)\""", RegexOptions.IgnoreCase | RegexOptions.Singleline).Groups[1].Value;
					string pictureTemp = Regex.Match(temp, @"<img.*?class=\""answer_pic\"".*?/?>", RegexOptions.IgnoreCase | RegexOptions.Singleline).Groups[0].Value;
					string picture = Regex.Match(pictureTemp, @".*?src=\""([^\""]+)\""", RegexOptions.IgnoreCase | RegexOptions.Singleline).Groups[1].Value;
					Match ma = Regex.Match(temp, @".*?href=\""http://douban\.fm/\?context=([^\""]+)\""", RegexOptions.IgnoreCase | RegexOptions.Singleline);
					string context = null;
					if (ma != null) context = ma.Groups[1].Value;
					SearchItem item = new SearchItem(title, picture, link, null, true, context);
					if (!isSearchFilterEnabled || !string.IsNullOrEmpty(item.Context))
						items.Add(item);
				}

				//找出专辑
				mc = Regex.Matches(file, @"<tr.*?class=\""item\"">.*?</tr>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
				foreach (Match mm in mc)
				{
					string temp = mm.Groups[0].Value;
					string titleTemp = Regex.Match(temp, @"<a.*?class=\""nbg\"".*?/?>", RegexOptions.IgnoreCase | RegexOptions.Singleline).Groups[0].Value;
					string title = Regex.Match(titleTemp, @".*?title=\""([^\""]+)\""", RegexOptions.IgnoreCase | RegexOptions.Singleline).Groups[1].Value;
					string link = Regex.Match(titleTemp, @".*?href=\""([^\""]+)\""", RegexOptions.IgnoreCase | RegexOptions.Singleline).Groups[1].Value;
					string pictureTemp = Regex.Match(temp, @"<img.*?/?>", RegexOptions.IgnoreCase | RegexOptions.Singleline).Groups[0].Value;
					string picture = Regex.Match(pictureTemp, @".*?src=\""([^\""]+)\""", RegexOptions.IgnoreCase | RegexOptions.Singleline).Groups[1].Value;
					Match ma = Regex.Match(temp, @".*?href=\""http://douban\.fm/\?context=([^\""]+)\""", RegexOptions.IgnoreCase | RegexOptions.Singleline);
					string context = null;
					if (ma != null) context = ma.Groups[1].Value;
				
					SearchItem item = new SearchItem(title, picture, link, null, false, context);
					if (!isSearchFilterEnabled || !string.IsNullOrEmpty(item.Context))
						items.Add(item);
				}
			}
			catch { }

			return items;
		}
		/// <summary>
		/// 获取上一页的链接
		/// </summary>
		/// <returns></returns>
		private string GetPreviousPageLink(string file)
		{
			try
			{
				Match mc = Regex.Match(file, @"<span class=\""prev\"">.*?</span>", RegexOptions.IgnoreCase | RegexOptions.Multiline);
				Match mc2 = Regex.Match(mc.Groups[0].Value, @"<a href=\""([^\""]+)\""", RegexOptions.IgnoreCase | RegexOptions.Multiline);
				return mc2.Groups[1].Value;
			}
			catch { }
			return null;
		}
		/// <summary>
		/// 获取下一页的链接
		/// </summary>
		/// <returns></returns>
		private string GetNextPageLink(string file)
		{
			try
			{
				Match mc = Regex.Match(file, @"<span class=\""next\"">.*?</span>", RegexOptions.IgnoreCase | RegexOptions.Multiline);
				Match mc2 = Regex.Match(mc.Groups[0].Value, @"<a href=\""([^\""]+)\""", RegexOptions.IgnoreCase | RegexOptions.Multiline);
				return mc2.Groups[1].Value;
			}
			catch { }
			return null;
		}

		#endregion
	}
}
