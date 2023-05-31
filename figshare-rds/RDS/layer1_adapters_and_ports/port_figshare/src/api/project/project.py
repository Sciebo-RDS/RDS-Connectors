import inspect
import time
from RDS import ROParser
import logging
from lib.upload_figshare import Figshare
from flask import jsonify, request, g, current_app
from werkzeug.exceptions import abort
from lib.Util import require_api_key, to_jsonld, from_jsonld

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
    logger.debug(f"figshare_res: {res}")
    logger.debug(f"Entering at api/project/project.py {inspect.getframeinfo(inspect.currentframe()).function}")
  
    req = request.get_json(force=True)
    userId = req.get("userId")
    username = userId.split(":")[1].split("@")[0][2:]

    # this is the minimum of metadata that zenodo requires
    result = {
        'title': 'Untitled',
        'upload_type': 'other',
        'description': 'Uploaded from ScieboRDS',
        'creators': [{'name': f'{username}',
                      'affiliation': ''}]
    }

    # setting title
    try:
        result['title'] = res['name']
    except:
        result["title"] = f"Upload from ScieboRDS - {time.strftime('%Y-%m-%d')}"
    
    # setting description
    try:
        result["description"] = "".join(res["description"])
    except:
        result["description"] = f"Uploaded from ScieboRDS - {time.strftime('%Y-%m-%d')}"
    
    # setting publication_date
    try:
        result["publication_date"] = res["datePublished"][0]
    except:
        result["publication_date"] = time.strftime('%Y-%m-%d')
    
    # setting creators
    try:
        if not isinstance(res["creator"], list):
            res["creator"] = [res["creator"]]
        creators = []
        creator = {}
        for item in res["creator"]:
            if isinstance(item, str):
                creator['name'] = item
            else:
                if item['@type'] == 'Person':
                    creator['name'] = item['name']
                if item['@type'] == 'Organization':
                    creator['affiliation'] = item['name']
        creators.append(creator)
        result["creators"] = creators
    except:
        pass


    # setting funder
    try:
        if not isinstance(res["funder"], list):
            res["funder"] = [res["funder"]]
        funding = ""
        for item in res["funder"]:
            if isinstance(item, str):
                funding = item
            else:
                if item['@type'] == 'Person':
                    funding = item['name']
                if item['@type'] == 'Organization':
                    funding = item['name']
        result["funding"] = funding
    except:
        pass


    logger.debug(f"figshare_res: {result}")
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

        logger.debug(f"### articleResponse: {articleResponse}, {articleResponse.json()} ###")

        if articleResponse.status_code < 300:

            article_id = str(articleResponse.json()["entity_id"])
            try:
                metadata = articleResponse.json()['metadata']
            except:
                metadata = {}
            
            result = jsonify(
                {
                    "projectId": article_id,
                    "metadata": metadata,
                }
            )
            
            logger.debug(f"### post: {article_id}, {metadata} ###")
            return result

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
    logger.debug("original data: {}".format(metadata))
    if metadata is not None:
        try:
            try:
                doc = ROParser(metadata)
                docexpanded = doc.getElement(doc.rootIdentifier, expand=True, clean=False)
                logger.debug(f"doc: {docexpanded}")
                metadata = figshare(doc.getElement(
                    doc.rootIdentifier, expand=True, clean=False))
            except:
                metadata = figshare(metadata)
        except Exception as e:
            logger.debug("Error ROParser")
            logger.error(e, exc_info=True)

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
