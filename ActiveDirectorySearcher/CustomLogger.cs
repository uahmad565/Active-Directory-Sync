using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ActiveDirectorySearcher
{
    public class CustomLogger
    {
        private readonly string filePath;
        private readonly string header;

        public string FilePath => filePath;

        public CustomLogger(string fileName, string header)
        {
            var basePath = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);

            filePath = Path.Combine(basePath ?? "", "Logs", fileName);
            this.header = header;
            Initialize();
        }

        public void WriteInfo(string message) => Write(message, MessageType.Info);

        public void WriteWarn(string message) => Write(message, MessageType.Warn);

        public void WriteError(string message) => Write(message, MessageType.Error);

        public void WriteJson(object obj, string title)
        {
            try
            {
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(obj, Newtonsoft.Json.Formatting.Indented);
                Write($"[{title}]{Environment.NewLine}{json}", MessageType.Info);
            }
            catch
            {
            }
        }

        public void WriteException(Exception ex)
        {
            var sb = new StringBuilder();

            while (ex != null)
            {
                sb.AppendLine(ex.Message);
                sb.AppendLine(ex.StackTrace);
                sb.AppendLine("----------------------------------------------------------------------");
                ex = ex.InnerException;
            }

            Write(sb.ToString(), MessageType.Error);
        }

        private void Initialize()
        {
            var directory = Path.GetDirectoryName(this.filePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private void Write(string message, MessageType messageType)
        {
            var formattedMessage = $"[{DateTime.Now:yyyy-MM-dd:HH:mm:ss.fff}] [{header}] [{messageType.ToString().ToUpper()}]: {message}{Environment.NewLine}";
            File.AppendAllText(this.filePath, formattedMessage);
        }

        private enum MessageType
        {
            Info, Warn, Error
        }
    }
}
