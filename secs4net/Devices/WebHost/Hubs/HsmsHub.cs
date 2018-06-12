using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Secs4Net;
using Secs4Net.Sml;

namespace HsmsWebHost.Hubs
{
    public class HsmsHub: Hub
	{
		private readonly ConcurrentDictionary<string, (SecsGem gem, ConcurrentDictionary<int, PrimaryMessageWrapper> penddingMessages)> _devices = new ConcurrentDictionary<string, (SecsGem, ConcurrentDictionary<int, PrimaryMessageWrapper>)>();

        public override Task OnConnectedAsync()
		{
			if (_devices.TryRemove(Context.ConnectionId, out var device))
			{
				device.penddingMessages.Clear();
				device.gem.Dispose();
			}

			var query = ParseQueryString(Context.GetHttpContext());
			if (query == default)
			{
				return Task.CompletedTask;
			}

			device.penddingMessages = new ConcurrentDictionary<int, PrimaryMessageWrapper>();
			var gem = new SecsGem(query.active, query.ip, query.port);

			var caller = Clients.Caller;
			gem.ConnectionChanged += (sender, e) => 
				caller.SendAsync(nameof(gem.ConnectionChanged), gem.State.ToString());

			gem.PrimaryMessageReceived += (_, primaryMessage) =>
			{
				if (device.penddingMessages.TryAdd(primaryMessage.MessageId, primaryMessage))
				{
					caller.SendAsync(nameof(gem.PrimaryMessageReceived), primaryMessage.MessageId, primaryMessage.Message.ToSml());
				}
			};

			gem.Start();

			_devices.TryAdd(Context.ConnectionId, device);

			return base.OnConnectedAsync();
		}

		(IPAddress ip, int port, bool active) ParseQueryString(HttpContext httpContext)
		{

			var query = httpContext?.Request.Query;
			if (query == null)
			{
				BadRequest(httpContext.Response, "");
				return (default, default, default);
			}

			if (!query.TryGetValue("ipaddress", out var ipadderss)
				|| !IPAddress.TryParse(ipadderss, out var ip))
			{
				BadRequest(httpContext.Response, "ipaddress (IPv4 address)");
				return (default, default, default);
			}

			if (!query.TryGetValue("port", out var port)
				|| !int.TryParse(port, out var portNumber))
			{
				BadRequest(httpContext.Response, "port (integer)");
				return (default, default, default);
			}

			if (!query.TryGetValue("active", out var active)
				|| !bool.TryParse(active, out var isActive))
			{
				BadRequest(httpContext.Response, "active (boolean)");
				return (default, default, default);
			}

			return (ip, portNumber, isActive);
		}

		void BadRequest(HttpResponse response, string paramName)
		{
			//response.StatusCode = (int)HttpStatusCode.BadRequest;
			//response.WriteAsync($"please provide query string: {paramName}");
			Context.Abort();
		}

		public override Task OnDisconnectedAsync(Exception exception)
		{
			if (exception == null && _devices.TryRemove(Context.ConnectionId, out var device))
			{
				device.gem.Dispose();
				device.penddingMessages.Clear();
			}
			return base.OnDisconnectedAsync(exception);
		}
	}
}
