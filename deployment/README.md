# Race Management — Kubernetes deployment

Umbrella Helm chart that deploys the three cloud services and a single-replica Redis.

```
deployment/
├── Chart.yaml                 # umbrella chart; depends on redis-operator
├── values.yaml                # global env/secrets + Redis + per-service overrides
├── templates/
│   ├── redis.yaml             # RedisFailover CR (1 redis + 1 sentinel)
│   └── redis-master-service.yaml   # stable "redis-master:6379" ClusterIP
└── charts/
    ├── race-management-web-api/           # HTTP API + /web-status SignalR hub (ingress)
    ├── race-management-car-gateway/       # /car-status SignalR hub for cars (ingress)
    └── race-management-channel-processor/ # background worker (no ingress, single replica)
```

## Services

| Subchart | Image | Exposed at | Scaling |
|----------|-------|-----------|---------|
| `race-management-web-api` | `bigmission/race-management-web-api` | `https://api.redmist.racing/race-management/…` | HPA 1–4 |
| `race-management-car-gateway` | `bigmission/race-management-car-gateway` | `https://api.redmist.racing/car-gateway/car-status` | HPA 1–4 |
| `race-management-channel-processor` | `bigmission/race-management-channel-processor` | internal only | fixed at 1 |

Each ingress uses an nginx `rewrite-target: /$1` so the path prefix is stripped before
the request reaches the app (e.g. `/race-management/v1/configuration/...` → `/v1/configuration/...`).
The two SignalR services carry WebSocket + sticky-session annotations.

## Redis

Provisioned through the [Spotahome redis-operator](https://github.com/spotahome/redis-operator)
as a `RedisFailover` with **a single Redis replica and a single sentinel** (`values.yaml` →
`redis.replicas` / `redis.sentinel.replicas`). This is intentionally **not** highly available.
Storage is `emptyDir` (cache/backplane only — Postgres is the system of record). Services
reach it via the `redis-master:6379` ClusterIP.

**Auth is enabled** (`redis.auth.secretName: race-management-secrets`). The operator reads
the `password` key from that secret and applies `requirepass` to the Redis server and
sentinels. The apps connect using the `redis` key — the full connection string, which must
embed the **same** password. So `race-management-secrets` carries three keys:

| Key | Used by | Value |
|-----|---------|-------|
| `db` | apps (`ConnectionStrings__Default`) | Postgres connection string |
| `password` | redis-operator (`requirepass`) | the bare Redis password |
| `redis` | apps (`ConnectionStrings__Redis`) | `redis-master:6379,abortConnect=false,password=<same pw>` |

The secret must exist **before** the `RedisFailover` reconciles, or the operator rejects it.
Avoid commas in the password — StackExchange.Redis uses them as connection-string delimiters.
To disable auth, set `redis.auth.secretName: ""` and drop `,password=…` from the `redis` value.

## Secrets

Before deploying, create the secret referenced by `global.secrets`:

```sh
# Pick a Redis password (no commas — StackExchange.Redis delimiter).
REDIS_PW='ChangeMe-strong-redis-password'

kubectl create secret generic race-management-secrets -n <namespace> \
  --from-literal=db='Host=…;Database=…;Username=…;Password=…' \
  --from-literal=password="$REDIS_PW" \
  --from-literal=redis="redis-master:6379,abortConnect=false,password=$REDIS_PW"
```

The `password` key feeds the redis-operator's `requirepass`; the `redis` key is the
connection string the services use (same password inline).

Keycloak realm/URL come from `global.env` (no client secret needed — the services validate
bearer JWTs). RedMist upstream uses code defaults, so nothing extra is required.

## Deploy

```sh
# 1. Pull the redis-operator dependency (writes Chart.lock + charts/redis-operator-*.tgz)
helm dependency update ./deployment

# 2. Install / upgrade
helm upgrade --install race-management ./deployment \
  --namespace race-management --create-namespace \
  --set race-management-web-api.image.tag=<version> \
  --set race-management-car-gateway.image.tag=<version> \
  --set race-management-channel-processor.image.tag=<version> \
  --wait
```

`helm dependency update` must be run once (and whenever the redis-operator version in
`Chart.yaml` changes) before install. The redis-operator CRDs must be established before
the `RedisFailover` resource reconciles; on a first install you may need to re-run the
upgrade if the CR is rejected because the CRD was still registering.
