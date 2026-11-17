# BehpardakhtPosClient (Unofficial)

Unofficial .NET client for a Behpardakht Mellat POS devices terminal. It is not published or supported by Behpardakht. Amounts are Iranian rials. One `PosClient` talks to one terminal and runs one call at a time.

## Documents

The implementation follows two Behpardakht documents in [`docs/`](docs/):

- [POS_PC Library Guide Ver 1.4.47 .NET.pdf](docs/POS_PC%20Library%20Guide%20Ver%201.4.47%20.NET.pdf) defines the library surface: connection property names, `Transaction` method names such as `Debits_Goods_And_Service`, return codes (100 and above), and timeouts. `Transaction` in this SDK keeps those names. `PosClient` is the async form of the same calls.
- [POS PC VIII_010447- Protocol.pdf](docs/POS%20PC%20VIII_010447-%20Protocol.pdf) is protocol revision 4.7. It defines the TLV tags, service codes, CRC-16, BCD and ASCII fields, and the frame whose 4 ASCII digits are the body length. That framing is `PosFraming.Protocol47`, the default.

`PosFraming.LegacyLibrary` is the frame used by POS_PC.dll 1.4.47: the length digits count the whole message, and the body starts with the library type and version. Terminals that answer the 1.4.47 library use this framing.

## Install

From a packed folder:

```bash
dotnet pack BehpardakhtPosClient.csproj -c Release -o ./nupkgs
dotnet add package BehpardakhtPosClient --source ./nupkgs
```

from [Nuget](https://www.nuget.org/packages/BehpardakhtPosClient):

```bash
dotnet add package BehpardakhtPosClient
```

## Purchase

Terminals that work with POS_PC.dll 1.4.47 use `PosFraming.LegacyLibrary`. The default framing is protocol 4.7.

```csharp
using BehpardakhtPosClient;
using BehpardakhtPosClient.Protocol;

var connection = PosConnection.Tcp(
    "192.168.1.8",
    1024,
    TimeSpan.FromSeconds(180),
    PosFraming.LegacyLibrary);

using var client = new PosClient(connection);
var result = await client.PurchaseAsync(50_000, requestId: "123456");

if (result.IsApproved)
{
    Console.WriteLine($"{result.TraceNumber} {result.PAN} {result.Amount}");
}
else
{
    Console.WriteLine($"{result.ReturnCode} {result.PosResponseCode}");
}
```

`ReturnCode` 100 is success. Two-digit switch codes are reported on `ReasonCode`. A terminal code such as `FD` is `112`: the terminal did not send the transaction. Do not retry a purchase automatically; use `InquiryAsync` with the same request id.

Serial uses `PosConnection.Serial("COM1")`. The blocking API with the POS_PC.dll method names is `Transaction`.