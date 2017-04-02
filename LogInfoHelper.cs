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
        private Queue<IGetWriteLogInfo> m_MessageQueue = new Queue<IGetWriteLogInfo>();
        private Thread m_BackWriteThread;
        private static LogInfoHelper m_Instance = null;
        private static readonly object syncObj = new object();
        #endregion

        #region  Method
        /// <summary>
        /// 向日志文件中写入运行时信息
        /// </summary>
        /// <param name="message">要写入的运行时消息</param>
        /// <param name="messageType"></param>
        /// <returns></returns>
        private bool WriteRunTimeMessage(IGetWriteLogInfo logInfo)
        {
            lock (m_Lock)
            {
                m_MessageQueue.Enqueue(logInfo);
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
        public bool WriteRunTimeMessage(IGetWriteLogInfo logInfo, bool isAutoAddTimeInfo)
        {
            logInfo.Message = DateTime.Now.ToString() + "->" + logInfo.Message;
            return WriteRunTimeMessage(logInfo);
        }
        /// <summary>
        /// 异步写文件线程入口函数
        /// </summary>
        private void WriteTreadHandler()
        {
            Queue<IGetWriteLogInfo> tempQueue = new Queue<IGetWriteLogInfo>();
            while (true)
            {
                StreamWriter sw = null;
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
                        IGetWriteLogInfo messageInfo = tempQueue.Dequeue();
                        long fileSize = 0;

                        DateTime dtNow = DateTime.Now;
                        //日志文件名称
                        string FileName = messageInfo.BaseFileName + "-" + dtNow.ToShortDateString() + ".log";
                        //日志文件的完整路径
                        string FilePath = System.IO.Path.Combine(messageInfo.DictionaryPath, FileName);
                        FilePath = FilePath.Replace("/", "-");
                        //判断日志文件的大小
                        if (File.Exists(FilePath))
                        {
                            FileInfo FileInfo = new FileInfo(FilePath);
                            fileSize = FileInfo.Length / 1024 / 1024;
                            //当天异常日志大于10M的时候停止继续写入异常信息（PS：万一异常超级多 硬盘岂不是要炸~_~  服务器上全天跑着鬼知道那儿有bug）
                            if (fileSize > 10)
                            {
                                continue;
                            }
                        }
                        sw = new StreamWriter(FilePath, true, Encoding.UTF8, 1024);
                        sw.AutoFlush = true;
                        sw.WriteLine(messageInfo.Message);
                        sw.Close();
                        //sw.WriteLine("------------------------------------------------------------");
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

    }

    /// <summary>
    /// 运行时需要写入到日志文件的信息类型
    /// </summary>
    public class LogMessageInfo : IGetWriteLogInfo
    {

        #region field
        private string m_BaseName;

        private string m_MessageType;

        private string m_Message = string.Empty;

        private string m_DictionaryPath = string.Empty;
        #endregion

        #region property
        /// <summary>
        /// 写入日志文件的基础文件名称
        /// </summary>
        public string BaseFileName
        {
            get { return m_BaseName; }
        }
        /// <summary>
        /// 写入日志文件的日志类型
        /// </summary>
        public string MessageType
        {
            get { return m_MessageType; }
        }
        /// <summary>
        /// 写入日志文件的日志信息
        /// </summary>
        public string Message
        {
            get { return m_Message; }
            set { if (!string.IsNullOrEmpty(value)) { m_Message = value; } }
        }
        /// <summary>
        /// 写入日志的文件存放目录
        /// </summary>
        public string DictionaryPath
        {
            get { return m_DictionaryPath; }
        }
        #endregion

        #region constructor
        public LogMessageInfo(string DictionaryPath, string MessageFileBaseString, string MessageType, string Message)
        {
            if (!string.IsNullOrEmpty(DictionaryPath) && System.IO.Directory.Exists(DictionaryPath))
            {
                m_DictionaryPath = DictionaryPath;
            }
            else
            {
                throw new Exception("指定存放异常文件的目录不存在或者错误！！");
            }
            if (!string.IsNullOrEmpty(MessageFileBaseString) && !string.IsNullOrEmpty(MessageType))
            {
                m_BaseName = MessageFileBaseString;
                m_MessageType = MessageType;
                m_Message = Message;
            }
        }
        #endregion

        /// <summary>
        /// 获取要写入到日志文件的字符串数据
        /// </summary>
        /// <returns></returns>
        public string GetLogString()
        {
            return m_Message;
        }
    }

    /// <summary>
    /// 获取要写入到日志文件中的文字字符串
    /// </summary>
    public interface IGetWriteLogInfo
    {
        /// <summary>
        /// 获取要写入到日志文件的字符串信息
        /// </summary>
        /// <returns></returns>
        string GetLogString();
        /// <summary>
        /// 获取写入的日志文件的基名称
        /// </summary>
        string BaseFileName { get; }
        /// <summary>
        /// 获取写入的日志的日志类型
        /// </summary>
        string MessageType { get; }
        /// <summary>
        /// 获取要写入的日志的消息
        /// </summary>
        string Message { get; set; }
        /// <summary>
        /// 获取要写入日志文件的文件夹路径
        /// </summary>
        string DictionaryPath { get; }
    }
}

