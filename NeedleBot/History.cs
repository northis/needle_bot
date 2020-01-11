using NeedleBot.Dto;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NeedleBot.Helpers;
using Newtonsoft.Json.Linq;

namespace NeedleBot {
    public class History {
        public const string URL = "https://tvc4.forexpros.com/0e1949eecbcf379ebf4831347732c138/1578665287/7/7/18/history?symbol=1031677";

        public History(string storeFile = "history.json", int interval = 15)
        {
            Interval = interval;
            var currentDirectory = Directory.GetCurrentDirectory();
            StoreFilePath = Path.Combine(currentDirectory, storeFile);
        }

        public string StoreFilePath { get; }
        public int Interval { get; }

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

        public async Task LoadPrices(DateTimeOffset start, DateTimeOffset end, TimeSpan step)
        {
            if (File.Exists(StoreFilePath))
                return;

            var current = start;
            await using var file = File.CreateText(StoreFilePath);
            await file.WriteAsync("[");

            var first = true;
            var client = new RestClient();
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

                var url = $"{URL}&resolution={Interval}&from={current.ToUniversalTime().ToUnix()}&to={to.ToUniversalTime().ToUnix()}";

                var req = new RestRequest(url, Method.GET);
                var res = await client.ExecuteTaskAsync(req);
                var json = JObject.Parse(res.Content);

                var times = json.GetChildrenByName("t");
                var opens = json.GetChildrenByName("o");
                var closes = json.GetChildrenByName("c");
                var highs = json.GetChildrenByName("h");
                var lows = json.GetChildrenByName("l");

                var sb = new StringBuilder();
                for (var i = 0; i < times.LongLength; i++)
                {
                    var obj = new JObject(
                        new JProperty("t", times[i]),
                        new JProperty("o", opens[i]),
                        new JProperty("c", closes[i]),
                        new JProperty("h", highs[i]),
                        new JProperty("l", lows[i]));

                    sb.Append(obj);

                    if (i < times.LongLength - 1)
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