using System.Text;
using BehpardakhtPosClient.Protocol;

namespace BehpardakhtPosClient;

internal static class PosValidation
{
    public static PosResult? Validate(PosConnection connection, WireRequest request)
    {
        if (!connection.IsTcp && !connection.IsSerial)
        {
            return PosResult.Fail(ReturnCode.ERR_PC_INVALID_POSPC_COMMUNICATION_TYPE);
        }

        if (connection.IsTcp)
        {
            if (connection.POS_PORTtcp is < 1024 or > 65535)
            {
                return PosResult.Fail(ReturnCode.ERR_PC_INVALID_INPUT_TCP_SOCKET_PORT);
            }

            if (connection.POSPC_TCPCOMMU_SocketRecTimeout is < 2000 or > 600_000)
            {
                return PosResult.Fail(ReturnCode.ERR_PC_INVALID_INPUT_TCP_SOCKET_RECEIVED_TIMEOUT);
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(connection.PC_PORT_Name))
            {
                return PosResult.Fail(ReturnCode.ERR_PC_INVALID_INPUT_PORTNAME);
            }

            if (connection.PC_PORT_ReadTimeout is < 2000 or > 600_000)
            {
                return PosResult.Fail(ReturnCode.ERR_PC_INVALID_INPUT_TIMEOUT);
            }
        }

        if (request.PlainText)
        {
            return string.IsNullOrEmpty(request.PlainCommand)
                ? PosResult.Fail(ReturnCode.ERR_POS_PC_OTHER, "Missing terminal command.")
                : null;
        }

        if (!IsId(request.PcId, 1, 12))
        {
            return PosResult.Fail(ReturnCode.ERR_POS_PC_OTHER, "PcID must be 1 to 12 digits.");
        }

        if (request.Amount is not null && !IsAmount(request.Amount, 12))
        {
            return PosResult.Fail(ReturnCode.ERR_PC_INVALID_AMOUNT);
        }

        if (request.PayerId is not null && !IsId(request.PayerId, 3, 31))
        {
            return PosResult.Fail(ReturnCode.ERR_PC_INVALID_INPUT_PAYERID);
        }

        if (request.AccountId is not null && !IsId(request.AccountId, 1, 24))
        {
            return PosResult.Fail(ReturnCode.ERR_PC_INVALID_INPUT_ACCOUNTID);
        }

        if (request.BillId is not null && !IsLooseNumber(request.BillId, 6, 18))
        {
            return PosResult.Fail(ReturnCode.ERR_PC_INVALID_INPUT_BILLID);
        }

        if (request.PayId is not null && !IsLooseNumber(request.PayId, 6, 18))
        {
            return PosResult.Fail(ReturnCode.ERR_PC_INVALID_INPUT_PAYID);
        }

        if (request.ReferenceNumber is not null && !IsId(request.ReferenceNumber, 1, 18))
        {
            return PosResult.Fail(ReturnCode.ERR_PC_INVALID_INPUT_REFERENCE_NUMBER);
        }

        if (request.RequestId is not null && !IsLooseNumber(request.RequestId, 1, 20))
        {
            return PosResult.Fail(ReturnCode.ERR_POS_PC_OTHER, "RequestID must be 1 to 20 digits.");
        }

        if (request.AdditionalData is not null && Encoding.UTF8.GetByteCount(request.AdditionalData) > 100)
        {
            return PosResult.Fail(ReturnCode.ERR_PC_INVALID_INPUT_MERCHANT_FIELD);
        }

        if (request.MerchantMessage is not null)
        {
            if (request.MerchantMessage.Length > 160)
            {
                return PosResult.Fail(ReturnCode.ERR_PC_INVALID_INPUT_MERCHANT_MESSAGE);
            }

            if (IranSystem.Encode(request.MerchantMessage).Length > 160)
            {
                return PosResult.Fail(ReturnCode.ERR_PC_INVALID_INPUT_MERCHANT_MESSAGE);
            }
        }

        if (request.MultiPaymentDetails is { Length: 0 or > 400 })
        {
            return PosResult.Fail(ReturnCode.ERR_PC_INVALID_INPUT_MULTI_PAYMENT_REQUEST_LIST);
        }

        if (request.PrintDetail is > 3)
        {
            return PosResult.Fail(ReturnCode.ERR_PC_INVALID_INPUT_MULTI_PAYMENT_REQUEST_LIST);
        }

        if (request.WaitTime is not null && !IsWait(request.WaitTime))
        {
            return PosResult.Fail(ReturnCode.ERR_PC_INVALID_INPUT_TIMEOUT);
        }

        if (request.Step is not null && request.Step is not ("0" or "1"))
        {
            return PosResult.Fail(ReturnCode.ERR_POS_PC_OTHER, "Step must be 0 or 1.");
        }

        return null;
    }

    public static bool IsAmount(string value, int maxDigits) =>
        value.Length is > 0 && value.Length <= maxDigits && value.All(char.IsAsciiDigit) && long.TryParse(value, out var amount) && amount > 0;

    public static bool IsId(string value, int min, int max) =>
        value.Length >= min && value.Length <= max && value.All(char.IsAsciiDigit) && value.Any(ch => ch != '0');

    private static bool IsLooseNumber(string value, int min, int max) =>
        value.Length >= min && value.Length <= max && value.All(char.IsAsciiDigit) && long.TryParse(value, out var number) && number > 0;

    private static bool IsWait(string value) =>
        int.TryParse(value, out var seconds) && seconds is >= 15 and <= 120;
}
