using System.Text;
using BehpardakhtPosClient.Protocol;

namespace BehpardakhtPosClient;

public sealed class PosResult
{
    public int ReturnCode { get; init; }

    public string? PosResponseCode { get; init; }

    public string? ReasonCode { get; init; }

    public string? SerialTransaction { get; init; }

    public string? TraceNumber { get; init; }

    public string? TerminalNo { get; init; }

    public string? TransactionDate { get; init; }

    public string? TransactionTime { get; init; }

    public string? PAN { get; init; }

    public string? BIN { get; init; }

    public string? AccountNo { get; init; }

    public string? Amount { get; init; }

    public string? DiscountAmount { get; init; }

    public string? RequestId { get; init; }

    public string? ReqID => RequestId;

    public string? PcID { get; init; }

    public string? TransactionType { get; init; }

    public string? HashedPan { get; init; }

    public string? EncryptedNationalId { get; init; }

    public string? SnapAmount { get; init; }

    public string? SnapPaymentType { get; init; }

    public string? AccountSaleCreadit { get; init; }

    public string? RawText { get; init; }

    public byte[]? RawResponse { get; set; }

    public IReadOnlyDictionary<byte, byte[]> Fields { get; init; } = new Dictionary<byte, byte[]>();

    public bool IsApproved => ReturnCode == (int)BehpardakhtPosClient.ReturnCode.RET_OK;

    public string? ReasonDescription => PosCatalog.DescribeReason(ReasonCode);

    public string ReferenceNumber => TraceNumber ?? "";

    public string Pan => PAN ?? "";

    public string AccountNumber => AccountNo ?? "";

    public string TerminalId => TerminalNo ?? "";

    public string SerialNumber => SerialTransaction ?? "";

    public override string ToString() => PosCatalog.Describe(this);

    internal static PosResult Fail(ReturnCode code, string? rawText = null, byte[]? rawResponse = null) => new()
    {
        ReturnCode = (int)code,
        RawText = rawText,
        RawResponse = rawResponse
    };

    internal static PosResult FromMessage(PosMessage message, WireRequest request)
    {
        var fields = message.Fields.GroupBy(field => field.Tag).ToDictionary(group => group.Key, group => group.Last().Value);
        var responseCode = message.Ascii(PosTags.ResponseCode)?.Trim() ?? "";
        var bin = message.Bcd(PosTags.PanBin, significant: false)?.TrimStart('0') ?? "";
        var last = message.Bcd(PosTags.PanLast, significant: false)?.TrimStart('0') ?? "";
        var pan = bin.Length > 0 && last.Length > 0 ? $"{bin}***{last}" : (last.Length > 0 ? last : bin);
        var account = message.Bcd(PosTags.Account) ?? message.Ascii(PosTags.AccountAscii)?.Trim();

        var result = new PosResult
        {
            PosResponseCode = responseCode,
            ReasonCode = "",
            SerialTransaction = message.Bcd(PosTags.Serial) ?? "",
            TraceNumber = message.Bcd(PosTags.Trace) ?? "",
            TerminalNo = message.Bcd(PosTags.Terminal) ?? "",
            TransactionDate = message.Ascii(PosTags.Date) ?? "",
            TransactionTime = message.Ascii(PosTags.Time) ?? "",
            PAN = pan,
            BIN = bin,
            AccountNo = account ?? "",
            Amount = message.Bcd(PosTags.Amount) ?? "",
            DiscountAmount = message.Bcd(PosTags.Discount) ?? "",
            RequestId = message.Ascii(PosTags.RequestId) ?? message.Bcd(PosTags.RequestId) ?? "",
            PcID = message.Ascii(PosTags.PcId) ?? "",
            TransactionType = message.Bcd(PosTags.TransactionType) ?? "",
            HashedPan = message.Ascii(PosTags.HashedPan) ?? "",
            EncryptedNationalId = message.Ascii(PosTags.EncryptedNationalId) ?? "",
            SnapAmount = message.Bcd(PosTags.SnapAmount) ?? "",
            SnapPaymentType = message.Ascii(PosTags.SnapKind) ?? "",
            Fields = fields
        };

        if (responseCode.Length == 0)
        {
            return Copy(result, (int)BehpardakhtPosClient.ReturnCode.ERR_POS_PC_OTHER);
        }

        if (responseCode is "00" or "0" or "OK")
        {
            if (request.CompareAmount
                && request.Amount is { Length: > 0 } sent
                && result.Amount is { Length: > 0 } received
                && !SameNumber(sent, received))
            {
                return Copy(result, (int)BehpardakhtPosClient.ReturnCode.ERR_PC_INVALID_AMOUNT);
            }

            if (request.ServiceCode.Length > 0
                && message.Bcd(PosTags.Service) is { } echoed
                && !SameNumber(echoed, request.ServiceCode))
            {
                return Copy(result, (int)BehpardakhtPosClient.ReturnCode.ERR_PC_INVALID_REC_PROCESS_CODE);
            }

            return Copy(result, (int)BehpardakhtPosClient.ReturnCode.RET_OK);
        }

        if (PosCatalog.TryMapTerminalCode(responseCode, out var mapped))
        {
            var withReason = responseCode.Equals("EA", StringComparison.OrdinalIgnoreCase)
                ? Copy(result, (int)mapped, "EA")
                : Copy(result, (int)mapped);
            return withReason;
        }

        if (responseCode.Length >= 2 && int.TryParse(responseCode[..2], out var reason) && reason is > 0 and < 100)
        {
            return Copy(result, (int)BehpardakhtPosClient.ReturnCode.ERR_POS_FAILED_TRANSACTION, responseCode[..2]);
        }

        return Copy(result, (int)BehpardakhtPosClient.ReturnCode.ERR_POS_PC_OTHER);
    }

    internal static PosResult FromRawText(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Equals("OK", StringComparison.OrdinalIgnoreCase))
        {
            return new PosResult
            {
                ReturnCode = (int)BehpardakhtPosClient.ReturnCode.RET_OK,
                PosResponseCode = "OK",
                RawText = trimmed
            };
        }

        if (trimmed.Equals("FU", StringComparison.OrdinalIgnoreCase))
        {
            return new PosResult
            {
                ReturnCode = (int)BehpardakhtPosClient.ReturnCode.ERR_POS_USER_ABORT,
                PosResponseCode = "FU",
                RawText = trimmed
            };
        }

        return Fail(BehpardakhtPosClient.ReturnCode.ERR_POS_PC_OTHER, trimmed);
    }

    private static bool SameNumber(string left, string right) =>
        long.TryParse(left, out var a) && long.TryParse(right, out var b) && a == b;

    private static PosResult Copy(PosResult source, int returnCode, string? reasonCode = null) => new()
    {
        ReturnCode = returnCode,
        PosResponseCode = source.PosResponseCode,
        ReasonCode = reasonCode ?? source.ReasonCode,
        SerialTransaction = source.SerialTransaction,
        TraceNumber = source.TraceNumber,
        TerminalNo = source.TerminalNo,
        TransactionDate = source.TransactionDate,
        TransactionTime = source.TransactionTime,
        PAN = source.PAN,
        BIN = source.BIN,
        AccountNo = source.AccountNo,
        Amount = source.Amount,
        DiscountAmount = source.DiscountAmount,
        RequestId = source.RequestId,
        PcID = source.PcID,
        TransactionType = source.TransactionType,
        HashedPan = source.HashedPan,
        EncryptedNationalId = source.EncryptedNationalId,
        SnapAmount = source.SnapAmount,
        SnapPaymentType = source.SnapPaymentType,
        AccountSaleCreadit = source.AccountSaleCreadit,
        RawText = source.RawText,
        Fields = source.Fields
    };
}

internal sealed class WireRequest
{
    public string ServiceCode { get; init; } = "";

    public string PcId { get; init; } = "1";

    public string? Amount { get; init; }

    public string? PayerId { get; init; }

    public string? AccountId { get; init; }

    public string? BillId { get; init; }

    public string? PayId { get; init; }

    public string? ReferenceNumber { get; init; }

    public string? RequestId { get; init; }

    public string? MerchantMessage { get; init; }

    public string? AdditionalData { get; init; }

    public string? MultiPaymentDetails { get; init; }

    public byte? PrintDetail { get; init; }

    public string? WaitTime { get; init; }

    public string? Step { get; init; }

    public bool AutoSettle { get; init; }

    public bool CompareAmount { get; init; }

    public bool PlainText { get; init; }

    public string? PlainCommand { get; init; }
}
