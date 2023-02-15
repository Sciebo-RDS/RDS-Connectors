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
    
    articleResponse = g.figshare.get_article(metadataFilter=req)
    logger.debug("article response: {}".format(articleResponse))

    output = []
    for article in articleResponse:
        try:
            metadata = to_jsonld(article)

        except Exception as e:
            logger.error(f"Exception at api/project/project.py {inspect.getframeinfo(inspect.currentframe()).function}")
            logger.error(e, exc_info=True)
            metadata = article

        output.append({
            "projectId": str(article["prereserve_doi"]["recid"]),
            "metadata": metadata
        })

    return jsonify(output)


@require_api_key
def get(project_id):
    logger.debug(f"Entering at api/project/project.py {inspect.getframeinfo(inspect.currentframe()).function}")
    req = request.json.get("metadata")

    articleResponse = g.figshare.get_article(
        id=int(project_id), metadataFilter=req)

    logger.debug("article reponse: {}".format(articleResponse))

    output = articleResponse
    try:
        output = to_jsonld(articleResponse.get("metadata") or articleResponse)

    except Exception as e:
        logger.error(f"Exception at api/project/project.py {inspect.getframeinfo(inspect.currentframe()).function}")
        logger.error(e, exc_info=True)
        output = articleResponse

    logger.debug("output: {}".format(output))

    return jsonify(output)


def figshare(res):
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

    if res["figsharecategory"].find("/") > 0:
        typ, subtyp = tuple(res["figsharecategory"].split("/", 1))
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
                metadata = figshare(doc.getElement(
                    doc.rootIdentifier, expand=True, clean=True))
            except Exception as e:
                logger.error(f"Exception at api/project/project.py {inspect.getframeinfo(inspect.currentframe()).function}")
                logger.error(e, exc_info=True)

        logger.debug("send metadata: {}".format(metadata))

        articleResponse = g.figshare.create_new_article_internal(
            metadata=metadata, return_response=True
        )

        if articleResponse.status_code < 300:
            articleResponse = articleResponse.json()
            return jsonify(
                {
                    "projectId": str(articleResponse.get("id")),
                    "metadata": articleResponse.get("metadata"),
                }
            )

        abort(articleResponse.status_code)
    except Exception as e:
        logger.error(f"Exception at api/project/project.py {inspect.getframeinfo(inspect.currentframe()).function}, outer exception")
        logger.error(e, exc_info=True)
        abort(500)


@require_api_key
def delete(project_id):
    logger.debug(f"Entering at api/project/project.py {inspect.getframeinfo(inspect.currentframe()).function}")
    if g.figshare.remove_article_internal(int(project_id)):
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
            metadata = figshare(doc.getElement(
                doc.rootIdentifier, expand=True, clean=True))
        except Exception as e:
            logger.error(f"Exception at api/project/project.py {inspect.getframeinfo(inspect.currentframe()).function}")
            logger.error(e, exc_info=True)

    userId = req.get("userId")
    logger.debug("transformed data: {}".format(metadata))

    articleResponse = g.figshare.change_metadata_in_article_internal(
        article_id=int(project_id), metadata=metadata, return_response=True
    )

    if articleResponse.status_code == 200:
        output = articleResponse.json()

        logger.debug("output: {}".format(output))

        try:
            output["metadata"] = to_jsonld(output["metadata"])

        except Exception as e:
            logger.error(f"Exception at api/project/project.py {inspect.getframeinfo(inspect.currentframe()).function}, second exception")
            logger.error(e, exc_info=True)

        logger.debug("finished output: {}".format(output))

        return jsonify(output["metadata"])

    abort(articleResponse.status_code)


@require_api_key
def put(project_id):
    logger.debug(f"Entering at api/project/project.py {inspect.getframeinfo(inspect.currentframe()).function}")
    if g.figshare.publish_article_internal(article_id=int(project_id)):
        return True, 200

    abort(400)
