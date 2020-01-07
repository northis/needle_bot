using System;

namespace NeedleBot.Helpers
{
    public static class Extensions
    {
        public static long ToUnix(this DateTimeOffset dateTime)
        {
            var res = (long) dateTime.Subtract(new DateTimeOffset(1970, 1, 1, 0, 0, 0, dateTime.Offset))
                .TotalMilliseconds;
            return res;
        }
    }
}