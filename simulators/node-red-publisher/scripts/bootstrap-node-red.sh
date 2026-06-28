#!/bin/sh
set -eu

source_dir="/opt/node-red-source"
data_dir="/data"

mkdir -p "$data_dir/scripts"

rm -f "$data_dir/flows.json" "$data_dir/settings.js"
rm -f "$data_dir/.config.runtime.json" "$data_dir/.config.runtime.json.backup"
cp "$source_dir/flows.json" "$data_dir/flows.json"
cp "$source_dir/settings.js" "$data_dir/settings.js"
cp "$source_dir/scripts/render-flows-credentials.js" "$data_dir/scripts/render-flows-credentials.js"
cp "$source_dir/scripts/render-flows-runtime.js" "$data_dir/scripts/render-flows-runtime.js"

node "$data_dir/scripts/render-flows-runtime.js"
node "$data_dir/scripts/render-flows-credentials.js"
exec node-red --userDir "$data_dir" --settings "$data_dir/settings.js"
