using System.Text.Json.Nodes;
using Newtonsoft.Json;
using osu.NET;
using osu.NET.Authorization;
using osu.NET.Enums;
using JsonSerializer = System.Text.Json.JsonSerializer;

Console.WriteLine("@sore guys hows things");

var provider = new OsuClientAccessTokenProvider("id", "secret");
var client = new OsuApiClient(provider, null);

var userIds = File.ReadAllLines("input.csv").Select(int.Parse).ToArray();

foreach (var userId in userIds)
{
    try
    {
        var userResponse = await client.GetUserAsync(userId, Ruleset.Osu);
        if (userResponse.IsFailure)
        {
            Console.WriteLine($"{userId} user query failed: {userResponse.Error}");
            File.AppendAllLines("output.csv", [",,,,,,,,,,"]);
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

        if (userScores.Length > 0)
        {
            percentageOfFl = userScores.Count(x => x.Mods.Any(m => m.Acronym == "FL")) / (double)userScores.Length;
            percentageOfEz = userScores.Count(x => x.Mods.Any(m => m.Acronym == "EZ")) / (double)userScores.Length;
            percentageOfHd = userScores.Count(x => x.Mods.Any(m => m.Acronym == "HD")) / (double)userScores.Length;
            percentageOfHr = userScores.Count(x => x.Mods.Any(m => m.Acronym == "HR")) / (double)userScores.Length;
            percentageOfLazer =
                1 - userScores.Count(x => x.Mods.Any(m => m.Acronym == "CL")) / (double)userScores.Length;
        }

        // username, rank, playtime, ranked score, %of lazer topscores, amount of badges, registration date, amount of #1s, % of fl scores, % of ez scores, ez medal

        File.AppendAllLines("output.csv",
        [
            $"{user.Username},{user.Statistics?.GlobalRank},{user.Statistics?.PlayTime},{user.Statistics?.RankedScore},{percentageOfLazer:N6},{user.Badges?.Length ?? 0},{user.JoinDate},{user.FirstScoresCount},{percentageOfFl:N6},{percentageOfEz:N6},{percentageOfHd:N6},{percentageOfHr:N6},{user.Achievements?.Any(x => x.Id == 142) ?? false}"
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
    await Task.Delay(100);
    var userScoresResponse =
        await client.GetUserScoresAsync(userId, UserScoreType.Best, limit: 100, ruleset: Ruleset.Osu);
    var userScores = userScoresResponse.Value;
    await Task.Delay(100);

    var userScoresPage2Response =
        await client.GetUserScoresAsync(userId, UserScoreType.Best, offset: 100, limit: 100, ruleset: Ruleset.Osu);
    var userScoresPage2 = userScoresPage2Response.Value;
    await Task.Delay(100);

    var userPinnedResponse = await client.GetUserScoresAsync(userId, UserScoreType.Pinned, limit: 100, ruleset: Ruleset.Osu);
    var userPinned = userPinnedResponse.Value;
    await Task.Delay(200);

    var userFirstsResponse =
        await client.GetUserScoresAsync(userId, UserScoreType.First, limit: 100, ruleset: Ruleset.Osu);
    var userFirsts = userFirstsResponse.Value;

    return userScores!.Concat(userScoresPage2!).Concat(userFirsts!).Concat(userPinned!).Select(x=> new LeScore() { Mods = x.Mods.Select(m=> new Mod() {Acronym = m.Acronym}).ToArray()}).ToArray();
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