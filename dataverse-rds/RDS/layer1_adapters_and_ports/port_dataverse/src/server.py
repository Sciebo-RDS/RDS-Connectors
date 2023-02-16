#!/usr/bin/env python

from RDS import Util, BaseService, FileTransferMode, FileTransferArchive
from __init__ import app
import os


redirect_uri = os.getenv("RDS_OAUTH_REDIRECT_URI", "")
dataverse_address = os.getenv("DATAVERSE_ADDRESS", "https://dataverse.com")
dataverse_api_address = os.getenv("DATAVERSE_API_ADDRESS", "https://api.dataverse.com/v2")
dataverse_oauth_token_url = dataverse_api_address + "/token"
dataverse_oauth_id = os.getenv("DATAVERSE_OAUTH_CLIENT_ID", "XY")
dataverse_oauth_secret = os.getenv("DATAVERSE_OAUTH_CLIENT_SECRET", "ABC")

dataverse_oauth_authorize = "{}/account/applications/authorize?redirect_uri={}&response_type=code&scope=all&client_id={}".format(
    dataverse_address, redirect_uri, dataverse_oauth_id
)
service = BaseService(
    servicename="port-dataverse",
    implements=["metadata"],
    fileTransferMode=FileTransferMode.active,
    fileTransferArchive=FileTransferArchive.zip,
    description={"en": "Dataverse is a repository where users can make all of their research outputs available in a citable, shareable and discoverable manner",
                 "de": "dataverse is a repository where users can make all of their research outputs available in a citable, shareable and discoverable manner"},
    displayName="Dataverse",
    infoUrl="https://dataverse.knaw.nl/en/data-stations/archaeology/",
    helpUrl="https://archaeology.datastations.nl/",
    icon="./dataverse.png"
)

Util.register_service(service)

# set the WSGI application callable to allow using uWSGI:
# uwsgi --http :8080 -w app
app.run(port=8080, server='gevent')
