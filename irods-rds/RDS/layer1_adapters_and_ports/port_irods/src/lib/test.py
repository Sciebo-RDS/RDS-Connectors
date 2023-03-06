import base64

def encode_path(path):
    return base64.b64encode(bytes(path, 'utf-8')).decode('utf-8')

def decode_path(path):
    return base64.b64decode(bytes(path, 'utf-8')).decode('utf-8')

if __name__ == "__main__":
    path = "/irods/research/untitled-1234"
    print(path)
    path_new = encode_path(path)
    print(path_new)
    path_newer = decode_path(path_new)
    print(path_newer)