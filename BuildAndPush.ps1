param($version)

docker build -t ghcr.io/natashalysakova/bober-bot:latest . -f .\TelegramMultibot\Dockerfile --platform linux/arm64
docker image tag ghcr.io/natashalysakova/bober-bot:latest ghcr.io/natashalysakova/bober-bot:v$version
docker image push ghcr.io/natashalysakova/bober-bot:latest
docker image push ghcr.io/natashalysakova/bober-bot:v$version