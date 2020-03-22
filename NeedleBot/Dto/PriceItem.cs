using System;
using System.Runtime.Serialization;
using NeedleBot.Helpers;
using Newtonsoft.Json;
using Trady.Core.Infrastructure;

namespace NeedleBot.Dto {

    [DataContract]
    public class PriceItem : IOhlcv
    {
        public decimal Price => Close;

        [DataMember]
        [JsonProperty("t")]
        [JsonConverter(typeof(DateConverter))]
        public DateTimeOffset DateTime { get; set; }

        [DataMember]
        [JsonProperty("o")]
        [JsonConverter(typeof(DoubleConverter))]
        public decimal Open { get; set; }

        [DataMember]
        [JsonProperty("h")]
        [JsonConverter(typeof(DoubleConverter))]
        public decimal High { get; set; }

        [DataMember]
        [JsonProperty("l")]
        [JsonConverter(typeof(DoubleConverter))]
        public decimal Low { get; set; }

        [DataMember]
        [JsonProperty("c")]
        [JsonConverter(typeof(DoubleConverter))]
        public decimal Close { get; set; }

        [DataMember]
        [JsonProperty("v")]
        [JsonConverter(typeof(DoubleConverter))]
        public decimal Volume { get; set; }
    }
}