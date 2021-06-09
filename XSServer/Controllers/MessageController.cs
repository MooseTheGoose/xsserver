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
  public class MessageController : ControllerBase {
    private readonly ILogger<MessageController> _logger;
    public const int MaxMessageLength = 0x10000;

    public MessageController(ILogger<MessageController> logger) {
      _logger = logger;
    }

    [HttpPost()]
    [Consumes("application/octet-stream", "text/plain")]
    public async Task Message([FromQuery] string key = "") {
      byte[] error = null;
      if(! ControllerUtilities.TryExtractKey(key, out key, out error)) {
	Response.StatusCode = 400;
        await Response.Body.WriteAsync(error);
      } else if(Request.ContentLength > MaxMessageLength) {
	Response.StatusCode = 400;
        error = Encoding.UTF8.GetBytes("Request is too long. Must be " 
          + MaxMessageLength + " bytes or less\n");
	await Response.Body.WriteAsync(error);
      } else {
        byte[] message = new byte[(int)Request.ContentLength];
        await Request.Body.ReadAsync(message);
        await Task.Run(() => XSService.SendMessage(message, key));
      }	
    }
  }
}
