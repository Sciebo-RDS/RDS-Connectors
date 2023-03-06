# Figshare RDS connector

This connector port is based on the Zenodo port, which is part of the Sciebo-RDS monorepo.
The port is written in Python.

## Helm charts
Charts are also provided. Just drop them into place and add an entry in your values.yaml and run a helm update.

## Docker build
Use this file figshare-rds/RDS/layer1_adapters_and_ports/port_figshare/dockerfile to build your docker image to be used in the k8 cluster.

## Tests
Add your api-key and user to local-env.ini in order to be able to test the port implementation.
Run following commands in order to run the tests locally:

```sh
cd irods-rds/RDS/layer1_adapters_and_ports/port_irods
pipenv install
pipenv shell
pip install -r requirements-dev.txt
python -m pytest
```

## The port
The code for the port itself can be found here: irods-rds/RDS/layer1_adapters_and_ports/port_irods

## TODO
* Once metadata is working, we still need to make sure metadata is used to update the uploaded data.

## File structure
The file structure provided is the same as he Zenodo port in the Sciebo-RDS monorepo has.

```
.
├── charts
│   └── layer1_port_irods
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
│       └── port_irods
│           ├── dockerfile
│           ├── irods.png
│           ├── local-env.ini
│           ├── Makefile
│           ├── Pipfile
│           ├── Pipfile.lock
│           ├── PortIrods-Irods-pact.json
│           ├── postman.http
│           ├── pydocmd.yml
│           ├── pytest.ini
│           ├── Readme.md
│           ├── requirements-dev.txt
│           ├── requirements.txt
│           ├── src
│           │   ├── api
│           │   │   └── project
│           │   │       ├── files.py
│           │   │       ├── __init__.py
│           │   │       └── project.py
│           │   ├── __init__.py
│           │   ├── interface_port_metadata.yml
│           │   ├── irods.png
│           │   ├── lib
│           │   │   ├── firods.jsonld
│           │   │   ├── __pycache__
│           │   │   │   ├── upload_figshare.cpython-38.pyc
│           │   │   │   ├── upload_yoda.cpython-38.pyc
│           │   │   │   └── Util.cpython-38.pyc
│           │   │   ├── test.py
│           │   │   ├── upload_irods.py
│           │   │   └── Util.py
│           │   └── server.py
│           └── tests
│               ├── conftest.py
│               ├── constant.py
│               ├── lib
│               │   ├── test_lib_upload_irods.py
│               │   └── test_lib_util.py
│               ├── __pycache__
│               │   ├── conftest.cpython-310-pytest-6.2.4.pyc
│               │   ├── conftest.cpython-38-pytest-6.2.4.pyc
│               │   ├── constant.cpython-310.pyc
│               │   └── constant.cpython-38.pyc
│               └── server
│                   └── test_server.py
└── README.MD

17 directories, 46 files
```