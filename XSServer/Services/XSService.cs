using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using System.Text;
using System.IO;
using Microsoft.AspNetCore.Http;
using XSServer.Models;

namespace XSServer.Services {
  public class XSServiceData {
    private ConcurrentQueue<byte[]> queuedMessages;
    private HttpResponse response;
    public int Timestamp;

    public XSServiceData(HttpResponse resp) {
      queuedMessages = new ConcurrentQueue<byte[]>();
      response = resp;
      Timestamp = Environment.TickCount;
    }

    public void EnqueueMessage(byte[] msg) {
      queuedMessages.Enqueue(msg);
    }

    public async Task DequeueMessages(Func<byte[], byte[]> messageMapping) {
      byte[] curr;
      while(queuedMessages.TryDequeue(out curr)) {
	await response.Body.WriteAsync(messageMapping(curr));
	Timestamp = Environment.TickCount;
      }
      await response.Body.FlushAsync();
    }
  }

  public static class XSService {
    private static ConcurrentDictionary<XSServiceData, XSSubscriber> dataStorage;

    static XSService() {
      dataStorage = new ConcurrentDictionary<XSServiceData, XSSubscriber>();
    }

    public static XSServiceData Subscribe(HttpResponse resp, XSSubscriber sub) {
      XSServiceData newdata = new XSServiceData(resp);
      dataStorage.GetOrAdd(newdata, sub);
      return newdata;
    }

    public static void Unsubscribe(XSServiceData data) {
      XSSubscriber dummy;
      while(dataStorage.ContainsKey(data) && ! dataStorage.TryRemove(data, out dummy))
	;
    }

    public static void SendMessage(byte[] message, string key) {
      foreach(KeyValuePair<XSServiceData, XSSubscriber> dataval in dataStorage) {
        if(dataval.Value.Key.Equals(key)) {
	  dataval.Key.EnqueueMessage(message);
	}
      }
    }
  }
}
