import inspect
from RDS import ROParser
import logging
from flask import jsonify, request, g
from werkzeug.exceptions import abort
from lib.Util import require_api_key, to_jsonld

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
        except Exception as e:
            logger.error(f"Exception at api/project/project.py {inspect.getframeinfo(inspect.currentframe()).function}")
            logger.error(e, exc_info=True)
            metadata = dataset

        output.append({
            "projectId": project_id,
            "metadata": metadata
        })

    return jsonify(output)


@require_api_key
def get(project_id):
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
    result = {}

    result["title"] = res["name"]
    result["description"] = res["description"]
    creator = res["creator"]
    result["publication_date"] = res["datePublished"]

    creator = []

    if not isinstance(res["creator"], list):
        res["creator"] = [res["creator"]]

    for c in res["creator"]:
        if isinstance(c, str):
            creator.append({
                "name": c
            })
        else:
            creator.append(c)

    result["creators"] = creator

    if res["dataversecategory"].find("/") > 0:
        typ, subtyp = tuple(res["dataversecategory"].split("/", 1))
        result["upload_type"] = typ
        result["{}_type".format(typ)] = subtyp

    return result


@require_api_key
def post():
    logger.debug(f"Entering at api/project/project.py {inspect.getframeinfo(inspect.currentframe()).function}")
    try:
        req = request.get_json(force=True)
        metadata = req.get("metadata")

        logger.debug(f"got metadata: {metadata}")

        if metadata is not None:
            try:
                doc = ROParser(metadata)
                metadata = dataverse(doc.getElement(
                    doc.rootIdentifier, expand=True, clean=True))
            except Exception as e:
                logger.error(f"Exception at api/project/project.py {inspect.getframeinfo(inspect.currentframe()).function}")
                logger.error(e, exc_info=True)

        logger.debug("send metadata: {}".format(metadata))

        datasetResponse = g.dataverse.create_new_dataset_internal(
            metadata=metadata, return_response=True
        )

        if datasetResponse.status_code < 300:
            datasetResponse = datasetResponse.json()
            return jsonify(
                {
                    "projectId": str(datasetResponse["data"]["persistentId"]),
                    "metadata": metadata,
                }
            )

        abort(datasetResponse.status_code)
    except Exception as e:
        logger.error(f"Exception at api/project/project.py {inspect.getframeinfo(inspect.currentframe()).function}, outer exception")
        logger.error(e, exc_info=True)
        abort(500)


@require_api_key
def delete(project_id):
    logger.debug(f"Entering at api/project/project.py {inspect.getframeinfo(inspect.currentframe()).function}")
    if g.dataverse.remove_dataset_internal(int(project_id)):
        return "", 204

    abort(404)


@require_api_key
def patch(project_id):
    logger.debug(f"Entering at api/project/project.py {inspect.getframeinfo(inspect.currentframe()).function}")
    req = request.get_json(force=True)
    logger.debug("request data: {}".format(req))

    metadata = req.get("metadata")
    if metadata is not None:
        try:
            doc = ROParser(metadata)
            metadata = dataverse(doc.getElement(
                doc.rootIdentifier, expand=True, clean=True))
        except Exception as e:
            logger.error(f"Exception at api/project/project.py {inspect.getframeinfo(inspect.currentframe()).function}")
            logger.error(e, exc_info=True)

    userId = req.get("userId")
    logger.debug("transformed data: {}".format(metadata))

    datasetResponse = g.dataverse.change_metadata_in_dataset_internal(
        dataset_id=int(project_id), metadata=metadata, return_response=True
    )

    if datasetResponse.status_code == 200:
        output = datasetResponse.json()

        logger.debug("output: {}".format(output))

        try:
            output["metadata"] = to_jsonld(output["metadata"])

        except Exception as e:
            logger.error(f"Exception at api/project/project.py {inspect.getframeinfo(inspect.currentframe()).function}, second exception")
            logger.error(e, exc_info=True)

        logger.debug("finished output: {}".format(output))

        return jsonify(output["metadata"])

    abort(datasetResponse.status_code)


@require_api_key
def put(project_id):
    logger.debug(f"Entering at api/project/project.py {inspect.getframeinfo(inspect.currentframe()).function}")
    if g.dataverse.publish_dataset_internal(dataset_id=int(project_id)):
        return True, 200

    abort(400)
