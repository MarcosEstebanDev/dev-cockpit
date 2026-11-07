using System.Net;
using System.Text.Json;
using DevCockpit.Core.Domain;
using DevCockpit.Core.Ports;
using DevCockpit.Infrastructure.Llm;

namespace DevCockpit.Infrastructure.Tests.Llm;

public class ProvidersTests
{
    private readonly FakeHttpHandler _handler = new();
    private HttpClient Http => new(_handler);
    private readonly CancellationToken _ct = CancellationToken.None;

    private sealed class MemorySecrets : ISecretStore
    {
        public Dictionary<string, string> Values { get; } = [];
        public string? Get(string key) => Values.GetValueOrDefault(key);
        public void Set(string key, string value) => Values[key] = value;
        public void Delete(string key) => Values.Remove(key);
    }

    [Fact]
    public async Task Ollama_sends_model_and_returns_response()
    {
        _handler.ResponseBody = """{"model":"qwen2.5:7b-instruct","response":"resumen listo","done":true}""";
        var ollama = new OllamaProvider(Http, "http://localhost:11434", "qwen2.5:7b-instruct");

        var text = await ollama.CompleteAsync("hola", _ct);

        Assert.Equal("resumen listo", text);
        Assert.Equal("http://localhost:11434/api/generate", _handler.LastRequest!.RequestUri!.ToString());
        using var body = JsonDocument.Parse(_handler.LastBody);
        Assert.Equal("qwen2.5:7b-instruct", body.RootElement.GetProperty("model").GetString());
        Assert.Equal("hola", body.RootElement.GetProperty("prompt").GetString());
        Assert.False(body.RootElement.GetProperty("stream").GetBoolean());
        Assert.True(ollama.IsLocal);
    }

    [Fact]
    public async Task Ollama_url_with_trailing_slash_is_normalized()
    {
        _handler.ResponseBody = """{"response":"ok"}""";
        var ollama = new OllamaProvider(Http, "http://localhost:11434/", "m");

        await ollama.CompleteAsync("hola", _ct);

        Assert.Equal("http://localhost:11434/api/generate", _handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task Ollama_connection_refused_throws_LlmException()
    {
        _handler.Throw = new HttpRequestException("No se puede establecer una conexión");
        var ollama = new OllamaProvider(Http, "http://localhost:11434", "qwen2.5:7b-instruct");

        var ex = await Assert.ThrowsAsync<LlmException>(() => ollama.CompleteAsync("hola", _ct));
        Assert.Contains("Ollama", ex.Message);
    }

    [Fact]
    public async Task Ollama_unknown_model_throws_LlmException_with_server_message()
    {
        _handler.Status = HttpStatusCode.NotFound;
        _handler.ResponseBody = """{"error":"model 'nada' not found"}""";
        var ollama = new OllamaProvider(Http, "http://localhost:11434", "nada");

        var ex = await Assert.ThrowsAsync<LlmException>(() => ollama.CompleteAsync("hola", _ct));
        Assert.Contains("model 'nada' not found", ex.Message);
    }

    [Fact]
    public async Task Anthropic_sends_key_header_and_parses_text()
    {
        _handler.ResponseBody = """
            {"id":"msg_1","type":"message","role":"assistant","model":"claude-opus-5-5",
             "content":[{"type":"thinking","thinking":"","signature":"x"},{"type":"text","text":"Hola desde Claude"}],
             "stop_reason":"end_turn","stop_sequence":null,"usage":{"input_tokens":3,"output_tokens":4}}
            """;
        var claude = new AnthropicProvider(Http, "sk-ant-test", "claude-opus-5-5");

        var text = await claude.CompleteAsync("hola", _ct);

        Assert.Equal("Hola desde Claude", text);
        // El SDK 13.x manda la key como Bearer (no en x-api-key); lo que importa es que sea la nuestra.
        Assert.Equal("Bearer sk-ant-test", _handler.LastRequest!.Headers.Authorization!.ToString());
        Assert.EndsWith("/v1/messages", _handler.LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("claude-opus-5-5", _handler.LastBody);
        Assert.False(claude.IsLocal);
    }

    [Fact]
    public async Task Anthropic_refusal_throws_LlmException()
    {
        _handler.ResponseBody = """
            {"id":"msg_1","type":"message","role":"assistant","model":"claude-opus-5-5","content":[],
             "stop_reason":"refusal","stop_sequence":null,"usage":{"input_tokens":3,"output_tokens":0}}
            """;
        var claude = new AnthropicProvider(Http, "sk-ant-test", "claude-opus-5-5");

        await Assert.ThrowsAsync<LlmException>(() => claude.CompleteAsync("hola", _ct));
    }

    [Fact]
    public async Task Anthropic_unauthorized_throws_LlmException()
    {
        _handler.Status = HttpStatusCode.Unauthorized;
        _handler.ResponseBody = """{"type":"error","error":{"type":"authentication_error","message":"invalid x-api-key"}}""";
        var claude = new AnthropicProvider(Http, "mala", "claude-opus-5-5");

        await Assert.ThrowsAsync<LlmException>(() => claude.CompleteAsync("hola", _ct));
    }

    [Fact]
    public async Task OpenAI_parses_first_choice()
    {
        _handler.ResponseBody = """{"choices":[{"index":0,"message":{"role":"assistant","content":"Hola desde GPT"}}]}""";
        var gpt = new OpenAIProvider(Http, "sk-test", "gpt-test");

        var text = await gpt.CompleteAsync("hola", _ct);

        Assert.Equal("Hola desde GPT", text);
        Assert.Equal("Bearer sk-test", _handler.LastRequest!.Headers.Authorization!.ToString());
        Assert.Equal("https://api.openai.com/v1/chat/completions", _handler.LastRequest.RequestUri!.ToString());
        Assert.Contains("\"gpt-test\"", _handler.LastBody);
    }

    [Fact]
    public async Task Non_success_status_throws_LlmException()
    {
        _handler.Status = HttpStatusCode.InternalServerError;
        var gpt = new OpenAIProvider(Http, "sk-test", "gpt-test");

        await Assert.ThrowsAsync<LlmException>(() => gpt.CompleteAsync("hola", _ct));
    }

    [Fact]
    public async Task Unexpected_json_throws_LlmException()
    {
        _handler.ResponseBody = """{"algo":"distinto"}""";
        var ollama = new OllamaProvider(Http, "http://localhost:11434", "m");

        await Assert.ThrowsAsync<LlmException>(() => ollama.CompleteAsync("hola", _ct));
    }

    [Fact]
    public void Factory_None_returns_null()
    {
        var factory = new LlmProviderFactory(Http, new MemorySecrets());

        Assert.Null(factory.Create(new AppSettings { Provider = LlmProviderKind.None }));
    }

    [Fact]
    public void Factory_Ollama_needs_no_key()
    {
        var factory = new LlmProviderFactory(Http, new MemorySecrets());

        Assert.IsType<OllamaProvider>(factory.Create(new AppSettings()));
    }

    [Fact]
    public void Factory_cloud_without_key_throws()
    {
        var factory = new LlmProviderFactory(Http, new MemorySecrets());

        var ex = Assert.Throws<LlmException>(() => factory.Create(new AppSettings { Provider = LlmProviderKind.Anthropic }));
        Assert.Contains("API key", ex.Message);
    }

    [Fact]
    public void Factory_openai_without_model_throws()
    {
        var secrets = new MemorySecrets();
        secrets.Set(LlmProviderFactory.SecretKey(LlmProviderKind.OpenAI), "sk");
        var factory = new LlmProviderFactory(Http, secrets);

        Assert.Throws<LlmException>(() => factory.Create(new AppSettings { Provider = LlmProviderKind.OpenAI }));
    }

    [Fact]
    public void Factory_uses_stored_key()
    {
        var secrets = new MemorySecrets();
        secrets.Set(LlmProviderFactory.SecretKey(LlmProviderKind.Anthropic), "sk-ant");
        var factory = new LlmProviderFactory(Http, secrets);

        Assert.IsType<AnthropicProvider>(factory.Create(new AppSettings { Provider = LlmProviderKind.Anthropic }));
    }
}
