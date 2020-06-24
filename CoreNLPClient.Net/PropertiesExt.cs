namespace CoreNLPClientDotNet
{
    using System;
    using System.IO;
    using System.Text;     
    using Authlete.Util;
    using Newtonsoft.Json.Linq;

    public static class PropertiesExt
    {
        public static void Update(this JObject thisProps, JObject properties)
        {
            foreach (var prop in properties)
                thisProps[prop.Key] = prop.Value;
        }

        public static void ReadCoreNlpProps(this JObject properties, string path)
        {
            using (var sr = new StreamReader(path))
            {
                var dictProp = PropertiesLoader.Load(sr);
                foreach (var prop in dictProp)
                    properties.Add(prop.Key, prop.Value);
            }
        }

        public static string WriteCoreNlpProps(this JObject properties, string path = "")
        {
            if (string.IsNullOrEmpty(path))
            {
                var strUid = Guid.NewGuid().ToString();
                strUid = strUid.Replace("-", string.Empty).Substring(0, 16);
                path = $"corenlp_server-{strUid}.props";
            }

            using (var sw = new StreamWriter(File.Open(path, FileMode.Create), Encoding.GetEncoding("iso-8859-1")))
            {
                foreach (var prop in properties)
                {
                    sw.WriteLine(prop.Key + " = " + prop.Value.ToString());
                    sw.WriteLine(string.Empty);
                }

                return path;
            }
        }
    }
}
