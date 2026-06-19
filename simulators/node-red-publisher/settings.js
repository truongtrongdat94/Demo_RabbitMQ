module.exports = {
    flowFile: "flows.json",
    credentialSecret: process.env.NODE_RED_CREDENTIAL_SECRET,
    mqttReconnectTime: 15000,
    logging: {
        console: {
            level: "info",
            metrics: false,
            audit: false
        }
    }
};

