using Haley.Enums;
using Haley.Models;
using Haley.Abstractions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Nodes;
using Haley.Rest;

namespace Haley.Utils {
    public class WorkflowEngine : IWorkflowEngine {
        private readonly string _engineId = Guid.NewGuid().ToString();
        private readonly IClient _client;
        private readonly ILogger<WorkflowEngine> _logger;
        private readonly Dictionary<Guid, WorkflowDefinition> _definitionCache = new();

        public WorkflowEngine(ILogger<WorkflowEngine> logger) {
            //_client = ClientStore.Get("dev");
            _logger = logger;
        }

        // --- Engine lifecycle ---
        public async Task RegisterAsync(string environment) {
            var payload = new { engineId = _engineId, environment };
            var response = await (await _client?
                .WithEndPoint(ENDPOINTS.ENGINE_REGISTER)
                .WithBody(new RawBodyRequestContent(payload.ToJson(), true, BodyContentType.StringContent) {
                    OverrideMIMETypeAutomatically = false
                })
                .PostAsync())?.AsStringResponseAsync();

            if (response == null || !response.IsSuccessStatusCode) {
                _logger.LogError("Engine registration failed for env={env}.", environment);
                throw new Exception("Engine registration failed.");
            }

            _logger.LogInformation("Engine {engineId} registered for environment {env}.", _engineId, environment);
        }

        public async Task HeartbeatAsync() {
            var payload = new JsonObject {
                ["engineId"] = _engineId,
                ["timestamp"] = DateTime.UtcNow
            };

            var _ = await (await _client
                .WithEndPoint(ENDPOINTS.ENGINE_HEARTBEAT)
                .WithBody(new RawBodyRequestContent(payload.ToJsonString(), true, BodyContentType.StringContent) {
                    OverrideMIMETypeAutomatically = false
                })
                .PostAsync())?.AsStringResponseAsync();
        }

        // --- Polling & execution ---
        public async Task PollAndExecuteAsync(string environment) {
            var response = await (await _client
                .WithEndPoint(ENDPOINTS.INSTANCE_PENDING)
                .WithQuery(new QueryParam("env", environment))
                .GetAsync())?.AsStringResponseAsync();

            if (response == null || !response.IsSuccessStatusCode) {
                _logger.LogWarning("Polling pending instances failed for env={env}.", environment);
                return;
            }

            var instances = response.Content.FromJson<List<WorkflowInstance>>() ?? new List<WorkflowInstance>();
            foreach (var instance in instances) {
                if (await ClaimInstanceAsync(instance.InstanceId)) {
                    _ = ExecuteAsync(instance.InstanceId); // fire-and-forget
                }
            }
        }

        public async Task RecoverOrphanedAsync(string environment) {
            var response = await (await _client
                .WithEndPoint(ENDPOINTS.INSTANCE_ORPHANED)
                .WithQuery(new QueryParam("env", environment))
                .GetAsync())?.AsStringResponseAsync();

            if (response == null || !response.IsSuccessStatusCode) {
                _logger.LogWarning("Recover orphaned instances failed for env={env}.", environment);
                return;
            }

            var instances = response.Content.FromJson<List<WorkflowInstance>>() ?? new List<WorkflowInstance>();
            foreach (var instance in instances) {
                if (await ClaimInstanceAsync(instance.InstanceId)) {
                    _ = ExecuteAsync(instance.InstanceId);
                }
            }
        }

        private async Task<bool> ClaimInstanceAsync(Guid instanceId) {
            var payload = new { engineId = _engineId };
            var response = await (await _client
                .WithEndPoint(ENDPOINTS.INSTANCE_CLAIM)
                .WithQuery(new QueryParam("id", instanceId.ToString()))
                .WithBody(new RawBodyRequestContent(payload.ToJson(), true, BodyContentType.StringContent) {
                    OverrideMIMETypeAutomatically = false
                })
                .PutAsync())?.AsStringResponseAsync();

            var ok = response != null && response.IsSuccessStatusCode;
            _logger.LogInformation("Claim {instanceId} by {engineId}: {ok}", instanceId, _engineId, ok);
            return ok;
        }

        public async Task ExecuteAsync(Guid instanceId) {
            // Load instance
            var loadResponse = await (await _client
                .WithEndPoint(ENDPOINTS.INSTANCE)
                .WithQuery(new QueryParam("guid", instanceId.ToString()))
                .GetAsync())?.AsStringResponseAsync();

            if (loadResponse == null || !loadResponse.IsSuccessStatusCode) {
                _logger.LogError("Instance load failed: {instanceId}", instanceId);
                throw new Exception($"Instance {instanceId} not found.");
            }

            var instance = loadResponse.Content.FromJson<WorkflowInstance>();
            if (instance == null) {
                _logger.LogError("Instance parse failed: {instanceId}", instanceId);
                throw new Exception("Invalid instance payload.");
            }

            // Load definition
            var definition = await LoadDefinitionAsync(instance.DefinitionId);

            // Initialize state
            var state = new WorkflowState {
                Status = WorkflowStatus.Running,
                StepResults = new Dictionary<int, StepResult>(),
                RuntimeContext = new Dictionary<string, object>(),
                Logs = new List<StepLog>()
            };

            // Execute workflow
            foreach (var phase in definition.Phases) {
                state.CurrentPhaseCode = phase.Code;

                foreach (var stepCode in phase.Steps) {
                    var step = definition.Steps.First(s => s.Code == stepCode);
                    state.CurrentStepCode = step.Code;

                    var result = await ExecuteStepAsync(step, instance.Parameters, instance.Urls);
                    state.StepResults[step.Code] = result;

                    state.Logs.Add(new StepLog {
                        StepCode = step.Code,
                        Status = result.Status,
                        Timestamp = DateTime.UtcNow,
                        Message = result.ErrorMessage ?? "Step executed."
                    });

                    if (result.Status == WorkflowStatus.Failed) {
                        state.Status = WorkflowStatus.Failed;
                        break;
                    }
                }

                if (state.Status == WorkflowStatus.Failed) break;
            }

            instance.State = state.Status;
            instance.LastUpdated = DateTime.UtcNow;

            // Push update back to API
            var updatePayload = new { instance, state };
            var updateResponse = await (await _client
                .WithEndPoint(ENDPOINTS.INSTANCE)
                .WithBody(new RawBodyRequestContent(updatePayload.ToJson(), true, BodyContentType.StringContent) {
                    OverrideMIMETypeAutomatically = false
                })
                .PutAsync())?.AsStringResponseAsync();

            if (updateResponse == null || !updateResponse.IsSuccessStatusCode) {
                _logger.LogError("Instance update failed: {instanceId}", instanceId);
                throw new Exception("Failed to update instance.");
            }

            _logger.LogInformation("Workflow {instanceId} completed with status {status}.", instanceId, state.Status);
        }

        // --- Helpers ---
        private async Task<WorkflowDefinition> LoadDefinitionAsync(Guid defGuid) {
            if (_definitionCache.TryGetValue(defGuid, out var cached))
                return cached;

            var response = await (await _client
                .WithEndPoint(ENDPOINTS.WORKFLOW)
                .WithQuery(new QueryParam("guid", defGuid.ToString()))
                .GetAsync())?.AsStringResponseAsync();

            if (response == null || !response.IsSuccessStatusCode) {
                _logger.LogError("Definition load failed: {defGuid}", defGuid);
                throw new Exception("Failed to load workflow definition.");
            }

            var fb = response.Content.FromJson<Feedback<WorkflowDefinition>>();
            if (fb == null || !fb.Status || fb.Result == null) {
                _logger.LogError("Definition feedback invalid: {defGuid}", defGuid);
                throw new Exception(fb?.Message ?? "Invalid definition response.");
            }

            _definitionCache[defGuid] = fb.Result;
            return fb.Result;
        }

        private async Task<StepResult> ExecuteStepAsync(WorkflowStep step, Dictionary<string, object> parameters, Dictionary<string, string> urlOverrides) {
            // Placeholder for actual dispatch logic (HTTP call to app, etc.)
            await Task.Delay(100);

            return new StepResult {
                Status = WorkflowStatus.Completed,
                Output = new { success = true },
                StartedAt = DateTime.UtcNow.AddSeconds(-1),
                CompletedAt = DateTime.UtcNow
            };
        }

        public async Task HandleWebhookAsync(Guid instanceId, string eventKey, Dictionary<string, object> payload) {
            var instanceResponse = await (await _client
                .WithEndPoint(ENDPOINTS.INSTANCE)
                .WithQuery(new QueryParam("guid", instanceId.ToString()))
                .GetAsync())?.AsStringResponseAsync();

            if (instanceResponse == null || !instanceResponse.IsSuccessStatusCode) {
                _logger.LogWarning("Webhook: instance not found {instanceId}", instanceId);
                return;
            }

            var instance = instanceResponse.Content.FromJson<WorkflowInstance>();
            var definition = await LoadDefinitionAsync(instance.DefinitionId);

            var triggeredStep = definition.Steps.FirstOrDefault(s => s.Trigger == eventKey);
            if (triggeredStep == null) return;

            var result = await ExecuteStepAsync(triggeredStep, payload, instance.Urls);

            var stateResponse = await (await _client
                .WithEndPoint(ENDPOINTS.STATE)
                .WithQuery(new QueryParam("guid", instanceId.ToString()))
                .GetAsync())?.AsStringResponseAsync();

            if (stateResponse == null || !stateResponse.IsSuccessStatusCode) {
                _logger.LogWarning("Webhook: state load failed {instanceId}", instanceId);
                return;
            }

            var state = stateResponse.Content.FromJson<WorkflowState>();
            state.StepResults[triggeredStep.Code] = result;
            state.Status = result.Status;

            var updatePayload = new { instance, state };
            var updateResponse = await (await _client
                .WithEndPoint(ENDPOINTS.INSTANCE)
                .WithBody(new RawBodyRequestContent(updatePayload.ToJson(), true, BodyContentType.StringContent) {
                    OverrideMIMETypeAutomatically = false
                })
                .PutAsync())?.AsStringResponseAsync();

            if (updateResponse == null || !updateResponse.IsSuccessStatusCode) {
                _logger.LogError("Webhook: instance update failed {instanceId}", instanceId);
            }
        }
    }
}
