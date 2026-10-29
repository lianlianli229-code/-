using System;
using System.Collections.Generic;

namespace StickyTickets.Data
{
    public enum UrgencyState
    {
        Green = 0,
        Yellow = 1,
        Orange = 2,
        Red = 3
    }

    public class TicketStepRecord
    {
        public long Id { get; set; }
        public long TicketId { get; set; }
        public int SortOrder { get; set; }
        public string StepType { get; set; }
        public string Content { get; set; }
        public bool IsCompleted { get; set; }

        public TicketStepRecord()
        {
            StepType = "core";
            Content = "";
        }
    }

    public class TicketRecord
    {
        public long Id { get; set; }
        public string TicketNo { get; set; }
        public string Title { get; set; }
        public string ScratchNotes { get; set; }
        public double PosX { get; set; }
        public double PosY { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public bool IsDockedOnRail { get; set; }
        public int RailOrder { get; set; }
        public bool IsOrderLocked { get; set; }
        public bool IsClickThrough { get; set; }
        public string Status { get; set; }
        public int ServedScore { get; set; }
        public int TipEarned { get; set; }
        public long CreatedAtUnix { get; set; }
        public long StartTimeUnix { get; set; }
        public long DeadlineUnix { get; set; }
        public List<TicketStepRecord> Steps { get; set; }

        public TicketRecord()
        {
            TicketNo = "#001";
            Title = "新建待办小票";
            ScratchNotes = "";
            PosX = 120;
            PosY = 90;
            Width = 360;
            Height = 420;
            IsDockedOnRail = true;
            Status = "Active";
            Steps = new List<TicketStepRecord>();
        }

        public bool IsStickyNote
        {
            get
            {
                return !string.IsNullOrEmpty(TicketNo) &&
                       (TicketNo.StartsWith("📝") || TicketNo.StartsWith("NOTE", StringComparison.OrdinalIgnoreCase));
            }
        }

        public UrgencyState ComputeUrgency(long nowUnix)
        {
            if (IsStickyNote) return UrgencyState.Green;
            if (DeadlineUnix <= 0 || StartTimeUnix <= 0) return UrgencyState.Green;
            long remainingSec = DeadlineUnix - nowUnix;
            if (remainingSec <= 0) return UrgencyState.Red;

            long totalSec = Math.Max(60, DeadlineUnix - StartTimeUnix);
            double ratio = (double)remainingSec / totalSec;

            UrgencyState proportionalState = UrgencyState.Green;
            if (ratio <= 0.15) proportionalState = UrgencyState.Orange;
            else if (ratio <= 0.50) proportionalState = UrgencyState.Yellow;

            if (totalSec > 3600 && remainingSec <= 1800)
            {
                if (UrgencyState.Orange > proportionalState)
                    return UrgencyState.Orange;
            }
            return proportionalState;
        }

        public string FormatRemainingTime(long nowUnix, out string accessibilityShapeLabel)
        {
            if (IsStickyNote)
            {
                accessibilityShapeLabel = "📝 笔记";
                return "便签";
            }
            UrgencyState u = ComputeUrgency(nowUnix);
            switch (u)
            {
                case UrgencyState.Red: accessibilityShapeLabel = "■ 催单"; break;
                case UrgencyState.Orange: accessibilityShapeLabel = "◆ 紧迫"; break;
                case UrgencyState.Yellow: accessibilityShapeLabel = "▲ 提醒"; break;
                default: accessibilityShapeLabel = "● 充裕"; break;
            }

            if (Status == "Served") return "已出餐";
            if (DeadlineUnix <= 0) return "不限时";
            long rem = DeadlineUnix - nowUnix;
            if (rem <= 0) return "已超时!";
            TimeSpan ts = TimeSpan.FromSeconds(rem);
            if (ts.TotalHours >= 1)
                return string.Format("{0:D2}:{1:D2}:{2:D2}", (int)ts.TotalHours, ts.Minutes, ts.Seconds);
            return string.Format("{0:D2}:{1:D2}", ts.Minutes, ts.Seconds);
        }
    }

    public class TipLedgerRecord
    {
        public long Id { get; set; }
        public long TicketId { get; set; }
        public int Delta { get; set; }
        public string Reason { get; set; }
        public long CreatedAtUnix { get; set; }

        public TipLedgerRecord()
        {
            Reason = "";
        }
    }

    public class UserEconomyRecord
    {
        public int TotalTips { get; set; }
        public bool HideTipJarOnUi { get; set; }
        public string WishTitle { get; set; }
        public int WishTargetTips { get; set; }
        public string ActiveSkin { get; set; }
        public bool FirstRunGuided { get; set; }
        public bool OnboardingNoticeAcked { get; set; }
        public int ReminderMinutes { get; set; }

        public UserEconomyRecord()
        {
            WishTitle = "周末请自己吃顿真汉堡!";
            WishTargetTips = 150;
            ActiveSkin = "BurgerShop";
            ReminderMinutes = 30;
        }
    }

    public class RecipeRecord
    {
        public long Id { get; set; }
        public string Keywords { get; set; }
        public string StepsText { get; set; }

        public RecipeRecord()
        {
            Keywords = "";
            StepsText = "";
        }
    }
}
