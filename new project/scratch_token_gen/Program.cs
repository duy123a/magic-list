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
        Console.WriteLine("          AD08001 SEARCH API TEST                ");
        Console.WriteLine("=================================================\n");

        Console.WriteLine("--- POST /api/sales/shipment/ad08001/search ---");
        var searchBody = @"{
            ""baseCd"": ""hn"",
            ""salesInspectionSec"": ""1"",
            ""shipmentDateFrom"": null,
            ""shipmentDateTo"": null,
            ""shipmentNo"": null,
            ""shipmentCtrlerCd"": null,
            ""clientCd"": null,
            ""deliveryDestCd"": null,
            ""salesOrderDateFrom"": null,
            ""salesOrderDateTo"": null,
            ""salesOrderNo"": null,
            ""articleCd"": null,
            ""articleName1"": null,
            ""articleName2"": null,
            ""earmarkingSalesSec"": null,
            ""transactionSec"": null
        }";
        await TestApiAsync(client, HttpMethod.Post, "http://localhost:7192/api/sales/shipment/ad08001/search", searchBody);

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
