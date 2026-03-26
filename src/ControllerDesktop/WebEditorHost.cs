using ControllerDesktop.Models;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ControllerDesktop.Services;

public sealed class WebEditorHost : IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly Func<AppConfiguration> _getConfiguration;
    private readonly Func<AppConfiguration, Task> _saveConfigurationAsync;
    private readonly Func<RuntimeStatus> _getStatus;
    private readonly Func<string> _getConfigPath;
    private readonly Action _startCapture;
    private readonly Func<CaptureSnapshot> _getCaptureStatus;
    private readonly Action _cancelCapture;
    private readonly Dictionary<string, (string ResourceName, string ContentType)> _resourceMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["/"] = ("ControllerDesktop.Web.index.html", "text/html; charset=utf-8"),
        ["/index.html"] = ("ControllerDesktop.Web.index.html", "text/html; charset=utf-8"),
        ["/app.css"] = ("ControllerDesktop.Web.app.css", "text/css; charset=utf-8"),
        ["/app.js"] = ("ControllerDesktop.Web.app.js", "application/javascript; charset=utf-8")
    };

    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _serverTask;

    public WebEditorHost(
        Func<AppConfiguration> getConfiguration,
        Func<AppConfiguration, Task> saveConfigurationAsync,
        Func<RuntimeStatus> getStatus,
        Func<string> getConfigPath,
        Action startCapture,
        Func<CaptureSnapshot> getCaptureStatus,
        Action cancelCapture)
    {
        _getConfiguration = getConfiguration;
        _saveConfigurationAsync = saveConfigurationAsync;
        _getStatus = getStatus;
        _getConfigPath = getConfigPath;
        _startCapture = startCapture;
        _getCaptureStatus = getCaptureStatus;
        _cancelCapture = cancelCapture;
    }

    public string EditorUrl { get; private set; } = string.Empty;

    public Task StartAsync()
    {
        if (_listener is not null)
        {
            return Task.CompletedTask;
        }

        var port = GetFreePort();
        EditorUrl = $"http://127.0.0.1:{port}/";
        _listener = new HttpListener();
        _listener.Prefixes.Add(EditorUrl);
        _listener.Start();

        _cts = new CancellationTokenSource();
        _serverTask = Task.Run(() => ListenLoopAsync(_cts.Token), _cts.Token);
        return Task.CompletedTask;
    }

    private async Task ListenLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener is not null)
        {
            HttpListenerContext? context = null;
            try
            {
                context = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                continue;
            }

            _ = Task.Run(() => HandleRequestAsync(context), cancellationToken);
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        try
        {
            var path = context.Request.Url?.AbsolutePath ?? "/";
            if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
            {
                await HandleApiAsync(context, path).ConfigureAwait(false);
                return;
            }

            if (_resourceMap.TryGetValue(path, out var resource))
            {
                await WriteResourceAsync(context.Response, resource.ResourceName, resource.ContentType).ConfigureAwait(false);
                return;
            }

            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            await WriteTextAsync(context.Response, "Not Found", "text/plain; charset=utf-8").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            await WriteJsonAsync(context.Response, new { message = ex.Message }).ConfigureAwait(false);
        }
        finally
        {
            context.Response.OutputStream.Close();
        }
    }

    private async Task HandleApiAsync(HttpListenerContext context, string path)
    {
        switch (path.ToLowerInvariant())
        {
            case "/api/config" when context.Request.HttpMethod == "GET":
                await WriteJsonAsync(context.Response, new
                {
                    configPath = _getConfigPath(),
                    configuration = _getConfiguration()
                }).ConfigureAwait(false);
                break;

            case "/api/config" when context.Request.HttpMethod == "PUT":
                var configuration = await JsonSerializer.DeserializeAsync<AppConfiguration>(context.Request.InputStream, SerializerOptions).ConfigureAwait(false);
                if (configuration is null)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    await WriteJsonAsync(context.Response, new { message = "配置内容无效。" }).ConfigureAwait(false);
                    break;
                }

                await _saveConfigurationAsync(configuration).ConfigureAwait(false);
                await WriteJsonAsync(context.Response, new { saved = true, configPath = _getConfigPath() }).ConfigureAwait(false);
                break;

            case "/api/status" when context.Request.HttpMethod == "GET":
                await WriteJsonAsync(context.Response, _getStatus()).ConfigureAwait(false);
                break;

            case "/api/capture/start" when context.Request.HttpMethod == "POST":
                _startCapture();
                await WriteJsonAsync(context.Response, new { started = true }).ConfigureAwait(false);
                break;

            case "/api/capture" when context.Request.HttpMethod == "GET":
                await WriteJsonAsync(context.Response, _getCaptureStatus()).ConfigureAwait(false);
                break;

            case "/api/capture/cancel" when context.Request.HttpMethod == "POST":
                _cancelCapture();
                await WriteJsonAsync(context.Response, new { cancelled = true }).ConfigureAwait(false);
                break;

            default:
                context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                await WriteJsonAsync(context.Response, new { message = "不支持的请求。" }).ConfigureAwait(false);
                break;
        }
    }

    private static async Task WriteResourceAsync(HttpListenerResponse response, string resourceName, string contentType)
    {
        response.ContentType = contentType;
        response.Headers["Cache-Control"] = "no-store";

        await using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"找不到资源 {resourceName}");
        response.ContentLength64 = stream.Length;
        await stream.CopyToAsync(response.OutputStream).ConfigureAwait(false);
    }

    private static Task WriteJsonAsync(HttpListenerResponse response, object payload)
    {
        var json = JsonSerializer.Serialize(payload, SerializerOptions);
        return WriteTextAsync(response, json, "application/json; charset=utf-8");
    }

    private static async Task WriteTextAsync(HttpListenerResponse response, string text, string contentType)
    {
        response.ContentType = contentType;
        response.Headers["Cache-Control"] = "no-store";
        var buffer = Encoding.UTF8.GetBytes(text);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
    }

    private static int GetFreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    public void Dispose()
    {
        if (_cts is not null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        if (_listener is not null)
        {
            _listener.Stop();
            _listener.Close();
            _listener = null;
        }
    }
}
