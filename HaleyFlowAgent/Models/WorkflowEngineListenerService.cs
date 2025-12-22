using Haley.Abstractions;
using Haley.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;


namespace Haley.Models {
    public class WorkflowEngineListenerService : IHostedService {
        private readonly IHostApplicationLifetime _lifetime;

        public WorkflowEngineListenerService(IHostApplicationLifetime lifetime) {
            _lifetime = lifetime;
        }

        public Task StartAsync(CancellationToken cancellationToken) {
            var builder = WebApplication.CreateBuilder();
            builder.Services.AddControllers();
            builder.Services.AddSingleton<IWorkflowEngine, WorkflowEngine>();

            var app = builder.Build();
            app.MapControllers();
            _ = app.RunAsync(); // Fire-and-forget

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
