#!/usr/bin/env python

from RDS import Util, OAuth2Service, FileTransferMode, FileTransferArchive
from __init__ import app
import os


redirect_uri = os.getenv("RDS_OAUTH_REDIRECT_URI", "")
figshare_address = os.getenv("FIGSHARE_ADDRESS", "https://figshare.com")
figshare_api_address = os.getenv("FIGSHARE_API_ADDRESS", "https://api.figshare.com/v2")
figshare_oauth_token_url = figshare_api_address + "/token"
figshare_oauth_id = os.getenv("FIGSHARE_OAUTH_CLIENT_ID", "XY")
figshare_oauth_secret = os.getenv("FIGSHARE_OAUTH_CLIENT_SECRET", "ABC")

figshare_oauth_authorize = "{}/account/applications/authorize?redirect_uri={}&response_type=code&scope=all&client_id={}".format(
    figshare_address, redirect_uri, figshare_oauth_id
)

service = OAuth2Service(
    servicename="port-figshare",
    implements=["metadata"],
    fileTransferMode=FileTransferMode.active,
    fileTransferArchive=FileTransferArchive.zip,
    authorize_url=figshare_oauth_authorize,
    refresh_url=figshare_oauth_token_url,
    client_id=figshare_oauth_id,
    client_secret=figshare_oauth_secret,
    description={"en": "Figshare is a repository where users can make all of their research outputs available in a citable, shareable and discoverable manner",
                 "de": "figshare is a repository where users can make all of their research outputs available in a citable, shareable and discoverable manner"},
    displayName="Figshare",
    infoUrl="https://knowledge.figshare.com/about",
    helpUrl="https://help.figshare.com/",
    icon="./figshare.png"
)

Util.register_service(service)

# set the WSGI application callable to allow using uWSGI:
# uwsgi --http :8080 -w app
app.run(port=8080, server='gevent')
