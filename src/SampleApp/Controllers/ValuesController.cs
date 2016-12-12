using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

// For more information on enabling Web API for empty projects, visit http://go.microsoft.com/fwlink/?LinkID=397860

namespace SampleApp.Controllers
{
    [Route("api/[controller]")]
    public class ValuesController : Controller
    {
        private readonly ILogger<ValuesController> logger;

        public ValuesController(ILogger<ValuesController> logger)
        {
            this.logger = logger;
        }

        [HttpGet("{id}")]
        public string Get(string id)
        {
            logger.LogInformation(id);
            return id;
        }
    }
}
