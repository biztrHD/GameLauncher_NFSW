﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Security.Cryptography;
using System.IO;
using System.Xml;
using System.Diagnostics;
using System.Threading;
using System.Reflection;
using System.Net.NetworkInformation;
using GameLauncher.Resources;
using GameLauncher.App.Classes;
using Newtonsoft.Json;
using SoapBox.JsonScheme;
using GameLauncher.App.Classes.Events;
using GameLauncherReborn;
using Microsoft.Win32;
using GameLauncher.App;
using GameLauncher.HashPassword;
using System.Linq;
using System.Text.RegularExpressions;
using GameLauncher.App.Classes.Logger;
using System.IO.Compression;
using GameLauncher.App.Classes.Auth;
using DiscordRPC;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Threading.Tasks;
using GameLauncher.App.Classes.ModNetReloaded;
using GameLauncher.App.Classes.HashPassword;
using GameLauncher.App.Classes.RPC;
using GameLauncher.App.Classes.GPU;
using GameLauncher.App.Classes.SystemPlatform.Windows;
using System.Management.Automation;
using WindowsFirewallHelper;
using GameLauncher.App.Classes.InsiderKit;
using System.Net.Http;
using GameLauncher.App.Classes.LauncherCore.ModNet;
using GameLauncher.App.Classes.LauncherCore.FileReadWrite;
using GameLauncher.App.Classes.LauncherCore.APICheckers;
using GameLauncher.App.Classes.LauncherCore.Visuals;
using GameLauncher.App.Classes.LauncherCore.Validator.JSON;
using GameLauncher.App.Classes.LauncherCore.Validator.Email;

namespace GameLauncher
{
    public sealed partial class MainScreen : Form
    {
        private Point _mouseDownPoint = Point.Empty;
        private bool _loginEnabled;
        private bool _serverEnabled;
        private bool _builtinserver;
        private bool _useSavedPassword;
        private bool _skipServerTrigger;
        private bool _ticketRequired;
        private bool _playenabled;
        private bool _loggedIn;
        private bool _allowRegistration;
        private bool _isDownloading = true;
        private bool _modernAuthSupport = false;
        private bool _gameKilledBySpeedBugCheck = false;
        private bool _disableLogout = false;

        public static String getTempNa = Path.GetTempFileName();

        public bool _disableProxy;
        public bool _disableDiscordRPC;

        private int _lastSelectedServerId;
        private int _nfswPid;
        private Thread _nfswstarted;

        private DateTime _downloadStartTime;
        private readonly Downloader _downloader;

        private string _serverWebsiteLink = "";
        private string _serverFacebookLink = "";
        private string _serverDiscordLink = "";
        private string _serverTwitterLink = "";
        private string _loginWelcomeTime = "";
        private string _loginToken = "";
        private string _userId = "";
        private string _serverIp = "";
        private string _langInfo;

        private readonly RichPresence _presence = new RichPresence();

        private string _presenceImageKey;
        private string _NFSW_Installation_Source;
        private string _realServername;
        private string _realServernameBanner;
        public string _OS;

        public static String ModNetFileNameInUse = String.Empty;
        readonly Queue<Uri> modFilesDownloadUrls = new Queue<Uri>();
        public List<string> ModNetFiles = new List<string>();
        public List<string> ModNetHash = new List<string>();
        bool isDownloadingModNetFiles = false;
        int CurrentModFileCount = 0;
        int TotalModFileCount = 0;

        ServerInfo _serverInfo = null;
        GetServerInformation json = new GetServerInformation();

        public static DiscordRpcClient discordRpcClient;

        readonly Dictionary<string, int> serverStatusDictionary = new Dictionary<string, int>();

        readonly String filename_pack = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GameFiles.sbrwpack");

        //UltimateLauncherFunction: SelectServer
        private static ServerInfo _ServerList;
        public static ServerInfo ServerName
        {
            get { return _ServerList; }
            set { _ServerList = value; }
        }

        public static Random random = new Random();

        private void MoveWindow_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Y <= 90) _mouseDownPoint = new Point(e.X, e.Y);
        }

        private void MoveWindow_MouseUp(object sender, MouseEventArgs e)
        {
            _mouseDownPoint = Point.Empty;
            Opacity = 1;
        }

        private void MoveWindow_MouseMove(object sender, MouseEventArgs e)
        {
            if (_mouseDownPoint.IsEmpty) { return; }
            var f = this as Form;
            f.Location = new Point(f.Location.X + (e.X - _mouseDownPoint.X), f.Location.Y + (e.Y - _mouseDownPoint.Y));
            Opacity = 0.9;
        }

        public MainScreen()
        {
            ParseUri uri = new ParseUri(Environment.GetCommandLineArgs());

            if (uri.IsDiscordPresent())
            {
                Notification.Visible = true;
                Notification.BalloonTipIcon = ToolTipIcon.Info;
                Notification.BalloonTipTitle = "SBRW Launcher";
                Notification.BalloonTipText = "Discord features are not yet completed.";
                Notification.ShowBalloonTip(5000);
                Notification.Dispose();
            }

            Log.Visuals("CORE: Entered mainScreen");

            Random rnd;
            rnd = new Random(Environment.TickCount);

            discordRpcClient = new DiscordRpcClient(Self.DiscordRPCID);

            discordRpcClient.OnReady += (sender, e) =>
            {
                Log.Debug("DISCORD: Discord ready. Detected user: " + e.User.Username + ". Discord version: " + e.Version);
                Self.discordid = e.User.ID.ToString();
            };

            discordRpcClient.OnError += (sender, e) =>
            {
                Log.Error($"DISCORD: Discord Error\n{e.Message}");
            };

            discordRpcClient.Initialize();

            Log.Debug("CORE: Setting SSL Protocol");
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

            if (DetectLinux.LinuxDetected())
            {
                ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            }

            Log.Info("LAUNCHER: Detecting OS");
            if (DetectLinux.LinuxDetected())
            {
                _OS = DetectLinux.Distro();
                Log.System("SYSTEM: Detected OS: " + _OS);
            }
            else
            {
                _OS = (string)Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\Windows NT\\CurrentVersion").GetValue("productName");
                Log.System("SYSTEM: Detected OS: " + _OS);
                if (Environment.Is64BitOperatingSystem == true)
                {
                    Log.Debug("SYSTEM: OS Type: 64 Bit");
                }
                Log.System("SYSTEM: OS Details: " + Environment.OSVersion);
                Log.System("SYSTEM: Video Card: " + GPUHelper.CardName());
                Log.System("SYSTEM: Driver Version: " + GPUHelper.DriverVersion());
            }

            _downloader = new Downloader(this, 3, 2, 16)
            {
                ProgressUpdated = new ProgressUpdated(OnDownloadProgress),
                DownloadFinished = new DownloadFinished(DownloadTracksFiles),
                DownloadFailed = new DownloadFailed(OnDownloadFailed),
                ShowMessage = new ShowMessage(OnShowMessage),
                ShowExtract = new ShowExtract(OnShowExtract)
            };

            Log.Visuals("CORE: InitializeComponent");
            InitializeComponent();

            Log.Visuals("CORE: Applying Fonts & Theme");
            SetVisuals();

            _disableProxy = (FileSettingsSave.Proxy == "1");
            _disableDiscordRPC = (FileSettingsSave.RPC == "1");
            Log.Debug("PROXY: Checking if Proxy Is Disabled from User Settings! It's value is " + _disableProxy);

            Self.CenterScreen(this);

            Log.Visuals("CORE: Disabling MaximizeBox");
            MaximizeBox = false;
            Log.Visuals("CORE: Setting Styles");
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer | ControlStyles.OptimizedDoubleBuffer, true);

            this.Load += new EventHandler(MainScreen_Load);

            this.Shown += (x, y) =>
            {
                if (UriScheme.ForceGame == true)
                {
                    PlayButton_Click(x, y);
                }

                new Thread(() =>
                {
                    discordRpcClient.Invoke();

                    //Let's fetch all servers
                    List<ServerInfo> allServs = ServerListUpdater.CleanList.FindAll(i => string.Equals(i.IsSpecial, false));
                    allServs.ForEach(delegate (ServerInfo server) {
                        try
                        {
                            WebClient pingServer = new WebClient();
                            pingServer.DownloadString(server.IpAddress + "/GetServerInformation");

                            if (!serverStatusDictionary.ContainsKey(server.Id))
                            {
                                serverStatusDictionary.Add(server.Id, 1);
                            }
                        }
                        catch
                        {
                            if (!serverStatusDictionary.ContainsKey(server.Id))
                            {
                                serverStatusDictionary.Add(server.Id, 0);
                            }
                        }
                    });
                }).Start();
            };

            Log.Core("CORE: Checking permissions");
            if (!Self.HasWriteAccessToFolder(Directory.GetCurrentDirectory()))
            {
                Log.Error("CORE: Check Permission Failed.");
                MessageBox.Show(null, "Failed to write the test file. Make sure you're running the launcher with administrative privileges.", "GameLauncher", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            Log.Core("LAUNCHER: Checking InstallationDirectory: " + FileSettingsSave.GameInstallation);
            if (string.IsNullOrEmpty(FileSettingsSave.GameInstallation))
            {
                Log.Core("LAUNCHER: First run!");

                try
                {
                    Form welcome = new WelcomeScreen();
                    DialogResult welcomereply = welcome.ShowDialog();

                    if (welcomereply != DialogResult.OK)
                    {
                        Process.GetProcessById(Process.GetCurrentProcess().Id).Kill();
                    }
                    else
                    {
                        FileSettingsSave.CDN = CDN.CDNUrl;
                        _NFSW_Installation_Source = CDN.CDNUrl;
                        FileSettingsSave.SaveSettings();
                    }
                }
                catch
                {
                    Log.Warning("LAUNCHER: CDN Source URL was Empty! Setting a Null Safe URL 'http://localhost'");
                    FileSettingsSave.CDN = "http://localhost";
                    _NFSW_Installation_Source = "http://localhost";
                    Log.Core("LAUNCHER: Installation Directory was Empty! Creating and Setting Directory at " + AppDomain.CurrentDomain.BaseDirectory + "\\Game Files");
                    FileSettingsSave.GameInstallation = AppDomain.CurrentDomain.BaseDirectory + "\\Game Files";
                    FileSettingsSave.SaveSettings();
                }

                var fbd = new CommonOpenFileDialog
                {
                    EnsurePathExists = true,
                    EnsureFileExists = false,
                    AllowNonFileSystemItems = false,
                    Title = "Select the location to Find or Download NFS:W",
                    IsFolderPicker = true
                };

                if (fbd.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    if (!Self.HasWriteAccessToFolder(fbd.FileName))
                    {
                        Log.Error("LAUNCHER: Not enough permissions. Exiting.");
                        MessageBox.Show(null, "You don't have enough permission to select this path as installation folder. Please select another directory.", "GameLauncher", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        Environment.Exit(Environment.ExitCode);
                    }

                    if (fbd.FileName == AppDomain.CurrentDomain.BaseDirectory)
                    {
                        Directory.CreateDirectory("Game Files");
                        Log.Warning("LAUNCHER: Installing NFSW in same directory where the launcher resides is disadvised.");
                        MessageBox.Show(null, string.Format("Installing NFSW in same directory where the launcher resides is disadvised. Instead, we will install it on {0}.", AppDomain.CurrentDomain.BaseDirectory + "\\Game Files"), "GameLauncher", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        FileSettingsSave.GameInstallation = AppDomain.CurrentDomain.BaseDirectory + "\\Game Files";
                        FileSettingsSave.SaveSettings();
                    }
                    else
                    {
                        Log.Core("LAUNCHER: Directory Set: " + fbd.FileName);
                        FileSettingsSave.GameInstallation = fbd.FileName;
                        FileSettingsSave.SaveSettings();
                    }
                }
                else
                {
                    Log.Core("LAUNCHER: Exiting");
                    Environment.Exit(Environment.ExitCode);
                }
                fbd.Dispose();
            }

            if (!DetectLinux.LinuxDetected())
            {
                CheckGameFilesDirectoryPrevention();

                Log.Visuals("CORE: Setting cursor.");
                string temporaryFile = Path.GetTempFileName();
                File.WriteAllBytes(temporaryFile, ExtractResource.AsByte("GameLauncher.SoapBoxModules.cursor.ani"));
                Cursor mycursor = new Cursor(Cursor.Current.Handle);
                IntPtr colorcursorhandle = User32.LoadCursorFromFile(temporaryFile);
                mycursor.GetType().InvokeMember("handle", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.SetField, null, mycursor, new object[] { colorcursorhandle });
                Cursor = mycursor;
                File.Delete(temporaryFile);

                //Windows Defender (Windows 10)
                if (WindowsProductVersion.GetWindowsNumber() >= 10.0 && (FileSettingsSave.WindowsDefenderStatus == "Not Excluded"))
                {
                    Log.Core("WINDOWS DEFENDER: Windows 10 Detected! Running Exclusions for Core Folders");
                    WindowsDefenderFirstRun();
                }
                else if (WindowsProductVersion.GetWindowsNumber() >= 10.0 && !string.IsNullOrEmpty(FileSettingsSave.WindowsDefenderStatus))
                {
                    Log.Core("WINDOWS DEFENDER: Found 'WindowsDefender' key! Its value is " + FileSettingsSave.WindowsDefenderStatus);
                }
            }

            Log.Core("CORE: Loading ModManager Cache");
            ModManager.LoadModCache();
        }

        private void ComboBox1_DrawItem(object sender, DrawItemEventArgs e)
        {
            var font = (sender as ComboBox).Font;
            Brush backgroundColor;
            Brush textColor;

            var serverListText = "";
            int onlineStatus = 2; //0 = offline | 1 = online | 2 = checking

            if (sender is ComboBox cb)
            {
                if (cb.Items[e.Index] is ServerInfo si)
                {
                    serverListText = si.Name;
                    onlineStatus = serverStatusDictionary.ContainsKey(si.Id) ? serverStatusDictionary[si.Id] : 2;
                }
            }

            if (serverListText.StartsWith("<GROUP>"))
            {
                font = new Font(font, FontStyle.Bold);
                e.Graphics.FillRectangle(Brushes.White, e.Bounds);
                e.Graphics.DrawString(serverListText.Replace("<GROUP>", string.Empty), font, Brushes.Black, e.Bounds);
            }
            else
            {
                font = new Font(font, FontStyle.Regular);
                if ((e.State & DrawItemState.Selected) == DrawItemState.Selected && e.State != DrawItemState.ComboBoxEdit)
                {
                    backgroundColor = SystemBrushes.Highlight;
                    textColor = SystemBrushes.HighlightText;
                }
                else
                {
                    if (onlineStatus == 2)
                    {
                        //CHECKING
                        backgroundColor = Brushes.Khaki;
                    }
                    else if (onlineStatus == 1)
                    {
                        //ONLINE
                        backgroundColor = Brushes.PaleGreen;
                    }
                    else
                    {
                        //OFFLINE
                        backgroundColor = Brushes.LightCoral;
                    }

                    textColor = Brushes.Black;
                }

                e.Graphics.FillRectangle(backgroundColor, e.Bounds);
                e.Graphics.DrawString("    " + serverListText, font, textColor, e.Bounds);
            }
        }

        private void MainScreen_Load(object sender, EventArgs e)
        {
            Log.Visuals("CORE: Entering mainScreen_Load");

            Log.Visuals("CORE: Setting WindowName");
            Text = "SBRW Launcher: v" + Application.ProductVersion;

            Log.Core("CORE: Centering Window location");
            Self.CenterScreen(this);

            if (!string.IsNullOrEmpty(EnableInsider.BuildNumber()))
            {
                InsiderBuildNumberText.Visible = EnableInsider.ShouldIBeAnInsider();
                InsiderBuildNumberText.Text = "Insider Build Date: " + EnableInsider.BuildNumber();
            }

            _NFSW_Installation_Source = !string.IsNullOrEmpty(FileSettingsSave.CDN) ? FileSettingsSave.CDN : "http://localhost";
            Log.Core("LAUNCHER: NFSW Download Source is now: " + _NFSW_Installation_Source);

            Log.Visuals("CORE: Applyinng ContextMenu");
            translatedBy.Text = "";
            ContextMenu = new ContextMenu();
            ContextMenu.MenuItems.Add(new MenuItem("About", AboutButton_Click));
            ContextMenu.MenuItems.Add(new MenuItem("Donate", (b, n) => { Process.Start("https://paypal.me/metonator95"); }));
            ContextMenu.MenuItems.Add("-");
            //ContextMenu.MenuItems.Add(new MenuItem("Settings", SettingsButton_Click));
            ContextMenu.MenuItems.Add(new MenuItem("Add Server", AddServer_Click));
            ContextMenu.MenuItems.Add("-");
            ContextMenu.MenuItems.Add(new MenuItem("Close launcher", CloseBTN_Click));

            Notification.ContextMenu = ContextMenu;
            Notification.Icon = new Icon(Icon, Icon.Width, Icon.Height);
            Notification.Text = "GameLauncher";
            Notification.Visible = true;

            ContextMenu = null;

            MainEmail.Text = FileAccountSave.UserRawEmail;
            MainPassword.Text = Properties.Settings.Default.PasswordDecoded;
            if (!string.IsNullOrEmpty(FileAccountSave.UserRawEmail) && !string.IsNullOrEmpty(FileAccountSave.UserHashedPassword))
            {
                Log.Core("LAUNCHER: Restoring last saved email and password");
                RememberMe.Checked = true;
            }

            /* Server Display List */
            ServerPick.DisplayMember = "Name";
            Log.Core("LAUNCHER: Setting server list");
            ServerPick.DataSource = ServerListUpdater.CleanList;

            //ForceSelectServer
            if (string.IsNullOrEmpty(FileAccountSave.ChoosenGameServer))
            {
                //SelectServerBtn_Click(null, null);
                new SelectServer().ShowDialog();

                if (ServerName != null)
                {
                    this.SelectServerBtn.Text = "[...] " + ServerName.Name;
                    FileAccountSave.ChoosenGameServer = ServerName.IpAddress;
                    FileAccountSave.SaveAccount();
                }
                else
                {
                    Process.GetProcessById(Process.GetCurrentProcess().Id).Kill();
                }
            }

            Log.Core("SERVERLIST: Checking...");
            Log.Core("SERVERLIST: Setting first server in list");
            Log.Core("SERVERLIST: Checking if server is set on INI File");
            try
            {
                if (string.IsNullOrEmpty(FileAccountSave.ChoosenGameServer))
                {
                    Log.Warning("SERVERLIST: Failed to find anything... assuming " + ((ServerInfo)ServerPick.SelectedItem).IpAddress);
                    FileAccountSave.ChoosenGameServer = ((ServerInfo)ServerPick.SelectedItem).IpAddress;
                    FileAccountSave.SaveAccount();
                }
            }
            catch
            {
                Log.Error("SERVERLIST: Failed to write anything...");
                FileAccountSave.ChoosenGameServer = string.Empty;
                FileAccountSave.SaveAccount();
            }

            Log.Core("SERVERLIST: Re-Checking if server is set on INI File");
            if (!string.IsNullOrEmpty(FileAccountSave.ChoosenGameServer))
            {
                Log.Core("SERVERLIST: Found something!");
                _skipServerTrigger = true;

                Log.Core("SERVERLIST: Checking if server exists on our database");

                if (ServerListUpdater.CleanList.FindIndex(i => string.Equals(i.IpAddress, FileAccountSave.ChoosenGameServer)) != 0 /*_slresponse.Contains(_settingFile.Read("Server"))*/)
                {
                    Log.Core("SERVERLIST: Server found! Checking ID");
                    var index = ServerListUpdater.CleanList.FindIndex(i => string.Equals(i.IpAddress, FileAccountSave.ChoosenGameServer));

                    Log.Core("SERVERLIST: ID is " + index);
                    if (index >= 0)
                    {
                        Log.Core("SERVERLIST: ID set correctly");
                        ServerPick.SelectedIndex = index;
                    }
                }
                else
                {
                    Log.Warning("SERVERLIST: Unable to find anything, assuming default");
                    ServerPick.SelectedIndex = 1;
                    Log.Warning("SERVERLIST: Deleting unknown entry");
                    FileAccountSave.ChoosenGameServer = string.Empty;
                    FileAccountSave.SaveAccount();
                }

                Log.Core("SERVERLIST: Triggering server change");
                if (ServerPick.SelectedIndex == 1)
                {
                    ServerPick_SelectedIndexChanged(sender, e);
                }
                Log.Core("SERVERLIST: All done");
            }

            Log.Core("LAUNCHER: Checking for password");
            if (!string.IsNullOrEmpty(FileAccountSave.UserHashedPassword))
            {
                _loginEnabled = true;
                _serverEnabled = true;
                _useSavedPassword = true;
                LoginButton.BackgroundImage = Theming.GrayButton;
                LoginButton.ForeColor = Theming.FivithTextForeColor;
            }
            else
            {
                _loginEnabled = false;
                _serverEnabled = false;
                _useSavedPassword = false;
                LoginButton.BackgroundImage = Theming.GrayButton;
                LoginButton.ForeColor = Theming.SixTextForeColor;
            }

            /* Fix this at a later date - DavidCarbon */
            /*
            Log.Core("LAUNCHER: Setting game language");

            if (_settingFile.KeyExists("Language"))
            {
                SettingsLanguage.SelectedValue = _settingFile.Read("Language");
            }
            */

            Log.Core("LAUNCHER: Re-checking InstallationDirectory: " + FileSettingsSave.GameInstallation);

            var drive = Path.GetPathRoot(FileSettingsSave.GameInstallation);
            if (!Directory.Exists(drive))
            {
                if (!string.IsNullOrEmpty(drive))
                {
                    var newdir = AppDomain.CurrentDomain.BaseDirectory + "\\Game Files";
                    FileSettingsSave.GameInstallation = newdir;
                    FileSettingsSave.SaveSettings();
                    Log.Error(string.Format("LAUNCHER: Drive {0} was not found. Your actual installation directory is set to {1} now.", drive, newdir));

                    MessageBox.Show(null, string.Format("Drive {0} was not found. Your actual installation directory is set to {1} now.", drive, newdir), "GameLauncher", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            Log.Core("CORE: Setting Registry Options");
            try
            {
                var gameInstallDirValue = Registry.GetValue("HKEY_LOCAL_MACHINE\\software\\Electronic Arts\\Need For Speed World", "GameInstallDir", RegistryValueKind.String);
                if (gameInstallDirValue == null || gameInstallDirValue.ToString() != Path.GetFullPath(FileSettingsSave.GameInstallation))
                {
                    try
                    {
                        Registry.SetValue("HKEY_LOCAL_MACHINE\\software\\Electronic Arts\\Need For Speed World", "GameInstallDir", Path.GetFullPath(FileSettingsSave.GameInstallation));
                        Registry.SetValue("HKEY_LOCAL_MACHINE\\software\\Electronic Arts\\Need For Speed World", "LaunchInstallDir", Path.GetFullPath(Application.ExecutablePath));
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
            }

            Log.Core("DISCORD: Initializing DiscordRPC");
            Log.Core("DISCORD: Checking if Discord RPC is Disabled from User Settings! It's value is " + _disableDiscordRPC);

            _presence.State = _OS;
            _presence.Details = "In-Launcher: " + Application.ProductVersion;
            _presence.Assets = new Assets
            {
                LargeImageText = "SBRW",
                LargeImageKey = "nfsw"
            };
            if (discordRpcClient != null) discordRpcClient.SetPresence(_presence);

            BeginInvoke((MethodInvoker)delegate
            {
                Log.Core("CORE: 'GetServerInformation' from all Servers in Server List and Download Selected Server Banners");
                LaunchNfsw();
            });

            this.BringToFront();

            if (!DetectLinux.LinuxDetected())
            {
                new LauncherUpdateCheck(LauncherIconStatus, LauncherStatusText, LauncherStatusDesc).ChangeVisualStatus();
            }
            else
            {
                LauncherIconStatus.BackgroundImage = Theming.UpdateIconSuccess;
                LauncherStatusText.ForeColor = Theming.Sucess;
                LauncherStatusText.Text = "Launcher Status:\n - Linux Build";
                LauncherStatusDesc.Text = "Version: v" + Application.ProductVersion;
            }

            /* Load Settings API Connection Status */
            Task.Delay(800);
            PingServerListAPIStatus();

            /* Remove TracksHigh Folder and Files */
            RemoveTracksHighFiles();
        }

        private void AboutButton_Click(object sender, EventArgs e)
        {
            new About().ShowDialog();
        }

        private void CloseBTN_Click(object sender, EventArgs e)
        {
            CloseBTN.BackgroundImage = Theming.CloseButtonClick;

            FileSettingsSave.SaveSettings();
            FileAccountSave.SaveAccount();

            Process[] allOfThem = Process.GetProcessesByName("nfsw");
            foreach (var oneProcess in allOfThem)
            {
                Process.GetProcessById(oneProcess.Id).Kill();
            }

            //Kill DiscordRPC
            if (discordRpcClient != null)
            {
                discordRpcClient.Dispose();
            }

            ServerProxy.Instance.Stop();
            Notification.Dispose();

            var linksPath = Path.Combine(FileSettingsSave.GameInstallation + "\\.links");
            ModNetLinksCleanup.CleanLinks(linksPath);

            //Leave this here. Its to properly close the launcher from Visual Studio (And Close the Launcher a well)
            try { this.Close(); } catch { }
        }

        private void AddServer_Click(object sender, EventArgs e)
        {
            new AddServer().Show();
        }

        private void OpenDebugWindow(object sender, EventArgs e)
        {
            if (!(ServerPick.SelectedItem is ServerInfo server)) return;

            var form = new DebugWindow(server.IpAddress, server.Name);
            form.Show();
        }

        private void CloseBTN_MouseEnter(object sender, EventArgs e)
        {
            CloseBTN.BackgroundImage = Theming.CloseButtonHover;
        }

        private void CloseBTN_MouseLeave(object sender, EventArgs e)
        {
            CloseBTN.BackgroundImage = Theming.CloseButton;
        }

        private void LoginEnter(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Return && _loginEnabled)
            {
                LoginButton_Click(null, null);
                e.SuppressKeyPress = true;
            }
        }

        private void Loginbuttonenabler(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(MainEmail.Text) || string.IsNullOrEmpty(MainPassword.Text))
            {
                _loginEnabled = false;
                LoginButton.BackgroundImage = Theming.GrayButton;
                LoginButton.ForeColor = Theming.SixTextForeColor;
            }
            else
            {
                _loginEnabled = true;
                LoginButton.BackgroundImage = Theming.GrayButton;
                LoginButton.ForeColor = Theming.FivithTextForeColor;
            }

            _useSavedPassword = false;
        }

        private void LoginButton_MouseUp(object sender, EventArgs e)
        {
            if (_loginEnabled || _builtinserver)
            {
                LoginButton.BackgroundImage = Theming.GrayButtonHover;
            }
            else
            {
                LoginButton.BackgroundImage = Theming.GrayButton;
            }
        }

        private void LoginButton_MouseDown(object sender, EventArgs e)
        {
            if (_loginEnabled || _builtinserver)
            {
                LoginButton.BackgroundImage = Theming.GrayButtonClick;
            }
            else
            {
                LoginButton.BackgroundImage = Theming.GrayButton;
            }
        }

        private void LoginButton_Click(object sender, EventArgs e)
        {
            if ((_loginEnabled == false || _serverEnabled == false) && _builtinserver == false)
            {
                return;
            }

            if (_isDownloading)
            {
                MessageBox.Show(null, "Please wait while launcher is still downloading gamefiles.", "GameLauncher", MessageBoxButtons.OK);
                return;
            }

            Tokens.Clear();

            String username = MainEmail.Text.ToString();
            String realpass;

            Tokens.IPAddress = _serverInfo.IpAddress;
            Tokens.ServerName = _serverInfo.Name;

            Self.userAgent = _serverInfo.ForceUserAgent ?? null;

            if (_modernAuthSupport == false)
            {
                //ClassicAuth sends password in SHA1
                realpass = (_useSavedPassword) ? FileAccountSave.UserHashedPassword : SHA.HashPassword(MainPassword.Text.ToString()).ToLower();
                ClassicAuth.Login(username, realpass);
            }
            else
            {
                //ModernAuth sends passwords in plaintext, but is POST request
                realpass = (_useSavedPassword) ? FileAccountSave.UserHashedPassword : MainPassword.Text.ToString();
                ModernAuth.Login(username, realpass);
            }

            if (RememberMe.Checked)
            {
                FileAccountSave.UserRawEmail = username;
                FileAccountSave.UserHashedPassword = realpass;
                FileAccountSave.SaveAccount();
                Properties.Settings.Default.PasswordDecoded = MainPassword.Text.ToString();
            }
            else
            {
                FileAccountSave.UserRawEmail = string.Empty;
                FileAccountSave.UserHashedPassword = string.Empty;
                FileAccountSave.SaveAccount();
                Properties.Settings.Default.PasswordDecoded = String.Empty;
            }

            Properties.Settings.Default.Save();

            try
            {
                if (!(ServerPick.SelectedItem is ServerInfo server)) return;
                FileAccountSave.ChoosenGameServer = server.IpAddress;
            }
            catch
            {
            }

            if (String.IsNullOrEmpty(Tokens.Error))
            {
                _loggedIn = true;
                _userId = Tokens.UserId;
                _loginToken = Tokens.LoginToken;
                _serverIp = Tokens.IPAddress;

                if (!String.IsNullOrEmpty(Tokens.Warning))
                {
                    MessageBox.Show(null, Tokens.Warning, "GameLauncher", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                LoginFormElements(false);
                LoggedInFormElements(true);
            }
            else
            {
                //Main Screen Login
                MainEmailBorder.Image = Theming.BorderEmailError;
                MainPasswordBorder.Image = Theming.BorderPasswordError;
                MessageBox.Show(null, Tokens.Error, "GameLauncher", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoginButton_MouseEnter(object sender, EventArgs e)
        {
            if (_loginEnabled || _builtinserver)
            {
                LoginButton.BackgroundImage = Theming.GrayButtonHover;
                LoginButton.ForeColor = Theming.FivithTextForeColor;
            }
            else
            {
                LoginButton.BackgroundImage = Theming.GrayButton;
                LoginButton.ForeColor = Theming.SixTextForeColor;
            }
        }

        private void LoginButton_MouseLeave(object sender, EventArgs e)
        {
            if (_loginEnabled || _builtinserver)
            {
                LoginButton.BackgroundImage = Theming.GrayButton;
                LoginButton.ForeColor = Theming.FivithTextForeColor;
            }
            else
            {
                LoginButton.BackgroundImage = Theming.GrayButton;
                LoginButton.ForeColor = Theming.SixTextForeColor;
            }
        }

        private void ServerPick_SelectedIndexChanged(object sender, EventArgs e)
        {
            MainEmailBorder.Image = Theming.BorderEmail;
            MainPasswordBorder.Image = Theming.BorderPassword;

            //ServerStatusBar(_colorLoading, _startPoint, _endPoint);

            _serverInfo = (ServerInfo)ServerPick.SelectedItem;
            _realServername = _serverInfo.Name;
            _realServernameBanner = _serverInfo.Name;
            _modernAuthSupport = false;

            if (_serverInfo.IsSpecial)
            {
                ServerPick.SelectedIndex = _lastSelectedServerId;
                return;
            }

            if (!_skipServerTrigger) { return; }

            _lastSelectedServerId = ServerPick.SelectedIndex;
            _allowRegistration = false;
            _loginEnabled = false;

            ServerStatusText.Text = "Server Status:\n - Pinging";
            ServerStatusText.ForeColor = Theming.SecondaryTextForeColor;
            ServerStatusDesc.Text = "";
            ServerStatusIcon.BackgroundImage = Theming.ServerIconChecking;

            LoginButton.ForeColor = Theming.SixTextForeColor;
            var verticalImageUrl = "";
            VerticalBanner.Image = VerticalBanners.Grayscale(".BannerCache/" + SHA.HashPassword(_realServernameBanner) + ".bin");
            VerticalBanner.BackColor = Theming.VerticalBannerBackColor;

            var serverIp = _serverInfo.IpAddress;
            string numPlayers;
            string numRegistered;

            //Disable Social Panel when switching
            DisableSocialPanelandClearIt();

            if (ServerPick.GetItemText(ServerPick.SelectedItem) == "Offline Built-In Server")
            {
                _builtinserver = true;
                LoginButton.BackgroundImage = Theming.GrayButton;
                LoginButton.Text = "Launch".ToUpper();
                LoginButton.ForeColor = Theming.FivithTextForeColor;
                ServerInfoPanel.Visible = false;
            }
            else
            {
                _builtinserver = false;
                LoginButton.BackgroundImage = Theming.GrayButton;
                LoginButton.Text = "Login".ToUpper();
                LoginButton.ForeColor = Theming.SixTextForeColor;
                ServerInfoPanel.Visible = false;
            }

            WebClient client = new WebClient();
            var artificialPingStart = Self.GetTimestamp();
            VerticalBanner.BackColor = Color.Transparent;

            var stringToUri = new Uri(serverIp + "/GetServerInformation");
            client.DownloadStringAsync(stringToUri);

            System.Timers.Timer aTimer = new System.Timers.Timer(10000);
            aTimer.Elapsed += (x, y) => { client.CancelAsync(); };
            aTimer.Enabled = true;

            client.DownloadStringCompleted += (sender2, e2) =>
            {
                aTimer.Enabled = false;

                var artificialPingEnd = Self.GetTimestamp();

                if (e2.Cancelled)
                {
                    //ServerStatusBar(_colorOffline, _startPoint, _endPoint);

                    ServerStatusText.Text = "Server Status:\n - Offline ( OFF )";
                    ServerStatusText.ForeColor = Theming.Error;
                    ServerStatusDesc.Text = "Failed to connect to server.";
                    ServerStatusIcon.BackgroundImage = Theming.ServerIconOffline;
                    _serverEnabled = false;
                    _allowRegistration = false;
                    //Disable Login & Register Button
                    LoginButton.Enabled = false;
                    RegisterText.Enabled = false;
                    //Disable Social Panel
                    DisableSocialPanelandClearIt();

                    if (!serverStatusDictionary.ContainsKey(_serverInfo.Id))
                    {
                        serverStatusDictionary.Add(_serverInfo.Id, 2);
                    }
                    else
                    {
                        serverStatusDictionary[_serverInfo.Id] = 2;
                    }
                }
                else if (e2.Error != null)
                {
                    //ServerStatusBar(_colorOffline, _startPoint, _endPoint);

                    ServerStatusText.Text = "Server Status:\n - Offline ( OFF )";
                    ServerStatusText.ForeColor = Theming.Error;
                    ServerStatusDesc.Text = "Server seems to be offline.";
                    ServerStatusIcon.BackgroundImage = Theming.ServerIconOffline;
                    _serverEnabled = false;
                    _allowRegistration = false;
                    //Disable Login & Register Button
                    LoginButton.Enabled = false;
                    RegisterText.Enabled = false;
                    //Disable Social Panel
                    DisableSocialPanelandClearIt();

                    if (!serverStatusDictionary.ContainsKey(_serverInfo.Id))
                    {
                        serverStatusDictionary.Add(_serverInfo.Id, 0);
                    }
                    else
                    {
                        serverStatusDictionary[_serverInfo.Id] = 0;
                    }
                }
                else
                {
                    if (_realServername == "Offline Built-In Server")
                    {
                        DisableSocialPanelandClearIt();
                        numPlayers = "∞";
                        numRegistered = "∞";
                    }
                    else
                    {
                        if (!serverStatusDictionary.ContainsKey(_serverInfo.Id))
                        {
                            serverStatusDictionary.Add(_serverInfo.Id, 1);
                        }
                        else
                        {
                            serverStatusDictionary[_serverInfo.Id] = 1;
                        }
                        //Enable Social Panel
                        ServerInfoPanel.Visible = true;

                        String purejson = String.Empty;
                        purejson = e2.Result;
                        json = JsonConvert.DeserializeObject<GetServerInformation>(e2.Result);
                        Self.rememberjson = e2.Result;
                        try
                        {
                            if (!string.IsNullOrEmpty(json.BannerUrl))
                            {
                                bool result;

                                try
                                {
                                    result = Uri.TryCreate(json.BannerUrl, UriKind.Absolute, out Uri uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
                                }
                                catch
                                {
                                    result = false;
                                }

                                if (result)
                                {
                                    verticalImageUrl = json.BannerUrl;
                                }
                                else
                                {
                                    verticalImageUrl = null;
                                }
                            }
                            else
                            {
                                verticalImageUrl = null;
                            }
                        }
                        catch
                        {
                            verticalImageUrl = null;
                        }

                        /* Social Panel Core */

                        //Discord Invite Display
                        try
                        {
                            if (string.IsNullOrEmpty(json.DiscordUrl))
                            {
                                DiscordIcon.BackgroundImage = Theming.DiscordIconDisabled;
                                DiscordInviteLink.Enabled = false;
                                _serverDiscordLink = null;
                                DiscordInviteLink.Text = "";
                            }
                            else
                            {
                                DiscordIcon.BackgroundImage = Theming.DiscordIcon;
                                DiscordInviteLink.Enabled = true;
                                _serverDiscordLink = json.DiscordUrl;
                                DiscordInviteLink.Text = "Discord Invite";
                            }
                        }
                        catch
                        {
                            DiscordIcon.BackgroundImage = Theming.DiscordIconDisabled;
                            DiscordInviteLink.Enabled = false;
                            _serverDiscordLink = null;
                            DiscordInviteLink.Text = "";
                        }

                        //Homepage Display
                        try
                        {
                            if (string.IsNullOrEmpty(json.HomePageUrl))
                            {
                                HomePageIcon.BackgroundImage = Theming.HomeIconDisabled;
                                HomePageLink.Enabled = false;
                                _serverWebsiteLink = null;
                                HomePageLink.Text = "";
                            }
                            else
                            {
                                HomePageIcon.BackgroundImage = Theming.HomeIcon;
                                HomePageLink.Enabled = true;
                                _serverWebsiteLink = json.HomePageUrl;
                                HomePageLink.Text = "Home Page";
                            }
                        }
                        catch
                        {
                            HomePageIcon.BackgroundImage = Theming.HomeIconDisabled;
                            HomePageLink.Enabled = false;
                            _serverWebsiteLink = null;
                            HomePageLink.Text = "";
                        }

                        //Facebook Group Display
                        try
                        {
                            if (string.IsNullOrEmpty(json.FacebookUrl) || json.FacebookUrl == "Your facebook page url")
                            {
                                FacebookIcon.BackgroundImage = Theming.FacebookIconDisabled;
                                FacebookGroupLink.Enabled = false;
                                _serverFacebookLink = null;
                                FacebookGroupLink.Text = "";
                            }
                            else
                            {
                                FacebookIcon.BackgroundImage = Theming.FacebookIcon;
                                FacebookGroupLink.Enabled = true;
                                _serverFacebookLink = json.FacebookUrl;
                                FacebookGroupLink.Text = "Facebook Page";
                            }
                        }
                        catch
                        {
                            FacebookIcon.BackgroundImage = Theming.FacebookIconDisabled;
                            FacebookGroupLink.Enabled = false;
                            _serverFacebookLink = null;
                            FacebookGroupLink.Text = "";
                        }

                        //Twitter Account Display
                        try
                        {
                            if (string.IsNullOrEmpty(json.TwitterUrl))
                            {
                                TwitterIcon.BackgroundImage = Theming.TwitterIconDisabled;
                                TwitterAccountLink.Enabled = false;
                                _serverTwitterLink = null;
                                TwitterAccountLink.Text = "";
                            }
                            else
                            {
                                TwitterIcon.BackgroundImage = Theming.TwitterIcon;
                                TwitterAccountLink.Enabled = true;
                                _serverTwitterLink = json.TwitterUrl;
                                TwitterAccountLink.Text = "Twitter Feed";
                            }
                        }
                        catch
                        {
                            TwitterIcon.BackgroundImage = Theming.TwitterIconDisabled;
                            TwitterAccountLink.Enabled = false;
                            _serverTwitterLink = null;
                            TwitterAccountLink.Text = "";
                        }

                        //Server Set Speedbug Timer Display
                        try
                        {
                            int serverSecondsToShutDown = (json.SecondsToShutDown != 0) ? json.SecondsToShutDown : 2 * 60 * 60;
                            TimeSpan t = TimeSpan.FromSeconds(serverSecondsToShutDown);
                            string serverSecondsToShutDownNamed = string.Format("Gameplay Timer: " + t.Hours + " Hours");

                            this.ServerShutDown.Text = serverSecondsToShutDownNamed;
                        }
                        catch
                        {
                            this.ServerShutDown.Text = "∞ and Beyond";
                        }

                        //Scenery Group Display
                        switch (String.Join("", json.ActivatedHolidaySceneryGroups))
                        {
                            case "SCENERY_GROUP_NEWYEARS":
                                this.SceneryGroupText.Text = "Scenery: New Years";
                                break;
                            case "SCENERY_GROUP_OKTOBERFEST":
                                this.SceneryGroupText.Text = "Scenery: OKTOBERFEST";
                                break;
                            case "SCENERY_GROUP_HALLOWEEN":
                                this.SceneryGroupText.Text = "Scenery: Halloween";
                                break;
                            case "SCENERY_GROUP_CHRISTMAS":
                                this.SceneryGroupText.Text = "Scenery: Christmas";
                                break;
                            default:
                                this.SceneryGroupText.Text = "Scenery: Normal";
                                break;
                        }

                        try
                        {
                            if (string.IsNullOrEmpty(json.RequireTicket))
                            {
                                _ticketRequired = true;
                            }
                            else if (json.RequireTicket == "true")
                            {
                                _ticketRequired = true;
                            }
                            else
                            {
                                _ticketRequired = false;
                            }
                        }
                        catch
                        {
                            _ticketRequired = false;
                        }

                        try
                        {
                            if (string.IsNullOrEmpty(json.ModernAuthSupport))
                            {
                                _modernAuthSupport = false;
                            }
                            else if (json.ModernAuthSupport == "true")
                            {
                                if (stringToUri.Scheme == "https")
                                {
                                    _modernAuthSupport = true;
                                }
                                else
                                {
                                    _modernAuthSupport = false;
                                }
                            }
                            else
                            {
                                _modernAuthSupport = false;
                            }
                        }
                        catch
                        {
                            _modernAuthSupport = false;
                        }

                        if (json.MaxUsersAllowed == 0)
                        {
                            numPlayers = string.Format("{0}", json.OnlineNumber);
                            numRegistered = string.Format("{0}", json.NumberOfRegistered);
                        }
                        else
                        {
                            numPlayers = string.Format("{0} / {1}", json.OnlineNumber, json.MaxUsersAllowed.ToString());
                            numRegistered = string.Format("{0}", json.NumberOfRegistered);
                        }

                        _allowRegistration = true;

                        //ServerStatusBar(_colorOnline, _startPoint, _endPoint);
                    }

                    try
                    {
                        ServerStatusText.Text = "Server Status:\n - Online ( ON )";
                        ServerStatusText.ForeColor = Theming.Sucess;
                        ServerStatusIcon.BackgroundImage = Theming.ServerIconSuccess;
                        _loginEnabled = true;
                        //Enable Login & Register Button
                        LoginButton.ForeColor = Theming.FivithTextForeColor;
                        LoginButton.Enabled = true;
                        RegisterText.Enabled = true;

                        if (((ServerInfo)ServerPick.SelectedItem).Category == "DEV")
                        {
                            //Disable Social Panel
                            DisableSocialPanelandClearIt();
                        }
                    }
                    catch
                    {
                        //¯\_(ツ)_/¯
                    }

                    if (!DetectLinux.LinuxDetected())
                    {
                        ServerPingStatusText.ForeColor = Theming.FivithTextForeColor;

                        Ping pingSender = new Ping();
                        pingSender.SendAsync(stringToUri.Host, 1000, new byte[1], new PingOptions(64, true), new AutoResetEvent(false));
                        pingSender.PingCompleted += (sender3, e3) => {
                            PingReply reply = e3.Reply;

                            if (reply.Status == IPStatus.Success && _realServername != "Offline Built-In Server")
                            {
                                if (this.ServerPingStatusText.InvokeRequired)
                                {
                                    ServerStatusDesc.Invoke(new Action(delegate () {
                                        ServerPingStatusText.Text = string.Format("Your Ping to the Server \n{0}".ToUpper(), reply.RoundtripTime + "ms");
                                    }));
                                }
                                else
                                {
                                    this.ServerPingStatusText.Text = string.Format("Your Ping to the Server \n{0}".ToUpper(), reply.RoundtripTime + "ms");
                                }
                            }
                            else
                            {
                                this.ServerPingStatusText.Text = string.Format("");
                            }
                        };
                    }
                    else
                    {
                        this.ServerPingStatusText.Text = string.Format("");
                    }

                    //for thread safety
                    if (this.ServerStatusDesc.InvokeRequired)
                    {
                        ServerStatusDesc.Invoke(new Action(delegate ()
                        {
                            ServerStatusDesc.Text = string.Format("Players Online: {0}\nRegistered: {1}", numPlayers, numRegistered);
                        }));
                    }
                    else
                    {
                        this.ServerStatusDesc.Text = string.Format("Players Online: {0}\nRegistered: {1}", numPlayers, numRegistered);
                    }

                    _serverEnabled = true;

                    if (!Directory.Exists(".BannerCache")) { Directory.CreateDirectory(".BannerCache"); }
                    if (!string.IsNullOrEmpty(verticalImageUrl))
                    {

                        WebClient client2 = new WebClient();
                        Uri stringToUri3 = new Uri(verticalImageUrl);
                        client2.DownloadDataAsync(stringToUri3);
                        client2.DownloadProgressChanged += (sender4, e4) =>
                        {
                            if (e4.TotalBytesToReceive > 2000000)
                            {
                                client2.CancelAsync();
                                Log.Warning("Unable to Cache " + _realServername + " Server Banner! {Over 2MB?}");
                            }
                        };

                        client2.DownloadDataCompleted += (sender4, e4) =>
                        {
                            if (e4.Cancelled)
                            {
                                //Load cached banner!
                                VerticalBanner.Image = VerticalBanners.Grayscale(".BannerCache/" + SHA.HashPassword(_realServernameBanner) + ".bin");
                                VerticalBanner.BackColor = Theming.VerticalBannerBackColor;
                                return;
                            }
                            else if (e4.Error != null)
                            {
                                //Load cached banner!
                                VerticalBanner.Image = VerticalBanners.Grayscale(".BannerCache/" + SHA.HashPassword(_realServernameBanner) + ".bin");
                                VerticalBanner.BackColor = Theming.VerticalBannerBackColor;
                                return;
                            }
                            else
                            {
                                try
                                {
                                    if (UriScheme.ForceGame != true)
                                    {
                                        Image image;
                                        var memoryStream = new MemoryStream(e4.Result);
                                        image = Image.FromStream(memoryStream);

                                        VerticalBanner.Image = image;
                                        VerticalBanner.BackColor = Theming.VerticalBannerBackColor;

                                        Console.WriteLine(VerticalBanners.GetFileExtension(verticalImageUrl));

                                        if (VerticalBanners.GetFileExtension(verticalImageUrl) != "gif")
                                        {
                                            File.WriteAllBytes(".BannerCache/" + SHA.HashPassword(_realServernameBanner) + ".bin", memoryStream.ToArray());
                                        }
                                    }
                                    else
                                    {
                                        VerticalBanner.Image = null;
                                        VerticalBanner.BackColor = Theming.VerticalBannerBackColor;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(ex.Message);
                                    Log.Error(ex.Message);
                                    VerticalBanner.Image = null;
                                }
                            }
                        };
                    }
                    else
                    {
                        //Load cached banner!
                        VerticalBanner.Image = VerticalBanners.Grayscale(".BannerCache/" + SHA.HashPassword(_realServernameBanner) + ".bin");
                        VerticalBanner.BackColor = Theming.VerticalBannerBackColor;
                    }
                }
            };
        }

        private void RegisterText_LinkClicked(object sender, EventArgs e)
        {
            if (_allowRegistration)
            {
                if (!string.IsNullOrEmpty(json.WebSignupUrl))
                {
                    Process.Start(json.WebSignupUrl);
                    MessageBox.Show(null, "A browser window has been opened to complete registration on " + json.ServerName, "GameLauncher", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (_realServername == "WorldUnited Official" || _realServername == "WorldUnited OFFICIAL")
                {
                    Process.Start("https://signup.worldunited.gg/?discordid=" + Self.discordid);
                    MessageBox.Show(null, "A browser window has been opened to complete registration on WorldUnited OFFICIAL", "GameLauncher", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                CurrentWindowInfo.Text = "REGISTER ON \n" + _realServername.ToUpper();
                LoginFormElements(false);
                RegisterFormElements(true);
            }
            else
            {
                MessageBox.Show(null, "Server seems to be offline.", "GameLauncher", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ForgotPassword_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (!string.IsNullOrEmpty(json.WebRecoveryUrl))
            {
                Process.Start(json.WebRecoveryUrl);
                MessageBox.Show(null, "A browser window has been opened to complete password recovery on " + json.ServerName, "GameLauncher", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            else
            {
                string send = Prompt.ShowDialog("Please specify your email address.", "GameLauncher");

                if (send != String.Empty)
                {
                    String responseString;
                    try
                    {
                        Uri resetPasswordUrl = new Uri(_serverInfo.IpAddress + "/RecoveryPassword/forgotPassword");

                        var request = (HttpWebRequest)System.Net.WebRequest.Create(resetPasswordUrl);
                        var postData = "email=" + send;
                        var data = Encoding.ASCII.GetBytes(postData);
                        request.Method = "POST";
                        request.ContentType = "application/x-www-form-urlencoded";
                        request.ContentLength = data.Length;

                        using (var stream = request.GetRequestStream())
                        {
                            stream.Write(data, 0, data.Length);
                        }

                        var response = (HttpWebResponse)request.GetResponse();
                        responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();
                    }
                    catch
                    {
                        responseString = "Failed to send email!";
                    }

                    MessageBox.Show(null, responseString, "GameLauncher", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        /* Main Screen Elements */

        /* Social Panel | Ping or Offline | */
        private void DisableSocialPanelandClearIt()
        {
            //Hides Social Panel
            ServerInfoPanel.Visible = false;
            //Home
            HomePageIcon.BackgroundImage = Theming.HomeIconDisabled;
            HomePageLink.Enabled = false;
            _serverWebsiteLink = null;
            //Discord
            DiscordIcon.BackgroundImage = Theming.DiscordIconDisabled;
            DiscordInviteLink.Enabled = false;
            _serverDiscordLink = null;
            //Facebook
            FacebookIcon.BackgroundImage = Theming.FacebookIconDisabled;
            FacebookGroupLink.Enabled = false;
            _serverFacebookLink = null;
            //Twitter
            TwitterIcon.BackgroundImage = Theming.TwitterIconDisabled;
            TwitterAccountLink.Enabled = false;
            _serverTwitterLink = null;
            //Scenery
            SceneryGroupText.Text = "But It's Me!";
            //Restart Timer
            ServerShutDown.Text = "Game Launcher!";
        }

        /*  After Successful Login, Hide Login Forms */
        private void LoggedInFormElements(bool hideElements)
        {
            if (hideElements == true)
            {
                DateTime currentTime = DateTime.Now;

                if (currentTime.Hour < 12)
                {
                    _loginWelcomeTime = "Good Morning";
                }
                else if (currentTime.Hour <= 16)
                {
                    _loginWelcomeTime = "Good Afternoon";
                }
                else if (currentTime.Hour <= 20)
                {
                    _loginWelcomeTime = "Good Evening";
                }
                else
                {
                    _loginWelcomeTime = "Good Night";
                }

                CurrentWindowInfo.Text = string.Format(_loginWelcomeTime + "\n{0}", MainEmail.Text).ToUpper();

                PlayButton.BackgroundImage = Theming.PlayButton;
                PlayButton.ForeColor = Theming.FivithTextForeColor;

                LogoutButton.BackgroundImage = Theming.GrayButton;
                LogoutButton.ForeColor = Theming.FivithTextForeColor;
            }

            ShowPlayPanel.Visible = hideElements;
        }

        private void LoginFormElements(bool hideElements)
        {
            if (hideElements == true)
                CurrentWindowInfo.Text = "Enter Your Account Information to Log In".ToUpper();

            RememberMe.Visible = hideElements;
            LoginButton.Visible = hideElements;

            RegisterText.Visible = hideElements;
            MainEmail.Visible = hideElements;
            MainPassword.Visible = hideElements;
            ForgotPassword.Visible = hideElements;
            SettingsButton.Visible = hideElements;

            AddServer.Enabled = hideElements;
            ServerPick.Enabled = hideElements;
            SelectServerBtn.Enabled = hideElements;

            //Input Strokes
            MainEmailBorder.Visible = hideElements;
            MainEmailBorder.Image = Theming.BorderEmail;
            MainPasswordBorder.Visible = hideElements;
            MainPasswordBorder.Image = Theming.BorderPassword;
        }

        private void RegisterFormElements(bool hideElements)
        {
            bool CertainElemnts;

            if (hideElements == true)
            {
                CertainElemnts = false;
                RegisterEmail.BackColor = Theming.Input;
                RegisterPassword.BackColor = Theming.Input;
                RegisterConfirmPassword.BackColor = Theming.Input;
                RegisterTicket.BackColor = Theming.Input;

                RegisterButton.BackgroundImage = Theming.GreenButton;
                RegisterButton.ForeColor = Theming.SeventhTextForeColor;

                RegisterCancel.BackgroundImage = Theming.GrayButton;
                RegisterCancel.ForeColor = Theming.FivithTextForeColor;

                RegisterAgree.ForeColor = Theming.FivithTextForeColor;

                RegisterEmail.ForeColor = Theming.FivithTextForeColor;
                RegisterPassword.ForeColor = Theming.FivithTextForeColor;
                RegisterConfirmPassword.ForeColor = Theming.FivithTextForeColor;
                RegisterTicket.ForeColor = Theming.FivithTextForeColor;
            }
            else
            {
                CertainElemnts = true;
            }

            RegisterPanel.Visible = hideElements;
            RegisterTicket.Visible = _ticketRequired && hideElements;

            AddServer.Enabled = CertainElemnts;
            ServerPick.Enabled = CertainElemnts;

            // Reset fields
            RegisterEmail.Text = "";
            RegisterPassword.Text = "";
            RegisterConfirmPassword.Text = "";
            RegisterAgree.Checked = false;

            RegisterAgree.ForeColor = Theming.FivithTextForeColor;
            //Reset Input Stroke Images
            RegisterEmailBorder.Image = Theming.BorderEmail;
            RegisterPasswordBorder.Image = Theming.BorderPassword;
            RegisterConfirmPasswordBorder.Image = Theming.BorderPassword;
            RegisterTicketBorder.Image = Theming.BorderTicket;

            //Input Strokes
            RegisterEmailBorder.Visible = hideElements;
            RegisterPasswordBorder.Visible = hideElements;
            RegisterConfirmPasswordBorder.Visible = hideElements;
            RegisterTicketBorder.Visible = _ticketRequired && hideElements;
        }

        private void LogoutButton_Click(object sender, EventArgs e)
        {
            if (_disableLogout == true)
            {
                return;
            }

            _loggedIn = false;
            LoggedInFormElements(false);
            LoginFormElements(true);

            _userId = String.Empty;
            _loginToken = String.Empty;
        }

        private void Greenbutton_hover_MouseEnter(object sender, EventArgs e)
        {
            RegisterText.BackgroundImage = Theming.GreenButtonHover;
            RegisterButton.BackgroundImage = Theming.GreenButtonHover;
        }

        private void Greenbutton_MouseLeave(object sender, EventArgs e)
        {
            RegisterText.BackgroundImage = Theming.GreenButton;
            RegisterButton.BackgroundImage = Theming.GreenButton;
        }

        private void Greenbutton_hover_MouseUp(object sender, EventArgs e)
        {
            RegisterText.BackgroundImage = Theming.GreenButtonHover;
            RegisterButton.BackgroundImage = Theming.GreenButtonHover;
        }

        private void Greenbutton_click_MouseDown(object sender, EventArgs e)
        {
            RegisterText.BackgroundImage = Theming.GreenButtonClick;
            RegisterButton.BackgroundImage = Theming.GreenButtonClick;
        }

        private void RegisterCancel_Click(object sender, EventArgs e)
        {
            RegisterFormElements(false);
            LoginFormElements(true);
        }

        private void RegisterAgree_CheckedChanged(object sender, EventArgs e)
        {
            RegisterAgree.ForeColor = Theming.FivithTextForeColor;
        }

        private void RegisterEmail_TextChanged(object sender, EventArgs e)
        {
            RegisterEmailBorder.Image = Theming.BorderEmail;
        }

        private void RegisterTicket_TextChanged(object sender, EventArgs e)
        {
            RegisterTicketBorder.Image = Theming.BorderTicket;
        }

        private void RegisterConfirmPassword_TextChanged(object sender, EventArgs e)
        {
            RegisterConfirmPasswordBorder.Image = Theming.BorderPassword;
        }

        private void RegisterPassword_TextChanged(object sender, EventArgs e)
        {
            RegisterPasswordBorder.Image = Theming.BorderPassword;
        }

        private void Email_TextChanged(object sender, EventArgs e)
        {
            MainEmailBorder.Image = Theming.BorderEmail;
        }

        private void Password_TextChanged(object sender, EventArgs e)
        {
            MainEmailBorder.Image = Theming.BorderEmail;
            MainPasswordBorder.Image = Theming.BorderPassword;
        }

        private void Graybutton_click_MouseDown(object sender, EventArgs e)
        {
            LogoutButton.BackgroundImage = Theming.GrayButtonClick;
            RegisterCancel.BackgroundImage = Theming.GrayButtonClick;
        }

        private void Graybutton_hover_MouseEnter(object sender, EventArgs e)
        {
            LogoutButton.BackgroundImage = Theming.GrayButtonHover;
            RegisterCancel.BackgroundImage = Theming.GrayButtonHover;
        }

        private void Graybutton_MouseLeave(object sender, EventArgs e)
        {
            LogoutButton.BackgroundImage = Theming.GrayButton;
            RegisterCancel.BackgroundImage = Theming.GrayButton;
        }

        private void Graybutton_hover_MouseUp(object sender, EventArgs e)
        {
            LogoutButton.BackgroundImage = Theming.GrayButtonHover;
            RegisterCancel.BackgroundImage = Theming.GrayButtonHover;
        }

        public void DrawErrorAroundTextBox(TextBox x)
        {
            x.BorderStyle = BorderStyle.FixedSingle;
            var p = new Pen(Color.Red);
            var g = CreateGraphics();
            var variance = 1;
            g.DrawRectangle(p, new Rectangle(x.Location.X - variance, x.Location.Y - variance, x.Width + variance, x.Height + variance));
        }

        private void RegisterButton_Click(object sender, EventArgs e)
        {
            Refresh();

            List<string> registerErrors = new List<string>();

            if (string.IsNullOrEmpty(RegisterEmail.Text))
            {
                registerErrors.Add("Please enter your e-mail.");
                RegisterEmailBorder.Image = Theming.BorderEmailError;

            }
            else if (IsEmailValid.Validate(RegisterEmail.Text) == false)
            {
                registerErrors.Add("Please enter a valid e-mail address.");
                RegisterEmailBorder.Image = Theming.BorderEmailError;
            }

            if (string.IsNullOrEmpty(RegisterTicket.Text) && _ticketRequired)
            {
                registerErrors.Add("Please enter your ticket.");
                RegisterTicketBorder.Image = Theming.BorderTicketError;
            }

            if (string.IsNullOrEmpty(RegisterPassword.Text))
            {
                registerErrors.Add("Please enter your password.");
                RegisterPasswordBorder.Image = Theming.BorderPasswordError;
            }

            if (string.IsNullOrEmpty(RegisterConfirmPassword.Text))
            {
                registerErrors.Add("Please confirm your password.");
                RegisterConfirmPasswordBorder.Image = Theming.BorderPasswordError;
            }

            if (RegisterConfirmPassword.Text != RegisterPassword.Text)
            {
                registerErrors.Add("Passwords don't match.");
                RegisterConfirmPasswordBorder.Image = Theming.BorderPasswordError;
            }

            if (!RegisterAgree.Checked)
            {
                registerErrors.Add("You have not agreed to the Terms of Service.");
                RegisterAgree.ForeColor = Theming.Error;
            }

            if (registerErrors.Count == 0)
            {
                bool allowReg = false;

                try
                {
                    WebClient breachCheck = new WebClient();
                    String checkPassword = SHA.HashPassword(RegisterPassword.Text.ToString()).ToUpper();

                    var regex = new Regex(@"([0-9A-Z]{5})([0-9A-Z]{35})").Split(checkPassword);

                    String range = regex[1];
                    String verify = regex[2];
                    String serverReply = breachCheck.DownloadString("https://api.pwnedpasswords.com/range/" + range);

                    string[] hashes = serverReply.Split('\n');
                    foreach (string hash in hashes)
                    {
                        var splitChecks = hash.Split(':');
                        if (splitChecks[0] == verify)
                        {
                            var passwordCheckReply = MessageBox.Show(null, "Password used for registration has been breached " + Convert.ToInt32(splitChecks[1]) + " times, you should consider using different one.\r\nAlternatively you can use unsafe password anyway. Use it?", "GameLauncher", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                            if (passwordCheckReply == DialogResult.Yes)
                            {
                                allowReg = true;
                            }
                            else
                            {
                                allowReg = false;
                            }
                        }
                        else
                        {
                            allowReg = true;
                        }
                    }
                }
                catch
                {
                    allowReg = true;
                }

                if (allowReg == true)
                {
                    Tokens.Clear();

                    String username = RegisterEmail.Text.ToString();
                    String realpass;
                    String token = (_ticketRequired) ? RegisterTicket.Text : null;

                    Tokens.IPAddress = _serverInfo.IpAddress;
                    Tokens.ServerName = _serverInfo.Name;

                    if (_modernAuthSupport == false)
                    {
                        realpass = SHA.HashPassword(RegisterPassword.Text.ToString()).ToLower();
                        ClassicAuth.Register(username, realpass, token);
                    }
                    else
                    {
                        realpass = RegisterPassword.Text.ToString();
                        ModernAuth.Register(username, realpass, token);
                    }

                    if (!String.IsNullOrEmpty(Tokens.Success))
                    {
                        _loggedIn = true;
                        _userId = Tokens.UserId;
                        _loginToken = Tokens.LoginToken;
                        _serverIp = Tokens.IPAddress;

                        MessageBox.Show(null, Tokens.Success, "GameLauncher", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                        RegisterFormElements(false);
                        LoginFormElements(true);

                        _loggedIn = true;
                    }
                    else
                    {
                        MessageBox.Show(null, Tokens.Error, "GameLauncher", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else
                {
                    var message = "There were some errors while registering, please fix them:\n\n";

                    foreach (var error in registerErrors)
                    {
                        message += "• " + error + "\n";
                    }

                    MessageBox.Show(null, message, "GameLauncher", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /* SETTINGS PAGE LAYOUT */
        private void SettingsButton_Click(object sender, EventArgs e)
        {
            SettingsButton.BackgroundImage = Theming.GearButtonClick;

            if (!(ServerPick.SelectedItem is ServerInfo server)) return;

            new SettingsScreen(server.IpAddress, server.Name).ShowDialog();
        }

        private void SettingsButton_MouseEnter(object sender, EventArgs e)
        {
            SettingsButton.BackgroundImage = Theming.GearButtonHover;
        }

        private void SettingsButton_MouseLeave(object sender, EventArgs e)
        {
            SettingsButton.BackgroundImage = Theming.GearButton;
        }

        private void StartGame(string userId, string loginToken)
        {
            if (UriScheme.ServerIP != String.Empty)
            {
                _serverIp = UriScheme.ServerIP;
            }

            if (_realServername == "Freeroam Sparkserver")
            {
                //Force proxy enabled.
                Log.Core("LAUNCHER: Forcing Proxified connection for FRSS");
                _disableProxy = false;
            }

            _nfswstarted = new Thread(() =>
            {
                if (_disableProxy == true)
                {
                    if (_disableDiscordRPC == true)
                    {
                        discordRpcClient.Dispose();
                        discordRpcClient = null;
                    }

                    Uri convert = new Uri(_serverIp);

                    if (convert.Scheme == "http")
                    {
                        Match match = Regex.Match(convert.Host, @"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}");
                        if (!match.Success)
                        {
                            _serverIp = _serverIp.Replace(convert.Host, Self.HostName2IP(convert.Host));
                        }
                    }

                    LaunchGame(userId, loginToken, _serverIp, this);
                }
                else
                {
                    if (_disableDiscordRPC == true)
                    {
                        discordRpcClient.Dispose();
                        discordRpcClient = null;
                    }
                    LaunchGame(userId, loginToken, "http://127.0.0.1:" + Self.ProxyPort + "/nfsw/Engine.svc", this);
                }
            })
            { IsBackground = true };

            _nfswstarted.Start();

            if (_disableDiscordRPC == false)
            {
                _presenceImageKey = _serverInfo.DiscordPresenceKey;
                _presence.State = _realServername;
                _presence.Details = "In-Game";
                _presence.Assets = new Assets
                {
                    LargeImageText = "Need for Speed: World",
                    LargeImageKey = "nfsw",
                    SmallImageText = _realServername,
                    SmallImageKey = _presenceImageKey
                };

                if (discordRpcClient != null) discordRpcClient.SetPresence(_presence);
            }
        }

        //Check Serverlist API Status Upon Main Screen load - DavidCarbon
        private void PingServerListAPIStatus()
        {
            APIStatusText.Text = "United API:\n - Online";
            APIStatusText.ForeColor = Theming.Sucess;
            APIStatusDesc.Text = "Connected to API";
            APIStatusIcon.BackgroundImage = Theming.APIIconSuccess;

            if (VisualsAPIChecker.UnitedAPI != true)
            {
                APIStatusText.Text = "Carbon API:\n - Online";
                APIStatusText.ForeColor = Theming.Sucess;
                APIStatusDesc.Text = "Connected to API";
                APIStatusIcon.BackgroundImage = Theming.APIIconSuccess;
            }
            else if (VisualsAPIChecker.CarbonAPI != true)
            {
                APIStatusText.Text = "Carbon 2nd API:\n - Online";
                APIStatusText.ForeColor = Theming.Sucess;
                APIStatusDesc.Text = "Connected to API";
                APIStatusIcon.BackgroundImage = Theming.APIIconSuccess;
            }
            else if (VisualsAPIChecker.CarbonAPITwo != true)
            {
                APIStatusText.Text = "WOPL API:\n - Online";
                APIStatusText.ForeColor = Theming.Sucess;
                APIStatusDesc.Text = "Connected to API";
                APIStatusIcon.BackgroundImage = Theming.APIIconSuccess;
            }
            else if (VisualsAPIChecker.WOPLAPI != true)
            {
                APIStatusText.Text = "Connection API:\n - Error";
                APIStatusText.ForeColor = Theming.Error;
                APIStatusDesc.Text = "Failed to Connect to APIs";
                APIStatusIcon.BackgroundImage = Theming.APIIconError;
                Log.Api("PINGING API: Failed to Connect to APIs! Quick Hide and Bunker Down! (Ask for help)");
            }
        }

        private void LaunchGame(string userId, string loginToken, string serverIp, Form x)
        {
            var oldfilename = FileSettingsSave.GameInstallation + "/nfsw.exe";

            var args = _serverInfo.Id.ToUpper() + " " + serverIp + " " + loginToken + " " + userId;
            var psi = new ProcessStartInfo();

            if (DetectLinux.LinuxDetected())
            {
                psi.UseShellExecute = false;
            }

            psi.WorkingDirectory = FileSettingsSave.GameInstallation;
            psi.FileName = oldfilename;
            psi.Arguments = args;

            var nfswProcess = Process.Start(psi);
            nfswProcess.PriorityClass = ProcessPriorityClass.AboveNormal;

            var processorAffinity = 0;
            for (var i = 0; i < Math.Min(Math.Max(1, Environment.ProcessorCount), 8); i++)
            {
                processorAffinity |= 1 << i;
            }

            nfswProcess.ProcessorAffinity = (IntPtr)processorAffinity;

            AntiCheat.process_id = nfswProcess.Id;

            //TIMER HERE
            int secondsToShutDown = (json.SecondsToShutDown != 0) ? json.SecondsToShutDown : 2 * 60 * 60;
            System.Timers.Timer shutdowntimer = new System.Timers.Timer();
            shutdowntimer.Elapsed += (x2, y2) =>
            {
                if (secondsToShutDown == 300)
                {
                    Notification.Visible = true;
                    Notification.BalloonTipIcon = ToolTipIcon.Info;
                    Notification.BalloonTipTitle = "SpeedBug Fix - " + _realServername;
                    Notification.BalloonTipText = "Game is going to shut down in 5 minutes. Please restart it manually before the launcher does it.";
                    Notification.ShowBalloonTip(5000);
                    Notification.Dispose();
                }

                Process[] allOfThem = Process.GetProcessesByName("nfsw");

                if (secondsToShutDown <= 0)
                {
                    if (Self.CanDisableGame == true)
                    {
                        foreach (var oneProcess in allOfThem)
                        {
                            _gameKilledBySpeedBugCheck = true;
                            Process.GetProcessById(oneProcess.Id).Kill();
                        }
                    }
                    else
                    {
                        secondsToShutDown = 0;
                    }
                }

                //change title

                foreach (var oneProcess in allOfThem)
                {
                    long p = oneProcess.MainWindowHandle.ToInt64();
                    TimeSpan t = TimeSpan.FromSeconds(secondsToShutDown);
                    string secondsToShutDownNamed = string.Format("{0:D2}:{1:D2}:{2:D2}", t.Hours, t.Minutes, t.Seconds);

                    if (secondsToShutDown == 0)
                    {
                        secondsToShutDownNamed = "Waiting for event to finish.";
                    }

                    User32.SetWindowText((IntPtr)p, "NEED FOR SPEED™ WORLD | Server: " + _realServername + " | Launcher Build: " + ProductVersion + " | Force Restart In: " + secondsToShutDownNamed);
                }

                --secondsToShutDown;
            };

            shutdowntimer.Interval = 1000;
            shutdowntimer.Enabled = true;

            if (nfswProcess != null)
            {
                nfswProcess.EnableRaisingEvents = true;
                _nfswPid = nfswProcess.Id;

                nfswProcess.Exited += (sender2, e2) =>
                {
                    _nfswPid = 0;
                    var exitCode = nfswProcess.ExitCode;

                    if (_gameKilledBySpeedBugCheck == true) exitCode = 2137;

                    if (exitCode == 0)
                    {
                        CloseBTN_Click(null, null);
                    }
                    else
                    {
                        x.BeginInvoke(new Action(() =>
                        {
                            x.WindowState = FormWindowState.Normal;
                            x.Opacity = 1;
                            x.ShowInTaskbar = true;
                            String errorMsg = "Game Crash with exitcode: " + exitCode.ToString() + " (0x" + exitCode.ToString("X") + ")";
                            if (exitCode == -1073741819) errorMsg = "Game Crash: Access Violation (0x" + exitCode.ToString("X") + ")";
                            if (exitCode == -1073740940) errorMsg = "Game Crash: Heap Corruption (0x" + exitCode.ToString("X") + ")";
                            if (exitCode == -1073740791) errorMsg = "Game Crash: Stack buffer overflow (0x" + exitCode.ToString("X") + ")";
                            if (exitCode == -805306369) errorMsg = "Game Crash: Application Hang (0x" + exitCode.ToString("X") + ")";
                            if (exitCode == -1073741515) errorMsg = "Game Crash: Missing dependency files (0x" + exitCode.ToString("X") + ")";
                            if (exitCode == -1073740972) errorMsg = "Game Crash: Debugger crash (0x" + exitCode.ToString("X") + ")";
                            if (exitCode == -1073741676) errorMsg = "Game Crash: Division by Zero (0x" + exitCode.ToString("X") + ")";
                            if (exitCode == 1) errorMsg = "The process nfsw.exe was killed via Task Manager";
                            if (exitCode == 2137) errorMsg = "Launcher killed your game to prevent SpeedBugging.";
                            if (exitCode == -3) errorMsg = "The Server was unable to resolve the request";
                            if (exitCode == -4) errorMsg = "Another instance is already executed";
                            if (exitCode == -5) errorMsg = "DirectX Device was not found. Please install GPU Drivers before playing";
                            if (exitCode == -6) errorMsg = "Server was unable to resolve your request";
                            //ModLoader
                            if (exitCode == 2) errorMsg = "ModNet: Game was launched with invalid command line parameters.";
                            if (exitCode == 3) errorMsg = "ModNet: .links file should not exist upon startup!";
                            if (exitCode == 4) errorMsg = "ModNet: An Unhandled Error Appeared";
                            PlayProgressText.Text = errorMsg.ToUpper();
                            PlayProgress.Value = 100;
                            PlayProgress.ForeColor = Theming.Error;
                            if (_nfswPid != 0)
                            {
                                try
                                {
                                    Process.GetProcessById(_nfswPid).Kill();
                                }
                                catch { /* ignored */ }
                            }

                            _nfswstarted.Abort();
                            DialogResult restartApp = MessageBox.Show(null, errorMsg + "\nWould you like to restart the launcher?", "GameLauncher", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                            if (restartApp == DialogResult.Yes)
                            {
                                Properties.Settings.Default.IsRestarting = true;
                                Properties.Settings.Default.Save();
                                Application.Restart();
                                Application.ExitThread();
                            }
                            this.CloseBTN_Click(null, null);
                        }));
                    }
                };
            }
        }

        public void DownloadModNetFilesRightNow(string path)
        {
            while (isDownloadingModNetFiles == false)
            {
                CurrentModFileCount++;
                var url = modFilesDownloadUrls.Dequeue();
                string FileName = url.ToString().Substring(url.ToString().LastIndexOf("/") + 1, (url.ToString().Length - url.ToString().LastIndexOf("/") - 1));

                ModNetFileNameInUse = FileName;

                try
                {
                    WebClient client2 = new WebClient();

                    client2.DownloadProgressChanged += new DownloadProgressChangedEventHandler(Client_DownloadProgressChanged_RELOADED);
                    client2.DownloadFileCompleted += (test, stuff) =>
                    {
                        Log.Core("LAUNCHER: Downloaded: " + FileName);
                        isDownloadingModNetFiles = false;
                        if (modFilesDownloadUrls.Any() == false)
                        {
                            LaunchGame();
                        }
                        else
                        {
                            //Redownload other file
                            DownloadModNetFilesRightNow(path);
                        }
                    };
                    client2.DownloadFileAsync(url, path + "/" + FileName);
                }
                catch
                {
                    CurrentWindowInfo.Text = string.Format(_loginWelcomeTime + "\n{0}", IsEmailValid.Mask(FileAccountSave.UserRawEmail)).ToUpper();
                }

                isDownloadingModNetFiles = true;
            }
        }

        private async void PlayButton_Click(object sender, EventArgs e)
        {
            /* Disable Play Button and Logout Buttons */
            PlayButton.Visible = false;
            LogoutButton.Visible = false;

            if (UriScheme.ForceGame != true)
            {
                if (_loggedIn == false)
                {
                    if (_useSavedPassword == false) return;
                    LoginButton_Click(sender, e);
                }

                if (_playenabled == false)
                {
                    return;
                }
            }
            else
            {
                //set background black
                VerticalBanner.Image = null;

                _userId = UriScheme.UserID;
                _loginToken = UriScheme.LoginToken;
                _serverIp = UriScheme.ServerIP;
            }

            _disableLogout = true;
            DisablePlayButton();

            if (!DetectLinux.LinuxDetected())
            {
                var installDir = FileSettingsSave.GameInstallation;
                DriveInfo driveInfo = new DriveInfo(installDir);

                if (!string.Equals(driveInfo.DriveFormat, "NTFS", StringComparison.InvariantCultureIgnoreCase))
                {
                    MessageBox.Show(
                        $"Playing the game on a non-NTFS-formatted drive is not supported.\nDrive '{driveInfo.Name}' is formatted with: {driveInfo.DriveFormat}",
                        "Compatibility",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }
            }

            ModManager.ResetModDat(FileSettingsSave.GameInstallation);

            if (Directory.Exists(FileSettingsSave.GameInstallation + "/modules")) Directory.Delete(FileSettingsSave.GameInstallation + "/modules", true);
            if (!Directory.Exists(FileSettingsSave.GameInstallation + "/scripts")) Directory.CreateDirectory(FileSettingsSave.GameInstallation + "/scripts");

            /* Get Remote ModNet list to process for checking required ModNet files are present and current */
            String modules = new WebClient().DownloadString(Self.modnetserver + "/launcher-modules/modules.json");
            string[] modules_newlines = modules.Split(new string[] { "\n" }, StringSplitOptions.None);
            foreach (String modules_newline in modules_newlines)
            {
                if (modules_newline.Trim() == "{" || modules_newline.Trim() == "}") continue;

                String trim_modules_newline = modules_newline.Trim();
                String[] modules_files = trim_modules_newline.Split(new char[] { ':' });
                /* Make the dynamic Filenames list for checking */
                String ModNetList = modules_files[0].Replace("\"", "");
                ModNetFiles.Add(ModNetList);
                ModNetFiles.ToList();
                /* Make another for the SHA's to validate against */
                String ModNetSHA = modules_files[1].Replace("\"", "").Replace(",", "");
                ModNetHash.Add(ModNetSHA);
                ModNetHash.ToList();
            }

            Log.Core("LAUNCHER: Installing ModNet");
            PlayProgressText.Text = ("Detecting ModNet Support for " + _realServernameBanner).ToUpper();
            String jsonModNet = ModNetReloaded.ModNetSupported(_serverIp);

            if (jsonModNet != String.Empty)
            {
                PlayProgressText.Text = "ModNet support detected, setting up...".ToUpper();

                try
                {
                    string[] newFiles = ModNetFiles.ToArray();

                    foreach (string file in newFiles)
                    {
                        var url = Self.modnetserver + "/launcher-modules/" + file;
                        string fileETAG = null;
                        if (EnableInsider.ShouldIBeAnInsider() == true)
                        {
                            Log.Debug(url);
                        }
                        using (var client = new HttpClient())
                        {
                            string FinalETagHash = null;
                            var response = await client.GetAsync(url);
                            var etag1 = response.Headers.ETag;
                            if (EnableInsider.ShouldIBeAnInsider() == true)
                            {
                                Log.Debug("ETAG: " + etag1);
                            }

                            if (etag1 != null)
                            {
                                FinalETagHash = etag1.ToString().TrimStart('"').TrimEnd('"');
                                fileETAG = FinalETagHash.ToUpper();
                            }
                            var content = await response.Content.ReadAsStringAsync();
                        }

                        if (EnableInsider.ShouldIBeAnInsider() == true)
                        {
                            Log.Debug("ETAG FINAL: " + fileETAG);
                        }

                        if (fileETAG == null && File.Exists(FileSettingsSave.GameInstallation + "/" + file))
                        {
                            PlayProgressText.Text = ("ModNet: Fail Safe -> Found " + file).ToUpper();

                            Log.Debug("MODNET CORE: Using Local " + file + " File! (Unable to get ETAG for File)");
                        }
                        else if (MDFive.HashFile(FileSettingsSave.GameInstallation + "/" + file) != fileETAG || !File.Exists(FileSettingsSave.GameInstallation + "/" + file))
                        {
                            PlayProgressText.Text = ("ModNet: Downloading " + file).ToUpper();

                            Log.Warning("MODNET CORE: " + file + " Does not match MD5 Hash on File Server -> Online Hash: '" + fileETAG + "'");

                            if (File.Exists(FileSettingsSave.GameInstallation + "/" + file))
                            {
                                File.Delete(FileSettingsSave.GameInstallation + "/" + file);
                            }

                            WebClient newModNetFilesDownload = new WebClient();
                            newModNetFilesDownload.DownloadFile(Self.modnetserver + "/launcher-modules/" + file, FileSettingsSave.GameInstallation + "/" + file);
                        }
                        else
                        {
                            PlayProgressText.Text = ("ModNet: Up to Date " + file).ToUpper();

                            Log.Debug("MODNET CORE: " + file + " Is Up to Date!");
                        }

                        Application.DoEvents();

                    }

                    //get files now
                    MainJson json2 = JsonConvert.DeserializeObject<MainJson>(jsonModNet);

                    //metonator was here!
                    String remoteCarsFile = String.Empty;
                    String remoteEventsFile = String.Empty;
                    try
                    {
                        remoteCarsFile = new WebClient().DownloadString(json2.BasePath + "/cars.json");
                    }
                    catch { }

                    try
                    {
                        remoteEventsFile = new WebClient().DownloadString(json2.BasePath + "/events.json");
                    }
                    catch { }

                    //Version 1.3 @metonator - DavidCarbon
                    if (IsJSONValid.ValidJson(remoteCarsFile) == true)
                    {
                        Log.Info("DISCORD: Found RemoteRPC List for cars.json");
                        CarsList.remoteCarsList = remoteCarsFile;
                    }
                    else
                    {
                        Log.Warning("DISCORD: RemoteRPC List for cars.json does not exist");
                        CarsList.remoteCarsList = String.Empty;
                    }

                    if (IsJSONValid.ValidJson(remoteEventsFile) == true)
                    {
                        Log.Info("DISCORD: Found RemoteRPC List for events.json");
                        EventsList.remoteEventsList = remoteEventsFile;
                    }
                    else
                    {
                        Log.Warning("DISCORD: RemoteRPC List for events.json does not exist");
                        EventsList.remoteEventsList = String.Empty;
                    }

                    //get new index
                    Uri newIndexFile = new Uri(json2.BasePath + "/index.json");
                    Log.Core("CORE: Loading Server Mods List");
                    String jsonindex = new WebClient().DownloadString(newIndexFile);

                    IndexJson json3 = JsonConvert.DeserializeObject<IndexJson>(jsonindex);

                    int CountFilesTotal = 0;
                    CountFilesTotal = json3.Entries.Count;

                    String path = Path.Combine(FileSettingsSave.GameInstallation, "MODS", MDFive.HashPassword(json2.ServerID).ToLower());
                    if (!Directory.Exists(path)) Directory.CreateDirectory(path);

                    foreach (IndexJsonEntry modfile in json3.Entries)
                    {
                        if (SHA.HashFile(path + "/" + modfile.Name).ToLower() != modfile.Checksum)
                        {
                            modFilesDownloadUrls.Enqueue(new Uri(json2.BasePath + "/" + modfile.Name));
                            TotalModFileCount++;
                        }
                    }

                    if (modFilesDownloadUrls.Count != 0)
                    {
                        this.DownloadModNetFilesRightNow(path);
                        _presence.State = "Downloading Server Mods!";
                        if (discordRpcClient != null) discordRpcClient.SetPresence(_presence);
                    }
                    else
                    {
                        LaunchGame();
                    }

                    foreach (var file in Directory.GetFiles(path))
                    {
                        var name = Path.GetFileName(file);

                        if (json3.Entries.All(en => en.Name != name))
                        {
                            Log.Core("LAUNCHER: removing package: " + file);
                            try
                            {
                                File.Delete(file);
                            }
                            catch (Exception ex)
                            {
                                Log.Error($"Failed to remove {file}: {ex.Message}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("LAUNCHER " + ex.Message);
                    CurrentWindowInfo.Text = string.Format(_loginWelcomeTime + "\n{0}", IsEmailValid.Mask(FileAccountSave.UserRawEmail)).ToUpper();
                    MessageBox.Show(null, $"There was an error downloading ModNet Files:\n{ex.Message}", "GameLauncher", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                //Rofl
                LaunchGame();
            }
        }

        void Client_DownloadProgressChanged_RELOADED(object sender, DownloadProgressChangedEventArgs e)
        {
            this.BeginInvoke((MethodInvoker)delegate
            {
                double bytesIn = double.Parse(e.BytesReceived.ToString());
                double totalBytes = double.Parse(e.TotalBytesToReceive.ToString());
                double percentage = bytesIn / totalBytes * 100;
                PlayProgressText.Text = ("[" + CurrentModFileCount + " / " + TotalModFileCount + "] Downloading " + ModNetFileNameInUse + ": " + TimeConversions.FormatFileSize(e.BytesReceived) + " of " + TimeConversions.FormatFileSize(e.TotalBytesToReceive)).ToUpper();

                ExtractingProgress.Value = Convert.ToInt32(Decimal.Divide(e.BytesReceived, e.TotalBytesToReceive) * 100);
                ExtractingProgress.Width = Convert.ToInt32(Decimal.Divide(e.BytesReceived, e.TotalBytesToReceive) * 519);
            });
        }

        //Launch game
        public void LaunchGame()
        {
            if (_serverInfo.DiscordAppId != null)
            {
                discordRpcClient.Dispose();
                discordRpcClient = null;
                discordRpcClient = new DiscordRpcClient(_serverInfo.DiscordAppId);
                discordRpcClient.Initialize();
            }

            if ((_disableDiscordRPC == false) && ((ServerInfo)ServerPick.SelectedItem).Category == "DEV")
            {
                discordRpcClient.Dispose();
                discordRpcClient = null;
            }

            try
            {
                if
                  (
                    SHA.HashFile(FileSettingsSave.GameInstallation + "/nfsw.exe") == "7C0D6EE08EB1EDA67D5E5087DDA3762182CDE4AC" ||
                    SHA.HashFile(FileSettingsSave.GameInstallation + "/nfsw.exe") == "DB9287FB7B0CDA237A5C3885DD47A9FFDAEE1C19" ||
                    SHA.HashFile(FileSettingsSave.GameInstallation + "/nfsw.exe") == "E69890D31919DE1649D319956560269DB88B8F22"
                  )
                {
                    ServerProxy.Instance.SetServerUrl(_serverIp);
                    ServerProxy.Instance.SetServerName(_realServername);

                    AntiCheat.user_id = _userId;
                    AntiCheat.serverip = new Uri(_serverIp).Host;

                    StartGame(_userId, _loginToken);

                    if (_builtinserver)
                    {
                        PlayProgressText.Text = "Soapbox server launched. Waiting for queries.".ToUpper();
                    }
                    else
                    {
                        var secondsToCloseLauncher = 10;

                        ExtractingProgress.Value = 100;
                        ExtractingProgress.Width = 519;

                        while (secondsToCloseLauncher > 0)
                        {
                            PlayProgressText.Text = string.Format("Loading game. Launcher will minimize in {0} seconds.", secondsToCloseLauncher).ToUpper(); //"LOADING GAME. LAUNCHER WILL MINIMIZE ITSELF IN " + secondsToCloseLauncher + " SECONDS";
                            Delay.WaitSeconds(1);
                            secondsToCloseLauncher--;
                        }

                        if (secondsToCloseLauncher == 0)
                        {
                            CurrentWindowInfo.Text = string.Format(_loginWelcomeTime + "\n{0}", IsEmailValid.Mask(FileAccountSave.UserRawEmail)).ToUpper();
                        }

                        PlayProgressText.Text = "";

                        WindowState = FormWindowState.Minimized;
                        ShowInTaskbar = false;

                        ContextMenu = new ContextMenu();
                        ContextMenu.MenuItems.Add(new MenuItem("Donate", (b, n) => { Process.Start("https://paypal.me/metonator95"); }));
                        ContextMenu.MenuItems.Add("-");
                        ContextMenu.MenuItems.Add(new MenuItem("Close Launcher", (sender2, e2) =>
                        {
                            MessageBox.Show(null, "Please close the game before closing launcher.", "Please close the game before closing launcher.", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }));

                        Update();
                        Refresh();

                        Notification.ContextMenu = ContextMenu;
                    }
                }
                else
                {
                    MessageBox.Show(null, "Your NFSW.exe is modified. Please re-download the game.", "GameLauncher", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(null, ex.Message, "GameLauncher", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void PlayButton_MouseUp(object sender, EventArgs e)
        {
            if (_playenabled == false)
            {
                return;
            }

            PlayButton.BackgroundImage = Theming.PlayButtonHover;
        }

        private void PlayButton_MouseDown(object sender, EventArgs e)
        {
            if (_playenabled == false)
            {
                return;
            }

            PlayButton.BackgroundImage = Theming.PlayButtonClick;
        }

        private void PlayButton_MouseEnter(object sender, EventArgs e)
        {
            if (_playenabled == false)
            {
                return;
            }

            PlayButton.BackgroundImage = Theming.PlayButtonHover;
        }

        private void PlayButton_MouseLeave(object sender, EventArgs e)
        {
            if (_playenabled == false)
            {
                return;
            }

            PlayButton.BackgroundImage = Theming.PlayButton;
        }

        private void LaunchNfsw()
        {
            PlayButton.BackgroundImage = Theming.PlayButton;
            PlayButton.ForeColor = Theming.ThirdTextForeColor;

            PlayProgressText.Text = "Checking up all files".ToUpper();
            PlayProgress.Width = 0;
            ExtractingProgress.Width = 0;

            string speechFile;

            try
            {
                speechFile = string.IsNullOrEmpty(FileSettingsSave.Lang) ? "en" : FileSettingsSave.Lang.ToLower();
            }
            catch (Exception)
            {
                speechFile = "en";
            }

            if (!File.Exists(FileSettingsSave.GameInstallation + "/Sound/Speech/copspeechhdr_" + speechFile + ".big"))
            {
                PlayProgressText.Text = "Loading list of files to download...".ToUpper();

                DriveInfo[] allDrives = DriveInfo.GetDrives();
                foreach (DriveInfo d in allDrives)
                {
                    if (d.Name == Path.GetPathRoot(FileSettingsSave.GameInstallation))
                    {
                        if (d.TotalFreeSpace < 8589934592)
                        {
                            ExtractingProgress.Value = 100;
                            ExtractingProgress.Width = 519;
                            ExtractingProgress.Image = Theming.ProgressBarWarning;
                            ExtractingProgress.ProgressColor = Theming.ExtractingProgressColor;

                            PlayProgressText.Text = "Please make sure you have at least 8GB free space on hard drive.".ToUpper();

                            TaskbarProgress.SetState(Handle, TaskbarProgress.TaskbarStates.Paused);
                            TaskbarProgress.SetValue(Handle, 100, 100);
                        }
                        else
                        {
                            DownloadCoreFiles();
                        }
                    }
                }
            }
            else
            {
                OnDownloadFinished();
            }
        }

        public void RemoveTracksHighFiles()
        {
            if (File.Exists(FileSettingsSave.GameInstallation + "/TracksHigh/STREAML5RA_98.BUN"))
            {
                Directory.Delete(FileSettingsSave.GameInstallation + "/TracksHigh", true);
            }
        }

        public void DownloadCoreFiles()
        {
            PlayProgressText.Text = "Checking Core Files...".ToUpper();
            PlayProgress.Width = 0;
            ExtractingProgress.Width = 0;

            TaskbarProgress.SetState(Handle, TaskbarProgress.TaskbarStates.Indeterminate);

            //Guess who is Back - DavidCarbon
            if (File.Exists(filename_pack))
            {
                PlayProgressTextTimer.Visible = true;
                PlayProgressText.Text = "Local GameFiles sbrwpack Found In Launcher Folder".ToUpper();
                PlayProgressTextTimer.Text = "Loading".ToUpper();

                //GameFiles.sbrwpack
                LocalGameFiles();
            }
            else if (!File.Exists(FileSettingsSave.GameInstallation + "/nfsw.exe"))
            {
                _downloadStartTime = DateTime.Now;
                Log.Info("DOWNLOAD: Getting Core Game Files");
                _downloader.StartDownload(_NFSW_Installation_Source, "", FileSettingsSave.GameInstallation, false, false, 1130632198);
            }
            else
            {
                DownloadTracksFiles();
            }
        }

        public void DownloadTracksFiles()
        {
            PlayProgressText.Text = "Checking Tracks Files...".ToUpper();
            PlayProgress.Width = 0;
            ExtractingProgress.Width = 0;

            TaskbarProgress.SetState(Handle, TaskbarProgress.TaskbarStates.Indeterminate);

            if (!File.Exists(FileSettingsSave.GameInstallation + "/Tracks/STREAML5RA_98.BUN"))
            {
                _downloadStartTime = DateTime.Now;
                Log.Info("DOWNLOAD: Getting Tracks Folder");
                _downloader.StartDownload(_NFSW_Installation_Source, "Tracks", FileSettingsSave.GameInstallation, false, false, 615494528);
            }
            else
            {
                DownloadSpeechFiles();
            }
        }

        public void DownloadSpeechFiles()
        {
            PlayProgressText.Text = "Looking for correct Speech Files...".ToUpper();
            PlayProgress.Width = 0;
            ExtractingProgress.Width = 0;

            TaskbarProgress.SetState(Handle, TaskbarProgress.TaskbarStates.Indeterminate);

            string speechFile;
            int speechSize;

            try
            {
                if (string.IsNullOrEmpty(FileSettingsSave.Lang))
                {
                    speechFile = "en";
                    speechSize = 141805935;
                    _langInfo = "ENGLISH";
                }
                else
                {
                    WebClient wc = new WebClient();
                    var response = wc.DownloadString(_NFSW_Installation_Source + "/" + FileSettingsSave.Lang.ToLower() + "/index.xml");

                    response = response.Substring(3, response.Length - 3);

                    var speechFileXml = new XmlDocument();
                    speechFileXml.LoadXml(response);
                    var speechSizeNode = speechFileXml.SelectSingleNode("index/header/compressed");

                    speechFile = FileSettingsSave.Lang.ToLower();
                    speechSize = Convert.ToInt32(speechSizeNode.InnerText);
                    /* Fix this issue - DavidCarbon */
                    //_langInfo = SettingsLanguage.GetItemText(SettingsLanguage.SelectedItem).ToUpper();
                }
            }
            catch (Exception)
            {
                speechFile = "en";
                speechSize = 141805935;
                _langInfo = "ENGLISH";
            }

            PlayProgressText.Text = string.Format("Checking for {0} Speech Files.", _langInfo).ToUpper();

            if (!File.Exists(FileSettingsSave.GameInstallation + "\\Sound\\Speech\\copspeechsth_" + speechFile + ".big"))
            {
                _downloadStartTime = DateTime.Now;
                Log.Info("DOWNLOAD: Getting Speech/Audio Files");
                _downloader.StartDownload(_NFSW_Installation_Source, speechFile, FileSettingsSave.GameInstallation, false, false, speechSize);
            }
            else
            {
                OnDownloadFinished();
                Log.Info("DOWNLOAD: Game Files Download is Complete!");
            }
        }

        //Check Local GameFiles Hash
        private async void LocalGameFiles()
        {
            await Task.Delay(5000);
            if (SHA.HashFile("GameFiles.sbrwpack") == "88C886B6D131C052365C3D6D14E14F67A4E2C253")
            {
                TaskbarProgress.SetValue(Handle, 100, 100);
                PlayProgress.Value = 100;
                PlayProgress.Width = 519;

                GoForUnpack(filename_pack);
            }
        }

        //That's right the Protype Extractor from 2.1.5.x, now back from the dead - DavidCarbon
        public void GoForUnpack(string filename_pack)
        {
            //Thread.Sleep(1);

            Thread unpacker = new Thread(() => {
                this.BeginInvoke((MethodInvoker)delegate {
                    using (ZipArchive archive = ZipFile.OpenRead(filename_pack))
                    {
                        int numFiles = archive.Entries.Count;
                        int current = 1;

                        foreach (ZipArchiveEntry entry in archive.Entries)
                        {
                            string fullName = entry.FullName;

                            ExtractingProgress.Value = (int)((long)100 * current / numFiles);
                            ExtractingProgress.Width = (int)((long)519 * current / numFiles);

                            TaskbarProgress.SetValue(Handle, (int)(100 * current / numFiles), 100);

                            if (!File.Exists(Path.Combine(FileSettingsSave.GameInstallation, fullName.Replace(".sbrw", String.Empty))))
                            {
                                PlayProgressText.Text = ("Unpacking " + fullName.Replace(".sbrw", String.Empty)).ToUpper();
                                PlayProgressTextTimer.Text = "[" + current + " / " + archive.Entries.Count + "]";


                                if (fullName.Substring(fullName.Length - 1) == "/")
                                {
                                    //Is a directory, create it!
                                    string folderName = fullName.Remove(fullName.Length - 1);
                                    if (Directory.Exists(Path.Combine(FileSettingsSave.GameInstallation, folderName)))
                                    {
                                        Directory.Delete(Path.Combine(FileSettingsSave.GameInstallation, folderName), true);
                                    }

                                    Directory.CreateDirectory(Path.Combine(FileSettingsSave.GameInstallation, folderName));
                                }
                                else
                                {
                                    String oldFileName = fullName.Replace(".sbrw", String.Empty);
                                    String[] split = oldFileName.Split('/');

                                    String newFileName = String.Empty;

                                    if (split.Length >= 2)
                                    {
                                        newFileName = Path.Combine(split[split.Length - 2], split[split.Length - 1]);
                                    }
                                    else
                                    {
                                        newFileName = split.Last();
                                    }

                                    String KEY = Regex.Replace(SHA.HashPassword(newFileName), "[^0-9.]", "").Substring(0, 8);
                                    String IV = Regex.Replace(MDFive.HashPassword(newFileName), "[^0-9.]", "").Substring(0, 8);

                                    entry.ExtractToFile(getTempNa, true);

                                    DESCryptoServiceProvider dESCryptoServiceProvider = new DESCryptoServiceProvider()
                                    {
                                        Key = Encoding.ASCII.GetBytes(KEY),
                                        IV = Encoding.ASCII.GetBytes(IV)
                                    };

                                    FileStream fileStream = new FileStream(Path.Combine(FileSettingsSave.GameInstallation, oldFileName), FileMode.Create);
                                    CryptoStream cryptoStream = new CryptoStream(fileStream, dESCryptoServiceProvider.CreateDecryptor(), CryptoStreamMode.Write);
                                    BinaryWriter binaryFile = new BinaryWriter(cryptoStream);

                                    using (BinaryReader reader = new BinaryReader(File.Open(getTempNa, FileMode.Open)))
                                    {
                                        long numBytes = new FileInfo(getTempNa).Length;
                                        binaryFile.Write(reader.ReadBytes((int)numBytes));
                                        binaryFile.Close();
                                    }
                                }
                            }
                            else
                            {
                                PlayProgressText.Text = ("Skipping " + fullName).ToUpper();
                            }

                            _presence.State = "Unpacking game: " + (100 * current / numFiles) + "%";
                            if (discordRpcClient != null) discordRpcClient.SetPresence(_presence);

                            Application.DoEvents();

                            if (numFiles == current)
                            {
                                PlayProgressTextTimer.Visible = false;
                                PlayProgressTextTimer.Text = "";

                                _isDownloading = false;
                                OnDownloadFinished();

                                Notification.Visible = true;
                                Notification.BalloonTipIcon = ToolTipIcon.Info;
                                Notification.BalloonTipTitle = "SBRW Launcher";
                                Notification.BalloonTipText = "Your game is now ready to launch!";
                                Notification.ShowBalloonTip(5000);
                                Notification.Dispose();
                            }

                            current++;
                        }
                    }
                });
            });

            unpacker.Start();
        }

        private void OnDownloadProgress(long downloadLength, long downloadCurrent, long compressedLength, string filename, int skiptime = 0)
        {
            if (downloadCurrent < compressedLength)
            {
                PlayProgressText.Text = String.Format("Downloading — {0} of {1} ({3}%) — {2}", TimeConversions.FormatFileSize(downloadCurrent), TimeConversions.FormatFileSize(compressedLength), TimeConversions.EstimateFinishTime(downloadCurrent, compressedLength, _downloadStartTime), (int)(100 * downloadCurrent / compressedLength)).ToUpper();
            }

            try
            {
                PlayProgress.Value = (int)(100 * downloadCurrent / compressedLength);
                PlayProgress.Width = (int)(519 * downloadCurrent / compressedLength);

                _presence.State = string.Format("Downloaded {0}% of the Game!", (int)(100 * downloadCurrent / compressedLength));
                if (discordRpcClient != null) discordRpcClient.SetPresence(_presence);

                TaskbarProgress.SetValue(Handle, (int)(100 * downloadCurrent / compressedLength), 100);
            }
            catch
            {
                TaskbarProgress.SetValue(Handle, 0, 100);
                PlayProgress.Value = 0;
                PlayProgress.Width = 0;
            }

            TaskbarProgress.SetState(Handle, TaskbarProgress.TaskbarStates.Normal);
        }

        private void OnDownloadFinished()
        {
            try
            {
                File.WriteAllBytes(FileSettingsSave.GameInstallation + "/GFX/BootFlow.gfx", ExtractResource.AsByte("GameLauncher.SoapBoxModules.BootFlow.gfx"));
            }
            catch
            {
                // ignored
            }

            if (!string.IsNullOrEmpty(FileSettingsSave.GameInstallation))
            {
                //Remove current Firewall for the Game Files 
                string CurrentGameFilesExePath = Path.Combine(FileSettingsSave.GameInstallation + "\\nfsw.exe");

                if (File.Exists(CurrentGameFilesExePath) && FirewallHelper.RuleExist("SBRW - Game") == false)
                {
                    bool removeFirewallRule = false;
                    bool firstTimeRun = true;

                    string nameOfGame = "SBRW - Game";
                    string localOfGame = CurrentGameFilesExePath;

                    string groupKeyGame = "Need for Speed: World";
                    string descriptionGame = groupKeyGame;

                    //Inbound & Outbound
                    FirewallHelper.DoesRulesExist(removeFirewallRule, firstTimeRun, nameOfGame, localOfGame, groupKeyGame, descriptionGame, FirewallProtocol.Any);
                }
                else
                {
                    Log.Core("WINDOWS FIREWALL: Already Exlcuded SBRW - Game {Both}");
                }
            }

            PlayProgressText.Text = "Ready!".ToUpper();
            _presence.State = "Ready!";
            if (discordRpcClient != null) discordRpcClient.SetPresence(_presence);

            EnablePlayButton();

            ExtractingProgress.Width = 519;

            TaskbarProgress.SetValue(Handle, 100, 100);
            TaskbarProgress.SetState(Handle, TaskbarProgress.TaskbarStates.Normal);
        }

        private void EnablePlayButton()
        {
            _isDownloading = false;
            _playenabled = true;

            ExtractingProgress.Value = 100;
            ExtractingProgress.Width = 519;
        }

        private void DisablePlayButton()
        {
            _isDownloading = false;
            _playenabled = false;

            ExtractingProgress.Value = 100;
            ExtractingProgress.Width = 519;
        }

        private void OnDownloadFailed(Exception ex)
        {
            string failureMessage;
            MessageBox.Show(null, "Failed to download gamefiles. \n\nCDN might be offline. \n\nPlease select a different CDN on Next Screen", "GameLauncher - Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

            //CDN Went Offline Screen switch - DavidCarbon
            SettingsButton_Click(null, null);

            try
            {
                failureMessage = ex.Message;
            }
            catch
            {
                failureMessage = "Download failed.";
            }

            _presence.State = "Failed to Downloaded Game!";
            if (discordRpcClient != null) discordRpcClient.SetPresence(_presence);

            ExtractingProgress.Value = 100;
            ExtractingProgress.Width = 519;
            ExtractingProgress.Image = Theming.ProgressBarError;
            ExtractingProgress.ProgressColor = Theming.Error;

            PlayProgressText.Text = failureMessage.ToUpper();

            TaskbarProgress.SetValue(Handle, 100, 100);
            TaskbarProgress.SetState(Handle, TaskbarProgress.TaskbarStates.Error);
        }

        private void OnShowExtract(string filename, long currentCount, long allFilesCount)
        {
            if (PlayProgress.Value == 100)
            {
                PlayProgressText.Text = String.Format("Extracting — {0} of {1} ({3}%) — {2}", TimeConversions.FormatFileSize(currentCount), TimeConversions.FormatFileSize(allFilesCount), TimeConversions.EstimateFinishTime(currentCount, allFilesCount, _downloadStartTime), (int)(100 * currentCount / allFilesCount)).ToUpper();
            }

            ExtractingProgress.Value = (int)(100 * currentCount / allFilesCount);
            ExtractingProgress.Width = (int)(519 * currentCount / allFilesCount);
        }

        private void OnShowMessage(string message, string header)
        {
            MessageBox.Show(message, header);
        }

        public void ServerStatusBar(Pen color, Point startPoint, Point endPoint, int Thickness = 2)
        {
            Graphics _formGraphics = CreateGraphics();

            for (int x = 0; x <= Thickness; x++)
            {
                _formGraphics.DrawLine(color, new Point(startPoint.X, startPoint.Y - x), new Point(endPoint.X, endPoint.Y - x));
            }

            _formGraphics.Dispose();
        }

        private void SelectServerBtn_Click(object sender, EventArgs e)
        {
            new SelectServer().ShowDialog();

            if (ServerName != null)
            {
                this.SelectServerBtn.Text = "[...] " + ServerName.Name;

                var index = ServerListUpdater.CleanList.FindIndex(i => string.Equals(i.IpAddress, ServerName.IpAddress));
                ServerPick.SelectedIndex = index;
            }
        }

        private void DiscordInviteLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (_serverDiscordLink != null)
                Process.Start(_serverDiscordLink);
        }

        private void HomePageLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (_serverWebsiteLink != null)
                Process.Start(_serverWebsiteLink);
        }

        private void FacebookGroupLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (_serverFacebookLink != null)
                Process.Start(_serverFacebookLink);
        }

        private void TwitterAccountLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (_serverTwitterLink != null)
                Process.Start(_serverTwitterLink);
        }
        private void WindowsDefenderFirstRun()
        {
            // Create Windows Defender Exclusion
            try
            {
                Log.Info("WINDOWS DEFENDER: Excluding Core Folders");
                //Add Exclusion to Windows Defender
                using (PowerShell ps = PowerShell.Create())
                {
                    ps.AddScript($"Add-MpPreference -ExclusionPath \"{AppDomain.CurrentDomain.BaseDirectory}\"");
                    ps.AddScript($"Add-MpPreference -ExclusionPath \"{FileSettingsSave.GameInstallation}\"");
                    var result = ps.Invoke();
                }
                FileSettingsSave.FirewallStatus = "Excluded";
                FileSettingsSave.SaveSettings();
            }
            catch
            {
                FileSettingsSave.FirewallStatus = "Not Excluded";
                FileSettingsSave.SaveSettings();
            }
        }

        private void CheckGameFilesDirectoryPrevention()
        {
            switch (Self.CheckFolder(FileSettingsSave.GameInstallation))
            {
                case FolderType.IsSameAsLauncherFolder:
                    Directory.CreateDirectory("Game Files");
                    Log.Error("LAUNCHER: Installing NFSW in same directory where the launcher resides is disadvised.");
                    MessageBox.Show(null, string.Format("Installing NFSW in same directory where the launcher resides is disadvised. Instead, we will install it at {0}.", AppDomain.CurrentDomain.BaseDirectory + "Game Files"), "GameLauncher", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    FileSettingsSave.GameInstallation = AppDomain.CurrentDomain.BaseDirectory + "\\Game Files";
                    break;
                case FolderType.IsTempFolder:
                    Directory.CreateDirectory("Game Files");
                    Log.Error("LAUNCHER: (╯°□°）╯︵ ┻━┻ Installing NFSW in the Temp Folder is disadvised!");
                    MessageBox.Show(null, string.Format("(╯°□°）╯︵ ┻━┻\n\nInstalling NFSW in the Temp Folder is disadvised! Instead, we will install it at {0}.", AppDomain.CurrentDomain.BaseDirectory + "\\Game Files" + "\n\n┬─┬ ノ( ゜-゜ノ)"), "GameLauncher", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    FileSettingsSave.GameInstallation = AppDomain.CurrentDomain.BaseDirectory + "\\Game Files";
                    break;
                case FolderType.IsProgramFilesFolder:
                case FolderType.IsUsersFolders:
                case FolderType.IsWindowsFolder:
                    Directory.CreateDirectory("Game Files");
                    Log.Error("LAUNCHER: Installing NFSW in a Special Directory is disadvised.");
                    MessageBox.Show(null, string.Format("Installing NFSW in a Special Directory is disadvised. Instead, we will install it at {0}.", AppDomain.CurrentDomain.BaseDirectory + "\\Game Files"), "GameLauncher", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    FileSettingsSave.GameInstallation = AppDomain.CurrentDomain.BaseDirectory + "\\Game Files";
                    break;
            }
            FileSettingsSave.SaveSettings();
        }

        private void SetVisuals()
        {
            /*******************************/
            /* Set Font                     /
            /*******************************/

            FontFamily DejaVuSans = FontWrapper.Instance.GetFontFamily("DejaVuSans.ttf");
            FontFamily DejaVuSansBold = FontWrapper.Instance.GetFontFamily("DejaVuSans-Bold.ttf");
            /* Front Screen */
            InsiderBuildNumberText.Font = new Font(DejaVuSans, 9f * 100f / CreateGraphics().DpiY, FontStyle.Regular);
            SelectServerBtn.Font = new Font(DejaVuSans, 9f * 100f / CreateGraphics().DpiY, FontStyle.Regular);
            translatedBy.Font = new Font(DejaVuSans, 8f * 100f / CreateGraphics().DpiY, FontStyle.Regular);
            ServerPick.Font = new Font(DejaVuSansBold, 9f * 100f / CreateGraphics().DpiY, FontStyle.Bold);
            AddServer.Font = new Font(DejaVuSansBold, 8f * 100f / CreateGraphics().DpiY, FontStyle.Bold);
            ShowPlayPanel.Font = new Font(DejaVuSans, 8f * 100f / CreateGraphics().DpiY, FontStyle.Regular);
            CurrentWindowInfo.Font = new Font(DejaVuSansBold, 9f * 100f / CreateGraphics().DpiY, FontStyle.Bold);
            LauncherStatusText.Font = new Font(DejaVuSansBold, 9f * 100f / CreateGraphics().DpiY, FontStyle.Bold);
            LauncherStatusDesc.Font = new Font(DejaVuSans, 9f * 100f / CreateGraphics().DpiY, FontStyle.Regular);
            ServerStatusText.Font = new Font(DejaVuSansBold, 9f * 100f / CreateGraphics().DpiY, FontStyle.Bold);
            ServerStatusDesc.Font = new Font(DejaVuSans, 9f * 100f / CreateGraphics().DpiY, FontStyle.Regular);
            APIStatusText.Font = new Font(DejaVuSansBold, 9f * 100f / CreateGraphics().DpiY, FontStyle.Bold);
            APIStatusDesc.Font = new Font(DejaVuSans, 9f * 100f / CreateGraphics().DpiY, FontStyle.Regular);
            ExtractingProgress.Font = new Font(DejaVuSansBold, 9f * 100f / CreateGraphics().DpiY, FontStyle.Bold);
            /* Social Panel */
            //ServerInfoPanel.Font = new Font(DejaVuSans, 8f, FontStyle.Regular);
            HomePageLink.Font = new Font(DejaVuSans, 9f * 100f / CreateGraphics().DpiY, FontStyle.Regular);
            DiscordInviteLink.Font = new Font(DejaVuSans, 9f * 100f / CreateGraphics().DpiY, FontStyle.Regular);
            FacebookGroupLink.Font = new Font(DejaVuSans, 9f * 100f / CreateGraphics().DpiY, FontStyle.Regular);
            TwitterAccountLink.Font = new Font(DejaVuSans, 9f * 100f / CreateGraphics().DpiY, FontStyle.Regular);
            SceneryGroupText.Font = new Font(DejaVuSansBold, 9f * 100f / CreateGraphics().DpiY, FontStyle.Bold);
            ServerShutDown.Font = new Font(DejaVuSansBold, 9f * 100f / CreateGraphics().DpiY, FontStyle.Bold);
            /* Log In Panel */
            MainEmail.Font = new Font(DejaVuSans, 9f * 100f / CreateGraphics().DpiY, FontStyle.Regular);
            MainPassword.Font = new Font(DejaVuSans, 9f * 100f / CreateGraphics().DpiY, FontStyle.Regular);
            RememberMe.Font = new Font(DejaVuSansBold, 9f * 100f / CreateGraphics().DpiY, FontStyle.Bold);
            ForgotPassword.Font = new Font(DejaVuSans, 9f * 100f / CreateGraphics().DpiY, FontStyle.Regular);
            LoginButton.Font = new Font(DejaVuSansBold, 10f * 100f / CreateGraphics().DpiY, FontStyle.Bold);
            RegisterText.Font = new Font(DejaVuSansBold, 10f * 100f / CreateGraphics().DpiY, FontStyle.Bold);
            ServerPingStatusText.Font = new Font(DejaVuSansBold, 9f * 100f / CreateGraphics().DpiY, FontStyle.Bold);
            LogoutButton.Font = new Font(DejaVuSansBold, 10f * 100f / CreateGraphics().DpiY, FontStyle.Bold);
            PlayButton.Font = new Font(DejaVuSansBold, 14f * 100f / CreateGraphics().DpiY, FontStyle.Bold);
            PlayProgress.Font = new Font(DejaVuSans, 9f * 100f / CreateGraphics().DpiY, FontStyle.Regular);
            PlayProgressText.Font = new Font(DejaVuSansBold, 9f * 100f / CreateGraphics().DpiY, FontStyle.Bold);
            PlayProgressTextTimer.Font = new Font(DejaVuSans, 9f * 100f / CreateGraphics().DpiY, FontStyle.Regular);
            /* Registering Panel */
            RegisterPanel.Font = new Font(DejaVuSansBold, 9f * 100f / CreateGraphics().DpiY, FontStyle.Bold);
            RegisterEmail.Font = new Font(DejaVuSans, 9f * 100f / CreateGraphics().DpiY, FontStyle.Regular);
            RegisterPassword.Font = new Font(DejaVuSans, 9f * 100f / CreateGraphics().DpiY, FontStyle.Regular);
            RegisterConfirmPassword.Font = new Font(DejaVuSans, 9f * 100f / CreateGraphics().DpiY, FontStyle.Regular);
            RegisterTicket.Font = new Font(DejaVuSans, 9f * 100f / CreateGraphics().DpiY, FontStyle.Regular);
            RegisterAgree.Font = new Font(DejaVuSansBold, 9f * 100f / CreateGraphics().DpiY, FontStyle.Bold);
            RegisterButton.Font = new Font(DejaVuSansBold, 9f * 100f / CreateGraphics().DpiY, FontStyle.Bold);
            RegisterCancel.Font = new Font(DejaVuSansBold, 9f * 100f / CreateGraphics().DpiY, FontStyle.Bold);

            /********************************/
            /* Set Theme Colors & Images     /
            /********************************/

            /* Set Background with Transparent Key */
            BackgroundImage = Theming.MainScreen;
            TransparencyKey = Theming.MainScreenTransparencyKey;

            logo.BackgroundImage = Theming.LogoMain;
            SettingsButton.BackgroundImage = Theming.GearButton;
            CloseBTN.BackgroundImage = Theming.CloseButton;

            ProgressBarOutline.BackgroundImage = Theming.ProgressBarOutline;
            PlayProgress.Image = Theming.ProgressBarPreload;
            ExtractingProgress.Image = Theming.ProgressBarSuccess;

            PlayProgressText.ForeColor = Theming.FivithTextForeColor;
            PlayProgressTextTimer.ForeColor = Theming.FivithTextForeColor;

            MainEmailBorder.Image = Theming.BorderEmail;
            MainPasswordBorder.Image = Theming.BorderPassword;

            CurrentWindowInfo.ForeColor = Theming.FivithTextForeColor;

            LauncherStatusDesc.ForeColor = Theming.FivithTextForeColor;
            ServerStatusDesc.ForeColor = Theming.FivithTextForeColor;
            APIStatusDesc.ForeColor = Theming.FivithTextForeColor;

            LoginButton.ForeColor = Theming.FivithTextForeColor;
            LoginButton.BackgroundImage = Theming.GrayButton;

            RegisterText.ForeColor = Theming.SeventhTextForeColor;
            RegisterText.BackgroundImage = Theming.GreenButton;

            RememberMe.ForeColor = Theming.FivithTextForeColor;

            ForgotPassword.ActiveLinkColor = Theming.ActiveLink;
            ForgotPassword.LinkColor = Theming.Link;

            MainEmail.BackColor = Theming.Input;
            MainEmail.ForeColor = Theming.FivithTextForeColor;
            MainPassword.BackColor = Theming.Input;
            MainPassword.ForeColor = Theming.FivithTextForeColor;

            ServerShutDown.ForeColor = Theming.SecondaryTextForeColor;
            SceneryGroupText.ForeColor = Theming.SecondaryTextForeColor;

            TwitterAccountLink.LinkColor = Theming.SecondaryTextForeColor;
            FacebookGroupLink.LinkColor = Theming.SecondaryTextForeColor;
            DiscordInviteLink.LinkColor = Theming.SecondaryTextForeColor;
            HomePageLink.LinkColor = Theming.SecondaryTextForeColor;

            TwitterAccountLink.ActiveLinkColor = Theming.FivithTextForeColor;
            FacebookGroupLink.ActiveLinkColor = Theming.FivithTextForeColor;
            DiscordInviteLink.ActiveLinkColor = Theming.FivithTextForeColor;
            HomePageLink.ActiveLinkColor = Theming.FivithTextForeColor;

            InsiderBuildNumberText.ForeColor = Theming.FivithTextForeColor;

            /********************************/
            /* Events                        /
            /********************************/

            CloseBTN.MouseEnter += new EventHandler(CloseBTN_MouseEnter);
            CloseBTN.MouseLeave += new EventHandler(CloseBTN_MouseLeave);
            CloseBTN.Click += new EventHandler(CloseBTN_Click);

            SettingsButton.MouseEnter += new EventHandler(SettingsButton_MouseEnter);
            SettingsButton.MouseLeave += new EventHandler(SettingsButton_MouseLeave);
            SettingsButton.Click += new EventHandler(SettingsButton_Click);

            LoginButton.MouseEnter += new EventHandler(LoginButton_MouseEnter);
            LoginButton.MouseLeave += new EventHandler(LoginButton_MouseLeave);
            LoginButton.MouseUp += new MouseEventHandler(LoginButton_MouseUp);
            LoginButton.MouseDown += new MouseEventHandler(LoginButton_MouseDown);
            LoginButton.Click += new EventHandler(LoginButton_Click);

            RegisterButton.MouseEnter += Greenbutton_hover_MouseEnter;
            RegisterButton.MouseLeave += Greenbutton_MouseLeave;
            RegisterButton.MouseUp += Greenbutton_hover_MouseUp;
            RegisterButton.MouseDown += Greenbutton_click_MouseDown;
            RegisterButton.Click += RegisterButton_Click;

            RegisterCancel.MouseEnter += new EventHandler(Graybutton_hover_MouseEnter);
            RegisterCancel.MouseLeave += new EventHandler(Graybutton_MouseLeave);
            RegisterCancel.MouseUp += new MouseEventHandler(Graybutton_hover_MouseUp);
            RegisterCancel.MouseDown += new MouseEventHandler(Graybutton_click_MouseDown);
            RegisterCancel.Click += new EventHandler(RegisterCancel_Click);

            LogoutButton.MouseEnter += new EventHandler(Graybutton_hover_MouseEnter);
            LogoutButton.MouseLeave += new EventHandler(Graybutton_MouseLeave);
            LogoutButton.MouseUp += new MouseEventHandler(Graybutton_hover_MouseUp);
            LogoutButton.MouseDown += new MouseEventHandler(Graybutton_click_MouseDown);
            LogoutButton.Click += new EventHandler(LogoutButton_Click);

            AddServer.Click += new EventHandler(AddServer_Click);

            MainEmail.KeyUp += new KeyEventHandler(Loginbuttonenabler);
            MainEmail.KeyDown += new KeyEventHandler(LoginEnter);
            MainPassword.KeyUp += new KeyEventHandler(Loginbuttonenabler);
            MainPassword.KeyDown += new KeyEventHandler(LoginEnter);

            ServerPick.SelectedIndexChanged += new EventHandler(ServerPick_SelectedIndexChanged);
            ServerPick.DrawItem += new DrawItemEventHandler(ComboBox1_DrawItem);

            ForgotPassword.LinkClicked += new LinkLabelLinkClickedEventHandler(ForgotPassword_LinkClicked);

            MouseMove += new MouseEventHandler(MoveWindow_MouseMove);
            MouseUp += new MouseEventHandler(MoveWindow_MouseUp);
            MouseDown += new MouseEventHandler(MoveWindow_MouseDown);

            logo.MouseMove += new MouseEventHandler(MoveWindow_MouseMove);
            logo.MouseUp += new MouseEventHandler(MoveWindow_MouseUp);
            logo.MouseDown += new MouseEventHandler(MoveWindow_MouseDown);

            PlayButton.MouseEnter += new EventHandler(PlayButton_MouseEnter);
            PlayButton.MouseLeave += new EventHandler(PlayButton_MouseLeave);
            PlayButton.MouseUp += new MouseEventHandler(PlayButton_MouseUp);
            PlayButton.MouseDown += new MouseEventHandler(PlayButton_MouseDown);
            PlayButton.Click += new EventHandler(PlayButton_Click);

            RegisterText.MouseEnter += new EventHandler(Greenbutton_hover_MouseEnter);
            RegisterText.MouseLeave += new EventHandler(Greenbutton_MouseLeave);
            RegisterText.MouseUp += new MouseEventHandler(Greenbutton_hover_MouseUp);
            RegisterText.MouseDown += new MouseEventHandler(Greenbutton_click_MouseDown);
            RegisterText.Click += new EventHandler(RegisterText_LinkClicked);
        }
    }
    /* Moved 7 Unused Code to Gist */
    /* https://gist.githubusercontent.com/DavidCarbon/97494268b0175a81a8F89a5e5aebce38/raw/00de505302fbf9f8cfea9b163a707d9f8f122552/MainScreen.cs */
}
