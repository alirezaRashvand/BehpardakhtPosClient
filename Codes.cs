namespace BehpardakhtPosClient;

/// <summary>
/// Library return codes from POS-PC library guide 1.4.47, appendix A.
/// Values 100 and above are produced by the client. Two-digit codes inside
/// <see cref="PosResult.ReasonCode"/> come from the payment switch.
/// </summary>
public enum ReturnCode
{
    RET_OK = 100,
    ERR_PC_INVALID_REC_SIZE = 101,
    ERR_POS_INVALID_DATA = 102,
    ERR_PC_INVALID_REC_PROCESS_CODE = 103,
    ERR_PC_INVALID_AMOUNT = 104,
    ERR_PC_INVALID_INPUT_PAYERID = 105,
    ERR_PC_INVALID_INPUT_TIMEOUT = 106,
    ERR_PC_PORT_TIMEOUT_FOR_REC = 107,
    ERR_POS_RESPONSE_RECEIVED_TOO_LATE = 108,
    ERR_POS_FAILED_TRANSACTION = 109,
    ERR_POS_PRINTER = 110,
    ERR_POS_COMMUNICATION = 111,
    ERR_POS_TO_SEND_TRANSACTION = 112,
    ERR_PC_INVALID_INPUT_PORTNAME = 113,
    ERR_POS_USER_ABORT = 114,
    ERR_PC_INVALID_INPUT_BILLID = 115,
    ERR_PC_INVALID_INPUT_PAYID = 116,
    ERR_PC_PORT_OPEN_FAILED = 117,
    ERR_PC_PORT_ACCESS_FAILED = 118,
    ERR_PC_INVALID_PORT_STATE = 119,
    ERR_PC_INVALID_PORT_PARAMETERS = 120,
    ERR_PC_INVALID_PORT_NAME = 121,
    ERR_PC_NULL_STR_TO_WRITE_IN_PORT = 122,
    ERR_PC_PORT_TIMEOUT_FOR_SEND = 123,
    ERR_POS_CARD_SWIPE_FAILED = 124,
    ERR_PC_INVALID_INPUT_ACCOUNTID = 125,
    ERR_POS_INVALID_INPUT_ACCOUNTID = 126,
    ERR_POS_INVALID_INPUT_PAYERID = 127,
    ERR_POS_INVALID_INPUT_AMOUNT = 128,
    ERR_POS_INVALID_INPUT_REFRENCE_NUMBER = 129,
    ERR_POS_INVALID_INPUT_BILL_ID = 130,
    ERR_POS_INVALID_INPUT_PAYMENT_ID = 131,
    ERR_POS_INVALID_INPUT_ADDITIONALDATA = 132,
    ERR_POS_INVALID_MULTI_PAYMENT_AMOUNT = 133,
    ERR_POS_UNCONFIRM_REC_DATA = 134,
    ERR_PC_INVALID_INPUT_MULTI_PAYMENT_REQUEST_LIST = 161,
    ERR_PC_INVALID_INPUT_MULTI_PAYMENT_AMOUNT = 162,
    ERR_PC_INVALID_INPUT_REFERENCE_NUMBER = 163,
    ERR_POS_PC_CRCERROR_INVALID_DATA = 164,
    ERR_PC_INVALID_POSPC_COMMUNICATION_TYPE = 165,
    ERR_PC_INVALID_INPUT_TCP_SOCKET_PORT = 166,
    ERR_PC_INVALID_INPUT_TCP_SOCKET_RECEIVED_TIMEOUT = 167,
    ERR_PC_TCP_SOCKET_FAILED = 168,
    ERR_PC_TCP_SOCKET_SEND_MSG_FAILED = 169,
    ERR_PC_TCP_SOCKET_RECEIVED_MSG_FAILED = 170,
    ERR_PC_INVALID_INPUT_MERCHANT_MESSAGE = 171,
    ERR_PC_PREPARE_TLV_MSG_FAILED = 172,
    ERR_PC_PORT_EXCEPTION_FOR_REC = 173,
    ERR_PC_NULL_STR_IN_READ_PORT = 174,
    ERR_PC_CALCULATE_CRC_ERROR = 175,
    ERR_PC_INVALID_INPUT_MERCHANT_FIELD = 176,
    RET_OK_RequestID = 177,
    RET_POS_Busy = 178,
    ERR_POS_TRANSACTION_NOT_FOUND = 179,
    ERR_POS_PC_OTHER = 200
}

public static class ServiceCodes
{
    public const string Purchase = "01";
    public const string Payment = "02";
    public const string MultiPayment = "04";
    public const string BillPayment = "05";
    public const string EsfahanCitizen = "11";
    public const string EsfahanCreditCard = "12";
    public const string EsfahanCounterCard = "13";
    public const string Inquiry = "16";
    public const string LastInquiry = "17";

    /// <summary>
    /// Verify is exposed by library guide 1.4.47. Protocol revision 4.7 does not assign this service a code;
    /// 18 is the code this client sends, with the reference number in tag 0xB5.
    /// </summary>
    public const string Verify = "18";

    /// <summary>
    /// Reverse is exposed by library guide 1.4.47. Protocol revision 4.7 does not assign this service a code;
    /// 19 is the code this client sends, with the reference number in tag 0xB5.
    /// </summary>
    public const string Reverse = "19";

    public const string GetCard = "20";
    public const string AppSekehPurchase = "21";
    public const string NavoshCharge = "24";
    public const string NavoshBalance = "25";
    public const string Judiciary = "26";
    public const string Mana = "27";
    public const string GamPurchase = "28";
    public const string CreditPurchase = "29";
    public const string Refund = "30";
    public const string HashCard = "35";
}

public static class PosTags
{
    public const byte LibraryType = 0xA0;
    public const byte LibraryVersion = 0xA1;
    public const byte Service = 0xA2;
    public const byte TransactionType = 0xA3;
    public const byte PcId = 0xA4;
    public const byte WaitTime = 0xAA;
    public const byte TransactionStep = 0xAB;
    public const byte Amount = 0xB0;
    public const byte PayerId = 0xB1;
    public const byte AccountId = 0xB2;
    public const byte BillId = 0xB3;
    public const byte PayId = 0xB4;
    public const byte Reference = 0xB5;
    public const byte MerchantMessage = 0xB6;
    public const byte MultiPaymentDetails = 0xB7;
    public const byte PrintDetail = 0xB8;
    public const byte AdditionalData = 0xB9;
    public const byte RequestId = 0xBA;
    public const byte EncryptedNationalId = 0xBB;

    /// <summary>
    /// Sent only when <c>settle</c> is false. Protocol 4.7 does not name this tag;
    /// it is the auto-settle marker used by library guide 1.4.47.
    /// </summary>
    public const byte AutoSettle = 0xBC;

    public const byte ResponseCode = 0xC0;
    public const byte Serial = 0xC1;
    public const byte Trace = 0xC2;
    public const byte Date = 0xC3;
    public const byte Time = 0xC4;
    public const byte PanLast = 0xC5;
    public const byte PanBin = 0xC6;
    public const byte Terminal = 0xC7;
    public const byte Account = 0xC8;
    public const byte HashedPan = 0xC9;
    public const byte Discount = 0xCA;
    public const byte SnapKind = 0xD0;
    public const byte SnapAmount = 0xD1;
    public const byte AccountAscii = 0x35;
    public const byte CardType = 0x19;
}

public enum ReceiptPrint : byte
{
    Both = 0,
    Customer = 1,
    Merchant = 2,
    None = 3
}
