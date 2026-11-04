// FluentWeather network relay.
//
// Setup compiles this file on your PC with the C# compiler that ships with
// Windows (.NET Framework 4.x). The result is FluentWeatherNet.exe in
// C:\Program Files\FluentWeatherLocal.
//
// Purpose: your firewall can allow this one program, instead of git and MSBuild.
// The update script starts it, points git and NuGet (inside MSBuild) at it, and
// stops it when the update ends. git and MSBuild then only connect to this relay
// on your own PC (127.0.0.1). The relay makes the outside connections.
//
// Limits enforced here:
//   - Listens on 127.0.0.1 only. Other computers cannot use it.
//   - Accepts only HTTPS tunnels (the CONNECT method) to port 443.
//   - Connects only to host names listed in allowed-hosts.txt (exact match).
//     Anything else is refused and written to the log as BLOCKED.
//   - Exits when the update script exits, or after 3 hours at most.
//   - Does not decrypt or change any traffic.
//
// Usage (the update script does this):
//   FluentWeatherNet.exe <allowed-hosts file> <port file> <log file> <parent process id>
//
// Language level: C# 5, so the compiler built into Windows can build it.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

[assembly: System.Reflection.AssemblyTitle("FluentWeather network relay")]
[assembly: System.Reflection.AssemblyDescription("Local HTTPS relay for FluentWeather updates. Allows only hosts in allowed-hosts.txt.")]
[assembly: System.Reflection.AssemblyProduct("FluentWeather Local")]
[assembly: System.Reflection.AssemblyCompany("Built on this PC by FluentWeatherLocal Setup")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.0.0")]

namespace FluentWeatherLocal
{
    internal static class Relay
    {
        private const int MaxHeaderBytes = 8192;
        private static readonly TimeSpan MaxLifetime = TimeSpan.FromHours(3);

        private static HashSet<string> _allowedHosts;
        private static string _logPath;
        private static readonly object LogLock = new object();

        private static int Main(string[] args)
        {
            if (args.Length != 4)
            {
                Console.Error.WriteLine("Usage: FluentWeatherNet.exe <allowed-hosts file> <port file> <log file> <parent pid>");
                return 2;
            }

            _logPath = args[2];
            _allowedHosts = LoadAllowedHosts(args[0]);
            if (_allowedHosts.Count == 0)
            {
                Log("No allowed hosts. Exiting.");
                return 1;
            }

            int parentPid;
            if (!int.TryParse(args[3], out parentPid))
            {
                Log("Invalid parent process id. Exiting.");
                return 2;
            }

            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            File.WriteAllText(args[1], port.ToString());
            Log("Listening on 127.0.0.1:" + port + ". Allowed hosts: " + string.Join(", ", _allowedHosts));

            var watcher = new Thread(delegate () { WatchParent(parentPid, listener); });
            watcher.IsBackground = true;
            watcher.Start();

            while (true)
            {
                TcpClient client;
                try
                {
                    client = listener.AcceptTcpClient();
                }
                catch (SocketException)
                {
                    break; // listener stopped
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                var worker = new Thread(delegate () { HandleClient(client); });
                worker.IsBackground = true;
                worker.Start();
            }

            Log("Stopped.");
            return 0;
        }

        private static HashSet<string> LoadAllowedHosts(string path)
        {
            var hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var raw in File.ReadAllLines(path))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#"))
                        continue;
                    hosts.Add(line.TrimEnd('.'));
                }
            }
            catch (Exception ex)
            {
                Log("Could not read allowed hosts: " + ex.Message);
            }
            return hosts;
        }

        private static void WatchParent(int parentPid, TcpListener listener)
        {
            var started = DateTime.UtcNow;
            while (true)
            {
                Thread.Sleep(2000);
                bool parentGone;
                try
                {
                    var parent = Process.GetProcessById(parentPid);
                    parentGone = parent.HasExited;
                }
                catch (ArgumentException)
                {
                    parentGone = true; // no process with that id
                }
                catch (InvalidOperationException)
                {
                    parentGone = true;
                }

                if (parentGone || DateTime.UtcNow - started > MaxLifetime)
                {
                    Log(parentGone ? "Update script ended." : "Maximum run time reached.");
                    listener.Stop();
                    Environment.Exit(0);
                }
            }
        }

        private static void HandleClient(TcpClient client)
        {
            TcpClient upstream = null;
            try
            {
                client.NoDelay = true;
                var clientStream = client.GetStream();
                clientStream.ReadTimeout = 30000;

                byte[] extra;
                var header = ReadHeader(clientStream, out extra);
                if (header == null)
                    return;

                var firstLine = header.Split(new[] { "\r\n" }, StringSplitOptions.None)[0];
                var parts = firstLine.Split(' ');
                if (parts.Length != 3 || !string.Equals(parts[0], "CONNECT", StringComparison.Ordinal))
                {
                    Log("REFUSED non-tunnel request: " + Truncate(firstLine, 120));
                    Reply(clientStream, "405 Method Not Allowed");
                    return;
                }

                string host;
                int port;
                if (!TryParseHostPort(parts[1], out host, out port))
                {
                    Log("REFUSED malformed target: " + Truncate(parts[1], 120));
                    Reply(clientStream, "400 Bad Request");
                    return;
                }

                if (port != 443 || !_allowedHosts.Contains(host))
                {
                    Log("BLOCKED " + host + ":" + port + " (not in allowed-hosts.txt)");
                    Reply(clientStream, "403 Forbidden");
                    return;
                }

                upstream = new TcpClient();
                upstream.NoDelay = true;
                upstream.Connect(host, port);
                var upstreamStream = upstream.GetStream();

                Reply(clientStream, "200 Connection Established");
                Log("ALLOWED " + host + ":" + port);

                if (extra.Length > 0)
                    upstreamStream.Write(extra, 0, extra.Length);

                clientStream.ReadTimeout = Timeout.Infinite;
                Pipe(clientStream, upstreamStream);
            }
            catch (Exception ex)
            {
                Log("Connection error: " + ex.Message);
            }
            finally
            {
                if (upstream != null)
                    upstream.Close();
                client.Close();
            }
        }

        // Reads bytes up to the end of the HTTP request header (blank line).
        // Bytes after the header, if any, are returned in "extra".
        private static string ReadHeader(NetworkStream stream, out byte[] extra)
        {
            extra = new byte[0];
            var buffer = new byte[MaxHeaderBytes];
            var total = 0;
            while (total < buffer.Length)
            {
                var read = stream.Read(buffer, total, buffer.Length - total);
                if (read <= 0)
                    return null;
                total += read;

                var end = FindHeaderEnd(buffer, total);
                if (end >= 0)
                {
                    var extraLength = total - end;
                    if (extraLength > 0)
                    {
                        extra = new byte[extraLength];
                        Buffer.BlockCopy(buffer, end, extra, 0, extraLength);
                    }
                    return Encoding.ASCII.GetString(buffer, 0, end);
                }
            }
            return null; // header too large
        }

        private static int FindHeaderEnd(byte[] buffer, int length)
        {
            for (var i = 3; i < length; i++)
            {
                if (buffer[i - 3] == '\r' && buffer[i - 2] == '\n' && buffer[i - 1] == '\r' && buffer[i] == '\n')
                    return i + 1;
            }
            return -1;
        }

        private static bool TryParseHostPort(string target, out string host, out int port)
        {
            host = null;
            port = 0;
            var colon = target.LastIndexOf(':');
            if (colon <= 0 || colon == target.Length - 1)
                return false;
            if (!int.TryParse(target.Substring(colon + 1), out port) || port < 1 || port > 65535)
                return false;

            host = target.Substring(0, colon).Trim().TrimEnd('.');
            // Host names only. IP addresses and bracketed IPv6 literals are refused,
            // because the allow list is written as names.
            if (host.Length == 0 || host.StartsWith("[") || Uri.CheckHostName(host) != UriHostNameType.Dns)
                return false;
            return true;
        }

        private static void Reply(NetworkStream stream, string status)
        {
            var bytes = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + "\r\nContent-Length: 0\r\n\r\n");
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush();
        }

        // Copies data in both directions until either side closes.
        private static void Pipe(NetworkStream a, NetworkStream b)
        {
            var done = new ManualResetEvent(false);
            ThreadStart aToB = delegate { CopyQuietly(a, b); done.Set(); };
            ThreadStart bToA = delegate { CopyQuietly(b, a); done.Set(); };
            var t1 = new Thread(aToB);
            var t2 = new Thread(bToA);
            t1.IsBackground = true;
            t2.IsBackground = true;
            t1.Start();
            t2.Start();
            done.WaitOne();
        }

        private static void CopyQuietly(Stream from, Stream to)
        {
            var buffer = new byte[65536];
            try
            {
                int read;
                while ((read = from.Read(buffer, 0, buffer.Length)) > 0)
                {
                    to.Write(buffer, 0, read);
                }
            }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
        }

        private static string Truncate(string text, int max)
        {
            return text.Length <= max ? text : text.Substring(0, max) + "...";
        }

        private static void Log(string message)
        {
            try
            {
                lock (LogLock)
                {
                    File.AppendAllText(_logPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine);
                }
            }
            catch (Exception)
            {
                // Logging must never stop the relay.
            }
        }
    }
}
