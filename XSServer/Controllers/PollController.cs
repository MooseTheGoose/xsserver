using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.IO;
using System.Text;
using XSServer.Services;
using XSServer.Models;

namespace XSServer.Controllers {
  [ApiController]
  [Route("[controller]")]
  public class PollController : ControllerBase {
    private readonly ILogger<WeatherForecastController> _logger;
    private const int MaxPollTimeMs = 5000;

    public PollController(ILogger<WeatherForecastController> logger) {
      _logger = logger;
    }

    [HttpPost("sub")]
    public async Task Subscribe([FromBody]XSSubscriber sub) {
      sub.Key = ControllerUtilities.ExtractKey(sub.Key);
      if(sub.Key == null || sub.Key.Length > ControllerUtilities.MaxKeyLen) {
        return;
      }
      XSServiceData data = XSService.Subscribe(Response, sub);
      try {
	int datatime = data.Timestamp;
	int timestamp = datatime;
	await data.DequeueMessages();
	while(data.Timestamp == datatime && ControllerUtilities.TimeAbsDifference(timestamp, datatime) < MaxPollTimeMs) {
	  await ControllerUtilities.PollXSServiceFuture(data, 100);
	  timestamp = Environment.TickCount;
	}
        XSService.Unsubscribe(data);
      } catch {
        XSService.Unsubscribe(data);
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


    [HttpPost("message")]
    public async Task Message([FromQuery] string key) {
      key = ControllerUtilities.ExtractKey(key);
      if(key == null || key.Length > ControllerUtilities.MaxKeyLen) {
        return;
      }
      if(Request.ContentLength >= 65536) {
        return;
      }
      byte[] message = new byte[(int)Request.ContentLength];
      await Request.Body.ReadAsync(message, 0, message.Length);
      Response.Headers.Add("Access-Control-Allow-Origin", "*");
      await Task.Run(() => XSService.SendMessage(message, key)); 
    }
  }
}
