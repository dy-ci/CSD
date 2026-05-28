using Microsoft.UI.Text;
using Windows.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Text;


using CSD.Views;
using CSD.Models;
using CSD.Services;
using CSD.Helpers;
using CSD.Settings;


namespace CSD.Helpers
{
    /// <summary>
    /// Markdown/MFM 文本渲染器
    /// 支持的语法：
    ///   Markdown: 标题、粗体、斜体、删除线、代码、链接、列表、引用、代码块
    ///   MFM: 
    ///     - 提及(@user)、话题标签(#tag)
    ///     - 动画效果($[jelly,tada,bounce,spin,shake,twitch,jump,rainbow,sparkle,x2,x3,x4,scale,flip,blur])
    ///     - 样式($[ruby,font,fg.color,bg.color,rotate,position,border])
    ///     - 布局(<small>,<center>)
    ///     - 原样显示(<plain>)
    /// 优先级：Markdown > MFM（冲突时优先使用 Markdown 格式）
    /// </summary>
    internal static class MarkdownTextRenderer
    {
        #region 文本规范化

        /// <summary>
        /// 规范化存储文本，统一使用 \n 换行
        /// </summary>
        public static string NormalizeStorageText(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return text
                .Replace("\r\n", "\n")
                .Replace("\r", "\n");
        }

        /// <summary>
        /// 转换为编辑器文本，使用系统换行符
        /// </summary>
        public static string ToEditorText(string? text)
        {
            return NormalizeStorageText(text).Replace("\n", Environment.NewLine);
        }

        /// <summary>
        /// 获取纯文本（移除 Markdown 格式标记）
        /// </summary>
        public static string GetPlainText(string? text)
        {
            var normalized = NormalizeStorageText(text);
            if (string.IsNullOrWhiteSpace(normalized))
                return string.Empty;

            var result = new StringBuilder(normalized.Length);

            int i = 0;
            while (i < normalized.Length)
            {
                // 跳过代码块标记
                if (TrySkip(ref i, normalized, "```"))
                {
                    // 跳过整个代码块内容直到结束标记
                    while (i < normalized.Length && !TrySkip(ref i, normalized, "```"))
                    {
                        result.Append(normalized[i]);
                        i++;
                    }
                    continue;
                }

                // 跳过格式标记
                if (TrySkip(ref i, normalized, "**") ||
                    TrySkip(ref i, normalized, "__") ||
                    TrySkip(ref i, normalized, "~~") ||
                    TrySkip(ref i, normalized, "*") ||
                    TrySkip(ref i, normalized, "_") ||
                    TrySkip(ref i, normalized, "`"))
                {
                    continue;
                }

                // 跳过链接语法，保留文本
                if (normalized[i] == '[')
                {
                    int textEnd = normalized.IndexOf(']', i + 1);
                    if (textEnd > i + 1)
                    {
                        // 提取链接文本
                        result.Append(normalized.AsSpan(i + 1, textEnd - i - 1));
                        i = textEnd + 1;

                        // 跳过 URL 部分 (url)
                        if (i < normalized.Length && normalized[i] == '(')
                        {
                            int urlEnd = normalized.IndexOf(')', i + 1);
                            if (urlEnd > i)
                                i = urlEnd + 1;
                        }
                        continue;
                    }
                }

                // 跳过行首标记
                if (i == 0 || normalized[i - 1] == '\n')
                {
                    // 跳过标题标记
                    int headerLevel = 0;
                    while (i + headerLevel < normalized.Length && 
                           headerLevel < 6 && 
                           normalized[i + headerLevel] == '#')
                    {
                        headerLevel++;
                    }
                    if (headerLevel > 0 && i + headerLevel < normalized.Length && 
                        normalized[i + headerLevel] == ' ')
                    {
                        i += headerLevel + 1;
                        continue;
                    }

                    // 跳过引用标记
                    if (normalized[i] == '>')
                    {
                        i++;
                        if (i < normalized.Length && normalized[i] == ' ')
                            i++;
                        continue;
                    }

                    // 跳过列表标记
                    if (i + 1 < normalized.Length && 
                        (normalized[i] == '-' || normalized[i] == '*') && 
                        normalized[i + 1] == ' ')
                    {
                        i += 2;
                        continue;
                    }

                    // 跳过有序列表标记
                    if (char.IsDigit(normalized[i]))
                    {
                        int j = i;
                        while (j < normalized.Length && char.IsDigit(normalized[j]))
                            j++;
                        if (j < normalized.Length && normalized[j] == '.' && 
                            j + 1 < normalized.Length && normalized[j + 1] == ' ')
                        {
                            i = j + 2;
                            continue;
                        }
                    }
                }

                result.Append(normalized[i]);
                i++;
            }

            return result.ToString();
        }

        private static bool TrySkip(ref int index, string text, string marker)
        {
            if (index + marker.Length <= text.Length &&
                text.AsSpan(index, marker.Length).SequenceEqual(marker))
            {
                index += marker.Length;
                return true;
            }
            return false;
        }

        #endregion

        #region RichTextBlock 创建

        /// <summary>
        /// 创建 RichTextBlock 并渲染 Markdown 内容
        /// </summary>
        public static RichTextBlock CreateRichTextBlock(string? text, double fontSize, Brush? foreground = null)
        {
            var richTextBlock = new RichTextBlock
            {
                TextWrapping = TextWrapping.WrapWholeWords,
                IsTextSelectionEnabled = false,
                FontSize = fontSize
            };

            if (foreground is not null)
            {
                richTextBlock.Foreground = foreground;
            }

            var normalized = NormalizeStorageText(text);
            foreach (var paragraph in BuildParagraphs(normalized, fontSize))
            {
                richTextBlock.Blocks.Add(paragraph);
            }

            if (richTextBlock.Blocks.Count == 0)
            {
                var paragraph = new Paragraph();
                paragraph.Inlines.Add(new Run { Text = string.Empty });
                richTextBlock.Blocks.Add(paragraph);
            }

            return richTextBlock;
        }

        #endregion

        #region 段落构建

        private static IEnumerable<Paragraph> BuildParagraphs(string text, double fontSize)
        {
            var lines = text.Split('\n');
            var inCodeBlock = false;
            var inPlain = false;
            var inCenter = false;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i] ?? string.Empty;
                var trimmed = line.Trim();

                // 检测 <plain> 块
                if (trimmed.Equals("<plain>", StringComparison.OrdinalIgnoreCase))
                {
                    inPlain = true;
                    continue;
                }
                if (trimmed.Equals("</plain>", StringComparison.OrdinalIgnoreCase))
                {
                    inPlain = false;
                    continue;
                }

                // 检测 <center> 块
                if (trimmed.Equals("<center>", StringComparison.OrdinalIgnoreCase))
                {
                    inCenter = true;
                    continue;
                }
                if (trimmed.Equals("</center>", StringComparison.OrdinalIgnoreCase))
                {
                    inCenter = false;
                    continue;
                }

                // 检测代码块开始/结束
                if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    inCodeBlock = !inCodeBlock;
                    continue;
                }

                if (inCodeBlock)
                {
                    yield return BuildCodeParagraph(line, fontSize);
                    continue;
                }

                // 处理 <plain> 内容 - 原样显示，不解析任何语法
                if (inPlain)
                {
                    yield return BuildPlainParagraph(line, fontSize);
                    continue;
                }

                // 预处理单行 <center>...</center>：拆为居中段落
                var preprocessed = line;
                var centerOpenIdx = preprocessed.IndexOf("<center>", StringComparison.OrdinalIgnoreCase);
                var centerCloseIdx = preprocessed.IndexOf("</center>", StringComparison.OrdinalIgnoreCase);
                if (centerOpenIdx >= 0 && centerCloseIdx > centerOpenIdx)
                {
                    var before = preprocessed.Substring(0, centerOpenIdx);
                    var centerContent = preprocessed.Substring(centerOpenIdx + 8, centerCloseIdx - centerOpenIdx - 8);
                    var after = preprocessed.Substring(centerCloseIdx + 9);

                    if (!string.IsNullOrWhiteSpace(before))
                        yield return BuildParagraph(before, fontSize, null, null);
                    if (!string.IsNullOrWhiteSpace(centerContent))
                        yield return BuildParagraph(centerContent, fontSize, null, TextAlignment.Center);
                    if (!string.IsNullOrWhiteSpace(after))
                        yield return BuildParagraph(after, fontSize, null, null);
                    continue;
                }

                // 构建段落并应用块级样式
                TextAlignment? alignment = inCenter ? TextAlignment.Center : (TextAlignment?)null;
                var paragraph = BuildParagraph(line, fontSize, null, alignment);

                yield return paragraph;
            }
        }

        private static Paragraph BuildPlainParagraph(string line, double fontSize)
        {
            var paragraph = new Paragraph { Margin = new Thickness(0) };
            paragraph.Inlines.Add(new Run { Text = line });
            return paragraph;
        }

        private static Paragraph BuildParagraph(string line, double fontSize, Brush? foreground = null, TextAlignment? alignment = null)
        {
            var paragraph = new Paragraph { Margin = new Thickness(0) };
            if (alignment.HasValue)
                paragraph.TextAlignment = alignment.Value;

            if (string.IsNullOrEmpty(line))
            {
                var run = new Run { Text = " ", FontSize = fontSize };
                if (foreground != null) run.Foreground = foreground;
                paragraph.Inlines.Add(run);
                return paragraph;
            }

            // 标题
            int headerLevel = 0;
            int contentStart = 0;
            while (contentStart < line.Length && line[contentStart] == '#' && headerLevel < 6)
            {
                headerLevel++;
                contentStart++;
            }
            if (headerLevel > 0 && contentStart < line.Length && line[contentStart] == ' ')
            {
                contentStart++;
                paragraph.FontWeight = FontWeights.Bold;
                paragraph.FontSize = Math.Max(fontSize + (7 - headerLevel) * 2, fontSize + 2);
                AppendInlineContent(paragraph.Inlines, line.AsSpan(contentStart), fontSize, foreground);
                return paragraph;
            }

            // 引用
            if (line.StartsWith(">", StringComparison.Ordinal))
            {
                paragraph.Margin = new Thickness(16, 0, 0, 0);
                paragraph.FontStyle = Windows.UI.Text.FontStyle.Italic;
                var quoteContent = line.AsSpan(1).TrimStart(' ');
                AppendInlineContent(paragraph.Inlines, quoteContent, fontSize, foreground);
                return paragraph;
            }

            // 有序列表
            int digitEnd = 0;
            while (digitEnd < line.Length && char.IsDigit(line[digitEnd]))
                digitEnd++;
            if (digitEnd > 0 && digitEnd + 1 < line.Length && 
                line[digitEnd] == '.' && line[digitEnd + 1] == ' ')
            {
                var numberText = line.AsSpan(0, digitEnd + 2);
                var run = new Run { Text = numberText.ToString(), FontSize = fontSize };
                if (foreground != null) run.Foreground = foreground;
                paragraph.Inlines.Add(run);
                AppendInlineContent(paragraph.Inlines, line.AsSpan(digitEnd + 2), fontSize, foreground);
                return paragraph;
            }

            // 无序列表
            if (line.Length >= 2 && (line[0] == '-' || line[0] == '*') && line[1] == ' ')
            {
                var run = new Run { Text = "• ", FontSize = fontSize };
                if (foreground != null) run.Foreground = foreground;
                paragraph.Inlines.Add(run);
                AppendInlineContent(paragraph.Inlines, line.AsSpan(2), fontSize, foreground);
                return paragraph;
            }

            // 普通文本
            AppendInlineContent(paragraph.Inlines, line.AsSpan(), fontSize, foreground);
            return paragraph;
        }

        private static Paragraph BuildCodeParagraph(string line, double fontSize)
        {
            var paragraph = new Paragraph
            {
                Margin = new Thickness(12, 0, 0, 0),
                FontFamily = new FontFamily("Cascadia Mono"),
                FontSize = Math.Max(12, fontSize - 1)
            };

            paragraph.Inlines.Add(new Run { Text = string.IsNullOrEmpty(line) ? " " : line });
            return paragraph;
        }

        #endregion

        #region 内联内容解析

        private static void AppendInlineContent(InlineCollection inlines, ReadOnlySpan<char> text, double fontSize = 0, Brush? foreground = null)
        {
            if (text.IsEmpty)
            {
                inlines.Add(new Run { Text = string.Empty });
                return;
            }

            var sb = new StringBuilder();
            int i = 0;

            // 辅助方法：创建带默认样式的 Run
            Run MakeRun(string? t)
            {
                var run = new Run { Text = t ?? string.Empty };
                if (fontSize > 0) run.FontSize = fontSize;
                if (foreground != null) run.Foreground = foreground;
                return run;
            }

            while (i < text.Length)
            {
                // 尝试解析链接 [text](url)
                if (text[i] == '[')
                {
                    // 先输出累积的普通文本
                    if (sb.Length > 0)
                    {
                        inlines.Add(MakeRun(sb.ToString()));
                        sb.Clear();
                    }

                    if (TryParseLink(text, ref i, out string? linkText, out string? linkUrl))
                    {
                        if (Uri.TryCreate(linkUrl, UriKind.Absolute, out var uri))
                        {
                            var hyperlink = new Hyperlink { NavigateUri = uri };
                            hyperlink.Inlines.Add(MakeRun(linkText));
                            inlines.Add(hyperlink);
                        }
                        else
                        {
                            inlines.Add(MakeRun(linkText));
                        }
                        continue;
                    }
                    // 不是有效的链接，输出 [ 字符并继续
                    sb.Append('[');
                    i++;
                    continue;
                }

                // 尝试解析粗体 **text** 或 __text__
                if (TryParseDelimited(text, ref i, ref sb, inlines, "**", static s => s.FontWeight = FontWeights.Bold, fontSize, foreground))
                    continue;
                if (TryParseDelimited(text, ref i, ref sb, inlines, "__", static s => s.FontWeight = FontWeights.Bold, fontSize, foreground))
                    continue;

                // 尝试解析删除线 ~~text~~
                if (TryParseDelimited(text, ref i, ref sb, inlines, "~~", static s => s.TextDecorations = Windows.UI.Text.TextDecorations.Strikethrough, fontSize, foreground))
                    continue;

                // 尝试解析斜体 *text* 或 _text_
                if (TryParseDelimited(text, ref i, ref sb, inlines, "*", static s => s.FontStyle = Windows.UI.Text.FontStyle.Italic, fontSize, foreground))
                    continue;
                if (TryParseDelimited(text, ref i, ref sb, inlines, "_", static s => s.FontStyle = Windows.UI.Text.FontStyle.Italic, fontSize, foreground))
                    continue;

                // 尝试解析行内代码 `code`
                if (text[i] == '`')
                {
                    if (sb.Length > 0)
                    {
                        inlines.Add(MakeRun(sb.ToString()));
                        sb.Clear();
                    }

                    if (TryParseCode(text, ref i, out string? codeContent))
                    {
                        var span = new Span { FontFamily = new FontFamily("Cascadia Mono") };
                        span.Inlines.Add(MakeRun(codeContent));
                        inlines.Add(span);
                        continue;
                    }
                    // 不是有效的代码，输出 ` 字符并继续
                    sb.Append('`');
                    i++;
                    continue;
                }

                // 尝试解析自动链接 URL
                if (i + 7 < text.Length && 
                    (text.Slice(i, 7).SequenceEqual("http://".AsSpan()) ||
                     text.Slice(i, 8).SequenceEqual("https://".AsSpan())))
                {
                    if (sb.Length > 0)
                    {
                        inlines.Add(MakeRun(sb.ToString()));
                        sb.Clear();
                    }

                    if (TryParseUrl(text, ref i, out string? url))
                    {
                        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                        {
                            var hyperlink = new Hyperlink { NavigateUri = uri };
                            hyperlink.Inlines.Add(MakeRun(url));
                            inlines.Add(hyperlink);
                        }
                        else
                        {
                            inlines.Add(MakeRun(url));
                        }
                        continue;
                    }
                }

                // ========== MFM 语法解析（在 Markdown 之后，优先级较低）==========

                // MFM <small>...</small> 内联标签
                if (i + 6 < text.Length && text.Slice(i, 7).SequenceEqual("<small>".AsSpan()))
                {
                    if (sb.Length > 0)
                    {
                        inlines.Add(MakeRun(sb.ToString()));
                        sb.Clear();
                    }

                    int savedIndex = i;
                    i += 7; // 跳过 <small>
                    int contentStart = i;
                    int closeIndex = text.Slice(i).IndexOf("</small>".AsSpan());
                    if (closeIndex >= 0)
                    {
                        closeIndex += i;
                        var smallContent = text.Slice(contentStart, closeIndex - contentStart).ToString();
                        var smallSpan = new Span
                        {
                            FontSize = fontSize > 0 ? fontSize * 0.8 : 12,
                            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(180, 100, 100, 100))
                        };
                        AppendInlineContent(smallSpan.Inlines, smallContent.AsSpan(), fontSize * 0.8, smallSpan.Foreground);
                        inlines.Add(smallSpan);
                        i = closeIndex + 8; // 跳过 </small>
                        continue;
                    }
                    // 没有找到关闭标签，输出 < 并继续
                    i = savedIndex;
                    sb.Append('<');
                    i++;
                    continue;
                }

                // MFM 函数 $[effect params... content]
                if (text[i] == '$' && i + 1 < text.Length && text[i + 1] == '[')
                {
                    if (sb.Length > 0)
                    {
                        inlines.Add(MakeRun(sb.ToString()));
                        sb.Clear();
                    }

                    int savedIndex = i;
                    if (TryParseMfmFunction(text, ref i, out string? effectWithParams, out string? mfmContent))
                    {
                        // 处理 ruby 特殊语法 $[ruby 文字 注音]
                        if (effectWithParams != null && effectWithParams.StartsWith("ruby", StringComparison.OrdinalIgnoreCase))
                        {
                            var rubySpan = ParseMfmRuby(mfmContent);
                            if (rubySpan != null)
                            {
                                inlines.Add(rubySpan);
                                continue;
                            }
                        }

                        // 递归解析 MFM 函数内的内容
                        var span = new Span();
                        ApplyMfmEffectStyle(span, effectWithParams ?? string.Empty);
                        AppendInlineContent(span.Inlines, mfmContent.AsSpan());
                        inlines.Add(span);
                        continue;
                    }
                    // 解析失败，输出 $ 并继续
                    i = savedIndex;
                    sb.Append('$');
                    i++;
                    continue;
                }

                // MFM 提及 @username 或 @username@instance
                if (text[i] == '@')
                {
                    if (sb.Length > 0)
                    {
                        inlines.Add(MakeRun(sb.ToString()));
                        sb.Clear();
                    }

                    int savedIndex = i;
                    if (TryParseMfmMention(text, ref i, out string? mention))
                    {
                        // 提及显示为蓝色可点击样式
                        var mentionSpan = new Span
                        {
                            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 153, 188))
                        };
                        mentionSpan.Inlines.Add(new Run { Text = mention });
                        inlines.Add(mentionSpan);
                        continue;
                    }
                    // 解析失败，输出 @ 并继续
                    i = savedIndex;
                    sb.Append('@');
                    i++;
                    continue;
                }

                // MFM 话题标签 #tag
                if (text[i] == '#')
                {
                    if (sb.Length > 0)
                    {
                        inlines.Add(MakeRun(sb.ToString()));
                        sb.Clear();
                    }

                    int savedIndex = i;
                    if (TryParseMfmHashtag(text, ref i, out string? hashtag))
                    {
                        // 话题标签显示为蓝色可点击样式
                        var hashtagSpan = new Span
                        {
                            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 153, 188))
                        };
                        hashtagSpan.Inlines.Add(new Run { Text = hashtag });
                        inlines.Add(hashtagSpan);
                        continue;
                    }
                    // 解析失败，输出 # 并继续
                    i = savedIndex;
                    sb.Append('#');
                    i++;
                    continue;
                }

                // 普通字符
                sb.Append(text[i]);
                i++;
            }

            // 输出剩余的普通文本
            if (sb.Length > 0)
            {
                inlines.Add(MakeRun(sb.ToString()));
            }
        }

        private static bool TryParseLink(ReadOnlySpan<char> text, ref int index, out string? linkText, out string? linkUrl)
        {
            linkText = null;
            linkUrl = null;

            if (index >= text.Length || text[index] != '[')
                return false;

            // 查找 ] 结束文本部分
            int textEnd = text.Slice(index + 1).IndexOf(']');
            if (textEnd < 0)
                return false;
            textEnd += index + 1;

            // 检查后面是否有 (url)
            int urlStart = textEnd + 1;
            if (urlStart >= text.Length || text[urlStart] != '(')
                return false;

            // 查找 ) 结束 URL 部分
            int urlEnd = text.Slice(urlStart + 1).IndexOf(')');
            if (urlEnd < 0)
                return false;
            urlEnd += urlStart + 1;

            // 提取文本和 URL
            linkText = text.Slice(index + 1, textEnd - index - 1).ToString();
            linkUrl = text.Slice(urlStart + 1, urlEnd - urlStart - 1).ToString();

            index = urlEnd + 1;
            return true;
        }

        private static bool TryParseDelimited(
            ReadOnlySpan<char> text,
            ref int index,
            ref StringBuilder buffer,
            InlineCollection inlines,
            string delimiter,
            Action<Span> applyStyle,
            double fontSize = 0,
            Brush? foreground = null)
        {
            if (index + delimiter.Length > text.Length)
                return false;

            if (!text.Slice(index, delimiter.Length).SequenceEqual(delimiter.AsSpan()))
                return false;

            // 查找结束标记
            int contentStart = index + delimiter.Length;
            int end = text.Slice(contentStart).IndexOf(delimiter.AsSpan());
            if (end < 0)
                return false;
            end += contentStart;

            // 内容不能为空
            if (end == contentStart)
                return false;

            // 输出缓冲区内容
            if (buffer.Length > 0)
            {
                var run = new Run { Text = buffer.ToString() };
                if (fontSize > 0) run.FontSize = fontSize;
                if (foreground != null) run.Foreground = foreground;
                inlines.Add(run);
                buffer.Clear();
            }

            // 创建样式 span
            var span = new Span();
            applyStyle(span);
            span.Inlines.Add(new Run { Text = text.Slice(contentStart, end - contentStart).ToString() });
            inlines.Add(span);

            index = end + delimiter.Length;
            return true;
        }

        private static bool TryParseCode(ReadOnlySpan<char> text, ref int index, out string? codeContent)
        {
            codeContent = null;

            if (index >= text.Length || text[index] != '`')
                return false;

            // 查找结束 `
            int end = text.Slice(index + 1).IndexOf('`');
            if (end < 0)
                return false;
            end += index + 1;

            codeContent = text.Slice(index + 1, end - index - 1).ToString();
            index = end + 1;
            return true;
        }

        private static bool TryParseUrl(ReadOnlySpan<char> text, ref int index, out string? url)
        {
            url = null;

            int start = index;
            
            // 确认是 http:// 或 https:// 开头
            if (text.Slice(index).StartsWith("http://".AsSpan()))
                index += 7;
            else if (text.Slice(index).StartsWith("https://".AsSpan()))
                index += 8;
            else
                return false;

            // 读取 URL 直到遇到空白或结束
            while (index < text.Length && !char.IsWhiteSpace(text[index]))
            {
                index++;
            }

            url = text.Slice(start, index - start).ToString();
            return true;
        }

        /// <summary>
        /// 应用 MFM 效果样式（支持带参数的效果，如 spin.speed=5s,left, fg.color=f00）
        /// </summary>
        private static void ApplyMfmEffectStyle(Span span, string effectWithParams)
        {
            if (string.IsNullOrEmpty(effectWithParams))
                return;

            // 解析效果和参数
            // MFM 格式: effectName, effectName.param=value, param=value, modifier
            // 示例: "spin.speed=5s,left" → mainEffect=spin, params={speed=5s, left=true}
            // 示例: "fg.color=f00" → mainEffect=fg, params={color=f00}
            // 示例: "font.serif" → mainEffect=font, params={serif=true}
            // 示例: "x2" → mainEffect=x2, params={}
            var parts = effectWithParams.Split(',');
            string? mainEffect = null;
            var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                // 检查是否是 "key=value" 格式的参数
                var eqIndex = trimmed.IndexOf('=');
                if (eqIndex > 0)
                {
                    var key = trimmed.Substring(0, eqIndex).Trim();
                    var value = trimmed.Substring(eqIndex + 1).Trim();

                    // 如果 key 包含点号（如 fg.color=f00），提取效果名
                    if (mainEffect == null)
                    {
                        var dotInKey = key.IndexOf('.');
                        if (dotInKey > 0)
                        {
                            mainEffect = key.Substring(0, dotInKey);
                            var subKey = key.Substring(dotInKey + 1);
                            if (!string.IsNullOrEmpty(subKey))
                                parameters[subKey] = value;
                            continue;
                        }
                    }

                    parameters[key] = value;
                }
                else if (mainEffect == null)
                {
                    // 第一个无等号的项是效果名（可能带点号，如 font.serif）
                    mainEffect = trimmed;
                }
                else
                {
                    // 后续无等号的项是修饰符（如 left, right, alternate, x, y, serif 等）
                    parameters[trimmed] = "true";
                }
            }

            if (string.IsNullOrEmpty(mainEffect))
                return;

            // 提取效果名的基础部分（去掉点号后的参数前缀）
            // 例如 "font.serif" 整体是 mainEffect，需要提取 "font" 和 "serif"
            string baseEffect = mainEffect;
            var dotIndex = mainEffect.IndexOf('.');
            if (dotIndex > 0)
            {
                baseEffect = mainEffect.Substring(0, dotIndex);
                var subParam = mainEffect.Substring(dotIndex + 1);
                if (!string.IsNullOrEmpty(subParam))
                    parameters[subParam] = "true";
            }

            // 应用效果
            switch (baseEffect.ToLowerInvariant())
            {
                // 缩放效果
                case "x2":
                    span.FontSize = span.FontSize > 0 ? span.FontSize * 1.5 : fontSize * 1.5;
                    break;
                case "x3":
                    span.FontSize = span.FontSize > 0 ? span.FontSize * 2 : fontSize * 2;
                    break;
                case "x4":
                    span.FontSize = span.FontSize > 0 ? span.FontSize * 2.5 : fontSize * 2.5;
                    break;
                case "scale":
                    if (parameters.TryGetValue("x", out var scaleX) && double.TryParse(scaleX, out var sx))
                    {
                        span.FontSize = span.FontSize > 0 ? span.FontSize * sx : fontSize * sx;
                    }
                    else if (parameters.TryGetValue("y", out var scaleY) && double.TryParse(scaleY, out var sy))
                    {
                        span.FontSize = span.FontSize > 0 ? span.FontSize * sy : fontSize * sy;
                    }
                    else
                    {
                        span.FontSize = span.FontSize > 0 ? span.FontSize * 1.5 : fontSize * 1.5;
                    }
                    break;

                // 视觉效果
                case "blur":
                    span.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(128, 128, 128, 128));
                    break;
                case "flip":
                    // flip, flip.v, flip.h, flip.h,v 等
                    if (parameters.ContainsKey("v") && !parameters.ContainsKey("h"))
                        span.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(200, 100, 100, 200));
                    else if (parameters.ContainsKey("h") && !parameters.ContainsKey("v"))
                        span.FontStyle = Windows.UI.Text.FontStyle.Italic;
                    else
                        span.FontStyle = Windows.UI.Text.FontStyle.Italic;
                    break;

                // 动画效果（静态表示）
                case "tada":
                case "jelly":
                case "bounce":
                case "shake":
                case "twitch":
                case "jump":
                    span.FontWeight = FontWeights.Bold;
                    span.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 100, 100));
                    break;

                case "spin":
                    // spin, spin.left, spin.alternate, spin.x, spin.y 等
                    span.FontWeight = FontWeights.Bold;
                    if (parameters.ContainsKey("x"))
                        span.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 100, 150, 255));
                    else if (parameters.ContainsKey("y"))
                        span.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 100, 255, 150));
                    else
                        span.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 150, 100));
                    break;

                case "rainbow":
                    // 彩虹效果 - 使用渐变色模拟
                    span.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 128));
                    break;

                case "sparkle":
                    span.FontWeight = FontWeights.Bold;
                    span.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 215, 0));
                    break;

                // 字体
                case "font":
                    if (parameters.TryGetValue("serif", out _))
                        span.FontFamily = new FontFamily("Times New Roman, Georgia, serif");
                    else if (parameters.TryGetValue("monospace", out _))
                        span.FontFamily = new FontFamily("Consolas, Courier New, monospace");
                    else if (parameters.TryGetValue("cursive", out _))
                        span.FontFamily = new FontFamily("Comic Sans MS, cursive");
                    else if (parameters.TryGetValue("fantasy", out _))
                        span.FontFamily = new FontFamily("Impact, fantasy");
                    break;

                // 颜色
                case "fg":
                    if (parameters.TryGetValue("color", out var fgColor))
                        span.Foreground = ParseMfmColor(fgColor);
                    break;
                case "bg":
                    // 背景色在 Inline 上无法直接设置，使用高亮模拟
                    if (parameters.TryGetValue("color", out var bgColor))
                    {
                        span.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 0, 0));
                        // 注意：Span 没有 Background 属性，这里仅作标记
                    }
                    break;

                // 旋转
                case "rotate":
                    if (parameters.TryGetValue("deg", out var degStr) && double.TryParse(degStr, out var deg))
                    {
                        // 使用斜体模拟旋转效果
                        span.FontStyle = Windows.UI.Text.FontStyle.Italic;
                        span.FontWeight = FontWeights.Bold;
                    }
                    break;

                // 位置
                case "position":
                    // 位置调整在 Span 上无法实现，使用下划线标记
                    if (parameters.ContainsKey("x") || parameters.ContainsKey("y"))
                    {
                        span.TextDecorations = Windows.UI.Text.TextDecorations.Underline;
                    }
                    break;

                // 边框
                case "border":
                    // 边框在 Span 上无法实现，使用粗体和背景色模拟
                    span.FontWeight = FontWeights.Bold;
                    if (parameters.TryGetValue("color", out var borderColor))
                        span.Foreground = ParseMfmColor(borderColor);
                    break;

                // 注音
                case "ruby":
                    // ruby 特殊处理，在 ParseMfmFunctionWithArgs 中处理
                    break;
            }
        }

        /// <summary>
        /// 解析 MFM ruby 注音语法 $[ruby 文字 注音]
        /// </summary>
        private static Span? ParseMfmRuby(string? content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return null;

            var parts = content.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                return null;

            // 最后一部分是注音，前面的是文字
            var rubyText = parts[^1];
            var baseText = string.Join(" ", parts[0..^1]);

            var span = new Span();
            span.Inlines.Add(new Run { Text = baseText });
            // 注音在 RichTextBlock 中无法直接显示，用小括号代替
            span.Inlines.Add(new Run 
            { 
                Text = $"({rubyText})",
                FontSize = span.FontSize > 0 ? span.FontSize * 0.7 : 10
            });

            return span;
        }

        /// <summary>
        /// 解析 MFM 颜色代码（支持 3、4、6 位十六进制）
        /// </summary>
        private static SolidColorBrush ParseMfmColor(string? colorCode)
        {
            if (string.IsNullOrEmpty(colorCode))
                return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 0, 0));

            var code = colorCode.TrimStart('#');

            try
            {
                // 3 位颜色 #RGB
                if (code.Length == 3)
                {
                    var r = Convert.ToByte(new string(code[0], 2), 16);
                    var g = Convert.ToByte(new string(code[1], 2), 16);
                    var b = Convert.ToByte(new string(code[2], 2), 16);
                    return new SolidColorBrush(Windows.UI.Color.FromArgb(255, r, g, b));
                }
                // 4 位颜色 #ARGB
                else if (code.Length == 4)
                {
                    var a = Convert.ToByte(new string(code[0], 2), 16);
                    var r = Convert.ToByte(new string(code[1], 2), 16);
                    var g = Convert.ToByte(new string(code[2], 2), 16);
                    var b = Convert.ToByte(new string(code[3], 2), 16);
                    return new SolidColorBrush(Windows.UI.Color.FromArgb(a, r, g, b));
                }
                // 6 位颜色 #RRGGBB
                else if (code.Length == 6)
                {
                    var r = Convert.ToByte(code.Substring(0, 2), 16);
                    var g = Convert.ToByte(code.Substring(2, 2), 16);
                    var b = Convert.ToByte(code.Substring(4, 2), 16);
                    return new SolidColorBrush(Windows.UI.Color.FromArgb(255, r, g, b));
                }
            }
            catch { }

            return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 0, 0));
        }

        /// <summary>
        /// 尝试解析 MFM 提及 @username 或 @username@instance
        /// </summary>
        private static bool TryParseMfmMention(ReadOnlySpan<char> text, ref int index, out string? mention)
        {
            mention = null;

            if (index >= text.Length || text[index] != '@')
                return false;

            int start = index;
            index++; // 跳过 @

            // 读取用户名（允许字母、数字、下划线、连字符）
            int nameStart = index;
            while (index < text.Length && (char.IsLetterOrDigit(text[index]) || text[index] == '_' || text[index] == '-'))
            {
                index++;
            }

            if (index == nameStart)
                return false;

            // 检查是否有 @instance 部分
            if (index < text.Length && text[index] == '@')
            {
                index++;
                int instanceStart = index;
                while (index < text.Length && (char.IsLetterOrDigit(text[index]) || text[index] == '.' || text[index] == '-'))
                {
                    index++;
                }
                if (index == instanceStart)
                    return false;
            }

            mention = text.Slice(start, index - start).ToString();
            return true;
        }

        /// <summary>
        /// 尝试解析 MFM 话题标签 #tag
        /// </summary>
        private static bool TryParseMfmHashtag(ReadOnlySpan<char> text, ref int index, out string? hashtag)
        {
            hashtag = null;

            if (index >= text.Length || text[index] != '#')
                return false;

            // 确保不是 Markdown 标题（# 后面有空格且不在行首）
            if (index == 0 || text[index - 1] == '\n')
            {
                // 在行首，可能是标题，检查后面是否有空格
                int check = index + 1;
                while (check < text.Length && text[check] == '#')
                    check++;
                if (check < text.Length && text[check] == ' ')
                    return false; // 这是 Markdown 标题
            }

            int start = index;
            index++; // 跳过 #

            // 标签必须以字母或数字开头
            if (index >= text.Length || !char.IsLetterOrDigit(text[index]))
                return false;

            // 读取标签内容（允许字母、数字、下划线）
            while (index < text.Length && (char.IsLetterOrDigit(text[index]) || text[index] == '_'))
            {
                index++;
            }

            hashtag = text.Slice(start, index - start).ToString();
            return true;
        }

        /// <summary>
        /// 尝试解析 MFM 函数 $[effect params... content]
        /// 支持带参数的效果，如 $[spin.speed=5s,left content], $[fg.color=f00 text], $[ruby 漢字 かんじ]
        /// </summary>
        private static bool TryParseMfmFunction(ReadOnlySpan<char> text, ref int index, out string? effectWithParams, out string? content)
        {
            effectWithParams = null;
            content = null;

            if (index + 2 > text.Length || text[index] != '$' || text[index + 1] != '[')
                return false;

            index += 2; // 跳过 $[

            // 读取效果名称和参数（直到遇到空格或内容）
            int paramsStart = index;
            while (index < text.Length && text[index] != ']' && text[index] != '[')
            {
                // 遇到空格且已经有效果名了，说明参数结束
                if (char.IsWhiteSpace(text[index]) && index > paramsStart)
                {
                    // 检查后面是否还有内容（不是立即结束）
                    int check = index + 1;
                    while (check < text.Length && char.IsWhiteSpace(text[check]))
                        check++;
                    if (check < text.Length && text[check] != ']')
                        break; // 有空格后有内容，参数结束
                }
                index++;
            }

            if (index == paramsStart)
                return false;

            effectWithParams = text.Slice(paramsStart, index - paramsStart).ToString().Trim();

            // 跳过空格
            while (index < text.Length && char.IsWhiteSpace(text[index]))
            {
                index++;
            }

            // 查找结束 ]
            int contentStart = index;
            int bracketCount = 1;
            while (index < text.Length && bracketCount > 0)
            {
                if (text[index] == '[')
                    bracketCount++;
                else if (text[index] == ']')
                    bracketCount--;
                index++;
            }

            if (bracketCount > 0)
                return false;

            // 提取内容（不包括最后的 ]）
            content = text.Slice(contentStart, index - contentStart - 1).ToString();
            return true;
        }

        #endregion
    }
}
