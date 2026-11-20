using MessageQueueNET.Client.Models;
using MessageQueueNET.Client.Services.Abstraction;
using MessageQueueNET.Extensions;
using MessageQueueNET.Middleware.Authentication;
using MessageQueueNET.Models.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using System;

namespace MessageQueueNET.Controllers
{
    [Route("[controller]")]
    [ApiController]
    [RequiredRole(ClientRole.Read)]
    public class InfoController : ControllerBase
    {
        private readonly IMessageQueueApiVersionService _apiVersionService;
        private readonly ILogger<InfoController> _logger;

        public InfoController(IMessageQueueApiVersionService apiVersionService,
                              ILogger<InfoController> logger)
        {
            _apiVersionService = apiVersionService;
            _logger = logger;
        }

        [HttpGet]
        [Route("")]
        public InfoResult Info()
        {
            var result = new InfoResult();

            try
            {
                result.Version = _apiVersionService.Version;
            }
            catch (Exception ex)
            {
                result.AddExceptionMessage(ex, _logger);
            }

            return result;
        }
    }
}
