using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using csscript;
using CSScripting.CodeDom;
using CSScriptLib;

namespace CSScripting
{
    /// <summary>
    /// The configuration and methods of the global context.
    /// </summary>
    public partial class Globals
    {
        static internal string DynamicWrapperClassName = "DynamicClass";
        static internal string RootClassName = "css_root";
        // Roslyn still does not support anything else but `Submission#0` (17 Jul 2019) [update]
        // Roslyn now does support alternative class names (1 Jan 2020)

        static internal void StartBuildServer(bool report = false)
        {
            if (Globals.BuildServerIsDeployed)
                "dotnet".RunAsync($"\"{Globals.build_server}\" -start -csc:\"{Globals.csc}\"");

            if (report)
                PrintBuildServerInfo();
        }

        static internal void RestartBuildServer(bool report = false)
        {
            StopBuildServer();
            StartBuildServer(report);
        }

        static internal void ResetBuildServer(bool report = false)
        {
            StopBuildServer();
            while (BuildServer.IsServerAlive(null))
                Thread.Sleep(500);
            RemoveBuildServer();
            DeployBuildServer();
            StartBuildServer(report);
        }

        static internal void PrintBuildServerInfo()
        {
            if (Globals.BuildServerIsDeployed)
            {    // CSScriptLib.CoreExtensions.RunAsync(
                var alive = BuildServer.IsServerAlive(null);
                Console.WriteLine($"Build server: {Globals.build_server.GetFullPath()}");

                var pingResponse = BuildServer.PingRemoteInstance(null).Split('\n');
                var pid = alive ?
                    $" ({pingResponse.FirstOrDefault()})"
                    : "";
                Console.WriteLine($"Build server compiler: {(alive ? pingResponse[2] : "")}");
                Console.WriteLine($"Build server is {(alive ? "" : "not ")}running{pid}.");
            }
            else
            {
                Console.WriteLine("Build server is not deployed.");
                Console.WriteLine($"Expected deployment: {Globals.build_server.GetFullPath()}");
            }
        }

        static internal void Install(bool installRequest)
        {
            if (Globals.BuildServerIsDeployed)
            {    // CSScriptLib.CoreExtensions.RunAsync(
                Console.WriteLine($"Build server: {Globals.build_server.GetFullPath()}");
                Console.WriteLine($"Build server compiler: {Globals.csc.GetFullPath()}");
                Console.WriteLine($"Build server is {(BuildServer.IsServerAlive(null) ? "" : "not ")}running.");
            }
            else
            {
                Console.WriteLine("Build server is not deployed.");
                Console.WriteLine($"Expected deployment: {Globals.build_server.GetFullPath()}");
            }
        }

        static internal void StopBuildServer()
        {
            if (Globals.BuildServerIsDeployed)
                "dotnet".RunAsync($"\"{Globals.build_server}\" -stop");
        }

        static internal string build_server
        {
            get
            {
                var path = Environment.SpecialFolder.CommonApplicationData.GetPath().PathJoin("cs-script",
                                                                                     "bin",
                                                                                     "compiler",
                                                                                     Assembly.GetExecutingAssembly().GetName().Version,
                                                                                     "build.dll");
                if (Runtime.IsLinux)
                {
                    path = path.Replace("/usr/share/cs-script", "/usr/local/share/cs-script");
                }
                return path;
            }
        }

        /// <summary>
        /// Removes the build server from the target system.
        /// </summary>
        /// <returns><c>true</c> if success; otherwise <c>false</c></returns>
        static public bool RemoveBuildServer()
        {
            try
            {
                File.Delete(build_server);
                File.Delete(build_server.ChangeExtension(".deps.json"));
                File.Delete(build_server.ChangeExtension(".runtimeconfig.json"));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return !File.Exists(build_server);
        }

        /// <summary>
        /// Deploys the build server on the target system.
        /// </summary>
        static public bool DeployBuildServer()
        {
            try
            {
                Directory.CreateDirectory(build_server.GetDirName());

                File.WriteAllBytes(build_server, Resources.build);
                File.WriteAllBytes(build_server.ChangeExtension(".deps.json"), Resources.build_deps);
                File.WriteAllBytes(build_server.ChangeExtension(".runtimeconfig.json"), Resources.build_runtimeconfig);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return File.Exists(build_server);
        }

        /// <summary>
        /// Pings the running instance of the build server.
        /// </summary>
        static public void Ping()
        {
            Console.WriteLine(BuildServer.PingRemoteInstance(null));
        }

        // static internal bool IsRemoteInstanceRunning() { try { using (var clientSocket = new
        // TcpClient()) { return clientSocket .ConnectAsync(IPAddress.Loopback, port ?? serverPort)
        // .Wait(TimeSpan.FromMilliseconds(20)); } } catch { return false; } }

        /// <summary>
        /// Gets a value indicating whether build server is deployed.
        /// </summary>
        /// <value><c>true</c> if build server is deployed; otherwise, <c>false</c>.</value>
        static public bool BuildServerIsDeployed
        {
            get
            {
#if !DEBUG
                if (!build_server.FileExists())
#endif
                try
                {
                    Directory.CreateDirectory(build_server.GetDirName());

                    File.WriteAllBytes(build_server, Resources.build);
                    File.WriteAllBytes(build_server.ChangeExtension(".deps.json"), Resources.build_deps);
                    File.WriteAllBytes(build_server.ChangeExtension(".runtimeconfig.json"), Resources.build_runtimeconfig);
                }
                catch { }

                return build_server.FileExists();
            }
        }

        static string csc_file = Environment.GetEnvironmentVariable("css_csc_file");

        static internal string LibDir => Assembly.GetExecutingAssembly().Location.GetDirName().PathJoin("lib");

        /// <summary>
        /// Gets the path to the assembly implementing Roslyn compiler.
        /// </summary>
        static public string roslyn => typeof(Microsoft.CodeAnalysis.CSharp.Scripting.CSharpScript).Assembly.Location;

        /// <summary>
        /// Gets the path to the dotnet executable.
        /// </summary>
        /// <value>The dotnet executable path.</value>
        static public string dotnet
        {
            get
            {
                var dotnetExeName = Runtime.IsLinux ? "dotnet" : "dotnet.exe";

                var file = "".GetType().Assembly.Location
                    .Split(Path.DirectorySeparatorChar)
                    .TakeWhile(x => x != "dotnet")
                    .JoinBy(Path.DirectorySeparatorChar.ToString())
                    .PathJoin("dotnet", dotnetExeName);

                return File.Exists(file) ? file : dotnetExeName;
            }
        }

        static internal string GetCompilerFor(string file)
            => file.GetExtension().SameAs(".cs") ? csc : csc.ChangeFileName("vbc.dll");

        static internal string CheckAndGenerateSdkWarning()
        {
            if (!csc.FileExists())
            {
                return $"WARNING: .NET {Environment.Version.Major} SDK cannot be found. It's required for `csc` and `dotnet` compiler engines.";
            }
            return null;
        }

        /// <summary>
        /// Gets or sets the path to the C# compiler executable (e.g. csc.exe or csc.dll)
        /// </summary>
        /// <value>The CSC.</value>
        static public string csc
        {
            set
            {
                csc_file = value;
            }

            get
            {
                if (csc_file == null)
                {
#if class_lib
                    if (!Runtime.IsCore)
                    {
                        csc_file = Path.Combine(Path.GetDirectoryName("".GetType().Assembly.Location), "csc.exe");
                    }
                    else
#endif
                    {
                        // Win: C:\Program Files\dotnet\sdk\6.0.100-rc.2.21505.57\Roslyn\bincore\csc.dll
                        //      C:\Program Files (x86)\dotnet\sdk\5.0.402\Roslyn\bincore\csc.dll
                        // Linux: ~dotnet/.../3.0.100-preview5-011568/Roslyn/... (cannot find SDK in preview)

                        // win: program_files/dotnet/sdk/<version>/Roslyn/csc.exe
                        var dotnet_root = "".GetType().Assembly.Location;

                        // find first "dotnet" parent dir by trimming till the last "dotnet" token
                        dotnet_root = dotnet_root.Split(Path.DirectorySeparatorChar)
                                                 .Reverse()
                                                 .SkipWhile(x => x != "dotnet")
                                                 .Reverse()
                                                 .JoinBy(Path.DirectorySeparatorChar.ToString());

                        if (dotnet_root.PathJoin("sdk").DirExists()) // need to check as otherwise it will throw
                        {
                            var dirs = dotnet_root.PathJoin("sdk")
                                                  .PathGetDirs($"{Environment.Version.Major}*")
                                                  .Where(dir => char.IsDigit(dir.GetFileName()[0]))
                                                  .OrderBy(x => System.Version.Parse(x.GetFileName().Split('-').First()))
                                                  .SelectMany(dir => dir.PathGetDirs("Roslyn"))
                                                  .ToArray();

                            csc_file = dirs.Select(dir => dir.PathJoin("bincore", "csc.dll"))
                                                   .LastOrDefault(File.Exists);
                        }
                    }
                }
                return csc_file;
            }
        }
    }
}