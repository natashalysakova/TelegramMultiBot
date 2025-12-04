// Plan (pseudocode):
// 1. Define C# DTO classes matching the JSON shape:
//    - ProgressState { type, data }
//    - ProgressData { prompt_id, nodes }
//    - NodeInfo { value, max, state, node_id, prompt_id, display_node_id, parent_node_id, real_node_id }
//    Note: nodes is a map/dictionary where keys can be arbitrary (e.g. "3", "abc").
// 2. Mark properties with JsonProperty attributes (Newtonsoft.Json) to ensure correct binding.
// 3. Provide a helper ParseProgressState(json) that:
//    - Uses JsonConvert.DeserializeObject<ProgressState>(json) to parse.
//    - Or parses to JObject and converts the "nodes" child to Dictionary<string, NodeInfo>.
// 4. Provide example usage showing how to iterate nodes by key and access NodeInfo values.
// 5. Keep nullables for fields that may be missing or null in JSON.

// Implementation:

using AngleSharp.Dom;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Telegram.Bot.Types;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace TelegramMultiBot.ImageGeneration.ComfyUI
{
    // Root container for the incoming websocket message
    public class WebsocketResponce
    {
        // status - Overall system status updates
        //execution_start - When a prompt execution begins
        //execution_cached - When cached results are used
        //executing - Updates during node execution
        //progress - Progress updates for long-running operations
        //executed - When a node completes execution
        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;

        [JsonProperty("data")]
        public ProgressData Data { get; set; } = new();
    }

    public class ProgressData
    {
        internal string? exception_message;

        [JsonProperty("prompt_id")]
        public string PromptId { get; set; } = string.Empty;

        // Use Dictionary<string, NodeInfo> so arbitrary keys (like "3") are handled
        [JsonProperty("nodes")]
        public Dictionary<string, NodeInfo> Nodes { get; set; } = new();

        [JsonProperty("status")]
        public Status Status { get; set; }

        [JsonProperty("sid")]
        public string Sid { get; set; }




        [JsonProperty("output")]
        public Output Output { get; set; }

        [JsonProperty("node_type")]
        public string NodeType { get; set; }

        [JsonProperty("exception_message")]
        public string ExceptionMessage { get; set; }

    }

    public class Status
    {
        [JsonProperty("exec_info")]
        public ExecutionInfo ExecutionInfo { get; set; }
    }

    public class ExecutionInfo
    {
        [JsonProperty("queue_remaining")]
        public int QueueRemaining { get; set; }
    }

    public class NodeInfo
    {
        [JsonProperty("value")]
        public double Value { get; set; }

        [JsonProperty("max")]
        public double Max { get; set; }

        [JsonProperty("state")]
        public string? State { get; set; }

        [JsonProperty("node_id")]
        public string? NodeId { get; set; }

        [JsonProperty("prompt_id")]
        public string? PromptId { get; set; }

        [JsonProperty("display_node_id")]
        public string? DisplayNodeId { get; set; }

        // parent_node_id can be null in JSON, use nullable string
        [JsonProperty("parent_node_id")]
        public string? ParentNodeId { get; set; }

        [JsonProperty("real_node_id")]
        public string? RealNodeId { get; set; }
    }

    public static class WebsocketResponceParser
    {
        // Preferred: direct deserialize into typed classes
        public static WebsocketResponce Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("json is empty", nameof(json));
            return JsonConvert.DeserializeObject<WebsocketResponce>(json) ?? throw new InvalidOperationException("Could not parse ProgressState");
        }

        // Alternate: using JObject when you need defensive handling or partial parsing
        public static WebsocketResponce ParseWithJObject(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("json is empty", nameof(json));
            var j = JObject.Parse(json);

            var result = new WebsocketResponce
            {
                Type = j["type"]?.Value<string>() ?? string.Empty,
                Data = new ProgressData
                {
                    PromptId = j["data"]?["prompt_id"]?.Value<string>() ?? string.Empty,
                    Nodes = new Dictionary<string, NodeInfo>()
                }
            };

            var nodesToken = j["data"]?["nodes"] as JObject;
            if (nodesToken != null)
            {
                foreach (var prop in nodesToken.Properties())
                {
                    // convert each child object to NodeInfo
                    var node = prop.Value.ToObject<NodeInfo>() ?? new NodeInfo();
                    result.Data.Nodes[prop.Name] = node;
                }
            }

            return result;
        }
    }

    // Example usage:
    // var ps = ProgressStateParser.Parse(jsonString);
    // foreach (var kv in ps.Data.Nodes)
    // {
    //     string nodeKey = kv.Key; // e.g. "3"
    //     NodeInfo info = kv.Value;
    //     Console.WriteLine($"Node {nodeKey}: {info.Value}/{info.Max} state={info.State}");
    // }
}