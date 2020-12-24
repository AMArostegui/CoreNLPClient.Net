namespace CoreNLPClientDotNet
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;

    public class ShouldRetryException : Exception
    {
    }

    public class PermanentlyFailedException : Exception
    {
        public PermanentlyFailedException(string msg) : base(msg)
        {
        }
    }

    public class RobustService
    {
        public const int CheckAliveTimeout = 120000;

        protected string _endPoint;

        protected string _procName;
        protected string _procArgs;

        protected string _host;
        protected int _port;

        protected bool _beQuiet;

        protected bool _ignoreBindingError;
        private Process _srvProc;
        private bool _isActive;        

        public RobustService()
        {
            _isActive = false;
            _beQuiet = false;
            _ignoreBindingError = false;
        }

        public bool IsAlive()
        {
            if (!_ignoreBindingError && _srvProc != null && _srvProc.HasExited)                
                return false;

            var req = (HttpWebRequest)WebRequest.Create(_endPoint + "/ping");
            req.Method = "GET";
            req.Accept = "*";

            try
            {
                var response = (HttpWebResponse)req.GetResponse();
                return response.StatusCode == HttpStatusCode.OK;
            }
            catch
            {
                throw new ShouldRetryException();
            }
        }

        public void EnsureAlive()
        {
            if (_isActive)
            {
                try
                {
                    if (IsAlive())
                        return;
                    else
                        Stop();
                }
                catch (ShouldRetryException)
                {
                }
            }

            if (_srvProc == null)
                Start();

            var startTime = Stopwatch.StartNew();
            while (true)
            {
                try
                {
                    if (IsAlive())
                        break;
                }
                catch (ShouldRetryException)
                {
                }

                if (startTime.ElapsedMilliseconds < CheckAliveTimeout)
                    Thread.Sleep(1000);
                else
                    throw new PermanentlyFailedException("Timed out waiting for service to come alive.");
            }

            _isActive = true;
        }

        public void Start()
        {
            if (string.IsNullOrEmpty(_procName) || string.IsNullOrEmpty(_procArgs))
                return;            
            
            try
            {
                var ipAddress = Dns.GetHostEntry(_host).AddressList[0];
                var tcpListener = new TcpListener(ipAddress, _port);
                tcpListener.Start();
                tcpListener.Stop();
            }
            catch (SocketException)
            {
                if (_ignoreBindingError)
                {
                    _srvProc = null;
                    return;
                }

                throw new PermanentlyFailedException($"Error: unable to start the CoreNLP server on port {_port} (possibly something is already running there)");
            }

            _srvProc = new Process();
            _srvProc.StartInfo.FileName = _procName;
            _srvProc.StartInfo.Arguments = _procArgs;
            _srvProc.StartInfo.RedirectStandardError = _beQuiet;
            _srvProc.Start();
        }

        public void Stop()
        {
            if (_srvProc == null)
                return;

            _srvProc.CloseMainWindow();
            if (!_srvProc.WaitForExit(5000))
                _srvProc.Kill();

            _isActive = false;
            _srvProc = null;
        }
    }
}
