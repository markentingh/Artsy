# Artsy.SQL

PostgreSQL database schema for the Artsy template.

## Structure

- `Tables/` - Table definitions for authentication and user management.
- `SeedData/` - Initial seed data (e.g., AppRoles).
- `Indexes/` - Database indexes.
- `Functions/` - PostgreSQL functions (e.g., sequence reset).
- `Sequences/` - Custom sequences.
- `deploy.sql` - Master deployment script that runs all schema files.
- `gulpfile.js` - Auto-generates `deploy.sql` from the files in the subfolders.
- `package.json` - Node dependencies for the gulpfile.

## Generating deploy.sql

After adding or renaming schema files, regenerate `deploy.sql` so it includes all files in the correct order:

```bash
npm install
gulp update-deploy
```

You can also watch for changes:

```bash
gulp watch
```

## Deployment

1. Create the PostgreSQL database if it does not already exist:

```bash
psql -h localhost -U postgres -c "SELECT 'CREATE DATABASE artsy' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'artsy')\gexec"
```

2. Run `deploy.sql` against the database:

```bash
psql -h localhost -U postgres -d artsy -f deploy.sql
```

## Important Notes

- This project intentionally does **not** include SQL Server artifacts or migration scripts.
- All PostgreSQL objects are created in the `public` schema.
- The `ResetAllSequences()` function resets serial sequences to `MAX(id) + 1` on startup.
