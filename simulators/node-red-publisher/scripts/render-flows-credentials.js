const fs = require("fs");
const path = require("path");
const crypto = require("crypto");

const required = [
    "NODE_RED_MQTT_USERNAME",
    "NODE_RED_MQTT_PASSWORD",
    "NODE_RED_CREDENTIAL_SECRET"
];

for (const name of required) {
    if (!process.env[name]) {
        throw new Error(`${name} is required`);
    }
}

const credentialsPath = path.join("/data", "flows_cred.json");
const credentials = {
    f0c7e86d4b2e1002: {
        user: process.env.NODE_RED_MQTT_USERNAME,
        password: process.env.NODE_RED_MQTT_PASSWORD
    }
};

const encryptionKey = crypto
    .createHash("sha256")
    .update(process.env.NODE_RED_CREDENTIAL_SECRET)
    .digest();
const initVector = crypto.randomBytes(16);
const cipher = crypto.createCipheriv("aes-256-ctr", encryptionKey, initVector);
const encryptedCredentials = {
    $:
        initVector.toString("hex") +
        cipher.update(JSON.stringify(credentials), "utf8", "base64") +
        cipher.final("base64")
};

fs.writeFileSync(credentialsPath, `${JSON.stringify(encryptedCredentials, null, 4)}\n`, {
    encoding: "utf8",
    mode: 0o600
});
