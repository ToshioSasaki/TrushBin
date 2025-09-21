using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace TrushBin
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                TrushLogic.EnsureTrushDir();

                // 初回セットアップ（ショートカット作成）
                if (args.Any(a => a.Equals("--setup", StringComparison.OrdinalIgnoreCase) ||
                                  a.Equals("--setup:2", StringComparison.OrdinalIgnoreCase) ||
                                  a.Equals("--twoshortcuts", StringComparison.OrdinalIgnoreCase)))
                {
                    bool two = args.Any(a => a.Equals("--setup:2", StringComparison.OrdinalIgnoreCase) ||
                                             a.Equals("--twoshortcuts", StringComparison.OrdinalIgnoreCase));
                    ShortcutHelper.CreateOrUpdateShortcuts(two);
                    TrushLogic.UpdateAllShortcutIcons();
                    MessageBox.Show(two ? "デスクトップにショートカットを2つ作成しました。" :
                                          "デスクトップにショートカットを作成しました。",
                                    "TrushBin");
                    return;
                }

                // 空にする
                if (args.Any(a => a.Equals("--empty", StringComparison.OrdinalIgnoreCase)))
                {
                    var totalNow = TrushLogic.CountEntries(TrushLogic.TrushPath);
                    var confirm = MessageBox.Show(
                        $"ゴミ箱を永久的に削除します。よろしいですか？\n現在 {totalNow} 件",
                        "TrushBin", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);

                    if (confirm == DialogResult.OK)
                    {
                        int f, d;
                        int total = TrushLogic.EmptyTrush(out f, out d);
                        MessageBox.Show($"削除しました。{total} 件（ファイル {f} / フォルダ {d}）", "TrushBin");
                    }

                    TrushLogic.UpdateAllShortcutIcons();
                    return;
                }

                // ドロップ（ショートカットへD&D）または引数でパスを受けた場合
                var paths = args.Where(a => !a.StartsWith("--")).ToArray();
                if (paths.Length > 0)
                {
                    int moved = TrushLogic.MoveIntoTrush(paths);
                    MessageBox.Show($"移動しました。{moved} 件\n→ {TrushLogic.TrushPath}", "TrushBin");
                    TrushLogic.UpdateAllShortcutIcons();
                    return;
                }

                // 引数なし：1アイコン運用の簡易メニュー
                var count = TrushLogic.CountEntries(TrushLogic.TrushPath);
                var result = MessageBox.Show(
                    $"どうする？\n\n[はい]：空にする（現在 {count} 件）\n[いいえ]：フォルダを開く\n[キャンセル]：閉じる",
                    "TrushBin", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    var confirm = MessageBox.Show(
                        $"ゴミ箱を永久的に削除します。よろしいですか？\n現在 {count} 件",
                        "TrushBin", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                    if (confirm == DialogResult.OK)
                    {
                        int f, d;
                        int total = TrushLogic.EmptyTrush(out f, out d);
                        MessageBox.Show($"削除しました。{total} 件（ファイル {f} / フォルダ {d}）", "TrushBin");
                    }
                    TrushLogic.UpdateAllShortcutIcons();
                    return;
                }
                else if (result == DialogResult.No)
                {
                    System.Diagnostics.Process.Start("explorer.exe", TrushLogic.TrushPath);
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("エラー: " + ex.Message, "TrushBin", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    internal static class TrushLogic
    {
        public static readonly string TrushPath = @"C:\trush"; // ご希望どおりの表記

        public static void EnsureTrushDir()
        {
            if (!Directory.Exists(TrushPath))
                Directory.CreateDirectory(TrushPath);
        }

        public static int MoveIntoTrush(string[] inputPaths)
        {
            EnsureTrushDir();
            int moved = 0;
            var failures = new List<string>();

            foreach (var raw in inputPaths)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                var p = raw.Trim().Trim('"');

                try
                {
                    if (File.Exists(p))
                    {
                        var dest = UniqueDestination(Path.Combine(TrushPath, Path.GetFileName(p)));
                        MoveFileSafe(p, dest);
                        if (!File.Exists(p)) moved++; else failures.Add(p);
                    }
                    else if (Directory.Exists(p))
                    {
                        var dest = UniqueDestination(Path.Combine(TrushPath, new DirectoryInfo(p).Name));
                        MoveDirectorySafe(p, dest);
                        if (!Directory.Exists(p)) moved++; else failures.Add(p);
                    }
                }
                catch
                {
                    failures.Add(p);
                }
            }

            if (failures.Count > 0)
            {
                MessageBox.Show(
                    "移動できなかった項目（使用中/権限/特殊パスの可能性）:\n" +
                    string.Join("\n", failures.Take(10)) +
                    (failures.Count > 10 ? $"\n…ほか {failures.Count - 10} 件" : ""),
                    "TrushBin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            return moved;
        }

        // ---------- フォルダも確実に空にする版 ----------
        public static int EmptyTrush() => EmptyTrush(out _, out _);

        public static int EmptyTrush(out int filesDeleted, out int dirsDeleted)
        {
            filesDeleted = 0;
            dirsDeleted = 0;

            if (!Directory.Exists(TrushPath)) return 0;

            // 1) 全ファイル削除（属性を戻してから）
            foreach (var f in Directory.EnumerateFiles(TrushPath, "*", SearchOption.AllDirectories))
            {
                try
                {
                    File.SetAttributes(f, FileAttributes.Normal);
                    File.Delete(f);
                    filesDeleted++;
                }
                catch { /* 続行 */ }
            }

            // 2) 全ディレクトリを“深い順”に削除（入れ子を確実に除去）
            var allDirsDeepFirst = Directory
                .EnumerateDirectories(TrushPath, "*", SearchOption.AllDirectories)
                .OrderByDescending(d => d.Length);

            foreach (var d in allDirsDeepFirst)
            {
                try
                {
                    var di = new DirectoryInfo(d);
                    di.Attributes = FileAttributes.Normal;
                    Directory.Delete(d, recursive: false); // 中身は既に空のはず
                    dirsDeleted++;
                }
                catch { /* 続行 */ }
            }

            // 3) 念のため直下に残った空ディレクトリがあれば最後に一掃
            foreach (var d in Directory.EnumerateDirectories(TrushPath, "*", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var di = new DirectoryInfo(d);
                    di.Attributes = FileAttributes.Normal;
                    Directory.Delete(d, recursive: false);
                    dirsDeleted++;
                }
                catch { /* 続行 */ }
            }

            return filesDeleted + dirsDeleted;
        }

        public static int CountEntries(string dir)
        {
            if (!Directory.Exists(dir)) return 0;
            int files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Length;
            int dirs = Directory.GetDirectories(dir, "*", SearchOption.AllDirectories).Length;
            return files + dirs;
        }

        public static void UpdateAllShortcutIcons()
        {
            try
            {
                bool hasItems = CountEntries(TrushPath) > 0;
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string exePath = Application.ExecutablePath;

                string emptyIco = Path.Combine(Path.GetDirectoryName(exePath)!, "empty.ico");
                string fullIco = Path.Combine(Path.GetDirectoryName(exePath)!, "full.ico");

                var mainLnk = Path.Combine(desktop, "ゴミ箱（Trush）.lnk");
                if (File.Exists(mainLnk))
                    ShortcutHelper.SetShortcutIcon(mainLnk, hasItems ? fullIco : emptyIco);
            }
            catch { /* 致命ではないので無視 */ }
        }

        // ---------- 内部ユーティリティ ----------

        private static bool IsCrossVolume(string src, string dst)
        {
            string r1 = Path.GetPathRoot(Path.GetFullPath(src))!;
            string r2 = Path.GetPathRoot(Path.GetFullPath(dst))!;
            return !string.Equals(r1, r2, StringComparison.OrdinalIgnoreCase);
        }

        private static void MoveFileSafe(string src, string dst)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
            try
            {
                if (!IsCrossVolume(src, dst))
                {
                    File.Move(src, dst); // 同一ボリュームは原子的移動
                }
                else
                {
                    File.Copy(src, dst, overwrite: false);
                    File.SetAttributes(src, FileAttributes.Normal);
                    File.Delete(src);   // 別ボリューム：コピー→元削除
                }
            }
            catch (IOException)
            {
                // 念のためフォールバック
                File.Copy(src, dst, overwrite: false);
                File.SetAttributes(src, FileAttributes.Normal);
                File.Delete(src);
            }
        }

        private static void CopyDirectoryRecursive(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var f in Directory.GetFiles(sourceDir))
            {
                var df = Path.Combine(destDir, Path.GetFileName(f));
                File.Copy(f, df, overwrite: false);
            }
            foreach (var d in Directory.GetDirectories(sourceDir))
            {
                CopyDirectoryRecursive(d, Path.Combine(destDir, Path.GetFileName(d)));
            }
        }

        private static void MoveDirectorySafe(string src, string dst)
        {
            try
            {
                if (!IsCrossVolume(src, dst))
                {
                    Directory.Move(src, dst);
                }
                else
                {
                    CopyDirectoryRecursive(src, dst);
                    Directory.Delete(src, recursive: true);
                }
            }
            catch (IOException)
            {
                CopyDirectoryRecursive(src, dst);
                Directory.Delete(src, recursive: true);
            }
        }

        private static string UniqueDestination(string destPath)
        {
            if (!File.Exists(destPath) && !Directory.Exists(destPath)) return destPath;

            string dir = Path.GetDirectoryName(destPath)!;
            string baseName = Path.GetFileNameWithoutExtension(destPath);
            string ext = Path.GetExtension(destPath);

            for (int i = 1; i < 10000; i++)
            {
                string trial = Path.Combine(dir, $"{baseName} ({i}){ext}");
                if (!File.Exists(trial) && !Directory.Exists(trial))
                    return trial;
            }
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmssfff");
            return Path.Combine(dir, $"{baseName}_{stamp}{ext}");
        }
    }

    internal static class ShortcutHelper
    {
        /// <summary>
        /// デスクトップにショートカットを作成/更新。
        /// twoShortcuts = true のときのみ「空にする」専用も作る。
        /// </summary>
        public static void CreateOrUpdateShortcuts(bool twoShortcuts = false)
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string exe = Application.ExecutablePath;

            string mainLnk = Path.Combine(desktop, "ゴミ箱（Trush）.lnk");
            string emptyLnk = Path.Combine(desktop, "ゴミ箱を空にする.lnk");

            string emptyIco = Path.Combine(Path.GetDirectoryName(exe)!, "empty.ico");

            CreateOrUpdateShortcut(mainLnk, exe, "", emptyIco);

            if (twoShortcuts)
                CreateOrUpdateShortcut(emptyLnk, exe, "--empty", emptyIco);
        }

        public static void SetShortcutIcon(string lnkPath, string iconPath)
        {
            if (!File.Exists(lnkPath) || !File.Exists(iconPath)) return;

            IShellLinkW link = (IShellLinkW)new ShellLink();
            ((IPersistFile)link).Load(lnkPath, 0);
            link.SetIconLocation(iconPath, 0);
            ((IPersistFile)link).Save(lnkPath, true);
        }

        private static void CreateOrUpdateShortcut(string lnkPath, string targetExe, string args, string iconPath)
        {
            IShellLinkW link = (IShellLinkW)new ShellLink();
            link.SetPath(targetExe);
            link.SetArguments(args);
            link.SetWorkingDirectory(Path.GetDirectoryName(targetExe));
            link.SetIconLocation(File.Exists(iconPath) ? iconPath : targetExe, 0);
            link.SetDescription("ドラッグ＆ドロップで C:\\trush へ移動 / 実行で操作メニュー");

            IPersistFile file = (IPersistFile)link;
            file.Save(lnkPath, true);
        }

        // --------- COM Interop 定義 ---------
        [ComImport]
        [Guid("00021401-0000-0000-C000-000000000046")]
        private class ShellLink { }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        private interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short wHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int iShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int iIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
            void Resolve(IntPtr hwnd, uint fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport]
        [Guid("0000010B-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPersistFile
        {
            void GetClassID(out Guid pClassID);
            [PreserveSig] int IsDirty();
            void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
            void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, bool fRemember);
            void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
            void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
        }
    }
}
