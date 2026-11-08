using System.Net;
using System.Text;

namespace DevCockpit.Infrastructure.Tests.Llm;

/// <summary>Simula un servidor HTTP: guarda el último request y devuelve la respuesta configurada.</summary>
public sealed class FakeHttpHandler : HttpMessageHandler
{
    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
    public string ResponseBody { get; set; } = "{}";
    public Exception? Throw { get; set; }

    public HttpRequestMessage? LastRequest { get; private set; }
    public string LastBody { get; private set; } = "";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        LastRequest = request;
        LastBody = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
        if (Throw is not null) throw Throw;
        return new HttpResponseMessage(Status) { Content = new StringContent(ResponseBody, Encoding.UTF8, "application/json") };
    }
}
