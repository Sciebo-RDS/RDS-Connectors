## DORIS RDS-connector

This connector is used in Sunet Drive for publishing data to a dedicated NextCloud directory and pushing RO-Crate metadata to SND/Doris.

### Describo profile

The connector will only support the dataset `name` attribute for the first release.
Right now we do not use any Describo profile as the `name` attribute is available in the UI anyway, 
but this profile for should limit and set the name as required (untested):

```json
{
    "metadata": {
        "name": "DORIS Profile",
        "description": "Just the label",
        "version": 0.1,
        "warnMissingProperty": true,
        "keywords": ["doris", "Dataset"]
    },
    "hide": {
        "Dataset": []
    },
    "layouts": {
        "Dataset": [
            {
                "name": "About",
                "description": "descriptive name for the dataset",
                "inputs": [
                    "name"
                ]
            }
        ]
    },
    "classes": {
        "Dataset": {
            "definition": "override",
            "subClassOf": [],
            "inputs": [
                {
                    "id": "https://schema.org/name",
                    "name": "name",
                    "label": "Title",
                    "help": "Use a descriptive name for the dataset",
                    "required": true,
                    "multiple": false,
                    "type": ["Text"]
                }
            ]
        }
    },
    "lookup": {
    }
}
```
