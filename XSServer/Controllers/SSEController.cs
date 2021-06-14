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
  public class SSEController : ControllerBase {
    private readonly ILogger<PollController> _logger;
    private const int SSEPingTimeMs = 20000;
    private const int PollInterval = 200;

    public SSEController(ILogger<PollController> logger) {
      _logger = logger;
    }

    public byte[] SSEMessageMapping(byte[] message) {
      return Encoding.ASCII.GetBytes("event: message\ndata: " + Convert.ToBase64String(message) + "\n\n");
    }

    private async Task SSEMainLoop(XSServiceData data, XSSubscriber sub, object webData) {
      HttpResponse response = (HttpResponse)webData;
      Response.Headers.Add("Content-Type", "text/event-stream");
      await data.DequeueMessages();
      int prevDataTick = data.Timestamp;
      while(! response.HttpContext.RequestAborted.IsCancellationRequested) {
        if(Environment.TickCount > ((long)prevDataTick + SSEPingTimeMs)) {
          await Response.Body.WriteAsync(Encoding.ASCII.GetBytes("event: ping\ndata: \n\n"));
          prevDataTick = Environment.TickCount;
        } else {
          await ControllerUtilities.PollXSServiceFuture(data, PollInterval);
          if(prevDataTick < data.Timestamp) {
            prevDataTick = data.Timestamp;
          }
        }
      }
    }

    [HttpPost()]
    public async Task Subscribe([FromBody]XSSubscriber sub) {
      XSServiceCallbacks callbacks = new XSServiceCallbacks {
        writeCallback = ControllerUtilities.WriteToResponseTaskGenerator(Response),
        flushCallback = ControllerUtilities.FlushResponseTaskGenerator(Response),
        messageMapping = SSEMessageMapping
      };
      await ControllerUtilities.RunSubscriber(sub, Response, (object)Response, callbacks, SSEMainLoop);
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
