using System;
using System.Collections.Generic;
using System.Text;
using System.Configuration;

namespace ApplicationSetting
{
    public class GatewaySettings : ConfigurationSection
    {
        [ConfigurationProperty("AppName")]
        public string AppName
        {
            get { return (string)this["AppName"]; }
            set { this["AppName"] = value; }
        }

        [ConfigurationProperty("EntityName")]
        public string EntityName
        {
            get { return (string)this["EntityName"]; }
            set { this["EntityName"] = value; }
        }

        [ConfigurationProperty("Profile")]
        public string Profile
        {
            get { return (string)this["Profile"]; }
            set { this["Profile"] = value; }
        }
    }

    public class DBGatewaySettings : ConfigurationSection
    {
        [ConfigurationProperty("ClassName")]
        public string ClassName
        {
            get { return (string)this["ClassName"]; }
            set { this["ClassName"] = value; }
        }
    }

    public class UMSGatewaySettings : ConfigurationSection
    {
        [ConfigurationProperty("ClassName")]
        public string ClassName
        {
            get { return (string)this["ClassName"]; }
            set { this["ClassName"] = value; }
        }
    }

    public class VMDSImporterSettings : ConfigurationSection
    {
        [ConfigurationProperty("ProgramDirectory")]
        public string ProgramDirectory
        {
            get { return (string)this["ProgramDirectory"]; }
            set { this["ProgramDirectory"] = value; }
        }

        [ConfigurationProperty("ExecutionFile")]
        public string ExecutionFile
        {
            get { return (string)this["ExecutionFile"]; }
            set { this["ExecutionFile"] = value; }
        }

        [ConfigurationProperty("WorkingDirectory")]
        public string WorkingDirectory
        {
            get { return (string)this["WorkingDirectory"]; }
            set { this["WorkingDirectory"] = value; }
        }
    }

    public class DispatcherContextSettings : ConfigurationSection
    {
        [ConfigurationProperty("MaxThread")]
        public int MaxThread
        {
            get { return (int)this["MaxThread"]; }
            set { this["MaxThread"] = value; }
        }

        [ConfigurationProperty("DefaultWaitInterval")]
        public int DefaultWaitInterval
        {
            get { return (int)this["DefaultWaitInterval"]; }
            set { this["DefaultWaitInterval"] = value; }
        }

        [ConfigurationProperty("NoRecordWaitInterval")]
        public int NoRecordWaitInterval
        {
            get { return (int)this["NoRecordWaitInterval"]; }
            set { this["NoRecordWaitInterval"] = value; }
        }

        [ConfigurationProperty("ErrorWaitInterval")]
        public int ErrorWaitInterval
        {
            get { return (int)this["ErrorWaitInterval"]; }
            set { this["ErrorWaitInterval"] = value; }
        }
    }
}
