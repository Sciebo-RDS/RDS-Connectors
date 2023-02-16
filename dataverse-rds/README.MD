# Dataverse RDS connector

This connector port is based on the Zenodo port, which is part of the Sciebo-RDS monorepo.
The port is written in Python.

## Helm charts
Charts are also provided. Just drop them into place and add an entry in your values.yaml and run a helm update.

## Docker build
Use this file dataverse-rds/RDS/layer1_adapters_and_ports/port_dataverse/dockerfile to build your docker image to be used in the k8 cluster.

## Tests
Add your api-key to local-env.ini in order to be able to test the port implementation.
Run following commands in order to run the tests locally:

```sh
cd dataverse-rds/RDS/layer1_adapters_and_ports/port_dataverse
pipenv install
pipenv shell
pip install -r requirements-dev.txt
python -m pytest
```

## The port
The code for the port itself can be found here: dataverse-rds/RDS/layer1_adapters_and_ports/port_dataverse

## File structure
The file structure provided is the same as he Zenodo port in the Sciebo-RDS monorepo has.
```
.
├── charts
│   └── layer1_port_dataverse
│       ├── Chart.lock
│       ├── charts
│       │   └── common-0.1.2.tgz
│       ├── Chart.yaml
│       ├── templates
│       │   ├── configmap.yaml
│       │   ├── deployment.yaml
│       │   ├── _helpers.tpl
│       │   ├── service.yaml
│       │   └── tests
│       │       └── test-connection.yaml
│       └── values.yaml
├── RDS
│   └── layer1_adapters_and_ports
│       └── port_dataverse
│           ├── dataverse.png
│           ├── dockerfile
│           ├── local-env.ini
│           ├── Pipfile
│           ├── Pipfile.lock
│           ├── PortDataverse-Dataverse-pact.json
│           ├── pydocmd.yml
│           ├── pytest.ini
│           ├── requirements-dev.txt
│           ├── requirements.txt
│           ├── src
│           │   ├── api
│           │   │   └── project
│           │   │       ├── files.py
│           │   │       ├── __init__.py
│           │   │       └── project.py
│           │   ├── dataverse.png
│           │   ├── __init__.py
│           │   ├── interface_port_metadata.yml
│           │   ├── lib
│           │   │   ├── fdataverse.jsonld
│           │   │   ├── upload_dataverse.py
│           │   │   └── Util.py
│           │   └── server.py
│           └── tests
│               ├── conftest.py
│               ├── constant.py
│               ├── lib
│               │   ├── test_lib_upload_dans.py
│               │   └── test_lib_util.py
│               └── server
│                   └── test_server.py
└── README.MD

15 directories, 35 files
```