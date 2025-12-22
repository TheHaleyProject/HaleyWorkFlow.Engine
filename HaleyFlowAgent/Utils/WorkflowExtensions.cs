using Haley.Enums;
using Haley.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haley.Utils {
    public static class WorkflowStatusExtensions {
        public static bool CanTransitionTo(this WorkflowStatus current, WorkflowStatus next) {
            return (current, next) switch {
                (WorkflowStatus.Pending, WorkflowStatus.Running) => true,
                (WorkflowStatus.Running, WorkflowStatus.Retrying) => true,
                (WorkflowStatus.Running, WorkflowStatus.Waiting) => true,
                (WorkflowStatus.Waiting, WorkflowStatus.Running) => true,
                (WorkflowStatus.Running, WorkflowStatus.Completed) => true,
                (WorkflowStatus.Running, WorkflowStatus.Failed) => true,
                _ => false
            };
        }

        public static object ResolveParameter(this WorkflowInstance instance, WorkflowDefinition definition, string key) {
            if (instance.Parameters != null && instance.Parameters.TryGetValue(key, out var value))
                return value;

            if (definition.Parameters != null && definition.Parameters.TryGetValue(key, out var fallback))
                return fallback;

            throw new KeyNotFoundException($"Parameter '{key}' not found in instance or definition.");
        }

    }
}
