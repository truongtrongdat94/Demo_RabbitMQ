FROM nodered/node-red:4.0.9

USER root

RUN mkdir -p /opt/node-red-source/scripts \
    && chown -R node-red:node-red /opt/node-red-source

COPY --chown=node-red:node-red flows.json /opt/node-red-source/flows.json
COPY --chown=node-red:node-red settings.js /opt/node-red-source/settings.js
COPY --chown=node-red:node-red scripts /opt/node-red-source/scripts

RUN chmod 0755 /opt/node-red-source/scripts/bootstrap-node-red.sh

USER node-red

ENTRYPOINT ["/bin/sh", "/opt/node-red-source/scripts/bootstrap-node-red.sh"]
CMD []
