using System;
using System.Timers;
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
  public static class ControllerUtilities {
    private static Regex hexregex = new Regex("^(0[xX])?([0-9a-fA-F]*)$");
    public const int MaxKeyLen = 128;
    private const int TimeOverflowMask = int.MaxValue;
    private const int TimeOverflow = 0xFFFFF;

    public static bool TryExtractKey(string payload, out string key, out byte[] error) {
      MatchCollection mc = hexregex.Matches(payload);
      bool success = false;
      key = null;
      error = null;
      if(mc.Count != 0) {
        GroupCollection gc = mc[0].Groups;
        key = gc[gc.Count - 1].Value.ToUpper();
        if(key.Length <= MaxKeyLen) {
          success = true;
        } else {
	  error = Encoding.ASCII.GetBytes("Key is too long  (Max length is " + MaxKeyLen + " characters)\n");
	}
      } else {
        error = Encoding.ASCII.GetBytes("Key does not match hexstring regex: " + hexregex.ToString() + "\n");
      }
      return success;
    }

    public static async Task PollXSServiceFuture(XSServiceData data, int milliseconds) {
      using(Timer futurepoll = new Timer(milliseconds)) {
	Task future = new Task(async () => await data.DequeueMessages()); 
	futurepoll.Elapsed += (Object source, ElapsedEventArgs e) => {
	  future.Start(); 
	};
        futurepoll.AutoReset = false;
	futurepoll.Start();
	await future; 
      }
    }

    public delegate Task SubMainLoop(XSServiceData data, XSSubscriber sub, object webData);

    public static Func<byte[], Task> WriteToResponseTaskGenerator(HttpResponse response) {
      return (byte[] message) => {
        return response.Body.WriteAsync(message, 0, message.Length);
      };
    }

    public static Func<Task> FlushResponseTaskGenerator(HttpResponse response) {
      return () => { return response.Body.FlushAsync(); };
    }

    public static async Task RunSubscriber(XSSubscriber sub, HttpResponse response, 
        object webData, XSServiceCallbacks callbacks, SubMainLoop main) {
      string key = null;
      byte[] error = null;
      sub.Key ??= "";
      if(! ControllerUtilities.TryExtractKey(sub.Key, out key, out error)) {
        response.StatusCode = 400;
	await response.Body.WriteAsync(error);
        await response.Body.FlushAsync();
      } else {
        sub.Key = key;
        XSServiceData data = XSService.Subscribe(callbacks, sub);
        try {
          await Task.Run(() => main(data, sub, webData));
          XSService.Unsubscribe(data);
        } catch {
          XSService.Unsubscribe(data);
        }
      }
    }

    public static int TimeAbsDifference(int lhs, int rhs) {
      lhs &= TimeOverflowMask;
      rhs &= TimeOverflowMask;
      int delta = Math.Abs(lhs - rhs);
      return (Math.Abs(delta) > TimeOverflow ? TimeOverflowMask - delta : delta);
    }
  }
}
