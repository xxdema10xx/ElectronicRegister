using System;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

public sealed partial class Mt5bJwtTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DuplicateSignedJwtHeaderPropertiesAreRejected(bool selection)
    {
        var token = selection ? await Ticket() : await Access();
        var parts = token.Split('.');
        var header = Base64UrlEncoder.Decode(parts[0]);
        header = header[..^1] + ",\"alg\":\"HS256\"}";
        var unsigned = Base64UrlEncoder.Encode(header) + "." + parts[1];
        var forged = unsigned + "." + Base64UrlEncoder.Encode(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Mt3cHosting.Key), Encoding.ASCII.GetBytes(unsigned)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await Request(selection ? "/api/Auth/institutions" : "/api/Auth/context", forged)).StatusCode);
    }
}
