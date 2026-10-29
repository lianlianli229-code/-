using System;
using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace StickyTickets.Data
{
    public partial class TicketRepository : IDisposable
    {
        public const int CurrentSchemaVersion = 1;
        public const int MinTicketAgeSecondsForTips = 15;
        public const int DailyTipCap = 300;

        private IntPtr _db = IntPtr.Zero;
        private readonly string _dbPath;

        public string DatabasePath { get { return _dbPath; } }

        public TicketRepository(string dbPath)
        {
            if (string.IsNullOrWhiteSpace(dbPath))
                throw new ArgumentException("Database path cannot be empty.");

            _dbPath = Path.GetFullPath(dbPath);
            string dir = Path.GetDirectoryName(_dbPath);
            if (!string.IsNullOrEmpty(dir))
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                TryRestrictDirectoryAclToCurrentUser(dir);
            }

            OpenAndMigrate();
        }

        /// <summary>
        /// Restricts the local database directory ACL on shared Windows machines so only the current
        /// user account, SYSTEM, and Administrators have access (mitigating cross-user local read).
        /// </summary>
        private static void TryRestrictDirectoryAclToCurrentUser(string directoryPath)
        {
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrEmpty(localAppData)) return;
                string normalizedDir = Path.GetFullPath(directoryPath).TrimEnd('\\');
                string normalizedAppData = Path.GetFullPath(localAppData).TrimEnd('\\');
                if (!normalizedDir.StartsWith(normalizedAppData + "\\", StringComparison.OrdinalIgnoreCase))
                    return;

                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                if (identity == null || identity.User == null) return;

                DirectoryInfo di = new DirectoryInfo(normalizedDir);
                DirectorySecurity security = new DirectorySecurity();
                security.SetAccessRuleProtection(true, false);

                InheritanceFlags inh = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
                security.AddAccessRule(new FileSystemAccessRule(
                    identity.User,
                    FileSystemRights.FullControl,
                    inh,
                    PropagationFlags.None,
                    AccessControlType.Allow));
                security.AddAccessRule(new FileSystemAccessRule(
                    new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                    FileSystemRights.FullControl,
                    inh,
                    PropagationFlags.None,
                    AccessControlType.Allow));
                security.AddAccessRule(new FileSystemAccessRule(
                    new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                    FileSystemRights.FullControl,
                    inh,
                    PropagationFlags.None,
                    AccessControlType.Allow));

                di.SetAccessControl(security);
            }
            catch
            {
                // Preserve availability on filesystems or sandboxes that do not support NTFS ACLs
            }
        }

        private void OpenAndMigrate()
        {
            int flags = Winsqlite3Native.SQLITE_OPEN_READWRITE |
                        Winsqlite3Native.SQLITE_OPEN_CREATE |
                        Winsqlite3Native.SQLITE_OPEN_FULLMUTEX;

            int rc = Winsqlite3Native.sqlite3_open_v2(Winsqlite3Native.ToUtf8NullTerminated(_dbPath), out _db, flags, IntPtr.Zero);
            if (rc != Winsqlite3Native.SQLITE_OK)
                throw new InvalidOperationException("无法打开本地 SQLite 数据库，错误码: " + rc);

            ExecuteNonQuery("PRAGMA foreign_keys = ON;");
            ExecuteNonQuery("PRAGMA journal_mode = WAL;");
            ExecuteNonQuery("PRAGMA synchronous = NORMAL;");

            int version = ExecuteScalarInt("PRAGMA user_version;");
            if (version < 1)
            {
                ApplyMigrationV1();
                ExecuteNonQuery("PRAGMA user_version = 1;");
            }
            // Non-destructive column migration for upcoming deadline reminder lead time (default 30 min)
            ExecuteNonQuery("ALTER TABLE UserEconomy ADD COLUMN ReminderMinutes INTEGER NOT NULL DEFAULT 30;");
            // Non-destructive column migration for startup onboarding notice board acknowledgement
            ExecuteNonQuery("ALTER TABLE UserEconomy ADD COLUMN OnboardingNoticeAcked INTEGER NOT NULL DEFAULT 0;");

            // Clear any legacy initial bonus points per user requirement
            ExecuteNonQuery("DELETE FROM TipLedger WHERE TicketId = 0 OR Reason LIKE '%初始%';");
            SyncLedgerTotalTips();
            EnsureRecipeTableAndSeeds();

            PurgeExpiredTrash(30);
            EnsureSeedTicketsIfEmpty();
        }

        private void ApplyMigrationV1()
        {
            ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS Tickets (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    TicketNo TEXT NOT NULL,
                    Title TEXT NOT NULL,
                    ScratchNotes TEXT NOT NULL DEFAULT '',
                    PosX REAL NOT NULL DEFAULT 120,
                    PosY REAL NOT NULL DEFAULT 90,
                    Width REAL NOT NULL DEFAULT 360,
                    Height REAL NOT NULL DEFAULT 420,
                    IsDockedOnRail INTEGER NOT NULL DEFAULT 1,
                    RailOrder INTEGER NOT NULL DEFAULT 0,
                    IsOrderLocked INTEGER NOT NULL DEFAULT 0,
                    IsClickThrough INTEGER NOT NULL DEFAULT 0,
                    Status TEXT NOT NULL DEFAULT 'Active',
                    ServedScore INTEGER NOT NULL DEFAULT 0,
                    TipEarned INTEGER NOT NULL DEFAULT 0,
                    CreatedAtUnix INTEGER NOT NULL DEFAULT 0
                );
                CREATE INDEX IF NOT EXISTS IX_Tickets_Status_RailOrder ON Tickets(Status, RailOrder);

                CREATE TABLE IF NOT EXISTS TicketSteps (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    TicketId INTEGER NOT NULL,
                    SortOrder INTEGER NOT NULL DEFAULT 0,
                    StepType TEXT NOT NULL DEFAULT 'core',
                    Content TEXT NOT NULL,
                    IsCompleted INTEGER NOT NULL DEFAULT 0,
                    FOREIGN KEY(TicketId) REFERENCES Tickets(Id) ON DELETE CASCADE
                );
                CREATE INDEX IF NOT EXISTS IX_TicketSteps_TicketId_SortOrder ON TicketSteps(TicketId, SortOrder);

                CREATE TABLE IF NOT EXISTS TicketAlarms (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    TicketId INTEGER NOT NULL UNIQUE,
                    StartTimeUnix INTEGER NOT NULL DEFAULT 0,
                    DeadlineUnix INTEGER NOT NULL DEFAULT 0,
                    FOREIGN KEY(TicketId) REFERENCES Tickets(Id) ON DELETE CASCADE
                );
                CREATE INDEX IF NOT EXISTS IX_TicketAlarms_TicketId_Deadline ON TicketAlarms(TicketId, DeadlineUnix);

                CREATE TABLE IF NOT EXISTS TicketImages (
                    Id TEXT PRIMARY KEY,
                    TicketId INTEGER NOT NULL,
                    MimeType TEXT NOT NULL DEFAULT 'image/png',
                    ImageBlob BLOB NOT NULL,
                    Width INTEGER NOT NULL,
                    Height INTEGER NOT NULL,
                    FOREIGN KEY(TicketId) REFERENCES Tickets(Id) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS TipLedger (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    TicketId INTEGER NOT NULL DEFAULT 0,
                    Delta INTEGER NOT NULL,
                    Reason TEXT NOT NULL,
                    CreatedAtUnix INTEGER NOT NULL
                );

                CREATE TABLE IF NOT EXISTS UserEconomy (
                    Id INTEGER PRIMARY KEY CHECK (Id = 1),
                    TotalTips INTEGER NOT NULL DEFAULT 0,
                    HideTipJarOnUi INTEGER NOT NULL DEFAULT 0,
                    WishTitle TEXT NOT NULL DEFAULT '周末请自己吃顿真汉堡!',
                    WishTargetTips INTEGER NOT NULL DEFAULT 150,
                    ActiveSkin TEXT NOT NULL DEFAULT 'BurgerShop',
                    FirstRunGuided INTEGER NOT NULL DEFAULT 0,
                    ReminderMinutes INTEGER NOT NULL DEFAULT 30,
                    OnboardingNoticeAcked INTEGER NOT NULL DEFAULT 0
                );
            ");

            ExecuteNonQuery(@"
                INSERT OR IGNORE INTO UserEconomy (Id, TotalTips, HideTipJarOnUi, WishTitle, WishTargetTips, ActiveSkin, FirstRunGuided, ReminderMinutes, OnboardingNoticeAcked)
                VALUES (1, 0, 0, '周末请自己吃顿真汉堡!', 150, 'BurgerShop', 0, 30, 0);
            ");
        }

        private void EnsureSeedTicketsIfEmpty()
        {
            int count = ExecuteScalarInt("SELECT COUNT(1) FROM Tickets;");
            if (count > 0) return;

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

#if CLEAN_RELEASE
            TicketRecord t1 = new TicketRecord
            {
                TicketNo = "#001",
                Title = "新手快速上手指南",
                ScratchNotes = "读完勾选上方 6 条引导，点击右下角「叮！交单出餐」即可开启你的第一单！",
                IsDockedOnRail = false,
                PosX = 140,
                PosY = 100,
                RailOrder = 0,
                CreatedAtUnix = now - 60,
                StartTimeUnix = now - 60,
                DeadlineUnix = now + 3600,
                Steps = new List<TicketStepRecord>
                {
                    new TicketStepRecord { SortOrder = 0, StepType = "comm", Content = "一键隐藏全部：按 Ctrl+H 隐藏/呼出全部，按 Ctrl+G 保留当前小票仅收起顶部栏", IsCompleted = false },
                    new TicketStepRecord { SortOrder = 1, StepType = "prep", Content = "新建任务与笔记：点顶部「+ 新建小票」建倒计时任务，点「📝 新建笔记」建自由便签", IsCompleted = false },
                    new TicketStepRecord { SortOrder = 2, StepType = "core", Content = "收纳与桌面钉住：往下拖钉桌面，推回顶端吸回挂单杆，点标签或底部收起可展开/收起", IsCompleted = false },
                    new TicketStepRecord { SortOrder = 3, StepType = "prep", Content = "配方自动拆单：「📖 配方」保存常用模板，写单输入触发词回车自动生成固定工序", IsCompleted = false },
                    new TicketStepRecord { SortOrder = 4, StepType = "warn", Content = "交单获取积分：勾选步骤后点「🔔 叮！交单出餐」，按时完成或填图片数量赚取积分", IsCompleted = false },
                    new TicketStepRecord { SortOrder = 5, StepType = "comm", Content = "积分战报：点顶部「⭐ 积分」查看今日按时完成战报与历史记录，积分代表完成的图片张数", IsCompleted = false }
                }
            };
            SaveTicket(t1);
#else
            TicketRecord t1 = new TicketRecord
            {
                TicketNo = "#041",
                Title = "提交季度报销单",
                ScratchNotes = "财务截止收单，记得附上电子行程单。",
                IsDockedOnRail = true,
                RailOrder = 0,
                CreatedAtUnix = now - 3600,
                StartTimeUnix = now - 3600,
                DeadlineUnix = now + 240, // 4 min left -> Orange/Red urgency
                Steps = new List<TicketStepRecord>
                {
                    new TicketStepRecord { SortOrder = 0, StepType = "prep", Content = "导出滴滴行程单并打印", IsCompleted = true },
                    new TicketStepRecord { SortOrder = 1, StepType = "comm", Content = "找李经理签字审批", IsCompleted = true },
                    new TicketStepRecord { SortOrder = 2, StepType = "warn", Content = "下班前交到财务室!", IsCompleted = false }
                }
            };
            SaveTicket(t1);

            TicketRecord t2 = new TicketRecord
            {
                TicketNo = "#042",
                Title = "给王总发季度合同",
                ScratchNotes = "可输入 /time 30m 快速改倒计时；按住 Ctrl+Alt 唤醒穿透窗口。",
                IsDockedOnRail = true,
                RailOrder = 1,
                CreatedAtUnix = now - 1800,
                StartTimeUnix = now - 1800,
                DeadlineUnix = now + 1455, // ~24:15 left -> Yellow urgency
                Steps = new List<TicketStepRecord>
                {
                    new TicketStepRecord { SortOrder = 0, StepType = "prep", Content = "核对附件报价表数字", IsCompleted = true },
                    new TicketStepRecord { SortOrder = 1, StepType = "core", Content = "让法务审核核心条款", IsCompleted = false },
                    new TicketStepRecord { SortOrder = 2, StepType = "comm", Content = "盖章并扫描成 PDF", IsCompleted = false },
                    new TicketStepRecord { SortOrder = 3, StepType = "warn", Content = "发邮件记得抄送财务", IsCompleted = false }
                }
            };
            SaveTicket(t2);
#endif
        }

        private void EnsureRecipeTableAndSeeds()
        {
            ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS RecipeTemplates (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Keywords TEXT NOT NULL,
                    StepsText TEXT NOT NULL
                );
            ");

            int count = ExecuteScalarInt("SELECT COUNT(1) FROM RecipeTemplates;");
            if (count == 0)
            {
#if CLEAN_RELEASE
                SaveRecipe(0, "周报、汇报", "整理本周数据、梳理核心进展、列出下周计划、检查并发送汇报");
                SaveRecipe(0, "设计、修图", "收集参考素材、完成初稿制作、细节自检校对、导出成品交付");
#else
                SaveRecipe(0, "特效、口播", "准备参考图、和ai沟通理解需求、提取描述词、准备去生图、生图中、出图检查中、上传云端、已交付");
                SaveRecipe(0, "换脸", "下载原视频、打开软件、开始换脸、验收上传、交付");
#endif
            }
            Services.LocalAiAndOcrService.SyncRecipesFromDatabase(GetRecipes());
        }

        public List<RecipeRecord> GetRecipes()
        {
            List<RecipeRecord> list = new List<RecipeRecord>();
            string sql = "SELECT Id, Keywords, StepsText FROM RecipeTemplates ORDER BY Id ASC;";
            IntPtr stmt;
            if (Winsqlite3Native.sqlite3_prepare_v2(_db, Winsqlite3Native.ToUtf8NullTerminated(sql), -1, out stmt, IntPtr.Zero) == Winsqlite3Native.SQLITE_OK)
            {
                try
                {
                    while (Winsqlite3Native.sqlite3_step(stmt) == Winsqlite3Native.SQLITE_ROW)
                    {
                        list.Add(new RecipeRecord
                        {
                            Id = Winsqlite3Native.sqlite3_column_int64(stmt, 0),
                            Keywords = Winsqlite3Native.GetText(stmt, 1),
                            StepsText = Winsqlite3Native.GetText(stmt, 2)
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

        public void SaveRecipe(long id, string keywords, string stepsText)
        {
            if (string.IsNullOrWhiteSpace(keywords) || string.IsNullOrWhiteSpace(stepsText)) return;
            if (id <= 0)
            {
                ExecuteNonQuery("INSERT INTO RecipeTemplates (Keywords, StepsText) VALUES (?1, ?2);",
                    stmt =>
                    {
                        Winsqlite3Native.BindText(stmt, 1, keywords.Trim());
                        Winsqlite3Native.BindText(stmt, 2, stepsText.Trim());
                    });
            }
            else
            {
                ExecuteNonQuery("UPDATE RecipeTemplates SET Keywords = ?1, StepsText = ?2 WHERE Id = ?3;",
                    stmt =>
                    {
                        Winsqlite3Native.BindText(stmt, 1, keywords.Trim());
                        Winsqlite3Native.BindText(stmt, 2, stepsText.Trim());
                        Winsqlite3Native.sqlite3_bind_int64(stmt, 3, id);
                    });
            }
            Services.LocalAiAndOcrService.SyncRecipesFromDatabase(GetRecipes());
        }

        public void DeleteRecipe(long id)
        {
            ExecuteNonQuery("DELETE FROM RecipeTemplates WHERE Id = ?1;",
                stmt => Winsqlite3Native.sqlite3_bind_int64(stmt, 1, id));
            Services.LocalAiAndOcrService.SyncRecipesFromDatabase(GetRecipes());
        }

        public void PurgeExpiredTrash(int retentionDays)
        {
            long cutoff = DateTimeOffset.UtcNow.AddDays(-retentionDays).ToUnixTimeSeconds();
            ExecuteNonQuery("DELETE FROM Tickets WHERE Status = 'Trash' AND CreatedAtUnix > 0 AND CreatedAtUnix < ?1;",
                stmt => Winsqlite3Native.sqlite3_bind_int64(stmt, 1, cutoff));
        }

        public void ExecuteNonQuery(string sql, Action<IntPtr> binder = null)
        {
            if (binder != null)
            {
                IntPtr singleStmt;
                int rcSingle = Winsqlite3Native.sqlite3_prepare_v2(_db, Winsqlite3Native.ToUtf8NullTerminated(sql), -1, out singleStmt, IntPtr.Zero);
                if (rcSingle == Winsqlite3Native.SQLITE_OK && singleStmt != IntPtr.Zero)
                {
                    try
                    {
                        binder(singleStmt);
                        Winsqlite3Native.sqlite3_step(singleStmt);
                    }
                    finally
                    {
                        Winsqlite3Native.sqlite3_finalize(singleStmt);
                    }
                }
                return;
            }

            string[] statements = sql.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string raw in statements)
            {
                string trimmed = raw.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                IntPtr stmt;
                int rc = Winsqlite3Native.sqlite3_prepare_v2(_db, Winsqlite3Native.ToUtf8NullTerminated(trimmed + ";"), -1, out stmt, IntPtr.Zero);
                if (rc != Winsqlite3Native.SQLITE_OK || stmt == IntPtr.Zero)
                    continue;
                try
                {
                    if (binder != null) binder(stmt);
                    Winsqlite3Native.sqlite3_step(stmt);
                }
                finally
                {
                    Winsqlite3Native.sqlite3_finalize(stmt);
                }
            }
        }

        public int ExecuteScalarInt(string sql, Action<IntPtr> binder = null)
        {
            IntPtr stmt;
            int rc = Winsqlite3Native.sqlite3_prepare_v2(_db, Winsqlite3Native.ToUtf8NullTerminated(sql), -1, out stmt, IntPtr.Zero);
            if (rc != Winsqlite3Native.SQLITE_OK || stmt == IntPtr.Zero) return 0;
            try
            {
                if (binder != null) binder(stmt);
                if (Winsqlite3Native.sqlite3_step(stmt) == Winsqlite3Native.SQLITE_ROW)
                    return Winsqlite3Native.sqlite3_column_int(stmt, 0);
                return 0;
            }
            finally
            {
                Winsqlite3Native.sqlite3_finalize(stmt);
            }
        }

        public void Dispose()
        {
            if (_db != IntPtr.Zero)
            {
                ExecuteNonQuery("PRAGMA wal_checkpoint(TRUNCATE);");
                Winsqlite3Native.sqlite3_close_v2(_db);
                _db = IntPtr.Zero;
            }
        }
    }
}
