using AwayPlayer.Managers;
using AwayPlayer.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SiraUtil.Logging;
using SiraUtil.Web;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AwayPlayer.Utils;

namespace AwayPlayer
{
    internal class APIWrapper
    {
        private readonly CacheManager<Score> ScoreCache;
        private readonly IHttpService HttpService;
        private readonly SiraLog SiraLogger;

        private readonly TimeSpan RateLimitTime = TimeSpan.FromSeconds(10) + TimeSpan.FromMilliseconds(100);
        private readonly int RateLimitCount = 9;
        private readonly int BatchSize = 100;

        public APIWrapper(SiraLog siraLog, IHttpService httpService, CacheManager<Score> scoreCache)
        {
            SiraLogger = siraLog;
            HttpService = httpService;
            ScoreCache = scoreCache;
#if DEBUG
            SiraLogger.DebugMode = true;
#endif
        }

        public async Task<List<Score>> GetReplayListAsync()
        {
            // Get the player UserId
            var userId = PlatformUserId.Get();

            // If userId of iPixelGalaxy alt, force userId to iPixelGalaxy main account, else normal behaviour
            // Ask pixel why this is here - DO NOT REMOVE
            if (userId == "76561199480289698") userId = "76561198967815164";

            // User ids used for debugging
            //userId = "76561198967815164"; // iPixelGalaxy user id

            // Fetch the total number of scores available
            var totalResponse = await HttpService.GetAsync($"https://api.beatleader.xyz/player/{userId}/scores?count=0&sortBy=date");
            string totalJsonData = await totalResponse.ReadAsStringAsync();
            int total = JObject.Parse(totalJsonData)["metadata"]["total"].Value<int>();

            var cacheTotal = ScoreCache.CountCacheItems();

            if (total == cacheTotal) return ScoreCache.GetAllCacheEntries();

            if (total < cacheTotal)
            {
                SiraLogger.Error("Bad cache detected, cleared and reaquiring....");
                ScoreCache.ClearCache(); // If the cache contains more than the server has, we can assume we have borked cache, so lets clear it and try again
                cacheTotal = 0;
            }

            var requestsNeeded = (int)Math.Ceiling((double)(total - cacheTotal) / BatchSize);

            List<Score> scores = new List<Score>();
            bool rateLimitHit = false;

            for (int i = 0; i < requestsNeeded; i++)
            {
                var page = i + 1; // Pages start from 1
                string fetchUri = $"https://api.beatleader.xyz/player/{userId}/scores?count={BatchSize}&page={page}&sortBy=date";

                SiraLogger.Debug($"Fetching from: {fetchUri}");

                if (page % RateLimitCount == 0 || rateLimitHit)
                {
                    var time = DateTime.Now;
                    while (time + RateLimitTime > DateTime.Now) ; // Ugly but works
                    rateLimitHit = false;
                }

                JArray scoresArray;
                string jsonData = string.Empty;

                try
                {
                    // Fetch scores for the current page
                    var response = await HttpService.GetAsync(fetchUri);
                    jsonData = await response.ReadAsStringAsync();

                    // Deserialize JSON data into a JArray
                    scoresArray = JObject.Parse(jsonData)["data"] as JArray;
                }
                catch (JsonReaderException e)
                {
                    if (jsonData == "API calls quota exceeded! maximum admitted 10 per 10s.")
                    {
                        SiraLogger.Info("Rate limit hit! Waiting 10 seconds for ratelimit to clear");
                        rateLimitHit = true;
                        i--; // Will have to repeat current request
                        continue;
                    }

                    SiraLogger.Debug(jsonData);
                    SiraLogger.Error(e);
                    return null;
                }

                // Process scores in the current page
                foreach (var scoreJObject in scoresArray)
                {
                    var score = scoreJObject.ToObject<Score>();
                    score.Song = scoreJObject["leaderboard"]["song"].ToObject<Song>();
                    score.Difficulty = scoreJObject["leaderboard"]["difficulty"].ToObject<Difficulty>();
                    scores.Add(score);
                    ScoreCache.AddOrUpdateCache(score.LeaderboardId, score);
                }
            }

            return scores;
        }



        public async Task<byte[]> GetReplayDataAsync(string url)
        {
            var response = await HttpService.GetAsync(url);
            return await response.ReadAsByteArrayAsync();
        }

    }
}
