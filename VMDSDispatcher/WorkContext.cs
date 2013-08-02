using System;
using System.Collections.Generic;
using System.Configuration;
using System.Text;
using System.Data;
using System.Threading;
using System.IO;
using ApplicationSetting;
using Miles.Logging;

namespace VMDSDispatcher
{
    public class WorkContext : BaseContext
    {
        public System.Threading.ManualResetEvent manualEvent;
        public int ContextIdx;
        public int IDX;
        public int CAMPAIGNLISTIDX;
        public string CAMPAIGNCD;
        public string CAMPAIGNDT;
        public DateTime JSTARTTIME;    // 該名單的預定外撥開始時間
        public DateTime JENDTIME;  // 該名單的預訂外撥結束時間
        public DateTime DSTARTTIME;  // 送至UMS的外撥開始時間
        public DateTime DENDTIME;   // 送至UMS的外撥結束時間
        public int PRIORITY;
        public int STATUS;
        public int RESULT;
        public int PHONEIDX;
        public string DIALNO;        
        public int OLDPHONEIDX;
        public string OLDDIALNO;
        public int PHASE;
        public string CUSTOMERID;
        public string CUSTOMERIDERR;
        public int UMSREF;
        public int DIALPLAN;
        public int MAXTRY;
        public string AREA;
        public string TEL;
        public int ResultCode;
        public frmMain formObj = null;
        public System.Data.SqlClient.SqlConnection VmdsSqlConn = new System.Data.SqlClient.SqlConnection();
        public VMDS.ConnectionStringLoader VmdsDbConnstrLoader = new VMDS.ConnectionStringLoader(Microsoft.Win32.Registry.LocalMachine);

         public WorkContext(string identifier)
            : base(identifier)
        {
        }

        public override bool Create()
        {         
            VmdsDbConnstrLoader.Load("VMDSDatabase");
            VmdsSqlConn.ConnectionString = VmdsDbConnstrLoader.ConnectionString;
            return true;
        }

        public override void Terminate()
        {
            if (VmdsSqlConn.State == ConnectionState.Open)
                VmdsSqlConn.Close();
            manualEvent.Set();
        }

        private bool ValidateVODJob()
        {
            LogEntry ent = null;
            StringBuilder stmt = null;
            try
            {
                if (VmdsSqlConn.State == ConnectionState.Closed)
                    VmdsSqlConn.Open();
                stmt = new StringBuilder();
                stmt.AppendFormat("EXEC sp_validate_VODJob {0}, {1}, '{2}', '{3}', '{4}', {5}",
                                    IDX, CAMPAIGNLISTIDX, CAMPAIGNDT, CUSTOMERID, CUSTOMERIDERR, PRIORITY);

                ent = new LogEntry(Severity.Information, "ValidateVODJob", string.Format("execute-sp : sp_validate_VODJob : ID={0}, CAMPAIGNLISTIDX={1}, CAMPAIGNDT={2}", IDX, CAMPAIGNLISTIDX, CAMPAIGNDT));
                if (m_JournalLogEnabled)
                    ent.AddMessage(stmt.ToString());
                m_Logger.Write(ent);

                System.Data.SqlClient.SqlCommand cmd = new System.Data.SqlClient.SqlCommand();
                cmd.Connection = VmdsSqlConn;
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = "sp_validate_VODJob";
                System.Data.SqlClient.SqlCommandBuilder.DeriveParameters(cmd);
                cmd.Parameters["@IDX"].Value = IDX;
                cmd.Parameters["@CAMPAIGNLISTIDX"].Value = CAMPAIGNLISTIDX;
                cmd.Parameters["@CAMPAIGNDT"].Value = CAMPAIGNDT;
                cmd.Parameters["@PIDNO"].Value = CUSTOMERID;
                cmd.Parameters["@PIDNOERR"].Value = CUSTOMERIDERR;
                cmd.Parameters["@PRI"].Value = PRIORITY;
                cmd.ExecuteNonQuery();
                int ReturnValue = (int)cmd.Parameters["@RETURN_VALUE"].Value;
                if (ReturnValue == 0)
                    return false;
                // Get DialNo
                stmt = new StringBuilder();
                System.Data.SqlClient.SqlDataAdapter da = null;
                System.Data.DataSet ds = new DataSet();
                System.Data.DataTable dt = null;
                stmt.AppendFormat("SELECT * FROM tblVODDialNo WITH (NOLOCK) WHERE MASTERIDX = {0} AND POS > {1} AND FLAG = 0", IDX, OLDPHONEIDX);

                ent = new LogEntry(Severity.Information, "ValidateVODJob", string.Format("Get VODDialNo : MASTERIDX = {0}, POS > {1}", IDX, OLDPHONEIDX));
                if (m_JournalLogEnabled)
                    ent.AddMessage(stmt.ToString());
                m_Logger.Write(ent);

                da = new System.Data.SqlClient.SqlDataAdapter(stmt.ToString(), VmdsSqlConn);
                da.Fill(ds, "tblVODDialNo");
                dt = ds.Tables["tblVODDialNo"];
                if (dt.Rows.Count == 0)
                {
                    UpdateVodRecord(IDX, 3, 0, OLDPHONEIDX, "");
                    return false;
                }
                PHONEIDX = (int)dt.Rows[0]["POS"];
                AREA = dt.Rows[0]["AREACD"].ToString();
                TEL = dt.Rows[0]["TELNO"].ToString();
                return true;
            }
            catch (System.Exception ex)
            {
                m_ExceptionHandler.HandleException(ex, "ValidateVODJob");
                return false;
            }
        }

        private bool UpdateVodRecord(int idx, int status, int result, int phoneidx, string dialno)
        {
            LogEntry ent = null;
            try
            {
                ent = new LogEntry(Severity.Information, "UpdateVodRecord", string.Empty);
                ent.AddMessage("UpdateVodRecord : idx = {0}, status = {1}, result = {2}, phoneidx = {3}", idx, status, result, phoneidx);
                m_Logger.Write(ent);
                
                string tmp = string.Empty;
                if (dialno.Length > 16)
                    tmp = dialno.Substring(0, 16);
                else
                    tmp = dialno;
                if (VmdsSqlConn.State == ConnectionState.Closed)
                    VmdsSqlConn.Open();
                StringBuilder stmt = new StringBuilder();
                stmt.AppendFormat("UPDATE tblVODMaster " +
                                "SET STATUS = {0}, RESULT = {1}, PHONEIDX = {2}, DIALNO = '{3}', UPDATEDT = GETDATE() " +
                                "WHERE IDX = {4}", status, result, phoneidx, dialno, idx);
                if (m_JournalLogEnabled)
                {
                    ent = new LogEntry(Severity.Information, "UpdateVodRecord", string.Empty);
                    ent.AddMessage(stmt.ToString());
                    m_Logger.Write(ent);
                }

                System.Data.SqlClient.SqlCommand cmd = new System.Data.SqlClient.SqlCommand();
                cmd.Connection = VmdsSqlConn;
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = stmt.ToString();
                cmd.ExecuteNonQuery();
                return true;
            }
            catch (System.Exception ex)
            {
                m_ExceptionHandler.HandleException(ex, "UpdateVodRecord");
                return false;
            }
        }

        private bool UpdateVodRecord(int idx, int status, int result, int phoneidx, string dialno, string jst)
        {
            LogEntry ent = null;
            try
            {
                ent = new LogEntry(Severity.Information, "UpdateVodRecord", string.Empty);
                ent.AddMessage("UpdateVodRecord : idx = {0}, status = {1}, result = {2}, phoneidx = {3}, jst = {4}", idx, status, result, phoneidx, jst);
                m_Logger.Write(ent);

                string tmp = string.Empty;
                if (dialno.Length > 16)
                    tmp = dialno.Substring(0, 16);
                else
                    tmp = dialno;
                if (VmdsSqlConn.State == ConnectionState.Closed)
                    VmdsSqlConn.Open();
                StringBuilder stmt = new StringBuilder();
                stmt.AppendFormat("UPDATE tblVODMaster " +
                                "SET STATUS = {0}, RESULT = {1}, PHONEIDX = {2}, DIALNO = '{3}', UPDATEDT = GETDATE(), JSTARTTIME = '{4}' " +
                                "WHERE IDX = {5}", status, result, phoneidx, dialno, jst, idx);
                if (m_JournalLogEnabled)
                {
                    ent = new LogEntry(Severity.Information, string.Empty, "UpdateVodRecord");
                    ent.AddMessage(stmt.ToString());
                    m_Logger.Write(ent);
                }
                System.Data.SqlClient.SqlCommand cmd = new System.Data.SqlClient.SqlCommand();
                cmd.Connection = VmdsSqlConn;
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = stmt.ToString();
                cmd.ExecuteNonQuery();
                return true;
            }
            catch (System.Exception ex)
            {
                m_ExceptionHandler.HandleException(ex, "UpdateVodRecord");
                return false;
            }
        }

        public override bool Loop()
        {
            if (!ValidateVODJob())
                return false;
            string tmp = tmp = AREA + TEL;
            if (!UpdateVodRecord(this.IDX, 2, 0, PHONEIDX, tmp, DSTARTTIME.ToString("yyyy/MM/dd HH:mm:ss")))
                return false;
            formObj.SendUmsGateway(this);
            return false;
        }
    }
}
