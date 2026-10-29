using System;
using System.Collections.Generic;
using System.IO;

namespace StickyTickets.Data
{
    public partial class TicketRepository
    {
        public int ServeTicketAndAwardTips(TicketRecord t, out string awardMessage)
        {
            awardMessage = "";
            if (t == null || t.Status == "Served") return 0;

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            UrgencyState u = t.ComputeUrgency(now);

            foreach (TicketStepRecord st in t.Steps)
            {
                st.IsCompleted = true;
            }
            t.Status = "Served";

            switch (u)
            {
                case UrgencyState.Green:
                    t.ServedScore = 100;
                    break;
                case UrgencyState.Yellow:
                    t.ServedScore = 90;
                    break;
                case UrgencyState.Orange:
                    t.ServedScore = 78;
                    break;
                default:
                    t.ServedScore = 60;
                    break;
            }

            int pointsFromTicket = t.TipEarned;
            int parsedPts;
            string dummyRem;
            if (pointsFromTicket <= 0 && Services.LocalAiAndOcrService.TryParseImageCountPoints(t.Title, out parsedPts, out dummyRem))
                pointsFromTicket = parsedPts;
            if (pointsFromTicket <= 0 && Services.LocalAiAndOcrService.TryParseImageCountPoints(t.ScratchNotes, out parsedPts, out dummyRem))
                pointsFromTicket = parsedPts;
            if (pointsFromTicket <= 0)
            {
                foreach (TicketStepRecord st in t.Steps)
                {
                    if (Services.LocalAiAndOcrService.TryParseImageCountPoints(st.Content, out parsedPts, out dummyRem) && parsedPts > 0)
                    {
                        pointsFromTicket = parsedPts;
                        break;
                    }
                }
            }

            int earned = Math.Max(0, pointsFromTicket);
            if (earned > 0)
            {
                string reason = string.Format("交单 {0} ({1} · 图片数量: {2})", t.TicketNo, t.Title, earned);
                ExecuteNonQuery(
                    "INSERT INTO TipLedger (TicketId, Delta, Reason, CreatedAtUnix) VALUES (?1, ?2, ?3, ?4);",
                    stmt =>
                    {
                        Winsqlite3Native.sqlite3_bind_int64(stmt, 1, t.Id);
                        Winsqlite3Native.sqlite3_bind_int(stmt, 2, earned);
                        Winsqlite3Native.BindText(stmt, 3, reason);
                        Winsqlite3Native.sqlite3_bind_int64(stmt, 4, now);
                    });
                SyncLedgerTotalTips();
                awardMessage = string.Format("PERFECT! 本单积分 +{0}", earned);
            }
            else
            {
                SyncLedgerTotalTips();
                awardMessage = "已出餐 (未填写图片数量，本单 +0 积分)";
            }

            t.TipEarned = earned;
            SaveTicket(t);
            return earned;
        }

        /// <summary>
        /// Calculates TODAY's points only (resets to 0 at local midnight every day).
        /// Historical records remain intact in TipLedger for the daily date summary!
        /// </summary>
        public void SyncLedgerTotalTips()
        {
            long startOfTodayLocal = new DateTimeOffset(DateTime.Now.Date).ToUnixTimeSeconds();
            int todaySum = ExecuteScalarInt(
                "SELECT COALESCE(SUM(Delta), 0) FROM TipLedger WHERE CreatedAtUnix >= ?1;",
                stmt => Winsqlite3Native.sqlite3_bind_int64(stmt, 1, startOfTodayLocal));

            ExecuteNonQuery("UPDATE UserEconomy SET TotalTips = ?1 WHERE Id = 1;",
                stmt => Winsqlite3Native.sqlite3_bind_int(stmt, 1, Math.Max(0, todaySum)));
        }

        public List<string> GetDailyPointsHistorySummary()
        {
            List<TipLedgerRecord> all = GetRecentTipLedger(500);
            Dictionary<string, int> datePoints = new Dictionary<string, int>();
            Dictionary<string, int> dateOrders = new Dictionary<string, int>();
            List<string> orderedDates = new List<string>();

            string todayKey = DateTime.Now.ToString("yyyy-MM-dd");
            datePoints[todayKey] = 0;
            dateOrders[todayKey] = 0;
            orderedDates.Add(todayKey);

            foreach (TipLedgerRecord r in all)
            {
                string dKey = DateTimeOffset.FromUnixTimeSeconds(r.CreatedAtUnix).ToLocalTime().ToString("yyyy-MM-dd");
                if (!datePoints.ContainsKey(dKey))
                {
                    datePoints[dKey] = 0;
                    dateOrders[dKey] = 0;
                    orderedDates.Add(dKey);
                }
                datePoints[dKey] += r.Delta;
                dateOrders[dKey] += 1;
            }

            orderedDates.Sort((a, b) => string.CompareOrdinal(b, a));
            List<string> lines = new List<string>();
            foreach (string d in orderedDates)
            {
                string tag = (d == todayKey) ? " (今日)" : "";
                lines.Add(string.Format("📅 {0}{1}  ——  当日累计积分: ⭐ {2}   (完成计分单: {3} 单)",
                    d, tag, datePoints[d], dateOrders[d]));
            }
            return lines;
        }

        /// <summary>
        /// Clears all historical points records (TipLedger) and completed/trashed ticket archives,
        /// while keeping active unserved tickets intact.
        /// </summary>
        public void ClearAllPointsHistory()
        {
            ExecuteNonQuery(@"
                BEGIN IMMEDIATE;
                DELETE FROM TipLedger;
                DELETE FROM Tickets WHERE Status = 'Served' OR Status = 'Trash';
                DELETE FROM TicketSteps WHERE TicketId NOT IN (SELECT Id FROM Tickets);
                DELETE FROM TicketAlarms WHERE TicketId NOT IN (SELECT Id FROM Tickets);
                DELETE FROM TicketImages WHERE TicketId NOT IN (SELECT Id FROM Tickets);
                UPDATE UserEconomy SET TotalTips = 0 WHERE Id = 1;
                COMMIT;
            ");
        }

        public UserEconomyRecord GetEconomy()
        {
            SyncLedgerTotalTips();
            UserEconomyRecord rec = new UserEconomyRecord();
            string sql = "SELECT TotalTips, HideTipJarOnUi, WishTitle, WishTargetTips, ActiveSkin, FirstRunGuided, ReminderMinutes, OnboardingNoticeAcked FROM UserEconomy WHERE Id = 1;";
            IntPtr stmt;
            if (Winsqlite3Native.sqlite3_prepare_v2(_db, Winsqlite3Native.ToUtf8NullTerminated(sql), -1, out stmt, IntPtr.Zero) == Winsqlite3Native.SQLITE_OK)
            {
                try
                {
                    if (Winsqlite3Native.sqlite3_step(stmt) == Winsqlite3Native.SQLITE_ROW)
                    {
                        rec.TotalTips = Winsqlite3Native.sqlite3_column_int(stmt, 0);
                        rec.HideTipJarOnUi = Winsqlite3Native.sqlite3_column_int(stmt, 1) != 0;
                        rec.WishTitle = Winsqlite3Native.GetText(stmt, 2);
                        rec.WishTargetTips = Math.Max(10, Winsqlite3Native.sqlite3_column_int(stmt, 3));
                        rec.ActiveSkin = Winsqlite3Native.GetText(stmt, 4);
                        rec.FirstRunGuided = Winsqlite3Native.sqlite3_column_int(stmt, 5) != 0;
                        rec.ReminderMinutes = Math.Max(0, Math.Min(1440, Winsqlite3Native.sqlite3_column_int(stmt, 6)));
                        rec.OnboardingNoticeAcked = Winsqlite3Native.sqlite3_column_int(stmt, 7) != 0;
                    }
                }
                finally
                {
                    Winsqlite3Native.sqlite3_finalize(stmt);
                }
            }
            return rec;
        }

        public void SaveEconomy(UserEconomyRecord rec)
        {
            if (rec == null) return;
            ExecuteNonQuery(@"
                UPDATE UserEconomy
                SET HideTipJarOnUi = ?1, WishTitle = ?2, WishTargetTips = ?3, ActiveSkin = ?4, FirstRunGuided = ?5, ReminderMinutes = ?6, OnboardingNoticeAcked = ?7
                WHERE Id = 1;",
                stmt =>
                {
                    Winsqlite3Native.sqlite3_bind_int(stmt, 1, rec.HideTipJarOnUi ? 1 : 0);
                    Winsqlite3Native.BindText(stmt, 2, rec.WishTitle);
                    Winsqlite3Native.sqlite3_bind_int(stmt, 3, Math.Max(10, rec.WishTargetTips));
                    Winsqlite3Native.BindText(stmt, 4, rec.ActiveSkin);
                    Winsqlite3Native.sqlite3_bind_int(stmt, 5, rec.FirstRunGuided ? 1 : 0);
                    Winsqlite3Native.sqlite3_bind_int(stmt, 6, Math.Max(0, Math.Min(1440, rec.ReminderMinutes)));
                    Winsqlite3Native.sqlite3_bind_int(stmt, 7, rec.OnboardingNoticeAcked ? 1 : 0);
                });
        }

        public List<TipLedgerRecord> GetRecentTipLedger(int limit = 25)
        {
            List<TipLedgerRecord> list = new List<TipLedgerRecord>();
            string sql = "SELECT Id, TicketId, Delta, Reason, CreatedAtUnix FROM TipLedger ORDER BY Id DESC LIMIT ?1;";
            IntPtr stmt;
            if (Winsqlite3Native.sqlite3_prepare_v2(_db, Winsqlite3Native.ToUtf8NullTerminated(sql), -1, out stmt, IntPtr.Zero) == Winsqlite3Native.SQLITE_OK)
            {
                try
                {
                    Winsqlite3Native.sqlite3_bind_int(stmt, 1, limit);
                    while (Winsqlite3Native.sqlite3_step(stmt) == Winsqlite3Native.SQLITE_ROW)
                    {
                        list.Add(new TipLedgerRecord
                        {
                            Id = Winsqlite3Native.sqlite3_column_int64(stmt, 0),
                            TicketId = Winsqlite3Native.sqlite3_column_int64(stmt, 1),
                            Delta = Winsqlite3Native.sqlite3_column_int(stmt, 2),
                            Reason = Winsqlite3Native.GetText(stmt, 3),
                            CreatedAtUnix = Winsqlite3Native.sqlite3_column_int64(stmt, 4)
                        });
                    }
                }
                finally
                {
                    Winsqlite3Native.sqlite3_finalize(stmt);
                }
            }
            return list;
        }

        public void SaveTicketImage(long ticketId, byte[] pngBlob, int width, int height)
        {
            ExecuteNonQuery("DELETE FROM TicketImages WHERE TicketId = ?1;",
                stmt => Winsqlite3Native.sqlite3_bind_int64(stmt, 1, ticketId));

            string id = Guid.NewGuid().ToString("N");
            ExecuteNonQuery(
                "INSERT INTO TicketImages (Id, TicketId, MimeType, ImageBlob, Width, Height) VALUES (?1, ?2, 'image/png', ?3, ?4, ?5);",
                stmt =>
                {
                    Winsqlite3Native.BindText(stmt, 1, id);
                    Winsqlite3Native.sqlite3_bind_int64(stmt, 2, ticketId);
                    Winsqlite3Native.BindBlob(stmt, 3, pngBlob);
                    Winsqlite3Native.sqlite3_bind_int(stmt, 4, width);
                    Winsqlite3Native.sqlite3_bind_int(stmt, 5, height);
                });
        }

        public void DeleteTicketImage(long ticketId)
        {
            ExecuteNonQuery("DELETE FROM TicketImages WHERE TicketId = ?1;",
                stmt => Winsqlite3Native.sqlite3_bind_int64(stmt, 1, ticketId));
        }

        public byte[] GetTicketImage(long ticketId)
        {
            string sql = "SELECT ImageBlob FROM TicketImages WHERE TicketId = ?1 LIMIT 1;";
            IntPtr stmt;
            if (Winsqlite3Native.sqlite3_prepare_v2(_db, Winsqlite3Native.ToUtf8NullTerminated(sql), -1, out stmt, IntPtr.Zero) == Winsqlite3Native.SQLITE_OK)
            {
                try
                {
                    Winsqlite3Native.sqlite3_bind_int64(stmt, 1, ticketId);
                    if (Winsqlite3Native.sqlite3_step(stmt) == Winsqlite3Native.SQLITE_ROW)
                        return Winsqlite3Native.GetBlob(stmt, 0);
                }
                finally
                {
                    Winsqlite3Native.sqlite3_finalize(stmt);
                }
            }
            return Array.Empty<byte>();
        }

        /// <summary>
        /// Exports a consistent snapshot using SQLite Online Backup API.
        /// </summary>
        public void ExportBackupSafely(string destinationPath)
        {
            if (string.IsNullOrWhiteSpace(destinationPath))
                throw new ArgumentException("备份路径不能为空。");

            string fullDest = Path.GetFullPath(destinationPath);
            if (!fullDest.EndsWith(".db", StringComparison.OrdinalIgnoreCase) &&
                !fullDest.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("备份文件扩展名必须为 .db 或 .bak。");
            }

            if (File.Exists(fullDest)) File.Delete(fullDest);

            IntPtr destDb;
            int flags = Winsqlite3Native.SQLITE_OPEN_READWRITE | Winsqlite3Native.SQLITE_OPEN_CREATE;
            int rc = Winsqlite3Native.sqlite3_open_v2(Winsqlite3Native.ToUtf8NullTerminated(fullDest), out destDb, flags, IntPtr.Zero);
            if (rc != Winsqlite3Native.SQLITE_OK)
                throw new InvalidOperationException("无法创建目标备份文件。");

            try
            {
                byte[] mainName = Winsqlite3Native.ToUtf8NullTerminated("main");
                IntPtr backup = Winsqlite3Native.sqlite3_backup_init(destDb, mainName, _db, mainName);
                if (backup == IntPtr.Zero)
                    throw new InvalidOperationException("初始化 SQLite 在线备份失败。");

                Winsqlite3Native.sqlite3_backup_step(backup, -1);
                Winsqlite3Native.sqlite3_backup_finish(backup);
            }
            finally
            {
                Winsqlite3Native.sqlite3_close_v2(destDb);
            }
        }

        /// <summary>
        /// SECURITY GATE: Validates an untrusted external .db file before importing.
        /// 1. Verifies SQLite file magic header.
        /// 2. Runs PRAGMA integrity_check in read-only mode.
        /// 3. Verifies PRAGMA user_version matches supported schema.
        /// 4. Inspects sqlite_master and strictly rejects ANY database containing triggers, views, or unknown tables.
        /// </summary>
        public static void ValidateUntrustedDatabaseFile(string untrustedPath)
        {
            if (string.IsNullOrWhiteSpace(untrustedPath) || !File.Exists(untrustedPath))
                throw new FileNotFoundException("找不到指定的备份文件。");

            string fullPath = Path.GetFullPath(untrustedPath);
            FileInfo fi = new FileInfo(fullPath);
            if (fi.Length < 100 || fi.Length > 64 * 1024 * 1024)
                throw new InvalidOperationException("备份文件体积异常，已拒绝导入。");

            byte[] header = new byte[16];
            using (FileStream fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (fs.Read(header, 0, 16) != 16)
                    throw new InvalidOperationException("无法读取备份文件头。");
            }
            string magic = System.Text.Encoding.ASCII.GetString(header, 0, 15);
            if (magic != "SQLite format 3")
                throw new InvalidOperationException("非法的 SQLite 文件头，已拦截导入。");

            IntPtr checkDb;
            int rc = Winsqlite3Native.sqlite3_open_v2(
                Winsqlite3Native.ToUtf8NullTerminated(fullPath),
                out checkDb,
                Winsqlite3Native.SQLITE_OPEN_READONLY,
                IntPtr.Zero);

            if (rc != Winsqlite3Native.SQLITE_OK || checkDb == IntPtr.Zero)
                throw new InvalidOperationException("无法以只读隔离模式打开备份文件。");

            try
            {
                Winsqlite3Native.TryEnableDefensiveMode(checkDb);

                // 1. PRAGMA integrity_check (Fail-Closed!)
                IntPtr stmt;
                if (Winsqlite3Native.sqlite3_prepare_v2(checkDb, Winsqlite3Native.ToUtf8NullTerminated("PRAGMA integrity_check;"), -1, out stmt, IntPtr.Zero) != Winsqlite3Native.SQLITE_OK ||
                    stmt == IntPtr.Zero)
                {
                    throw new InvalidOperationException("数据库完整性校验启动失败，已拒绝导入。");
                }
                try
                {
                    if (Winsqlite3Native.sqlite3_step(stmt) != Winsqlite3Native.SQLITE_ROW ||
                        !string.Equals(Winsqlite3Native.GetText(stmt, 0), "ok", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException("数据库完整性校验 (integrity_check) 未通过。");
                    }
                }
                finally
                {
                    Winsqlite3Native.sqlite3_finalize(stmt);
                }

                // 2. PRAGMA user_version (Fail-Closed!)
                if (Winsqlite3Native.sqlite3_prepare_v2(checkDb, Winsqlite3Native.ToUtf8NullTerminated("PRAGMA user_version;"), -1, out stmt, IntPtr.Zero) != Winsqlite3Native.SQLITE_OK ||
                    stmt == IntPtr.Zero)
                {
                    throw new InvalidOperationException("无法读取备份数据库 Schema 版本，已拒绝导入。");
                }
                try
                {
                    int ver = Winsqlite3Native.sqlite3_step(stmt) == Winsqlite3Native.SQLITE_ROW
                        ? Winsqlite3Native.sqlite3_column_int(stmt, 0) : -1;
                    if (ver != CurrentSchemaVersion)
                        throw new InvalidOperationException("备份文件 Schema 版本不兼容 (user_version=" + ver + ")。");
                }
                finally
                {
                    Winsqlite3Native.sqlite3_finalize(stmt);
                }

                // 3. Strict allowlist of sqlite_master objects (Fail-Closed!)
                HashSet<string> allowedTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "Tickets", "TicketSteps", "TicketAlarms", "TicketImages", "TipLedger", "UserEconomy", "RecipeTemplates", "sqlite_sequence"
                };

                string masterSql = "SELECT type, name, sql FROM sqlite_master;";
                if (Winsqlite3Native.sqlite3_prepare_v2(checkDb, Winsqlite3Native.ToUtf8NullTerminated(masterSql), -1, out stmt, IntPtr.Zero) != Winsqlite3Native.SQLITE_OK ||
                    stmt == IntPtr.Zero)
                {
                    throw new InvalidOperationException("无法审查备份数据库结构 (sqlite_master)，已拒绝导入。");
                }
                try
                {
                    while (Winsqlite3Native.sqlite3_step(stmt) == Winsqlite3Native.SQLITE_ROW)
                    {
                        string objType = Winsqlite3Native.GetText(stmt, 0);
                        string objName = Winsqlite3Native.GetText(stmt, 1);
                        string objSql = Winsqlite3Native.GetText(stmt, 2);

                        if (string.Equals(objType, "trigger", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(objType, "view", StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidOperationException("安全拦截：备份数据库包含危险的触发器或视图 (" + objName + ")，已拒绝导入!");
                        }

                        if (!string.Equals(objType, "table", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(objType, "index", StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidOperationException("安全拦截：备份数据库包含未知结构类型 (" + objType + ")，已拒绝导入!");
                        }

                        if (string.Equals(objType, "table", StringComparison.OrdinalIgnoreCase) &&
                            !allowedTables.Contains(objName))
                        {
                            throw new InvalidOperationException("安全拦截：备份数据库包含未知表 (" + objName + ")，已拒绝导入!");
                        }

                        if (!string.IsNullOrEmpty(objSql))
                        {
                            if (objSql.IndexOf("VIRTUAL", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                objSql.IndexOf("ATTACH", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                throw new InvalidOperationException("安全拦截：检测到危险的虚拟表或 ATTACH 定义，已拒绝导入!");
                            }

                            // Block expression/partial custom indexes that could execute SQL functions on open/query
                            if (string.Equals(objType, "index", StringComparison.OrdinalIgnoreCase) &&
                                objSql.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                throw new InvalidOperationException("安全拦截：检测到可疑的条件/表达式索引 (" + objName + ")，已拒绝导入!");
                            }
                        }
                    }
                }
                finally
                {
                    Winsqlite3Native.sqlite3_finalize(stmt);
                }
            }
            finally
            {
                Winsqlite3Native.sqlite3_close_v2(checkDb);
            }
        }

        public void ImportValidatedBackup(string untrustedPath)
        {
            if (string.IsNullOrWhiteSpace(untrustedPath) || !File.Exists(untrustedPath))
                throw new FileNotFoundException("找不到指定的备份文件。");

            // Copy external file into private app directory first to eliminate TOCTOU race between validation and import
            string targetDir = Path.GetDirectoryName(_dbPath) ?? Path.GetTempPath();
            string stagedPath = Path.Combine(targetDir, ".import_staging_" + Guid.NewGuid().ToString("N") + ".db");

            try
            {
                File.Copy(Path.GetFullPath(untrustedPath), stagedPath, true);
                ValidateUntrustedDatabaseFile(stagedPath);

                IntPtr srcDb;
                int rc = Winsqlite3Native.sqlite3_open_v2(
                    Winsqlite3Native.ToUtf8NullTerminated(stagedPath),
                    out srcDb,
                    Winsqlite3Native.SQLITE_OPEN_READONLY,
                    IntPtr.Zero);
                if (rc != Winsqlite3Native.SQLITE_OK || srcDb == IntPtr.Zero)
                    throw new InvalidOperationException("无法读取校验通过的备份源文件。");

                try
                {
                    Winsqlite3Native.TryEnableDefensiveMode(srcDb);
                    byte[] mainName = Winsqlite3Native.ToUtf8NullTerminated("main");
                    IntPtr backup = Winsqlite3Native.sqlite3_backup_init(_db, mainName, srcDb, mainName);
                    if (backup == IntPtr.Zero)
                        throw new InvalidOperationException("无法启动数据库恢复。");

                    int stepRc = Winsqlite3Native.sqlite3_backup_step(backup, -1);
                    int finishRc = Winsqlite3Native.sqlite3_backup_finish(backup);
                    if (stepRc != Winsqlite3Native.SQLITE_DONE || finishRc != Winsqlite3Native.SQLITE_OK)
                        throw new InvalidOperationException("备份恢复过程未完整结束，操作已中止。");

                    ExecuteNonQuery("ALTER TABLE UserEconomy ADD COLUMN ReminderMinutes INTEGER NOT NULL DEFAULT 30;");
                    ExecuteNonQuery("ALTER TABLE UserEconomy ADD COLUMN OnboardingNoticeAcked INTEGER NOT NULL DEFAULT 0;");
                }
                finally
                {
                    Winsqlite3Native.sqlite3_close_v2(srcDb);
                }
            }
            finally
            {
                try
                {
                    if (File.Exists(stagedPath)) File.Delete(stagedPath);
                }
                catch
                {
                }
            }
        }
    }
}
