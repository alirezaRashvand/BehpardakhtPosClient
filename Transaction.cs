namespace BehpardakhtPosClient;

/// <summary>
/// Blocking API with the same method names as POS_PC.dll / library guide 1.4.47.
/// </summary>
public sealed class Transaction : IDisposable
{
    private readonly PosClient _client;

    public Transaction(PosConnection connection)
    {
        _client = new PosClient(connection);
    }

    public sealed class Connection : PosConnection;

    public PosResult Debits_Goods_And_Service(string requestId, string pcId, string amount, string payerId, string merchantMsg, string additionalData, bool settle = true) =>
        Sale(ServiceCodes.Purchase, requestId, pcId, amount, payerId, merchantMsg, additionalData, settle);

    public PosResult Payment(string requestId, string pcId, string amount, string payerId, string accountId, string additionalData, bool settle = true) =>
        Sale(ServiceCodes.Payment, requestId, pcId, amount, payerId, "", additionalData, settle, accountId);

    public PosResult Bill_Payment_Service(string requestId, string pcId, string billId, string payId, string merchantMsg, string additionalData) =>
        Block(_client.BillPaymentAsync(billId, payId, requestId, pcId, merchantMsg, additionalData));

    public PosResult MultiPayment(string requestId, string pcId, string totalAmount, MultiPaymentReqDataSet[] requestList, byte printDetail, string additionalData, bool settle = true)
    {
        if (!long.TryParse(totalAmount, out var total))
        {
            return PosResult.Fail(ReturnCode.ERR_PC_INVALID_AMOUNT);
        }

        return Block(_client.MultiPaymentAsync(total, requestList, (ReceiptPrint)printDetail, requestId, pcId, additionalData: additionalData, settle: settle));
    }

    public PosResult Inquery_Service(string requestId, string pcId, string additionalData) =>
        Block(_client.InquiryAsync(requestId, pcId, additionalData));

    public PosResult Last_Inquery_Service(string pcId, string additionalData) =>
        Block(_client.LastInquiryAsync(pcId, additionalData));

    public PosResult Get_Card_Service(string processCode, string pcId, string additionalData) =>
        Block(_client.GetCardAsync(processCode, pcId, additionalData));

    public PosResult Get_Card_Debits_Goods_And_Service(string requestId, string pcId, string amount, string payerId, string merchantMsg, string additionalData) =>
        Sale(ServiceCodes.Purchase, requestId, pcId, amount, payerId, merchantMsg, additionalData, settle: true);

    public PosResult Etebary_Debits_Goods_And_Service(string requestId, string pcId, string amount, string payerId, string merchantMsg, string additionalData) =>
        Sale(ServiceCodes.CreditPurchase, requestId, pcId, amount, payerId, merchantMsg, additionalData, settle: true);

    public PosResult GAM_Debits_Goods_And_Service(string requestId, string pcId, string amount, string payerId, string merchantMsg, string additionalData, bool settle = true) =>
        Sale(ServiceCodes.GamPurchase, requestId, pcId, amount, payerId, merchantMsg, additionalData, settle);

    public PosResult Karid_Az_Mahale_Etebar_Service(string requestId, string pcId, string amount, string payerId, string merchantMsg, string additionalData, bool settle = true) =>
        Sale(ServiceCodes.CreditPurchase, requestId, pcId, amount, payerId, merchantMsg, additionalData, settle);

    public PosResult Refound(string pcId, string amount, string registerNo) =>
        long.TryParse(amount, out var value)
            ? Block(_client.RefundAsync(value, registerNo, pcId))
            : PosResult.Fail(ReturnCode.ERR_PC_INVALID_AMOUNT);

    public PosResult Verify_Service(string pcId, string referenceId) =>
        Block(_client.VerifyAsync(referenceId, pcId));

    public PosResult Reverse_Service(string pcId, string referenceId) =>
        Block(_client.ReverseAsync(referenceId, pcId));

    public PosResult Navosh_Charge_Service(string requestId, string pcId, string amount, string merchantMsg, string additionalData) =>
        Sale(ServiceCodes.NavoshCharge, requestId, pcId, amount, "", merchantMsg, additionalData, settle: true);

    public PosResult Navosh_Inventory_Service(string requestId, string pcId, string merchantMsg, string additionalData) =>
        Block(_client.NavoshBalanceAsync(requestId, pcId, merchantMsg, additionalData));

    public PosResult Ghoveh_Ghazai(string pcId, string inquiryCode) =>
        Block(_client.JudiciaryAsync(inquiryCode, pcId));

    public PosResult Mana(string processCode, string pcId, string waitTime, string step) =>
        CardData(processCode, pcId, waitTime, step);

    public PosResult HashCard(string pcId, string waitTime, string step) =>
        CardData(ServiceCodes.HashCard, pcId, waitTime, step);

    public PosResult AppSekeh_Debits_Goods_And_Service(string pcId, string amount, string requestId, string merchantMsg, string additionalData, bool settle = true) =>
        Sale(ServiceCodes.AppSekehPurchase, requestId, pcId, amount, "", merchantMsg, additionalData, settle);

    public PosResult Ready_Cancel(string command) =>
        Block(_client.SekehAsync(command));

    public PosResult Sekeh(string command, string amount) =>
        Block(_client.SekehAsync(command));

    public PosResult EsfahanCitizenCharge(string pcId, string amount, string merchantMsg, string additionalData) =>
        Sale(ServiceCodes.EsfahanCitizen, "", pcId, amount, "", merchantMsg, additionalData, settle: true);

    public PosResult EsfahanResellerCharge(string pcId, string cardType, string amount, string payerId, string accountId, string merchantMsg, string additionalData) =>
        Sale(cardType == "0" ? ServiceCodes.EsfahanCreditCard : ServiceCodes.EsfahanCounterCard, "", pcId, amount, payerId, merchantMsg, additionalData, settle: true, accountId);

    public void Dispose() => _client.Dispose();

    private PosResult Sale(string service, string requestId, string pcId, string amount, string payerId, string merchantMsg, string additionalData, bool settle, string? accountId = null)
    {
        if (!long.TryParse(amount, out _))
        {
            return PosResult.Fail(ReturnCode.ERR_PC_INVALID_AMOUNT);
        }

        return Block(_client.SendServiceAsync(service, pcId, amount, requestId, payerId, accountId, merchantMessage: merchantMsg, additionalData: additionalData, settle: settle, compareAmount: true));
    }

    private PosResult CardData(string service, string pcId, string waitTime, string step)
    {
        if (!int.TryParse(waitTime, out var seconds))
        {
            return PosResult.Fail(ReturnCode.ERR_PC_INVALID_INPUT_TIMEOUT);
        }

        return Block(_client.ManaAsync(seconds, step == "1", pcId, service));
    }

    private static PosResult Block(Task<PosResult> call) => call.GetAwaiter().GetResult();
}
