﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace JKChat.Core.Helpers {
	public static class ColourTextHelper {
		public static string CleanString(string value, List<AttributeData<int>> colorAttributes = null, List<AttributeData<Uri>> uriAttributes = null) {
			if (string.IsNullOrEmpty(value)) {
				return string.Empty;
			}
			int colorLength = 0;
			var stringBuilder = new StringBuilder();
			var uriStringBuilder = new StringBuilder();
			for (int i = 0; i < value.Length; i++) {
				if (value[i] == '^' && i+1 < value.Length && (char.IsDigit(value[i+1]))) {
					colorLength = 0;
					if (colorAttributes != null) {
						colorAttributes.Add(new AttributeData<int>() {
							Start = stringBuilder.Length,
							Length = 0,
							Value = value[i+1] - '0'
						});
					}
					i++;
					uriStringBuilder.Clear();
					continue;
				}
				if (colorAttributes != null && colorAttributes.Count >= 1) {
					colorAttributes[colorAttributes.Count-1].Length = ++colorLength;
				}
				stringBuilder.Append(value[i]);
				if (uriAttributes != null) {
					if (value[i] != ' ') {
						uriStringBuilder.Append(value[i]);
					}
					if ((value[i] == ' ' || i+1 >= value.Length) && uriStringBuilder.Length > 0) {
						const string webUrls = @"(?:(?:https?|ftp|file):\/\/|www\.|ftp\.)(?:\([-A-Z0-9+&@#\/%=~_|$?!:,.]*\)|[-A-Za-z0-9+&@#\/%=~_|$?!:,.])*(?:\([-A-Za-z0-9+&@#\/%=~_|$?!:,.]*\)|[A-Za-z0-9+&@#\/%=~_|$])";
						const string schemeSep = "://";
						const string telScheme = "tel:";
						const string mailtoScheme = "mailto:";
						string uriStr = uriStringBuilder.ToString();
						var match = Regex.Match(uriStr, webUrls, RegexOptions.IgnoreCase | RegexOptions.Multiline);
						/* valid:
						 * www.site.com
						 * http(s)://site.com
						 * ftp://site.com
						 * file://site.com
						 * scheme://path
						 */
						if (match.Success
							|| (uriStr.Contains(schemeSep, StringComparison.OrdinalIgnoreCase) && !uriStr.StartsWith(schemeSep, StringComparison.OrdinalIgnoreCase))
							|| uriStr.StartsWith(telScheme, StringComparison.OrdinalIgnoreCase)
							|| uriStr.StartsWith(mailtoScheme, StringComparison.OrdinalIgnoreCase)) {
							if (!uriStr.Contains(schemeSep, StringComparison.OrdinalIgnoreCase)) {
								//site.com -> https://site.com
								uriStr = "https://" + uriStr;
							}
							if (Uri.TryCreate(uriStr, UriKind.Absolute, out Uri result)) {
								if (result.Scheme != "print" || value.IndexOf("^֎", StringComparison.Ordinal) < 0) {
									int startOffset = value[i] == ' ' ? 1 : 0;
									uriAttributes.Add(new AttributeData<Uri>() {
										Start = stringBuilder.Length - uriStringBuilder.Length - startOffset,
										Length = uriStringBuilder.Length,
										Value = result
									});
								}
							}
						}
						uriStringBuilder.Clear();
					}
				}
			}
			return stringBuilder.ToString();
		}
	}

	public class AttributeData<T> {
		public int Start { get; set; }
		public int Length { get; set; }
		public T Value { get; set; }
	}
}
