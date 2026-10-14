# Bot service on the home server

The bot service runs on the home server next to the PostgreSQL container. Render reaches it
through a forwarded port, the same way it reaches PostgreSQL. Caddy terminates TLS with a
self-signed certificate that the backend pins by its SHA-256 fingerprint, and the backend
authenticates with an API key.

```
Render (ChessLab.Web) --HTTPS :51821--> router --> home server :51821 (Caddy) --> bots:8080
```

The files in this directory are templates. The real `compose.yml`, `.env` and certificate live
in `~/chesslab-bots` on the server and are never committed.

## One-time server setup

Run these on the home server (`ssh <user>@<server LAN IP> -p <SSH port>`).

1. Create the directory and copy the templates into it:

   ```sh
   mkdir -p ~/chesslab-bots/certs
   cd ~/chesslab-bots
   # copy compose.yml, Caddyfile and .env.example from deploy/bot-service/ in the repository
   cp .env.example .env
   ```

2. Generate the API key (at least 32 characters) and put it in `.env` as `BOT_SERVICE_KEY`:

   ```sh
   openssl rand -hex 32
   ```

3. Create the self-signed certificate. Use the public IP address (or the host name Render
   uses) as the subject alternative name:

   ```sh
   openssl req -x509 -newkey rsa:4096 -sha256 -days 3650 -nodes \
     -keyout certs/bot-service.key -out certs/bot-service.crt \
     -subj "/CN=chesslab-bots" -addext "subjectAltName=IP:<public IP>"
   chmod 600 certs/bot-service.key
   openssl x509 -in certs/bot-service.crt -noout -fingerprint -sha256
   ```

   The last command prints the fingerprint for `Bots__ServiceCertificateThumbprint` (colons are
   allowed).

4. Open the port in ufw:

   ```sh
   sudo ufw allow 51821/tcp comment 'ChessLab bot service'
   ```

   fail2ban needs no change: the service has no login, and requests without the key are
   answered 401 before any work is done.

5. Forward TCP port 51821 on the router to `<server LAN IP>:51821`, by hand in the router's admin
   page or with the existing `setup-port-forward-upnp.sh`.

6. Let Docker pull the image from GHCR (only needed while the package is private):

   ```sh
   echo <personal access token with read:packages> | docker login ghcr.io -u <GitHub user> --password-stdin
   ```

7. Start it and check it:

   ```sh
   docker compose up -d
   curl -k https://localhost:51821/health
   ```

   `/health` answers 200 with `version` and `workers` once every worker's Stockfish is running,
   and 503 otherwise.

## Render settings

Set these environment variables on the Render service:

| Variable | Value |
| --- | --- |
| `Bots__ServiceUrl` | `https://<public IP>:51821/` |
| `Bots__ServiceKey` | the `BOT_SERVICE_KEY` from step 2 |
| `Bots__ServiceCertificateThumbprint` | the SHA-256 fingerprint from step 3 |

Without `Bots__ServiceUrl` the backend runs without bots: rooms cannot add bots and `/hc`
reports the `bots` check as Degraded.

## Continuous deployment

On every push to `main` the `Build and deploy` workflow publishes
`ghcr.io/majkelll/chesslab-bot-service` tagged with the short commit hash, then the self-hosted
runner (on the same LAN) runs:

```sh
ssh -p <SSH port> <user>@<server LAN IP> \
  "cd ~/chesslab-bots && BOT_SERVICE_TAG=<hash> docker compose pull && BOT_SERVICE_TAG=<hash> docker compose up -d"
```

and waits up to 2 minutes for `/health` to report that hash before deploying the web image to
Render. The workflow needs:

- the runner user's SSH key in the server user's `~/.ssh/authorized_keys`;
- the repository secrets `BOT_SERVICE_SSH_HOST`, `BOT_SERVICE_SSH_PORT` and `BOT_SERVICE_SSH_USER`;
- the repository variable `BOT_SERVICE_HEALTH_URL`, for example `https://<server LAN IP>:51821/health`.

## Rotating the certificate

1. Create the new certificate next to the old one and note its fingerprint.
2. Set `Bots__ServiceCertificateThumbprint` on Render to the new fingerprint and swap the files
   in `certs/` in the same maintenance window, then run `docker compose restart proxy`.

Bots are unavailable between the two steps; running games pause and end after 5 minutes if the
swap takes longer.
