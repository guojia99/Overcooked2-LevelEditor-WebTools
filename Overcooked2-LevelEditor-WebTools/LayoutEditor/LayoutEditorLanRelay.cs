using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

/// <summary>
/// LAN 中继：Windows 上 HttpListener 走 HTTP.sys，绑定任何非回环前缀（* / + / 0.0.0.0）
/// 都需要 URLACL 管理员权限；主服务因此回退为仅 127.0.0.1 时，虚拟机/局域网无法访问。
/// TcpListener 不经过 HTTP.sys、无需 URLACL，这里用原始 TCP 字节转发把
/// 0.0.0.0:relayPort 的流量原样中继到 127.0.0.1:targetPort。
/// 仅在主服务通配绑定失败（仅回环）时由 LayoutEditorHttpServer 自动启动。
/// </summary>
public static class LayoutEditorLanRelay
{
    private static TcpListener _listener;
    private static Thread _acceptThread;
    private static volatile bool _running;
    private static int _relayPort;

    public static bool IsRunning
    {
        get { return _running; }
    }

    public static int RelayPort
    {
        get { return _relayPort; }
    }

    /// <summary>在 0.0.0.0:relayPort 监听并把每个连接中继到 127.0.0.1:targetPort。</summary>
    public static void Start(int relayPort, int targetPort)
    {
        if (_running)
            return;
        try
        {
            _listener = new TcpListener(IPAddress.Any, relayPort);
            _listener.Start();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Layout Editor: LAN 中继绑定端口 " + relayPort + " 失败：" + ex.Message);
            _listener = null;
            return;
        }
        _relayPort = relayPort;
        _running = true;
        _acceptThread = new Thread(delegate() { AcceptLoop(targetPort); });
        _acceptThread.IsBackground = true;
        _acceptThread.Start();
        Debug.Log("Layout Editor: LAN 中继已启动 0.0.0.0:" + relayPort + " -> 127.0.0.1:" + targetPort
            + "（虚拟机/局域网请访问 http://<本机IP>:" + relayPort + "/，并放行防火墙 TCP " + relayPort + "）");
    }

    public static void Stop()
    {
        if (!_running)
            return;
        _running = false;
        try
        {
            if (_listener != null)
                _listener.Stop();
        }
        catch { }
        _listener = null;
    }

    private static void AcceptLoop(int targetPort)
    {
        while (_running)
        {
            TcpClient inbound;
            try
            {
                inbound = _listener.AcceptTcpClient();
            }
            catch
            {
                break; // listener 已停止
            }
            Thread t = new Thread(delegate() { RelayConnection(inbound, targetPort); });
            t.IsBackground = true;
            t.Start();
        }
    }

    private static void RelayConnection(TcpClient inbound, int targetPort)
    {
        TcpClient outbound = null;
        try
        {
            outbound = new TcpClient();
            outbound.NoDelay = true;
            inbound.NoDelay = true;
            outbound.Connect(IPAddress.Loopback, targetPort);
        }
        catch
        {
            try { inbound.Close(); } catch { }
            if (outbound != null) { try { outbound.Close(); } catch { } }
            return;
        }

        TcpClient a = inbound;
        TcpClient b = outbound;
        Thread up = new Thread(delegate() { Pump(a, b); });
        up.IsBackground = true;
        up.Start();
        Pump(b, a);
        try { a.Close(); } catch { }
        try { b.Close(); } catch { }
    }

    /// <summary>单向字节搬运；对端读完后半关本方向写端，由调用方统一关闭连接。</summary>
    private static void Pump(TcpClient from, TcpClient to)
    {
        try
        {
            NetworkStream src = from.GetStream();
            NetworkStream dst = to.GetStream();
            byte[] buf = new byte[16384];
            int n;
            while ((n = src.Read(buf, 0, buf.Length)) > 0)
                dst.Write(buf, 0, n);
            try { to.Client.Shutdown(SocketShutdown.Send); } catch { }
        }
        catch { }
    }
}
