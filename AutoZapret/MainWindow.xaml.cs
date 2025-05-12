using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace AutoZapert
{
    public partial class MainWindow : Window
    {
        private Process winwsProcess;
        private Forms.NotifyIcon trayIcon;
        private bool isRunning = false;

        public MainWindow()
        {
            InitializeComponent();

            if (!IsAdministrator())
            {
                RelaunchAsAdmin();
                Application.Current.Shutdown();
                SystemEvents.SessionEnding += OnSessionEnding;
                return;
            }

            SetupTrayIcon();
            HookHotkey();

            // Скрываем окно, если не нужно показывать
            this.Hide();
            SystemEvents.SessionEnding += OnSessionEnding;
        }

        private void OnSessionEnding(object sender, SessionEndingEventArgs e)
        {
            // Здесь можно остановить winwsProcess и сохранить данные
            if (winwsProcess != null && !winwsProcess.HasExited)
            {
                try
                {
                    StopWinws(); // или winwsProcess.CloseMainWindow();
                    System.IO.File.AppendAllText("shutdown.log", $"Процесс завершен при выходе из Windows: {DateTime.Now}\n");
                }
                catch (Exception ex)
                {
                    System.IO.File.AppendAllText("shutdown.log", $"Ошибка завершения процесса: {DateTime.Now} {ex.Message}\n");
                }
            }
        }

        private void HookHotkey()
        {
            Thread thread = new Thread(() =>
            {
                while (true)
                {
                    if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 &&
                        Keyboard.IsKeyDown(Key.F12))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            ToggleWinws();
                        });

                        Thread.Sleep(1000); // анти-дребезг
                    }

                    Thread.Sleep(100);
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
        }

        private void ToggleWinws()
        {
            if (isRunning)
                StopWinws();
            else
                StartWinws();

            UpdateTrayMenu();
        }

        private void StartWinws()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string binDir = Path.Combine(baseDir, @"zapret\bin");
            string listDir = Path.Combine(baseDir, @"Resources");
            string exePath = Path.Combine(binDir, @"winws.exe");
            string listGeneral = Path.Combine(listDir, @"list-general.txt");
            string ipsetCloudflare = Path.Combine(listDir, @"ipset-cloudflare.txt");
            string quicFile = Path.Combine(binDir, @"quic_initial_www_google_com.bin");

            // ✅ Обновляем TextBox на форме
            TextBox1.Text = baseDir;
            TextBox1.Text += "\n" + binDir;
            TextBox1.Text += "\n" + listDir;
            TextBox1.Text += "\n" + exePath;
            TextBox1.Text += "\n" + listGeneral;
            TextBox1.Text += "\n" + ipsetCloudflare;
            TextBox1.Text += "\n" + quicFile;
            if (!File.Exists(exePath))
            {
                Forms.MessageBox.Show("winws.exe не найден!");
                return;
            }

            string args = string.Join(" ", new[]
            {
                "--wf-tcp=80,443", "--wf-udp=443,50000-50100",
                "--filter-udp=443", $"--hostlist=\"{listGeneral}\"", "--dpi-desync=fake", "--dpi-desync-repeats=11",
                $"--dpi-desync-fake-quic=\"{quicFile}\"", "--new",
                "--filter-udp=50000-50100", "--filter-l7=discord,stun", "--dpi-desync=fake", "--dpi-desync-repeats=6", "--new",
                "--filter-tcp=80", $"--hostlist=\"{listGeneral}\"", "--dpi-desync=fake,fakedsplit", "--dpi-desync-autottl=2", "--dpi-desync-fooling=md5sig", "--new",
                "--filter-tcp=443", $"--hostlist=\"{listGeneral}\"", "--dpi-desync=fake,multidisorder", "--dpi-desync-split-pos=1,midsld",
                "--dpi-desync-repeats=11", "--dpi-desync-fooling=md5sig", "--dpi-desync-fake-tls-mod=rnd,dupsid,sni=www.google.com", "--new",
                "--filter-udp=443", $"--ipset=\"{ipsetCloudflare}\"", "--dpi-desync=fake", "--dpi-desync-repeats=11",
                $"--dpi-desync-fake-quic=\"{quicFile}\"", "--new",
                "--filter-tcp=80", $"--ipset=\"{ipsetCloudflare}\"", "--dpi-desync=fake,fakedsplit", "--dpi-desync-autottl=2",
                "--dpi-desync-fooling=md5sig", "--new",
                "--filter-tcp=443", $"--ipset=\"{ipsetCloudflare}\"", "--dpi-desync=fake,multidisorder", "--dpi-desync-split-pos=1,midsld",
                "--dpi-desync-repeats=11", "--dpi-desync-fooling=md5sig"
            });

            winwsProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = args,
                    CreateNoWindow = true,
                    UseShellExecute = false
                }
            };

            try
            {
                winwsProcess.Start();
                isRunning = true;
            }
            catch (Exception ex)
            {
                Forms.MessageBox.Show($"Ошибка запуска winws.exe: {ex.Message}");
            }
        }
        private void RunCmd(string command)
        {
            ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c " + command)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (Process process = Process.Start(psi))
            {
                process.WaitForExit();
            }
        }

        private void StopWinws()
        {
            try
            {
                if (winwsProcess != null && !winwsProcess.HasExited)
                {
                    winwsProcess.Kill();
                    winwsProcess = null;
                }

                // Останавливаем WinDivert
                RunCmd("net stop WinDivert");
                RunCmd("sc delete WinDivert");
                RunCmd("net stop WinDivert14");
                RunCmd("sc delete WinDivert14");

                isRunning = false;
            }
            catch (Exception ex)
            {
                Forms.MessageBox.Show($"Ошибка остановки winws.exe: {ex.Message}");
            }
        }


        private void SetupTrayIcon()
        {
            trayIcon = new Forms.NotifyIcon
            {
                Icon = new Drawing.Icon("icon.ico"),
                Visible = true,
                Text = "AutoZapert"
            };

            UpdateTrayMenu();
        }

        private void UpdateTrayMenu()
        {
            var contextMenu = new Forms.ContextMenu();

            var toggleItem = new Forms.MenuItem(isRunning ? "Остановить" : "Запустить", (s, e) => ToggleWinws());
            var statusItem = new Forms.MenuItem($"Статус: {(isRunning ? "работает" : "остановлен")}") { Enabled = false };
            var exitItem = new Forms.MenuItem("Выход", (s, e) =>
            {
                StopWinws();
                trayIcon.Visible = false;
                Application.Current.Shutdown();
            });

            contextMenu.MenuItems.Add(toggleItem);
            contextMenu.MenuItems.Add(statusItem);
            contextMenu.MenuItems.Add("-");
            contextMenu.MenuItems.Add(exitItem);

            trayIcon.ContextMenu = contextMenu;
        }

        private bool IsAdministrator()
        {
            var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }

        private void RelaunchAsAdmin()
        {
            var exeName = Process.GetCurrentProcess().MainModule.FileName;
            var startInfo = new ProcessStartInfo(exeName)
            {
                UseShellExecute = true,
                Verb = "runas"
            };

            try
            {
                Process.Start(startInfo);
            }
            catch
            {
                Forms.MessageBox.Show("Не удалось запустить с правами администратора.");
            }
        }
    }
}
