using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using StickyTickets.Data;

namespace StickyTickets.Services
{
    public static class LocalAiAndOcrService
    {
        private const string DefaultLoopbackBaseUrl = "http://127.0.0.1:11434";
        private const int MaxOllamaResponseChars = 65536;
        private static readonly TimeSpan SafeRegexTimeout = TimeSpan.FromMilliseconds(250);

        /// <summary>
        /// Resolves the local Ollama generate endpoint, strictly enforcing loopback-only IP/localhost
        /// to prevent SSRF or accidental exfiltration if STICKY_TICKETS_OLLAMA_LOOPBACK_URL is set.
        /// </summary>
        public static bool TryResolveSafeLoopbackGenerateUri(string candidateBaseUrl, out Uri generateUri)
        {
            generateUri = null;
            string rawBase = string.IsNullOrWhiteSpace(candidateBaseUrl)
                ? Environment.GetEnvironmentVariable("STICKY_TICKETS_OLLAMA_LOOPBACK_URL")
                : candidateBaseUrl;
            if (string.IsNullOrWhiteSpace(rawBase))
            {
                rawBase = DefaultLoopbackBaseUrl;
            }

            Uri baseUri;
            if (!Uri.TryCreate(rawBase.Trim(), UriKind.Absolute, out baseUri))
                return false;
            if (!string.Equals(baseUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
                return false;
            if (!string.IsNullOrEmpty(baseUri.UserInfo) || !string.IsNullOrEmpty(baseUri.Query) || !string.IsNullOrEmpty(baseUri.Fragment))
                return false;

            bool isLoopbackHost = false;
            IPAddress ip;
            if (IPAddress.TryParse(baseUri.DnsSafeHost, out ip))
            {
                isLoopbackHost = IPAddress.IsLoopback(ip);
            }
            else if (string.Equals(baseUri.DnsSafeHost, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                isLoopbackHost = true;
            }

            if (!isLoopbackHost || baseUri.Port <= 0 || baseUri.Port > 65535)
                return false;

            UriBuilder ub = new UriBuilder(Uri.UriSchemeHttp, baseUri.Host, baseUri.Port, "/api/generate");
            generateUri = ub.Uri;
            return true;
        }

        // Built-in & user-customizable recipe macros: Keyword -> Fixed workflow steps
        private static readonly Dictionary<string, string[]> RecipeMacros = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
#if CLEAN_RELEASE
            {
                "周报",
                new[]
                {
                    "[准]整理本周数据",
                    "[主]梳理核心进展",
                    "[主]列出下周计划",
                    "[沟]检查并发送汇报"
                }
            },
            {
                "设计",
                new[]
                {
                    "[准]收集参考素材",
                    "[主]完成初稿制作",
                    "[急]细节自检校对",
                    "[沟]导出成品交付"
                }
            }
#else
            {
                "特效",
                new[]
                {
                    "[准]准备参考图",
                    "[沟]和ai沟通理解需求",
                    "[主]提取描述词",
                    "[准]准备去生图",
                    "[主]生图中",
                    "[急]出图检查中",
                    "[准]上传云端",
                    "[沟]已交付"
                }
            },
            {
                "口播",
                new[]
                {
                    "[准]准备参考图",
                    "[沟]和ai沟通理解需求",
                    "[主]提取描述词",
                    "[准]准备去生图",
                    "[主]生图中",
                    "[急]出图检查中",
                    "[准]上传云端",
                    "[沟]已交付"
                }
            },
            {
                "换脸",
                new[]
                {
                    "[准]下载原视频",
                    "[准]打开软件",
                    "[主]开始换脸",
                    "[急]验收上传",
                    "[沟]交付"
                }
            }
#endif
        };

        public static void SyncRecipesFromDatabase(List<RecipeRecord> records)
        {
            if (records == null) return;
            RecipeMacros.Clear();
            foreach (RecipeRecord r in records)
            {
                if (string.IsNullOrWhiteSpace(r.Keywords) || string.IsNullOrWhiteSpace(r.StepsText))
                    continue;

                string[] stepParts = r.StepsText.Split(new[] { '、', '，', ',', '；', ';', '|', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                List<string> cleanedSteps = new List<string>();
                foreach (string sp in stepParts)
                {
                    string s = sp.Trim();
                    if (s.Length > 0) cleanedSteps.Add(s);
                }
                if (cleanedSteps.Count == 0) continue;

                // Support multiple keywords separated by 、 , ， ； ; or space
                string[] keys = r.Keywords.Split(new[] { '、', '，', ',', '；', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string k in keys)
                {
                    string cleanKey = k.Trim();
                    if (cleanKey.Length > 0)
                    {
                        RecipeMacros[cleanKey] = cleanedSteps.ToArray();
                    }
                }
            }
        }

        public static string ExportRecipesAsText()
        {
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<string, string[]> kv in RecipeMacros)
            {
                sb.Append(kv.Key).Append("=");
                for (int i = 0; i < kv.Value.Length; i++)
                {
                    if (i > 0) sb.Append("、");
                    sb.Append(Regex.Replace(kv.Value[i], @"^\[.\]", ""));
                }
                sb.AppendLine();
            }
            return sb.ToString().TrimEnd();
        }

        public static void ImportRecipesFromText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                int eq = line.IndexOf('=');
                if (eq <= 0) eq = line.IndexOf('：');
                if (eq <= 0) eq = line.IndexOf(':');
                if (eq <= 0) continue;

                string key = line.Substring(0, eq).Trim();
                string val = line.Substring(eq + 1).Trim();
                if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(val)) continue;

                string[] parts = val.Split(new[] { '、', '，', ',', '；', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    List<string> cleaned = new List<string>();
                    foreach (string p in parts)
                    {
                        string s = p.Trim();
                        if (s.Length > 0) cleaned.Add(s);
                    }
                    if (cleaned.Count > 0)
                    {
                        RecipeMacros[key] = cleaned.ToArray();
                    }
                }
            }
        }

        /// <summary>
        /// Checks if the user's input contains a registered Recipe Keyword (such as "周报" or "特效").
        /// Example input: "29号需求，项目周报" or "29号需求 项目周报 https://docs.example.com/..."
        /// Automatically sets the ticket title to the demand phrase and expands the fixed workflow steps!
        /// </summary>
        public static bool TryExpandRecipeMacro(
            string rawInput,
            out string autoTitle,
            out string extractedNotesUrl,
            out List<TicketStepRecord> expandedSteps)
        {
            autoTitle = null;
            extractedNotesUrl = null;
            expandedSteps = null;
            if (string.IsNullOrWhiteSpace(rawInput)) return false;

            foreach (KeyValuePair<string, string[]> kv in RecipeMacros)
            {
                if (rawInput.IndexOf(kv.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // Extract any URLs so the title stays crisp and the URL is preserved
                    string textWithoutUrl = rawInput;
                    Match urlMatch = Regex.Match(rawInput, @"https?://\S+", RegexOptions.IgnoreCase, SafeRegexTimeout);
                    if (urlMatch.Success)
                    {
                        extractedNotesUrl = urlMatch.Value;
                        textWithoutUrl = rawInput.Remove(urlMatch.Index, urlMatch.Length).Trim();
                    }

                    string cleanTitle = textWithoutUrl.Trim(' ', '，', ',', '；', ';', '。', '#');
                    if (string.IsNullOrEmpty(cleanTitle)) cleanTitle = kv.Key + "任务";
                    if (cleanTitle.Length > 40) cleanTitle = cleanTitle.Substring(0, 40);
                    autoTitle = cleanTitle;

                    expandedSteps = new List<TicketStepRecord>();
                    for (int i = 0; i < kv.Value.Length; i++)
                    {
                        List<TicketStepRecord> single = SplitLinesIntoValidatedSteps(kv.Value[i]);
                        if (single.Count > 0)
                        {
                            TicketStepRecord st = single[0];
                            st.SortOrder = expandedSteps.Count;
                            // If a reference URL was pasted on the same line, attach it to Step 1 (准备参考图) as well!
                            if (i == 0 && !string.IsNullOrEmpty(extractedNotesUrl))
                            {
                                st.Content = st.Content + " (" + extractedNotesUrl + ")";
                            }
                            expandedSteps.Add(st);
                        }
                    }
                    return expandedSteps.Count > 0;
                }
            }
            return false;
        }

        /// <summary>
        /// Parses natural and command time formats from input:
        /// - "/time 8h", "time 8h", "8h", "8hr", "8小时"
        /// - "/time 30m", "time 30m", "30m", "30min", "30分钟", "30分"
        /// - "/time 45s", "time 45s", "45s", "45秒"
        /// Strips the matched time token from remainingText so titles stay clean.
        /// </summary>
        public static bool TryParseQuickTimeCommand(string input, out int durationSeconds, out string remainingText)
        {
            durationSeconds = 0;
            remainingText = input ?? string.Empty;
            if (string.IsNullOrWhiteSpace(input)) return false;

            Match m = Regex.Match(
                input,
                @"(?:(?:/time|\btime)\s+)?(\d+(?:\.\d+)?)\s*(h|hr|hrs|hour|hours|小时|个?小时|m|min|mins|minute|minutes|分钟|分|s|sec|secs|秒)(?:\b|$)",
                RegexOptions.IgnoreCase,
                SafeRegexTimeout);

            if (!m.Success) return false;

            double val;
            if (!double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out val) || val <= 0)
                return false;

            string unit = m.Groups[2].Value.ToLowerInvariant();
            if (unit.StartsWith("h") || unit.Contains("时"))
                durationSeconds = (int)Math.Min(val * 3600, 30 * 86400);
            else if (unit.StartsWith("s") || unit.Contains("秒"))
                durationSeconds = (int)Math.Max(10, val);
            else
                durationSeconds = (int)Math.Min(val * 60, 30 * 86400);

            remainingText = input.Remove(m.Index, m.Length).Trim();
            return true;
        }

        /// <summary>
        /// Parses "图片数量：15", "图片数量: 15", "图片数量15", "数量：15", "张数：15", or "15张"
        /// as the exact point value for this ticket, and strips it from remainingText.
        /// </summary>
        public static bool TryParseImageCountPoints(string input, out int points, out string remainingText)
        {
            points = 0;
            remainingText = input ?? string.Empty;
            if (string.IsNullOrWhiteSpace(input)) return false;

            Match m = Regex.Match(
                input,
                @"(?:图片数量|图片张数|出图数量|数量|张数|积分)\s*[:：]?\s*(\d+)|(\d+)\s*张(?:\b|$)",
                RegexOptions.IgnoreCase,
                SafeRegexTimeout);

            if (!m.Success) return false;

            string numStr = !string.IsNullOrEmpty(m.Groups[1].Value) ? m.Groups[1].Value : m.Groups[2].Value;
            int val;
            if (!int.TryParse(numStr, out val) || val < 0) return false;

            points = val;
            remainingText = input.Remove(m.Index, m.Length).Trim(' ', '，', ',', '；', ';', '。');
            return true;
        }

        public static bool TryExtractTitleDirective(string input, out string newTitle, out string remainingText)
        {
            newTitle = null;
            remainingText = input ?? string.Empty;
            if (string.IsNullOrWhiteSpace(input)) return false;

            Match m = Regex.Match(input, @"^\s*(?:#|/title\s+)([^；;。\r\n\|]+)[；;。\r\n\|]?\s*", RegexOptions.None, SafeRegexTimeout);
            if (!m.Success) return false;

            string candidate = m.Groups[1].Value.Trim();
            if (candidate.Length > 0 && !Regex.IsMatch(candidate, @"^\d+$", RegexOptions.None, SafeRegexTimeout))
            {
                newTitle = candidate.Length > 40 ? candidate.Substring(0, 40) : candidate;
                remainingText = input.Substring(m.Length).Trim();
                return true;
            }
            return false;
        }

        public static List<TicketStepRecord> ConvertScratchToStepsSafely(string scratchNotes, out string statusHint)
        {
            statusHint = "";
            if (string.IsNullOrWhiteSpace(scratchNotes))
            {
                statusHint = "输入内容为空，请先写单。";
                return new List<TicketStepRecord>();
            }

            string autoTitle;
            string urlNote;
            List<TicketStepRecord> macroSteps;
            if (TryExpandRecipeMacro(scratchNotes, out autoTitle, out urlNote, out macroSteps))
            {
                statusHint = string.Format("识别到固定配方触发词，已自动展开「{0}」全套 {1} 道工序！", autoTitle, macroSteps.Count);
                return macroSteps;
            }

            try
            {
                Uri loopbackUri;
                if (TryResolveSafeLoopbackGenerateUri(null, out loopbackUri))
                {
                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(loopbackUri);
                    req.Proxy = null; // Never route local note text through system/WPAD HTTP proxies
                    req.Method = "POST";
                    req.ContentType = "application/json";
                    req.Timeout = 1500;
                    req.ReadWriteTimeout = 1500;
                    req.AllowAutoRedirect = false;

                    StringBuilder sb = new StringBuilder();
                    string truncated = scratchNotes.Length > 500 ? scratchNotes.Substring(0, 500) : scratchNotes;
                    foreach (char ch in truncated)
                    {
                        if (ch == '\\') sb.Append("\\\\");
                        else if (ch == '\"') sb.Append("\\\"");
                        else if (ch < 0x20) sb.Append(' ');
                        else sb.Append(ch);
                    }
                    string escapedPrompt = sb.ToString();

                    string payload = "{\"model\":\"qwen2.5:3b\",\"stream\":false,\"prompt\":\"拆分为简短待办步骤(每行一条): " + escapedPrompt + "\"}";
                    byte[] bytes = Encoding.UTF8.GetBytes(payload);
                    using (Stream s = req.GetRequestStream())
                    {
                        s.Write(bytes, 0, bytes.Length);
                    }
                    using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                    using (Stream respStream = resp.GetResponseStream())
                    {
                        if (respStream != null)
                        {
                            using (StreamReader sr = new StreamReader(respStream, Encoding.UTF8))
                            {
                                char[] buf = new char[4096];
                                StringBuilder jsonSb = new StringBuilder();
                                int read;
                                while ((read = sr.Read(buf, 0, buf.Length)) > 0)
                                {
                                    if (jsonSb.Length + read > MaxOllamaResponseChars)
                                    {
                                        jsonSb.Append(buf, 0, MaxOllamaResponseChars - jsonSb.Length);
                                        break;
                                    }
                                    jsonSb.Append(buf, 0, read);
                                }
                                string json = jsonSb.ToString();
                                Match m = Regex.Match(json, "\"response\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.None, SafeRegexTimeout);
                                if (m.Success)
                                {
                                    string unescaped = Regex.Unescape(m.Groups[1].Value);
                                    List<TicketStepRecord> parsed = SplitLinesIntoValidatedSteps(unescaped);
                                    if (parsed.Count > 0)
                                    {
                                        statusHint = "已通过本地 Ollama 完成智能拆单。";
                                        return parsed;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
            }

            List<TicketStepRecord> localSteps = SplitLinesIntoValidatedSteps(scratchNotes);
            statusHint = string.Format("已按拆单规则自动拆解为 {0} 条工序步骤。", localSteps.Count);
            return localSteps;
        }

        public static List<TicketStepRecord> SplitLinesIntoValidatedSteps(string rawText)
        {
            List<TicketStepRecord> result = new List<TicketStepRecord>();
            if (string.IsNullOrWhiteSpace(rawText)) return result;

            string normalized = Regex.Replace(rawText, @"(?:^|\s+)(?:\d+[\.\、\)]|[①②③④⑤⑥⑦⑧])\s+", "\n", RegexOptions.None, SafeRegexTimeout);
            string[] clauses = Regex.Split(normalized, @"[\r\n；;。\|]+|->|➔", RegexOptions.None, SafeRegexTimeout);

            int order = 0;
            foreach (string c in clauses)
            {
                string trimmed = c.Trim();
                if (trimmed.Length < 1) continue;

                string explicitType = null;
                if (trimmed.StartsWith("[主]") || trimmed.StartsWith("[核]")) { explicitType = "core"; trimmed = trimmed.Substring(3).Trim(); }
                else if (trimmed.StartsWith("[准]") || trimmed.StartsWith("[备]")) { explicitType = "prep"; trimmed = trimmed.Substring(3).Trim(); }
                else if (trimmed.StartsWith("[沟]") || trimmed.StartsWith("[联]") || trimmed.StartsWith("@"))
                {
                    explicitType = "comm";
                    if (trimmed.StartsWith("[")) trimmed = trimmed.Substring(3).Trim();
                }
                else if (trimmed.StartsWith("[急]") || trimmed.StartsWith("[注]") || trimmed.StartsWith("!"))
                {
                    explicitType = "warn";
                    if (trimmed.StartsWith("[")) trimmed = trimmed.Substring(3).Trim();
                }

                if (trimmed.Length == 0) continue;
                if (trimmed.Length > 140) trimmed = trimmed.Substring(0, 140);

                string type = explicitType ?? InferStepType(trimmed);
                result.Add(new TicketStepRecord
                {
                    SortOrder = order++,
                    StepType = TicketRepository.ValidateStepType(type),
                    Content = trimmed,
                    IsCompleted = false
                });
                if (result.Count >= 16) break;
            }
            return result;
        }

        private static string InferStepType(string text)
        {
            if (Regex.IsMatch(text, "检查|注意|记得|千万|必须|前交|截止|别忘|加急|!|！", RegexOptions.None, SafeRegexTimeout)) return "warn";
            if (Regex.IsMatch(text, "沟通|交付|找|发|联系|签字|经理|主管|客户|群里|通知|抄送|回复|确认|对接|@", RegexOptions.None, SafeRegexTimeout)) return "comm";
            if (Regex.IsMatch(text, "参考图|准备|上传|打印|导出|核对|汇总|下载|搜集|查|整理|截图|备份", RegexOptions.None, SafeRegexTimeout)) return "prep";
            return "core";
        }
    }
}
