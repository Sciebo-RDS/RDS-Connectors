import inspect
from RDS import ROParser
import logging
from flask import jsonify, request, g
from werkzeug.exceptions import abort
from lib.Util import require_api_key, to_jsonld, encode_path, decode_path

logger = logging.getLogger()


@require_api_key
def index():
    logger.debug(f"Entering at api/project/project.py {inspect.getframeinfo(inspect.currentframe()).function}")

    req = request.json.get("metadata")
    
    collectionResponse = g.irods.get_collection(metadataFilter=req)
    logger.debug("collection response: {}".format(collectionResponse))

    output = []
    for collection in collectionResponse:
        try:
            metadata = to_jsonld(collection)

        except Exception as e:
            logger.error(f"Exception at api/project/project.py {inspect.getframeinfo(inspect.currentframe()).function}")
            logger.error(e, exc_info=True)
            metadata = collection

        output.append({
            "projectId": encode_path(collection["path"]),
            "metadata": metadata
        })

    return jsonify(output)


@require_api_key
def get(project_id):
    logger.debug(f"Entering at api/project/project.py {inspect.getframeinfo(inspect.currentframe()).function}")
    req = request.json.get("metadata")

    collectionResponse = g.irods.get_collection(
        path=decode_path(project_id), metadataFilter=req)

    logger.debug("collection reponse: {}".format(collectionResponse))

    output = collectionResponse
    try:
        output = to_jsonld(collectionResponse.get("metadata") or collectionResponse)

    except Exception as e:
        logger.error(f"Exception at api/project/project.py {inspect.getframeinfo(inspect.currentframe()).function}")
        logger.error(e, exc_info=True)
        output = collectionResponse

    logger.debug("output: {}".format(output))

    return jsonify(output)


def irods(res):
    logger.debug(f"### before: {res}")
    logger.debug(f"Entering at api/project/project.py {inspect.getframeinfo(inspect.currentframe()).function}")
    result = {}
    # {'Affiliation': 'Surf',
    #         'Description': 'Dit is een test 123',
    #         'Family_Name': 'Trompert',
    #         'Given_Name': 'David',
    #         'Title': 'test123'}

    # result["title"] = res["name"]
    # result["description"] = res["description"]
    # creator = res["creator"]
    # result["publication_date"] = res["datePublished"]

    # creator = []

    # if not isinstance(res["creator"], list):
    #     res["creator"] = [res["creator"]]

    # for c in res["creator"]:
    #     if isinstance(c, str):
    #         creator.append({
    #             "name": c
    #         })
    #     else:
    #         creator.append(c)

    # result["creators"] = creator

    # if res["irodscategory"].find("/") > 0:
    #     typ, subtyp = tuple(res["irodscategory"].split("/", 1))
    #     result["upload_type"] = typ
    #     result["{}_type".format(typ)] = subtyp
    # logger.debug(f"### after: {result}")
    # return result
    return result


@require_api_key
def post():
    logger.debug(f"Entering at api/project/project.py {inspect.getframeinfo(inspect.currentframe()).function}")
    try:
        req = request.get_json(force=True)
        logger.debug(f"### req: {req}")
        
        metadata = req.get("metadata")
        logger.debug(f"### got metadata: {metadata}")

        try:
            if metadata is None:
                metadata = irods({})
            else:
                doc = ROParser(metadata)
                metadata = irods(doc.getElement(
                    doc.rootIdentifier, expand=True, clean=False))
        except Exception as e:
            logger.error(f"Exception at api/project/project.py {inspect.getframeinfo(inspect.currentframe()).function}")
            logger.error(e, exc_info=True)

        logger.debug("### send metadata: {}".format(metadata))

        collectionResponse = g.irods.create_new_collection_internal(
            metadata=metadata
        )
        logger.debug(f"### collectionResponse: {collectionResponse}")

        if collectionResponse is not None:
            return jsonify(
                {
                    "projectId": encode_path(collectionResponse["path"]),
                    "metadata": collectionResponse["metadata"],
                }
            )

        abort(collectionResponse)
    except Exception as e:
        logger.error(f"Exception at api/project/project.py {inspect.getframeinfo(inspect.currentframe()).function}, outer exception")
        logger.error(e, exc_info=True)
        abort(500)


@require_api_key
def delete(project_id):
    logger.debug(f"Entering at api/project/project.py {inspect.getframeinfo(inspect.currentframe()).function}")
    if g.irods.remove_collection_internal(path=decode_path(project_id)):
        return "", 204

    abort(404)


@require_api_key
def patch(project_id):
    logger.debug(f"Entering at api/project/project.py {inspect.getframeinfo(inspect.currentframe()).function}")
    logger.debug("Not implemented")
    return True, 200


@require_api_key
def put(project_id):
    logger.debug(f"Entering at api/project/project.py {inspect.getframeinfo(inspect.currentframe()).function}")
    logger.debug("Not implemented")
    return True, 200
