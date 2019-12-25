using System;

namespace NeedleBot.Helpers
{
    public static class Extensions
    {
        public static long ToUnix(this DateTime dateTime)
        {
            var res = (long) dateTime.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
            return res;
        }
    }
}