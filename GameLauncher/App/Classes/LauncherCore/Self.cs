﻿using GameLauncher.App.Classes;
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Flurl;
using Flurl.Http;
using System.Management;
using System.Net;

namespace GameLauncherReborn
{
    class Self
    {
        public static string mainserver = "https://api.worldunited.gg";
        public static string fileserver = "https://files.worldunited.gg";
        public static string staticapiserver = "http://api-sbrw.davidcarbon.download";
        public static string secondstaticapiserver = "http://api2-sbrw.davidcarbon.download";
        public static string woplserver = "http://worldonline.pl";
        public static string modnetserver = "http://cdn.soapboxrace.world";

        public static string[] serverlisturl = new string[]
        {
            mainserver + "/serverlist.json",
            staticapiserver + "/serverlist.json",
            secondstaticapiserver + "/serverlist.json",
            woplserver + "/serverlist.json"
        };

        public static string[] cdnlisturl = new string[]
        {
            mainserver + "/cdn_list.json",
            staticapiserver + "/cdn_list.json",
            secondstaticapiserver + "/cdn_list.json",
            woplserver + "/cdn_list.json"
        };

        public static string[] anticheatreporting = new string[]
        {
            mainserver + "/report",
            "http://anticheat.worldonline.pl/report",
            "http://la-sbrw.davidcarbon.download/report?"
        };

        public static string statsurl = mainserver + "/stats";

        public static string DiscordRPCID = "540651192179752970";

        public static int ProxyPort = new Random().Next(6260, 8269);
        public static Boolean sendRequest = true;

        public static String userAgent = null;

        public static Boolean CanDisableGame = true;
        public static String gamedir = null;

        public static string rememberjson = "";
        public static string discordid = String.Empty;

        public static string currentLanguage = "EN";

        public static long GetTimestamp(bool valid = false)
        {
            long ticks = DateTime.UtcNow.Ticks - DateTime.Parse("01/01/1970 00:00:00").Ticks;

            if (valid == true)
            {
                ticks /= 10000000;
            }
            else
            {
                ticks /= 10000;
            }

            return ticks;
        }

        public static bool HasWriteAccessToFolder(string path)
        {
            try
            {
                File.Create(path + "temp.txt").Close();
                File.Delete(path + "temp.txt");
            }
            catch
            {
                return false;
            }

            return true;
        }

        public static async Task SubmitError(Exception exception)
        {
            var mainsrv = DetectLinux.LinuxDetected() ? mainserver.Replace("https", "http") : mainserver;
            Url url = new Url(mainsrv + "/error-report");
            await url.PostJsonAsync(new
            {
                message = exception.Message ?? "no message",
                stackTrace = exception.StackTrace ?? "no stack trace"
            });
        }

        public static string CountryName(string twoLetterCountryCode)
        {
            CultureInfo[] cultures = CultureInfo.GetCultures(CultureTypes.SpecificCultures);

            foreach (CultureInfo culture in cultures)
            {
                RegionInfo region = new RegionInfo(culture.LCID);
                if (region.TwoLetterISORegionName.ToUpper() == twoLetterCountryCode.ToUpper())
                {
                    return region.EnglishName;
                }
            }

            return "Unknown";
        }

        public static void CenterScreen(Form form)
        {
            form.StartPosition = FormStartPosition.Manual;
            form.Top = (Screen.PrimaryScreen.Bounds.Height - form.Height) / 2;
            form.Left = (Screen.PrimaryScreen.Bounds.Width - form.Width) / 2;
        }

        //Let's actually make it cleaner and nicer - MeTonaTOR
        public static FolderType CheckFolder(string FolderName)
        {
            if (FolderName.Contains("C:\\Users") && FolderName.Contains("Temp")) return FolderType.IsTempFolder;
            if (FolderName.Contains("C:\\Users"))                                return FolderType.IsUsersFolders;
            if (FolderName.Contains("C:\\Program Files"))                        return FolderType.IsProgramFilesFolder;
            if (FolderName.Contains("C:\\Windows"))                              return FolderType.IsWindowsFolder;
            if (FolderName + "\\" == AppDomain.CurrentDomain.BaseDirectory)      return FolderType.IsSameAsLauncherFolder;

            return FolderType.Unknown;
        }

        public static string CleanFromUnknownChars(string s)
        {
            StringBuilder sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                if
                 (
                  (int)c >= 48 && (int)c <= 57 ||
                  (int)c == 60 ||
                  (int)c == 62 ||
                  (int)c >= 65 && (int)c <= 90 ||
                  (int)c >= 97 && (int)c <= 122 ||
                  (int)c == 47 ||
                  (int)c == 45 ||
                  (int)c == 46
                 )
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        public static bool CheckArchitectureFile(string fileName)
        {
            const int PE_POINTER_OFFSET = 60;
            const int MACHINE_OFFSET = 4;
            byte[] data = new byte[4096];

            using (Stream s = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            {
                s.Read(data, 0, 4096);
            }

            int PE_HEADER_ADDR = BitConverter.ToInt32(data, PE_POINTER_OFFSET);
            int machineUint = BitConverter.ToUInt16(data, PE_HEADER_ADDR + MACHINE_OFFSET);
            return machineUint == 0x014c;
        }

        public static bool GetInstalledHotFix(string identification)
        {
            var search = new ManagementObjectSearcher("SELECT HotFixID FROM Win32_QuickFixEngineering");
            var collection = search.Get();

            foreach (ManagementObject quickFix in collection)
            {
                Console.WriteLine("Updates installed: " + quickFix["HotFixID"].ToString());
                if (quickFix["HotFixID"].ToString() == identification)
                {
                    return true;
                }
            }

            return false;
        }

        public static string HostName2IP(string hostname)
        {
            IPHostEntry iphost = Dns.GetHostEntry(hostname);
            IPAddress[] addresses = iphost.AddressList;
            return addresses[0].ToString();
        }

        /* Moved "runAsAdmin" Code to Gist */
        /* https://gist.githubusercontent.com/DavidCarbon/97494268b0175a81a5f89a5e5aebce38/raw/eec2f9f80aa4b350ab98d32383e1ee1f2e1c26fd/Self.cs */

    }

    enum FolderType
    {
        IsTempFolder,
        IsUsersFolders,
        IsProgramFilesFolder,
        IsWindowsFolder,
        IsSameAsLauncherFolder,
        Unknown
    }
}
