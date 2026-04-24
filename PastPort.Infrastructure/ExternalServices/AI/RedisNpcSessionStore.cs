using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using StackExchange.Redis;
using System.Text.Json;
using PastPort.Application.Interfaces; // تأكد من استدعاء الانترفيس بتاعك

namespace PastPort.Infrastructure.ExternalServices.AI;

public class RedisNpcSessionStore : INpcSessionStore
{
    private readonly IDatabase _redis;
    private const int SessionTtlHours = 1;

    public RedisNpcSessionStore(IConnectionMultiplexer redis)
    {
        _redis = redis.GetDatabase();
    }

    public string CreateSession(NpcSessionData data)
    {
        var sessionId = Guid.NewGuid().ToString();
        // data.CreatedAt = DateTime.UtcNow; // لو عندك الخاصية دي في الموديل

        var json = JsonSerializer.Serialize(data);

        // FIX 2: TTL set at Redis level — no memory leak
        _redis.StringSet(
            $"npc:session:{sessionId}",
            json,
            TimeSpan.FromHours(SessionTtlHours));

        return sessionId;
    }

    public NpcSessionData? GetSession(string sessionId)
    {
        var json = _redis.StringGet($"npc:session:{sessionId}");
        if (json.IsNullOrEmpty) return null;
        return JsonSerializer.Deserialize<NpcSessionData>(json!);
    }

    public void RemoveSession(string sessionId)
    {
        _redis.KeyDelete($"npc:session:{sessionId}");
    }
}