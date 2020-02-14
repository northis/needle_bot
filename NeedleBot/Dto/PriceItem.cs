using System;
using System.Runtime.Serialization;
using NeedleBot.Helpers;
using Newtonsoft.Json;

namespace NeedleBot.Dto {

    [DataContract]
    public class PriceItem
    {
        public double Price => Close;

        [DataMember]
        [JsonProperty("l")]
        [JsonConverter(typeof(DoubleConverter))]
        public double Low { get; set; }

        [DataMember]
        [JsonProperty("h")]
        [JsonConverter(typeof(DoubleConverter))]
        public double High { get; set; }

        [DataMember]
        [JsonProperty("o")]
        [JsonConverter(typeof(DoubleConverter))]
        public double Open { get; set; }

        [DataMember]
        [JsonProperty("c")]
        [JsonConverter(typeof(DoubleConverter))]
        public double Close { get; set; }

        [DataMember]
        [JsonProperty("t")]
        [JsonConverter(typeof(DateConverter))]
        public DateTimeOffset Date { get; set; }
    }
}