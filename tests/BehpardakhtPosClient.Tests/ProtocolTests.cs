using System.Net;
using System.Net.Sockets;
using System.Text;
using BehpardakhtPosClient.Protocol;
using Xunit;

namespace BehpardakhtPosClient.Tests;

public class ProtocolTests
{
    [Fact]
    public void KnownBodies_MatchPublishedCrcs()
    {
        var purchase = Convert.FromHexString("b0021250b1020108b906313233343536a20101a40435323633");
        Assert.Equal("ACFD", Convert.ToHexString(Crc16.LittleEndian(purchase)));

        var inquiryWithAmount = Convert.FromHexString("B003010000B1020108A20101BA0433303030A403323030");
        Assert.Equal("9BE5", Convert.ToHexString(Crc16.LittleEndian(inquiryWithAmount)));
    }

    [Fact]
    public void InquiryFrame_UsesProtocol47Layout()
    {
        var frame = RequestEncoder.Encode(new WireRequest
        {
            ServiceCode = "16",
            RequestId = "3000",
            PcId = "200"
        }, PosFraming.Protocol47);

        var decoded = PosFrame.Decode(frame, PosFraming.Protocol47);
        Assert.Equal(0, decoded.ErrorCode);
        Assert.Equal("16", decoded.Message!.Bcd(PosTags.Service));
        Assert.Equal("3000", decoded.Message.Ascii(PosTags.RequestId));
        Assert.Equal("200", decoded.Message.Ascii(PosTags.PcId));
    }

    [Fact]
    public void LegacyFrame_LengthCountsTheWholeMessage()
    {
        var frame = RequestEncoder.Encode(new WireRequest
        {
            ServiceCode = "01",
            Amount = "1250",
            PcId = "1"
        }, PosFraming.LegacyLibrary);

        var declared = int.Parse(Encoding.ASCII.GetString(frame, 0, 4));
        Assert.Equal(frame.Length, declared);
        var decoded = PosFrame.Decode(frame, PosFraming.LegacyLibrary);
        Assert.Equal(0, decoded.ErrorCode);
        Assert.Equal(".NET", decoded.Message!.Ascii(PosTags.LibraryType));
        Assert.Equal("1250", decoded.Message.Bcd(PosTags.Amount));
    }

    [Fact]
    public void CorruptCrc_IsRejected()
    {
        var frame = RequestEncoder.Encode(new WireRequest
        {
            ServiceCode = "01",
            Amount = "1250",
            PcId = "1"
        }, PosFraming.Protocol47);
        frame[^1] ^= 0xFF;

        var decoded = PosFrame.Decode(frame, PosFraming.Protocol47);
        Assert.Equal((int)ReturnCode.ERR_POS_PC_CRCERROR_INVALID_DATA, decoded.ErrorCode);
    }

    [Fact]
    public void IranSystem_MapsDigits()
    {
        Assert.Equal(new byte[] { 0x81, 0x82, 0x83 }, IranSystem.Encode("123"));
    }

    [Fact]
    public void LiveReadyFrame_IsLegacyLengthPlusOk()
    {
        var frame = Convert.FromHexString("30303236C0024F4BA20103C70407989152C805536944773786B5");
        Assert.True(PosFrame.TryMatch(frame, out var framing, out var length));
        Assert.Equal(PosFraming.LegacyLibrary, framing);
        Assert.Equal(frame.Length, length);

        var decoded = PosFrame.Decode(frame, framing);
        Assert.Equal(0, decoded.ErrorCode);
        Assert.Equal("OK", decoded.Message!.Ascii(PosTags.ResponseCode));
        Assert.Equal("7989152", decoded.Message.Bcd(PosTags.Terminal));
        Assert.Equal("5369447737", decoded.Message.Bcd(PosTags.Account));
    }
    [Fact]
    public void BillAmount_FollowsTheLibraryRule()
    {
        Assert.Equal("15000", PosClient.BillAmount("12345678901234", "1500012345"));
    }
}

public class LoopbackTests
{
    [Fact]
    public async Task Purchase_RoundTripsOverTcp()
    {
        await using var terminal = await FakeTerminal.Start();
        using var client = new PosClient(PosConnection.Tcp(terminal.Host, terminal.Port, TimeSpan.FromSeconds(5)));

        var result = await client.PurchaseAsync(1250, requestId: "42", pcId: "5263", payerId: "108", additionalData: "123456");

        Assert.True(result.IsApproved, result.ToString());
        Assert.Equal("171", result.SerialTransaction);
        Assert.Equal("127879949527", result.TraceNumber);
        Assert.Equal("21009268", result.TerminalNo);
        Assert.Equal("12404580", result.AccountNo);
        Assert.Equal("629827***4869", result.PAN);
        Assert.Equal("1396/01/29", result.TransactionDate);
        Assert.Equal("14:24", result.TransactionTime);
        Assert.Equal("1250", result.Amount);
        Assert.Equal("500", result.DiscountAmount);
        Assert.Equal("42", result.RequestId);

        var request = PosFrame.Decode(terminal.LastRequest, PosFraming.Protocol47);
        Assert.Equal("1250", request.Message!.Bcd(PosTags.Amount));
        Assert.Equal("108", request.Message.Bcd(PosTags.PayerId));
        Assert.Equal("5263", request.Message.Ascii(PosTags.PcId));
        Assert.Equal("42", request.Message.Ascii(PosTags.RequestId));
        Assert.Equal("123456", Encoding.ASCII.GetString(request.Message.Get(PosTags.AdditionalData)!));
        Assert.DoesNotContain(request.Message.Fields, field => field.Tag == PosTags.AutoSettle);
    }

    [Fact]
    public async Task UnsettledPurchase_SendsAutoSettleTag()
    {
        await using var terminal = await FakeTerminal.Start();
        using var client = new PosClient(PosConnection.Tcp(terminal.Host, terminal.Port, TimeSpan.FromSeconds(5)));

        var result = await client.PurchaseAsync(1250, pcId: "1", settle: false);

        Assert.True(result.IsApproved, result.ToString());
        var request = PosFrame.Decode(terminal.LastRequest, PosFraming.Protocol47);
        Assert.Equal(new byte[] { 0x00 }, request.Message!.Get(PosTags.AutoSettle));
    }

    [Fact]
    public async Task ReadyCommand_AcceptsPlainOk()
    {
        await using var terminal = await FakeTerminal.Start(Encoding.ASCII.GetBytes("OK"));
        using var client = new PosClient(PosConnection.Tcp(terminal.Host, terminal.Port, TimeSpan.FromSeconds(5)));

        var result = await client.ReadyAsync();

        Assert.True(result.IsApproved, result.ToString());
        Assert.Equal("Ready?", Encoding.ASCII.GetString(terminal.LastRequest));
    }

    [Fact]
    public async Task InvalidAmount_DoesNotOpenASocket()
    {
        using var client = new PosClient(PosConnection.Tcp("127.0.0.1", 1024, TimeSpan.FromSeconds(5)));
        var result = await client.PurchaseAsync(0);
        Assert.Equal((int)ReturnCode.ERR_PC_INVALID_AMOUNT, result.ReturnCode);
    }

    [Fact]
    public async Task AmountMismatch_IsReported()
    {
        var response = ResponseFrame("1251");
        await using var terminal = await FakeTerminal.Start(response);
        using var client = new PosClient(PosConnection.Tcp(terminal.Host, terminal.Port, TimeSpan.FromSeconds(5)));

        var result = await client.PurchaseAsync(1250, pcId: "5263");

        Assert.Equal((int)ReturnCode.ERR_PC_INVALID_AMOUNT, result.ReturnCode);
        Assert.Equal("1251", result.Amount);
    }

    [Fact]
    public async Task TransactionNames_MatchTheLibraryGuide()
    {
        await using var terminal = await FakeTerminal.Start();
        var connection = new Transaction.Connection
        {
            CommunicationType = "tcp/ip",
            POS_IP = terminal.Host,
            POS_PORTtcp = terminal.Port,
            POSPC_TCPCOMMU_SocketRecTimeout = 5000
        };
        using var txn = new Transaction(connection);

        var result = txn.Debits_Goods_And_Service("42", "5263", "1250", "108", "", "123456");

        Assert.Equal((int)ReturnCode.RET_OK, result.ReturnCode);
        Assert.Equal("171", result.SerialTransaction);
    }

    private static byte[] ResponseFrame(string amount)
    {
        return PosFrame.Encode(
        [
            new Tlv(PosTags.ResponseCode, Encoding.ASCII.GetBytes("00")),
            new Tlv(PosTags.Service, Bcd.Encode("01")),
            new Tlv(PosTags.PcId, Encoding.ASCII.GetBytes("5263")),
            new Tlv(PosTags.Amount, Bcd.Encode(amount)),
            new Tlv(PosTags.Serial, Bcd.Encode("171")),
            new Tlv(PosTags.Trace, Bcd.Encode("127879949527")),
            new Tlv(PosTags.Date, Encoding.ASCII.GetBytes("1396/01/29")),
            new Tlv(PosTags.Time, Encoding.ASCII.GetBytes("14:24")),
            new Tlv(PosTags.PanLast, Bcd.Encode("4869")),
            new Tlv(PosTags.PanBin, Bcd.Encode("629827")),
            new Tlv(PosTags.Terminal, Bcd.Encode("21009268")),
            new Tlv(PosTags.Account, Bcd.Encode("12404580")),
            new Tlv(PosTags.Discount, Bcd.Encode("500")),
            new Tlv(PosTags.RequestId, Encoding.ASCII.GetBytes("42"))
        ], PosFraming.Protocol47);
    }

    private sealed class FakeTerminal : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly Task _serve;
        private readonly CancellationTokenSource _stop = new();

        private FakeTerminal(TcpListener listener, byte[]? fixedResponse)
        {
            _listener = listener;
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            Host = endpoint.Address.ToString();
            Port = endpoint.Port;
            _serve = ServeAsync(fixedResponse, _stop.Token);
        }

        public string Host { get; }

        public int Port { get; }

        public byte[] LastRequest { get; private set; } = [];

        public static async Task<FakeTerminal> Start(byte[]? fixedResponse = null)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var terminal = new FakeTerminal(listener, fixedResponse);
            await Task.Yield();
            return terminal;
        }

        public async ValueTask DisposeAsync()
        {
            _stop.Cancel();
            _listener.Stop();
            try
            {
                await _serve;
            }
            catch (OperationCanceledException)
            {
            }
            catch (SocketException)
            {
            }
        }

        private async Task ServeAsync(byte[]? fixedResponse, CancellationToken cancellationToken)
        {
            using var client = await _listener.AcceptTcpClientAsync(cancellationToken);
            var stream = client.GetStream();
            var buffer = new MemoryStream();
            var chunk = new byte[1024];
            while (!PosFrame.IsComplete(buffer.ToArray(), PosFraming.Protocol47))
            {
                var read = await stream.ReadAsync(chunk, cancellationToken);
                if (read == 0)
                {
                    break;
                }

                buffer.Write(chunk, 0, read);
                if (buffer.Length >= 4 && buffer.GetBuffer()[0] is < (byte)'0' or > (byte)'9')
                {
                    break;
                }
            }

            LastRequest = buffer.ToArray();
            if (PosFrame.IsComplete(LastRequest, PosFraming.Protocol47))
            {
                LastRequest = LastRequest[..LengthOf(LastRequest)];
            }

            var response = fixedResponse ?? ResponseFrame("1250");
            await stream.WriteAsync(response, cancellationToken);
        }

        private static int LengthOf(byte[] frame)
        {
            var body = int.Parse(Encoding.ASCII.GetString(frame, 0, 4));
            return body + 6;
        }
    }
}
