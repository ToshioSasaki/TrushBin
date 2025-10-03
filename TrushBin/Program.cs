using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace TrushBin
{
    internal static class Program
    {
        /// <summary>
        /// �����F
        ///   --setup        : �f�X�N�g�b�v�Ƀ��C���V���[�g�J�b�g�쐬
        ///   --setup:2      : ���C�� + �u��ɂ���v��p�V���[�g�J�b�g�쐬
        ///   --twoshortcuts : --setup:2 �Ɠ��`
        ///   --empty        : C:\trushbin ���Z�L���A�����i�i���\���j
        ///   [�p�X�Q]       : �������p�X�݂̂̏ꍇ�� C:\trushbin �ֈړ�
        ///   �i�����Ȃ��j   : �ȈՃ��j���[�\��
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                TrushLogic.EnsureTrushDir();

                // �Z�b�g�A�b�v�i�V���[�g�J�b�g�쐬�j
                //   --setup      : 1��
                //   --setup:2    : 2�i+�u��ɂ���v�j
                //   --twoshortcuts : --setup:2 �Ɠ��`
                if (args.Any(a => a.Equals("--setup", StringComparison.OrdinalIgnoreCase) ||
                                  a.Equals("--setup:2", StringComparison.OrdinalIgnoreCase) ||
                                  a.Equals("--twoshortcuts", StringComparison.OrdinalIgnoreCase)))
                {
                    bool two = args.Any(a => a.Equals("--setup:2", StringComparison.OrdinalIgnoreCase) ||
                                             a.Equals("--twoshortcuts", StringComparison.OrdinalIgnoreCase));
                    ShortcutHelper.CreateOrUpdateShortcuts(two);
                    TrushLogic.UpdateAllShortcutIcons();
                    MessageBox.Show(two ? "�f�X�N�g�b�v�ɃV���[�g�J�b�g��2�쐬���܂����B" :
                                          "�f�X�N�g�b�v�ɃV���[�g�J�b�g���쐬���܂����B",
                                    "TrushBin");
                    return;
                }

                // ��ɂ���i���S�����F�v���O���X�t���j
                //   --empty : �Z�L���A�����i�����_�����[����2�p�X�AADS�Ή��A�i���\���j
                if (args.Any(a => a.Equals("--empty", StringComparison.OrdinalIgnoreCase)))
                {
                    var totalNow = TrushLogic.CountEntries(TrushLogic.TrushPath);
                    var confirm = MessageBox.Show(
                        $"�S�~�������S�����i��������j���܂��B��낵���ł����H\n���� {totalNow} ��",
                        "TrushBin", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);

                    if (confirm == DialogResult.OK)
                    {
                        int f, d;
                        TrushLogic.EmptyTrushSecureWithProgress(out f, out d);   // �� �i���\����
                        MessageBox.Show($"���S�������܂����B�t�@�C�� {f} / �t�H���_ {d}", "TrushBin");
                    }

                    TrushLogic.UpdateAllShortcutIcons();
                    return;
                }

                // �h���b�v�i�����Ńp�X���󂯂��ꍇ�j: C:\trushbin �ֈړ�
                var paths = args.Where(a => !a.StartsWith("--")).ToArray();
                if (paths.Length > 0)
                {
                    int moved = TrushLogic.MoveIntoTrush(paths);
                    MessageBox.Show($"�ړ����܂����B{moved} ��\n�� {TrushLogic.TrushPath}", "TrushBin");
                    TrushLogic.UpdateAllShortcutIcons();
                    return;
                }

                // �����Ȃ��F�ȈՃ��j���[
                var count = TrushLogic.CountEntries(TrushLogic.TrushPath);
                var result = MessageBox.Show(
                    $"�ǂ�����H\n\n[�͂�]�F���S�����i���� {count} ���j\n[������]�F�t�H���_���J��\n[�L�����Z��]�F����",
                    "TrushBin", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    var confirm2 = MessageBox.Show(
                        $"�S�~�������S�����i��������j���܂��B��낵���ł����H\n���� {count} ��",
                        "TrushBin", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                    if (confirm2 == DialogResult.OK)
                    {
                        int f, d;
                        // �i���\����
                        TrushLogic.EmptyTrushSecureWithProgress(out f, out d);   
                        MessageBox.Show($"���S�������܂����B�t�@�C�� {f} / �t�H���_ {d}", "TrushBin");
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
                MessageBox.Show("�G���[: " + ex.Message, "TrushBin", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    /// <summary>
    /// �i���\���t�H�[��
    /// </summary>
    internal class ProgressForm : Form
    {
        private ProgressBar bar;
        private Label lblTitle;
        private Label lblDetail;

        // �E�� �~ �Ή��̂��߂̐���t���O
        // PauseRequested=true �ŏ������͈ꎞ��~���AContinue/Restore �̂ǂ��炩�ŉ���
        public volatile bool PauseRequested = false;
        public volatile bool ContinueRequested = false; // ���s�i�͂��j
        public volatile bool RestoreRequested = false;  // ���f�i�������j
        public bool YesMeansContinue = true;            //�u�͂������s / �����������f�v�ɌŒ�i�v���ǂ���j

        /// <summary>
        /// �R���X�g���N�^
        /// </summary>
        public ProgressForm()
        {
            Text = "���S�������c";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = true;
            Width = 520;
            Height = 160;

            lblTitle = new Label { Left = 14, Top = 12, Width = 480, Text = "�������c" };
            bar = new ProgressBar { Left = 14, Top = 40, Width = 480, Height = 22, Style = ProgressBarStyle.Continuous, Minimum = 0, Maximum = 100 };
            lblDetail = new Label { Left = 14, Top = 70, Width = 480, Height = 40, Text = "" };

            Controls.Add(lblTitle);
            Controls.Add(bar);
            Controls.Add(lblDetail);
        }

        /// <summary>
        /// �v���O���X�X�V
        /// </summary>
        /// <param name="percent">�i����</param>
        /// <param name="title">�v���O���X�o�[�^�C�g��</param>
        /// <param name="detail">�v���O���X�o�[���ו���</param>
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
        /// �v���O���X�o�[�����ڂ����t�߂�\��
        /// </summary>
        /// <param name="targetPercent">�t�߂�i��</param>
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
        /// �~�N���b�N���F�|�[�Y���m�F�_�C�A���O�����s/���f�̂����ꂩ���t���O�Œʒm
        /// </summary>
        /// <param name="e"></param>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true; // �����ŕ������Ȃ��i�������̔��f��҂j
                PauseRequested = true;

                var dr = MessageBox.Show(
                    "�t�@�C���폜����̂����߂܂����H",
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
    /// C:\trushbin �֘A�̃��W�b�N
    /// </summary>
    internal static class TrushLogic
    {
        /// <summary>
        /// �S�~���t�H���_�̃p�X
        /// </summary>
        public static readonly string TrushPath = @"C:\trushbin";

        /// <summary>
        /// C:\trushbin �t�H���_���쐬�i�Ȃ���΁j
        /// </summary>
        public static void EnsureTrushDir()
        {
            if (!Directory.Exists(TrushPath))
                Directory.CreateDirectory(TrushPath);
        }

        /// <summary>
        /// �w��p�X�Q�� C:\trushbin �Ɉړ��B
        /// ����{�����[��: Move�i���q�I�j�B�ʃ{�����[��: Copy �� �����Z�L���A�����B
        /// </summary>
        /// <param name="inputPaths">���͐�p�X</param>
        /// <returns>TrushBin�Ɉړ������t�@�C����</returns>
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
                    "�ړ��ł��Ȃ��������ځi�g�p��/����/����p�X�̉\���j:\n" +
                string.Join("\n", failures.Take(10)) +
                        (failures.Count > 10 ? $"\n�c�ق� {failures.Count - 10} ��" : ""),
                    "TrushBin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            return moved;
        }

        /// <summary>
        /// C:\trushbin ���Z�L���A�����i�i���\���j
        /// - ADS�̏㏑��
        /// - �{��2�p�X�㏑���i�����_�����[���j
        /// - �^�C���X�^���v�㏑��
        /// - �����_�����։��� �� 0���ɏk�� �� �폜
        /// UI�́~�Ń|�[�Y�����s or ���f�i�t�߂�\��, ������/���s�����\���j
        /// �t�@�C�����t�H���_�̏��ɏ����B
        /// </summary>
        /// <param name="filesDeleted">�t�@�C���폜��</param>
        /// <param name="dirsDeleted">�t�H���_�폜��</param>
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
                pf.YesMeansContinue = true; // [�͂�]=���s / [������]=���f
                pf.Show();
                pf.SetProgress(0, "�������c", $"�Ώۃt�@�C�� {files.Count} / �t�H���_ {dirsDeep.Count}");

                long processedBytes = 0;
                var fails = new List<string>();

                int localFilesDeleted = 0; // �W�v�̓��[�J�����Ō�� out ��
                int localDirsDeleted = 0;

                // �����������\���p�i���萔�j
                int filesAttempted = 0;
                int dirsAttempted = 0;

                var t = Task.Run(() =>
                {
                    // �t�@�C��
                    foreach (var f in files)
                    {
                        // �ꎞ��~�i�~�j�Ή�
                        while (pf.PauseRequested && !pf.ContinueRequested && !pf.RestoreRequested)
                            Thread.Sleep(30);
                        if (pf.RestoreRequested) return; // ���f

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
                                    pf.SetProgress(percent, "���S�������c", Shorten(name, 70));
                                });

                            if (ok) Interlocked.Increment(ref localFilesDeleted);
                            else lock (fails) fails.Add(f);
                        }
                        catch { lock (fails) fails.Add(f); }
                    }

                    // �f�B���N�g���i�[�����j
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
                                pf.SetProgress(Math.Min(99, percent), "�t�H���_�������c", Shorten(d, 70));
                            }
                        }
                        catch { lock (fails) fails.Add(d); }
                    }
                });

                // UI���񂵂����҂�
                while (!t.IsCompleted)
                {
                    Application.DoEvents();
                    Thread.Sleep(30);
                }

                try { ShellRefresh(TrushPath); } catch { }

                if (pf.RestoreRequested)
                {
                    // ���o�I�ȋt�߂�
                    pf.ReverseTo(0);

                    // �����������i���肵�Ă��Ȃ����́j
                    int remainingFiles = Math.Max(0, files.Count - filesAttempted);
                    int remainingDirs = Math.Max(0, dirsDeep.Count - dirsAttempted);

                    pf.SetProgress(0, "���f���܂���",
                        $"������: �t�@�C�� {remainingFiles} / �t�H���_ {remainingDirs} / ���s: {fails.Count}");
                    Application.DoEvents();
                    Thread.Sleep(200);
                }
                else
                {
                    pf.SetProgress(100, "�ŏI�������c", "");
                    Application.DoEvents();
                    Thread.Sleep(80);
                }

                if (!pf.RestoreRequested && localFilesDeleted + localDirsDeleted == 0 && (files.Count + dirsDeep.Count) > 0)
                {
                    MessageBox.Show("�����Ɏ��s�����\��������܂��B����/��L/����p�X���m�F���Ă��������B", "TrushBin",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                // out �֔��f
                filesDeleted = localFilesDeleted;
                dirsDeleted = localDirsDeleted;
            }
        }

        /// <summary>
        /// ��������w�蒷�ɒZ�k�i������ "..." �ɒu���j
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
        /// C:\trushbin ���̃G���g�����𐔂���
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
        /// �f�X�N�g�b�v�̃V���[�g�J�b�g�A�C�R�����X�V
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

                var mainLnk = Path.Combine(desktop, "�S�~���iTrush�j.lnk");
                if (System.IO.File.Exists(mainLnk))
                    ShortcutHelper.SetShortcutIcon(mainLnk, hasItems ? fullIco : emptyIco);
            }
            catch { /* �v���ł͂Ȃ��̂Ŗ��� */ }
        }

        // ===== �������[�e�B���e�B =====
        /// <summary>
        /// �ʃ{�����[�����ǂ���
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

        // �ʃ{�����[�����F�R�s�[���������S����
        private static void MoveFileSafeWithSourceWipe(string src, string dst)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
            try
            {
                if (!IsCrossVolume(src, dst))
                {
                    System.IO.File.Move(src, dst); // ����{�����[���͌��q�I�ړ�
                }
                else
                {
                    System.IO.File.Copy(src, dst, overwrite: false);
                    SecureWipe.SecureDeleteFile(src); // ���������S����
                }
            }
            catch (IOException)
            {
                System.IO.File.Copy(src, dst, overwrite: false);
                SecureWipe.SecureDeleteFile(src);
            }
        }

        /// <summary>
        /// �f�B���N�g�����ċA�I�ɃR�s�[�i�V���{���b�N�����N/�W�����N�V�����͖����j
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
        /// �ʃ{�����[�����ǂ������l�����ăf�B���N�g�����ړ��B
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
                    SecureWipe.SecureDeleteDirectoryTree(src); // ���������S����
                }
            }
            catch (IOException)
            {
                CopyDirectoryRecursive(src, dst);
                SecureWipe.SecureDeleteDirectoryTree(src);
            }
        }

        /// <summary>
        /// �w��p�X�����݂���ꍇ�A(1), (2), ... ��t�^���ă��j�[�N�������p�X��Ԃ�
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
        /// �V���{���b�N�����N/�W�����N�V�����𖳎����ăt�@�C����
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
        /// �V���{���b�N�����N/�W�����N�V�����𖳎����ăf�B���N�g����
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

        // Explorer�֍X�V�ʒm�i�\���Y���΍�j
        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
        private const uint SHCNE_UPDATEDIR = 0x00001000;
        private const uint SHCNF_PATHW = 0x0005;

        /// <summary>
        /// �w��t�H���_�� Explorer �ɍX�V�ʒm
        /// </summary>
        /// <param name="path">�p�X</param>
        private static void ShellRefresh(string path)
        {
            var h = Marshal.StringToHGlobalUni(path);
            try { SHChangeNotify(SHCNE_UPDATEDIR, SHCNF_PATHW, h, IntPtr.Zero); }
            finally { Marshal.FreeHGlobal(h); }
        }
    }

    /// <summary>
    /// �t�@�C��/�X�g���[���̃Z�L���A����
    /// </summary>
    internal static class SecureWipe
    {
        private const int BufferSize = 1024 * 1024; // 1MB
        private static readonly RNGCryptoServiceProvider Rng = new RNGCryptoServiceProvider();

        /// <summary>
        /// �f�B���N�g�����Z�L���A�����i�t�@�C�����Z�L���A�������f�B���N�g���͐[�����Ƀ��l�[�����폜�j
        /// </summary>
        /// <param name="dir"></param>
        public static void SecureDeleteDirectoryTree(string dir)
        {
            if (!Directory.Exists(dir)) return;

            // �t�@�C�����S����
            foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                try { SecureDeleteFile(f); } catch { /* ���s */ }
            }

            // �f�B���N�g���͐[�����Ƀ��l�[�����폜
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
                    catch { /* ���̂܂܍폜���s */ }

                    try { Directory.Delete(renamed, false); } catch { /* ���s */ }
                }
                catch { /* ���s */ }
            }

            try { Directory.Delete(dir, false); } catch { /* ���s */ }
        }

        /// <summary>
        /// �i���Ȃ��̊ȈՔŁi���̉ӏ�����̍ė��p�p�j
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static bool SecureDeleteFile(string path)
        {
            return SecureDeleteFileWithProgress(path, _ => { });
        }

        /// <summary>
        /// �i���t���F����/���s��Ԃ� 
        /// </summary>
        /// <param name="path"></param>
        /// <param name="progressBytesDelta"></param>
        /// <returns></returns>
        public static bool SecureDeleteFileWithProgress(string path, Action<long> progressBytesDelta)
        {
            if (!System.IO.File.Exists(path)) return true;

            try
            {
                // ��������
                try { System.IO.File.SetAttributes(path, FileAttributes.Normal); } catch { }

                // ADS�������i�\�Ȕ͈́j
                foreach (var streamName in NtfsAlternateStreams.List(path))
                {
                    try { OverwriteStream(path, streamName, progressBytesDelta); } catch { /* ���s */ }
                }

                // �{�̃f�[�^�㏑���i�����_�����[���j���i���R�[���o�b�N
                OverwriteFileWithProgress(path, progressBytesDelta);

                // �^�C���X�^���v�����ݎ����ŏ㏑��
                try
                {
                    var now = DateTime.Now;
                    System.IO.File.SetCreationTime(path, now);
                    System.IO.File.SetLastWriteTime(path, now);
                    System.IO.File.SetLastAccessTime(path, now);
                }
                catch { }

                // �����_�����։����i���^���՗}���j
                try
                {
                    var dir = Path.GetDirectoryName(path)!;
                    var rnd = RandomName() + Path.GetExtension(path);
                    var newPath = Path.Combine(dir, rnd);
                    System.IO.File.Move(path, newPath);
                    path = newPath;
                }
                catch { /* �������s�͂��̂܂� */ }

                // ����0�ɏk�����Ă���폜
                try
                {
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None))
                    {
                        fs.SetLength(0);
                        fs.Flush(true);
                    }
                }
                catch { /* ���s */ }

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
        /// �t�@�C���{�̂�2�p�X�㏑���i�����_�����[���j�A�i���R�[���o�b�N�t��
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

            // 1�p�X�ځF�����_��
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

            // 2�p�X�ځF�[��
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
        /// ADS���[���ŏ㏑���i�i���R�[���o�b�N�t���j
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
        /// �����_���Ȗ��O�𐶐��i16�����A�p�����j
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
    /// NTFS�̑�փf�[�^�X�g���[���iADS�j��
    /// </summary>
    internal static class NtfsAlternateStreams
    {
        /// <summary>
        /// �w��t�@�C����ADS�����
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
        /// MAX_PATH + 36 �� Windows API �̒�`�ɏ���
        /// </summary>
        private const int MAX_PATH = 260;

        /// <summary>
        /// �����ȃn���h���l
        /// </summary>
        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        /// <summary>
        /// �X�g���[����񃌃x��
        /// </summary>
        private enum StreamInfoLevels { FindStreamInfoStandard = 0 }

        /// <summary>
        /// WIN32_FIND_STREAM_DATA �\����
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WIN32_FIND_STREAM_DATA
        {
            public long StreamSize;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH + 36)]
            public string cStreamName;
        }

        /// <summary>
        /// FindFirstStreamW �֐�
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
        /// FindNextStreamW �֐�
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
    /// �V���[�g�J�b�g�쐬/�X�V���W�b�N
    /// </summary>
    internal static class ShortcutHelper
    {
        /// <summary>
        /// �f�X�N�g�b�v�ɃV���[�g�J�b�g���쐬/�X�V�B
        /// twoShortcuts = true �̂Ƃ��̂݁u��ɂ���v��p�����B
        /// </summary>
        public static void CreateOrUpdateShortcuts(bool twoShortcuts = false)
        {
            /// �f�X�N�g�b�v�p�X
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            /// ���s�t�@�C���p�X
            string exe = Application.ExecutablePath;
            /// �V���[�g�J�b�g�p�X
            string mainLnk = Path.Combine(desktop, "�S�~���iTrush�j.lnk");
            /// ������̃V���[�g�J�b�g�p�X
            string emptyLnk = Path.Combine(desktop, "�S�~������ɂ���.lnk");
            /// ��A�C�R���p�X
            string emptyIco = Path.Combine(Path.GetDirectoryName(exe)!, "empty.ico");

            /// ���t�A�C�R���p�X
            CreateOrUpdateShortcut(mainLnk, exe, "", emptyIco);

            if (twoShortcuts)
                CreateOrUpdateShortcut(emptyLnk, exe, "--empty", emptyIco);
        }

        /// <summary>
        /// �V���[�g�J�b�g�̃A�C�R����ύX
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
        /// �V���[�g�J�b�g���쐬/�X�V
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
            link.SetDescription("�h���b�O���h���b�v�� C:\\trushbin �ֈړ� / ���s�ő��상�j���[");

            IPersistFile file = (IPersistFile)link;
            file.Save(lnkPath, true);
        }

        // --------- COM Interop ��` ---------
        /// <summary>
        /// ShellLink �N���X
        /// </summary>
        [ComImport]
        [Guid("00021401-0000-0000-C000-000000000046")]
        private class ShellLink { }

        /// <summary>
        /// IShellLinkW �C���^�[�t�F�C�X
        /// COM�o�R�ɂăV���[�g�J�b�g�A�C�R���̐ݒ�Ȃ�
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
        /// IPersistFile �C���^�[�t�F�C�X
        /// COM�o�R�ɂăV���[�g�J�b�g�A�C�R���̌ďo�E�ۑ��Ȃ�
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
