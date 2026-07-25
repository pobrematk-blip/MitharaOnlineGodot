using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Mithara.Web.Services;

namespace Mithara.Web.Controllers;

[Route("api/site-status")]
[ApiController]
public class SiteStatusController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly GameDbService _gameDb;

    public SiteStatusController(GameDbService gameDb)
    {
        _gameDb = gameDb;
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Get()
    {
        return Ok(_gameDb.GetSiteStatusSnapshot());
    }

    [HttpGet("stream")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task Stream(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-cache, no-store";
        Response.Headers.Connection = "keep-alive";
        Response.ContentType = "text/event-stream";

        while (!cancellationToken.IsCancellationRequested)
        {
            var payload = JsonSerializer.Serialize(_gameDb.GetSiteStatusSnapshot(), JsonOptions);
            await Response.WriteAsync($"event: status\ndata: {payload}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
        }
    }
}
