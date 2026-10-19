using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace GameLog
{
    /// <summary>HTTP 访问封装：共享 Cookie、TLS1.2、gzip、超时与编码探测。</summary>
    public static class Net
    {
        public const string UserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36";

        public static readonly CookieContainer Cookies = new CookieContainer();
        static readonly Regex CharsetRegex = new Regex(@"(?i)charset\s*=\s*[""']?([a-zA-Z0-9_\-]+)", RegexOptions.Compiled);
        static int consecutiveFailures;
        static readonly object FailGate = new object();

        /// <summary>连续失败次数（用于判断是否断网，避免逐个请求超时）。</summary>
        public static int ConsecutiveFailures
        {
            get { lock (FailGate) { return consecutiveFailures; } }
        }

        static Net()
        {
            try
            {
                ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol | SecurityProtocolType.Tls12;
            }
            catch { }
            try { ServicePointManager.DefaultConnectionLimit = 8; } catch { }
            try { ServicePointManager.Expect100Continue = false; } catch { }
        }

        public static string GetString(string url, string referer)
        {
            byte[] data = GetBytes(url, referer);
            return DecodeText(data);
        }

        public static byte[] GetBytes(string url, string referer)
        {
            string finalUrl;
            return GetBytes(url, referer, out finalUrl);
        }

        public static byte[] GetBytes(string url, string referer, out string finalUrl)
        {
            finalUrl = url;
            try
            {
                var data = GetBytesCore(url, referer, out finalUrl);
                lock (FailGate) { consecutiveFailures = 0; }
                return data;
            }
            catch (System.Net.WebException wex)
            {
                // 只有连不上（DNS/超时/断网）才算网络故障；403/404 说明网站可达，不应触发断网熔断
                var resp = wex.Response as System.Net.HttpWebResponse;
                if (resp == null) { lock (FailGate) { consecutiveFailures++; } }
                throw;
            }
            catch
            {
                lock (FailGate) { consecutiveFailures++; }
                throw;
            }
        }

        /// <summary>不参与断网计数的请求（用于百度百科等经常反爬的站点）。</summary>
        public static byte[] GetBytesSoft(string url, string referer, out string finalUrl)
        {
            finalUrl = url;
            try { return GetBytesCore(url, referer, out finalUrl); }
            catch { return null; }
        }

        static byte[] GetBytesCore(string url, string referer, out string finalUrl)
        {
            finalUrl = url;
            var req = Create(url, referer);
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var stream = resp.GetResponseStream())
            using (var ms = new MemoryStream())
            {
                try { if (resp.ResponseUri != null) finalUrl = resp.ResponseUri.AbsoluteUri; }
                catch { }
                byte[] buffer = new byte[16384];
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                    if (ms.Length > 12 * 1024 * 1024) break;
                }
                return ms.ToArray();
            }
        }

        static HttpWebRequest Create(string url, string referer)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = UserAgent;
            req.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8";
            req.Headers["Accept-Language"] = "zh-CN,zh;q=0.9,en;q=0.7";
            req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            req.Timeout = 12000;
            req.ReadWriteTimeout = 12000;
            req.KeepAlive = true;
            req.AllowAutoRedirect = true;
            req.MaximumAutomaticRedirections = 6;
            req.CookieContainer = Cookies;
            req.Proxy = null;
            if (!string.IsNullOrEmpty(referer)) req.Referer = referer;
            return req;
        }

        public static string DecodeText(byte[] data)
        {
            if (data == null || data.Length == 0) return "";
            if (data.Length > 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
                return Encoding.UTF8.GetString(data, 3, data.Length - 3);

            string charset = null;
            try
            {
                int head = Math.Min(data.Length, 4096);
                string ascii = Encoding.ASCII.GetString(data, 0, head);
                var m = CharsetRegex.Match(ascii);
                if (m.Success) charset = m.Groups[1].Value;
            }
            catch { }

            if (!string.IsNullOrEmpty(charset))
            {
                try
                {
                    var enc = Encoding.GetEncoding(charset);
                    return enc.GetString(data);
                }
                catch { }
            }
            return Encoding.UTF8.GetString(data);
        }
    }
}
