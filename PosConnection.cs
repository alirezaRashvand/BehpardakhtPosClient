using System.IO.Ports;
using BehpardakhtPosClient.Protocol;

namespace BehpardakhtPosClient;

/// <summary>
/// Where the terminal is and how frames are wrapped.
/// Property names match POS-PC library guide 1.4.47.
/// </summary>
public class PosConnection
{
    public string CommunicationType { get; set; } = "tcp/ip";

    public string POS_IP { get; set; } = "127.0.0.1";

    public int POS_PORTtcp { get; set; } = 1024;

    public int POSPC_TCPCOMMU_SocketRecTimeout { get; set; } = 180_000;

    public string PC_PORT_Name { get; set; } = "COM1";

    public int PC_PORT_BaudRate { get; set; } = 115_200;

    public int PC_PORT_DataBits { get; set; } = 8;

    public Parity PC_PORT_Parity { get; set; } = Parity.None;

    public StopBits PC_PORT_StopBits { get; set; } = StopBits.One;

    public int PC_PORT_ReadTimeout { get; set; } = 180_000;

    /// <summary>
    /// Protocol 4.7 is what current Mellat terminals speak.
    /// <see cref="PosFraming.LegacyLibrary"/> matches POS_PC.dll 1.4.47 on this terminal.
    /// </summary>
    public PosFraming Framing { get; set; } = PosFraming.Protocol47;

    /// <summary>
    /// POS_PC.dll 1.4.47 prepends its type and version on every framed request.
    /// </summary>
    public bool IncludeLibraryHeader { get; set; } = true;

    public string Host
    {
        get => POS_IP;
        set => POS_IP = value;
    }

    public int Port
    {
        get => POS_PORTtcp;
        set => POS_PORTtcp = value;
    }

    public bool IsTcp =>
        CommunicationType.Equals("tcp/ip", StringComparison.OrdinalIgnoreCase)
        || CommunicationType.Equals("tcp", StringComparison.OrdinalIgnoreCase);

    public bool IsSerial => CommunicationType.Equals("serial", StringComparison.OrdinalIgnoreCase);

    public TimeSpan ReceiveTimeout
    {
        get => TimeSpan.FromMilliseconds(IsSerial ? PC_PORT_ReadTimeout : POSPC_TCPCOMMU_SocketRecTimeout);
        set
        {
            var milliseconds = ToTimeoutMilliseconds(value);
            POSPC_TCPCOMMU_SocketRecTimeout = milliseconds;
            PC_PORT_ReadTimeout = milliseconds;
        }
    }

    public static PosConnection Tcp(string host, int port, TimeSpan? timeout = null, PosFraming framing = PosFraming.Protocol47)
    {
        var connection = new PosConnection
        {
            CommunicationType = "tcp/ip",
            POS_IP = host,
            POS_PORTtcp = port,
            Framing = framing
        };
        if (timeout is not null)
        {
            connection.ReceiveTimeout = timeout.Value;
        }

        return connection;
    }

    public static PosConnection Serial(string portName, int baudRate = 115_200, TimeSpan? timeout = null, PosFraming framing = PosFraming.Protocol47)
    {
        var connection = new PosConnection
        {
            CommunicationType = "serial",
            PC_PORT_Name = portName,
            PC_PORT_BaudRate = baudRate,
            Framing = framing
        };
        if (timeout is not null)
        {
            connection.ReceiveTimeout = timeout.Value;
        }

        return connection;
    }

    internal static int ToTimeoutMilliseconds(TimeSpan timeout)
    {
        var milliseconds = (int)timeout.TotalMilliseconds;
        return milliseconds;
    }
}
