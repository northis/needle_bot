using System;
using System.Runtime.Serialization;
using NeedleBot.Helpers;
using Newtonsoft.Json;

namespace NeedleBot.Dto {

    [DataContract]
    public class PriceItem {

        [DataMember]
        [JsonProperty("priceUsd")]
        [JsonConverter(typeof(DoubleConverter))]
        public double Price { get; set; }

        [DataMember]
        public DateTime Date { get; set; }
    }
}