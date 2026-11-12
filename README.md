[![Docker Image CI](https://github.com/natashalysakova/TelegramMultiBot/actions/workflows/docker-image.yml/badge.svg)](https://github.com/natashalysakova/TelegramMultiBot/actions/workflows/docker-image.yml)

## Database provider and compatibility

The application uses Pomelo.EntityFrameworkCore.MySql 9 with the MySqlConnector driver
and EF Core 9 against MariaDB 11.6. The projects target .NET 11. EF Core 9 is out of
support, and MariaDB 11.6 is not an LTS release; this combination is a temporary
compatibility choice, not a supported long-term configuration. Oracle's
`MySql.EntityFrameworkCore` is a different provider and cannot be substituted without
reviewing provider-specific migrations and testing database compatibility.

The bot runs `Database.Migrate()` and seeds data on startup. Before deployment, back up
each existing database and verify that the backup can be restored. Check that all
committed migration IDs are present in `__EFMigrationsHistory` and that the schema
matches the model; the latest committed migration is
`20260414164003_AddUnqueIndex`. A matching last ID alone does not establish that the
history or schema is complete. Rehearse startup against a restored, sanitized copy
before deploying. Do not delete migration history or recreate an existing database.
If rollback is needed, restore the previous application version; use a verified
database backup if schema or data changes also need to be reversed.

## MariaDB integration test

The provider test without a database runs with:

```sh
dotnet test BotTests/BotTests.csproj --filter 'FullyQualifiedName~MariaDbProviderTests'
```

For the integration test, start an isolated MariaDB 11.6 instance and provision a
dedicated database whose name begins with `telegrammultibot_test_`. Supply its
connection string via `TELEGRAM_MULTIBOT_TEST_DB` in your environment; do not commit
credentials. Never point the test at a production database. The test verifies the
server version and migration history. It applies the committed migrations if the
test database is empty, then exercises startup migration, seeding, and representative
reads and writes in a rolled-back transaction. A nonempty test database must already
have the complete migration history. The integration case is reported as skipped
when the environment variable is absent.

This synthetic fixture does not establish that a real deployment's schema and data
match the model. A separate rehearsal against a sanitized restored copy remains a
deployment prerequisite. Container builds require the .NET 11 SDK/runtime images.

## MeTube connectivity

Set `MeTubeUrl` in the `VideoDownloader` settings stored in the database. The default,
`http://metube:8081`, works for the bot container on the same Docker Compose network as
MeTube. When running the bot directly on the host with the Compose port mapping, use
`http://localhost:8082` instead. If MeTube is unreachable or times out, video processing
and MeTube history cleanup retry on the next polling interval; database cleanup still
runs. Shutdown cancellation stops the current request without logging a service failure.
