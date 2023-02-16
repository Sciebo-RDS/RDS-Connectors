import inspect
import requests
import json
import os
import logging
from flask import abort, request
import functools
import re
import time

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


class Dataverse(object):

    def __init__(self, api_key, api_address=None, *args, **kwargs):
        self.dataverse_api_address = api_address
        if api_address is None:
            self.dataverse_api_address = os.getenv(
                "DATAVERSE_API_ADDRESS", "https://demo.dataverse.org/api"
            )

        self.api_key = api_key

        # monkeypatching all functions with internals
        self.get_dataset = self.get_dataset_internal
        self.create_new_dataset = self.create_new_dataset_internal
        self.remove_dataset = self.remove_dataset_internal
        self.upload_new_file_to_dataset = self.upload_new_file_to_dataset_internal
        self.change_metadata_in_dataset = self.change_metadata_in_dataset_internal
        self.publish_dataset = self.publish_dataset_internal
        self.delete_all_files_from_dataset = (
            self.delete_all_files_from_dataset_internal
        )

    @classmethod
    def get_dataset(cls, api_key, *args, **kwargs):
        return cls(api_key, *args, **kwargs).get_dataset(*args, **kwargs)

    @classmethod
    def create_new_dataset(cls, api_key, *args, **kwargs):
        return cls(api_key, *args, **kwargs).create_new_dataset_internal(
            *args, **kwargs
        )

    @classmethod
    def remove_dataset(cls, api_key, *args, **kwargs):
        return cls(api_key, *args, **kwargs).remove_dataset(*args, **kwargs)

    @classmethod
    def upload_new_file_to_dataset(cls, api_key, *args, **kwargs):
        return cls(api_key, *args, **kwargs).upload_new_file_to_dataset(
            *args, **kwargs
        )

    @classmethod
    def change_metadata_in_dataset(cls, api_key, *args, **kwargs):
        return cls(api_key, *args, **kwargs).change_metadata_in_dataset_internal(
            *args, **kwargs
        )

    @classmethod
    def publish_dataset(cls, api_key, *args, **kwargs):
        return cls(api_key).publish_dataset(*args, **kwargs)

    @classmethod
    def delete_all_files_from_dataset(cls, api_key, *args, **kwargs):
        return cls(api_key).delete_all_files_from_dataset_internal(*args, **kwargs)

    @classmethod
    def check_token(cls, api_key, *args, **kwargs):
        """Check the API-Token `api_key`.

        Returns `True` if the token is correct and usable, otherwise `False`."""
        log.debug("Check token: Starts")
        
        response = cls(api_key, *args, **kwargs).create_new_dataset(return_response=True)
        persistent_id = response.json()['data']['persistentId']

        r = cls(api_key, *args, **kwargs).get_dataset(persistent_id=persistent_id, return_response=True)
        log.debug(f"Check Token: Status Code: {r.status_code}")

        # cleanup
        cls(api_key, *args, **kwargs).remove_dataset(persistent_id)

        return r.status_code == 200

    def get_user_dataverse(self):
        """This method will return a dataverse name based on the userId.
        Each time it is called it will try to create a dataverse with the name
        based on the userId. If it already exists, then it will not create a new
        dataverse, but return the existing dataverse name.

        If it is not possible to retreive a userId or create a new dataverse (for
        example in testing), then the parent dataverse name will be returned.

        Returns:
            _str: name of the dataverse
        """
        # parent dataverse at demo.dataverse.org is "demo"
        parent_dataverse = "demo"

        # get userid
        try:
            req = request.get_json(force=True)
        except:
            try:
                req = request.form.to_dict()
            except:
                req = None
        log.debug("got request data: {}".format(req))

        try:
            user_id = req.get("userId")
            email = user_id.split(":")[1].split("//")[1]
            dataverse_user = re.sub('[\W\_]', '', email)
        except:
            email = "surf@surf-rds.nl"
            dataverse_user = re.sub('[\W\_]', '', email)

        # create a dataverse inside the parent dataverse of the installation
        # If it already exist it will not be created
        dataverse = {
            "name": dataverse_user,
            "alias": dataverse_user,
            "dataverseContacts": [
                {
                "contactEmail": email
                }
            ],
            "affiliation": "Surf Research Drive",
            "description": "This dataverse has been created as part of the automated transfer of data from Surf Research Drive",
            "dataverseType": "UNCATEGORIZED"
            }
        headers = {
            'X-Dataverse-key': self.api_key,
            'Content-Type': 'application/json'
        }
        url = f"{self.dataverse_api_address}/dataverses/{parent_dataverse}"
        payload = json.dumps(dataverse)
        r = requests.request("POST", url, headers=headers, data=payload)
        print(r.json())
        return dataverse_user


    def get_persistent_id_with_id(self, id: int):
        """Will get the persistent_id of a dataset.

        Args:
            id (int): id of a dataset

        Returns:
            str: persistent_id of the dataset
        """
        headers = {
            'X-Dataverse-key': self.api_key,
            'Content-Type': 'application/json'
        }
        
        url = f"{self.dataverse_api_address}/datasets/{id}/"
        
        r = requests.request("GET", url, headers=headers)

        persistent_id = r.json()["data"]["latestVersion"]["datasetPersistentId"]
        
        return persistent_id


    def get_latest_persistent_id(self):
        """Gets the persistent_id of the latest dataset created

        Returns:
            str: persistent_id of the dataset
        """

        headers = {
            'X-Dataverse-key': self.api_key,
            'Content-Type': 'application/json'
        }
        
        user_dataverse = self.get_user_dataverse()
        
        url = f"{self.dataverse_api_address}/dataverses/{user_dataverse}/contents"
        
        r = requests.request("GET", url, headers=headers)
        
        last_dataset_id = r.json()["data"][-1]['id']

        return self.get_persistent_id_with_id(last_dataset_id)
    

    @_rate_limit(per_second=5)
    def get_dataset_internal(
        self, persistent_id: str = None, return_response: bool = False, metadataFilter: dict = None
    ):
        """
        Get dataset information of specified persistent_id.
        If no persistent_id is given then all available datasets will we returned

        Args:
            persistent_id (str, optional): The dataset id.
            return_response (bool, optional): Set to True will return the API response. Defaults to False.
            metadataFilter (dict, optional): filter for selecting what data needs to returned. Defaults to None.

        Returns:
            json: : API response if return_response is set to True
        """
        log.debug(
            f"Entering at lib/upload_dataverse.py {inspect.getframeinfo(inspect.currentframe()).function}")

        user_dataverse = self.get_user_dataverse()

        try:
            headers = {
                'X-Dataverse-key': self.api_key,
                'Content-Type': 'application/json'
            }

            if persistent_id is not None:
                url = f"{self.dataverse_api_address}/datasets/:persistentId/?persistentId={persistent_id}"
            else:
                url = f"{self.dataverse_api_address}/dataverses/{user_dataverse}/contents"
            r = requests.request("GET", url, headers=headers)
            if return_response:
                return r
            if r.status_code >= 300:
                abort(r.status_code)
            result = r.json()

            return result
    
        except Exception as e:
            log.error(
                f"Exception at lib/upload_dataverse.py {inspect.getframeinfo(inspect.currentframe()).function}")
            log.error(str(e))


    def create_new_dataset_internal(self, metadata=None, return_response=False):
        """Creates a new untitled dataset in the parent dataverse.
        If metadata is specified, it will changes metadata after creating.

        Args:
            metadata (_type_, optional): Metadata for the dataset. Defaults to None.
            return_response (bool, optional): Set to True will return the API response. Defaults to False.

        Returns:
            json: API response if return_response is set to True
        """
        log.debug(
            f"Entering at lib/upload_dataverse.py {inspect.getframeinfo(inspect.currentframe()).function}")
        log.debug("Create new dataset: Starts")

        user_dataverse = self.get_user_dataverse()

        headers = {
            'X-Dataverse-key': self.api_key,
            'Content-Type': 'application/json'
        }

        # Create a dataset as part of the parent dataverse
        url = f"{self.dataverse_api_address}/dataverses/{user_dataverse}/datasets"

        # use metadata here to set values of below variables els:
        try:
            title = metadata["metadata"]["title"]
        except:
            title = "untitled"
        try:
            authorName = metadata["metadata"]["creators"][0]["name"]
        except:
            authorName = "not set"
        try:
            authorAffiliation = metadata["metadata"]["creators"][0]["affiliations"]
        except:
            authorAffiliation = "not set"
        try:
            datasetContactEmail = metadata["metadata"]["email"]
        except:
            datasetContactEmail = "not@set.com"
        try:
            datasetContactName = metadata["metadata"]["contactname"]
        except:
            datasetContactName = "not set"
        try:
            dsDescriptionValue = metadata["metadata"]["description"]
        except:
            dsDescriptionValue = "not set"
        subject = "Other"
        dataset = {
            "datasetVersion": {
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
        }

        payload = json.dumps(dataset)
        r = requests.request("POST", url, headers=headers, data=payload)

        log.debug(
            f"Create new datasets: Status Code: {r.json()['status']}")

        return r.json() if not return_response else r

    def remove_dataset_internal(self, persistent_id, return_response=False):
        """Will remove an dataset based on it's id.

        Args:
            persistent_id (_type_): the dataset id
            return_response (bool, optional): Set to True will return the API response. Defaults to False.

        Returns:
            json: API response if return_response is set to True
        """
        log.debug(
            f"Entering at lib/upload_dataverse.py {inspect.getframeinfo(inspect.currentframe()).function}")
        headers = {
            'X-Dataverse-key': self.api_key,
            'Content-Type': 'application/json'
        }

        url = f"{self.dataverse_api_address}/datasets/:persistentId/?persistentId={persistent_id}"
        r = requests.request("DELETE", url, headers=headers)

        return r.status_code == 204 if not return_response else r

    def upload_new_file_to_dataset_internal(
        self, persistent_id: str, path_to_file: str, file=None, return_response=False, test=False
    ):
        """Uploads a file to a dataset on Dataverse.

        Args:
            persistent_id (str): id of the dataset to upload to
            path_to_file (str): path to the file to be uploaded. Example: ~/mydatapackage.csv
            file (bytes, optional): file as a bytes object. Defaults to None.
            return_response (bool, optional): Set to True will return the API response. Defaults to False.

        Returns:
            bool: Alternative: json if return_response=True
        """
        log.debug(
            f"Entering at lib/upload_dataverse.py {inspect.getframeinfo(inspect.currentframe()).function}")
               
        try:
            if not test:
                with open(path_to_file, 'wb') as ff:
                    ff.write(file.read())

            file_content = open(path_to_file, 'rb')

            files = {'file': (path_to_file.split("/")[-1], file_content)}

            params = dict()#dict(description='not set',
                      #    categories=['other'])

            params_as_json_string = json.dumps(params)

            payload = dict(jsonData=params_as_json_string)
            if persistent_id == "None" or persistent_id is None:
                persistent_id = self.get_latest_persistent_id()

            url_persistent_id = f"{self.dataverse_api_address}/datasets/:persistentId/add?persistentId={persistent_id}&key={self.api_key}"

            response = requests.post(
                url_persistent_id, data=payload, files=files)

            if return_response:
                return response
            return {"success": True}

        except Exception as e:
            log.error(
                f"Exception at lib/upload_dataverse.py {inspect.getframeinfo(inspect.currentframe()).function}")
            log.error(str(e))

    def get_files_from_dataset(self, persistent_id):
        """will get all the files metadata from the dataset requested.

        Args:
            persistent_id (int): id of the dataset

        Returns:
            json: json containing all the files from the dataset
        
        Example return value:
        ```python
            [
                {'description': 'Blue skies!',
                'label': 'dataverse.png',
                'restricted': False,
                'version': 1,
                'datasetVersionId': 225669,
                'categories': ['Jack of Hearts', 'Lily', 'Rosemary'],
                'dataFile': {'id': 2023741,
                            'persistentId': '',
                            'pidURL': '',
                            'filename': 'dataverse.png',
                            'contentType': 'image/png',
                            'filesize': 22688,
                            'description': 'Blue skies!',
                            'storageIdentifier': 's3://demo-dataverse-org:1862b5280c4-053bbecef4d5',
                            'rootDataFileId': -1,
                            'md5': 'cf3b4d8ac22c5b98898f9a0fd74c0e1f',
                            'checksum': {'type': 'MD5',
                                        'value': 'cf3b4d8ac22c5b98898f9a0fd74c0e1f'},
                            'creationDate': '2023-02-07'}
                }
            ]
        ```
        """
        log.debug(
            f"Entering at lib/upload_dataverse.py {inspect.getframeinfo(inspect.currentframe()).function}")

        response = self.get_dataset(
            persistent_id=persistent_id, return_response=True)

        if response.status_code < 300:
            return response.json()['data']['latestVersion']['files']

        return []

    def change_metadata_in_dataset_internal(
        self, persistent_id, metadata, return_response=False
    ):
        """Will update a dataset according to the provided metadata.

        Args:
            persistent_id (int): id of the dataset
            return_response (bool, optional): Set to True will return the API response. Defaults to False.
            metadata (dict): A data-dict json-like object
                ```python
                Example: data = {
                    'metadata': {
                        'title': 'My first upload',
                        'upload_type': 'poster',
                        'description': 'This is my first upload',
                        'creators': [{'name': 'Doe, John',
                                    'affiliation': 'Dataverse'}]
                    }
                }
                ```
        Returns:
            object: response of the PUT request to the datasets endpoint
        """
        log.debug(
            f"Entering at lib/upload_dataverse.py {inspect.getframeinfo(inspect.currentframe()).function}")

        headers = {
            'X-Dataverse-key': self.api_key,
            'Content-Type': 'application/json'
        }

        url = f"{self.dataverse_api_address}/datasets/:persistentId/versions/:draft?persistentId={persistent_id}"

        # use metadata here to set values of below variables els:
        try:
            title = metadata["metadata"]["title"]
        except:
            title = "untitled"
        try:
            authorName = metadata["metadata"]["creators"][0]["name"]
        except:
            authorName = "not set"
        try:
            authorAffiliation = metadata["metadata"]["creators"][0]["affiliations"]
        except:
            authorAffiliation = "not set"
        try:
            datasetContactEmail = metadata["metadata"]["email"]
        except:
            datasetContactEmail = "not@set.com"
        try:
            datasetContactName = metadata["metadata"]["contactnamee"]
        except:
            datasetContactName = "not set"
        try:
            dsDescriptionValue = metadata["metadata"]["description"]
        except:
            dsDescriptionValue = "not set"
        subject = "Other"
        dataset = {
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
        payload = json.dumps(dataset)
        r = requests.request("PUT", url, headers=headers, data=payload)
        return r.status_code == 204 if not return_response else r

    def publish_dataset_internal(self, persistent_id, return_response=False):
        """Will publish an dataset if it is not under embargo

        Args:
            persistent_id (_type_): id of the dataset
            return_response (bool, optional): Set to True will return the API response. Defaults to False.

        Returns:
            object: response of the POST request to the datasets publish endpoint
        """
        log.debug(
            f"Entering at lib/upload_dataverse.py {inspect.getframeinfo(inspect.currentframe()).function}")
        headers = {
            'X-Dataverse-key': self.api_key,
            'Content-Type': 'application/json'
        }

        url = f"{self.dataverse_api_address}/datasets/:persistentId/actions/:publish?persistentId={persistent_id}&type=major"

        r = requests.request("POST", url, headers=headers)

        return r.status_code == 204 if not return_response else r

    def delete_all_files_from_dataset_internal(self, persistent_id):
        """Will delete all files from an dataset.

        Args:
            persistent_id (int): id of an dataset

        Returns:
            bool: True if successful, False if not
        """
        log.debug(
            f"Entering at lib/upload_dataverse.py {inspect.getframeinfo(inspect.currentframe()).function}")
        for file in self.get_files_from_dataset(persistent_id):
            log.debug(f"found file: {file}")

            if not self.delete_file_from_dataset_internal(persistent_id, file["dataFile"]["id"]):
                return False

        return True

    def delete_file_from_dataset_internal(self, persistent_id, file_id):
        """Not supported
        Will delete a file from an dataset

        Args:
            persistent_id (int): id of the dataset
            file_id (int): id of file

        Returns:
            bool: True if successful, False if not
        """
        log.debug(
            f"Entering at lib/upload_dataverse.py {inspect.getframeinfo(inspect.currentframe()).function}")
        log.debug("Deletion of dataset files is not supported by the Dataverse API")

        return False


if __name__ == "__main__":
    """Below code will test the code that interfaces the uploads to dataverse
    """
    import configparser

    try:
        config = configparser.ConfigParser()
        config.read('../../local-env.ini')
    except Exception as e:
        config = None
        log.error(str(e))
    try:
        api_key = os.getenv(
            "DATAVERSE_API_KEY",
            config.get('TESTS', 'DATAVERSE_API_KEY')
        )
    except Exception as e:
        log.error(f"Could not get an api_key for testing: {str(e)}")
        log.info("Halting tests")
        os.exit()

    api_address = os.getenv(
        "DATAVERSE_API_ADDRESS",
        "https://demo.dataverse.org/api"
    )

    dataverse = Dataverse(api_key=api_key, api_address=api_address)

    print("### check token ###")
    check = dataverse.check_token(api_key)
    print(check)
    if check:

        print("### Create dataset ###")
        dataset = dataverse.create_new_dataset(return_response=True)
        print(dataset.json())

        print("### Get the persistent_id ###")
        persistent_id = dataset.json()['data']['persistentId']
        print(persistent_id)

        print("### upload a file with None as persistent_id ###")
        file_path = "../dataverse.png"
        r = dataverse.upload_new_file_to_dataset(
            persistent_id="None", path_to_file=file_path, file=None, return_response=True, test=True)
        print(r)
        print(r.text)

        print("### Get dataset info ###")
        r = dataverse.get_dataset(persistent_id=persistent_id, return_response=True)
        print(r)
        print(r.text)
        id = r.json()['data']['id']


        print("### Get dataset files ###")
        r = dataverse.get_files_from_dataset(persistent_id=persistent_id)
        print(r)

        print("### Update dataset metadata ###")
        data = {
            'metadata': {
                'title': 'My first upload',
                'upload_type': 'poster',
                'description': 'This is my first upload',
                'creators': [{'name': 'Doe, John',
                            'affiliation': 'Dataverse'}]
            }
        }
        r = dataverse.change_metadata_in_dataset_internal(
            persistent_id=persistent_id, metadata=data, return_response=True)
        print(r)
        print(r.text)

        print("### Get latest persistent_id ###")
        print(dataverse.get_latest_persistent_id())
        print(f"Should be the same as: {persistent_id}")


        print("### Get persistent_id with id ###")
        print(dataverse.get_persistent_id_with_id(id))
        print(f"Should be the same as: {persistent_id}")


        print("### Publish dataset ###")
        print("Not executed as normal users cannot remove published datasets")
        # r = dataverse.publish_dataset_internal(persistent_id, return_response=True)
        # print(r)
        # print(r.text)

        print("### Delete all files from dataset ###")
        r = dataverse.delete_all_files_from_dataset(persistent_id=persistent_id)
        print(r)

        print("### Remove dataset ###")
        r = dataverse.remove_dataset(persistent_id=persistent_id, return_response=True)
        print(r)
        print(r.text)