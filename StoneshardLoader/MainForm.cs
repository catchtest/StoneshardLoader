using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace StoneshardLoader
{
    public partial class MainForm : Form
    {
        private string BasePath;
        private string[] CharacterFolders;
        private string CharacterPath;

        private static MainForm _instance;

        #region Hook
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private static readonly LowLevelKeyboardProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
            IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                if (vkCode != 0)
                {
                    _instance.CheckHotkey((Keys)vkCode);
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }
        #endregion

        public MainForm()
        {
            _instance = this;   // for static function call

            InitializeComponent();

            InitializeDropdown();

            _hookID = SetHook(_proc);

            label3.Parent = picLogo;
        }

        private void InitializeDropdown()
        {
            SetKeyDropdown(cboBackupKey, "BackupHotkey");
            SetKeyDropdown(cboLoadKey, "LoadHotkey");
        }

        private void SetKeyDropdown(ComboBox comboBox, string settingKey)
        {
            var keys = Enum.GetValues(typeof(Keys)).Cast<Keys>()
                .Where(x => x == Keys.None || (x >= Keys.F1 && x <= Keys.F12))
                .ToList();

            comboBox.DataSource = keys;
            comboBox.SelectedIndex = 0;

            var key = (Keys)Properties.Settings.Default[settingKey];
            comboBox.SelectedIndex = keys.IndexOf(key);

            comboBox.SelectedIndexChanged += (_sender, _e) =>
            {
                Properties.Settings.Default[settingKey] = (int)((ComboBox)_sender).SelectedItem;
                Properties.Settings.Default.Save();
            };
        }

        private void CheckHotkey(Keys key)
        {
            if (key == (Keys)cboBackupKey.SelectedItem)
            {
                btnBackup.PerformClick();
            }
            else if (key == (Keys)cboLoadKey.SelectedItem)
            {
                btnLoad.PerformClick();
            }
        }

        private void btnBackup_Click(object sender, EventArgs e)
        {
            var sourcePath = Path.Combine(CharacterPath, "exitsave_1");
            if (!Directory.Exists(sourcePath))
            {
                MessageBox.Show("Can't find save folder.", "Error");
                return;
            }

            var targetPath = Path.Combine(CharacterPath, $"exitsave_1_{DateTime.Now:yyyyMMdd_HHmmss}");
            if (Directory.Exists(targetPath))
            {
                System.Threading.Thread.Sleep(1000);
            }
            Directory.CreateDirectory(targetPath);

            CopyDirectory(sourcePath, targetPath);
            lblMessage.Text = "Save file copied.";
            LoadSaveFolders();
        }

        static void CopyDirectory(string sourcePath, string targetPath)
        {
            foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));
            }

            foreach (string newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
            {
                File.Copy(newPath, newPath.Replace(sourcePath, targetPath), true);
            }
        }

        private void btnLoad_Click(object sender, EventArgs e)
        {
            var targetPath = Path.Combine(CharacterPath, "exitsave_1");
            if (Directory.Exists(targetPath))
            {
                MessageBox.Show("Save folder already exists", "Error");
                return;
            }

            var folders = Directory.GetDirectories(CharacterPath, "exitsave_1_*");
            if (!folders.Any())
            {
                MessageBox.Show("Can't find save path.", "Error");
                return;
            }

            // 取得最後一個
            var sourcePath = Path.Combine(CharacterPath, folders.OrderByDescending(x => x).FirstOrDefault());
            Directory.CreateDirectory(targetPath);
            CopyDirectory(sourcePath, targetPath);

            lblMessage.Text = "Save file loaded.";
            LoadSaveFolders();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var basePath = Path.Combine(localAppDataPath, "StoneShard", "characters_v1");

            if (!Directory.Exists(basePath))
            {
                MessageBox.Show($"Can't find {basePath}");
                Close();
                return;
            }
            BasePath = basePath;

            LoadCharacterFolders();
            AutoSelectLastFolder();
        }

        private void AutoSelectLastFolder()
        {
            var lastFolder = Properties.Settings.Default["LastSelectFolder"] as string;
            if (!string.IsNullOrEmpty(lastFolder) && CharacterFolders.Contains(lastFolder))
            {
                cboSave.SelectedItem = lastFolder;
                return;
            }

            Properties.Settings.Default["LastSelectFolder"] = null;
            Properties.Settings.Default.Save();
        }

        private void LoadCharacterFolders()
        {
            var saveFolders = Directory.GetDirectories(BasePath, "character_*");
            CharacterFolders = saveFolders.Select(x => Path.GetFileName(x)).ToArray();
            cboSave.Items.AddRange(CharacterFolders);
        }

        private void btnOpenFolder_Click(object sender, EventArgs e)
        {
            Process.Start(CharacterPath);
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            var folders = Directory.GetDirectories(CharacterPath, "exitsave_1_*");
            if (folders.Count() <= 1)
            {
                MessageBox.Show("No other folder.", "Info");
                return;
            }

            var deleteFolders = folders.OrderByDescending(x => x).Skip(1);
            foreach (var folder in deleteFolders)
            {
                Directory.Delete(folder, true);
            }
            lblMessage.Text = "Delete successfully.";
            LoadSaveFolders();
        }

        private void cboSave_SelectedIndexChanged(object sender, EventArgs e)
        {
            var folderName = cboSave.SelectedItem.ToString();
            var basePath = Path.Combine(BasePath, folderName);

            if (Directory.Exists(basePath))
            {
                CharacterPath = basePath;
                LoadSaveFolders();

                Properties.Settings.Default["LastSelectFolder"] = folderName;
                Properties.Settings.Default.Save();
            }
            else
            {
                MessageBox.Show($"Folder {basePath} not found.");
                cboSave.SelectedIndex = -1;
                LoadCharacterFolders();
            }
        }

        private void LoadSaveFolders()
        {
            var folders = Directory.GetDirectories(CharacterPath);
            folders = folders.Select(x => Path.GetFileName(x)).ToArray();

            lstSaveFolder.Items.Clear();
            lstSaveFolder.Items.AddRange(folders);

            LoadThumbnail();
        }

        private void LoadThumbnail()
        {
            var selectFolder = lstSaveFolder.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selectFolder))
            {
                selectFolder = lstSaveFolder.Items.Cast<string>().Where(x => x.StartsWith("exitsave_1_")).OrderByDescending(x => x).FirstOrDefault();
            }
            if (string.IsNullOrEmpty(selectFolder))
            {
                picPreview.ImageLocation = null;
            }
            var previewPath = Path.Combine(CharacterPath, selectFolder, "preview.png");
            if (File.Exists(previewPath))
            {
                picPreview.ImageLocation = previewPath;
            }
            else
            {
                picPreview.ImageLocation = null;
            }
        }

        private void lstSaveFolder_DoubleClick(object sender, EventArgs e)
        {
            var folder = lstSaveFolder.SelectedItem?.ToString();
            if (!string.IsNullOrEmpty(folder))
            {
                Process.Start(Path.Combine(CharacterPath, folder));
            }
        }

        private void lstSaveFolder_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadThumbnail();
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            UnhookWindowsHookEx(_hookID);
        }

        private void btnGithub_Click(object sender, EventArgs e)
        {
            Process.Start("https://github.com/catchtest/StoneshardLoader");
        }
    }
}
