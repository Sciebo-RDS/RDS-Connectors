#!/usr/bin/env python

from RDS import Util, BaseService, FileTransferMode, FileTransferArchive
from __init__ import app
import os


redirect_uri = os.getenv("RDS_OAUTH_REDIRECT_URI", "")
irods_address = os.getenv("IRODS_ADDRESS", "https://irods.com")
irods_api_address = os.getenv("IRODS_API_ADDRESS", "https://api.irods.com/v2")

service = BaseService(
    servicename="port-irods",
    implements=["metadata"],
    fileTransferMode=FileTransferMode.active,
    fileTransferArchive=FileTransferArchive.zip,
    description={"en": "Irods is a repository where users can make all of their research outputs available in a citable, shareable and discoverable manner",
                 "de": "irods is a repository where users can make all of their research outputs available in a citable, shareable and discoverable manner"},
    displayName="Irods",
    infoUrl="https://www.uu.nl/en/research/irods",
    helpUrl="https://www.uu.nl/en/research/irods/what-is-irods",
    icon="./irods.png"
)
 
Util.register_service(service)

# set the WSGI application callable to allow using uWSGI:
# uwsgi --http :8080 -w app
app.run(port=8080, server='gevent')
