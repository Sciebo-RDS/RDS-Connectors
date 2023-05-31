import inspect
import logging
import time

from flask import jsonify, request, g
from RDS import ROParser
from werkzeug.exceptions import abort

from lib.Util import require_api_key, to_jsonld, encode_string, decode_string

logger = logging.getLogger()


@require_api_key
def index():
    logger.debug(f"Entering at api/project/project.py {inspect.getframeinfo(inspect.currentframe()).function}")
    req = request.json.get("metadata")
    
    datasetResponse = g.dataverse.get_dataset(metadataFilter=req)['data']
    logger.debug("dataset response: {}".format(datasetResponse))

    output = []
    for dataset in datasetResponse:
        try:
            metadata = to_jsonld(dataset)
            project_id = g.dataverse.get_persistent_id_with_id(dataset["id"])
            logger.debug(f"### project_id index: {project_id}")
        except Exception as e:
            logger.error(f"Exception at api/project/project.py {inspect.getframeinfo(inspect.currentframe()).function}")
            logger.error(e, exc_info=True)
            metadata = dataset

        output.append({
            "projectId": encode_string(project_id),
            "metadata": metadata
        })

    return jsonify(output)


@require_api_key
def get(project_id):
    logger.debug(f"### project_id get: {project_id}")
    project_id = decode_string(project_id)
    logger.debug(f"### project_id get: {project_id}")
    logger.debug(f"Entering at api/project/project.py {inspect.getframeinfo(inspect.currentframe()).function}")
    req = request.json.get("metadata")

    datasetResponse = g.dataverse.get_dataset(
        persistent_id=project_id, metadataFilter=req)

    logger.debug("dataset reponse: {}".format(datasetResponse))

    output = datasetResponse
    try:
        output = to_jsonld(datasetResponse.get("metadataBlocks") or datasetResponse)

    except Exception as e:
        logger.error(f"Exception at api/project/project.py {inspect.getframeinfo(inspect.currentframe()).function}")
        logger.error(e, exc_info=True)
        output = datasetResponse

    logger.debug("output: {}".format(output))

    return jsonify(output)


def dataverse(res):
    logger.debug(f"Entering at api/project/project.py {inspect.getframeinfo(inspect.currentframe()).function}")
    logger.debug(f"### dataverse_res: {res}")

    try:
        req = request.get_json(force=True)
        userId = req.get("userId")
        useremail = userId.split(":")[1].split("//")[1]
        username = userId.split(":")[1].split("@")[0][2:]
    except:
        useremail = ""
        username = ""

    # setting title
    try:
        title = res['name']
    except:
        title = f"Upload from ScieboRDS - {time.strftime('%Y-%m-%d')}"

    # setting description
    try:
        dsDescriptionValue = "".join(res["description"])
    except:
        dsDescriptionValue = f"Uploaded from ScieboRDS - {time.strftime('%Y-%m-%d')}"

    # setting publication_date
    try:
        datePublished = res["datePublished"][0]
    except:
        datePublished = time.strftime('%Y-%m-%d')

    # setting creators
    authorName = ""
    authorAffiliation = ""
    try:
        if not isinstance(res["creator"], list):
            res["creator"] = [res["creator"]]
        for item in res["creator"]:
            if isinstance(item, str):
                authorName = item
            else:
                if item['@type'] == 'Person':
                    authorName = item['name']
                if item['@type'] == 'Organization':
                    authorAffiliation = item['name']
    except:
        authorName = username
        authorAffiliation = ""

    # setting contact
    datasetContactEmail = useremail
    datasetContactName = username

    # setting subject
    subject = "Other"

    result = {
            "metadataBlocks": {
                "citation": {
                    "fields": [
                        {
                            "value": title,
                            "typeClass": "primitive",
                            "multiple": False,
                            "typeName": "title"
                        },
                        {
                            "value": [
                                {
                                    "authorName": {
                                        "value": authorName,
                                        "typeClass": "primitive",
                                        "multiple": False,
                                        "typeName": "authorName"
                                    },
                                    "authorAffiliation": {
                                        "value": authorAffiliation,
                                        "typeClass": "primitive",
                                        "multiple": False,
                                        "typeName": "authorAffiliation"
                                    }
                                }
                            ],
                            "typeClass": "compound",
                            "multiple": True,
                            "typeName": "author"
                        },
                        {
                            "value": [
                                {
                                    "datasetContactEmail": {
                                        "typeClass": "primitive",
                                        "multiple": False,
                                        "typeName": "datasetContactEmail",
                                        "value": datasetContactEmail
                                    },
                                    "datasetContactName": {
                                        "typeClass": "primitive",
                                        "multiple": False,
                                        "typeName": "datasetContactName",
                                        "value": datasetContactName
                                    }
                                }
                            ],
                            "typeClass": "compound",
                            "multiple": True,
                            "typeName": "datasetContact"
                        },
                        {
                            "value": [
                                {
                                    "dsDescriptionValue": {
                                        "value": dsDescriptionValue,
                                        "multiple": False,
                                        "typeClass": "primitive",
                                        "typeName": "dsDescriptionValue"
                                    }
                                }
                            ],
                            "typeClass": "compound",
                            "multiple": True,
                            "typeName": "dsDescription"
                        },
                        {
                            "value": [
                                subject
                            ],
                            "typeClass": "controlledVocabulary",
                            "multiple": True,
                            "typeName": "subject"
                        }
                    ],
                    "displayName": "Citation Metadata"
                }
            }
        }

    logger.debug(f"### dataverse_result: {result}")
    return result


@require_api_key
def post():
    logger.debug(f"Entering at api/project/project.py {inspect.getframeinfo(inspect.currentframe()).function}")
    try:
        req = request.get_json(force=True)
        
        logger.debug(f"### req: {req}")
        metadata = req.get("metadata")

        logger.debug(f"### got metadata: {metadata}")

        # if metadata is not None:
        try:
            if metadata is None:
                metadata = dataverse({})
            else:
                try:
                    doc = ROParser(metadata)
                    expanddoc = doc.getElement(
                        doc.rootIdentifier, expand=True, clean=False)
                    logger.debug(f"### expanddoc: {expanddoc}")
                    metadata = dataverse(expanddoc)
                except:
                    metadata = dataverse({})
        except Exception as e:
            logger.error(f"Exception at api/project/project.py {inspect.getframeinfo(inspect.currentframe()).function}")
            logger.error(f"### {e}", exc_info=True)
        # else:
        #     metadata = {}

        logger.debug("### send metadata: {}".format(metadata))

        datasetResponse = g.dataverse.create_new_dataset_internal(
            metadata=metadata, return_response=True
        )

        if datasetResponse.status_code < 300:
            datasetResponse = datasetResponse.json()
            logger.debug(f"### datasetResponse: {datasetResponse}")
            result = jsonify(
                {
                    "projectId": encode_string(str(datasetResponse["data"]["persistentId"])),
                    "metadata": metadata,
                }
            )
            logger.debug(f"### result: {result}")
            return result
        abort(datasetResponse.status_code)
    except Exception as e:
        logger.error(f"Exception at api/project/project.py {inspect.getframeinfo(inspect.currentframe()).function}, outer exception")
        logger.error(e, exc_info=True)
        abort(500)


@require_api_key
def delete(project_id):
    logger.debug(f"### project_id delete: {project_id}")
    project_id = decode_string(project_id)
    logger.debug(f"### project_id delete: {project_id}")
    logger.debug(f"Entering at api/project/project.py {inspect.getframeinfo(inspect.currentframe()).function}")
    if g.dataverse.remove_dataset_internal(str(project_id)):
        return "", 204

    abort(404)


@require_api_key
def patch(project_id):
    logger.debug(f"### project_id patch: {project_id}")
    project_id = decode_string(project_id)
    logger.debug(f"### project_id patch: {project_id}")
    logger.debug(f"Entering at api/project/project.py {inspect.getframeinfo(inspect.currentframe()).function}")
    req = request.get_json(force=True)
    logger.debug("### request data: {}".format(req))

    metadata = req.get("metadata")
    logger.debug("### original data: {}".format(metadata))
    if metadata is not None:
        try:
            try:
                doc = ROParser(metadata)
                docexpanded = doc.getElement(doc.rootIdentifier, expand=True, clean=False)
                logger.debug(f"### doc: {docexpanded}")
                metadata = dataverse(docexpanded)
            except:
                metadata = dataverse({})
        except Exception as e:
            logger.debug("Error ROParser")
            logger.error(e, exc_info=True)

    logger.debug("### transformed data: {}".format(metadata))

    datasetResponse = g.dataverse.change_metadata_in_dataset_internal(
        persistent_id=project_id, metadata=metadata, return_response=True
    )
    logger.debug(f"### datasetResponse: {datasetResponse}")

    if datasetResponse.status_code == 200:
        output = datasetResponse.json()

        logger.debug("### output: {}".format(output))

        try:
            output["metadata"] = to_jsonld(output["metadata"])

        except Exception as e:
            logger.error(f"Exception at api/project/project.py {inspect.getframeinfo(inspect.currentframe()).function}, second exception")
            logger.error(e, exc_info=True)

        logger.debug("### finished output: {}".format(output))

        return jsonify(output["metadata"])

    abort(datasetResponse.status_code)


@require_api_key
def put(project_id):
    logger.debug(f"### project_id put: {project_id}")
    project_id = decode_string(project_id)
    logger.debug(f"### project_id put: {project_id}")
    logger.debug(f"Entering at api/project/project.py {inspect.getframeinfo(inspect.currentframe()).function}")
    if g.dataverse.publish_dataset_internal(persistent_id=project_id):
        return True, 200

    abort(400)
