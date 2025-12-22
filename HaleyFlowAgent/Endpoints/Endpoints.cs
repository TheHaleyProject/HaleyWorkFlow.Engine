using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haley.Models {
    internal static class ENDPOINTS {
        // --- Engine lifecycle ---
        public const string ENGINE_REGISTER = "engine/register";   // POST
        public const string ENGINE_HEARTBEAT = "engine/heartbeat";  // POST

        // --- Workflow definitions ---
        public const string WORKFLOW = "workflow";          // GET by guid, POST create, PUT update, DELETE
        public const string WORKFLOW_ALL = "workflow/all";      // GET all
                                                                // e.g. GET /workflow/{guid}, POST /workflow, PUT /workflow/{guid}, DELETE /workflow/{guid}

        // --- Workflow versions ---
        public const string VERSION = "workflow/version";  // GET by guid, POST create
        public const string VERSION_LATEST = "workflow/version/latest";   // GET latest by workflowId
        public const string VERSION_PUBLISH = "workflow/version/publish";  // PUT mark as published

        // --- Workflow instances ---
        public const string INSTANCE = "instance";          // GET by guid, POST create, PUT update
        public const string INSTANCE_PENDING = "instance/pending";  // GET pending by env
        public const string INSTANCE_CLAIM = "instance/claim";    // PUT claim by engine
        public const string INSTANCE_ORPHANED = "instance/orphaned"; // GET orphaned by env

        // --- Workflow state ---
        public const string STATE = "state";             // GET by instance guid, PUT update

        // --- Logs & steps ---
        public const string STEPS = "steps";             // GET steps by instance
        public const string LOGS = "logs";              // GET logs by instance
        public const string LOGS_ADD = "logs";              // POST new log (same base, different verb)
    }

}
