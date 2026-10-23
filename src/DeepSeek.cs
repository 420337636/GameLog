using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace GameLog
{
    /// <summary>
    /// 用 DeepSeek API 根据 exe 路径判断游戏名（deepseek-chat，非深度思考，输出限制很小以省 token）。
    /// 只有设置里填了 API Key 才会调用；没填就走原来的百度/游民星空流程。
    /// </summary>
    public static class DeepSeek
    {
        const string Endpoint = "https://api.deepseek.com/chat/completions";
        static readonly JavaScriptSerializer Ser = new JavaScriptSerializer();

        public static bool Enabled
        {
            get
            {
                try { return !string.IsNullOrEmpty(CurrentKey()); }
                catch { return false; }
            }
        }

        static string CurrentKey()
        {
            try { return (Store.Current == null ? null : Store.Current.Settings.DeepSeekKey) ?? ""; }
            catch { return ""; }
        }

        /// <summary>返回识别出的游戏名；失败/超时/Key 无效返回 null。</summary>
        public static string Identify(string exePath)
        {
            string key = CurrentKey();
            if (string.IsNullOrEmpty(key)) return null;
            if (string.IsNullOrEmpty(exePath)) return null;

            try
            {
                string content = Call(key, exePath);
                if (string.IsNullOrEmpty(content)) return null;
                string name = ExtractName(content);
                if (string.IsNullOrEmpty(name)) return null;
                return name;
            }
            catch (Exception ex)
            {
                Log.Warn("DeepSeek 识别失败：" + ex.Message);
                return null;
            }
        }

        static string Call(string key, string exePath)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            var payload = new Dictionary<string, object>();
            payload["model"] = "deepseek-chat";
            payload["temperature"] = 0;
            payload["max_tokens"] = 60;
            payload["stream"] = false;

            var msgs = new List<object>();
            msgs.Add(new Dictionary<string, object>
            {
                { "role", "system" },
                { "content", "你是游戏识别助手。根据 Windows 可执行文件路径判断是哪款游戏；如果是系列作品，结合路径里的代号/年份给出最可能的当代表。只输出 JSON：{\"name\":\"中文名或常用名\",\"confidence\":0.0}；不确定就输出 {\"name\":\"\",\"confidence\":0}。不要解释。" }
            });
            msgs.Add(new Dictionary<string, object>
            {
                { "role", "user" },
                { "content", "路径：" + exePath }
            });
            payload["messages"] = msgs;

            byte[] body = Encoding.UTF8.GetBytes(Ser.Serialize(payload));
            var req = (HttpWebRequest)WebRequest.Create(Endpoint);
            req.Method = "POST";
            req.ContentType = "application/json";
            req.Accept = "application/json";
            req.UserAgent = "GameLog/1.0";
            req.Timeout = 12000;
            req.ReadWriteTimeout = 12000;
            req.Headers["Authorization"] = "Bearer " + key;
            req.ContentLength = body.Length;

            using (var stream = req.GetRequestStream())
            {
                stream.Write(body, 0, body.Length);
            }

            string text;
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var rs = resp.GetResponseStream())
            using (var reader = new StreamReader(rs, Encoding.UTF8))
            {
                text = reader.ReadToEnd();
            }

            var m = Regex.Match(text, "\"content\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\"");
            if (!m.Success) return null;
            try { return Ser.Deserialize<string>("\"" + m.Groups[1].Value + "\""); }
            catch { return Regex.Unescape(m.Groups[1].Value); }
        }

        /// <summary>从模型输出里取出 name；输出不是严格 JSON 时退化为取第一行。</summary>
        static string ExtractName(string content)
        {
            if (string.IsNullOrEmpty(content)) return null;
            var m = Regex.Match(content, "\"name\"\\s*:\\s*\"([^\"]*)\"");
            string name = m.Success ? m.Groups[1].Value : null;

            if (string.IsNullOrEmpty(name))
            {
                // 兜底：取第一行，去掉引号、书名号和“游戏名：”之类前缀
                string first = content.Replace("\r", "\n").Split('\n')[0].Trim();
                first = Regex.Replace(first, "^(游戏名|名称|name)\\s*[:：]\\s*", "", RegexOptions.IgnoreCase);
                name = first;
            }

            if (string.IsNullOrEmpty(name)) return null;
            name = name.Trim().Trim('《', '》', '"', '\'', '。', '.', ' ');
            if (name.Length < 2 || name.Length > 60) return null;
            if (name.IndexOf("不确定", StringComparison.Ordinal) >= 0) return null;
            return name;
        }
    }
}
