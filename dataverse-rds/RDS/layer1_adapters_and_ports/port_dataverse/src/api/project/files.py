import inspect
import logging
from lib.Util import require_api_key, decode_string, encode_string
from flask import jsonify, request, g

logger = logging.getLogger()


# FIXME: all endpoints need server tests, but POST cannot currently be tested through pactman, because it only supports json as content type
@require_api_key
def index(project_id):
    logger.debug(f"Entering at api/project/files.py {inspect.getframeinfo(inspect.currentframe()).function}")

    logger.debug(f"### project_id files index: {project_id}")
    project_id = decode_string(project_id)
    logger.debug(f"### project_id files index: {project_id}")

    return g.dataverse.get_files_from_dataset(project_id)


@require_api_key
def get(project_id, file_id):
    logger.debug(f"Entering at api/project/files.py {inspect.getframeinfo(inspect.currentframe()).function}")
    logger.debug(f"### project_id files get: {project_id}")
    project_id = decode_string(project_id)
    logger.debug(f"### project_id files get: {project_id}")

    return g.dataverse.get_files_from_dataset(project_id)[file_id]


@require_api_key
def post(project_id):
    logger.debug(f"Entering at api/project/files.py {inspect.getframeinfo(inspect.currentframe()).function}")
    logger.debug(f"### project_id files post: {project_id}")
    project_id = decode_string(project_id)
    logger.debug(f"### project_id files post: {project_id}")

    logger.debug("Read file from request")
    file = request.files['file']

    req = request.form.to_dict()
    filename = req["filename"]
    logger.debug("file: {}, filename: {}".format(file, filename))

    logger.debug("Start file upload")
    resp = g.dataverse.upload_new_file_to_dataset(
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
    
    logger.debug(f"### project_id files patch: {project_id}")
    project_id = decode_string(project_id)
    logger.debug(f"### project_id files patch: {project_id}")

    raise NotImplementedError()


@require_api_key
def delete(project_id, file_id=None):
    logger.debug(f"Entering at api/project/files.py {inspect.getframeinfo(inspect.currentframe()).function}")

    logger.debug(f"### project_id files delete: {project_id}")
    project_id = decode_string(project_id)
    logger.debug(f"### project_id files delete: {project_id}")

    if file_id is None:
        return g.dataverse.delete_all_files_from_dataset(project_id)

    logger.error(f"Exception at api/project/files.py {inspect.getframeinfo(inspect.currentframe()).function}")
    raise NotImplementedError()
