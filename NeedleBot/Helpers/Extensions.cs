using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace NeedleBot.Helpers
{
    public static class Extensions
    {
        public static long ToUnix(this DateTimeOffset dateTime)
        {
            var res = (long) dateTime.Subtract(new DateTimeOffset(1970, 1, 1, 0, 0, 0, dateTime.Offset))
                .TotalSeconds;
            return res;
        }
        public static DateTimeOffset FromUnixDate(this string dateTime)
        {
            var res = DateTimeOffset.FromUnixTimeSeconds(long.Parse(dateTime));
            return res;
        }
    }
}