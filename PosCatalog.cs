namespace BehpardakhtPosClient;

public static class PosCatalog
{
    private static readonly Dictionary<int, string> Reasons = new()
    {
        [0] = "Transaction approved.",
        [1] = "Refer to card issuer.",
        [2] = "Previous correction was approved.",
        [3] = "Invalid merchant.",
        [4] = "Duplicate reference number.",
        [5] = "Invalid transaction.",
        [6] = "Issuer switch internal error.",
        [7] = "Invalid bill id.",
        [8] = "Invalid payment id.",
        [9] = "Previous request is still in progress.",
        [10] = "Original transaction was not found.",
        [11] = "Original processing code is invalid.",
        [12] = "PIN is required.",
        [13] = "Invalid amount.",
        [14] = "Invalid card number.",
        [15] = "Requested service is not available.",
        [16] = "Card number does not match the original transaction.",
        [17] = "Cancelled by the card holder.",
        [18] = "Service is not available right now.",
        [19] = "Repeat the transaction.",
        [20] = "System malfunction.",
        [21] = "No action taken.",
        [22] = "Security error.",
        [23] = "Invalid fee amount.",
        [24] = "Refund is not allowed; the original transaction was already reversed.",
        [25] = "Referenced transaction was not found.",
        [26] = "Refund total exceeds the original amount.",
        [27] = "Correction amount does not match the original transaction.",
        [28] = "Original transaction was already reversed by the system.",
        [29] = "Correction is not allowed.",
        [30] = "Request is outside the allowed time window.",
        [31] = "Invalid card issuer.",
        [32] = "No transaction for this settlement and customer number.",
        [33] = "Expired card.",
        [34] = "Refund customer does not match the original transaction.",
        [35] = "Merchant should contact the bank.",
        [36] = "Restricted card.",
        [37] = "Payer id is not required.",
        [38] = "PIN tries exceeded.",
        [39] = "Invalid account.",
        [40] = "Original payer id was not found.",
        [41] = "Invalid card.",
        [42] = "No transaction was made for this card hash.",
        [43] = "Merchant transmission time is not consistent.",
        [44] = "Customer is not valid in the central system.",
        [45] = "Payer id is required.",
        [46] = "Seller account cannot be debited remotely.",
        [47] = "Account type cannot be credited or debited.",
        [48] = "Account cannot be credited or debited.",
        [49] = "Seller account is not valid.",
        [50] = "Correction date does not match the original transaction.",
        [51] = "Insufficient funds.",
        [52] = "Invalid settlement date format.",
        [53] = "Correction customer does not match the original transaction.",
        [54] = "Deposit is below the account opening minimum.",
        [55] = "Incorrect PIN.",
        [56] = "Customer information cannot be deleted.",
        [57] = "Card holder is not allowed to do this transaction.",
        [58] = "Terminal is not allowed to do this transaction.",
        [59] = "No charge PIN exists for this amount.",
        [60] = "Internal error while reading the charge PIN.",
        [61] = "Withdrawal amount exceeds the limit.",
        [62] = "Invalid goods code.",
        [63] = "Internal error.",
        [64] = "Customer contact information is not defined.",
        [65] = "Withdrawal count exceeds the limit.",
        [66] = "Customer branch is not defined.",
        [67] = "Remittance was not found.",
        [68] = "No response from the card issuer.",
        [69] = "Invalid destination account or card.",
        [70] = "Invalid user name or password.",
        [71] = "Invalid message format.",
        [72] = "Amount is smaller than the fee.",
        [73] = "Customer type is neither a person nor a company.",
        [74] = "Seller account conditions are not valid.",
        [75] = "Duplicate reserve code.",
        [76] = "Duplicate tracking number.",
        [77] = "Invalid business date.",
        [78] = "Source and destination account or card are the same.",
        [79] = "Inactive card.",
        [80] = "Merchant account is not defined.",
        [81] = "Transfer correction is not allowed.",
        [82] = "The card's main account is not connected to the terminal.",
        [83] = "Card issuer is inactive.",
        [84] = "Payer id is not correct.",
        [85] = "Invalid issuer transaction.",
        [86] = "Invalid bill organization.",
        [87] = "Contact the issuer bank.",
        [88] = "Invalid terminal.",
        [89] = "Invalid acquirer bank.",
        [90] = "End-of-day is running. Try again later.",
        [91] = "Card issuer is not operational.",
        [92] = "Invalid MAC.",
        [93] = "MAC mismatch.",
        [94] = "Duplicate transaction.",
        [95] = "Information mismatch.",
        [96] = "Communication keys are missing.",
        [97] = "Encryption key mismatch.",
        [98] = "System error. Try again.",
        [99] = "Contact support."
    };

    private static readonly Dictionary<string, ReturnCode> TerminalCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["FE"] = ReturnCode.ERR_POS_INVALID_DATA,
        ["FB"] = ReturnCode.ERR_POS_INVALID_INPUT_AMOUNT,
        ["FT"] = ReturnCode.ERR_POS_INVALID_MULTI_PAYMENT_AMOUNT,
        ["FC"] = ReturnCode.ERR_POS_INVALID_INPUT_PAYERID,
        ["FI"] = ReturnCode.ERR_POS_INVALID_INPUT_ADDITIONALDATA,
        ["FL"] = ReturnCode.ERR_POS_UNCONFIRM_REC_DATA,
        ["FU"] = ReturnCode.ERR_POS_USER_ABORT,
        ["FD"] = ReturnCode.ERR_POS_TO_SEND_TRANSACTION,
        ["FA"] = ReturnCode.ERR_POS_COMMUNICATION,
        ["FF"] = ReturnCode.ERR_POS_RESPONSE_RECEIVED_TOO_LATE,
        ["FH"] = ReturnCode.ERR_POS_INVALID_INPUT_ACCOUNTID,
        ["FG"] = ReturnCode.ERR_POS_INVALID_INPUT_BILL_ID,
        ["FP"] = ReturnCode.ERR_POS_INVALID_INPUT_PAYMENT_ID,
        ["FR"] = ReturnCode.ERR_POS_PC_CRCERROR_INVALID_DATA,
        ["FM"] = ReturnCode.ERR_POS_INVALID_INPUT_REFRENCE_NUMBER,
        ["FS"] = ReturnCode.ERR_POS_PC_OTHER,
        ["BB"] = ReturnCode.RET_POS_Busy,
        ["EA"] = ReturnCode.ERR_POS_TRANSACTION_NOT_FOUND
    };

    public static bool TryMapTerminalCode(string code, out ReturnCode returnCode) =>
        TerminalCodes.TryGetValue(code, out returnCode);

    public static string Describe(PosResult result)
    {
        var code = (ReturnCode)result.ReturnCode;
        var name = Enum.IsDefined(code) ? code.ToString() : "UNKNOWN";
        if (result.ReturnCode == (int)ReturnCode.ERR_POS_FAILED_TRANSACTION
            && int.TryParse(result.ReasonCode, out var reason)
            && Reasons.TryGetValue(reason, out var reasonText))
        {
            return $"{result.ReturnCode} {name}: switch {result.ReasonCode}, {reasonText}";
        }

        if (result.ReturnCode == (int)ReturnCode.ERR_POS_TRANSACTION_NOT_FOUND)
        {
            return $"{result.ReturnCode} {name}: the terminal has no successful POS-PC transaction for this inquiry.";
        }

        return $"{result.ReturnCode} {name}";
    }

    public static string? DescribeReason(string? reasonCode) =>
        int.TryParse(reasonCode, out var reason) && Reasons.TryGetValue(reason, out var text) ? text : null;
}
