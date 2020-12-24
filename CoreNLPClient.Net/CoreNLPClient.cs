namespace CoreNLPClientDotNet
{
    using System;
    using System.Collections.Specialized;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Web;
    using Edu.Stanford.Nlp.Pipeline;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public enum StartServer
    {
        DontStart,
        ForceStart,
        TryStart
    }

    public class CoreNLPClient : RobustService, IDisposable
    {
        public const string DefaultEndpoint = "http://localhost:9000";
        public const int DefaultTimeout = 60000;
        public const int DefaultThreads = 5;
        public const string DefaultAnnotators = "tokenize,ssplit,pos,lemma,ner,depparse";
        public const string DefaultInputFormat = "text";
        public const string DefaultOutputFormat = "serialized";
        public const string DefaultMemory = "5G";
        public const int DefaultMaxCharLength = 100000;
        public const string DefaultSerializer = "edu.stanford.nlp.pipeline.ProtobufAnnotationSerializer";

        private StartServer _startServer;
        private int _timeout;
        private int _threads;
        private string _memory;
        private int _maxCharLength;
        private bool _preLoad;
        private string _classPath;
        private string[] _args;
        private JObject _kwargs;

        private object _annotators;
        private object _properties;
        private string _outputFormat;

        private JObject _srvPropsFile;
        private JObject _srvStartInfo;

        private JObject _propsCache;

        public CoreNLPClient(
            StartServer startServer = StartServer.TryStart,
            string endPoint = DefaultEndpoint,
            int timeout = DefaultTimeout,
            int threads = DefaultThreads,
            object annotators = null,
            object properties = null,
            string outputFormat = DefaultOutputFormat,
            string memory = DefaultMemory,
            bool beQuiet = false,
            int maxCharLength = DefaultMaxCharLength,
            bool preLoad = true,
            string classPath = "",
            string[] args = null,
            JObject kwargs = null)
        {
            _startServer = startServer;
            _endPoint = endPoint;
            _timeout = timeout;
            _threads = threads;
            _memory = memory;
            _beQuiet = beQuiet;
            _maxCharLength = maxCharLength;
            _preLoad = preLoad;
            _classPath = classPath;
            _args = args;
            _kwargs = kwargs;

            _annotators = annotators;
            _properties = properties;
            _outputFormat = outputFormat;

            var uri = new Uri(_endPoint);
            _host = uri.Host;
            _port = uri.Port;

            _propsCache = new JObject();
            _srvStartInfo = new JObject();

            _srvPropsFile = new JObject
            {
                ["is_temp"] = false,
                ["path"] = string.Empty
            };

            Init(properties, annotators, outputFormat);
        }

        public object Annotate(string text, object annotators = null, string outputFormat = "", string propertiesKey = "", JObject properties = null)
        {
            // Send a request to the CoreNLP server.

            // text: Raw text for the CoreNLPServer to parse
            // annotators: List of annotators to use
            // output_format: Output type from server: serialized, json, text, conll, conllu, or xml
            // properties_key: Key into properties cache for the client
            // properties: Additional request properties(written on top of defaults)

            // The properties for a request are written in this order:

            // 1. Server default properties(server side)
            // 2. Properties from client's properties_cache corresponding to properties_key (client side)
            //    If the properties_key is the name of a Stanford CoreNLP supported language:
            //    [Arabic, Chinese, English, French, German, Spanish], the Stanford CoreNLP defaults will be used (server side)
            // 3. Additional properties corresponding to properties(client side)
            // 4. Special case specific properties: annotators, output_format(client side)

            var requestProperties = new JObject();

            // Start with client defaults
            if (_annotators != null)
                requestProperties["annotators"] = Ann2Str(_annotators);
            if (!string.IsNullOrEmpty(_outputFormat))
                requestProperties["outputFormat"] = _outputFormat;

            // Set properties for server call
            // First look for a cached default properties set
            // If a Stanford CoreNLP supported language is specified, just pass { pipelineLanguage = "french"}
            if (!string.IsNullOrEmpty(propertiesKey))
            {
                var propsKeyLCase = propertiesKey.ToLower();
                if (propsKeyLCase == Pipeline.Lang.English || propsKeyLCase == Pipeline.Lang.EnglishShort)
                    requestProperties = Pipeline.GetEnglishDefaultReqProperties();
                else if (propsKeyLCase.IsLang())
                    requestProperties["pipelineLanguage"] = propsKeyLCase;
                else if (!_propsCache.ContainsKey(propertiesKey))
                    throw new Exception($"Properties cache does not have {propertiesKey}");
                else
                    requestProperties = new JObject(_propsCache[propertiesKey]);
            }

            // Add on custom properties for this request
            if (properties == null)
                properties = new JObject();
            requestProperties.Update(properties);

            // If annotators list is specified, override with that
            if (annotators != null)
                requestProperties["annotators"] = Ann2Str(annotators);

            // Always send an output format with request
            // In some scenario's the server's default output format is unknown, so default to serialized
            if (!string.IsNullOrEmpty(outputFormat))
                requestProperties["outputFormat"] = outputFormat;

            if (requestProperties["outputFormat"] == null)
            {
                JToken outputFmt = null;
                if (_srvStartInfo["props"] != null && _srvStartInfo["props"]["outputFormat"] != null)
                    outputFmt = _srvStartInfo["props"]["outputFormat"];

                if (outputFmt != null)
                    requestProperties["outputFormat"] = outputFmt;
                else
                    requestProperties["outputFormat"] = DefaultOutputFormat;
            }

            var response = Request(text, requestProperties, "/");
            if (response == null)
                return null;

            using (var respStream = response.GetResponseStream())
            {
                switch (requestProperties["outputFormat"].ToString())
                {
                    case "text":
                    case "conllu":
                    case "conll":
                    case "xml":
                        var sr = new StreamReader(respStream);
                        return sr.ReadToEnd();
                    case "serialized":
                        return Document.Parser.ParseDelimitedFrom(respStream);
                    case "json":
                        var srJson = new StreamReader(respStream);
                        var json = srJson.ReadToEnd();
                        return JsonConvert.DeserializeObject(json);
                    default:
                        return null;
                }
            }
        }

        public HttpWebResponse Request(string text, JObject properties, string path = "", NameValueCollection queryString = null)
        {
            EnsureAlive();

            var textBytes = Encoding.UTF8.GetBytes(text);

            var reqUriString = _endPoint;
            reqUriString += path;
            reqUriString += '?';

            if (queryString == null)
            {
                var props = new JObject(properties);
                if (props["outputFormat"] == null)
                    props.Add("outputFormat", DefaultOutputFormat);                

                queryString = HttpUtility.ParseQueryString(string.Empty);
                queryString.Add("properties", props.ToString());                
            }

            reqUriString += queryString;

            var host = _endPoint.ToLower();
            host = host.Replace("http://", string.Empty);
            host = host.Replace("https://", string.Empty);

            var contentType = string.Empty;
            var inputFormat = DefaultInputFormat;
            if (properties["inputFormat"] != null)
                inputFormat = properties["inputFormat"].ToString();

            switch (inputFormat)
            {
                case "text":
                    contentType = "text/plain; charset=utf-8";
                    break;
                case "serialized":
                    contentType = "application/x-protobuf";
                    break;
            }

            try
            {
                var req = (HttpWebRequest)WebRequest.Create(reqUriString);
                req.Method = "POST";
                req.Accept = "*";
                req.ContentLength = textBytes.Length;
                req.ContentType = contentType; 
                req.Host = host;
                req.Referer = _endPoint;

                using (var reqStream = req.GetRequestStream())
                {
                    reqStream.Write(textBytes, 0, textBytes.Length);
                    reqStream.Close();
                }

                return (HttpWebResponse)req.GetResponse();
            }
            catch (Exception ex)
            {
                Debug.Assert(false, "Exception thrown while trying to make a request. Msg=" + ex.Message);
                Console.Write(ex.Message);
                return null;
            }
        }

        public dynamic TokensRegex(string text, string pattern, bool filter = false)
        {
            return Regex("/tokensregex", text, pattern, filter);
        }

        public dynamic Semgrex(string text, string pattern, bool filter = false)
        {
            return Regex("/semgrex", text, pattern, filter);
        }

        public dynamic TRegex(string text, string pattern, bool filter = false)
        {
            return Regex("/tregex", text, pattern, filter);
        }

        public void Dispose()
        {
            Stop();

            var isPropsFileTemp = (bool)_srvPropsFile["is_temp"];
            if (isPropsFileTemp)
            {
                var propsFile = _srvStartInfo["props_file"].ToString();
                if (File.Exists(propsFile))
                    File.Delete(propsFile);
            }
        }

        private string Ann2Str(object annotators)
        {
            var annStr = annotators as string;
            if (!string.IsNullOrEmpty(annStr))
                return annStr;

            var annLst = annotators as string[];
            if (annLst != null)
                return string.Join(",", annLst);

            return string.Empty;
        }

        private dynamic Regex(string path, string text, string pattern, bool filter, object annotators = null, JObject properties = null)
        {
            EnsureAlive();

            if (properties == null)
            {
                properties = new JObject();
                properties.Update(new JObject
                {
                    ["inputFormat"] = "text",
                    ["serializer"] = DefaultSerializer
                });
            }

            if (annotators != null)
            {
                var annStr = annotators as string;
                if (!string.IsNullOrEmpty(annStr))
                    properties["annotators"] = annStr;
                else
                {
                    var annLst = annotators as string[];
                    if (annLst != null)
                        properties["annotators"] = string.Join(",", annLst);
                }
            }

            properties["outputFormat"] = "json";

            // TODO: get rid of this once corenlp 4.0.0 is released?
            // the "stupid reason" has hopefully been fixed on the corenlp side
            // but maybe people are married to corenlp 3.9.2 for some reason
            // HACK: For some stupid reason, CoreNLPServer will timeout if we
            // need to annotate something from scratch. So, we need to call
            // this to ensure that the _regex call doesn't timeout.
            //// Annotate(text, properties: properties);

            var props = new JObject();
            props.Add("outputFormat", "json");

            var queryString = HttpUtility.ParseQueryString(string.Empty);
            queryString.Add("properties", props.ToString());
            queryString.Add("pattern", pattern);
            queryString.Add("filter", filter.ToString());

            var response = Request(text, properties, path, queryString);
            if (response == null)
                return null;

            using (var respStream = response.GetResponseStream())
            {
                var srJson = new StreamReader(respStream);
                var json = srJson.ReadToEnd();
                return JsonConvert.DeserializeObject(json);
            }
        }

        private void Init(object properties, object annotators, string outputFormat)
        {
            if (_startServer == StartServer.DontStart)
                return;

            _ignoreBindingError = _startServer == StartServer.TryStart;

            SetupDefaultServerProps(properties, annotators, outputFormat);

            Debug.Assert(_host.ToLower().Substring(0, "localhost".Length) == "localhost", "Server host name must be 'localhost'");

            var isEnvVar = false;
            var classPath = _classPath;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                isEnvVar = classPath.ToUpper() == "%CLASSPATH%";
            else
                isEnvVar = classPath.ToUpper() == "$CLASSPATH";

            if (isEnvVar)
                classPath = Environment.GetEnvironmentVariable("CLASSPATH");
            else if (string.IsNullOrEmpty(classPath))
                classPath = Environment.GetEnvironmentVariable("CORENLP_HOME");

            Debug.Assert(!string.IsNullOrEmpty(classPath), "Classpath variable, used to locate CoreNLP server, is undefined");
            classPath += Path.DirectorySeparatorChar + "*";

            // Strangely, _beQuiet variable is used with a dual purpose. 
            //      1) Mute stdout, stderr. See: RobustService.Start()
            //      2) Mute mirroring input data. See: https://stanfordnlp.github.io/CoreNLP/corenlp-server.html#command-line-flags
            // TODO: For some reason, queries will timeout when beQuiet==true. Yet to debug

            _procName = "java";
            _procArgs = $"-Xmx{_memory}" +
                $" -cp \"{classPath}\"" +
                " edu.stanford.nlp.pipeline.StanfordCoreNLPServer" +
                $" -port {_port}" +
                $" -timeout {_timeout}" +
                $" -threads {_threads} " +
                $" -maxCharLength {_maxCharLength}" +
                $" -quiet {_beQuiet}" +
                $" -serverProperties {_srvPropsFile["path"]}";

            if (_preLoad)
            {
                var annAux = _srvStartInfo["preload_annotators"];
                if (annAux != null && annAux.Type == JTokenType.String)
                    _procArgs += $" -preload {annAux}";
            }

            if (_args != null)
            {
                foreach (var arg in new string[] { "ssl", "strict" })
                {
                    if (_args.Contains(arg))
                        _procArgs += $" -{arg}";
                }
            }

            if (_kwargs != null)
            { 
                foreach (var kwarg in new string[] { "status_port", "uriContext", "key", "username", "password", "blacklist", "server_id" })
                {
                    if (_kwargs[kwarg] != null)                     
                        _procArgs += $" -{kwarg} {_kwargs[kwarg]}";
                }
            }

            Start();
        }

        private void SetupDefaultServerProps(object properties, object annotators, string outputFormat)
        {
            // Set up the default properties for the server from either:
            // 1. File path on system or in CLASSPATH(e.g. / path / to / server.props or StanfordCoreNLP - french.properties
            // 2. Stanford CoreNLP supported language(e.g.french)
            // 3. Python dictionary(properties written to tmp file for Java server, erased at end)
            // 4. Default(just use standard defaults set server side in Java code, with the exception that the default
            //    default outputFormat is changed to serialized)

            // If defaults are being set client side, values of annotators and output_format will overwrite the
            // client side properties.If the defaults are being set server side, those parameters will be ignored.

            // Info about the properties used to start the server is stored in _srvStartInfo
            // If a file is used, info about the file(path, whether tmp or not) is stored in _srvPropFiles

            var propsStr = properties as string;
            var propsJObj = properties as JObject;

            // Ensure properties is string or JObject
            if (properties == null || (propsStr == null && propsJObj == null))
            {
                if (properties != null)
                    Console.WriteLine("Warning: properties passed invalid value (not a str or dict), setting properties to {}");
                propsJObj = new JObject();
            }            
            
            if (propsStr != null)
            {                
                var lang = propsStr.GetLang();
                if (!string.IsNullOrEmpty(lang))
                {
                    // Translate Stanford CoreNLP language name to properties file if properties is a language name
                    if (lang == Pipeline.Lang.English)
                        _srvPropsFile["path"] = "StanfordCoreNLP.properties";
                    else
                        _srvPropsFile["path"] = $"StanfordCoreNLP-{lang}.properties";

                    _srvStartInfo["preload_annotators"] = lang.GetLangDefaultAnnotators();

                    Console.WriteLine(
                        $"Using Stanford CoreNLP default properties for: {lang}. " +
                        $"Make sure to have {lang} models jar (available for download here: https://stanfordnlp.github.io/CoreNLP/) in CLASSPATH");
                }
                else
                {
                    // Otherwise assume properties string is a path
                    _srvPropsFile["path"] = propsStr;
                    if (File.Exists(propsStr))
                    {
                        var propsFromFile = new JObject();
                        propsFromFile.ReadCoreNlpProps(propsStr);
                        _srvStartInfo["props"] = propsFromFile;
                        _srvStartInfo["preload_annotators"] = propsFromFile["annotators"];
                    }
                    else
                        Console.WriteLine($"Warning: {propsStr} does not correspond to a file path.");

                    // Write client side props to a tmp file which will be erased at end
                    Console.WriteLine($"Setting server defaults from: {_srvPropsFile["path"]}");
                    _srvStartInfo["props_file"] = _srvPropsFile["path"];
                    _srvStartInfo["server_side"] = true;

                    if (annotators != null)
                        Console.WriteLine($"Warning: Server defaults being set server side, ignoring annotators={annotators}");
                    if (outputFormat != null)
                        Console.WriteLine($"Warning: Server defaults being set server side, ignoring output_format={outputFormat}");
                }
            }
            else
            {
                // Check if client side should set default properties
                // Set up properties from client side
                // The Java Stanford CoreNLP server defaults to "json" for outputFormat
                // But by default servers started by Python interface will override this to return serialized object
                var clientSideProps = new JObject
                {
                    ["annotators"] = DefaultAnnotators,
                    ["outputFormat"] = DefaultOutputFormat,
                    ["serializer"] = DefaultSerializer
                };

                clientSideProps.Update(propsJObj);

                if (annotators != null)
                {                    
                    var annLst = annotators as string[];
                    var ann = annLst != null ? string.Join(",", annLst) : (string)annotators;
                    clientSideProps["annotators"] = ann;
                }

                if (!string.IsNullOrEmpty(outputFormat))
                    clientSideProps["outputFormat"] = outputFormat;

                _srvPropsFile["path"] = clientSideProps.WriteCoreNlpProps();
                _srvPropsFile["is_temp"] = true;

                _srvStartInfo["client_side"] = true;
                _srvStartInfo["props"] = clientSideProps;
                _srvStartInfo["props_file"] = _srvPropsFile["path"];
                _srvStartInfo["preload_annotators"] = clientSideProps["annotators"];
            }
        }

        private void RegisterPropertiesKey(string key, string props)
        {
            if (key.IsLang())
            {
                Console.WriteLine(
                    $"Key {key} not registered in properties cache.  Names of Stanford CoreNLP supported languages are " +    
                    $"reserved for Stanford CoreNLP defaults for that language.  For instance \"french\" or \"fr\" " +    
                    $"corresponds to using the defaults in StanfordCoreNLP-french.properties which are stored with the " +    
                    $"server.  If you want to store custom defaults for that language, it is suggested to use a key like " +    
                    $" \"fr-custom\", etc...");
            }
            else
                _propsCache[key] = props;
        }
    }
}