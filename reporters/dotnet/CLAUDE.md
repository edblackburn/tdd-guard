# .NET Reporter

## Docker Exec for .NET Commands

All `dotnet` commands (build, run, test) must be executed inside the devcontainer from the host using `docker exec`. The tdd-guard devcontainer is a single container built from `.devcontainer/Dockerfile` — it does not use docker-compose.

```bash
docker exec -w /workspace <container-name> dotnet <command>
```

Find the container name via `docker ps`. The workspace mount is at `/workspace`.
