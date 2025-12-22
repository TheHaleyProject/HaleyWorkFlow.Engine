using Haley.Abstractions;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haley.Controllers {
    [ApiController]
    [Route("[controller]")]
    public class EngineListenerController : ControllerBase {
        private readonly IWorkflowEngine _engine;

        public EngineListenerController(IWorkflowEngine engine) {
            _engine = engine;
        }

        [HttpPost]
        public async Task<IActionResult> Execute([FromQuery] Guid instanceId) {
            await _engine.ExecuteAsync(instanceId);
            return Ok(new { message = "Execution started", instanceId });
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> Webhook([FromQuery] Guid instanceId, [FromQuery] string eventKey, [FromBody] Dictionary<string, object> payload) {
            await _engine.HandleWebhookAsync(instanceId, eventKey, payload);
            return Ok(new { message = "Webhook handled", instanceId, eventKey });
        }

        [HttpGet]
        public IActionResult Status() {
            return Ok(new { status = "Engine is alive", timestamp = DateTime.UtcNow });
        }
    }

}
