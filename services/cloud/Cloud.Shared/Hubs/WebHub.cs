using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

namespace Cloud.Shared.Hubs;

[Authorize]
public class WebHub(IConnectionMultiplexer cacheMux) : Hub
{
}
