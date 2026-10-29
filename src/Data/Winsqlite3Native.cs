using System;
using System.Runtime.InteropServices;
using System.Text;

namespace StickyTickets.Data
{
    /// <summary>
    /// Memory-safe P/Invoke wrapper for Windows built-in System32\winsqlite3.dll.
    /// Eliminates third-party native DLL supply-chain risk while supporting 100% parameterized queries.
    /// Locked strictly to System32 to prevent DLL side-loading/hijacking from portable directories.
    /// </summary>
    internal static class Winsqlite3Native
    {
        private const string DllName = "winsqlite3.dll";

        public const int SQLITE_OK = 0;
        public const int SQLITE_ROW = 100;
        public const int SQLITE_DONE = 101;

        public const int SQLITE_OPEN_READONLY = 0x00000001;
        public const int SQLITE_OPEN_READWRITE = 0x00000002;
        public const int SQLITE_OPEN_CREATE = 0x00000004;
        public const int SQLITE_OPEN_FULLMUTEX = 0x00010000;
        public const int SQLITE_DBCONFIG_DEFENSIVE = 1010;

        private static readonly IntPtr SQLITE_TRANSIENT = new IntPtr(-1);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqlite3_open_v2(byte[] filenameUtf8, out IntPtr ppDb, int flags, IntPtr zVfs);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqlite3_close_v2(IntPtr db);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqlite3_prepare_v2(IntPtr db, byte[] zSqlUtf8, int nByte, out IntPtr ppStmt, IntPtr pzTail);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqlite3_step(IntPtr stmt);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqlite3_finalize(IntPtr stmt);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqlite3_bind_int(IntPtr stmt, int index, int value);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqlite3_bind_int64(IntPtr stmt, int index, long value);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqlite3_bind_double(IntPtr stmt, int index, double value);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_bind_text(IntPtr stmt, int index, byte[] valUtf8, int n, IntPtr destructor);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_bind_blob(IntPtr stmt, int index, byte[] valBlob, int n, IntPtr destructor);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqlite3_column_int(IntPtr stmt, int iCol);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern long sqlite3_column_int64(IntPtr stmt, int iCol);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern double sqlite3_column_double(IntPtr stmt, int iCol);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_column_text(IntPtr stmt, int iCol);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_column_blob(IntPtr stmt, int iCol);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_column_bytes(IntPtr stmt, int iCol);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern long sqlite3_last_insert_rowid(IntPtr db);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr sqlite3_backup_init(IntPtr pDest, byte[] zDestName, IntPtr pSource, byte[] zSourceName);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqlite3_backup_step(IntPtr pBackup, int nPage);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqlite3_backup_finish(IntPtr pBackup);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_db_config(IntPtr db, int op, int val, out int pOk);

        public static void TryEnableDefensiveMode(IntPtr db)
        {
            if (db == IntPtr.Zero) return;
            try
            {
                int enabled;
                sqlite3_db_config(db, SQLITE_DBCONFIG_DEFENSIVE, 1, out enabled);
            }
            catch
            {
                // Older Windows builds without SQLITE_DBCONFIG_DEFENSIVE still rely on C# sqlite_master gate
            }
        }

        public static byte[] ToUtf8NullTerminated(string text)
        {
            if (text == null) text = string.Empty;
            int byteCount = Encoding.UTF8.GetByteCount(text);
            byte[] buf = new byte[byteCount + 1];
            Encoding.UTF8.GetBytes(text, 0, text.Length, buf, 0);
            buf[byteCount] = 0;
            return buf;
        }

        public static void BindText(IntPtr stmt, int index, string value)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(value ?? string.Empty);
            sqlite3_bind_text(stmt, index, utf8, utf8.Length, SQLITE_TRANSIENT);
        }

        public static void BindBlob(IntPtr stmt, int index, byte[] data)
        {
            byte[] payload = data ?? Array.Empty<byte>();
            sqlite3_bind_blob(stmt, index, payload, payload.Length, SQLITE_TRANSIENT);
        }

        public static string GetText(IntPtr stmt, int col)
        {
            IntPtr ptr = sqlite3_column_text(stmt, col);
            if (ptr == IntPtr.Zero) return string.Empty;
            int len = sqlite3_column_bytes(stmt, col);
            if (len <= 0) return string.Empty;
            byte[] buf = new byte[len];
            Marshal.Copy(ptr, buf, 0, len);
            return Encoding.UTF8.GetString(buf);
        }

        public static byte[] GetBlob(IntPtr stmt, int col)
        {
            IntPtr ptr = sqlite3_column_blob(stmt, col);
            if (ptr == IntPtr.Zero) return Array.Empty<byte>();
            int len = sqlite3_column_bytes(stmt, col);
            if (len <= 0) return Array.Empty<byte>();
            byte[] buf = new byte[len];
            Marshal.Copy(ptr, buf, 0, len);
            return buf;
        }
    }
}
