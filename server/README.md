# server/ (M3)

Docker packaging for the headless Linux dedicated server.

- `Dockerfile` (M3): slim Debian/Ubuntu base + `server/build/` output from
  `scripts/build-server.sh`; entrypoint runs the server binary with
  `-batchmode -nographics -port 7777`; exposes 7777/udp + 8080/tcp (status).
- Image: `bholcombe/potshot-server:<git-sha>` and `:dev`.
