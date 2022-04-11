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

## Deploying the website

Currently, the website runs inside a docker container, and any changes to the website need to be deployed manually. To do this,
SSH into the server, clone the [hol430/apsim-web](https://github.com/hol430/apsim-web) repository (which contains the docker
config), and run the deploy script located inside the `builds` directory. This script will rebuild the docker image from latest
git sources and will restart the running container with the newly-built image.

In the longer run we need to automate this, and have the unit tests run by a CI server before pull requests are merged, and then
have the CI server handle deployment of changes to the website.

## Legacy API

The APSIM.Builds REST API previously lived under https://apsimdev.apsim.info/APSIM.Builds.Service. This website still runs, and
old versions of apsim still make requests to the old API. Eventually it would be good to manually rewrite requests and remove
the old site altogether, but for now, the old site is still running and will proxy requests through to the new API (at
https://builds.apsim.info). The legacy site runs the code on the apsimdev branch.

In order to update the old website (ie for a bugfix), you will need to build the solution in visual studio, on windows (tested
working with VS2019). The deploy changes to the site, you need to right click on the APSIM.Builds.Service project in the solution
explorer, and choose "publish". Sometimes the changes may not become live/apparent immediately. If you want to force the issue,
you can remote into the server, open IIS server manager, right click on APSIM.Builds.Service website, and recycle the application
pool.

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

- Old APSIM github webhook endpoint (need to find a way to verify the HMAC keys in the request)
- Update official API users to use new API
