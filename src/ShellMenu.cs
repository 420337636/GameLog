using System;
using System.IO;
using System.Security.AccessControl;
using System.Text;
using Microsoft.Win32;

namespace GameLog
{
    /// <summary>资源管理器右键菜单（HKCU，无需管理员）。</summary>
    public static class ShellMenu
    {
        public const string Verb = "GameLogAdd";
        const string BaseKey = @"Software\Classes\exefile\shell\" + Verb;

        public static string AppExe
        {
            get
            {
                try { return System.Reflection.Assembly.GetEntryAssembly().Location; }
                catch { return ""; }
            }
        }

        /// <summary>在桌面创建快捷方式（避免用户直接双击 exe 时被 SmartScreen 拦）。</summary>
        public static string CreateDesktopShortcut()
        {
            try
            {
                string exe = AppExe;
                if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) return null;

                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string link = Path.Combine(desktop, "GameLog.lnk");

                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return null;
                object shell = Activator.CreateInstance(shellType);
                object shortcut = shellType.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { link });
                if (shortcut == null) return null;

                Type st = shortcut.GetType();
                st.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { exe });
                st.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { Path.GetDirectoryName(exe) });
                st.InvokeMember("IconLocation", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { exe + ",0" });
                st.InvokeMember("Description", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { "GameLog 游戏时光" });
                st.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, shortcut, null);
                Log.Info("已创建桌面快捷方式：" + link);
                return link;
            }
            catch (Exception ex)
            {
                Log.Warn("创建快捷方式失败：" + ex.Message);
                return null;
            }
        }

        /// <summary>去掉文件的“来自 Internet”标记（SmartScreen 每次拦截的常见原因）。</summary>
        public static bool UnblockSelf()
        {
            try
            {
                string exe = AppExe;
                if (string.IsNullOrEmpty(exe)) return false;
                File.Delete(exe + ":Zone.Identifier");
                Log.Info("已清除 SmartScreen 锁定标记");
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsInstalled()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(BaseKey))
                {
                    if (k == null) return false;
                }
                using (var c = Registry.CurrentUser.OpenSubKey(BaseKey + @"\command"))
                {
                    string cmd = c == null ? null : c.GetValue(null) as string;
                    return !string.IsNullOrEmpty(cmd);
                }
            }
            catch { return false; }
        }

        public static string InstalledCommand()
        {
            try
            {
                using (var c = Registry.CurrentUser.OpenSubKey(BaseKey + @"\command"))
                {
                    if (c == null) return null;
                    return c.GetValue(null) as string;
                }
            }
            catch { return null; }
        }

        /// <summary>生成 .reg 文件（注册表被安全软件拦截时，可手动右键合并）。</summary>
        public static string WriteRegFile(string directory, bool install)
        {
            try
            {
                Directory.CreateDirectory(directory);
                string name = install ? "GameLog-安装右键菜单.reg" : "GameLog-移除右键菜单.reg";
                string path = Path.Combine(directory, name);
                var sb = new StringBuilder();
                sb.AppendLine("Windows Registry Editor Version 5.00");
                sb.AppendLine();
                if (install)
                {
                    sb.AppendLine("[HKEY_CURRENT_USER\\Software\\Classes\\exefile\\shell\\" + Verb + "]");
                    sb.AppendLine("@=\"" + EscapeReg("添加到 GameLog") + "\"");
                    sb.AppendLine("\"Icon\"=\"" + EscapeReg("\"" + AppExe + "\",0") + "\"");
                    sb.AppendLine("\"MultiSelectModel\"=\"Single\"");
                    sb.AppendLine();
                    sb.AppendLine("[HKEY_CURRENT_USER\\Software\\Classes\\exefile\\shell\\" + Verb + "\\command]");
                    sb.AppendLine("@=\"" + EscapeReg("\"" + AppExe + "\" --add \"%1\"") + "\"");
                }
                else
                {
                    sb.AppendLine("[-HKEY_CURRENT_USER\\Software\\Classes\\exefile\\shell\\" + Verb + "]");
                }
                File.WriteAllText(path, sb.ToString(), Encoding.Unicode);
                return path;
            }
            catch (Exception ex)
            {
                Log.Error("生成 .reg 文件失败", ex);
                return null;
            }
        }

        static string EscapeReg(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        public static void Install()
        {
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(BaseKey))
                {
                    k.SetValue(null, "添加到 GameLog");
                    k.SetValue("Icon", "\"" + AppExe + "\",0");
                    k.SetValue("MultiSelectModel", "Single");
                }
                using (var c = Registry.CurrentUser.CreateSubKey(BaseKey + @"\command"))
                {
                    c.SetValue(null, "\"" + AppExe + "\" --add \"%1\"");
                }
                Log.Info("已注册右键菜单：" + BaseKey);
            }
            catch (Exception ex)
            {
                Log.Error("注册右键菜单失败", ex);
                throw;
            }
        }

        public static void Uninstall()
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(BaseKey, false);
                Log.Info("已移除右键菜单");
            }
            catch (Exception ex)
            {
                Log.Error("移除右键菜单失败", ex);
            }
        }

        /// <summary>程序被移动过时，刷新菜单里的命令路径。</summary>
        public static void EnsureUpToDate()
        {
            try
            {
                using (var c = Registry.CurrentUser.OpenSubKey(BaseKey + @"\command"))
                {
                    if (c == null) return;
                    string cmd = c.GetValue(null) as string;
                    string expected = "\"" + AppExe + "\" --add \"%1\"";
                    if (cmd != expected)
                    {
                        Install();
                    }
                }
            }
            catch { }
        }
    }

    /// <summary>开机自启（HKCU Run）。</summary>
    public static class AutoStart
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ValueName = "GameLog";

        public static bool IsEnabled()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                {
                    if (k == null) return false;
                    return !string.IsNullOrEmpty(k.GetValue(ValueName) as string);
                }
            }
            catch { return false; }
        }

        public static void SetEnabled(bool enabled)
        {
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (enabled) k.SetValue(ValueName, "\"" + ShellMenu.AppExe + "\" --minimized");
                    else k.DeleteValue(ValueName, false);
                }
            }
            catch (Exception ex) { Log.Error("设置开机自启失败", ex); }
        }
    }
}
