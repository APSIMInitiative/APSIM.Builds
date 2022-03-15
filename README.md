# APSIM Builds/Upgrades API

This repository contains the APSIM.Builds website which is an API tracking the metadata stored in the builds databases for apsim.

## Running the website

The following environment variables must be set in order to run the website:

- `JENKINS_URL`: URL of the jenkins server
- `JENKINS_TOKEN_NG`: Token used to remotely start a NextGen release job on the jenkins server
- `JWT_SECRET_KEY`: Token used to create/validate JWTs passed to API endpoints
- `HMAC_SECRET_KEY`: Private key for verification of github webhook requests
- `DOCUMENTATION_PATH`: Path to autodocs files on disk
- `NG_DB_CONNECTION_STRING`: NextGen builds DB connection string
- `OLD_DB_CONNECTION_STRING`: Old APSIM buidls DB connection string
- `INSTALLERS_PATH`: Path to the apsim installers.

It's possible to run a local instance of the website. In this case, you will need to be running MySQL servers for nextgen and classic. This is easy to accomplish with docker.

## Making Requests

Most of the API endpoints require authorization in the form of a JWT. This should be passed in the Authorization header:

```bash
curl -X POST -H "Authorization: bearer $JWT" "https://my.api/endpoint"
```

## Migrator

The repository contains a migrator project. This is a console application which
will migrate data from the old (apsimdev) DB schema to the new schema. When run,
the following environment variables must be set:

- `CONN_STR_CLASSIC_OLD`: Connection string to the apsimdev builds classic DB.
- `CONN_STR_CLASSIC_NEW`: Connection string to the new builds classic DB.
- `CONN_STR_NG_OLD`: Name of an environment variable which contains a connection string to the apsimdev builds classic DB.
- `CONN_STR_NG_NEW`: Name of an environment variable which contains a connection string to the new builds classic DB.

## TODO

- Migrate existing data
- Old APSIM github webhook endpoint (need to find a way to verify the HMAC keys in the request)
- Update official API users to use new API
