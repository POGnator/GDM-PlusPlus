﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using WpfAnimatedGif;

namespace Multiplayer.GDM
{
    public class Initialize
    {
        public Server Connection;
        public MainWindow Main;

        private Identity.AssemblyMetadata metadata = new Identity.AssemblyMetadata();

        public Utilities.JSON_Models.Level_Data CurrentLevelData;

        public Initialize(MainWindow _main)
        {
            this.Main = _main;
            // check from old versions
            /*LoadUserPrefs();
            InitializeClient();
            Note from POGnator (the modder):
            I commented out this feature because it wants to connect to adaf's server and download
            the latest, official version of GDM what I don't want for obvious reasons*/

            // load language
            GDM.Load_Language.Load();
            WebRequest.DefaultWebProxy = null;
            Globals.Global_Data.Initializer = this;

            foreach (var foo in Enum.GetValues(typeof(Utilities.FormLongs)))
            {
                GDM.Client.Client.IconsAndIDs.Add((int)foo, foo.ToString());
            }
        }
        public void LoadCaches()
        {
            if (!File.Exists(Globals.Paths.LevelsCache)) File.Create(Globals.Paths.LevelsCache).Close();
            Utilities.JSON_Models.Level_Cache.LevelIDandData = JsonConvert.DeserializeObject<Dictionary<int, string>>(
                File.ReadAllText(Globals.Paths.LevelsCache)
                   );

            if (Utilities.JSON_Models.Level_Cache.LevelIDandData == null) Utilities.JSON_Models.Level_Cache.LevelIDandData = new Dictionary<int, string>();


            if (!File.Exists(Globals.Paths.UsernamesCache)) File.Create(Globals.Paths.UsernamesCache).Close();
            Utilities.JSON_Models.Username_Cache.PlayerIDAndUsername = JsonConvert.DeserializeObject<Dictionary<int, string>>(
                File.ReadAllText(Globals.Paths.UsernamesCache)
                   );
            if (Utilities.JSON_Models.Username_Cache.PlayerIDAndUsername == null) Utilities.JSON_Models.Username_Cache.PlayerIDAndUsername = new Dictionary<int, string>();
        }
        public void SaveCaches()
        {
            string output = JsonConvert.SerializeObject(Utilities.JSON_Models.Level_Cache.LevelIDandData);
            System.IO.File.WriteAllText(Globals.Paths.LevelsCache, output);
            output = JsonConvert.SerializeObject(Utilities.JSON_Models.Username_Cache.PlayerIDAndUsername);
            System.IO.File.WriteAllText(Globals.Paths.UsernamesCache, output);
        }

        public void ClearCaches()
        {
            try
            {
                Globals.Global_Data.ReceiveNewClients = false;
                if (Globals.Global_Data.Connection != null)
                {
                    foreach (var g in Globals.Global_Data.Connection.model.players)
                    {
                        try
                        {
                            g.Disconnected();
                        }
                        catch (Exception ex)
                        {
                            Utilities.Utils.HandleException(ex);
                        }
                    }
                }

                if (Utilities.JSON_Models.Level_Cache.LevelIDandData != null)
                    Utilities.JSON_Models.Level_Cache.LevelIDandData.Clear();
                if (Utilities.JSON_Models.Username_Cache.PlayerIDAndUsername != null)
                    Utilities.JSON_Models.Username_Cache.PlayerIDAndUsername.Clear();

                Main.pstacks.Children.Clear();

                ImageBehavior.SetAnimatedSource(Main.image6, null);
                Main.image6.Source = null;

                try
                {
                    if (Directory.Exists(Globals.Paths.IconsFolder))
                        Utilities.Utils.DeleteDirectory(Globals.Paths.IconsFolder);
                }
                catch (Exception ex)
                {
                    Utilities.Utils.HandleException(ex);
                }
                Client.Client.IconsAlreadyBeingLoaded.Clear();
                Client.Client.IconsAlreadyLoaded.Clear();

                if (Globals.Global_Data.Connection != null) Globals.Global_Data.Connection.model.players.Clear();

                Globals.Global_Data.JSONCommunication.Clear();

                if (File.Exists(Globals.Paths.UsernamesCache)) File.WriteAllText(Globals.Paths.UsernamesCache, "");
                if (File.Exists(Globals.Paths.LevelsCache)) File.WriteAllText(Globals.Paths.LevelsCache, "");


                pfpset = false;
                Announce("Cache cleared! It make take a while to load new content now.");

                new Thread(() => { DownloadSelfIcons(); }).Start();

            }
            catch (Exception ex)
            {
                Utilities.Utils.HandleException(ex);
            }
            Globals.Global_Data.ReceiveNewClients = true;
        }
        public void InitializeClient()
        {
            InitializeDirectories();
            ShowFireWall();
            CheckVersion();
            new Thread(() =>
            {
                try
                {
                    LoadCaches();
                    LoadPlayerIDFromSaveFile();
                    if (Globals.Global_Data.PlayerIDLoaded)
                        DownloadSelfIcons();
                    // Globals.Global_Data.Initializer.SetPlayerName(Utilities.TCP.GetUsernameFromPlayerID(Globals.Global_Data.PlayerID));

                    Globals.Global_Data.Initializer.SetPlayerID(Globals.Global_Data.PlayerID);

                    // check server statuses

                }
                catch (Exception ex)
                {
                    Utilities.Utils.HandleException(ex);
                }
            }).Start();
        }
        public void LoadPlayerIDFromSaveFile()
        {
            int q = Utilities.Encryption.Save_File_Decryptor.GetPlayerID();
            if (q > 0)
            {
                Globals.Global_Data.PlayerID = q;
                Globals.Global_Data.PlayerIDLoaded = true;
                // try to add the user to server db if he doesnt exist
                var temp = Utilities.TCP.ReadURL("http://95.111.251.138/gdm/getInfo.php?id=" + q.ToString()).Result;
                GDM.Player_Watcher.Memory.InitClient();
                Debug.WriteLine("User check: " + temp);

            }
            else
            {
                Debug.WriteLine("Failed loading from savefile.");
                Globals.Global_Data.PlayerIDLoaded = false;
            }
        }
        public void ShowFireWall()
        {
            new Thread(() =>
            {
                try
                {
                    IPAddress ipAddress = Dns.GetHostEntry(Dns.GetHostName()).AddressList[0];
                    IPEndPoint ipLocalEndPoint = new IPEndPoint(ipAddress, 32591);
                    TcpListener t = new TcpListener(ipLocalEndPoint);
                    t.Start();
                    t.Stop();
                }
                catch (Exception ex)
                {
                    Utilities.Utils.HandleException(ex);
                }
            }).Start();
        }
        public void CheckVersion()
        {
            new Thread(() =>
            {
                try
                {
                    // check if show self rainbow
                    Models.UpdateData deserializedProduct = JsonConvert.DeserializeObject<Models.UpdateData>(
                        Utilities.TCP.ReadURL(Globals.Global_Data.VersionLink).Result
                        );

                    if (deserializedProduct != null)
                    {
                        Globals.Global_Data.Main.UserPref.CachedLevels = deserializedProduct.CachingEnabled;
                        Globals.Global_Data.Main.UserPref.CachedUsernames = deserializedProduct.CachingEnabled;

                        Application.Current.Dispatcher.Invoke(new Action(() =>
                        {
                            Main.arelevelscached.IsChecked = Main.UserPref.CachedLevels;
                            Main.areusernamescached.IsChecked = Main.UserPref.CachedUsernames;
                            if (deserializedProduct.Version > metadata.Version)
                            {
                                // Main.arelevelscached.IsChecked = Main.UserPref.CachedLevels;
                                // Main.areusernamescached.IsChecked = Main.UserPref.CachedUsernames;
                                Main.update.Visibility = Visibility.Visible;
                                Main.updatetext.Text = deserializedProduct.PatchNotes;
                                Main.update.IsOpen = true;
                                Main.updateButt.Click += (k, t) =>
                                {
                                    Main.update.IsOpen = false;
                                    Process.Start("Updater.exe");
                                    Environment.Exit(0);
                                };
                            }
                        }));
                        // Globals.Paths.Main.UserPref.CachedLevels = deserializedProduct.caching != ":)";

                    }
                }
                catch (Exception ex)
                {
                    Utilities.Utils.HandleException(ex);
                }
            }).Start();
        }
        public void Relogin()
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                Main.login.IsOpen = true;
            }));
        }

        public void LoadUserPrefs()
        {
            try
            {
                var user_data_folder = Path.GetDirectoryName(Globals.Paths.UserDataFile);

                Utilities.Files_and_Pathing.ValidateDirectory(user_data_folder);
                Utilities.Files_and_Pathing.ValidateFile(Globals.Paths.UserDataFile);

                string user_data = File.ReadAllText(Globals.Paths.UserDataFile);

                Main.UserPref = JsonConvert.DeserializeObject<Preferences>(user_data);
                if (Main.UserPref == null) Main.UserPref = new Preferences();
                if (Main.UserPref.Key.Length <= 0) Main.UserPref.Key = Utilities.Randomness.RandomBytes(4);
                Globals.Global_Data.VipKey = BitConverter.ToInt32(Main.UserPref.Key, 0);
                if (Properties.Settings.Default.IsVIP)
                    Main.border5.Visibility = Visibility.Collapsed;

                Main.areiconscached.IsChecked = Main.UserPref.CachedIcons;
                Main.arelevelscached.IsChecked = Main.UserPref.CachedLevels;
                Main.areusernamescached.IsChecked = Main.UserPref.CachedUsernames;

                if (!Main.UserPref.CachedLevels) if (File.Exists(Globals.Paths.LevelsCache)) File.Delete(Globals.Paths.LevelsCache);
                    else if (!File.Exists(Globals.Paths.LevelsCache)) File.Create(Globals.Paths.LevelsCache).Close();
                if (!Main.UserPref.CachedUsernames) if (File.Exists(Globals.Paths.UsernamesCache)) File.Delete(Globals.Paths.UsernamesCache);
                    else if (!File.Exists(Globals.Paths.UsernamesCache)) File.Create(Globals.Paths.UsernamesCache).Close();

                if (string.IsNullOrEmpty(Main.UserPref.WindowName)) Main.UserPref.WindowName = "Geometry Dash";
                if (string.IsNullOrEmpty(Main.UserPref.MainModule)) Main.UserPref.WindowName = "GeometryDash.exe";

                Globals.Global_Data.ShowUsernames = Main.UserPref.ShowSelfUsername;
                // Main.UserPref.RenderCustomIcons = false;
                if (Main.UserPref.Version != metadata.Version)
                {
                    Directory.Delete(Globals.Paths.IconsFolder, true);
                }
                Main.UserPref.Version = metadata.Version;

            }
            catch (Exception ex)
            {
                Utilities.Utils.HandleException(ex);

                File.Delete(Globals.Paths.UserDataFile);
            }
        }
        public void ResetPrefs()
        {
            try
            {
                // Utilities.JSON_Models.LevelCache.LevelIDandData.Clear();
                // SaveCaches();
                // Main.UserPref = new UserPref();
                // Main.UserPref.PlayerID = Globals.Paths.PlayerID;
                // SaveUserPref();
                if (File.Exists(Globals.Paths.LevelsCache)) File.Delete(Globals.Paths.LevelsCache);
                if (File.Exists(Globals.Paths.UsernamesCache)) File.Delete(Globals.Paths.UsernamesCache);
                if (Directory.Exists(Globals.Paths.IconsFolder)) Directory.Delete(Globals.Paths.IconsFolder, true);
            }
            catch (Exception ex)
            {

                Utilities.Utils.HandleException(ex);
            }
            Announce("Settings reset!");
            Process.Start(Process.GetCurrentProcess().MainModule.FileName);
            Environment.Exit(0);
        }
        public void SaveUserPref()
        {
            // Announce("stats saved");
            System.Diagnostics.StackTrace t = new System.Diagnostics.StackTrace();
            // MessageBox.Show(t.ToString());
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                Main.UserPref.CachedLevels = Main.arelevelscached.IsChecked;
                Main.UserPref.CachedIcons = Main.areiconscached.IsChecked;
                Main.UserPref.CachedUsernames = Main.areusernamescached.IsChecked;
            }));

            string output = JsonConvert.SerializeObject(Main.UserPref);
            System.IO.File.WriteAllText(Globals.Paths.UserDataFile, output);

            if (!File.Exists(Globals.Paths.GDMTempDataFile)) File.Create(Globals.Paths.GDMTempDataFile).Close();
            File.WriteAllText(Globals.Paths.GDMTempDataFile, output);
        }
        int ServerIndex = 0;
        public void ServerConnected(int index)
        {
            try
            {
                ServerIndex = index;
                Connection = new Server(Globals.Global_Data.ServerIPs[index] /* 
                                                                          * Europe Connection = 0 */, this);
                Globals.Global_Data.Connection = Connection;
                Globals.Global_Data.ActiveServer = Globals.Global_Data.ServerIPs[index];
             //   Main.SG_Key.Text = "Secret Key: " + Utilities.Converter.BytesToString(Main.UserPref.Key);
                Main.StartAnimation("ServerSelected");

                Main.border5.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Globals.Global_Data.HandleException(ex);
            }
            // Main.StartAnimation("SingaporeActive");
        }

        public void SetPlayerCount(int players)
        {
            Task.Run(() =>
            {
                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                  //  Main.SG_Key.Text = "Online : ";
                    // Main.sg_badge.Badge = players.ToString();
                }));
            });
        }
        public void ClearLevels()
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                Main.levels.Children.Clear();
            }));
        }
        public async void LoadLevelID(int levelid)
        {
            Debug.WriteLine("New level id: " + levelid);
            int Tries = 3;
            while (Tries >= 0)
            {
                try
                {
                    string output = Utilities.TCP.GetLevelDataResponse(levelid.ToString());
                    if (output != "-1")
                    {

                        Utilities.Encryption.Robtop_Parser i = new Utilities.Encryption.Robtop_Parser(output);
                        if (i.Parse())
                        {
                            var CurrentLevelData = new Utilities.JSON_Models.Level_Data();
                            CurrentLevelData.name = i.KeysAndCrates[2];
                            CurrentLevelData.difficultyFace = i.GetDifficultyFace();

                            if (CurrentLevelData != null)
                            {
                                Application.Current.Dispatcher.Invoke(new Action(() =>
                                {
                                    try
                                    {
                                        Main.leveln.Text = CurrentLevelData.name;
                                        Main.levelid.Text = "";
                                        Main.author.Text = GetRoom(Globals.Global_Data.Room);
                                    }
                                    catch (Exception ex)
                                    {
                                        Globals.Global_Data.HandleException(ex);
                                    }
                                }));
                                // CurrentLevelData.featured == true? difficulty += "-featured" :difficulty = difficulty;
                                SetLevelDiff(CurrentLevelData.difficultyFace);
                                if (Globals.Global_Data.Connection != null)
                                    if (Globals.Global_Data.Connection.isHelloAcked)
                                    {
                                        Main.StartAnimation("ShowLevelsAndStats");
                                    }
                            }
                        }
                    }
                    Tries = -1;
                }
                catch (Exception ex)
                {
                    Tries--;
                    Globals.Global_Data.HandleException(ex);
                }
            }
        }
        bool isAttemptsReset = false;
        public void SetPlayerName(string name)
        {
            Task.Run(() =>
            {
                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    Main.playerW.Text = Globals.Global_Data.Lang.Welcome.Replace("%username%", name);//"Welcome back, " + name + "!";
                    Globals.Global_Data.Username = name;
                }));
            });
        }
        public void AddPlayer(Border border)
        {
            Task.Run(() =>
            {
                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    if (!Main.pstacks.Children.Contains(border))
                        Main.pstacks.Children.Add(border);
                }));
            });
        }
        public async void SetLevelDiff(string diff)
        {
            Debug.WriteLine("Set diff: " + diff);
            try
            {
                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    try
                    {
                        BitmapImage image = new BitmapImage(new Uri("UI/Images/Difficulties/" + diff + ".png", UriKind.Relative));

                        if (image != null)
                            Main.leveldif.Source = image;
                    }
                    catch (Exception ex)
                    {
                        Globals.Global_Data.HandleException(ex);
                    }
                }));
            }
            catch { }

        }
        public void WaitForGeometryDash()
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                Main.waitinggd.IsOpen = true;
            }));


            while (!Globals.Global_Data.IsInjected) Task.Delay(500);


            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                Main.waitinggd.IsOpen = false;
            }));
        }
        public void Announce(string text)
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                try
                {
                    Main.announcer.Text = text;
                    Main.StartAnimation("Announce");
                }
                catch (Exception ex)
                {
                    Globals.Global_Data.HandleException(ex);
                }
            }));
        }
        public void ClearPlayers()
        {
            Task.Run(() =>
            {
                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    if (Main.pstacks.Children.Count > 0)
                        Main.pstacks.Children.Clear();
                }));
            });
        }
        public bool pfpset = false;
        public void DisableCustomIcons() =>
            Properties.Settings.Default.RenderCustomAnimations = true;
        
        public void EnableCustomIcons()
        {
            if (!File.Exists(Globals.Paths.GDMTempDataFile)) File.Create(Globals.Paths.GDMTempDataFile).Close();

            string output = JsonConvert.SerializeObject(Main.UserPref);
            File.WriteAllText(Globals.Paths.GDMTempDataFile, output);
        }
        public void ClearSelfIcons()
        {
            try
            {
                string IconsDirectory = Path.GetFullPath(Globals.Paths.IconsFolder + "/0");
                if (Directory.Exists(IconsDirectory)) Directory.Delete(IconsDirectory, true);

                pfpset = false; iconsDownloaded = false; DownloadSelfIcons();
            }
            catch (Exception ex)
            {
                Globals.Global_Data.HandleException(ex);
            }
        }
        public bool iconsDownloaded = false;
        public void DownloadSelfIcons()
        {
            try
            {
                string IconsDirectory = Path.GetFullPath(Globals.Paths.IconsFolder + "/0");
                if (!pfpset)
                {
                    Announce(Globals.Global_Data.Lang.DownloadingIcons);
                    if (!Properties.Settings.Default.IsVIP)
                    {
                        Properties.Settings.Default.RenderCustomAnimations = false;
                        SaveUserPref();
                    }
                    if (Properties.Settings.Default.RenderCustomAnimations)
                        DisableCustomIcons();
                    ShowMainProgressBar();
                    SetMainProgressBarValue(10);
                    pfpset = true;
                    try
                    {
                        if (!Main.UserPref.CachedIcons)
                        {
                            if (!Directory.Exists(Globals.Paths.SelfIconsFolder)) Directory.CreateDirectory(Globals.Paths.SelfIconsFolder);
                            Utilities.Utils.DeleteFilesOfExtension(Globals.Paths.SelfIconsFolder, "png");
                            Utilities.Utils.DeleteFilesOfExtension(Globals.Paths.SelfIconsFolder, "gif");
                        }
                    }
                    catch (Exception ex)
                    {
                        Globals.Global_Data.HandleException(ex);
                    }
                    for (int i = 0; i < 7; i++)
                    {
                        string icon_type = Client.Client.IconsAndIDs[i];
                        int iconID = GDM.Player_Watcher.Memory.Icons[i];
                        string apiurl = Utilities.TCP.GetAPIUrl(
                            icon_type,
                            ((int)GDM.Player_Watcher.Memory.Col1).ToString(),
                            ((int)GDM.Player_Watcher.Memory.Col2).ToString(),
                            iconID.ToString(),
                            Globals.Global_Data.PlayerID.ToString(),
                            ((int)GDM.Player_Watcher.Memory.IsGlow).ToString(),
                            GDM.Player_Watcher.Memory.Icons[0].ToString()
                            );
                        string path = null;
                        if (Main.UserPref.CachedIcons)
                        {
                            path = CheckIfIconExists(IconsDirectory, i.ToString());
                        }
                        while (path is null)
                        {
                            path = Utilities.TCP.DownloadImageToDir(apiurl, Globals.Paths.SelfIconsFolder, i.ToString());
                            // Debug.WriteLine("Downloaded at " + path);
                        }
                        SetMainProgressBarValue(((i + 1) / 5d) * 100);

                    }
                    SaveUserPref();
                    HideMainProgressBar();
                    Announce(Globals.Global_Data.Lang.IconsLoaded);
                    SetMyPFP();
                    iconsDownloaded = true;
                }
            }
            catch (Exception ex)
            {
                Utilities.Utils.HandleException(ex);
            }
        }
        public string CheckIfIconExists(string u, string index)
        {
            if (File.Exists(u + "/" + index + ".png"))
                return u + "/" + index + ".png";
            if (File.Exists(u + "/" + index + "/0.png"))
                return u + "/" + index + "/image.gif";
            return null;
        }
        public void ShowMainProgressBar()
        {
            Main.StartAnimation("ShowMainProg");
        }
        public void SetMainProgressBarValue(double value)
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                try
                {
                    var anim = new DoubleAnimation
                    {
                        To = value,
                        EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseInOut },
                        Duration = TimeSpan.FromMilliseconds(500)
                    };
                    anim.Completed += (s, e) =>
                    {
                        if (Main.mainprog != null)
                            Main.mainprog.Value = value;
                    };
                    if (Main.mainprog != null)
                        Main.mainprog.BeginAnimation(ProgressBar.ValueProperty, anim);
                }
                catch (Exception ex)
                {
                    Utilities.Utils.HandleException(ex);
                }
            }));

        }
        public void HideMainProgressBar()
        {
            Main.StartAnimation("ShowMainProgR");
        }
        public void SetMyPFP()
        {
            // string y = Utilities.TCP.GetAPIUrl("cube","","","",Globals.Paths.PlayerID.ToString(),"");
            // var g = await Utilities.TCP.GetNewImageAsync(new Uri(y,UriKind.Absolute));
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                try
                {
                    string CubeIconPath = Globals.Paths.SelfIconsFolder + "/0.png";

                    if (!File.Exists(CubeIconPath)) CubeIconPath = Globals.Paths.SelfIconsFolder + "/0/image.gif";

                    var bitmap = new BitmapImage();
                    var stream = File.OpenRead(CubeIconPath);

                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    if (bitmap != null)
                        ImageBehavior.SetAnimatedSource(Main.image6, bitmap);

                    stream.Close();
                    stream.Dispose();


                    Main.StartAnimation("ShowPFP");
                }
                catch (Exception ex)
                {
                    Utilities.Utils.HandleException(ex);
                }
            }));

        }
        public void SetRoom(short room)
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                Main.author.Text = Main.Master.GetRoom(Globals.Global_Data.Room);
               // Main.elapsed.Text = Main.Master.GetRoom(Globals.Global_Data.Room);
            }));
        }
        public void SetPing(int milliseconds)
        {
            if (milliseconds > 0)
            {
                Task.Run(() =>
                {
                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        try
                        {
                            Main.accountID.Text = Globals.Global_Data.Lang.ping.Replace("%ping%", milliseconds.ToString()); // "Ping : " + milliseconds.ToString() + "ms";
                            if (milliseconds > 200)
                                Main.accountID.Foreground = Main.redc.Background;
                            else
                                Main.accountID.Foreground = Main.tutrqiosecolor.Background;
                        }
                        catch (Exception ex)
                        {
                            Globals.Global_Data.HandleException(ex);
                        }
                    }));
                });
            }
        }
        public void SetPlayerID(int id)
        {
            SetPlayerID(id.ToString());
        }
        public void SetPlayerID(string id)
        {
            new Thread(() =>
            {

                try
                {
                    Properties.Settings.Default.IsVIP = Utilities.TCP.isVip(Globals.Global_Data.PlayerID.ToString());
                    Debug.WriteLine("IsVip: " + Properties.Settings.Default.IsVIP.ToString());
                    if (Properties.Settings.Default.IsVIP)
                    {
                        string j = Utilities.TCP.ReadURL("http://95.111.251.138/gdm/isRainbow.php?id=" + Globals.Global_Data.PlayerID.ToString()).Result;
                        var deserializedProduct = JsonConvert.DeserializeObject<Utilities.JSON_Models.Client_Data>(j);
                        Globals.Global_Data.Main.UserPref.ShowSelfRainbow = Convert.ToBoolean(deserializedProduct.israinbow);
                        Globals.Global_Data.Main.UserPref.ShowSelfRainbowPastel = Convert.ToBoolean(deserializedProduct.israinbowpastel);

                        Color color = (Color)ColorConverter.ConvertFromString(deserializedProduct.hexcolor);

                        Globals.Global_Data.Main.UserPref.R = color.R;
                        Globals.Global_Data.Main.UserPref.G = color.G;
                        Globals.Global_Data.Main.UserPref.B = color.B;
                    }

                    if (Globals.Global_Data.Initializer.iconsDownloaded)
                        Globals.Global_Data.Main.Master.SaveUserPref();

                    Globals.Global_Data.Initializer.DownloadSelfIcons();

                    if (!Properties.Settings.Default.IsVIP)
                    {
                        Properties.Settings.Default.RenderCustomAnimations = false;
                        DisableCustomIcons();
                    }
                    // Globals.Paths.Initializer.SetMyPFP();
                }
                catch (Exception ex)
                {
                    Utilities.Utils.HandleException(ex);
                }

                Debug.WriteLine("Account ID: " + id);
            }).Start();

            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                Main.accountID.Text = Globals.Global_Data.Lang.Accountid.Replace("%accountid%", id);
            }));
        }
        public void SetPing(string milliseconds)
        {
            //Application.Current.Dispatcher.Invoke(new Action(() =>
            //{
            //    if (ServerIndex == 0)
            //        Main.sg_ping.Text = milliseconds;
            //    else if (ServerIndex == 1)
            //        Main.sg_ping2.Text = milliseconds;
            //}));
        }
        public string GetRoom(short room)
        {
            switch (room)
            {
                case 0:
                    return "Lobby : Public";
                default:
                    return "Lobby : " + Utilities.Converter.ToString(room);

            }
        }
        public void SetLocalPort(int milliseconds)
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
              //  Main.server_online.Text = "Local Port : " + milliseconds.ToString();
            }));
        }
        public void InitializeDirectories()
        {
            if (!Directory.Exists(Globals.Paths.DataFolder))
                Directory.CreateDirectory(Globals.Paths.DataFolder);

            if (!Main.UserPref.CachedIcons)
                if (Directory.Exists(Globals.Paths.IconsFolder))
                    Utilities.Utils.DeleteDirectory(Globals.Paths.IconsFolder);

            if (!Directory.Exists(Globals.Paths.IconsFolder))
                Directory.CreateDirectory(Globals.Paths.IconsFolder);

            if (Directory.Exists(Globals.Paths.TempIcons))
                Utilities.Utils.DeleteDirectory(Globals.Paths.TempIcons);
        }
    }
}
