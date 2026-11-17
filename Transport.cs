using System.Globalization;
using System.Net.Sockets;
using System.Text;
using BehpardakhtPosClient.Protocol;

namespace BehpardakhtPosClient;

internal static class RequestEncoder
{
    public static byte[] Encode(WireRequest request, PosFraming framing, bool includeLibraryHeader = true)
    {
        if (request.PlainText)
        {
            return Encoding.ASCII.GetBytes(request.PlainCommand ?? "");
        }

        var fields = new List<Tlv>();
        if (framing == PosFraming.LegacyLibrary && includeLibraryHeader)
        {
            fields.Add(new Tlv(PosTags.LibraryType, Encoding.ASCII.GetBytes(".NET")));
            fields.Add(new Tlv(PosTags.LibraryVersion, Bcd.Encode("010447")));
        }

        AddBcd(fields, PosTags.Service, request.ServiceCode);
        AddAscii(fields, PosTags.PcId, request.PcId);
        AddAscii(fields, PosTags.RequestId, request.RequestId);
        AddBcd(fields, PosTags.Amount, request.Amount);
        AddBcd(fields, PosTags.PayerId, request.PayerId);
        AddBcd(fields, PosTags.AccountId, request.AccountId);
        AddBcd(fields, PosTags.BillId, request.BillId);
        AddBcd(fields, PosTags.PayId, request.PayId);
        AddBcd(fields, PosTags.Reference, request.ReferenceNumber);
        if (!string.IsNullOrEmpty(request.MerchantMessage))
        {
            fields.Add(new Tlv(PosTags.MerchantMessage, IranSystem.Encode(request.MerchantMessage)));
        }

        if (!string.IsNullOrEmpty(request.MultiPaymentDetails))
        {
            fields.Add(new Tlv(PosTags.MultiPaymentDetails, Encoding.ASCII.GetBytes(request.MultiPaymentDetails)));
        }

        if (request.PrintDetail is not null)
        {
            fields.Add(new Tlv(PosTags.PrintDetail, [request.PrintDetail.Value]));
        }

        if (!string.IsNullOrEmpty(request.AdditionalData))
        {
            var extra = Encoding.UTF8.GetBytes(request.AdditionalData);
            fields.Add(new Tlv(PosTags.AdditionalData, extra));
        }

        AddBcd(fields, PosTags.WaitTime, request.WaitTime);
        AddBcd(fields, PosTags.TransactionStep, request.Step);
        if (request.AutoSettle)
        {
            fields.Add(new Tlv(PosTags.AutoSettle, [0x00]));
        }

        return PosFrame.Encode(fields, framing);
    }

    private static void AddAscii(List<Tlv> fields, byte tag, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            fields.Add(new Tlv(tag, Encoding.ASCII.GetBytes(value)));
        }
    }

    private static void AddBcd(List<Tlv> fields, byte tag, string? digits)
    {
        if (!string.IsNullOrEmpty(digits))
        {
            fields.Add(new Tlv(tag, Bcd.Encode(digits)));
        }
    }
}

internal static class PosTransport
{
    public static async Task<PosResult> ExchangeAsync(PosConnection connection, WireRequest request, CancellationToken cancellationToken)
    {
        var validation = PosValidation.Validate(connection, request);
        if (validation is not null)
        {
            return validation;
        }

        byte[] frame;
        try
        {
            frame = RequestEncoder.Encode(request, connection.Framing, connection.IncludeLibraryHeader);
            Console.Error.WriteLine($"SEND {Convert.ToHexString(frame)}");
        }
        catch (Exception)
        {
            return PosResult.Fail(ReturnCode.ERR_PC_PREPARE_TLV_MSG_FAILED);
        }

        try
        {
            var response = connection.IsTcp
                ? await ExchangeTcpAsync(connection, frame, cancellationToken).ConfigureAwait(false)
                : await ExchangeSerialAsync(connection, frame, cancellationToken).ConfigureAwait(false);
            return Interpret(response, request, connection.Framing);
        }
        catch (TimeoutException)
        {
            return PosResult.Fail(connection.IsTcp
                ? ReturnCode.ERR_PC_TCP_SOCKET_RECEIVED_MSG_FAILED
                : ReturnCode.ERR_PC_PORT_TIMEOUT_FOR_REC);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return PosResult.Fail(connection.IsTcp
                ? ReturnCode.ERR_PC_TCP_SOCKET_RECEIVED_MSG_FAILED
                : ReturnCode.ERR_PC_PORT_TIMEOUT_FOR_REC);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            return PosResult.Fail(ReturnCode.ERR_PC_PORT_OPEN_FAILED);
        }
        catch (UnauthorizedAccessException)
        {
            return PosResult.Fail(ReturnCode.ERR_PC_PORT_ACCESS_FAILED);
        }
        catch (ArgumentException)
        {
            return PosResult.Fail(connection.IsTcp
                ? ReturnCode.ERR_PC_TCP_SOCKET_FAILED
                : ReturnCode.ERR_PC_INVALID_PORT_NAME);
        }
        catch (IOException)
        {
            return PosResult.Fail(connection.IsTcp
                ? ReturnCode.ERR_PC_TCP_SOCKET_RECEIVED_MSG_FAILED
                : ReturnCode.ERR_PC_PORT_EXCEPTION_FOR_REC);
        }
        catch (SocketException)
        {
            return PosResult.Fail(ReturnCode.ERR_PC_TCP_SOCKET_FAILED);
        }
        catch (Exception)
        {
            return PosResult.Fail(ReturnCode.ERR_POS_PC_OTHER);
        }
    }

    private static PosResult Interpret(byte[] response, WireRequest request, PosFraming framing)
    {
        if (response.Length == 0)
        {
            return PosResult.Fail(ReturnCode.ERR_PC_INVALID_REC_SIZE);
        }

        var framingToUse = PosFrame.TryMatch(response, out var detected, out _) ? detected : framing;
        if (PosFrame.IsComplete(response, framingToUse) || (response.Length >= 4 && response[0] is >= (byte)'0' and <= (byte)'9'))
        {
            var decoded = PosFrame.Decode(response, framingToUse);
            if (decoded.ErrorCode != 0 || decoded.Message is null)
            {
                return PosResult.Fail((ReturnCode)decoded.ErrorCode, rawResponse: response);
            }

            var parsed = PosResult.FromMessage(decoded.Message, request);
            parsed.RawResponse = response;
            return parsed;
        }

        var text = PosResult.FromRawText(Encoding.ASCII.GetString(response));
        return new PosResult
        {
            ReturnCode = text.ReturnCode,
            PosResponseCode = text.PosResponseCode,
            RawText = text.RawText,
            RawResponse = response
        };
    }

    private static async Task<byte[]> ExchangeTcpAsync(PosConnection connection, byte[] frame, CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        connectCts.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            await client.ConnectAsync(connection.POS_IP, connection.POS_PORTtcp, connectCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SocketException((int)SocketError.TimedOut);
        }

        client.SendTimeout = 15_000;
        var stream = client.GetStream();
        await stream.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
        return await ReadResponseAsync(stream, connection.Framing, connection.ReceiveTimeout, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<byte[]> ExchangeSerialAsync(PosConnection connection, byte[] frame, CancellationToken cancellationToken)
    {
        using var port = new System.IO.Ports.SerialPort(
            connection.PC_PORT_Name,
            connection.PC_PORT_BaudRate,
            connection.PC_PORT_Parity,
            connection.PC_PORT_DataBits,
            connection.PC_PORT_StopBits)
        {
            Handshake = System.IO.Ports.Handshake.None,
            WriteTimeout = 15_000,
            ReadTimeout = connection.PC_PORT_ReadTimeout
        };

        port.Open();
        var stream = port.BaseStream;
        await stream.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
        return await ReadResponseAsync(stream, connection.Framing, connection.ReceiveTimeout, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<byte[]> ReadResponseAsync(Stream stream, PosFraming framing, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var overall = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        overall.CancelAfter(timeout);
        var data = new MemoryStream();
        var buffer = new byte[1024];

        while (true)
        {
            int read;
            try
            {
                read = await stream.ReadAsync(buffer, overall.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                if (data.Length > 0)
                {
                    return data.ToArray();
                }

                throw new TimeoutException("Timed out waiting for the POS.");
            }

            if (read == 0)
            {
                if (data.Length == 0)
                {
                    throw new IOException("The POS closed the connection.");
                }

                return data.ToArray();
            }

            data.Write(buffer, 0, read);
            var received = data.ToArray();
            if (PosFrame.TryMatch(received, out _, out var matched))
            {
                return received[..matched];
            }

            if (PosFrame.IsComplete(received, framing))
            {
                return received[..FramingLength(received, framing)];
            }

            if (received.Length >= 4 && !IsDigitHeader(received))
            {
                await WaitForMoreAsync(stream, data, buffer, TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);
                return data.ToArray();
            }

            if (received.Length is > 0 and < 4)
            {
                var grew = await WaitForMoreAsync(stream, data, buffer, TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
                if (!grew)
                {
                    return data.ToArray();
                }
            }
        }
    }

    private static int FramingLength(byte[] received, PosFraming framing)
    {
        var declared = int.Parse(Encoding.ASCII.GetString(received, 0, 4), CultureInfo.InvariantCulture);
        var body = framing == PosFraming.Protocol47 ? declared : declared - 6;
        return 4 + body + 2;
    }

    private static bool IsDigitHeader(byte[] received) =>
        received.Length >= 4 && received.Take(4).All(value => value is >= (byte)'0' and <= (byte)'9');

    private static async Task<bool> WaitForMoreAsync(Stream stream, MemoryStream data, byte[] buffer, TimeSpan quiet, CancellationToken cancellationToken)
    {
        using var idle = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        idle.CancelAfter(quiet);
        try
        {
            var read = await stream.ReadAsync(buffer, idle.Token).ConfigureAwait(false);
            if (read > 0)
            {
                data.Write(buffer, 0, read);
                return true;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return false;
    }
}
