using System;
using System.Collections.Generic;
using System.Text;
using System.Configuration;
using Engsound.Configuration;

namespace VMDSDispatcher
{
    public class TDbSetting
    {
        private static RuntimeEnvironmentConfigurationElement m_RuntimeConfig = null;
        private DatabaseConnectionConfigurationElement m_dbconfig = null;
        private string Key = string.Empty;
        private string ConnectionSetting = string.Empty;
        private string UserId = string.Empty;
        private string Password = string.Empty;


        static TDbSetting()
        {
            try
            {
                m_RuntimeConfig = ((EngsoundSetting)ConfigurationManager.GetSection(EngsoundSetting.SectionName)).ActiveEnvironment;
            }
            catch (Exception)
            {
            }
        }

        public TDbSetting()
        {

        }

        public bool LoadSetting(string key)
        {
            Key = key;
            if (!LoadConnectionSetting())
                return false;
            if (!LoadUserIdFromConfig())
                if (!LoadUserIdFromRegistry())
                    return false;
            if (!LoadPasswordFromConfig())
                if (!LoadPasswordFromRegistry())
                    return false;
            return true;
        }

        //public bool LoadSetting(string key)
        //{
        //    Key = key;
        //    try
        //    {
        //        m_dbconfig = m_RuntimeConfig.DatabaseConnection[Key];
        //        return (m_dbconfig != null);
        //    }
        //    catch (Exception)
        //    {
        //        return false;
        //    }
        //}

        public string GetConnectionString()
        {
            return m_dbconfig.ConnectionString;
        }

        private bool LoadConnectionSetting()
        {
            try
            {
                ConnectionSetting = ConfigurationManager.AppSettings[Key + ".ConnectionSetting"];
                if (ConnectionSetting == null)
                    return false;
                if (ConnectionSetting.Length == 0)
                    return false;
                string x = ConnectionSetting.Substring(ConnectionSetting.Length - 1, 1);
                if (x != ";")
                    ConnectionSetting += ";";
                return true;
            }
            catch (System.Exception ex)
            {
                return false;
            }
        }

        private bool LoadUserIdFromConfig()
        {
            try
            {
                UserId = ConfigurationManager.AppSettings[Key + ".UserId"];
                if (UserId == null)
                    return false;
                return true;
            }
            catch (System.Exception ex)
            {
                return false;
            }
        }

        private bool LoadUserIdFromRegistry()
        {
            try
            {
                Microsoft.Win32.RegistryKey rk = Microsoft.Win32.Registry.LocalMachine;
                
                Microsoft.Win32.RegistryKey k0 = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("Software");
                string[] n = k0.GetSubKeyNames();
                Microsoft.Win32.RegistryKey k1 = rk.OpenSubKey("SOFTWARE\\ENGSOUND\\VMDS\\" + Key);
                object uid = k1.GetValue("UserId");
                if (uid == null)
                    return false;
                UserId = (string)uid;
                return true;
            }
            catch (System.Exception ex)
            {
                return false;
            }
        }

        private bool LoadPasswordFromConfig()
        {
            try
            {
                Password = ConfigurationManager.AppSettings[Key + ".Password"];
                if (Password == null)
                    return false;
                return true;
            }
            catch (System.Exception ex)
            {
                return false;
            }
        }

        private bool LoadPasswordFromRegistry()
        {
            try
            {
                Microsoft.Win32.RegistryKey rk = Microsoft.Win32.Registry.LocalMachine;
                Microsoft.Win32.RegistryKey k1 = rk.OpenSubKey("SOFTWARE\\ENGSOUND\\VMDS\\" + Key, false);
                object pwd = k1.GetValue("Password");
                if (pwd == null)
                    return false;
                Password = (string)pwd;
                return true;
            }
            catch (System.Exception ex)
            {
                return false;
            }
        }
    }
}
