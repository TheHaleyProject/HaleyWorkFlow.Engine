using Haley.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Haley.Utils;
using Microsoft.Extensions.Hosting;

namespace Haley.Models {
    public class WorkflowEnginePollingWorkerService : BackgroundService {
        private readonly IWorkflowEngine _engine;

        public WorkflowEnginePollingWorkerService(IWorkflowEngine engine) {
            _engine = engine;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
            await _engine.RegisterAsync("dev");
            while (!stoppingToken.IsCancellationRequested) {
                await _engine.HeartbeatAsync();
                await _engine.PollAndExecuteAsync("dev");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }
}
