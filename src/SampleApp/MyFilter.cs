using System.Diagnostics.Activity;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace SampleApp
{
    internal class SamplingMiddleware
    {
        private readonly RequestDelegate next;

        public SamplingMiddleware(RequestDelegate next)
        {
            this.next = next;
        }

        public Task Invoke(HttpContext context)
        {
            var activity = new Activity("sampling");
            activity.WithBaggage("isSampled", bool.TrueString);
            Activity.SetCurrent(activity);
            return next.Invoke(context);
        }
    }
}