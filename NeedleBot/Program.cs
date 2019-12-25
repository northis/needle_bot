using System;

namespace NeedleBot
{
    class Program
    {
        static void Main(string[] args)
        {
            var history = new History();

            history.LoadPrices(
                    new DateTime(2019, 11, 1), 
                    new DateTime(2019, 11, 2), 
                    TimeSpan.FromDays(1))
                .ConfigureAwait(false).GetAwaiter().GetResult();

            var res = history.GetPrices();
        }
    }
}
