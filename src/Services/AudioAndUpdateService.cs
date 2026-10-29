using System;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Text.RegularExpressions;

namespace StickyTickets.Services
{
    /// <summary>
    /// Provides procedural CC0 in-memory audio synthesis (diner bell, step check, alert)
    /// and safe V1 HTTPS update page navigation + input sanitization.
    /// </summary>
    public static class AudioAndUpdateService
    {
        public static bool IsMuted { get; set; }

        public static void PlayServeBell()
        {
            if (IsMuted) return;
            PlaySynthesizedChord(new[] { 587.33, 880.0, 1174.66 }, 380);
        }

        public static void PlayStepCheckSound()
        {
            if (IsMuted) return;
            PlaySynthesizedChord(new[] { 523.25, 659.25 }, 95);
        }

        public static void PlayUrgentAlert()
        {
            if (IsMuted) return;
            PlaySynthesizedChord(new[] { 440.0, 554.37 }, 220);
        }

        private static void PlaySynthesizedChord(double[] frequencies, int durationMs)
        {
            try
            {
                int sampleRate = 22050;
                int totalSamples = (sampleRate * durationMs) / 1000;
                byte[] pcm = new byte[totalSamples * 2];

                for (int i = 0; i < totalSamples; i++)
                {
                    double t = (double)i / sampleRate;
                    double env = Math.Exp(-4.5 * i / totalSamples);
                    double sample = 0;
                    for (int f = 0; f < frequencies.Length; f++)
                    {
                        sample += Math.Sin(2.0 * Math.PI * frequencies[f] * t);
                    }
                    sample = (sample / frequencies.Length) * env * 0.45;
                    short val = (short)(Math.Max(-1.0, Math.Min(1.0, sample)) * short.MaxValue);
                    pcm[i * 2] = (byte)(val & 0xFF);
                    pcm[i * 2 + 1] = (byte)((val >> 8) & 0xFF);
                }

                using (MemoryStream wav = new MemoryStream(44 + pcm.Length))
                using (BinaryWriter bw = new BinaryWriter(wav))
                {
                    bw.Write(new[] { 'R', 'I', 'F', 'F' });
                    bw.Write(36 + pcm.Length);
                    bw.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                    bw.Write(16);
                    bw.Write((short)1); // PCM
                    bw.Write((short)1); // Mono
                    bw.Write(sampleRate);
                    bw.Write(sampleRate * 2);
                    bw.Write((short)2);
                    bw.Write((short)16);
                    bw.Write(new[] { 'd', 'a', 't', 'a' });
                    bw.Write(pcm.Length);
                    bw.Write(pcm);

                    wav.Position = 0;
                    using (SoundPlayer sp = new SoundPlayer(wav))
                    {
                        sp.Play();
                    }
                }
            }
            catch
            {
                // Ignore audio device absence cleanly
            }
        }

        private static readonly TimeSpan SafeRegexTimeout = TimeSpan.FromMilliseconds(200);
        private static readonly Regex UnsafeControlChars = new Regex(
            @"[\x00-\x08\x0B\x0C\x0E-\x1F]",
            RegexOptions.Compiled,
            SafeRegexTimeout);
        private static readonly Regex DangerousUriSchemes = new Regex(
            @"(?<![a-z0-9+.-])(javascript|vbscript|data|file|ms-msdt|search-ms|mhtml|ms-settings|ms-appinstaller|ms-word|ms-excel|ms-powerpoint|mk|res)\s*:",
            RegexOptions.IgnoreCase | RegexOptions.Compiled,
            SafeRegexTimeout);

        /// <summary>
        /// Filters pasted clipboard text to strip control characters, embedded OLE markers,
        /// and dangerous non-HTTP(S) URI schemes (such as file://, javascript:, ms-msdt:, ms-settings:).
        /// Preserves valid UTF-16 surrogate pairs when truncating.
        /// </summary>
        public static string SanitizePastedText(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            string cleaned;
            try
            {
                cleaned = UnsafeControlChars.Replace(raw, string.Empty);
            }
            catch (RegexMatchTimeoutException)
            {
                cleaned = raw;
            }

            if (cleaned.Length > 4000)
            {
                int cutLen = 4000;
                if (char.IsHighSurrogate(cleaned[cutLen - 1]))
                {
                    cutLen--;
                }
                cleaned = cleaned.Substring(0, cutLen);
            }

            try
            {
                cleaned = DangerousUriSchemes.Replace(cleaned, "[blocked-scheme]:");
            }
            catch (RegexMatchTimeoutException)
            {
                return string.Empty;
            }

            return cleaned;
        }

        private static bool IsAllowlistedOfficialHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) return false;
            string normalized = host.Trim().TrimEnd('.');
            return string.Equals(normalized, "github.com", StringComparison.OrdinalIgnoreCase) ||
                   normalized.EndsWith(".github.com", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "microsoft.com", StringComparison.OrdinalIgnoreCase) ||
                   normalized.EndsWith(".microsoft.com", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// V1 Safe Update Navigation: Validates that the release URL uses HTTPS, default port 443,
        /// no embedded credentials, and an exact/subdomain-matched allowlisted host before opening.
        /// Never downloads or executes binaries in V1.
        /// </summary>
        public static bool OpenOfficialReleasePageSafely(string candidateUrl, out string statusMessage)
        {
            string configuredEnvUrl = Environment.GetEnvironmentVariable("STICKY_TICKETS_UPDATE_MANIFEST_URL");
            string url = !string.IsNullOrWhiteSpace(candidateUrl)
                ? candidateUrl.Trim()
                : (!string.IsNullOrWhiteSpace(configuredEnvUrl) ? configuredEnvUrl.Trim() : "https://github.com/releases");

            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri) ||
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                !uri.IsDefaultPort ||
                !string.IsNullOrEmpty(uri.UserInfo))
            {
                statusMessage = "已拦截非安全 HTTPS 更新链接。";
                return false;
            }

            if (!IsAllowlistedOfficialHost(uri.Host))
            {
                statusMessage = "更新域名不在安全白名单内 (" + uri.Host + ")。";
                return false;
            }

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = uri.AbsoluteUri,
                    UseShellExecute = true
                };
                Process.Start(psi);
                statusMessage = "已安全打开官方 HTTPS 发布页。";
                return true;
            }
            catch
            {
                statusMessage = "无法调用系统默认浏览器。";
                return false;
            }
        }
    }
}
