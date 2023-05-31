import base64
import inspect
from functools import wraps
from lib.upload_dataverse import Dataverse
from flask import request, g, current_app, abort
import logging
from pyld import jsonld
import json
from RDS import Util

logger = logging.getLogger()


def encode_string(s):
    """Will do base64 encoding of string.

    Args:
        path (str): human readable file or folder path

    Returns:
        _str: base64 version of the file or folder path
    """
    return base64.b64encode(bytes(s, 'utf-8')).decode('utf-8')


def decode_string(s):
    """Will do base64 decoding of string.

    Args:
        path (str): base64 version of the file or folder path

    Returns:
        str: human readable file or folder path
    """
    return base64.b64decode(bytes(s, 'utf-8')).decode('utf-8')


def require_api_key(api_method):
    @wraps(api_method)
    def check_api_key(*args, **kwargs):
        g.dataverse = None

        try:
            req = request.get_json(force=True)
        except:
            req = request.form.to_dict()
        logger.debug("got request data: {}".format(req))
        
        apiKey = None
        
        try:
            # DT: Here we need to implement our own matching between userId and apiKey
            #  look like the userId is stored like this:
            #  <port-name>://<username>:<password>
            userId = req.get("userId")
            if apiKey is None:
                # We can ask the user to supply the apikey as the password and use that.
                # we can parse it from the userID
                apiKey = userId.split(":")[-1]
        except Exception as e:
            logger.error(f"Exception at lib/Util.py {inspect.getframeinfo(inspect.currentframe()).function}")
            logger.error(e, exc_info=True)
        
        logger.debug(f"{userId}, {apiKey}")

        if apiKey is None:
            logger.error(f"Exception at lib/Util.py {inspect.getframeinfo(inspect.currentframe()).function}")
            logger.error("apiKey or userId not found.")
            abort(401)

        logger.debug("found apiKey")
        g.dataverse = Dataverse(apiKey, api_address=current_app.dataverse_api_address)

        return api_method(*args, **kwargs)

    return check_api_key


dataverse_to_jsonld = {
    "description": "https://schema.org/description",
    "tags": "https://schema.org/keywords",
    "access_right": "https://schema.org/publicAccess",
    "publication_date": "https://schema.org/datePublished",
    "id": "https://schema.org/identifier",
    "dataversecategory": "https://www.research-data-services.org/jsonld/dataversecategory",
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
                output[dataverse_to_jsonld[parameter]] = creator[parameter]
            except KeyError as e:
                logger.error(f"Exception at lib/Util.py {inspect.getframeinfo(inspect.currentframe()).function}")
                logger.error(e)
                errors = True

        return output

    logger.debug("got metadata {}".format(metadata))

    creators = []

    try:
        for creator in metadata["creators"]:
            creators.append(parse_creator(creator))
    except:
        creators.append(parse_creator(metadata["creators"]))

    jsonld = {dataverse_to_jsonld["creators"]: creators}

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

            jsonld[dataverse_to_jsonld[left]] = data
        except:
            try:
                jsonld[dataverse_to_jsonld[parameter]] = metadata[parameter]
            except Exception as e:
                logger.debug("key {} not found.".format(e))

    try:
        publicAccess = metadata["access_right"] == "open"
        jsonld[dataverse_to_jsonld["access_right"]] = publicAccess
    except:
        jsonld[dataverse_to_jsonld["access_right"]] = True

    return jsonld


def from_jsonld(jsonld_data):
    logger.debug(f"Entering at lib/Util.py {inspect.getframeinfo(inspect.currentframe()).function}")
    if jsonld_data is None:
        return

    try:
        frame = json.load(open("src/lib/fdataverse.jsonld"))
    except:
        frame = json.load(open("lib/fdataverse.jsonld"))

    logger.debug("before transformation data: {}".format(jsonld_data))
    data = jsonld.frame(jsonld_data, frame)
    logger.debug("after framing: {}".format(data))

    data["title"] = data[dataverse_to_jsonld["title"].replace(
        "https://schema.org/", "")]
    del data[dataverse_to_jsonld["title"].replace("https://schema.org/", "")]

    data["creators"] = []

    data["publication_date"] = data[
        dataverse_to_jsonld["publication_date"].replace("https://schema.org/", "")
    ]
    del data[dataverse_to_jsonld["publication_date"].replace(
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

    try:
        del data["@context"]
        del data["@id"]
        del data["@type"]
    except:
        pass

    data["description"] = data["description"].replace("\n", "<br>")
    logger.debug("after transformation data: {}".format(data))

    return data
