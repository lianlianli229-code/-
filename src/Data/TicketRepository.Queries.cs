using System;
using System.Collections.Generic;
using System.IO;

namespace StickyTickets.Data
{
    public partial class TicketRepository
    {
        public List<TicketRecord> GetActiveTickets()
        {
            List<TicketRecord> list = new List<TicketRecord>();
            string sql = @"
                SELECT t.Id, t.TicketNo, t.Title, t.ScratchNotes, t.PosX, t.PosY, t.Width, t.Height,
                       t.IsDockedOnRail, t.RailOrder, t.IsOrderLocked, t.IsClickThrough,
                       t.Status, t.ServedScore, t.TipEarned, t.CreatedAtUnix,
                       COALESCE(a.StartTimeUnix, 0), COALESCE(a.DeadlineUnix, 0)
                FROM Tickets t
                LEFT JOIN TicketAlarms a ON a.TicketId = t.Id
                WHERE t.Status != 'Trash'
                ORDER BY t.RailOrder ASC, t.Id ASC;";

            IntPtr stmt;
            if (Winsqlite3Native.sqlite3_prepare_v2(_db, Winsqlite3Native.ToUtf8NullTerminated(sql), -1, out stmt, IntPtr.Zero) == Winsqlite3Native.SQLITE_OK)
            {
                try
                {
                    while (Winsqlite3Native.sqlite3_step(stmt) == Winsqlite3Native.SQLITE_ROW)
                    {
                        TicketRecord r = new TicketRecord
                        {
                            Id = Winsqlite3Native.sqlite3_column_int64(stmt, 0),
                            TicketNo = Winsqlite3Native.GetText(stmt, 1),
                            Title = Winsqlite3Native.GetText(stmt, 2),
                            ScratchNotes = Winsqlite3Native.GetText(stmt, 3),
                            PosX = Winsqlite3Native.sqlite3_column_double(stmt, 4),
                            PosY = Winsqlite3Native.sqlite3_column_double(stmt, 5),
                            Width = Winsqlite3Native.sqlite3_column_double(stmt, 6),
                            Height = Winsqlite3Native.sqlite3_column_double(stmt, 7),
                            IsDockedOnRail = Winsqlite3Native.sqlite3_column_int(stmt, 8) != 0,
                            RailOrder = Winsqlite3Native.sqlite3_column_int(stmt, 9),
                            IsOrderLocked = Winsqlite3Native.sqlite3_column_int(stmt, 10) != 0,
                            IsClickThrough = Winsqlite3Native.sqlite3_column_int(stmt, 11) != 0,
                            Status = Winsqlite3Native.GetText(stmt, 12),
                            ServedScore = Winsqlite3Native.sqlite3_column_int(stmt, 13),
                            TipEarned = Winsqlite3Native.sqlite3_column_int(stmt, 14),
                            CreatedAtUnix = Winsqlite3Native.sqlite3_column_int64(stmt, 15),
                            StartTimeUnix = Winsqlite3Native.sqlite3_column_int64(stmt, 16),
                            DeadlineUnix = Winsqlite3Native.sqlite3_column_int64(stmt, 17)
                        };
                        list.Add(r);
                    }
                }
                finally
                {
                    Winsqlite3Native.sqlite3_finalize(stmt);
                }
            }

            foreach (TicketRecord t in list)
            {
                t.Steps = GetStepsForTicket(t.Id);
            }
            return list;
        }

        public List<TicketStepRecord> GetStepsForTicket(long ticketId)
        {
            List<TicketStepRecord> steps = new List<TicketStepRecord>();
            string sql = "SELECT Id, TicketId, SortOrder, StepType, Content, IsCompleted FROM TicketSteps WHERE TicketId = ?1 ORDER BY SortOrder ASC, Id ASC;";
            IntPtr stmt;
            if (Winsqlite3Native.sqlite3_prepare_v2(_db, Winsqlite3Native.ToUtf8NullTerminated(sql), -1, out stmt, IntPtr.Zero) == Winsqlite3Native.SQLITE_OK)
            {
                try
                {
                    Winsqlite3Native.sqlite3_bind_int64(stmt, 1, ticketId);
                    while (Winsqlite3Native.sqlite3_step(stmt) == Winsqlite3Native.SQLITE_ROW)
                    {
                        steps.Add(new TicketStepRecord
                        {
                            Id = Winsqlite3Native.sqlite3_column_int64(stmt, 0),
                            TicketId = Winsqlite3Native.sqlite3_column_int64(stmt, 1),
                            SortOrder = Winsqlite3Native.sqlite3_column_int(stmt, 2),
                            StepType = ValidateStepType(Winsqlite3Native.GetText(stmt, 3)),
                            Content = Winsqlite3Native.GetText(stmt, 4),
                            IsCompleted = Winsqlite3Native.sqlite3_column_int(stmt, 5) != 0
                        });
                    }
                }
                finally
                {
                    Winsqlite3Native.sqlite3_finalize(stmt);
                }
            }
            return steps;
        }

        public static string ValidateStepType(string stepType)
        {
            switch (stepType)
            {
                case "core":
                case "prep":
                case "comm":
                case "warn":
                    return stepType;
                default:
                    return "core";
            }
        }

        public void SaveTicket(TicketRecord t)
        {
            if (t == null) return;
            if (t.CreatedAtUnix <= 0) t.CreatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            if (t.Id <= 0)
            {
                string ins = @"
                    INSERT INTO Tickets (TicketNo, Title, ScratchNotes, PosX, PosY, Width, Height, IsDockedOnRail, RailOrder, IsOrderLocked, IsClickThrough, Status, ServedScore, TipEarned, CreatedAtUnix)
                    VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9, ?10, ?11, ?12, ?13, ?14, ?15);";
                ExecuteNonQuery(ins, stmt =>
                {
                    Winsqlite3Native.BindText(stmt, 1, t.TicketNo);
                    Winsqlite3Native.BindText(stmt, 2, t.Title);
                    Winsqlite3Native.BindText(stmt, 3, t.ScratchNotes);
                    Winsqlite3Native.sqlite3_bind_double(stmt, 4, t.PosX);
                    Winsqlite3Native.sqlite3_bind_double(stmt, 5, t.PosY);
                    Winsqlite3Native.sqlite3_bind_double(stmt, 6, t.Width);
                    Winsqlite3Native.sqlite3_bind_double(stmt, 7, t.Height);
                    Winsqlite3Native.sqlite3_bind_int(stmt, 8, t.IsDockedOnRail ? 1 : 0);
                    Winsqlite3Native.sqlite3_bind_int(stmt, 9, t.RailOrder);
                    Winsqlite3Native.sqlite3_bind_int(stmt, 10, t.IsOrderLocked ? 1 : 0);
                    Winsqlite3Native.sqlite3_bind_int(stmt, 11, t.IsClickThrough ? 1 : 0);
                    Winsqlite3Native.BindText(stmt, 12, t.Status);
                    Winsqlite3Native.sqlite3_bind_int(stmt, 13, t.ServedScore);
                    Winsqlite3Native.sqlite3_bind_int(stmt, 14, t.TipEarned);
                    Winsqlite3Native.sqlite3_bind_int64(stmt, 15, t.CreatedAtUnix);
                });
                t.Id = Winsqlite3Native.sqlite3_last_insert_rowid(_db);
            }
            else
            {
                string upd = @"
                    UPDATE Tickets SET TicketNo=?1, Title=?2, ScratchNotes=?3, PosX=?4, PosY=?5, Width=?6, Height=?7,
                        IsDockedOnRail=?8, RailOrder=?9, IsOrderLocked=?10, IsClickThrough=?11, Status=?12, ServedScore=?13, TipEarned=?14
                    WHERE Id=?15;";
                ExecuteNonQuery(upd, stmt =>
                {
                    Winsqlite3Native.BindText(stmt, 1, t.TicketNo);
                    Winsqlite3Native.BindText(stmt, 2, t.Title);
                    Winsqlite3Native.BindText(stmt, 3, t.ScratchNotes);
                    Winsqlite3Native.sqlite3_bind_double(stmt, 4, t.PosX);
                    Winsqlite3Native.sqlite3_bind_double(stmt, 5, t.PosY);
                    Winsqlite3Native.sqlite3_bind_double(stmt, 6, t.Width);
                    Winsqlite3Native.sqlite3_bind_double(stmt, 7, t.Height);
                    Winsqlite3Native.sqlite3_bind_int(stmt, 8, t.IsDockedOnRail ? 1 : 0);
                    Winsqlite3Native.sqlite3_bind_int(stmt, 9, t.RailOrder);
                    Winsqlite3Native.sqlite3_bind_int(stmt, 10, t.IsOrderLocked ? 1 : 0);
                    Winsqlite3Native.sqlite3_bind_int(stmt, 11, t.IsClickThrough ? 1 : 0);
                    Winsqlite3Native.BindText(stmt, 12, t.Status);
                    Winsqlite3Native.sqlite3_bind_int(stmt, 13, t.ServedScore);
                    Winsqlite3Native.sqlite3_bind_int(stmt, 14, t.TipEarned);
                    Winsqlite3Native.sqlite3_bind_int64(stmt, 15, t.Id);
                });
            }

            ExecuteNonQuery(@"
                INSERT INTO TicketAlarms (TicketId, StartTimeUnix, DeadlineUnix) VALUES (?1, ?2, ?3)
                ON CONFLICT(TicketId) DO UPDATE SET StartTimeUnix=excluded.StartTimeUnix, DeadlineUnix=excluded.DeadlineUnix;",
                stmt =>
                {
                    Winsqlite3Native.sqlite3_bind_int64(stmt, 1, t.Id);
                    Winsqlite3Native.sqlite3_bind_int64(stmt, 2, t.StartTimeUnix);
                    Winsqlite3Native.sqlite3_bind_int64(stmt, 3, t.DeadlineUnix);
                });

            ExecuteNonQuery("DELETE FROM TicketSteps WHERE TicketId = ?1;",
                stmt => Winsqlite3Native.sqlite3_bind_int64(stmt, 1, t.Id));

            for (int i = 0; i < t.Steps.Count; i++)
            {
                TicketStepRecord st = t.Steps[i];
                st.SortOrder = i;
                st.StepType = ValidateStepType(st.StepType);
                ExecuteNonQuery("INSERT INTO TicketSteps (TicketId, SortOrder, StepType, Content, IsCompleted) VALUES (?1, ?2, ?3, ?4, ?5);",
                    stmt =>
                    {
                        Winsqlite3Native.sqlite3_bind_int64(stmt, 1, t.Id);
                        Winsqlite3Native.sqlite3_bind_int(stmt, 2, st.SortOrder);
                        Winsqlite3Native.BindText(stmt, 3, st.StepType);
                        Winsqlite3Native.BindText(stmt, 4, st.Content);
                        Winsqlite3Native.sqlite3_bind_int(stmt, 5, st.IsCompleted ? 1 : 0);
                    });
            }
        }
    }
}
