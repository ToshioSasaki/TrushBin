using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
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

                // �Z�b�g�A�b�v�i�V���[�g�J�b�g�쐬�j
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

                // ��ɂ���i���S�����j
                if (args.Any(a => a.Equals("--empty", StringComparison.OrdinalIgnoreCase)))
                {
                    var totalNow = TrushLogic.CountEntries(TrushLogic.TrushPath);
                    var confirm = MessageBox.Show(
                        $"�S�~�������S�����i��������j���܂��B��낵���ł����H\n���� {totalNow} ��",
                        "TrushBin", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);

                    if (confirm == DialogResult.OK)
                    {
                        int f, d;
                        int total = TrushLogic.EmptyTrushSecure(out f, out d);
                        MessageBox.Show($"���S�������܂����B{total} ���i�t�@�C�� {f} / �t�H���_ {d}�j", "TrushBin");
                    }

                    TrushLogic.UpdateAllShortcutIcons();
                    return;
                }

                // �h���b�v�i�����Ńp�X���󂯂��ꍇ�j
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
                    var confirm = MessageBox.Show(
                        $"�S�~�������S�����i��������j���܂��B��낵���ł����H\n���� {count} ��",
                        "TrushBin", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                    if (confirm == DialogResult.OK)
                    {
                        int f, d;
                        int total = TrushLogic.EmptyTrushSecure(out f, out d);
                        MessageBox.Show($"���S�������܂����B{total} ���i�t�@�C�� {f} / �t�H���_ {d}�j", "TrushBin");
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

    internal static class TrushLogic
    {
        // �ۑ���i�ύX�ς݁j
        public static readonly string TrushPath = @"C:\trushbin";

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
                        MoveFileSafeWithSourceWipe(p, dest);
                        if (!File.Exists(p)) moved++; else failures.Add(p);
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

        // ���S�����i�����������̂����J�E���g���A���s�͈ꗗ�\���j
        public static int EmptyTrushSecure(out int filesDeleted, out int dirsDeleted)
        {
            filesDeleted = 0;
            dirsDeleted = 0;
            var fails = new List<string>();

            if (!Directory.Exists(TrushPath)) return 0;

            // 1) �t�@�C�����S����
            foreach (var f in EnumerateFilesNoReparse(TrushPath))
            {
                try
                {
                    if (SecureWipe.SecureDeleteFile(f)) filesDeleted++;
                    else fails.Add(f);
                }
                catch { fails.Add(f); }
            }

            // 2) �f�B���N�g���i�[�����j���l�[�����폜
            var allDirsDeepFirst = EnumerateDirectoriesNoReparse(TrushPath)
                .OrderByDescending(d => d.Length)
                .ToList();

            foreach (var d in allDirsDeepFirst)
            {
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
                    catch { /* rename���s�̓X���[ */ }

                    try { Directory.Delete(renamed, false); } catch { }
                    if (!Directory.Exists(renamed)) dirsDeleted++; else fails.Add(d);
                }
                catch { fails.Add(d); }
            }

            // 3) �����̋�t�H���_�|��
            foreach (var d in Directory.EnumerateDirectories(TrushPath, "*", SearchOption.TopDirectoryOnly))
            {
                try { Directory.Delete(d, false); } catch { }
                if (!Directory.Exists(d)) dirsDeleted++; else fails.Add(d);
            }

            // Explorer�ɍX�V�ʒm�i�������̃Y�����y���j
            try { ShellRefresh(TrushPath); } catch { }

            if (fails.Count > 0)
            {
                MessageBox.Show(
                    "�ꕔ�̍��ڂ͏����ł��܂���ł����i�g�p��/����/����p�X�̉\���j:\n" +
                    string.Join("\n", fails.Take(10)) +
                    (fails.Count > 10 ? $"\n�c�ق� {fails.Count - 10} ��" : ""),
                    "TrushBin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            return filesDeleted + dirsDeleted;
        }

        public static int CountEntries(string dir)
        {
            if (!Directory.Exists(dir)) return 0;
            int files = EnumerateFilesNoReparse(dir).Count();
            int dirs = EnumerateDirectoriesNoReparse(dir).Count();
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

                var mainLnk = Path.Combine(desktop, "�S�~���iTrush�j.lnk");
                if (File.Exists(mainLnk))
                    ShortcutHelper.SetShortcutIcon(mainLnk, hasItems ? fullIco : emptyIco);
            }
            catch { /* �v���ł͂Ȃ��̂Ŗ��� */ }
        }

        // ===== �������[�e�B���e�B =====

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
                    File.Move(src, dst); // ����{�����[���͌��q�I�ړ�
                }
                else
                {
                    File.Copy(src, dst, overwrite: false);
                    SecureWipe.SecureDeleteFile(src); // ���������S����
                }
            }
            catch (IOException)
            {
                File.Copy(src, dst, overwrite: false);
                SecureWipe.SecureDeleteFile(src);
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

        // �C���ӏ�: EnumerateFilesNoReparse ���\�b�h
        private static IEnumerable<string> EnumerateFilesNoReparse(string root)
        {
            foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                bool isReparsePoint = false;
                try
                {
                    var attr = File.GetAttributes(f);
                    isReparsePoint = (attr & FileAttributes.ReparsePoint) != 0;
                }
                catch
                {
                    // Skip files that cause exceptions
                    continue;
                }

                if (!isReparsePoint)
                {
                    yield return f;
                }
            }
        }

        private static IEnumerable<string> EnumerateDirectoriesNoReparse(string root)
        {
            foreach (var d in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
            {
                bool isReparsePoint = false;
                try
                {
                    var attr = File.GetAttributes(d);
                    isReparsePoint = (attr & FileAttributes.ReparsePoint) != 0;
                }
                catch
                {
                    // Skip directories that cause exceptions
                    continue;
                }

                if (!isReparsePoint)
                {
                    yield return d;
                }
            }
        }

        // Explorer�֍X�V�ʒm�i�\���Y���΍�j
        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
        private const uint SHCNE_UPDATEDIR = 0x00001000;
        private const uint SHCNF_PATHW = 0x0005;

        private static void ShellRefresh(string path)
        {
            var h = Marshal.StringToHGlobalUni(path);
            try { SHChangeNotify(SHCNE_UPDATEDIR, SHCNF_PATHW, h, IntPtr.Zero); }
            finally { Marshal.FreeHGlobal(h); }
        }
    }

    internal static class SecureWipe
    {
        private const int BufferSize = 1024 * 1024; // 1MB
        private static readonly RNGCryptoServiceProvider Rng = new RNGCryptoServiceProvider();

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

        // ����/���s��Ԃ��i����t�@�C�����ł́g����������h�������Ɂj
        public static bool SecureDeleteFile(string path)
        {
            if (!File.Exists(path)) return true;

            try
            {
                // ��������
                try { File.SetAttributes(path, FileAttributes.Normal); } catch { }

                // ADS�������i�\�Ȕ͈́j
                foreach (var streamName in NtfsAlternateStreams.List(path))
                {
                    try { OverwriteStream(path, streamName); } catch { /* ���s */ }
                }

                // �{�̃f�[�^�㏑���i�����_�����[���j
                OverwriteFile(path);

                // �^�C���X�^���v�����ݎ����ŏ㏑��
                try
                {
                    var now = DateTime.Now;
                    File.SetCreationTime(path, now);
                    File.SetLastWriteTime(path, now);
                    File.SetLastAccessTime(path, now);
                }
                catch { }

                // �����_�����։����i���^���՗}���j
                try
                {
                    var dir = Path.GetDirectoryName(path)!;
                    var rnd = RandomName() + Path.GetExtension(path);
                    var newPath = Path.Combine(dir, rnd);
                    File.Move(path, newPath);
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

                try { File.Delete(path); } catch { }
                return !File.Exists(path);
            }
            catch
            {
                try { File.Delete(path); } catch { }
                return !File.Exists(path);
            }
        }

        private static void OverwriteFile(string path)
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
                }
                fs.Flush(true);
            }
        }

        private static void OverwriteStream(string basePath, string streamName)
        {
            var streamPath = basePath + ":" + streamName;
            if (!File.Exists(streamPath)) return;

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
                }
                fs.Flush(true);
            }
        }

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

    // NTFS Alternate Data Streams ��
    internal static class NtfsAlternateStreams
    {
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

        private const int MAX_PATH = 260;
        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        private enum StreamInfoLevels { FindStreamInfoStandard = 0 }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WIN32_FIND_STREAM_DATA
        {
            public long StreamSize;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH + 36)]
            public string cStreamName;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr FindFirstStreamW(
            string lpFileName,
            StreamInfoLevels InfoLevel,
            out WIN32_FIND_STREAM_DATA lpFindStreamData,
            uint dwFlags);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool FindNextStreamW(
            IntPtr hFindStream,
            out WIN32_FIND_STREAM_DATA lpFindStreamData);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FindClose(IntPtr hFindFile);
    }

    internal static class ShortcutHelper
    {
        /// <summary>
        /// �f�X�N�g�b�v�ɃV���[�g�J�b�g���쐬/�X�V�B
        /// twoShortcuts = true �̂Ƃ��̂݁u��ɂ���v��p�����B
        /// </summary>
        public static void CreateOrUpdateShortcuts(bool twoShortcuts = false)
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string exe = Application.ExecutablePath;

            string mainLnk = Path.Combine(desktop, "�S�~���iTrush�j.lnk");
            string emptyLnk = Path.Combine(desktop, "�S�~������ɂ���.lnk");

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
            link.SetDescription("�h���b�O���h���b�v�� C:\\trushbin �ֈړ� / ���s�ő��상�j���[");

            IPersistFile file = (IPersistFile)link;
            file.Save(lnkPath, true);
        }

        // --------- COM Interop ��` ---------
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
