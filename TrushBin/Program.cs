using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace TrushBin
{
    internal static class Program
    {
        /// <summary>
        /// 引数：
        ///   --setup        : デスクトップにメインショートカット作成
        ///   --setup:2      : メイン + 「空にする」専用ショートカット作成
        ///   --twoshortcuts : --setup:2 と同義
        ///   --empty        : C:\trushbin をセキュア消去（進捗表示）
        ///   [パス群]       : 引数がパスのみの場合は C:\trushbin へ移動
        ///   （引数なし）   : 簡易メニュー表示
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                TrushLogic.EnsureTrushDir();

                // セットアップ（ショートカット作成）
                //   --setup      : 1個
                //   --setup:2    : 2個（+「空にする」）
                //   --twoshortcuts : --setup:2 と同義
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

                // 空にする（安全消去：プログレス付き）
                //   --empty : セキュア消去（ランダム→ゼロの2パス、ADS対応、進捗表示）
                if (args.Any(a => a.Equals("--empty", StringComparison.OrdinalIgnoreCase)))
                {
                    var totalNow = TrushLogic.CountEntries(TrushLogic.TrushPath);
                    var confirm = MessageBox.Show(
                        $"ゴミ箱を完全消去（復旧困難化）します。よろしいですか？\n現在 {totalNow} 件",
                        "TrushBin", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);

                    if (confirm == DialogResult.OK)
                    {
                        int f, d;
                        TrushLogic.EmptyTrushSecureWithProgress(out f, out d);   // ← 進捗表示版
                        MessageBox.Show($"完全消去しました。ファイル {f} / フォルダ {d}", "TrushBin");
                    }

                    TrushLogic.UpdateAllShortcutIcons();
                    return;
                }

                // ドロップ（引数でパスを受けた場合）: C:\trushbin へ移動
                var paths = args.Where(a => !a.StartsWith("--")).ToArray();
                if (paths.Length > 0)
                {
                    int moved = TrushLogic.MoveIntoTrush(paths);
                    MessageBox.Show($"移動しました。{moved} 件\n→ {TrushLogic.TrushPath}", "TrushBin");
                    TrushLogic.UpdateAllShortcutIcons();
                    return;
                }

                // 引数なし：簡易メニュー
                var count = TrushLogic.CountEntries(TrushLogic.TrushPath);
                var result = MessageBox.Show(
                    $"どうする？\n\n[はい]：完全消去（現在 {count} 件）\n[いいえ]：フォルダを開く\n[キャンセル]：閉じる",
                    "TrushBin", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    var confirm2 = MessageBox.Show(
                        $"ゴミ箱を完全消去（復旧困難化）します。よろしいですか？\n現在 {count} 件",
                        "TrushBin", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                    if (confirm2 == DialogResult.OK)
                    {
                        int f, d;
                        // 進捗表示版
                        TrushLogic.EmptyTrushSecureWithProgress(out f, out d);   
                        MessageBox.Show($"完全消去しました。ファイル {f} / フォルダ {d}", "TrushBin");
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

    /// <summary>
    /// 進捗表示フォーム
    /// </summary>
    internal class ProgressForm : Form
    {
        private ProgressBar bar;
        private Label lblTitle;
        private Label lblDetail;

        // 右上 × 対応のための制御フラグ
        // PauseRequested=true で処理側は一時停止し、Continue/Restore のどちらかで解除
        public volatile bool PauseRequested = false;
        public volatile bool ContinueRequested = false; // 続行（はい）
        public volatile bool RestoreRequested = false;  // 中断（いいえ）
        public bool YesMeansContinue = true;            //「はい＝続行 / いいえ＝中断」に固定（要件どおり）

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public ProgressForm()
        {
            Text = "完全消去中…";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = true;
            Width = 520;
            Height = 160;

            lblTitle = new Label { Left = 14, Top = 12, Width = 480, Text = "準備中…" };
            bar = new ProgressBar { Left = 14, Top = 40, Width = 480, Height = 22, Style = ProgressBarStyle.Continuous, Minimum = 0, Maximum = 100 };
            lblDetail = new Label { Left = 14, Top = 70, Width = 480, Height = 40, Text = "" };

            Controls.Add(lblTitle);
            Controls.Add(bar);
            Controls.Add(lblDetail);
        }

        /// <summary>
        /// プログレス更新
        /// </summary>
        /// <param name="percent">進捗率</param>
        /// <param name="title">プログレスバータイトル</param>
        /// <param name="detail">プログレスバー明細文字</param>
        public void SetProgress(int percent, string title, string detail)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => SetProgress(percent, title, detail)));
                return;
            }
            bar.Value = Math.Max(0, Math.Min(100, percent));
            lblTitle.Text = title ?? "";
            lblDetail.Text = detail ?? "";
        }

        /// <summary>
        /// プログレスバー見た目だけ逆戻り表示
        /// </summary>
        /// <param name="targetPercent">逆戻り進捗</param>
        public void ReverseTo(int targetPercent)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => ReverseTo(targetPercent)));
                return;
            }
            int cur = bar.Value;
            targetPercent = Math.Max(0, Math.Min(100, targetPercent));
            for (int p = cur; p >= targetPercent; p -= 2)
            {
                bar.Value = p;
                Application.DoEvents();
                Thread.Sleep(10);
            }
        }

        /// <summary>
        /// ×クリック時：ポーズ→確認ダイアログ→続行/中断のいずれかをフラグで通知
        /// </summary>
        /// <param name="e"></param>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true; // ここで閉じさせない（処理側の判断を待つ）
                PauseRequested = true;

                var dr = MessageBox.Show(
                    "ファイル削除するのを辞めますか？",
                    "TrushBin", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (YesMeansContinue)
                {
                    if (dr == DialogResult.Yes)
                    {
                        ContinueRequested = true;
                        RestoreRequested = false;
                        PauseRequested = false;
                    }
                    else
                    {
                        RestoreRequested = true;
                        ContinueRequested = false;
                    }
                }
                else
                {
                    if (dr == DialogResult.Yes)
                    {
                        RestoreRequested = true;
                        ContinueRequested = false;
                    }
                    else
                    {
                        ContinueRequested = true;
                        RestoreRequested = false;
                        PauseRequested = false;
                    }
                }
            }
            else
            {
                base.OnFormClosing(e);
            }
        }
    }

    /// <summary>
    /// C:\trushbin 関連のロジック
    /// </summary>
    internal static class TrushLogic
    {
        /// <summary>
        /// ゴミ箱フォルダのパス
        /// </summary>
        public static readonly string TrushPath = @"C:\trushbin";

        /// <summary>
        /// C:\trushbin フォルダを作成（なければ）
        /// </summary>
        public static void EnsureTrushDir()
        {
            if (!Directory.Exists(TrushPath))
                Directory.CreateDirectory(TrushPath);
        }

        /// <summary>
        /// 指定パス群を C:\trushbin に移動。
        /// 同一ボリューム: Move（原子的）。別ボリューム: Copy → 元をセキュア消去。
        /// </summary>
        /// <param name="inputPaths">入力先パス</param>
        /// <returns>TrushBinに移動したファイル数</returns>
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
                    if (System.IO.File.Exists(p))
                    {
                        var dest = UniqueDestination(Path.Combine(TrushPath, Path.GetFileName(p)));
                        MoveFileSafeWithSourceWipe(p, dest);
                        if (!System.IO.File.Exists(p)) moved++; else failures.Add(p);
                    }
                    else if (Directory.Exists(p))
                    {
                        var dest = UniqueDestination(Path.Combine(TrushPath, new DirectoryInfo(p).Name));
                        MoveDirectorySafeWithSourceWipe(p, dest);
                        if (!Directory.Exists(p)) moved++; else failures.Add(p);
                    }
                }
                catch { failures.Add(p); }
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

        /// <summary>
        /// C:\trushbin をセキュア消去（進捗表示）
        /// - ADSの上書き
        /// - 本体2パス上書き（ランダム→ゼロ）
        /// - タイムスタンプ上書き
        /// - ランダム名へ改名 → 0長に縮小 → 削除
        /// UIの×でポーズ→続行 or 中断（逆戻り表示, 未処理/失敗件数表示）
        /// ファイル→フォルダの順に処理。
        /// </summary>
        /// <param name="filesDeleted">ファイル削除数</param>
        /// <param name="dirsDeleted">フォルダ削除数</param>
        public static void EmptyTrushSecureWithProgress(out int filesDeleted, out int dirsDeleted)
        {
            filesDeleted = 0;
            dirsDeleted = 0;

            if (!Directory.Exists(TrushPath)) return;

            var files = EnumerateFilesNoReparse(TrushPath).ToList();
            var dirsDeep = EnumerateDirectoriesNoReparse(TrushPath).OrderByDescending(d => d.Length).ToList();

            long totalBytes = 0;
            foreach (var f in files)
            {
                try { totalBytes += Math.Max(0, new FileInfo(f).Length) * 2; } catch { }
            }

            using (var pf = new ProgressForm())
            {
                pf.YesMeansContinue = true; // [はい]=続行 / [いいえ]=中断
                pf.Show();
                pf.SetProgress(0, "準備中…", $"対象ファイル {files.Count} / フォルダ {dirsDeep.Count}");

                long processedBytes = 0;
                var fails = new List<string>();

                int localFilesDeleted = 0; // 集計はローカル→最後に out へ
                int localDirsDeleted = 0;

                // 未処理件数表示用（着手数）
                int filesAttempted = 0;
                int dirsAttempted = 0;

                var t = Task.Run(() =>
                {
                    // ファイル
                    foreach (var f in files)
                    {
                        // 一時停止（×）対応
                        while (pf.PauseRequested && !pf.ContinueRequested && !pf.RestoreRequested)
                            Thread.Sleep(30);
                        if (pf.RestoreRequested) return; // 中断

                        Interlocked.Increment(ref filesAttempted);

                        string name = f;
                        try
                        {
                            bool ok = SecureWipe.SecureDeleteFileWithProgress(
                                f,
                                delta =>
                                {
                                    Interlocked.Add(ref processedBytes, delta);
                                    int percent = totalBytes > 0 ? (int)(processedBytes * 100 / totalBytes) : 0;
                                    pf.SetProgress(percent, "完全消去中…", Shorten(name, 70));
                                });

                            if (ok) Interlocked.Increment(ref localFilesDeleted);
                            else lock (fails) fails.Add(f);
                        }
                        catch { lock (fails) fails.Add(f); }
                    }

                    // ディレクトリ（深い順）
                    foreach (var d in dirsDeep)
                    {
                        while (pf.PauseRequested && !pf.ContinueRequested && !pf.RestoreRequested)
                            Thread.Sleep(30);
                        if (pf.RestoreRequested) return;

                        Interlocked.Increment(ref dirsAttempted);

                        try
                        {
                            var parent = Path.GetDirectoryName(d)!;
                            var renamed = d;
                            try
                            {
                                var rnd = SecureWipe.RandomName();
                                renamed = Path.Combine(parent, rnd);
                                Directory.Move(d, renamed);
                            }
                            catch { }

                            try { Directory.Delete(renamed, false); } catch { }
                            if (!Directory.Exists(renamed)) Interlocked.Increment(ref localDirsDeleted);
                            else lock (fails) fails.Add(d);

                            if (totalBytes == 0)
                            {
                                int percent = (int)(100.0 * (dirsDeep.IndexOf(d) + 1) / Math.Max(1, dirsDeep.Count));
                                pf.SetProgress(Math.Min(99, percent), "フォルダ整理中…", Shorten(d, 70));
                            }
                        }
                        catch { lock (fails) fails.Add(d); }
                    }
                });

                // UIを回しつつ完了待ち
                while (!t.IsCompleted)
                {
                    Application.DoEvents();
                    Thread.Sleep(30);
                }

                try { ShellRefresh(TrushPath); } catch { }

                if (pf.RestoreRequested)
                {
                    // 視覚的な逆戻り
                    pf.ReverseTo(0);

                    // 未処理件数（着手していないもの）
                    int remainingFiles = Math.Max(0, files.Count - filesAttempted);
                    int remainingDirs = Math.Max(0, dirsDeep.Count - dirsAttempted);

                    pf.SetProgress(0, "中断しました",
                        $"未処理: ファイル {remainingFiles} / フォルダ {remainingDirs} / 失敗: {fails.Count}");
                    Application.DoEvents();
                    Thread.Sleep(200);
                }
                else
                {
                    pf.SetProgress(100, "最終処理中…", "");
                    Application.DoEvents();
                    Thread.Sleep(80);
                }

                if (!pf.RestoreRequested && localFilesDeleted + localDirsDeleted == 0 && (files.Count + dirsDeep.Count) > 0)
                {
                    MessageBox.Show("消去に失敗した可能性があります。権限/占有/特殊パスを確認してください。", "TrushBin",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                // out へ反映
                filesDeleted = localFilesDeleted;
                dirsDeleted = localDirsDeleted;
            }
        }

        /// <summary>
        /// 文字列を指定長に短縮（中央を "..." に置換）
        /// </summary>
        /// <param name="s"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        private static string Shorten(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
            int keep = (max - 3) / 2;
            return s.Substring(0, keep) + "..." + s.Substring(s.Length - keep);
        }

        /// <summary>
        /// C:\trushbin 内のエントリ数を数える
        /// </summary>
        /// <param name="dir"></param>
        /// <returns></returns>
        public static int CountEntries(string dir)
        {
            if (!Directory.Exists(dir)) return 0;
            int files = EnumerateFilesNoReparse(dir).Count();
            int dirs = EnumerateDirectoriesNoReparse(dir).Count();
            return files + dirs;
        }

        /// <summary>
        /// デスクトップのショートカットアイコンを更新
        /// </summary>
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
                if (System.IO.File.Exists(mainLnk))
                    ShortcutHelper.SetShortcutIcon(mainLnk, hasItems ? fullIco : emptyIco);
            }
            catch { /* 致命ではないので無視 */ }
        }

        // ===== 内部ユーティリティ =====
        /// <summary>
        /// 別ボリュームかどうか
        /// </summary>
        /// <param name="src"></param>
        /// <param name="dst"></param>
        /// <returns></returns>
        private static bool IsCrossVolume(string src, string dst)
        {
            string r1 = Path.GetPathRoot(Path.GetFullPath(src))!;
            string r2 = Path.GetPathRoot(Path.GetFullPath(dst))!;
            return !string.Equals(r1, r2, StringComparison.OrdinalIgnoreCase);
        }

        // 別ボリューム時：コピー→元を安全消去
        private static void MoveFileSafeWithSourceWipe(string src, string dst)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
            try
            {
                if (!IsCrossVolume(src, dst))
                {
                    System.IO.File.Move(src, dst); // 同一ボリュームは原子的移動
                }
                else
                {
                    System.IO.File.Copy(src, dst, overwrite: false);
                    SecureWipe.SecureDeleteFile(src); // 元側を安全消去
                }
            }
            catch (IOException)
            {
                System.IO.File.Copy(src, dst, overwrite: false);
                SecureWipe.SecureDeleteFile(src);
            }
        }

        /// <summary>
        /// ディレクトリを再帰的にコピー（シンボリックリンク/ジャンクションは無視）
        /// </summary>
        /// <param name="sourceDir"></param>
        /// <param name="destDir"></param>
        private static void CopyDirectoryRecursive(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var f in Directory.GetFiles(sourceDir))
            {
                var df = Path.Combine(destDir, Path.GetFileName(f));
                System.IO.File.Copy(f, df, overwrite: false);
            }
            foreach (var d in Directory.GetDirectories(sourceDir))
            {
                CopyDirectoryRecursive(d, Path.Combine(destDir, Path.GetFileName(d)));
            }
        }

        /// <summary>
        /// 別ボリュームかどうかを考慮してディレクトリを移動。
        /// </summary>
        /// <param name="src"></param>
        /// <param name="dst"></param>
        private static void MoveDirectorySafeWithSourceWipe(string src, string dst)
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
                    SecureWipe.SecureDeleteDirectoryTree(src); // 元側を安全消去
                }
            }
            catch (IOException)
            {
                CopyDirectoryRecursive(src, dst);
                SecureWipe.SecureDeleteDirectoryTree(src);
            }
        }

        /// <summary>
        /// 指定パスが存在する場合、(1), (2), ... を付与してユニーク化したパスを返す
        /// </summary>
        /// <param name="destPath"></param>
        /// <returns></returns>
        private static string UniqueDestination(string destPath)
        {
            if (!System.IO.File.Exists(destPath) && !Directory.Exists(destPath)) return destPath;

            string dir = Path.GetDirectoryName(destPath)!;
            string baseName = Path.GetFileNameWithoutExtension(destPath);
            string ext = Path.GetExtension(destPath);

            for (int i = 1; i < 10000; i++)
            {
                string trial = Path.Combine(dir, $"{baseName} ({i}){ext}");
                if (!System.IO.File.Exists(trial) && !Directory.Exists(trial))
                    return trial;
            }
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmssfff");
            return Path.Combine(dir, $"{baseName}_{stamp}{ext}");
        }

        /// <summary>
        /// シンボリックリンク/ジャンクションを無視してファイル列挙
        /// </summary>
        /// <param name="root"></param>
        /// <returns></returns>
        private static IEnumerable<string> EnumerateFilesNoReparse(string root)
        {
            foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                bool isReparsePoint = false;
                try
                {
                    var attr = System.IO.File.GetAttributes(f);
                    isReparsePoint = (attr & FileAttributes.ReparsePoint) != 0;
                }
                catch { continue; }

                if (!isReparsePoint) yield return f;
            }
        }

        /// <summary>
        /// シンボリックリンク/ジャンクションを無視してディレクトリ列挙
        /// </summary>
        /// <param name="root"></param>
        /// <returns></returns>
        private static IEnumerable<string> EnumerateDirectoriesNoReparse(string root)
        {
            foreach (var d in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
            {
                bool isReparsePoint = false;
                try
                {
                    var attr = System.IO.File.GetAttributes(d);
                    isReparsePoint = (attr & FileAttributes.ReparsePoint) != 0;
                }
                catch { continue; }

                if (!isReparsePoint) yield return d;
            }
        }

        // Explorerへ更新通知（表示ズレ対策）
        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
        private const uint SHCNE_UPDATEDIR = 0x00001000;
        private const uint SHCNF_PATHW = 0x0005;

        /// <summary>
        /// 指定フォルダを Explorer に更新通知
        /// </summary>
        /// <param name="path">パス</param>
        private static void ShellRefresh(string path)
        {
            var h = Marshal.StringToHGlobalUni(path);
            try { SHChangeNotify(SHCNE_UPDATEDIR, SHCNF_PATHW, h, IntPtr.Zero); }
            finally { Marshal.FreeHGlobal(h); }
        }
    }

    /// <summary>
    /// ファイル/ストリームのセキュア消去
    /// </summary>
    internal static class SecureWipe
    {
        private const int BufferSize = 1024 * 1024; // 1MB
        private static readonly RNGCryptoServiceProvider Rng = new RNGCryptoServiceProvider();

        /// <summary>
        /// ディレクトリをセキュア消去（ファイルをセキュア消去→ディレクトリは深い順にリネーム→削除）
        /// </summary>
        /// <param name="dir"></param>
        public static void SecureDeleteDirectoryTree(string dir)
        {
            if (!Directory.Exists(dir)) return;

            // ファイル安全消去
            foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                try { SecureDeleteFile(f); } catch { /* 続行 */ }
            }

            // ディレクトリは深い順にリネーム→削除
            var allDirs = Directory.EnumerateDirectories(dir, "*", SearchOption.AllDirectories)
                .OrderByDescending(p => p.Length)
                .ToList();

            foreach (var d in allDirs)
            {
                try
                {
                    var parent = Path.GetDirectoryName(d)!;
                    var renamed = d;
                    try
                    {
                        var rnd = RandomName();
                        renamed = Path.Combine(parent, rnd);
                        Directory.Move(d, renamed);
                    }
                    catch { /* そのまま削除試行 */ }

                    try { Directory.Delete(renamed, false); } catch { /* 続行 */ }
                }
                catch { /* 続行 */ }
            }

            try { Directory.Delete(dir, false); } catch { /* 続行 */ }
        }

        /// <summary>
        /// 進捗なしの簡易版（他の箇所からの再利用用）
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static bool SecureDeleteFile(string path)
        {
            return SecureDeleteFileWithProgress(path, _ => { });
        }

        /// <summary>
        /// 進捗付き：成功/失敗を返す 
        /// </summary>
        /// <param name="path"></param>
        /// <param name="progressBytesDelta"></param>
        /// <returns></returns>
        public static bool SecureDeleteFileWithProgress(string path, Action<long> progressBytesDelta)
        {
            if (!System.IO.File.Exists(path)) return true;

            try
            {
                // 属性解除
                try { System.IO.File.SetAttributes(path, FileAttributes.Normal); } catch { }

                // ADSも処理（可能な範囲）
                foreach (var streamName in NtfsAlternateStreams.List(path))
                {
                    try { OverwriteStream(path, streamName, progressBytesDelta); } catch { /* 続行 */ }
                }

                // 本体データ上書き（ランダム→ゼロ）※進捗コールバック
                OverwriteFileWithProgress(path, progressBytesDelta);

                // タイムスタンプを現在時刻で上書き
                try
                {
                    var now = DateTime.Now;
                    System.IO.File.SetCreationTime(path, now);
                    System.IO.File.SetLastWriteTime(path, now);
                    System.IO.File.SetLastAccessTime(path, now);
                }
                catch { }

                // ランダム名へ改名（メタ痕跡抑制）
                try
                {
                    var dir = Path.GetDirectoryName(path)!;
                    var rnd = RandomName() + Path.GetExtension(path);
                    var newPath = Path.Combine(dir, rnd);
                    System.IO.File.Move(path, newPath);
                    path = newPath;
                }
                catch { /* 改名失敗はそのまま */ }

                // 長さ0に縮小してから削除
                try
                {
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None))
                    {
                        fs.SetLength(0);
                        fs.Flush(true);
                    }
                }
                catch { /* 続行 */ }

                try { System.IO.File.Delete(path); } catch { }
                return !System.IO.File.Exists(path);
            }
            catch
            {
                try { System.IO.File.Delete(path); } catch { }
                return !System.IO.File.Exists(path);
            }
        }

        /// <summary>
        /// ファイル本体を2パス上書き（ランダム→ゼロ）、進捗コールバック付き
        /// </summary>
        /// <param name="path"></param>
        /// <param name="progressBytesDelta"></param>
        private static void OverwriteFileWithProgress(string path, Action<long> progressBytesDelta)
        {
            long len = 0;
            try { len = new FileInfo(path).Length; } catch { }

            if (len <= 0)
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None))
                    fs.Flush(true);
                return;
            }

            byte[] buf = new byte[BufferSize];

            // 1パス目：ランダム
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None))
            {
                long remain = len;
                while (remain > 0)
                {
                    Rng.GetBytes(buf);
                    int write = (int)Math.Min(remain, buf.Length);
                    fs.Write(buf, 0, write);
                    remain -= write;
                    progressBytesDelta(write);
                }
                fs.Flush(true);
            }

            // 2パス目：ゼロ
            Array.Clear(buf, 0, buf.Length);
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None))
            {
                long remain = len;
                while (remain > 0)
                {
                    int write = (int)Math.Min(remain, buf.Length);
                    fs.Write(buf, 0, write);
                    remain -= write;
                    progressBytesDelta(write);
                }
                fs.Flush(true);
            }
        }

        /// <summary>
        /// ADSをゼロで上書き（進捗コールバック付き）
        /// </summary>
        /// <param name="basePath"></param>
        /// <param name="streamName"></param>
        /// <param name="progressBytesDelta"></param>
        private static void OverwriteStream(string basePath, string streamName, Action<long> progressBytesDelta)
        {
            var streamPath = basePath + ":" + streamName;
            if (!System.IO.File.Exists(streamPath)) return;

            long len = 0;
            try { len = new FileInfo(streamPath).Length; } catch { }
            if (len <= 0) return;

            byte[] buf = new byte[BufferSize];
            Array.Clear(buf, 0, buf.Length);

            using (var fs = new FileStream(streamPath, FileMode.Open, FileAccess.Write, FileShare.None))
            {
                long remain = len;
                while (remain > 0)
                {
                    int write = (int)Math.Min(remain, buf.Length);
                    fs.Write(buf, 0, write);
                    remain -= write;
                    progressBytesDelta(write);
                }
                fs.Flush(true);
            }
        }

        /// <summary>
        /// ランダムな名前を生成（16文字、英数字）
        /// </summary>
        /// <returns></returns>
        public static string RandomName()
        {
            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            Span<byte> rnd = stackalloc byte[16];
            using (var rng = RandomNumberGenerator.Create()) { rng.GetBytes(rnd); }
            var sb = new StringBuilder(16);
            foreach (var b in rnd) sb.Append(chars[b % chars.Length]);
            return sb.ToString();
        }
    }

    /// <summary>
    /// NTFSの代替データストリーム（ADS）列挙
    /// </summary>
    internal static class NtfsAlternateStreams
    {
        /// <summary>
        /// 指定ファイルのADS名を列挙
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static IEnumerable<string> List(string path)
        {
            IntPtr hFind = FindFirstStreamW(path, StreamInfoLevels.FindStreamInfoStandard, out WIN32_FIND_STREAM_DATA data, 0);
            if (hFind == INVALID_HANDLE_VALUE) yield break;

            try
            {
                do
                {
                    var name = data.cStreamName;
                    if (name.StartsWith(":") && name.EndsWith(":$DATA"))
                    {
                        var core = name.Substring(1, name.Length - 1 - ":$DATA".Length);
                        if (!string.IsNullOrEmpty(core))
                            yield return core;
                    }
                }
                while (FindNextStreamW(hFind, out data));
            }
            finally
            {
                FindClose(hFind);
            }
        }
        /// <summary>
        /// MAX_PATH + 36 は Windows API の定義に準拠
        /// </summary>
        private const int MAX_PATH = 260;

        /// <summary>
        /// 無効なハンドル値
        /// </summary>
        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        /// <summary>
        /// ストリーム情報レベル
        /// </summary>
        private enum StreamInfoLevels { FindStreamInfoStandard = 0 }

        /// <summary>
        /// WIN32_FIND_STREAM_DATA 構造体
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WIN32_FIND_STREAM_DATA
        {
            public long StreamSize;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH + 36)]
            public string cStreamName;
        }

        /// <summary>
        /// FindFirstStreamW 関数
        /// </summary>
        /// <param name="lpFileName"></param>
        /// <param name="InfoLevel"></param>
        /// <param name="lpFindStreamData"></param>
        /// <param name="dwFlags"></param>
        /// <returns></returns>
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr FindFirstStreamW(
            string lpFileName,
            StreamInfoLevels InfoLevel,
            out WIN32_FIND_STREAM_DATA lpFindStreamData,
            uint dwFlags);

        /// <summary>
        /// FindNextStreamW 関数
        /// </summary>
        /// <param name="hFindStream"></param>
        /// <param name="lpFindStreamData"></param>
        /// <returns></returns>
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool FindNextStreamW(
            IntPtr hFindStream,
            out WIN32_FIND_STREAM_DATA lpFindStreamData);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FindClose(IntPtr hFindFile);
    }

    /// <summary>
    /// ショートカット作成/更新ロジック
    /// </summary>
    internal static class ShortcutHelper
    {
        /// <summary>
        /// デスクトップにショートカットを作成/更新。
        /// twoShortcuts = true のときのみ「空にする」専用も作る。
        /// </summary>
        public static void CreateOrUpdateShortcuts(bool twoShortcuts = false)
        {
            /// デスクトップパス
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            /// 実行ファイルパス
            string exe = Application.ExecutablePath;
            /// ショートカットパス
            string mainLnk = Path.Combine(desktop, "ゴミ箱（Trush）.lnk");
            /// もう一つのショートカットパス
            string emptyLnk = Path.Combine(desktop, "ゴミ箱を空にする.lnk");
            /// 空アイコンパス
            string emptyIco = Path.Combine(Path.GetDirectoryName(exe)!, "empty.ico");

            /// 満杯アイコンパス
            CreateOrUpdateShortcut(mainLnk, exe, "", emptyIco);

            if (twoShortcuts)
                CreateOrUpdateShortcut(emptyLnk, exe, "--empty", emptyIco);
        }

        /// <summary>
        /// ショートカットのアイコンを変更
        /// </summary>
        /// <param name="lnkPath"></param>
        /// <param name="iconPath"></param>
        public static void SetShortcutIcon(string lnkPath, string iconPath)
        {
            if (!System.IO.File.Exists(lnkPath) || !System.IO.File.Exists(iconPath)) return;

            IShellLinkW link = (IShellLinkW)new ShellLink();
            ((IPersistFile)link).Load(lnkPath, 0);
            link.SetIconLocation(iconPath, 0);
            ((IPersistFile)link).Save(lnkPath, true);
        }

        /// <summary>
        /// ショートカットを作成/更新
        /// </summary>
        /// <param name="lnkPath"></param>
        /// <param name="targetExe"></param>
        /// <param name="args"></param>
        /// <param name="iconPath"></param>
        private static void CreateOrUpdateShortcut(string lnkPath, string targetExe, string args, string iconPath)
        {
            IShellLinkW link = (IShellLinkW)new ShellLink();
            link.SetPath(targetExe);
            link.SetArguments(args);
            link.SetWorkingDirectory(Path.GetDirectoryName(targetExe));
            link.SetIconLocation(System.IO.File.Exists(iconPath) ? iconPath : targetExe, 0);
            link.SetDescription("ドラッグ＆ドロップで C:\\trushbin へ移動 / 実行で操作メニュー");

            IPersistFile file = (IPersistFile)link;
            file.Save(lnkPath, true);
        }

        // --------- COM Interop 定義 ---------
        /// <summary>
        /// ShellLink クラス
        /// </summary>
        [ComImport]
        [Guid("00021401-0000-0000-C000-000000000046")]
        private class ShellLink { }

        /// <summary>
        /// IShellLinkW インターフェイス
        /// COM経由にてショートカットアイコンの設定など
        /// </summary>
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

        /// <summary>
        /// IPersistFile インターフェイス
        /// COM経由にてショートカットアイコンの呼出・保存など
        /// </summary>
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
