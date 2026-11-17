using System.Globalization;
using System.Text;

namespace BehpardakhtPosClient;

/// <summary>
/// Async client for a Behpardakht Mellat POS. One instance talks to one terminal.
/// Calls are serialized, so overlapping purchases wait their turn.
/// </summary>
public sealed class PosClient(PosConnection connection) : IDisposable
{
    private readonly PosConnection _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;

    public PosConnection Connection => _connection;

    public Task<PosResult> PurchaseAsync(
        long amount,
        string? requestId = null,
        string pcId = "1",
        string? payerId = null,
        string? merchantMessage = null,
        string? additionalData = null,
        bool settle = true,
        CancellationToken cancellationToken = default) =>
        SendAsync(Sale(ServiceCodes.Purchase, amount, requestId, pcId, payerId, merchantMessage, additionalData, settle), cancellationToken);

    public Task<PosResult> PaymentAsync(
        long amount,
        string accountId,
        string? requestId = null,
        string pcId = "1",
        string? payerId = null,
        string? merchantMessage = null,
        string? additionalData = null,
        bool settle = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accountId))
        {
            return Task.FromResult(PosResult.Fail(ReturnCode.ERR_PC_INVALID_INPUT_ACCOUNTID));
        }

        return SendAsync(Sale(ServiceCodes.Payment, amount, requestId, pcId, payerId, merchantMessage, additionalData, settle, accountId), cancellationToken);
    }

    public Task<PosResult> BillPaymentAsync(
        string billId,
        string payId,
        string? requestId = null,
        string pcId = "1",
        string? merchantMessage = null,
        string? additionalData = null,
        CancellationToken cancellationToken = default)
    {
        var amount = BillAmount(billId, payId);
        return SendAsync(new WireRequest
        {
            ServiceCode = ServiceCodes.BillPayment,
            PcId = Pc(pcId),
            Amount = amount,
            BillId = NullIfEmpty(billId),
            PayId = NullIfEmpty(payId),
            RequestId = NullIfEmpty(requestId),
            MerchantMessage = NullIfEmpty(merchantMessage),
            AdditionalData = NullIfEmpty(additionalData),
            CompareAmount = amount is not null
        }, cancellationToken);
    }

    public Task<PosResult> MultiPaymentAsync(
        long totalAmount,
        IReadOnlyList<MultiPaymentReqDataSet> items,
        ReceiptPrint printDetail = ReceiptPrint.Both,
        string? requestId = null,
        string pcId = "1",
        string? merchantMessage = null,
        string? additionalData = null,
        bool settle = true,
        CancellationToken cancellationToken = default)
    {
        var built = BuildMultiPayment(totalAmount, items, (byte)printDetail);
        if (built.Error is not null)
        {
            return Task.FromResult(built.Error);
        }

        return SendAsync(new WireRequest
        {
            ServiceCode = ServiceCodes.MultiPayment,
            PcId = Pc(pcId),
            Amount = totalAmount.ToString(CultureInfo.InvariantCulture),
            RequestId = NullIfEmpty(requestId),
            MerchantMessage = NullIfEmpty(merchantMessage),
            AdditionalData = NullIfEmpty(additionalData),
            MultiPaymentDetails = built.Details,
            PrintDetail = (byte)printDetail,
            AutoSettle = !settle,
            CompareAmount = true
        }, cancellationToken);
    }

    public Task<PosResult> InquiryAsync(string requestId, string pcId = "1", string? additionalData = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            return Task.FromResult(PosResult.Fail(ReturnCode.ERR_POS_PC_OTHER, "RequestID is required for inquiry."));
        }

        return SendAsync(new WireRequest
        {
            ServiceCode = ServiceCodes.Inquiry,
            PcId = Pc(pcId),
            RequestId = requestId.Trim(),
            AdditionalData = NullIfEmpty(additionalData)
        }, cancellationToken);
    }

    public Task<PosResult> LastInquiryAsync(string pcId = "1", string? additionalData = null, CancellationToken cancellationToken = default) =>
        SendAsync(new WireRequest
        {
            ServiceCode = ServiceCodes.LastInquiry,
            PcId = Pc(pcId),
            AdditionalData = NullIfEmpty(additionalData)
        }, cancellationToken);

    public Task<PosResult> GetCardAsync(string processCode = ServiceCodes.GetCard, string pcId = "1", string? additionalData = null, CancellationToken cancellationToken = default) =>
        SendAsync(new WireRequest
        {
            ServiceCode = processCode,
            PcId = Pc(pcId),
            AdditionalData = NullIfEmpty(additionalData)
        }, cancellationToken);

    public Task<PosResult> RefundAsync(long amount, string referenceNumber, string pcId = "1", CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(referenceNumber))
        {
            return Task.FromResult(PosResult.Fail(ReturnCode.ERR_PC_INVALID_INPUT_REFERENCE_NUMBER));
        }

        return SendAsync(new WireRequest
        {
            ServiceCode = ServiceCodes.Refund,
            PcId = Pc(pcId),
            Amount = amount.ToString(CultureInfo.InvariantCulture),
            ReferenceNumber = referenceNumber.Trim(),
            CompareAmount = true
        }, cancellationToken);
    }

    public Task<PosResult> VerifyAsync(string referenceNumber, string pcId = "1", CancellationToken cancellationToken = default) =>
        ReferenceAsync(ServiceCodes.Verify, referenceNumber, pcId, cancellationToken);

    public Task<PosResult> ReverseAsync(string referenceNumber, string pcId = "1", CancellationToken cancellationToken = default) =>
        ReferenceAsync(ServiceCodes.Reverse, referenceNumber, pcId, cancellationToken);

    public Task<PosResult> NavoshChargeAsync(long amount, string? requestId = null, string pcId = "1", string? merchantMessage = null, string? additionalData = null, CancellationToken cancellationToken = default) =>
        SendAsync(Sale(ServiceCodes.NavoshCharge, amount, requestId, pcId, null, merchantMessage, additionalData, settle: true), cancellationToken);

    public Task<PosResult> NavoshBalanceAsync(string? requestId = null, string pcId = "1", string? merchantMessage = null, string? additionalData = null, CancellationToken cancellationToken = default) =>
        SendAsync(new WireRequest
        {
            ServiceCode = ServiceCodes.NavoshBalance,
            PcId = Pc(pcId),
            RequestId = NullIfEmpty(requestId),
            MerchantMessage = NullIfEmpty(merchantMessage),
            AdditionalData = NullIfEmpty(additionalData)
        }, cancellationToken);

    public Task<PosResult> JudiciaryAsync(string inquiryCode, string pcId = "1", CancellationToken cancellationToken = default) =>
        SendAsync(new WireRequest
        {
            ServiceCode = ServiceCodes.Judiciary,
            PcId = Pc(pcId),
            PayerId = NullIfEmpty(inquiryCode)
        }, cancellationToken);

    public Task<PosResult> ManaAsync(int waitSeconds, bool twoStep, string pcId = "1", string serviceCode = ServiceCodes.Mana, CancellationToken cancellationToken = default) =>
        CardDataAsync(serviceCode, waitSeconds, twoStep, pcId, cancellationToken);

    public Task<PosResult> HashCardAsync(int waitSeconds, bool twoStep, string pcId = "1", CancellationToken cancellationToken = default) =>
        CardDataAsync(ServiceCodes.HashCard, waitSeconds, twoStep, pcId, cancellationToken);

    public Task<PosResult> GamPurchaseAsync(long amount, string? requestId = null, string pcId = "1", string? payerId = null, string? merchantMessage = null, string? additionalData = null, bool settle = true, CancellationToken cancellationToken = default) =>
        SendAsync(Sale(ServiceCodes.GamPurchase, amount, requestId, pcId, payerId, merchantMessage, additionalData, settle), cancellationToken);

    public Task<PosResult> CreditPurchaseAsync(long amount, string? requestId = null, string pcId = "1", string? payerId = null, string? merchantMessage = null, string? additionalData = null, bool settle = true, CancellationToken cancellationToken = default) =>
        SendAsync(Sale(ServiceCodes.CreditPurchase, amount, requestId, pcId, payerId, merchantMessage, additionalData, settle), cancellationToken);

    public Task<PosResult> AppSekehPurchaseAsync(long amount, string? requestId = null, string pcId = "1", string? merchantMessage = null, string? additionalData = null, bool settle = true, CancellationToken cancellationToken = default) =>
        SendAsync(Sale(ServiceCodes.AppSekehPurchase, amount, requestId, pcId, null, merchantMessage, additionalData, settle), cancellationToken);

    public Task<PosResult> EsfahanCitizenChargeAsync(long amount, string? requestId = null, string pcId = "1", string? merchantMessage = null, string? additionalData = null, CancellationToken cancellationToken = default) =>
        SendAsync(Sale(ServiceCodes.EsfahanCitizen, amount, requestId, pcId, null, merchantMessage, additionalData, settle: true), cancellationToken);

    public Task<PosResult> EsfahanResellerChargeAsync(long amount, bool counterCard, string? payerId = null, string? accountId = null, string? requestId = null, string pcId = "1", string? merchantMessage = null, string? additionalData = null, CancellationToken cancellationToken = default) =>
        SendAsync(Sale(counterCard ? ServiceCodes.EsfahanCounterCard : ServiceCodes.EsfahanCreditCard, amount, requestId, pcId, payerId, merchantMessage, additionalData, settle: true, accountId), cancellationToken);

    public Task<PosResult> ReadyAsync(CancellationToken cancellationToken = default) =>
        CommandAsync("Ready?", cancellationToken);

    public Task<PosResult> CancelAsync(CancellationToken cancellationToken = default) =>
        CommandAsync("CANCEL", cancellationToken);

    public Task<PosResult> SekehAsync(string command, CancellationToken cancellationToken = default) =>
        CommandAsync(command, cancellationToken);

    public Task<PosResult> SendServiceAsync(
        string serviceCode,
        string pcId = "1",
        string? amount = null,
        string? requestId = null,
        string? payerId = null,
        string? accountId = null,
        string? referenceNumber = null,
        string? merchantMessage = null,
        string? additionalData = null,
        bool settle = true,
        bool compareAmount = false,
        CancellationToken cancellationToken = default) =>
        SendAsync(new WireRequest
        {
            ServiceCode = serviceCode,
            PcId = Pc(pcId),
            Amount = NullIfEmpty(amount),
            RequestId = NullIfEmpty(requestId),
            PayerId = NullIfEmpty(payerId),
            AccountId = NullIfEmpty(accountId),
            ReferenceNumber = NullIfEmpty(referenceNumber),
            MerchantMessage = NullIfEmpty(merchantMessage),
            AdditionalData = NullIfEmpty(additionalData),
            AutoSettle = !settle,
            CompareAmount = compareAmount
        }, cancellationToken);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _gate.Dispose();
    }

    internal static string? BillAmount(string? billId, string? payId)
    {
        if (string.IsNullOrWhiteSpace(billId) || string.IsNullOrWhiteSpace(payId))
        {
            return null;
        }

        if (payId.Length < (billId.Length == 18 ? 9 : 6))
        {
            return payId;
        }

        var amount = billId.Length == 18
            ? payId[..^8]
            : payId[..^5];
        if (billId.Length <= 13)
        {
            amount += "000";
        }

        return amount.TrimStart('0') is { Length: > 0 } trimmed ? trimmed : null;
    }

    internal static (string? Details, PosResult? Error) BuildMultiPayment(long totalAmount, IReadOnlyList<MultiPaymentReqDataSet> items, byte printDetail)
    {
        if (items is null || items.Count is < 1 or > 10 || printDetail > 3)
        {
            return (null, PosResult.Fail(ReturnCode.ERR_PC_INVALID_INPUT_MULTI_PAYMENT_REQUEST_LIST));
        }

        ulong sum = 0;
        var details = new StringBuilder();
        foreach (var item in items)
        {
            var account = item.AccountID ?? "";
            var amount = item.Amount ?? "";
            var payer = item.PayerID ?? "";
            if (!PosValidation.IsId(account, 1, 10))
            {
                return (null, PosResult.Fail(ReturnCode.ERR_PC_INVALID_INPUT_ACCOUNTID));
            }

            if (!PosValidation.IsAmount(amount, 10))
            {
                return (null, PosResult.Fail(ReturnCode.ERR_PC_INVALID_AMOUNT));
            }

            if (payer.Length > 0 && !PosValidation.IsId(payer, 3, 31))
            {
                return (null, PosResult.Fail(ReturnCode.ERR_PC_INVALID_INPUT_PAYERID));
            }

            sum += ulong.Parse(amount, CultureInfo.InvariantCulture);
            details.Append(account).Append(',').Append(amount).Append(',').Append(payer).Append(';');
        }

        if (sum != (ulong)totalAmount)
        {
            return (null, PosResult.Fail(ReturnCode.ERR_PC_INVALID_INPUT_MULTI_PAYMENT_AMOUNT));
        }

        if (details.Length is < 1 or > 400)
        {
            return (null, PosResult.Fail(ReturnCode.ERR_PC_INVALID_INPUT_MULTI_PAYMENT_REQUEST_LIST));
        }

        return (details.ToString(), null);
    }

    private Task<PosResult> ReferenceAsync(string serviceCode, string referenceNumber, string pcId, CancellationToken cancellationToken) =>
        SendAsync(new WireRequest
        {
            ServiceCode = serviceCode,
            PcId = Pc(pcId),
            ReferenceNumber = NullIfEmpty(referenceNumber)
        }, cancellationToken);

    private Task<PosResult> CardDataAsync(string serviceCode, int waitSeconds, bool twoStep, string pcId, CancellationToken cancellationToken) =>
        SendAsync(new WireRequest
        {
            ServiceCode = serviceCode,
            PcId = Pc(pcId),
            WaitTime = waitSeconds.ToString(CultureInfo.InvariantCulture),
            Step = twoStep ? "1" : "0"
        }, cancellationToken);

    private Task<PosResult> CommandAsync(string command, CancellationToken cancellationToken) =>
        SendAsync(new WireRequest { PlainText = true, PlainCommand = command }, cancellationToken);

    private static WireRequest Sale(
        string serviceCode,
        long amount,
        string? requestId,
        string pcId,
        string? payerId,
        string? merchantMessage,
        string? additionalData,
        bool settle,
        string? accountId = null) => new()
    {
        ServiceCode = serviceCode,
        PcId = Pc(pcId),
        Amount = amount.ToString(CultureInfo.InvariantCulture),
        RequestId = NullIfEmpty(requestId),
        PayerId = NullIfEmpty(payerId),
        AccountId = NullIfEmpty(accountId),
        MerchantMessage = NullIfEmpty(merchantMessage),
        AdditionalData = NullIfEmpty(additionalData),
        AutoSettle = !settle,
        CompareAmount = true
    };

    private async Task<PosResult> SendAsync(WireRequest request, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await PosTransport.ExchangeAsync(_connection, request, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string Pc(string? pcId) => string.IsNullOrWhiteSpace(pcId) ? "1" : pcId.Trim();

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// One row of a multi-account payment. Property names match the POS-PC library.
/// </summary>
public sealed class MultiPaymentReqDataSet
{
    public string AccountID { get; set; } = "";

    public string Amount { get; set; } = "";

    public string PayerID { get; set; } = "";
}
