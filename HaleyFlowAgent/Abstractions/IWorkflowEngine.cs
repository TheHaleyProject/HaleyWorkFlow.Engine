using Haley.Enums;
using Haley.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haley.Abstractions {
    public interface IWorkflowEngine {
        Task HeartbeatAsync(); 
        Task RegisterAsync(string environment); // register engine with API
        Task PollAndExecuteAsync(string environment); // poll API for pending instances
        Task ExecuteAsync(Guid instanceId); // execute a claimed workflow
        Task RecoverOrphanedAsync(string environment); // reclaim orphaned workflows
        Task HandleWebhookAsync(Guid instanceId, string eventKey, Dictionary<string, object> payload);
    }
}
