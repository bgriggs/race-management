using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace Cloud.Shared.Hubs;

internal class Helpers
{
    public static string? GetClientId(Hub hub)
    {
        if (hub.Context.User == null)
        {
            return null;
        }

        var clientId = hub.Context.User.Claims.First(c => c.Type == "azp").Value;
        if (clientId == null)
        {
            return null;
        }
        return clientId;
    }
}
