using NeedleBot.Dto;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.IO;
using System.Threading.Tasks;
using NeedleBot.Helpers;
using Newtonsoft.Json.Linq;

namespace NeedleBot {
    public class History {
        public const string URL = "https://api.coincap.io/v2/assets/bitcoin/history";

        public History(string storeFile = "history.json", string interval = "m1")
        {
            Interval = interval;
            var currentDirectory = Directory.GetCurrentDirectory();
            StoreFilePath = Path.Combine(currentDirectory, storeFile);
        }

        public string StoreFilePath { get; }
        public string Interval { get; }

        public PriceItem[] GetPrices()
        {
            if (!File.Exists(StoreFilePath))
                return null;

            var json = File.ReadAllText(StoreFilePath);
            var items = JsonConvert.DeserializeObject<PriceItem[]>(json);

            return items;
        }

        public async Task LoadPrices(DateTime start, DateTime end, TimeSpan step)
        {
            var current = start;
            await using var file = File.CreateText(StoreFilePath);
            await file.WriteAsync("[");

            var first = true;
            var client = new RestClient("http://example.com");
            while (current < end)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    await file.WriteLineAsync(",");
                }

                var to = current.Add(step);

                var url = $"{URL}?interval={Interval}&start={current.ToUnix()}&end={to.ToUnix()}";

                var req = new RestRequest(url, Method.GET);
                var res = await client.ExecuteTaskAsync(req);
                var json = JObject.Parse(res.Content);
                var dataArrayString = json.First.First.ToString();

                var truncatedString = dataArrayString.Substring(1, dataArrayString.Length - 2);
                await file.WriteAsync(truncatedString);
                current = to;
            }
            await file.WriteAsync("]");
        }
    }
}