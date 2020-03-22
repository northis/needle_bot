using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NeedleBot.Dto;
using NeedleBot.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using Trady.Core.Infrastructure;
using Trady.Core.Period;

namespace NeedleBot
{
    public class CexImporter : IImporter
    {
        public const string URL = "https://cex.io/api/ohlcv/hd/";
        public const int CEX_REQUEST_INTERVAL_HOURS = 24;
        public const string CEX_INTERVAL_NAME = "data1m";

        public CexImporter()
        {
            _currentDirectory = Directory.GetCurrentDirectory();
        }

        private readonly string _currentDirectory;
        private Queue<PriceItem> _pricesCache;
        private string _currentQueueName;

        public async Task<IReadOnlyList<IOhlcv>> ImportAsync(
            string symbol, 
            DateTime? startTime = null, 
            DateTime? endTime = null, 
            PeriodOption period = PeriodOption.PerMinute, 
            CancellationToken token = default)
        {
            if (!startTime.HasValue)
                return null;

            if (!endTime.HasValue)
                endTime = DateTime.UtcNow;

            var fileName = $"{symbol}-{period}-{startTime:s}-{endTime:s}.json".Replace(":", ".");
            if (fileName == _currentQueueName)
            {
                return _pricesCache.ToList();
            }

            _currentQueueName = fileName;
            if (!await LoadPricesFromFile(token))
                await LoadPricesNet(startTime.Value, endTime.Value);

            return _pricesCache.ToList();
        }

        private async Task<bool> LoadPricesFromFile(CancellationToken token = default)
        {
            var filePath = Path.Combine(_currentDirectory, _currentQueueName);
            if (!File.Exists(filePath)) 
                return false;

            try
            {
                var json = await File.ReadAllTextAsync(filePath, token);
                var items = JsonConvert.DeserializeObject<PriceItem[]>(json);
                _pricesCache = new Queue<PriceItem>(items);
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                FileUtils.DeleteFileQuietly(filePath);
                _currentQueueName = null;
            }

            return false;
        }

        private async Task LoadPricesNet(DateTime start, DateTime end)
        {
            var current = start;
            await using var file = File.CreateText(_currentQueueName);
            await file.WriteAsync("[");

            var first = true;
            var client = new RestClient();
            var step = TimeSpan.FromHours(CEX_REQUEST_INTERVAL_HOURS);

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

                if (res.Content == null)
                {
                    current = to;
                    continue;
                }

                var json = JObject.Parse(res.Content);

                var strValue =
                    ((JProperty)json.Children().First(a => ((JProperty)a).Name == CEX_INTERVAL_NAME))
                    .First.Value<string>().Replace("[[", "").Replace("]]", "");
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
