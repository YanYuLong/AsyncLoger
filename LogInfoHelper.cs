using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;

namespace Model
{
    /// <summary>
    /// 日志文件帮助类
    /// </summary>
    public class LogInfoHelper
    {
        #region Field
        private static Object m_Lock = new object();
        // 消息队列 (内部加锁访问)
        private Queue<MessageInfo> m_MessageQueue = new Queue<MessageInfo>();
        private string m_Path = System.IO.Path.Combine(System.Environment.CurrentDirectory, "LogInfo");
        private readonly string m_ExceptionFileBaseName="Excep";
        private readonly string m_LogFileBaseName="Log";
        private Thread m_BackWriteThread;
        private static LogInfoHelper m_Instance = null;
        private static readonly object syncObj = new object();
        #endregion

        #region  Method
        private bool WriteRunTimeMessage(string message, RunTimeMessageType messageType)
        {
            lock (m_Lock)
            {
                m_MessageQueue.Enqueue(new MessageInfo(message, messageType));
            }
            return true;
        }

        /// <summary>
        /// 向日志文件中写入运行时的程序信息
        /// </summary>
        /// <param name="Message">要写入到日志文件中的信息</param>
        /// <param name="messageType">要写入的日志类型</param>
        /// <param name="isAutoAddTimeInfo">是否在写入的信息前加上当前系统时间</param>
        /// <returns></returns>
        public bool WriteRunTimeMessage(string Message, RunTimeMessageType messageType, bool isAutoAddTimeInfo)
        {
            string message = string.Empty;
            if (isAutoAddTimeInfo)
            {
                message = string.Format("Time->{0} {1}", DateTime.Now.ToString(), Message);
            }
            else
            {
                message = Message;
            }
            return WriteRunTimeMessage(message, messageType);
        }
        /// <summary>
        /// 异步写文件线程入口函数
        /// </summary>
        private void WriteTreadHandler()
        {
            Queue<MessageInfo> tempQueue = new Queue<MessageInfo>();
            StreamWriter sw = null;
            StreamWriter expSw = null;
            StreamWriter logSw = null;
            if (!Directory.Exists(m_Path))
            {
                Directory.CreateDirectory(m_Path);
            }
            DateTime dtNow = DateTime.Now;
            string expFileName = m_ExceptionFileBaseName + "-" + dtNow.ToShortDateString() + ".log";
            string expFilePath = System.IO.Path.Combine(m_Path, expFileName);
            expFilePath = expFilePath.Replace('/', '-');
            expSw = new StreamWriter(expFilePath, true, Encoding.UTF8, 1024);

            string logFileName = m_LogFileBaseName + "-" + dtNow.ToShortDateString() + ".log";
            string logFilePath = System.IO.Path.Combine(m_Path, logFileName);
            logFilePath = logFilePath.Replace('/', '-');
            logSw = new StreamWriter(logFilePath, true, Encoding.UTF8, 1024);
            while (true)
            {
                #region 加锁拷贝部分
                lock (m_MessageQueue)
                {
                    int nowMessageCount = m_MessageQueue.Count;
                    for (int i = 0; i < nowMessageCount; i++)
                    {
                        try
                        {
                            tempQueue.Enqueue(m_MessageQueue.Dequeue());
                        }
                        catch (System.InvalidOperationException exp)
                        {
                            break;
                        }
                    }
                }
                #endregion
                try
                {
                    int nowTempQueueCount = tempQueue.Count;
                    for (int i = 0; i < nowTempQueueCount; i++)
                    {
                        MessageInfo messageInfo = tempQueue.Dequeue();
                        long fileSize = 0;
                        if (messageInfo.Type == RunTimeMessageType.Exception)
                        {
                            if (expSw.BaseStream == null || !expSw.BaseStream.CanWrite)
                            {
                                expSw = new StreamWriter(expFilePath, true, Encoding.UTF8, 1024);
                            }
                            sw = expSw;
                            FileInfo exceptionFileInfo = new FileInfo(expFilePath);
                            fileSize = exceptionFileInfo.Length / 1024 / 1024;
                        }
                        else if (messageInfo.Type == RunTimeMessageType.Log)
                        {
                            if (logSw.BaseStream == null || !logSw.BaseStream.CanWrite)
                            {
                                logSw = new StreamWriter(logFilePath, true, Encoding.UTF8, 1024);
                            }
                            sw = logSw;
                            FileInfo logsFileInfo = new FileInfo(logFilePath);
                            fileSize = logsFileInfo.Length / 1024 / 1024;
                        }
                        //当天异常日志大于10M的时候停止继续写入异常信息（PS：万一异常超级多 硬盘岂不是要炸~_~  服务器上全天跑着鬼知道那儿有bug）
                        if (fileSize > 10)
                        {
                            sw.Close();
                        }
                        //sw.WriteLine("------------------------------------------------------------");
                        sw.WriteLine(messageInfo.Message);
                    }
                }
                catch (Exception exp)
                {
                    throw exp;
                }
                finally
                {
                    if (sw != null)
                    {
                        sw.Close();
                        sw.Dispose();
                    }
                }
                Thread.Sleep(1000);
            }
        }
        #endregion

        #region  Constructor
        /// <summary>
        /// 实例化对象的时候初始化写入线程
        /// </summary>
        private LogInfoHelper()
        {
            m_BackWriteThread = new Thread(WriteTreadHandler);
            m_BackWriteThread.IsBackground = true;
            m_BackWriteThread.Name = "异步写入日志文件线程";
            m_BackWriteThread.Start();
        }

        /// <summary>
        /// 指定异常文件和日志文件基名称的构造函数 
        /// </summary>
        /// <param name="BaseExceptionFileName">异常文件的基名称</param>
        /// <param name="BaseLogFileName">日志文件的基名称</param>
        private LogInfoHelper(string BaseExceptionFileName, string BaseLogFileName):this()
        {
            m_ExceptionFileBaseName = BaseExceptionFileName;
            m_LogFileBaseName = BaseLogFileName;
        }
        #endregion
        /// <summary>
        /// 获取LogInfoHelper的实例
        /// </summary>
        public static LogInfoHelper GetInstance()
        {
                if (null == m_Instance)
                {
                    lock (syncObj)
                    {
                        if (null == m_Instance)
                        {
                            m_Instance = new LogInfoHelper();
                        }
                    }
                }
                return m_Instance;
        }

        /// <summary>
        /// 获取LogInfoHelper的实例 并指定异常和日志文件的基名称 (PS：基名称只能指定一次)
        /// </summary>
        /// <param name="ExceptionFileBaseName"></param>
        /// <param name="LogFileBas
        /// eName"></param>
        /// <returns></returns>
        public static LogInfoHelper GetInstance(string ExceptionFileBaseName,string LogFileBaseName)
        {
            if (null == m_Instance)
            {
                lock (syncObj)
                {
                    if (null == m_Instance)
                    {
                        m_Instance = new LogInfoHelper(ExceptionFileBaseName,LogFileBaseName);
                    }
                }
            }
            return m_Instance;
        }
    }

    /// <summary>
    /// 运行时需要写入到日志文件的信息类型
    /// </summary>
    public enum RunTimeMessageType
    {
        Exception,
        Log
    }

    /// <summary>
    /// runTimeInfo的抽象类
    /// </summary>
    internal class MessageInfo
    {
        private string m_Message = string.Empty;
        private RunTimeMessageType m_Type;

        public string Message
        {
            get { return m_Message; }
        }
        public RunTimeMessageType Type
        {
            get { return m_Type; }
        }

        public MessageInfo(string message, RunTimeMessageType type)
        {
            m_Message = message;
            m_Type = type;
        }
        /// <summary>
        /// 深拷贝方法
        /// </summary>
        /// <returns></returns>
        public MessageInfo DeepClon()
        {
            MessageInfo result = new MessageInfo(m_Message, m_Type);
            return result;
        }
    }
}

