using System;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NeedleBot.Helpers
{
    public class DoubleConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {   
            return objectType == typeof(double);
        }

        public override object ReadJson(JsonReader reader, Type objectType,
            object existingValue, JsonSerializer serializer)
        {
            var token = JToken.Load(reader);
            var val = StringToDouble(token.ToString());

            return val;
        }

        public static decimal StringToDouble(string str)
        {
            var tokenStr = str.Replace(",", ".");
            decimal.TryParse(tokenStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal d);
            return d;
        }

        public override void WriteJson(JsonWriter writer, object value,
            JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
