using System;
using System.Collections.Generic;
using System.IO;
using StickyTickets.Data;
using StickyTickets.Services;

namespace StickyTickets.Tests
{
    public static class SelfTestRunner
    {
        public static int RunAllTests()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "StickyTickets_Test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string testDbPath = Path.Combine(tempDir, "test_sticky.db");

            try
            {
                Console.WriteLine("[1/5] Testing SQLite WAL, Foreign Keys, and SQL Injection parameterization...");
                using (TicketRepository repo = new TicketRepository(testDbPath))
                {
                    string payload = "Test'; DROP TABLE Tickets; --";
                    TicketRecord sqliTicket = new TicketRecord
                    {
                        TicketNo = "#999",
                        Title = payload,
                        ScratchNotes = payload,
                        CreatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 120,
                        StartTimeUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 120,
                        DeadlineUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600,
                        Steps = new List<TicketStepRecord>
                        {
                            new TicketStepRecord { StepType = "core", Content = payload }
                        }
                    };
                    repo.SaveTicket(sqliTicket);
                    List<TicketRecord> loaded = repo.GetActiveTickets();
                    bool found = false;
                    foreach (TicketRecord r in loaded)
                    {
                        if (r.Title == payload) found = true;
                    }
                    if (!found) throw new Exception("SQL parameterization test failed.");

                    Console.WriteLine("[2/5] Testing Short-Task Proportional Countdown Rule (v2 spec)...");
                    long now = 1000000;
                    TicketRecord shortTask = new TicketRecord
                    {
                        StartTimeUnix = now,
                        DeadlineUnix = now + 1200 // 20 min total
                    };
                    // 18 min remaining (90% left, even though < 30 min) -> must be Green!
                    if (shortTask.ComputeUrgency(now + 120) != UrgencyState.Green)
                        throw new Exception("Short task proportional rule failed: expected Green at 90%.");

                    TicketRecord longTask = new TicketRecord
                    {
                        StartTimeUnix = now,
                        DeadlineUnix = now + 7200 // 120 min total
                    };
                    // 25 min remaining on a 2-hour task -> must trigger last-30-min Orange!
                    if (longTask.ComputeUrgency(now + 7200 - 1500) != UrgencyState.Orange)
                        throw new Exception("Long task 30-min rule failed: expected Orange.");

                    Console.WriteLine("[3/5] Testing Image Count Points, Zero Initial Points & Daily Date History...");
                    UserEconomyRecord initialEco = repo.GetEconomy();
                    if (initialEco.TotalTips != 0)
                        throw new Exception("Initial points should be 0.");
                    if (initialEco.ReminderMinutes != 30)
                        throw new Exception("Default upcoming deadline reminder should be 30 minutes.");

                    initialEco.ReminderMinutes = 45;
                    repo.SaveEconomy(initialEco);
                    if (repo.GetEconomy().ReminderMinutes != 45)
                        throw new Exception("Updated ReminderMinutes failed to persist in SQLite.");

                    TicketRecord noCountTicket = new TicketRecord
                    {
                        TicketNo = "#888",
                        Title = "No image count specified",
                        CreatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        DeadlineUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 600
                    };
                    repo.SaveTicket(noCountTicket);
                    string msg1;
                    int zeroTips = repo.ServeTicketAndAwardTips(noCountTicket, out msg1);
                    if (zeroTips != 0)
                        throw new Exception("Ticket without 图片数量 should award 0 points.");

                    sqliTicket.Title = "活动海报 图片数量：18";
                    string msg2;
                    int validTips = repo.ServeTicketAndAwardTips(sqliTicket, out msg2);
                    if (validTips != 18 || repo.GetEconomy().TotalTips != 18)
                        throw new Exception("Ticket with '图片数量：18' should award exactly 18 points.");

                    if (repo.GetDailyPointsHistorySummary().Count == 0)
                        throw new Exception("Daily points history summary should contain today's entry.");

                    repo.ClearAllPointsHistory();
                    if (repo.GetEconomy().TotalTips != 0 || repo.GetRecentTipLedger(10).Count != 0)
                        throw new Exception("ClearAllPointsHistory should reset points and clear TipLedger.");

                    Console.WriteLine("[4/5] Testing Online Backup & Untrusted Database Trigger/View/Index Rejection...");
                    string cleanBackup = Path.Combine(tempDir, "clean_backup.db");
                    repo.ExportBackupSafely(cleanBackup);
                    TicketRepository.ValidateUntrustedDatabaseFile(cleanBackup);
                    repo.ImportValidatedBackup(cleanBackup);

                    string evilBackup = Path.Combine(tempDir, "evil_backup.db");
                    File.Copy(cleanBackup, evilBackup, true);
                    IntPtr evilDb;
                    if (Winsqlite3Native.sqlite3_open_v2(
                        Winsqlite3Native.ToUtf8NullTerminated(evilBackup),
                        out evilDb,
                        Winsqlite3Native.SQLITE_OPEN_READWRITE,
                        IntPtr.Zero) == Winsqlite3Native.SQLITE_OK)
                    {
                        IntPtr st;
                        Winsqlite3Native.sqlite3_prepare_v2(
                            evilDb,
                            Winsqlite3Native.ToUtf8NullTerminated("CREATE TRIGGER evil_trig AFTER INSERT ON Tickets BEGIN DELETE FROM Tickets; END;"),
                            -1, out st, IntPtr.Zero);
                        Winsqlite3Native.sqlite3_step(st);
                        Winsqlite3Native.sqlite3_finalize(st);
                        Winsqlite3Native.sqlite3_close_v2(evilDb);
                    }

                    bool blockedTrigger = false;
                    try
                    {
                        repo.ImportValidatedBackup(evilBackup);
                    }
                    catch (InvalidOperationException)
                    {
                        blockedTrigger = true;
                    }
                    if (!blockedTrigger)
                        throw new Exception("Security gate failed: untrusted DB with TRIGGER was not rejected!");

                    Console.WriteLine("[5/5] Testing Input Sanitization & HTTPS Update Allowlist...");
                    string dirty = "Click javascript:alert(1) or file:///C:/Windows/win.ini or ms-settings:display or vbscript :msgbox";
                    string sanitized = AudioAndUpdateService.SanitizePastedText(dirty);
                    if (sanitized.Contains("javascript:") || sanitized.Contains("file:") || sanitized.Contains("ms-settings:") || sanitized.Contains("vbscript"))
                        throw new Exception("URI scheme sanitizer failed.");

                    string updMsg;
                    if (AudioAndUpdateService.OpenOfficialReleasePageSafely("http://insecure.example.com/update.exe", out updMsg))
                        throw new Exception("Update service failed to block non-HTTPS untrusted URL.");
                    if (AudioAndUpdateService.OpenOfficialReleasePageSafely("https://evilgithub.com/releases", out updMsg))
                        throw new Exception("Update service failed to block lookalike suffix domain evilgithub.com.");
                    if (AudioAndUpdateService.OpenOfficialReleasePageSafely("https://user@github.com/releases", out updMsg))
                        throw new Exception("Update service failed to block embedded credentials in URL.");
                    if (AudioAndUpdateService.OpenOfficialReleasePageSafely("https://github.com:8443/releases", out updMsg))
                        throw new Exception("Update service failed to block non-default HTTPS port.");

                    Uri safeOllama;
                    if (!LocalAiAndOcrService.TryResolveSafeLoopbackGenerateUri("http://127.0.0.1:11434", out safeOllama))
                        throw new Exception("Loopback validator rejected valid 127.0.0.1 endpoint.");
                    if (LocalAiAndOcrService.TryResolveSafeLoopbackGenerateUri("http://192.168.1.50:11434", out safeOllama) ||
                        LocalAiAndOcrService.TryResolveSafeLoopbackGenerateUri("http://evil.example.com:11434", out safeOllama))
                    {
                        throw new Exception("Loopback SSRF guard failed to block non-loopback endpoint.");
                    }

                    string macroTitle, macroUrl;
                    List<TicketStepRecord> macroSteps;
#if CLEAN_RELEASE
                    if (!LocalAiAndOcrService.TryExpandRecipeMacro("周五项目周报", out macroTitle, out macroUrl, out macroSteps) ||
                        macroTitle != "周五项目周报" ||
                        macroSteps.Count != 4)
                    {
                        throw new Exception("Recipe macro expansion test failed for '周报'.");
                    }

                    if (!LocalAiAndOcrService.TryExpandRecipeMacro("新品设计任务", out macroTitle, out macroUrl, out macroSteps) ||
                        macroTitle != "新品设计任务" ||
                        macroSteps.Count != 4)
                    {
                        throw new Exception("Recipe macro expansion test failed for '设计'.");
                    }
#else
                    if (!LocalAiAndOcrService.TryExpandRecipeMacro("29号需求，产品口播", out macroTitle, out macroUrl, out macroSteps) ||
                        macroTitle != "29号需求，产品口播" ||
                        macroSteps.Count != 8)
                    {
                        throw new Exception("Recipe macro expansion test failed for '口播'.");
                    }

                    if (!LocalAiAndOcrService.TryExpandRecipeMacro("30号需求，视频换脸", out macroTitle, out macroUrl, out macroSteps) ||
                        macroTitle != "30号需求，视频换脸" ||
                        macroSteps.Count != 5)
                    {
                        throw new Exception("Recipe macro expansion test failed for '换脸'.");
                    }
#endif

                    int dur8h;
                    string remAfterTime;
                    if (!LocalAiAndOcrService.TryParseQuickTimeCommand("活动海报设计 time 8h", out dur8h, out remAfterTime) ||
                        dur8h != 8 * 3600 ||
                        remAfterTime != "活动海报设计")
                    {
                        throw new Exception("Natural 'time 8h' parsing failed.");
                    }

                    repo.SaveRecipe(0, "海报、封面", "找参考、排版设计、导出上传");
                    if (!LocalAiAndOcrService.TryExpandRecipeMacro("31号活动海报", out macroTitle, out macroUrl, out macroSteps) ||
                        macroSteps.Count != 3)
                    {
                        throw new Exception("Custom SQLite recipe persistence test failed.");
                    }
                }

                Console.WriteLine("ALL 5 SECURITY & ARCHITECTURE SELF-TESTS PASSED SUCCESSFULLY.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("SELF-TEST FAILED: " + ex.Message);
                return 1;
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }
}
