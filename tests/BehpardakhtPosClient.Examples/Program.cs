using BehpardakhtPosClient;
using BehpardakhtPosClient.Protocol;

var connection = PosConnection.Tcp("192.168.1.8", 1024, TimeSpan.FromSeconds(180), PosFraming.LegacyLibrary);
using var client = new PosClient(connection);

Console.WriteLine($"Terminal {connection.POS_IP}:{connection.POS_PORTtcp}");
if (args.Contains("ready-only"))
{
    Console.WriteLine("Sending Ready? ...");
    Print(await client.ReadyAsync());
    return;
}

if (args.Contains("plain"))
{
    connection.IncludeLibraryHeader = false;
}

if (args.Contains("inquiry"))
{
    Console.WriteLine("Last inquiry ...");
    Print(await client.LastInquiryAsync());
    return;
}
var requestId = DateTime.Now.ToString("HHmmss");
Console.WriteLine($"Purchase 50,000, request {requestId}. Present the card on the terminal.");
Print(await client.PurchaseAsync(50_000, requestId));

static void Print(PosResult result)
{
    Console.WriteLine(result);
    Console.WriteLine($"  approved: {result.IsApproved}");
    Console.WriteLine($"  pos code: {result.PosResponseCode}");
    Console.WriteLine($"  reason:   {result.ReasonCode} {result.ReasonDescription}");
    Console.WriteLine($"  trace:    {result.TraceNumber}");
    Console.WriteLine($"  serial:   {result.SerialTransaction}");
    Console.WriteLine($"  terminal: {result.TerminalNo}");
    Console.WriteLine($"  pan:      {result.PAN}");
    Console.WriteLine($"  account:  {result.AccountNo}");
    Console.WriteLine($"  amount:   {result.Amount}");
    Console.WriteLine($"  date:     {result.TransactionDate} {result.TransactionTime}");
    Console.WriteLine($"  raw:      {result.RawText}");
    Console.WriteLine($"  bytes:    {(result.RawResponse is null ? "" : Convert.ToHexString(result.RawResponse))}");
}
