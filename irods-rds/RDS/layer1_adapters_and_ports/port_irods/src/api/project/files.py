import inspect
import logging
from lib.Util import require_api_key, decode_path
from flask import g, request, jsonify

logger = logging.getLogger()


# FIXME: all endpoints need server tests, but POST cannot currently be tested through pactman, because it only supports json as content type
@require_api_key
def index(project_id):
    logger.debug(f"Entering at api/project/files.py {inspect.getframeinfo(inspect.currentframe()).function}")
    project_id = decode_path(project_id)
    return g.irods.get_files_from_collection(path=project_id)


@require_api_key
def get(project_id, file_id=None):
    logger.debug(f"Entering at api/project/files.py {inspect.getframeinfo(inspect.currentframe()).function}")

    project_id = decode_path(project_id)
    
    if file_id is None:
        return g.irods.get_files_from_collection(path=project_id)[0]
    else:
        file_id = decode_path(file_id)
        for obj in g.irods.get_files_from_collection(path=project_id):
            if obj.path == file_id:
                return obj
    

@require_api_key
def post(project_id):
    logger.debug(f"Entering at api/project/files.py {inspect.getframeinfo(inspect.currentframe()).function}")
    
    project_id = decode_path(project_id)

    logger.debug("Read file from request")
    file = request.files['file']

    req = request.form.to_dict()
    filename = req["filename"]
    logger.debug("file: {}, filename: {}".format(file, filename))

    logger.debug("Start file upload")
    
    resp = g.irods.upload_new_file_to_collection(
        path=project_id, path_to_file=filename, file=file)
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
    project_id = decode_path(project_id)
    if file_id is None:
        return g.irods.delete_all_files_from_collection(path=project_id)
    else:
        file_id = decode_path(file_id)
        return g.irods.delete_file_from_collection(path=file_id)