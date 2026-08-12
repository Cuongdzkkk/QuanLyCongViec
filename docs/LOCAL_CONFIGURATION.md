# Local configuration

The shared application settings intentionally do not contain a developer-specific SQL Server name or credentials.

For local SQL Server, provide one of these outside Git:

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=<your-server>;Database=<your-database>;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False"
```

For EF design-time commands, `ConnectionStrings__DefaultConnection` or `DEV_SQL_SERVER` must be set. SQL Server integration tests use `SPRINTA_TEST_SQL_SERVER`.

When no SQL Server is configured in Development and `Database:AllowDevelopmentInMemory` is enabled, the API keeps using the existing development InMemory fallback.

Production secrets and verified service configuration are supplied through deployment environment variables. See `.env.example` for variable names and placeholders only.
