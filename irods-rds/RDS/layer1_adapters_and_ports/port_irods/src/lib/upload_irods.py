import inspect
import os
import logging
from flask import abort
import functools
import time
from irods.session import iRODSSession

ssl_settings = {'client_server_negotiation': 'request_server_negotiation',
                'client_server_policy': 'CS_NEG_REQUIRE',
                'encryption_algorithm': 'AES-256-CBC',
                'encryption_key_size': 32,
                'encryption_num_hash_rounds': 16,
                'encryption_salt_size': 8,
                }

zone = 'irods'
folder = 'research-surfbasic'

log = logging.getLogger()

def _rate_limit(func=None, per_second=1):
    """Limit number of requests made per second.

    Will sleep for 1/``per_second`` seconds if the last request was
    made too recently.
    """

    if not func:
        return functools.partial(_rate_limit, per_second=per_second)

    @functools.wraps(func)
    def wrapper(self, *args, **kwargs):
        if hasattr(self, "last_request") and self.last_request is not None:
            now = time.time()
            delta = now - self.last_request
            if delta < (1 / per_second):
                waittimer = (1 / per_second) - delta
                log.debug("rate limiter wait for {}ms", waittimer)
                time.sleep(waittimer)

        self.last_request = time.time()
        return func(self, *args, **kwargs)

    return wrapper


class Irods(object):

    def __init__(self, api_key, user, api_address=None, *args, **kwargs):
        self.irods_api_address = api_address
        if api_address is None:
            self.irods_api_address = os.getenv(
                "IRODS_API_ADDRESS", "scuba-irods.irods.surfsara.nl"
            )

        self.api_key = api_key
        self.user = user

        # monkeypatching all functions with internals
        self.get_collection = self.get_collection_internal
        self.create_new_collection = self.create_new_collection_internal
        self.remove_collection = self.remove_collection_internal
        self.upload_new_file_to_collection = self.upload_new_file_to_collection_internal
        self.change_metadata_in_collection = self.change_metadata_in_collection_internal
        self.publish_collection = self.publish_collection_internal
        self.delete_all_files_from_collection = (
            self.delete_all_files_from_collection_internal
        )


    @classmethod
    def get_collection(cls, api_key, *args, **kwargs):
        return cls(api_key, *args, **kwargs).get_collection(*args, **kwargs)

    @classmethod
    def create_new_collection(cls, api_key, *args, **kwargs):
        return cls(api_key, *args, **kwargs).create_new_collection_internal(
            *args, **kwargs
        )

    @classmethod
    def remove_collection(cls, api_key, *args, **kwargs):
        return cls(api_key, *args, **kwargs).remove_collection(*args, **kwargs)

    @classmethod
    def upload_new_file_to_collection(cls, api_key, *args, **kwargs):
        return cls(api_key, *args, **kwargs).upload_new_file_to_collection(
            *args, **kwargs
        )

    @classmethod
    def change_metadata_in_collection(cls, api_key, *args, **kwargs):
        return cls(api_key, *args, **kwargs).change_metadata_in_collection_internal(
            *args, **kwargs
        )

    @classmethod
    def publish_collection(cls, api_key, *args, **kwargs):
        return cls(api_key).publish_collection(*args, **kwargs)

    @classmethod
    def delete_all_files_from_collection(cls, api_key, *args, **kwargs):
        return cls(api_key).delete_all_files_from_collection_internal(*args, **kwargs)

    @classmethod
    def check_token(cls, api_key, *args, **kwargs):
        """Check the API-Token `api_key`.

        Returns `True` if the token is correct and usable, otherwise `False`."""
        log.debug("Check token: Starts")
        try:
            r = cls(api_key, *args, **kwargs).get_collection()
            return True
        except:
            return False

    @_rate_limit(per_second=5)
    def get_collection_internal(
        self, path: str = None, metadataFilter: dict = None
    ):
        """Get all collections for the account, which owns the api-key.

        Args:
            path (str, optional): The collection path. Defaults to None.
            metadataFilter (dict, optional): filter for selecting what data needs to returned. Defaults to None.
                                                Currently not implemented.

        Returns:
            list: : List of paths of sub collections if return_response is set to True
        """
        log.debug(
            f"Entering at lib/upload_irods.py {inspect.getframeinfo(inspect.currentframe()).function}")
        log.debug(
            f"get collections from irods, path? {path}, metadata? {metadataFilter}"
        )

        available_collections = []

        if path is None:
            path = f"/{zone}/home"

        with iRODSSession(host=self.irods_api_address,
                            port=1247,
                            user=self.user,
                            password=self.api_key,
                            zone=zone,
                            authentication_scheme='pam',
                            **ssl_settings) as session:
            coll = session.collections.get(path)
            for col in coll.subcollections:
                    
                available_collection = {'create_time' : str(col.create_time),
                                        'data_objects' : [data_object.id for data_object in col.data_objects],
                                        'id' : col.id,
                                        'inheritance' : str(col.inheritance),
                                        'manager' : col.manager.get(col.path).id,
                                        'metadata' : {key : col.metadata.get_all(key)[0].value for key in col.metadata.keys()},
                                        'modify_time' : str(col.modify_time),
                                        'name' : str(col.name),
                                        'owner_name' : str(col.owner_name),
                                        'owner_zone' : str(col.owner_zone),
                                        'path' : str(col.path),
                                        'subcollections' : [col.path for col in col.subcollections]
                                        }
                available_collections.append(available_collection)

        return available_collections
            
    def create_new_collection_internal(self, metadata:dict = None):
        """Creates a new untitled collection.
        If metadata is specified, it will changes metadata after creating.

        Args:
            metadata (dict, optional): Metadata for the collection. Defaults to None.

        Returns:
            json: API response
        """
        log.debug(
            f"Entering at lib/upload_irods.py {inspect.getframeinfo(inspect.currentframe()).function}")
        log.debug("Create new collection: Starts")


        path = f"/{zone}/home/{folder}/untitled-{str(time.time()).replace('.','')}"

        with iRODSSession(host=self.irods_api_address,
                            port=1247,
                            user=self.user,
                            password=self.api_key,
                            zone=zone,
                            authentication_scheme='pam',
                            **ssl_settings) as session:
            coll = session.collections.create(path)

          
        available_collection = {'create_time' : str(coll.create_time),
                                'data_objects' : [data_object.id for data_object in coll.data_objects],
                                'id' : coll.id,
                                'inheritance' : str(coll.inheritance),
                                'manager' : coll.manager.get(coll.path).id,
                                'metadata' : {key : coll.metadata.get_all(key)[0].value for key in coll.metadata.keys()},
                                'modify_time' : str(coll.modify_time),
                                'name' : str(coll.name),
                                'owner_name' : str(coll.owner_name),
                                'owner_zone' : str(coll.owner_zone),
                                'path' : str(coll.path),
                                'subcollections' : [coll.path for col in coll.subcollections]
                                }


        log.debug(f"Metadata: {metadata}")
        if metadata is not None and isinstance(metadata, dict):
            log.debug(metadata)
            return self.change_metadata_in_collection_internal(
                coll.path, metadata, return_response=True
            )

        return available_collection

    def remove_collection_internal(self, path: str):
        """Will remove an collection based on it's path.

        Args:
            path (str): the collection path

        Returns:
            json: API response
        """
        log.debug(
            f"Entering at lib/upload_irods.py {inspect.getframeinfo(inspect.currentframe()).function}")
        try:
            with iRODSSession(host=self.irods_api_address,
                                port=1247,
                                user=self.user,
                                password=self.api_key,
                                zone=zone,
                                authentication_scheme='pam',
                                **ssl_settings) as session:
                r = session.collections.remove(path=path)
            if r is None:
                return True
        except:
            return False

    def upload_new_file_to_collection_internal(
        self, path, path_to_file, file=None, test=False
    ):
        """Uploads a file to an collection on Irods.

        Args:
            path (str): path of the collection to upload to
            path_to_file (str): path to the file to be uploaded. Example: ~/mydatapackage.csv
            file (bytes, optional): file as a bytes object. Defaults to None.

        Returns:
            json: Response Data of irods API
        """
        log.debug(
            f"Entering at lib/upload_irods.py {inspect.getframeinfo(inspect.currentframe()).function}")
        try:
            # in testing we do not have a file object passed in
            if not test:
                with open(path_to_file, 'wb') as ff:
                    ff.write(file.read())

            with iRODSSession(host=self.irods_api_address,
                                port=1247,
                                user=self.user,
                                password=self.api_key,
                                zone=zone,
                                authentication_scheme='pam',
                                **ssl_settings) as session:
                session.connection_timeout = 300
                r = session.data_objects.put(path_to_file, path)
            return {"success": True}
        except Exception as e:
            log.error(
                f"Exception at lib/upload_irods.py {inspect.getframeinfo(inspect.currentframe()).function}")
            log.error(str(e))


    def get_files_from_collection(self, path):
        """will get all the files from the collection requested.

        Args:
            path (str): path of the collection

        Returns:
            list: list containing all the file paths from the collection
        """
        log.debug(
            f"Entering at lib/upload_irods.py {inspect.getframeinfo(inspect.currentframe()).function}")
        with iRODSSession(host=self.irods_api_address,
                            port=1247,
                            user=self.user,
                            password=self.api_key,
                            zone=zone,
                            authentication_scheme='pam',
                            **ssl_settings) as session:
            coll = session.collections.get(path)
            result = []
            for obj in coll.data_objects:
                result.append(obj.path)
        return result

    def change_metadata_in_collection_internal(
        self, path, metadata
    ):
        """Will update an collection according to the provided metadata.

        Args:
            collection_id (int): id of the collection
            return_response (bool, optional): Set to True will return the API response. Defaults to False.
            metadata (dict): A data-dict json-like object
                ```python
                Example: data = {
                    'metadata': {
                        'title': 'My first upload',
                        'upload_type': 'poster',
                        'description': 'This is my first upload',
                        'creators': [{'name': 'Doe, John',
                                    'affiliation': 'Irods'}]
                    }
                }
                ```
        Returns:
            object: response of the PUT request to the collections endpoint
        """
        log.debug(
            f"Entering at lib/upload_irods.py {inspect.getframeinfo(inspect.currentframe()).function}")

        data = {}
        data = metadata

        log.info(f"send data: {data}")

        raise NotImplementedError()

    def publish_collection_internal(self, collection_id, return_response=False):
        """Will publish an collection if it is not under embargo

        Args:
            collection_id (_type_): id of the collection
            return_response (bool, optional): Set to True will return the API response. Defaults to False.

        Returns:
            object: response of the POST request to the collections publish endpoint
        """
        log.debug(
            f"Entering at lib/upload_irods.py {inspect.getframeinfo(inspect.currentframe()).function}")

        raise NotImplementedError()

    def delete_all_files_from_collection_internal(self, path):
        """Will delete all files from an collection.

        Args:
            path (str): path of an collection

        Returns:
            bool: True if successful, False if not
        """
        log.debug(
            f"Entering at lib/upload_irods.py {inspect.getframeinfo(inspect.currentframe()).function}")
        try:
            with iRODSSession(host=self.irods_api_address,
                                port=1247,
                                user=self.user,
                                password=self.api_key,
                                zone=zone,
                                authentication_scheme='pam',
                                **ssl_settings) as session:
                coll = session.collections.get(path)
                for obj in coll.data_objects:
                    obj.unlink(force=True)
                return True
        except:
            return False

    def delete_file_from_collection_internal(self, path):
        """Will delete a file from an collection

        Args:
            path (str): path of the file

        Returns:
            bool: True if successful, False if not
        """
        log.debug(
            f"Entering at lib/upload_irods.py {inspect.getframeinfo(inspect.currentframe()).function}")
        try:
            with iRODSSession(host=self.irods_api_address,
                                port=1247,
                                user=self.user,
                                password=self.api_key,
                                zone=zone,
                                authentication_scheme='pam',
                                **ssl_settings) as session:
                obj = session.data_objects.get(path)
                r = obj.unlink(force=True)

            if r is None:
                return True
            return False
        except:
            return False
