import inspect
import logging
from lib.Util import require_api_key
from flask import jsonify, request, g

logger = logging.getLogger()


# FIXME: all endpoints need server tests, but POST cannot currently be tested through pactman, because it only supports json as content type
@require_api_key
def index(project_id):
    logger.debug(f"Entering at api/project/files.py {inspect.getframeinfo(inspect.currentframe()).function}")
    return g.figshare.get_files_from_article(project_id)


@require_api_key
def get(project_id, file_id):
    logger.debug(f"Entering at api/project/files.py {inspect.getframeinfo(inspect.currentframe()).function}")
    return g.figshare.get_files_from_article(project_id)[file_id]


@require_api_key
def post(project_id):
    logger.debug(f"Entering at api/project/files.py {inspect.getframeinfo(inspect.currentframe()).function}")
    logger.debug("Read file from request")
    file = request.files['file']

    req = request.form.to_dict()
    filename = req["filename"]
    logger.debug("file: {}, filename: {}".format(file, filename))

    logger.debug("Start file upload")
    resp = g.figshare.upload_new_file_to_article(
        project_id, filename, file)
    logger.debug("Finished file upload")

    if resp:
        return jsonify({"success": True})

    else:
        logger.error(f"Exception at api/project/files.py {inspect.getframeinfo(inspect.currentframe()).function}")
        raise ValueError("Upload failed.")


@require_api_key
def patch(project_id, file_id):
    logger.error(f"Exception at api/project/files.py {inspect.getframeinfo(inspect.currentframe()).function}")
    raise NotImplementedError()


@require_api_key
def delete(project_id, file_id=None):
    logger.debug(f"Entering at api/project/files.py {inspect.getframeinfo(inspect.currentframe()).function}")
    if file_id is None:
        return g.figshare.delete_all_files_from_article(project_id)

    logger.error(f"Exception at api/project/files.py {inspect.getframeinfo(inspect.currentframe()).function}")
    raise NotImplementedError()
