using System.Net;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using osu.NET;
using osu.NET.Authorization;
using osu.NET.Enums;
using System.Text.Json.Nodes;
using JsonSerializer = System.Text.Json.JsonSerializer;

Console.WriteLine("@sore guys hows things");

var provider = new OsuClientAccessTokenProvider("id", "secret");
var client = new OsuApiClient(provider, null);

var userIds = File.ReadAllLines("input.csv").Select(int.Parse).ToArray();

for (var i = 0; i < userIds.Length; i++)
{
    var userId = userIds[i];
    try
    {
        if(i % 100 == 0)
            Console.WriteLine($"{i}/{userIds.Length}");

        var userResponse = await client.GetUserAsync(userId, Ruleset.Osu);
        if (userResponse.IsFailure)
        {
            Console.WriteLine($"{userId} user query failed: {userResponse.Error}");
            File.AppendAllLines("output.csv", [",,,,,,,,,,,,,,,,,,,"]);
            await Task.Delay(100);
            continue;
        }

        var user = userResponse.Value;

        var userScores = await GetHuisScores(userId);
        if (userScores.Length == 0)
            userScores = await GetOsuApiScores(userId);

        var percentageOfFl = 0.0;
        var percentageOfEz = 0.0;
        var percentageOfHd = 0.0;
        var percentageOfHr = 0.0;
        var percentageOfLazer = 0.0;

        var percentageOfHdOnly = 0.0;
        var percentageOfHdHrOnly = 0.0;
        var percentageOfHdDtOnly = 0.0;
        var percentageOfDtOnly = 0.0;

        if (userScores.Length > 0)
        {
            percentageOfFl = userScores.Count(x => x.Mods.Any(m => m.Acronym == "FL")) / (double)userScores.Length;
            percentageOfEz = userScores.Count(x => x.Mods.Any(m => m.Acronym == "EZ")) / (double)userScores.Length;
            percentageOfHd = userScores.Count(x => x.Mods.Any(m => m.Acronym == "HD")) / (double)userScores.Length;
            percentageOfHr = userScores.Count(x => x.Mods.Any(m => m.Acronym == "HR")) / (double)userScores.Length;
            percentageOfLazer =
                1 - userScores.Count(x => x.Mods.Any(m => m.Acronym == "CL")) / (double)userScores.Length;

            percentageOfHdOnly =
                userScores.Count(s =>
                    s.Mods.Any(m => m.Acronym == "HD") && s.Mods.All(m => m.Acronym == "HD" || m.Acronym == "CL")) / (double)userScores.Length;
            percentageOfHdHrOnly =
                userScores.Count(s =>
                    s.Mods.Any(m => m.Acronym == "HD") && s.Mods.Any(m => m.Acronym == "HR") &&
                    s.Mods.All(m => m.Acronym == "HD" || m.Acronym == "HR" || m.Acronym == "CL")) / (double)userScores.Length;
            percentageOfHdDtOnly =
                userScores.Count(s =>
                    s.Mods.Any(m => m.Acronym == "HD") && s.Mods.Any(m => m.Acronym == "DT") &&
                    s.Mods.All(m => m.Acronym == "HD" || m.Acronym == "DT" || m.Acronym == "CL")) / (double)userScores.Length;
            percentageOfDtOnly =
                userScores.Count(s =>
                    s.Mods.Any(m => m.Acronym == "DT") && s.Mods.All(m => m.Acronym == "DT" || m.Acronym == "CL")) / (double)userScores.Length;
        }

        var top1 = await GetOsuStatsScoreCount(user.Username, 1, 1);
        var top8 = await GetOsuStatsScoreCount(user.Username, 2, 8);
        var top50 = await GetOsuStatsScoreCount(user.Username, 9, 50);

        // username, rank, playtime, ranked score, %of lazer topscores, amount of badges, registration date, amount of #1s, % of fl scores, % of ez scores, ez medal, hd only, hdhr only, hddt only, dt only, hdfl medal, top1-1, top2-8, top9-50, is mapper

        File.AppendAllLines("output.csv",
        [
            $"{user.Username},{user.Statistics?.GlobalRank},{user.Statistics?.PlayTime},{user.Statistics?.RankedScore},{percentageOfLazer:N3},{user.Badges?.Length ?? 0},{user.JoinDate},{user.FirstScoresCount},{percentageOfFl:N3},{percentageOfEz:N3},{percentageOfHd:N3},{percentageOfHr:N3},{user.Achievements?.Any(x => x.Id == 142) ?? false},{percentageOfHdOnly:N3},{percentageOfHdHrOnly:N3},{percentageOfHdDtOnly:N3},{percentageOfDtOnly:N3},{user.Achievements?.Any(x => x.Id == 172) ?? false},{top1},{top8},{top50},{user.RankedBeatmapSetsCount > 0}"
        ]);
    }
    catch (Exception e)
    {
        Console.WriteLine($"{userId} failed: {e.Message}");
    }
    finally
    {
        await Task.Delay(100);
    }
}

return;

async Task<LeScore[]> GetHuisScores(int userId)
{
    try
    {
        var http = new HttpClient();
        var json = await http.GetStringAsync($"https://api.pp.huismetbenen.nl/player/scores/{userId}/14/topranks");
        if (string.IsNullOrEmpty(json))
            return [];

        var deserialized = JsonSerializer.Deserialize<JsonArray>(json);
        if (deserialized is null)
            return [];

        if (deserialized.Count == 0)
            return [];

        var userScores = new List<LeScore>();
        foreach (var score in deserialized)
        {
            userScores.Add(new LeScore()
            {
                Mods = score["mods"].AsArray().Select(x => new Mod() { Acronym = x["acronym"].GetValue<string>() })
                    .ToArray()
            });
        }

        return userScores.ToArray();
    }
    catch (Exception e)
    {
        Console.WriteLine($"{userId} failed to query huis: {e.Message}");
        return [];
    }
}

async Task<LeScore[]> GetOsuApiScores(int userId)
{
    var userScoresResponse =
        await client.GetUserScoresAsync(userId, UserScoreType.Best, limit: 100, ruleset: Ruleset.Osu);
    var userScores = userScoresResponse.Value;
    await Task.Delay(100);

    try 
    {
        var userScoresPage2Response =
            await client.GetUserScoresAsync(userId, UserScoreType.Best, offset: 100, limit: 100, ruleset: Ruleset.Osu);
        var userScoresPage2 = userScoresPage2Response.Value;
        userScores = userScores!.Concat(userScoresPage2!).ToArray();
        await Task.Delay(100);
    }
    catch (Exception e)
    {
        Console.WriteLine($"{userId} failed to query userScoresPage2: {e.Message}");
    }

    try
    {
        var userPinnedResponse = await client.GetUserScoresAsync(userId, UserScoreType.Pinned, limit: 100, ruleset: Ruleset.Osu);
        var userPinned = userPinnedResponse.Value;
        userScores = userScores!.Concat(userPinned!).ToArray();
        await Task.Delay(200);
    }
    catch (Exception e)
    {
        Console.WriteLine($"{userId} failed to query userPinned: {e.Message}");
    }

    try
    {
        var userFirstsResponse =
        await client.GetUserScoresAsync(userId, UserScoreType.First, limit: 100, ruleset: Ruleset.Osu);
        var userFirsts = userFirstsResponse.Value;
        userScores = userScores!.Concat(userFirsts!).ToArray();
    }
    catch (Exception e)
    {
        Console.WriteLine($"{userId} failed to query userFirsts: {e.Message}");
    }

    return userScores!.Select(x=> new LeScore { Mods = x.Mods.Select(m=> new Mod {Acronym = m.Acronym}).ToArray()}).ToArray();
}

async Task<int> GetOsuStatsScoreCount(string username, int rankMin, int rankMax)
{
    var http = new HttpClient();
    var response = await http.PostAsync($"https://osustats.ppy.sh/api/getScores", new StringContent($"{{\"u1\":\"{username}\",\"rankMin\": \"{rankMin}\",\"rankMax\":\"{rankMax}\",\"gamemode\":\"0\"}}", new MediaTypeHeaderValue("application/json")));
    var responseString = await response.Content.ReadAsStringAsync();
    if (!response.IsSuccessStatusCode)
    {
        if (response.StatusCode != HttpStatusCode.BadRequest)
            Console.WriteLine($"{username} failed to query osustats: {responseString}");

        return 0;
    }

    if (string.IsNullOrEmpty(responseString))
        return 0;

    var deserialized = JsonSerializer.Deserialize<JsonArray>(responseString);
    if (deserialized is null)
        return 0;

    return deserialized[1]?.GetValue<int>() ?? 0;
}

public class LeScore
{
    public Mod[] Mods { get; set; }

}
public class Mod
{
    [JsonProperty("acronym")]
    public string Acronym { get; set; }
}