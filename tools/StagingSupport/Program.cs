using System.Net;
using System.Text.Json;

// Operational helper, not an application endpoint or alternate IAM path.
if(args.Length!=2)return 2;
using var input=JsonDocument.Parse(await File.ReadAllTextAsync(args[1]));
if(args[0]=="health")
{
    using var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(4)};
    using var request=new HttpRequestMessage(HttpMethod.Get,"http://127.0.0.1:8080/health/ready");
    request.Headers.Host=input.RootElement.GetProperty("AllowedHosts").GetString();
    // Only the container's loopback is trusted for its local process probe.
    request.Headers.Add("X-Forwarded-Proto","https");
    try{using var response=await client.SendAsync(request);return response.StatusCode==HttpStatusCode.OK?0:1;}
    catch(HttpRequestException){return 1;}catch(TaskCanceledException){return 1;}
}
if(args[0]=="hashes")
{
    var result=input.RootElement.GetProperty("accounts").EnumerateObject().ToDictionary(p=>p.Name,p=>BCrypt.Net.BCrypt.HashPassword(p.Value.GetProperty("password").GetString()!,workFactor:11));
    Console.WriteLine(JsonSerializer.Serialize(result));return 0;
}
return 2;
