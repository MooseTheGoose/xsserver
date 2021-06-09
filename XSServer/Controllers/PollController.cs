using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Cors;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.IO;
using System.Text;
using XSServer.Services;
using XSServer.Models;

namespace XSServer.Controllers {
  [EnableCors]
  [ApiController]
  [Route("[controller]")]
  public class PollController : ControllerBase {
    private readonly ILogger<PollController> _logger;
    private const int MaxPollTimeMs = 5000;

    public PollController(ILogger<PollController> logger) {
      _logger = logger;
    }

    public byte[] PollMessageMapping(byte[] message) {
      return Encoding.ASCII.GetBytes(Convert.ToBase64String(message) + "\n");
    }

    [HttpPost()]
    public async Task Subscribe([FromBody]XSSubscriber sub) {
      string key = null;
      byte[] error = null;
      sub.Key ??= "";
      if(! ControllerUtilities.TryExtractKey(sub.Key, out key, out error)) {
        Response.StatusCode = 400;
	await Response.Body.WriteAsync(error);
        await Response.Body.FlushAsync();
      } else {
        sub.Key = key;
        XSServiceData data = XSService.Subscribe(Response, sub);
        try {
          int datatime = data.Timestamp;
          int timestamp = datatime;
          await data.DequeueMessages(PollMessageMapping);
          while(data.Timestamp == datatime && ControllerUtilities.TimeAbsDifference(timestamp, datatime) < MaxPollTimeMs) {
            await ControllerUtilities.PollXSServiceFuture(data, 100, PollMessageMapping);
            timestamp = Environment.TickCount;
          }
          if(data.Timestamp == datatime) {
            Response.StatusCode = 404;
          }
          XSService.Unsubscribe(data);
        } catch {
          XSService.Unsubscribe(data);
        }
      }
    }


    [HttpGet]
    public ContentResult Get() {
      return new ContentResult {
        ContentType = "text/html",
        StatusCode = 200,
        Content = ""
      };
    }

  }
}
