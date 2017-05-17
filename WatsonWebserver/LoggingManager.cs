using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace WatsonWebserver
{
    /// <summary>
    /// Logging methods for Watson Webserver.
    /// </summary>
    internal class LoggingManager
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private bool ConsoleLogging;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initialize the logging manager.
        /// </summary>
        /// <param name="consoleLogging">Enable or disable console logging.</param>
        public LoggingManager(bool consoleLogging)
        {
            ConsoleLogging = consoleLogging;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Send a log message.
        /// </summary>
        /// <param name="msg">The message to send.</param>
        public void Log(string msg)
        {
            if (ConsoleLogging) Console.WriteLine(msg);
        }

        /// <summary>
        /// Log exception details to the console.
        /// </summary>
        /// <param name="method">The method where the exception was encountered.</param>
        /// <param name="e">The exception.</param>
        public void LogException(string method, Exception e)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));
            var st = new StackTrace(e, true);
            var frame = st.GetFrame(0);
            int fileLine = frame.GetFileLineNumber();
            string filename = frame.GetFileName();

            string message =
                Environment.NewLine +
                "---" + Environment.NewLine +
                "An exception was encountered which triggered this message" + Environment.NewLine +
                "  Method     : " + method + Environment.NewLine +
                "  Type       : " + e.GetType().ToString() + Environment.NewLine +
                "  Data       : " + e.Data + Environment.NewLine +
                "  Inner      : " + e.InnerException + Environment.NewLine +
                "  Message    : " + e.Message + Environment.NewLine +
                "  Source     : " + e.Source + Environment.NewLine +
                "  StackTrace : " + e.StackTrace + Environment.NewLine +
                "  Stack      : " + StackToString() + Environment.NewLine +
                "  Line       : " + fileLine + Environment.NewLine +
                "  File       : " + filename + Environment.NewLine +
                "  ToString   : " + e.ToString() + Environment.NewLine +
                "  Servername : " + Dns.GetHostName() + Environment.NewLine +
                "---";

            Log(message);
        }

        #endregion

        #region Private-Methods

        private string StackToString()
        {
            string ret = "";

            StackTrace t = new StackTrace();
            for (int i = 0; i < t.FrameCount; i++)
            {
                if (i == 0)
                {
                    ret += t.GetFrame(i).GetMethod().Name;
                }
                else
                {
                    ret += " <= " + t.GetFrame(i).GetMethod().Name;
                }
            }

            return ret;
        }

        #endregion
    }
}
