import inspect
from functools import wraps
from lib.upload_figshare import Figshare
from flask import request, g, current_app, abort
import os
import requests
import logging
from pyld import jsonld
import json
from RDS import Util

logger = logging.getLogger()


def require_api_key(api_method):
    @wraps(api_method)
    def check_api_key(*args, **kwargs):
        g.figshare = None

        try:
            req = request.get_json(force=True)
        except Exception as e:
            logger.error(f"Exception at lib/Util.py {inspect.getframeinfo(inspect.currentframe()).function}")
            logger.error(e, exc_info=True)
            req = request.form.to_dict()
        logger.debug("got request data: {}".format(req))

        try:
            service, userId, apiKey = Util.parseUserId(req.get("userId"))
        except Exception as e:
            logger.error(f"Exception at lib/Util.py {inspect.getframeinfo(inspect.currentframe()).function}")
            logger.error(e, exc_info=True)
            apiKey = Util.loadToken(
                req.get("userId"), "port-figshare").access_token

        if apiKey is None:
            logger.error(f"Exception at lib/Util.py {inspect.getframeinfo(inspect.currentframe()).function}")
            logger.error("apiKey or userId not found.")
            abort(401)

        logger.debug("found apiKey")
        g.figshare = Figshare(apiKey, api_address=current_app.figshare_api_address)

        return api_method(*args, **kwargs)

    return check_api_key


figshare_to_jsonld = {
    "description": "https://schema.org/description",
    "tags": "https://schema.org/keywords",
    "access_right": "https://schema.org/publicAccess",
    "publication_date": "https://schema.org/datePublished",
    "id": "https://schema.org/identifier",
    "figsharecategory": "https://www.research-data-services.org/jsonld/figsharecategory",
    "license": "https://schema.org/license",
    "doi": "https://www.research-data-services.org/jsonld/doi",
    "creators": "https://schema.org/creator",
    "affiliation": "https://schema.org/affiliation",
    "name": "https://schema.org/name",
    "title": "https://schema.org/name",
}


def to_jsonld(metadata):
    logger.debug(f"Entering at lib/Util.py {inspect.getframeinfo(inspect.currentframe()).function}")
    def parse_creator(user):
        output = {}
        errors = False

        parameterlist = [
            ("affiliation"),
            ("name"),
        ]
        for parameter in parameterlist:
            try:
                output[figshare_to_jsonld[parameter]] = creator[parameter]
            except KeyError as e:
                logger.error(f"Exception at lib/Util.py {inspect.getframeinfo(inspect.currentframe()).function}")
                logger.error(e)
                errors = True

        return output

    logger.debug("got metadata {}".format(metadata))

    try:
        figsharecategory = "{}/{}".format(
            metadata["upload_type"], metadata["{}_type".format(
                metadata["upload_type"])]
        )
    except:
        try:
            figsharecategory = metadata["upload_type"]
        except:
            figsharecategory = None

    creators = []

    try:
        for creator in metadata["creators"]:
            creators.append(parse_creator(creator))
    except:
        creators.append(parse_creator(metadata["creators"]))

    jsonld = {figshare_to_jsonld["creators"]: creators}

    if figsharecategory is not None:
        jsonld[figshare_to_jsonld["figsharecategory"]] = figsharecategory

    parameterlist = [
        ("title"),
        ("description"),
        ("doi", ["prereserve_doi", "doi"]),
        ("id", ["prereserve_doi", "recid"]),
        ("publication_date"),
        ("license"),
    ]

    for parameter in parameterlist:
        try:
            left, right = parameter
            data = metadata

            for attr in right:
                data = data[attr]

            jsonld[figshare_to_jsonld[left]] = data
        except:
            try:
                jsonld[figshare_to_jsonld[parameter]] = metadata[parameter]
            except Exception as e:
                logger.debug("key {} not found.".format(e))

    try:
        publicAccess = metadata["access_right"] == "open"
        jsonld[figshare_to_jsonld["access_right"]] = publicAccess
    except:
        jsonld[figshare_to_jsonld["access_right"]] = True

    return jsonld


def from_jsonld(jsonld_data):
    logger.debug(f"Entering at lib/Util.py {inspect.getframeinfo(inspect.currentframe()).function}")
    if jsonld_data is None:
        return

    try:
        frame = json.load(open("src/lib/ffigshare.jsonld"))
    except:
        frame = json.load(open("lib/ffigshare.jsonld"))

    logger.debug("before transformation data: {}".format(jsonld_data))
    data = jsonld.frame(jsonld_data, frame)
    logger.debug("after framing: {}".format(data))

    data["title"] = data[figshare_to_jsonld["title"].replace(
        "https://schema.org/", "")]
    del data[figshare_to_jsonld["title"].replace("https://schema.org/", "")]

    data["creators"] = []

    data["publication_date"] = data[
        figshare_to_jsonld["publication_date"].replace("https://schema.org/", "")
    ]
    del data[figshare_to_jsonld["publication_date"].replace(
        "https://schema.org/", "")]

    def read_creator(creator):
        try:
            del creator["@id"]
            del creator["@type"]
        except Exception as e:
            logger.error(f"Exception at lib/Util.py {inspect.getframeinfo(inspect.currentframe()).function}")
            logger.error(e, exc_info=True)

        try:
            creator["affiliation"] = creator["affiliation"]["name"]
        except Exception as e:
            if "affiliation" in creator:
                del creator["affiliation"]
            logger.error(f"Exception at lib/Util.py {inspect.getframeinfo(inspect.currentframe()).function}")
            logger.error(e, exc_info=True)

        data["creators"].append(creator)

    if isinstance(data["creator"], list):
        for creator in data["creator"]:
            read_creator(creator)
    else:
        read_creator(data["creator"])

    del data["creator"]

    # if data["upload_type"].find("/") > 0:
    #     typ, subtyp = tuple(data["upload_type"].split("/", 1))
    #     data["upload_type"] = typ
    #     data["{}_type".format(typ)] = subtyp

    try:
        del data["@context"]
        del data["@id"]
        del data["@type"]
    except:
        pass

    data["description"] = data["description"].replace("\n", "<br>")
    logger.debug("after transformation data: {}".format(data))

    return data
