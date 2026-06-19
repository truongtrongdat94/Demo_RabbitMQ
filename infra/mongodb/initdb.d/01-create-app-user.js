const databaseName = process.env.MONGO_APP_DATABASE;
const username = process.env.MONGO_APP_USERNAME;
const password = process.env.MONGO_APP_PASSWORD;

if (!databaseName || !username || !password) {
    throw new Error("MONGO_APP_DATABASE, MONGO_APP_USERNAME, and MONGO_APP_PASSWORD are required");
}

const appDb = db.getSiblingDB(databaseName);

appDb.createUser({
    user: username,
    pwd: password,
    roles: [
        {
            role: "readWrite",
            db: databaseName
        }
    ]
});

appDb.createCollection("telemetry_events");
appDb.telemetry_events.createIndex(
    {
        MessageId: 1
    },
    {
        unique: true,
        name: "ux_telemetry_events_message_id"
    }
);

appDb.telemetry_events.createIndex(
    {
        "Meta.TestCase": 1,
        Timestamp_Gateway: -1
    },
    {
        name: "ix_telemetry_events_testcase_gateway_time"
    }
);
