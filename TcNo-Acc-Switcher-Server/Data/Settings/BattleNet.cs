﻿// TcNo Account Switcher - A Super fast account switcher
// Copyright (C) 2019-2022 TechNobo (Wesley Pyburn)
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

// Special thanks to iR3turnZ for contributing to this platform's account switcher
// iR3turnZ: https://github.com/HoeblingerDaniel

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.JSInterop;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TcNo_Acc_Switcher_Globals;
using TcNo_Acc_Switcher_Server.Pages.Basic;
using TcNo_Acc_Switcher_Server.Pages.BattleNet;
using TcNo_Acc_Switcher_Server.Pages.General;
using TcNo_Acc_Switcher_Server.Pages.General.Classes;

namespace TcNo_Acc_Switcher_Server.Data.Settings
{
    public class BattleNet
    {
        private static readonly Lang Lang = Lang.Instance;

        private static BattleNet _instance = new();

        private static readonly object LockObj = new();
        public static BattleNet Instance
        {
            get
            {
                lock (LockObj)
                {
                    // Load settings if have changed, or not set
                    if (_instance is { _currentlyModifying: true }) return _instance;
                    if (_instance != new BattleNet() && Globals.GetFileMd5(SettingsFile) == _instance._lastHash) return _instance;

                    _instance = new BattleNet { _currentlyModifying = true };

                    if (File.Exists(SettingsFile))
                    {
                        _instance = JsonConvert.DeserializeObject<BattleNet>(File.ReadAllText(SettingsFile), new JsonSerializerSettings() { });
                        _instance._lastHash = Globals.GetFileMd5(SettingsFile);
                    }
                    else
                    {
                        SaveSettings();
                    }

                    LoadBasicCompat(); // Add missing features in templated platforms system.

                    _instance._desktopShortcut = Shortcut.CheckShortcuts("BattleNet");
                    _instance._currentlyModifying = false;

                    return _instance;
                }
            }
            set => _instance = value;
        }
        private string _lastHash = "";
        private bool _currentlyModifying = false;
        public static void SaveSettings() => GeneralFuncs.SaveSettings(SettingsFile, Instance);

        #region Basic Compatability
        public static string GetShortcutImagePath(string gameShortcutName) =>
            Path.Join(GetShortcutImageFolder, PlatformFuncs.RemoveShortcutExt(gameShortcutName) + ".png");
        public static Dictionary<int, string> Shortcuts { get => Instance._shortcuts; set => Instance._shortcuts = value; }
        private static string GetShortcutImageFolder => $"img\\shortcuts\\BattleNet\\";
        private static string GetShortcutImagePath() => Path.Join(Globals.UserDataFolder, "wwwroot\\", GetShortcutImageFolder);
        public static string ShortcutFolder => "LoginCache\\BattleNet\\Shortcuts\\";
        private static readonly List<string> ShortcutFolders = new();
        private static string GetShortcutIgnoredPath(string shortcut) => Path.Join(ShortcutFolder, shortcut.Replace(".lnk", "_ignored.lnk").Replace(".url", "_ignored.url"));
        private static void LoadBasicCompat()
        {
            if (OperatingSystem.IsWindows())
            {
                // Add image for main platform button:
                var imagePath = Path.Join(GetShortcutImagePath(), "BattleNet.png");

                if (!File.Exists(imagePath))
                {
                    // Search start menu for BattleNet.
                    var startMenuFiles = Directory.GetFiles(BasicSwitcherFuncs.ExpandEnvironmentVariables("%StartMenuAppData%", true), "Battle.net.lnk", SearchOption.AllDirectories);
                    var commonStartMenuFiles = Directory.GetFiles(BasicSwitcherFuncs.ExpandEnvironmentVariables("%StartMenuProgramData%", true), "Battle.net.lnk", SearchOption.AllDirectories);
                    if (startMenuFiles.Length > 0)
                        Globals.SaveIconFromFile(startMenuFiles[0], imagePath);
                    else if (commonStartMenuFiles.Length > 0)
                        Globals.SaveIconFromFile(commonStartMenuFiles[0], imagePath);
                    else
                        Globals.SaveIconFromFile(Exe(), imagePath);
                }


                var cacheShortcuts = ShortcutFolder; // Shortcut cache
                foreach (var sFolder in ShortcutFolders)
                {
                    if (sFolder == "") continue;
                    // Foreach file in folder
                    foreach (var shortcut in new DirectoryInfo(BasicSwitcherFuncs.ExpandEnvironmentVariables(sFolder, true)).GetFiles())
                    {
                        var fName = shortcut.Name;
                        if (PlatformFuncs.RemoveShortcutExt(fName) == "BattleNet") continue; // Ignore self

                        // Check if in saved shortcuts and If ignored
                        if (File.Exists(GetShortcutIgnoredPath(fName)))
                        {
                            imagePath = Path.Join(GetShortcutImagePath(), PlatformFuncs.RemoveShortcutExt(fName) + ".png");
                            if (File.Exists(imagePath)) File.Delete(imagePath);
                            fName = fName.Replace("_ignored", "");
                            if (Shortcuts.ContainsValue(fName))
                                Shortcuts.Remove(Shortcuts.First(e => e.Value == fName).Key);
                            continue;
                        }

                        Directory.CreateDirectory(cacheShortcuts);
                        var outputShortcut = Path.Join(cacheShortcuts, fName);

                        // Exists and is not ignored: Update shortcut
                        File.Copy(shortcut.FullName, outputShortcut, true);
                        // Organization will be saved in HTML/JS
                    }
                }

                // Now get images for all the shortcuts in the folder, as long as they don't already exist:
                List<string> existingShortcuts = new();
                if (Directory.Exists(cacheShortcuts))
                    foreach (var f in new DirectoryInfo(cacheShortcuts).GetFiles())
                    {
                        if (f.Name.Contains("_ignored")) continue;
                        var imageName = PlatformFuncs.RemoveShortcutExt(f.Name) + ".png";
                        imagePath = Path.Join(GetShortcutImagePath(), imageName);
                        existingShortcuts.Add(f.Name);
                        if (!Shortcuts.ContainsValue(f.Name))
                        {
                            // Not found in list, so add!
                            var last = Shortcuts.Count > 0 ? Shortcuts.Last().Key : -1;
                            last += 1;
                            Shortcuts.Add(last, f.Name); // Organization added later
                        }

                        // Extract image and place in wwwroot (Only if not already there):
                        if (!File.Exists(imagePath))
                        {
                            Globals.SaveIconFromFile(f.FullName, imagePath);
                        }
                    }

                foreach (var (i, s) in Shortcuts)
                {
                    if (!existingShortcuts.Contains(s))
                        Shortcuts.Remove(i);
                }
            }

            SaveSettings();
        }

        [JSInvokable]
        public static void SaveShortcutOrderBNet(Dictionary<int, string> o)
        {
            Shortcuts = o;
            SaveSettings();
        }

        [JSInvokable]
        public static void HandleShortcutActionBNet(string shortcut, string action)
        {
            if (shortcut == "btnStartPlat") // Start platform requested
            {
                Basic.RunPlatform(Exe(), action == "admin", "", "BattleNet");
                return;
            }

            if (!Shortcuts.ContainsValue(shortcut)) return;

            switch (action)
            {
                case "hide":
                    {
                        // Remove shortcut from folder, and list.
                        Shortcuts.Remove(Shortcuts.First(e => e.Value == shortcut).Key);
                        var f = Path.Join(ShortcutFolder, shortcut);
                        if (File.Exists(f)) File.Move(f, f.Replace(".lnk", "_ignored.lnk").Replace(".url", "_ignored.url"));

                        // Save.
                        SaveSettings();
                        break;
                    }
                case "admin":
                    Basic.RunShortcut(shortcut, "LoginCache\\BattleNet\\Shortcuts", true);
                    break;
            }
        }
        #endregion

        #region VARIABLES

        [JsonProperty("FolderPath", Order = 1)] private string _folderPath = "C:\\Program Files (x86)\\Battle.net";
        [JsonProperty("BattleNet_Admin", Order = 3)] private bool _admin;
        [JsonProperty("BattleNet_TrayAccNumber", Order = 4)] private int _trayAccNumber = 3;
        [JsonProperty("ForgetAccountEnabled", Order = 5)] private bool _forgetAccountEnabled;
        [JsonProperty("OverwatchMode", Order = 6)] private bool _overwatchMode = true;
        [JsonProperty("ImageExpiryTime", Order = 7)] private int _imageExpiryTime = 7;
        [JsonProperty("AltClose", Order = 8)] private bool _altClose;
        [JsonProperty("ShortcutsJson", Order = 9)] private Dictionary<int, string> _shortcuts = new();
        [JsonIgnore] private bool _desktopShortcut;
        [JsonIgnore] private List<BattleNetSwitcherBase.BattleNetUser> _accounts = new();
        [JsonIgnore] private List<string> _ignoredAccounts = new();

        public static string FolderPath { get => Instance._folderPath; set => Instance._folderPath = value; }

        public static bool Admin { get => Instance._admin; set => Instance._admin = value; }

        public static int TrayAccNumber { get => Instance._trayAccNumber; set => Instance._trayAccNumber = value; }

        public static bool ForgetAccountEnabled { get => Instance._forgetAccountEnabled; set => Instance._forgetAccountEnabled = value; }

        public static bool OverwatchMode { get => Instance._overwatchMode; set => Instance._overwatchMode = value; }

        public static int ImageExpiryTime { get => Instance._imageExpiryTime; set => Instance._imageExpiryTime = value; }

        public static bool AltClose { get => Instance._altClose; set => Instance._altClose = value; }

        public static bool DesktopShortcut { get => Instance._desktopShortcut; set => Instance._desktopShortcut = value; }

        public static List<BattleNetSwitcherBase.BattleNetUser> Accounts { get => Instance._accounts; set => Instance._accounts = value; }

        public static List<string> IgnoredAccounts { get => Instance._ignoredAccounts; set => Instance._ignoredAccounts = value; }

        // Constants
        public static readonly string SettingsFile = "BattleNetSettings.json";
        public static readonly string Processes = "Battle.net";
        public static readonly string StoredAccPath = "LoginCache\\BattleNet\\StoredAccounts.json";
        public static readonly string IgnoredAccPath = "LoginCache\\BattleNet\\IgnoredAccounts.json";
        public static readonly string ImagePath = "wwwroot\\img\\profiles\\battlenet\\";
        public static readonly string ContextMenuJson = $@"[
              {{""{Lang["Context_SwapTo"]}"": ""swapTo(-1, event)""}},
              {{""{Lang["Context_BNet_SetBTag"]}"": ""showModal('changeUsername:BattleTag')""}},
              {{""{Lang["Context_BNet_DelBTag"]}"": ""forgetBattleTag()""}},
              {{""{Lang["Context_BNet_GetRAnk"]}"": ""refetchRank()""}},
              {{""{Lang["Context_CreateShortcut"]}"": ""createShortcut()""}},
              {{""{Lang["Context_ChangeImage"]}"": ""changeImage(event)""}},
              {{""{Lang["Forget"]}"": ""forget(event)""}}
            ]";

        #endregion

        #region FORGETTING_ACCOUNTS

        /// <summary>
        /// Updates the ForgetAccountEnabled bool in settings file
        /// </summary>
        /// <param name="enabled">Whether will NOT prompt user if they're sure or not</param>
        public static void SetForgetAcc(bool enabled)
        {
            Globals.DebugWriteLine(@"[Func:Data\Settings\BattleNet.SetForgetAcc]");
            if (ForgetAccountEnabled == enabled) return; // Ignore if already set
            ForgetAccountEnabled = enabled;
            SaveSettings();
        }

        #endregion


        /// <summary>
        /// Get Battle.net.exe path from BattleNetSettings
        /// </summary>
        /// <returns>Battle.net.exe's path string</returns>
        public static string Exe() => FolderPath + "\\Battle.net.exe";


        #region SETTINGS

        /// <summary>
        /// Default settings for BattleNetSettings.json
        /// </summary>
        public static void ResetSettings()
        {
            Globals.DebugWriteLine(@"[Func:Data\Settings\BattleNet.ResetSettings]");
            FolderPath = "C:\\Program Files (x86)\\Battle.net";
            Admin = false;
            TrayAccNumber = 3;
            OverwatchMode = true;
            // Should this also clear ignored accounts?
            DesktopShortcut = Shortcut.CheckShortcuts("BattleNet");
            AltClose = false;

            SaveSettings();
        }

        /// <summary>
        /// Load the Stored Accounts and Ignored Accounts
        /// </summary>
        public static void LoadAccounts()
        {
            _ = Directory.CreateDirectory("LoginCache\\BattleNet");
            if (File.Exists(StoredAccPath))
            {
                Accounts = JsonConvert.DeserializeObject<List<BattleNetSwitcherBase.BattleNetUser>>(Globals.ReadAllText(StoredAccPath)) ?? new List<BattleNetSwitcherBase.BattleNetUser>();
            }

            if (File.Exists(IgnoredAccPath))
            {
                IgnoredAccounts = JsonConvert.DeserializeObject<List<string>>(Globals.ReadAllText(IgnoredAccPath)) ?? new List<string>();
            }
        }

        /// <summary>
        /// Load the Stored Accounts and Ignored Accounts
        /// </summary>
        public static void SaveAccounts()
        {
            File.WriteAllText(StoredAccPath, JsonConvert.SerializeObject(Accounts));
            File.WriteAllText(IgnoredAccPath, JsonConvert.SerializeObject(IgnoredAccounts));
        }

        #endregion
    }
}
