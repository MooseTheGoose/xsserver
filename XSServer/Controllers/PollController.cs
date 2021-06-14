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
    private const int PollInterval = 100;

    public PollController(ILogger<PollController> logger) {
      _logger = logger;
    }

    public byte[] PollMessageMapping(byte[] message) {
      return Encoding.ASCII.GetBytes(Convert.ToBase64String(message) + "\n");
    }

    private async Task PollMainLoop(XSServiceData data, XSSubscriber sub, object webData) {
      int datatime = data.Timestamp;
      int timestamp = datatime;
      HttpResponse response = (HttpResponse)webData;
      await data.DequeueMessages();
      while(data.Timestamp == datatime && ControllerUtilities.TimeAbsDifference(timestamp, datatime) < MaxPollTimeMs) {
        await ControllerUtilities.PollXSServiceFuture(data, PollInterval);
        timestamp = Environment.TickCount;
      }
      if(data.Timestamp == datatime) {
        response.StatusCode = 404;
      }
    }

    [HttpPost()]
    public async Task Subscribe([FromBody]XSSubscriber sub) {
      XSServiceCallbacks callbacks = new XSServiceCallbacks {
        writeCallback = ControllerUtilities.WriteToResponseTaskGenerator(Response),
        flushCallback = ControllerUtilities.FlushResponseTaskGenerator(Response),
        messageMapping = PollMessageMapping
      };
      await ControllerUtilities.RunSubscriber(sub, Response, (object)Response, callbacks, PollMainLoop);
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
