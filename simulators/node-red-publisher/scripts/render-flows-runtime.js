const fs = require("fs");
const path = require("path");

const flowsPath = path.join("/data", "flows.json");
const mqttBrokerNodeId = "f0c7e86d4b2e1002";

function readBoolean(name) {
    const value = process.env[name];

    if (value === undefined || value === "") {
        return undefined;
    }

    if (value.toLowerCase() === "true") {
        return true;
    }

    if (value.toLowerCase() === "false") {
        return false;
    }

    throw new Error(`${name} must be true or false when set`);
}

const flows = JSON.parse(fs.readFileSync(flowsPath, "utf8"));
const brokerNode = flows.find((node) => node.id === mqttBrokerNodeId);

if (!brokerNode) {
    throw new Error(`MQTT broker node ${mqttBrokerNodeId} was not found in flows.json`);
}

if (process.env.NODE_RED_MQTT_BROKER) {
    brokerNode.broker = process.env.NODE_RED_MQTT_BROKER;
}

if (process.env.NODE_RED_MQTT_PORT) {
    brokerNode.port = process.env.NODE_RED_MQTT_PORT;
}

const useTls = readBoolean("NODE_RED_MQTT_USE_TLS");

if (useTls !== undefined) {
    brokerNode.usetls = useTls;

    if (!useTls) {
        brokerNode.tls = "";
    }
}

fs.writeFileSync(flowsPath, `${JSON.stringify(flows, null, 4)}\n`, "utf8");
