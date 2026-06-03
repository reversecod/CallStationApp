namespace CallStationApp.Middleware;

public class RespostasAutorizacaoAjaxMiddleware
{
    private const string MensagemNaoAutenticado = "Você precisa estar logado para executar esta ação.";
    private const string MensagemSemPermissao = "Você não tem permissão para executar esta ação.";

    private readonly RequestDelegate _next;

    public RespostasAutorizacaoAjaxMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        if (context.Response.HasStarted || !EhRequisicaoAjaxOuJson(context.Request))
            return;

        var statusAutorizacao = ObterStatusAutorizacao(context.Response);
        if (statusAutorizacao == null)
            return;

        context.Response.Clear();
        context.Response.StatusCode = statusAutorizacao.Value;
        context.Response.ContentType = "application/json; charset=utf-8";

        var mensagem = statusAutorizacao.Value == StatusCodes.Status401Unauthorized
            ? MensagemNaoAutenticado
            : MensagemSemPermissao;

        await context.Response.WriteAsJsonAsync(new
        {
            success = false,
            message = mensagem
        });
    }

    private static int? ObterStatusAutorizacao(HttpResponse response)
    {
        if (response.StatusCode is StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden)
            return response.StatusCode;

        if (response.StatusCode != StatusCodes.Status302Found)
            return null;

        var location = response.Headers.Location.ToString();
        if (string.IsNullOrWhiteSpace(location))
            return null;

        if (location.Contains("/Auth/Login", StringComparison.OrdinalIgnoreCase))
            return StatusCodes.Status401Unauthorized;

        if (location.Contains("AccessDenied", StringComparison.OrdinalIgnoreCase))
            return StatusCodes.Status403Forbidden;

        return null;
    }

    private static bool EhRequisicaoAjaxOuJson(HttpRequest request)
    {
        if (HeaderContem(request, "Accept", "application/json") ||
            HeaderContem(request, "Accept", "text/json") ||
            HeaderContem(request, "X-Requested-With", "XMLHttpRequest") ||
            ContentTypeContem(request, "application/json"))
        {
            return true;
        }

        return request.Query.ContainsKey("handler") && !HeaderContem(request, "Accept", "text/html");
    }

    private static bool HeaderContem(HttpRequest request, string nome, string valor)
    {
        return request.Headers.TryGetValue(nome, out var valores) &&
               valores.Any(item => item?.Contains(valor, StringComparison.OrdinalIgnoreCase) == true);
    }

    private static bool ContentTypeContem(HttpRequest request, string valor)
    {
        return request.ContentType?.Contains(valor, StringComparison.OrdinalIgnoreCase) == true;
    }
}

public static class RespostasAutorizacaoAjaxMiddlewareExtensions
{
    public static IApplicationBuilder UseRespostasAutorizacaoAjax(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RespostasAutorizacaoAjaxMiddleware>();
    }
}
