using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Services.Transport.Transports;
using UnityEditor;
using UnityEngine;

// 실제 포트를 모두 점유해 시작 실패와 복구를 시험한다. 게임 씬이나 에셋은 바꾸지 않는다.
public static class EditorBridgeRecoveryValidation
{
    private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
    private static readonly List<string> checks = new List<string>();

    [MenuItem("Tools/Playtest/Validate Editor Bridge Recovery")]
    public static void Begin()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Play와 컴파일이 끝난 뒤 실행하세요.");
        // 메뉴 호출의 응답이 나간 뒤 브리지를 잠시 끊고, finally에서 다시 시작한다.
        EditorApplication.delayCall += Run;
    }

    private static FieldInfo Field(string name) => typeof(StdioBridgeHost).GetField(name, PrivateStatic)
        ?? throw new MissingFieldException(typeof(StdioBridgeHost).FullName, name);

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        checks.Add("PASS " + message);
    }

    private static void Run()
    {
        checks.Clear();
        var blockers = new List<Socket>();
        bool passed = false;
        try
        {
            Check(StdioBridgeHost.IsRunning, "original editor bridge is running before fixture");
            int configured = PortManager.GetPortWithFallback();
            StdioBridgeHost.Stop();
            var ports = new HashSet<int> { configured };
            for (int port = 6400; port < 6500; port++) ports.Add(port);
            foreach (int port in ports)
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    socket.Bind(new IPEndPoint(IPAddress.Loopback, port));
                    socket.Listen(1);
                    blockers.Add(socket);
                }
                catch (SocketException exception) when (exception.SocketErrorCode == SocketError.AddressAlreadyInUse)
                {
                    socket.Dispose(); // 다른 프로세스가 이미 점유한 포트도 실패 조건을 만족한다.
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            }
            Check(!PortManager.IsPortAvailable(configured), "configured port is occupied by a real listener");
            int descriptors = Directory.Exists("/dev/fd") ? Directory.GetFiles("/dev/fd").Length : -1;
            for (int i = 0; i < 1000; i++)
                CheckProbe(configured);
            if (descriptors >= 0)
                Check(Directory.GetFiles("/dev/fd").Length <= descriptors + 4,
                    "1000 failed port probes do not accumulate file descriptors");

            // 3초 대기를 실제로 반복하지 않고 충분히 기다린 상태에서 대체 포트 탐색을 시험한다.
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Field("_portBusySince").SetValue(null, EditorApplication.timeSinceStartup - 4.0);
                StdioBridgeHost.Start();
                Check(!StdioBridgeHost.IsRunning, "exhausted range stays disconnected without throwing, attempt " + (attempt + 1));
                Check(Field("listener").GetValue(null) == null,
                    "failed listener is released, attempt " + (attempt + 1));
            }
            double deadline = (double)Field("nextStartAt").GetValue(null);
            Check(deadline - EditorApplication.timeSinceStartup > 25.0,
                "repeated startup failure backs off to about 30 seconds");
            int failures = (int)Field("startFailureCount").GetValue(null);
            typeof(StdioBridgeHost).GetMethod("InitializeAfterCompilation", PrivateStatic).Invoke(null, null);
            Check((int)Field("startFailureCount").GetValue(null) == failures,
                "delayCall startup does not bypass the backoff");
            typeof(StdioBridgeHost).GetMethod("ScheduleInitRetry", PrivateStatic).Invoke(null, null);
            Check((double)Field("nextStartAt").GetValue(null) >= deadline,
                "play-mode scheduling does not shorten the existing backoff");

            // 시작 도중 실패한 리스너도 Stop으로 정리하는지 확인한다.
            var partial = new TcpListener(IPAddress.Loopback, 0);
            partial.Start();
            int partialPort = ((IPEndPoint)partial.LocalEndpoint).Port;
            Field("listener").SetValue(null, partial);
            StdioBridgeHost.Stop();
            Check(Field("listener").GetValue(null) == null && PortManager.IsPortAvailable(partialPort),
                "Stop releases partially initialized listeners even when isRunning is false");
            passed = true;
        }
        catch (Exception exception)
        {
            checks.Add("FAIL " + exception);
        }
        finally
        {
            foreach (Socket socket in blockers) socket.Dispose();
            try
            {
                StdioBridgeHost.Start();
                Check(StdioBridgeHost.IsRunning, "bridge reconnects after ports are released");
                Check((int)Field("startFailureCount").GetValue(null) == 0
                    && (double)Field("nextStartAt").GetValue(null) == 0.0,
                    "successful startup resets failure count and deadline");
            }
            catch (Exception exception)
            {
                passed = false;
                checks.Add("FAIL restart " + exception);
            }
            string report = "EDITOR_BRIDGE_RECOVERY_" + (passed ? "PASS" : "FAIL") + "\n"
                + DateTime.Now.ToString("O") + " Unity " + Application.unityVersion + "\n"
                + "Real TCP port exhaustion/restart fixture. Not a guarantee against all OS/editor crashes.\n"
                + string.Join("\n", checks);
            Directory.CreateDirectory("docs/verification");
            File.WriteAllText("docs/verification/editor-bridge-recovery-2026-10-16.txt", report);
            if (passed) Debug.Log(report); else Debug.LogError(report);
        }
    }

    private static void CheckProbe(int port)
    {
        if (PortManager.IsPortAvailable(port))
            throw new InvalidOperationException("An occupied listener was reported as available.");
    }
}
