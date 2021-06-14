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
  public struct XSServiceCallbacks {
    public Func<byte[], Task> writeCallback;
    public Func<Task> flushCallback;
    public Func<byte[], byte[]> messageMapping;
  }

  public class XSServiceData {
    private ConcurrentQueue<byte[]> queuedMessages;
    private XSServiceCallbacks callbacks;
    public int Timestamp;

    public XSServiceData(XSServiceCallbacks sessionCallbacks) {
      queuedMessages = new ConcurrentQueue<byte[]>();
      callbacks = sessionCallbacks;
      Timestamp = Environment.TickCount;
    }

    public void EnqueueMessage(byte[] msg) {
      queuedMessages.Enqueue(msg);
    }

    public async Task DequeueMessages() {
      byte[] curr;
      bool messageSent = false;
      while(queuedMessages.TryDequeue(out curr)) {
	await callbacks.writeCallback(callbacks.messageMapping(curr));
	Timestamp = Environment.TickCount;
        messageSent = true;
      }
      if(messageSent) {
        await callbacks.flushCallback();
      }
    }
  }

  public static class XSService {
    private static ConcurrentDictionary<XSServiceData, XSSubscriber> dataStorage;

    static XSService() {
      dataStorage = new ConcurrentDictionary<XSServiceData, XSSubscriber>();
    }

    public static XSServiceData Subscribe(XSServiceCallbacks callbacks, XSSubscriber sub) {
      XSServiceData newdata = new XSServiceData(callbacks);
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
