using NeedleBot.Dto;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NeedleBot.Helpers;
using Newtonsoft.Json.Linq;

namespace NeedleBot {
    public class History {
        private readonly string _interval;
        public const string URL = "https://cex.io/api/ohlcv/hd/";

        public History(string storeFile = "history.json", string interval = "data1m")
        {
            _interval = interval;
            var currentDirectory = Directory.GetCurrentDirectory();
            StoreFilePath = Path.Combine(currentDirectory, storeFile);
        }

        public string StoreFilePath { get; }

        private PriceItem[] _prices;

        public PriceItem[] GetPrices()
        {
            if (!File.Exists(StoreFilePath))
                return null;

            if (_prices == null)
            {
                var json = File.ReadAllText(StoreFilePath);
                var items = JsonConvert.DeserializeObject<PriceItem[]>(json);
                _prices = items;
            }

            return _prices;
        }

        public async Task LoadPrices(DateTimeOffset start, DateTimeOffset end)
        {
            if (File.Exists(StoreFilePath))
                return;

            var current = start;
            await using var file = File.CreateText(StoreFilePath);
            await file.WriteAsync("[");

            var first = true;
            var client = new RestClient();
            var step = TimeSpan.FromDays(1);
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
                //https://cex.io/api/ohlcv/hd/20190111/BTC/USD
                var url = $"{URL}{current:yyyyMMdd}/BTC/USD";

                var req = new RestRequest(url, Method.GET);
                var res = await client.ExecuteTaskAsync(req);
                var json = JObject.Parse(res.Content);

                var strValue =
                    ((JProperty) json.Children().First(a => ((JProperty) a).Name == _interval))
                    .First.Value<string>().Replace("[[","").Replace("]]", "");
                var items = strValue.Split("],[", StringSplitOptions.RemoveEmptyEntries);

                var sb = new StringBuilder();
                for (var i = 0; i < items.Length; i++)
                {
                    var array = items[i].Split(",", StringSplitOptions.RemoveEmptyEntries);
                    var obj = new JObject(
                        new JProperty("t", array[0]),
                        new JProperty("h", array[1]),
                        new JProperty("l", array[2]),
                        new JProperty("o", array[3]),
                        new JProperty("c", array[4]),
                        new JProperty("v", array[5]));

                    sb.Append(obj);

                    if (i < items.Length - 1)
                        sb.Append(",\n");
                }

                sb.Remove(sb.Length - 2, 1);
                await file.WriteAsync(sb.ToString());

                current = to;
            }
            await file.WriteAsync("]");
        }
    }
}