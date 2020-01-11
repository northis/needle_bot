using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NeedleBot.Helpers
{
    public class DateConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {   
            return objectType == typeof(DateTimeOffset);
        }

        public override object ReadJson(JsonReader reader, Type objectType,
            object existingValue, JsonSerializer serializer)
        {
            var token = JToken.Load(reader);
            var val = token.ToString().FromUnixDate();

            return val;
        }

        public override void WriteJson(JsonWriter writer, object value,
            JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
