using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;

class Program
{
    static async Task Main()
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes("cd9ced556fa43a67fe6378315913b406d1cfd24430375e007bce349cba7ff0a1");
        
        var claims = new[]
        {
            new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/sid", "8f8f7fc5-eb4c-44da-877c-dbea2c2e6b20"),
            new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", "admin"),
            new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name", "Admin"),
            new Claim("http://schemas.OpassFab.com/identity/claims/UserNameKana", ""),
            new Claim("http://schemas.OpassFab.com/identity/claims/TenantCode", "ctjhxvyi4J"),
            new Claim("http://schemas.OpassFab.com/identity/claims/CtrlerCd", "67890"),
            new Claim("http://schemas.OpassFab.com/identity/claims/RealmName", "tenant1"),
            new Claim("http://schemas.OpassFab.com/identity/claims/InternalApi", "true")
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddDays(30),
            Issuer = "OpassFab",
            Audience = "OpassFab",
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);
        
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenString);
        
        Console.WriteLine("=================================================");
        Console.WriteLine("       AC03001 & AD08001 API TEST SUITE          ");
        Console.WriteLine("=================================================\n");

        // ---------------- AC03001 TESTS ----------------
        Console.WriteLine("--- AC03001: 1. GET /api/master/form-config/AC03001 ---");
        await TestApiAsync(client, HttpMethod.Get, "http://localhost:7192/api/master/form-config/AC03001");

        Console.WriteLine("\n--- AC03001: 2. POST /api/sales/shipment-assign/ac03001/search (Unassigned mode) ---");
        var searchAc03001Wait = @"{ ""shipmentAssignStatus"": ""0"" }";
        await TestApiAsync(client, HttpMethod.Post, "http://localhost:7192/api/sales/shipment-assign/ac03001/search", searchAc03001Wait);

        Console.WriteLine("\n--- AC03001: 3. POST /api/sales/shipment-assign/ac03001/search (Assigned mode) ---");
        var searchAc03001Done = @"{ ""shipmentAssignStatus"": ""1"" }";
        await TestApiAsync(client, HttpMethod.Post, "http://localhost:7192/api/sales/shipment-assign/ac03001/search", searchAc03001Done);

        Console.WriteLine("\n--- AC03001: 4. GET /api/sales/shipment-assign/ac03001/qtty-conversion ---");
        await TestApiAsync(client, HttpMethod.Get, "http://localhost:7192/api/sales/shipment-assign/ac03001/qtty-conversion?qtty=10&origUnitCd=EA&destUnitCd=EA&articleCd=ART01");

        Console.WriteLine("\n--- AC03001: 5. GET /api/sales/shipment-assign/ac03001/pkg-qtty-conversion ---");
        await TestApiAsync(client, HttpMethod.Get, "http://localhost:7192/api/sales/shipment-assign/ac03001/pkg-qtty-conversion?qtty=10&origUnitCd=EA&pkgUnitCd=BOX&articleCd=ART01");

        Console.WriteLine("\n--- AC03001: 6. GET /api/sales/shipment-assign/ac03001/lot-qtty-conversion ---");
        await TestApiAsync(client, HttpMethod.Get, "http://localhost:7192/api/sales/shipment-assign/ac03001/lot-qtty-conversion?qtty=10&origUnitCd=EA&destUnitCd=EA&articleCd=ART01");

        Console.WriteLine("\n--- AC03001: 7. POST /api/sales/shipment-assign/ac03001 (Registration) ---");
        var createAc03001Body = @"{
            ""isShipmentInstructionGrouped"": true,
            ""shipmentAssignDtlList"": [
                {
                    ""salesOrderNo"": ""SO99999"",
                    ""salesOrderRowNo"": 1,
                    ""modelState"": ""Added"",
                    ""ctrlerCd"": ""67890"",
                    ""shipmentAssignDate"": ""2026-07-22T00:00:00Z"",
                    ""shipmentDate"": ""2026-07-22T00:00:00Z"",
                    ""shipmentAssignQtty"": 5,
                    ""deliveryScheduledDate"": ""2026-07-25T00:00:00Z"",
                    ""deliveryScheduledTimeSec"": ""1"",
                    ""carrierCd"": ""101""
                }
            ]
        }";
        await TestApiAsync(client, HttpMethod.Post, "http://localhost:7192/api/sales/shipment-assign/ac03001", createAc03001Body);

        Console.WriteLine("\n--- AC03001: 8. PUT /api/sales/shipment-assign/ac03001 (Modify) ---");
        var modifyAc03001Body = @"{
            ""shipmentAssignDtlList"": [
                {
                    ""shipmentAssignNo"": ""SA99999"",
                    ""shipmentAssignRowNo"": 1,
                    ""salesOrderNo"": ""SO99999"",
                    ""salesOrderRowNo"": 1,
                    ""modelState"": ""Modified"",
                    ""ctrlerCd"": ""67890"",
                    ""shipmentAssignDate"": ""2026-07-22T00:00:00Z"",
                    ""shipmentDate"": ""2026-07-22T00:00:00Z"",
                    ""shipmentAssignQtty"": 5,
                    ""deliveryScheduledDate"": ""2026-07-25T00:00:00Z"",
                    ""deliveryScheduledTimeSec"": ""1"",
                    ""carrierCd"": ""101"",
                    ""shipmentAssignRv"": 1,
                    ""dtlRv"": 1
                }
            ]
        }";
        await TestApiAsync(client, HttpMethod.Put, "http://localhost:7192/api/sales/shipment-assign/ac03001", modifyAc03001Body);

        // ---------------- AD08001 TESTS ----------------
        Console.WriteLine("\n\n--- AD08001: 1. GET /api/master/form-config/AD08001 ---");
        await TestApiAsync(client, HttpMethod.Get, "http://localhost:7192/api/master/form-config/AD08001");

        Console.WriteLine("\n--- AD08001: 2. POST /api/sales/shipment/ad08001/search (Inspection pending) ---");
        var searchAd08001Wait = @"{ ""baseCd"": ""B01"", ""salesInspectionSec"": ""1"" }";
        await TestApiAsync(client, HttpMethod.Post, "http://localhost:7192/api/sales/shipment/ad08001/search", searchAd08001Wait);

        Console.WriteLine("\n--- AD08001: 3. POST /api/sales/shipment/ad08001/search (Inspection completed) ---");
        var searchAd08001Done = @"{ ""baseCd"": ""B01"", ""salesInspectionSec"": ""2"" }";
        await TestApiAsync(client, HttpMethod.Post, "http://localhost:7192/api/sales/shipment/ad08001/search", searchAd08001Done);

        Console.WriteLine("\n--- AD08001: 4. POST /api/sales/shipment/ad08001/detail-amount-and-tax ---");
        var recalcAd08001Body = @"{
            ""inspectionQtty"": 10,
            ""inspectionUnitPrice"": 100,
            ""taxationMethodSec"": ""1"",
            ""taxRate"": 0.1,
            ""currencyCd"": ""JPY"",
            ""currencyRate"": 1,
            ""clientCd"": ""CL01"",
            ""inspectionDate"": ""2026-07-22""
        }";
        await TestApiAsync(client, HttpMethod.Post, "http://localhost:7192/api/sales/shipment/ad08001/detail-amount-and-tax", recalcAd08001Body);

        Console.WriteLine("\n--- AD08001: 5. POST /api/sales/shipment/ad08001 (New Inspection) ---");
        var createAd08001Body = @"{
            ""salesInspectionList"": [
                {
                    ""rowNo"": 1,
                    ""modelState"": ""Added"",
                    ""salesOrderNo"": ""SO99999"",
                    ""salesOrderDtlNo"": 1,
                    ""inspectionCtrlerCd"": ""67890"",
                    ""inspectionDate"": ""2026-07-22T00:00:00Z"",
                    ""billingBaseDate"": ""2026-07-22T00:00:00Z"",
                    ""inspectionQtty"": 5,
                    ""inspectionUnitPrice"": 100,
                    ""currencyRate"": 1,
                    ""adjustmentAmount"": 0
                }
            ]
        }";
        await TestApiAsync(client, HttpMethod.Post, "http://localhost:7192/api/sales/shipment/ad08001", createAd08001Body);

        Console.WriteLine("\n--- AD08001: 6. PUT /api/sales/shipment/ad08001 (Modify Inspection) ---");
        var modifyAd08001Body = @"{
            ""salesInspectionList"": [
                {
                    ""rowNo"": 1,
                    ""modelState"": ""Modified"",
                    ""salesOrderNo"": ""SO99999"",
                    ""salesOrderDtlNo"": 1,
                    ""salesInspectionNo"": ""SI99999"",
                    ""salesInspectionDtlNo"": 1,
                    ""salesInspectionRv"": ""1"",
                    ""salesInspectionDtlRv"": ""1"",
                    ""inspectionCtrlerCd"": ""67890"",
                    ""inspectionDate"": ""2026-07-22T00:00:00Z"",
                    ""billingBaseDate"": ""2026-07-22T00:00:00Z"",
                    ""inspectionQtty"": 5,
                    ""inspectionUnitPrice"": 100,
                    ""currencyRate"": 1,
                    ""adjustmentAmount"": 0
                }
            ]
        }";
        await TestApiAsync(client, HttpMethod.Put, "http://localhost:7192/api/sales/shipment/ad08001", modifyAd08001Body);

        Console.WriteLine("\n=================================================");
        Console.WriteLine("              TEST SUITE COMPLETE                ");
        Console.WriteLine("=================================================");
    }

    static async Task TestApiAsync(HttpClient client, HttpMethod method, string url, string? jsonBody = null)
    {
        try
        {
            using var req = new HttpRequestMessage(method, url);
            if (!string.IsNullOrEmpty(jsonBody))
            {
                req.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            }
            var resp = await client.SendAsync(req);
            Console.WriteLine($"Status: {(int)resp.StatusCode} {resp.StatusCode}");
            var body = await resp.Content.ReadAsStringAsync();
            if (body.Length > 500)
                body = body.Substring(0, 500) + "... [truncated]";
            Console.WriteLine($"Response: {body}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}
