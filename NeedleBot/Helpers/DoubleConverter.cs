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
            var tokenStr = token.ToString().Replace(",",".");
            double.TryParse(tokenStr, NumberStyles.Any, CultureInfo.InvariantCulture,out double d);
            return d;
        }

        public override void WriteJson(JsonWriter writer, object value,
            JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
