using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Diagnostics.Context;
using Microsoft.Extensions.Logging;

namespace SampleApp.Controllers
{
    [MyFilter]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> logger;
        private readonly HttpClient httpClient;

        public HomeController(ILogger<HomeController> logger, HttpClient httpClient)
        {
            this.logger = logger;
            this.httpClient = httpClient;
        }

        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            logger.LogInformation("index");
            Task[] tasks = new Task[1];
            for (int i = 0; i < tasks.Length; i++)
            {
                int i1 = i;
                tasks[i] = httpClient.GetAsync($"http://127.0.0.1:5000/api/values/{i1}", cancellationToken);
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);

            return View();
        }


    }
}
