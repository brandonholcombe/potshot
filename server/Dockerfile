# Potshot dedicated server. Build context: server/ (expects build/ output
# from scripts/build-server.sh). linux/amd64 only — tow-c1 nodes.
FROM ubuntu:24.04

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY build/ /app/
RUN chmod +x /app/potshot-server.x86_64

EXPOSE 7777/udp

ENTRYPOINT ["/app/potshot-server.x86_64", \
    "-potshotServer", "-batchmode", "-nographics", \
    "-logFile", "/dev/stdout"]
